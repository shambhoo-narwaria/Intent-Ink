using System.Diagnostics;
using IntentInk.Core.Models;
using IntentInk.Core.Services.Interfaces;
using IntentInk.Desktop.Infrastructure;
using IntentInk.Desktop.Interop;

namespace IntentInk.Desktop.Services;

/// <summary>
/// Central coordinator orchestrating text selection detection, Ollama inference,
/// and safe in-place replacement of selected text.
/// </summary>
public sealed class CorrectionCoordinator : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly IGrammarClient _grammarClient;
    private readonly OverlayPresenter _overlay;
    private readonly GlobalSelectionHook _selectionHook;

    private readonly SemaphoreSlim _applyLock = new(1, 1);

    public CorrectionCoordinator(
        SettingsStore settings,
        OllamaClient ollamaClient,
        OverlayPresenter overlay)
        : this(settings, (IGrammarClient)ollamaClient, overlay)
    {
    }

    public CorrectionCoordinator(
        SettingsStore settings,
        IGrammarClient grammarClient,
        OverlayPresenter overlay)
    {
        _settings = settings;
        _grammarClient = grammarClient;
        _overlay = overlay;

        _selectionHook = new GlobalSelectionHook(_settings, _overlay.ContainsScreenPoint);
        _selectionHook.SelectionDetected += OnSelectionDetected;
        _selectionHook.SelectionCleared += OnSelectionCleared;
        _selectionHook.Start();

        DiagnosticsLogger.Log("Coordinator", "Selection-based CorrectionCoordinator active.");
    }

    private void OnSelectionDetected(ScreenRect bounds, nint targetHwnd, string appName)
    {
        if (_settings.Current.IsPaused) return;

        DiagnosticsLogger.LogInfo("Coordinator", $"Text selection detected in '{appName}'. Showing floating pill.");

        _overlay.ShowPill(
            bounds,
            appName,
            checkFunc: (ct) => CheckGrammarForSelectionAsync(targetHwnd, appName, ct),
            applyFunc: (candidate) => ReplaceSelectionAsync(targetHwnd, candidate));
    }

    private void OnSelectionCleared()
    {
        // If the user is currently viewing the Loading or Result card, do not dismiss!
        if (_overlay.IsCardOpen) return;
        _overlay.Hide();
    }

    private async Task<CorrectionCandidate?> CheckGrammarForSelectionAsync(
        nint targetHwnd,
        string appName,
        CancellationToken ct)
    {
        string? selectedText = await InputHelper.GetSelectedTextAsync(targetHwnd);

        if (string.IsNullOrWhiteSpace(selectedText) || selectedText.Trim().Length < 2)
        {
            DiagnosticsLogger.LogWarning("Selection", $"No text selected in '{appName}' (window 0x{targetHwnd:X}).");
            return null;
        }

        var snapshot = new EditorSnapshot
        {
            OriginalText = selectedText,
        };

        DiagnosticsLogger.LogInfo("Pipeline", $"Checking grammar for a {selectedText.Length}-character selection.");
        var sw = Stopwatch.StartNew();
        var candidate = await _grammarClient.CorrectAsync(snapshot, ct);
        sw.Stop();

        if (candidate != null)
        {
            DiagnosticsLogger.LogInference(
                _settings.Current.ModelName,
                snapshot.OriginalText.Length,
                candidate.CorrectedText.Length,
                sw.ElapsedMilliseconds,
                accepted: candidate.CorrectedText != snapshot.OriginalText);
        }

        return candidate;
    }

    private async Task<ApplyResult> ReplaceSelectionAsync(nint targetHwnd, CorrectionCandidate candidate)
    {
        await _applyLock.WaitAsync();
        try
        {
            DiagnosticsLogger.Log("Apply", "Applying replacement...");
            bool replaced = await InputHelper.ReplaceSelectionAsync(candidate.CorrectedText, targetHwnd);
            if (replaced)
            {
                return ApplyResult.Ok();
            }
            return ApplyResult.Fail("Could not replace text in target application.");
        }
        finally
        {
            _applyLock.Release();
        }
    }

    public void Dispose()
    {
        _selectionHook.SelectionDetected -= OnSelectionDetected;
        _selectionHook.SelectionCleared -= OnSelectionCleared;
        _selectionHook.Dispose();

        _overlay.Dispose();
        _applyLock.Dispose();
    }
}
