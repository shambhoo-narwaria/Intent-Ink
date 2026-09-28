using System.Runtime.InteropServices;

namespace IntentInk.Desktop.Interop;

public static partial class Win32
{
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW  = 0x00000080;
    public const int WS_EX_TOPMOST     = 0x00000008;
    public const int GWL_EXSTYLE       = -20;

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public nint hwndActive;
        public nint hwndFocus;
        public nint hwndCapture;
        public nint hwndMenuOwner;
        public nint hwndMoveSize;
        public nint hwndCaret;
        public RECT rcCaret;
    }

    public delegate nint HookProc(int nCode, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ClientToScreen(nint hWnd, ref POINT lpPoint);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static partial nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true)]
    public static partial nint GetModuleHandle(nint lpModuleName);

    [LibraryImport("user32.dll")]
    public static partial nint WindowFromPoint(POINT Point);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32.dll")]
    public static partial uint GetDoubleClickTime();

    public static nint GetWindowLongPtr(nint hWnd, int nIndex) =>
        GetWindowLongPtr64(hWnd, nIndex);

    public static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong) =>
        SetWindowLongPtr64(hWnd, nIndex, dwNewLong);

    /// <summary>
    /// Configures a WPF window as non-activating and toolwindow so it doesn't steal focus from target editor.
    /// </summary>
    public static void SetNonActivating(nint hwnd)
    {
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Obtains the screen coordinates of the blinking text caret across any Windows application.
    /// Falls back to cursor position if the caret cannot be determined.
    /// </summary>
    public static POINT GetCaretScreenPosition()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds 
            ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);

        var fg = GetForegroundWindow();
        if (fg != nint.Zero)
        {
            uint threadId = GetWindowThreadProcessId(fg, out _);
            var gui = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
            if (GetGUIThreadInfo(threadId, ref gui) && gui.hwndCaret != nint.Zero)
            {
                var pt = new POINT { X = gui.rcCaret.Left, Y = gui.rcCaret.Bottom };
                if (ClientToScreen(gui.hwndCaret, ref pt))
                {
                    if (pt.X >= 0 && pt.Y >= 0 && pt.X < screen.Width && pt.Y < screen.Height)
                    {
                        return pt;
                    }
                }
            }
        }

        var mouse = System.Windows.Forms.Cursor.Position;
        int clampedX = Math.Max(10, Math.Min(mouse.X, screen.Width - 50));
        int clampedY = Math.Max(10, Math.Min(mouse.Y, screen.Height - 50));
        return new POINT { X = clampedX, Y = clampedY };
    }

}
