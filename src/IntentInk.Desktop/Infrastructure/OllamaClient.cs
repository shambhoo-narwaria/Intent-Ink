using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using IntentInk.Core.Models;
using IntentInk.Core.Services;
using IntentInk.Core.Services.Interfaces;

namespace IntentInk.Desktop.Infrastructure;

/// <summary>
/// Implements IGrammarClient by calling a local Ollama instance with comprehensive diagnostics.
/// One shared HttpClient per instance; reuse the same OllamaClient for the app lifetime.
/// </summary>
public sealed class OllamaClient : IGrammarClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly Func<OllamaSettings> _settings;
    private readonly CorrectionValidator _validator = new();

    private const string SystemPrompt =
        "You are an expert grammar, spelling, phrasing, and sentence structure editor. " +
        "Your task is to fix spelling mistakes, grammatical errors, awkward word order, broken sentence structure, missing or trailing words, and punctuation in the input text while preserving the user's intended meaning and tone. " +
        "Specifically: " +
        "1. Fix incomplete phrasing and dangling trailing words (for example: trailing 'of the', 'to the', or dangling prepositions/articles like 'contains the object properties of the' -> 'contains the object properties.'). " +
        "2. Fix inverted or scrambled word order (e.g. 'i [name] am' -> 'I am [Name]'). " +
        "3. Ensure the sentence starts with a capital letter and ends with appropriate terminal punctuation (period, question mark, or exclamation point). " +
        "4. Preserve technical terms, code identifiers, and proper names (capitalize names appropriately). " +
        "5. Do not answer questions or add commentary; output only the corrected sentence. " +
        "6. If the text is already 100% grammatically perfect and complete, return it unchanged. " +
        "Return only an object matching the supplied schema with corrected_text.";

    private static readonly object SchemaDefinition = new
    {
        type = "object",
        properties = new { corrected_text = new { type = "string" } },
        required = new[] { "corrected_text" },
        additionalProperties = false,
    };

    public OllamaClient(Func<OllamaSettings> settings)
    {
        _settings = settings;
        var s = _settings();

        var baseUrl = s.BaseUrl.Trim();
        if (!baseUrl.EndsWith('/')) baseUrl += "/";

        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout     = TimeSpan.FromSeconds(45),
        };
    }

    /// <inheritdoc />
    public async Task<CorrectionCandidate?> CorrectAsync(
        EditorSnapshot snapshot, CancellationToken ct)
    {
        var s = _settings();

        if (string.IsNullOrWhiteSpace(snapshot.OriginalText))
        {
            DiagnosticsLogger.LogWarning("AI", "Skipped inference: input text is empty.");
            return null;
        }

        if (snapshot.OriginalText.Length > s.TextLimit)
        {
            DiagnosticsLogger.LogWarning("AI", $"Skipped inference: text length ({snapshot.OriginalText.Length}) exceeds limit ({s.TextLimit}).");
            return null;
        }

        if (string.IsNullOrWhiteSpace(s.ModelName))
        {
            DiagnosticsLogger.LogWarning("AI", "No Ollama model selected! Please open Settings and select an installed model.");
            return null;
        }

        DiagnosticsLogger.LogAi($"Calling Ollama [Model: {s.ModelName}] with {snapshot.OriginalText.Length} characters...");

        var body = new
        {
            model      = s.ModelName,
            stream     = false,
            keep_alive = "5m",
            options    = new { temperature = 0, num_predict = 1024 },
            format     = SchemaDefinition,
            messages   = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",
                      content = JsonSerializer.Serialize(
                          new { text = snapshot.OriginalText }) },
            },
        };

        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _http.PostAsJsonAsync("api/chat", body, ct);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();

            if (raw.Length > 65_536)
            {
                DiagnosticsLogger.LogError("AI", "Response rejected: payload exceeds 64KB safety limit.");
                throw new InvalidDataException("Response too large");
            }

            using var envelope = JsonDocument.Parse(raw);
            var root = envelope.RootElement;

            if (!root.GetProperty("done").GetBoolean())
            {
                DiagnosticsLogger.LogWarning("AI", "Response incomplete (done=false from Ollama).");
                throw new InvalidDataException("Incomplete response (done=false)");
            }

            if (root.TryGetProperty("done_reason", out var doneReason) &&
                doneReason.GetString() == "length")
            {
                DiagnosticsLogger.LogWarning("AI", "Response truncated due to token length limit.");
                throw new InvalidDataException("Incomplete response (length limit)");
            }

            var content = root
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "{}";

            using var result = JsonDocument.Parse(content);
            var correctedRaw = result.RootElement
                .GetProperty("corrected_text")
                .GetString();

            if (string.IsNullOrWhiteSpace(correctedRaw))
            {
                DiagnosticsLogger.LogAi("Ollama returned empty corrected_text.", LogLevel.Debug, sw.ElapsedMilliseconds);
                return null;
            }

            var candidate = _validator.Validate(snapshot, correctedRaw);
            if (candidate == null)
            {
                if (correctedRaw == snapshot.OriginalText)
                {
                    DiagnosticsLogger.LogAi($"Text is already acceptable/correct. No changes proposed.", LogLevel.Info, sw.ElapsedMilliseconds);
                    return new CorrectionCandidate
                    {
                        Source = snapshot,
                        CorrectedText = snapshot.OriginalText,
                    };
                }
                else
                {
                    DiagnosticsLogger.LogWarning("AI", $"Correction rejected by validator (edit ratio or protected token mismatch).");
                }
                return null;
            }

            DiagnosticsLogger.LogSuccess("AI", $"Correction generated ({candidate.CorrectedText.Length} characters).", sw.ElapsedMilliseconds);
            return candidate;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            DiagnosticsLogger.Log("AI", "Inference cancelled by user.", LogLevel.Debug);
            throw;
        }
        catch (OperationCanceledException)
        {
            DiagnosticsLogger.LogWarning("AI", "Inference timed out waiting for Ollama response (exceeded 45s).");
            throw new TimeoutException("Ollama inference timed out.");
        }
        catch (HttpRequestException ex)
        {
            DiagnosticsLogger.LogError("AI", $"Cannot connect to Ollama at {_http.BaseAddress}. Ensure 'ollama serve' is running.", ex);
            throw;
        }
        catch (Exception ex)
        {
            DiagnosticsLogger.LogError("AI", $"Ollama request failed: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>Returns all installed model names from the local Ollama instance.</summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync("api/tags", ct);
            using var doc = JsonDocument.Parse(json);
            var models = doc.RootElement
                      .GetProperty("models")
                      .EnumerateArray()
                      .Select(m => m.GetProperty("name").GetString() ?? "")
                      .Where(m => !string.IsNullOrEmpty(m))
                      .ToList();

            DiagnosticsLogger.LogInfo("Ollama", $"Discovered {models.Count} installed models: {string.Join(", ", models)}");
            return models;
        }
        catch (Exception ex)
        {
            DiagnosticsLogger.LogError("Ollama", $"Failed to list models: {ex.Message}", ex);
            return [];
        }
    }

    public void Dispose() => _http.Dispose();
}
