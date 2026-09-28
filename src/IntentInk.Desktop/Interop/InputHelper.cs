using System.Runtime.InteropServices;
using System.Windows;
using IntentInk.Desktop.Infrastructure;
using Clipboard = System.Windows.Clipboard;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using IDataObject = System.Windows.IDataObject;

namespace IntentInk.Desktop.Interop;

/// <summary>
/// Captures and replaces the active selection using standard copy and paste shortcuts.
/// Clipboard contents are restored after each operation.
/// </summary>
public static partial class InputHelper
{
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_C = 0x43;
    private const byte VK_V = 0x56;

    public static volatile bool IsSimulating;

    [LibraryImport("user32.dll", EntryPoint = "keybd_event")]
    private static partial void KeybdEvent(byte virtualKey, byte scanCode, uint flags, nuint extraInfo);

    public static async Task<string?> GetSelectedTextAsync(nint targetHwnd = default)
    {
        if (!await EnsureTargetIsForegroundAsync(targetHwnd))
        {
            DiagnosticsLogger.LogWarning("Selection", "Could not focus the application containing the selection.");
            return null;
        }

        if (!TrySnapshotClipboard(out IDataObject? previousClipboard))
        {
            DiagnosticsLogger.LogWarning("Selection", "The clipboard is busy; selected text could not be captured safely.");
            return null;
        }

        try
        {
            if (!TryClearClipboard()) return null;

            SendShortcut(VK_C);

            // Copy completion is asynchronous in some Electron and browser controls.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                await Task.Delay(40);
                string? selectedText = TryGetClipboardText();
                if (!string.IsNullOrWhiteSpace(selectedText) && selectedText.Trim().Length >= 2)
                {
                    DiagnosticsLogger.LogInfo("Selection", $"Captured {selectedText.Length} chars via Ctrl+C.");
                    return selectedText;
                }
            }

            DiagnosticsLogger.LogWarning("Selection", "The target application did not copy readable selected text.");
            return null;
        }
        finally
        {
            RestoreClipboard(previousClipboard);
        }
    }

    public static async Task<bool> ReplaceSelectionAsync(string correctedText, nint targetHwnd = default)
    {
        if (string.IsNullOrEmpty(correctedText)) return false;

        if (!await EnsureTargetIsForegroundAsync(targetHwnd))
        {
            DiagnosticsLogger.LogWarning("Replace", "Could not focus the target application.");
            return false;
        }

        if (!TrySnapshotClipboard(out IDataObject? previousClipboard))
        {
            DiagnosticsLogger.LogWarning("Replace", "The clipboard is busy; replacement was cancelled.");
            return false;
        }

        try
        {
            if (!TrySetClipboardText(correctedText)) return false;

            await Task.Delay(40);
            SendShortcut(VK_V);
            await Task.Delay(120);

            DiagnosticsLogger.LogSuccess("Replace", $"Replaced selection via Ctrl+V ({correctedText.Length} chars).");
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticsLogger.LogError("InputHelper.ReplaceSelectionAsync", ex);
            return false;
        }
        finally
        {
            RestoreClipboard(previousClipboard);
        }
    }

    private static async Task<bool> EnsureTargetIsForegroundAsync(nint targetHwnd)
    {
        if (targetHwnd == nint.Zero || Win32.GetForegroundWindow() == targetHwnd) return true;

        Win32.SetForegroundWindow(targetHwnd);
        await Task.Delay(50);
        return Win32.GetForegroundWindow() == targetHwnd;
    }

    private static void SendShortcut(byte key)
    {
        IsSimulating = true;
        try
        {
            KeybdEvent(VK_CONTROL, 0, 0, 0);
            KeybdEvent(key, 0, 0, 0);
            KeybdEvent(key, 0, KEYEVENTF_KEYUP, 0);
            KeybdEvent(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
        }
        finally
        {
            IsSimulating = false;
        }
    }

    private static bool TrySnapshotClipboard(out IDataObject? snapshot)
    {
        snapshot = null;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                IDataObject? current = Clipboard.GetDataObject();
                if (current == null) return true;

                string[] formats = current.GetFormats(autoConvert: false);
                var copy = new DataObject();
                int copiedFormats = 0;

                foreach (string format in formats)
                {
                    try
                    {
                        object? data = current.GetData(format, autoConvert: false);
                        if (data == null) continue;

                        copy.SetData(format, data);
                        copiedFormats++;
                    }
                    catch
                    {
                        // A clipboard owner may expose a format it cannot render now.
                    }
                }

                if (formats.Length > 0 && copiedFormats == 0) return false;

                snapshot = copiedFormats > 0 ? copy : null;
                return true;
            }
            catch
            {
                Thread.Sleep(25);
            }
        }

        return false;
    }

    private static bool TryClearClipboard()
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                Clipboard.Clear();
                return true;
            }
            catch
            {
                Thread.Sleep(25);
            }
        }

        return false;
    }

    private static string? TryGetClipboardText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySetClipboardText(string text)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                var data = new DataObject();
                data.SetData(DataFormats.UnicodeText, text);
                data.SetData("CanIncludeInClipboardHistory", 0);
                data.SetData("CanUploadToCloudClipboard", 0);
                Clipboard.SetDataObject(data, copy: false);
                return true;
            }
            catch
            {
                Thread.Sleep(25);
            }
        }

        return false;
    }

    private static void RestoreClipboard(IDataObject? previousClipboard)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                if (previousClipboard == null)
                {
                    Clipboard.Clear();
                }
                else
                {
                    Clipboard.SetDataObject(previousClipboard, copy: true);
                }

                DiagnosticsLogger.LogInfo("Clipboard", "Restored previous clipboard content.");
                return;
            }
            catch
            {
                Thread.Sleep(25);
            }
        }

        DiagnosticsLogger.LogWarning("Clipboard", "Could not restore the previous clipboard content.");
    }
}
