using System.Diagnostics;
using System.Runtime.InteropServices;
using IntentInk.Core.Models;
using IntentInk.Desktop.Infrastructure;

namespace IntentInk.Desktop.Interop;

/// <summary>
/// Low-level global hook capturing text selections across any Windows application
/// (via mouse drag, double/triple click word selection, Shift+Arrows, or Ctrl+A).
/// Automatically dismisses when typing normally or clicking away.
/// </summary>
public sealed class GlobalSelectionHook : IDisposable
{
    private nint _mouseHookId = nint.Zero;
    private nint _keyboardHookId = nint.Zero;
    private readonly Win32.HookProc _mouseProc;
    private readonly Win32.HookProc _keyboardProc;
    private readonly SettingsStore _settings;
    private readonly Func<int, int, bool>? _isOverOverlay;

    private Win32.POINT _lastMouseDownPt;
    private Win32.POINT _lastMouseUpPt;
    private DateTimeOffset _lastMouseUpTime = DateTimeOffset.MinValue;
    private readonly uint _doubleClickTime;

    private CancellationTokenSource? _keyboardDebounceCts;

    public event Action<ScreenRect, nint, string>? SelectionDetected;
    public event Action? SelectionCleared;

    public GlobalSelectionHook(SettingsStore settings, Func<int, int, bool>? isOverOverlay = null)
    {
        _settings = settings;
        _isOverOverlay = isOverOverlay;
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;

        uint dcTime = Win32.GetDoubleClickTime();
        _doubleClickTime = dcTime > 0 ? dcTime : 500;
    }

    public void Start()
    {
        if (_mouseHookId != nint.Zero || _keyboardHookId != nint.Zero) return;

        try
        {
            var moduleHandle = Win32.GetModuleHandle(nint.Zero);

            _mouseHookId = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
            if (_mouseHookId != nint.Zero)
            {
                DiagnosticsLogger.Log("GlobalSelectionHook", "Global mouse selection hook installed.");
            }

            _keyboardHookId = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
            if (_keyboardHookId != nint.Zero)
            {
                DiagnosticsLogger.Log("GlobalSelectionHook", "Global keyboard selection hook installed.");
            }
        }
        catch (Exception ex)
        {
            DiagnosticsLogger.LogError("GlobalSelectionHook.Start", ex);
        }
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && !_settings.Current.IsPaused && !InputHelper.IsSimulating)
        {
            var hook = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);

            // 1. Direct bounding box check on IntentInk's active overlay window
            if (_isOverOverlay != null && _isOverOverlay(hook.pt.X, hook.pt.Y))
            {
                // Interaction is directly on IntentInk UI -> do not clear selection or dismiss
                return Win32.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            }

            // 2. Fallback HWND process check
            var clickedHwnd = Win32.WindowFromPoint(hook.pt);
            if (clickedHwnd != nint.Zero)
            {
                Win32.GetWindowThreadProcessId(clickedHwnd, out uint clickedProcId);
                if (clickedProcId == (uint)Environment.ProcessId)
                {
                    return Win32.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
                }
            }

            if (wParam == Win32.WM_LBUTTONDOWN)
            {
                _lastMouseDownPt = hook.pt;
            }
            else if (wParam == Win32.WM_LBUTTONUP)
            {
                var now = DateTimeOffset.UtcNow;
                int dx = Math.Abs(hook.pt.X - _lastMouseDownPt.X);
                int dy = Math.Abs(hook.pt.Y - _lastMouseDownPt.Y);

                bool isDrag = (dx > 8 || dy > 8);
                bool isDoubleClick = (now - _lastMouseUpTime).TotalMilliseconds <= _doubleClickTime
                                     && Math.Abs(hook.pt.X - _lastMouseUpPt.X) < 12
                                     && Math.Abs(hook.pt.Y - _lastMouseUpPt.Y) < 12;

                _lastMouseUpTime = now;
                _lastMouseUpPt = hook.pt;

                if (isDrag || isDoubleClick)
                {
                    // Text selection occurred via mouse drag or double/triple click!
                    _ = Task.Run(async () =>
                    {
                        // 60ms delay allows target application to finish painting/updating selection
                        await Task.Delay(60);
                        TriggerSelectionDetected(hook.pt);
                    });
                }
                else
                {
                    // Single click without drag: user clicked to move cursor or deselect
                    SelectionCleared?.Invoke();
                }
            }
        }

        return Win32.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (wParam == Win32.WM_KEYDOWN || wParam == Win32.WM_SYSKEYDOWN) && !_settings.Current.IsPaused && !InputHelper.IsSimulating)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            bool isShiftDown = (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0; // VK_SHIFT
            bool isCtrlDown = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;  // VK_CONTROL

            // Keyboard selection: Shift + (Arrows, Home, End, PageUp, PageDown)
            bool isShiftNav = isShiftDown && (
                (vkCode >= 0x25 && vkCode <= 0x28) || // Left, Up, Right, Down
                vkCode == 0x24 ||                     // Home
                vkCode == 0x23 ||                     // End
                vkCode == 0x21 ||                     // PageUp
                vkCode == 0x22);                      // PageDown

            bool isSelectAll = isCtrlDown && (vkCode == 0x41); // Ctrl+A

            if (isShiftNav || isSelectAll)
            {
                // Debounce keyboard selection (200ms) to allow user to complete selection stroke
                _keyboardDebounceCts?.Cancel();
                _keyboardDebounceCts?.Dispose();
                _keyboardDebounceCts = new CancellationTokenSource();
                var token = _keyboardDebounceCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(200, token);
                        if (!token.IsCancellationRequested)
                        {
                            var pt = Win32.GetCaretScreenPosition();
                            TriggerSelectionDetected(pt);
                        }
                    }
                    catch (OperationCanceledException) { }
                }, token);
            }
            else
            {
                // Normal typing clears any active selection button (ONLY when Ctrl is NOT pressed!)
                // If Ctrl is held (e.g. Ctrl+C, Ctrl+V, Ctrl+X), do NOT clear selection!
                bool isTypingOrDismissKey = !isCtrlDown && (
                    (vkCode >= 0x41 && vkCode <= 0x5A) || // A-Z without Ctrl
                    (vkCode >= 0x30 && vkCode <= 0x39) || // 0-9
                    vkCode == 0x20 ||                     // Space
                    vkCode == 0x08 ||                     // Backspace
                    vkCode == 0x0D ||                     // Enter
                    vkCode == 0x1B ||                     // Escape
                    (vkCode >= 0xBA && vkCode <= 0xE2)    // OEM punctuation
                );

                if (isTypingOrDismissKey)
                {
                    SelectionCleared?.Invoke();
                }
            }
        }

        return Win32.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private void TriggerSelectionDetected(Win32.POINT pt)
    {
        try
        {
            var fg = Win32.GetForegroundWindow();
            if (fg == nint.Zero) return;

            Win32.GetWindowThreadProcessId(fg, out uint procId);
            if (procId == (uint)Environment.ProcessId) return; // Ignore IntentInk's own windows

            string procName = "unknown";
            try
            {
                using var proc = Process.GetProcessById((int)procId);
                procName = proc.ProcessName;
                var name = procName.ToLowerInvariant();
                if (_settings.Current.ExcludedProcessNames.Any(ex =>
                    string.Equals(ex, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }
            }
            catch { }

            var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds 
                ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            int clampedX = Math.Max(10, Math.Min(pt.X, screen.Width - 60));
            int clampedY = Math.Max(10, Math.Min(pt.Y, screen.Height - 60));

            var bounds = new ScreenRect(clampedX, clampedY, 20, 20);
            DiagnosticsLogger.LogInfo("Selection", $"Selection detected in '{procName}' at ({clampedX}, {clampedY})");
            SelectionDetected?.Invoke(bounds, fg, procName);
        }
        catch (Exception ex)
        {
            DiagnosticsLogger.LogError("GlobalSelectionHook.TriggerSelectionDetected", ex);
        }
    }

    public void Dispose()
    {
        if (_mouseHookId != nint.Zero)
        {
            Win32.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = nint.Zero;
        }

        if (_keyboardHookId != nint.Zero)
        {
            Win32.UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = nint.Zero;
        }

        _keyboardDebounceCts?.Cancel();
        _keyboardDebounceCts?.Dispose();
        _keyboardDebounceCts = null;

        DiagnosticsLogger.Log("GlobalSelectionHook", "Global selection hooks uninstalled.");
    }
}
