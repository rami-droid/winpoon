using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int WM_HOTKEY = 0x0312;

    private const int VK_TAB = 0x09;
    private const int VK_MENU = 0x12;   // Alt
    private const int VK_SHIFT = 0x10;
    private const int VK_Q = 0x51;
    private const int VK_H = 0x48;
    private const int VK_BACK = 0x08;
    private const int VK_NUMPAD1 = 0x61;
    private const int VK_NUMPAD9 = 0x69;

    private const int MOD_ALT = 0x0001;
    private const int MOD_CONTROL = 0x0002;

    private const int GW_OWNER = 4;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;

    private const int SW_HIDE = 0;
    private const int SW_RESTORE = 9;

    private const int EXIT_HOTKEY_ID = 1;

    private static readonly object StateLock = new();
    private static readonly LowLevelKeyboardProc HookProc = KeyboardHookCallback;
    private static readonly List<string> LogHistory = new();

    private static IntPtr _hookId = IntPtr.Zero;
    private static bool _altSessionActive;
    private static readonly List<IntPtr> SwitchableWindows = new();
    private static readonly List<IntPtr> HarpoonWindows = new();
    private static int _currentIndex = -1;
    private static IntPtr _lastExternalForegroundWindow;

    private static bool _debugConsole;
    private static bool _suppressAltTab = true;

    internal static event Action<string>? LogWritten;
    internal static event Action? BookmarksChanged;

    internal static void WriteLog(string message)
    {
        Log(message);
    }

    public static int Main(string[] args)
    {
        ParseArgs(args);

        ApplicationConfiguration.Initialize();

        using var singleInstance = new Mutex(true, "WinPoon.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Log("Another WinPoon instance is already running.");
            return 0;
        }

        if (HasArg(args, "--install-startup"))
        {
            InstallStartup();
        }

        if (HasArg(args, "--remove-startup"))
        {
            RemoveStartup();
        }

        _hookId = SetHook(HookProc);
        if (_hookId == IntPtr.Zero)
        {
            Log("Failed to install keyboard hook.");
            MessageBox.Show(
                "WinPoon could not install its keyboard hook and will now exit.\n\nSee the activity log for details.",
                "WinPoon startup failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        if (!RegisterHotKey(IntPtr.Zero, EXIT_HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_Q))
        {
            Log("Failed to register Ctrl+Alt+Q exit hotkey.");
        }

        using var context = new WinPoonApplicationContext();
        Application.Run(context);

        UnhookWindowsHookEx(_hookId);
        UnregisterHotKey(IntPtr.Zero, EXIT_HOTKEY_ID);
        return 0;
    }

    internal static bool SuppressAltTab
    {
        get => _suppressAltTab;
        set => _suppressAltTab = value;
    }

    internal static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("WinPoon") is not null;
    }

    internal static IReadOnlyList<string> GetLogHistory()
    {
        lock (StateLock)
        {
            return LogHistory.ToArray();
        }
    }

    internal static void RememberExternalForegroundWindow()
    {
        var foreground = GetForegroundWindow();
        if (IsExternalWindow(foreground))
        {
            _lastExternalForegroundWindow = foreground;
        }
    }

    internal static IReadOnlyList<WindowBookmark> GetHarpoonWindows()
    {
        lock (StateLock)
        {
            RemoveInvalidHarpoonWindows();
            return HarpoonWindows
                .Select(handle => new WindowBookmark(handle, GetWindowTitle(handle)))
                .ToArray();
        }
    }

    internal static bool AddForegroundWindow()
    {
        bool added;
        lock (StateLock)
        {
            var foreground = GetForegroundWindow();
            if (!IsExternalWindow(foreground))
            {
                foreground = _lastExternalForegroundWindow;
            }

            if (!IsEligibleWindow(foreground) || HarpoonWindows.Contains(foreground))
            {
                return false;
            }

            HarpoonWindows.Add(foreground);
            added = true;
        }

        BookmarksChanged?.Invoke();
        return added;
    }

    internal static bool RemoveWindow(IntPtr handle)
    {
        bool removed;
        lock (StateLock)
        {
            removed = HarpoonWindows.Remove(handle);
        }

        if (removed)
        {
            BookmarksChanged?.Invoke();
        }

        return removed;
    }

    internal static void ClearHarpoonWindows()
    {
        lock (StateLock)
        {
            HarpoonWindows.Clear();
            SwitchableWindows.Clear();
            _currentIndex = -1;
        }

        BookmarksChanged?.Invoke();
    }

    private static void ParseArgs(string[] args)
    {
        _debugConsole = HasArg(args, "--show-console");
        _suppressAltTab = !HasArg(args, "--allow-native-alt-tab");
    }

    private static bool HasArg(string[] args, string value)
    {
        foreach (var arg in args)
        {
            if (arg.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static void InstallStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null)
            {
                Log("Could not open startup registry key.");
                return;
            }

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                Log("Could not resolve executable path for startup registration.");
                return;
            }

            var command = $"\"{exePath}\"";
            key.SetValue("WinPoon", command);
            Log("Startup registration installed.");
        }
        catch (Exception ex)
        {
            Log($"Failed to install startup registration: {ex.Message}");
        }
    }

    internal static void RemoveStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null)
            {
                Log("Could not open startup registry key.");
                return;
            }

            key.DeleteValue("WinPoon", throwOnMissingValue: false);
            Log("Startup registration removed.");
        }
        catch (Exception ex)
        {
            Log($"Failed to remove startup registration: {ex.Message}");
        }
    }

    private static void MessageLoop()
    {
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_HOTKEY && (int)msg.wParam == EXIT_HOTKEY_ID)
            {
                Log("Exit hotkey pressed. Exiting.");
                break;
            }

            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        // Passing null resolves the current executable reliably for framework-dependent
        // and single-file published builds.
        var moduleHandle = GetModuleHandle(null);
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, moduleHandle, 0);
    }

    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != HC_ACTION)
        {
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        var msg = wParam.ToInt32();
        var kbData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

        if ((msg == WM_KEYUP || msg == WM_SYSKEYUP) && kbData.vkCode == VK_MENU)
        {
            lock (StateLock)
            {
                _altSessionActive = false;
                SwitchableWindows.Clear();
                _currentIndex = -1;
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
        {
            var numpadSlot = GetNumpadSlot(kbData);
            if (kbData.vkCode >= VK_NUMPAD1 && kbData.vkCode <= VK_NUMPAD9)
            {
                numpadSlot = (int)kbData.vkCode - VK_NUMPAD1;
            }

            var modifiers = ShortcutModifiers.None;
            if (IsAltDown()) modifiers |= ShortcutModifiers.Alt;
            if (IsCtrlDown()) modifiers |= ShortcutModifiers.Control;
            if (IsShiftDown()) modifiers |= ShortcutModifiers.Shift;

            var action = KeybindManager.Resolve((int)kbData.vkCode, numpadSlot >= 0 ? numpadSlot : null, modifiers);
            if (action is not null)
            {
                switch (action.Value)
                {
                    case KeybindAction.NextWindow:
                        Log($"Shortcut {KeybindManager.Get(action.Value)} pressed.");
                        CycleWindow(false);
                        break;
                    case KeybindAction.PreviousWindow:
                        Log($"Shortcut {KeybindManager.Get(action.Value)} pressed.");
                        CycleWindow(true);
                        break;
                    case KeybindAction.PinWindow:
                        Log($"Shortcut {KeybindManager.Get(action.Value)} pressed; pin {(AddForegroundWindow() ? "succeeded" : "failed") }.");
                        break;
                    case KeybindAction.UnpinWindow:
                        Log($"Shortcut {KeybindManager.Get(action.Value)} pressed; unpin {(RemoveForegroundWindow() ? "succeeded" : "failed") }.");
                        break;
                    case KeybindAction.Exit:
                        Log($"Shortcut {KeybindManager.Get(action.Value)} pressed; exiting.");
                        PostQuitMessage(0);
                        break;
                    default:
                        var slot = (int)action.Value - (int)KeybindAction.Slot1;
                        Log($"Shortcut {KeybindManager.Get(action.Value)} pressed.");
                        ActivateHarpoonWindow(slot);
                        break;
                }

                if ((action == KeybindAction.NextWindow || action == KeybindAction.PreviousWindow) && !_suppressAltTab)
                {
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static void CycleWindow(bool reverse)
    {
        lock (StateLock)
        {
            if (!_altSessionActive)
            {
                _altSessionActive = true;
                RefreshWindowList();
                if (SwitchableWindows.Count == 0)
                {
                    return;
                }

                var foreground = GetForegroundWindow();
                _currentIndex = SwitchableWindows.IndexOf(foreground);
                if (_currentIndex < 0)
                {
                    _currentIndex = reverse ? 0 : -1;
                }
            }

            if (SwitchableWindows.Count == 0)
            {
                return;
            }

            if (reverse)
            {
                _currentIndex = (_currentIndex - 1 + SwitchableWindows.Count) % SwitchableWindows.Count;
            }
            else
            {
                _currentIndex = (_currentIndex + 1) % SwitchableWindows.Count;
            }

            var target = SwitchableWindows[_currentIndex];
            var activated = ActivateWindow(target);
            CenterMouseOnWindow(target);
            Log($"Switched to bookmarked window: {GetWindowTitle(target)} ({(activated ? "activated" : "activation failed")}).");
        }
    }

    private static void ActivateHarpoonWindow(int index)
    {
        lock (StateLock)
        {
            RemoveInvalidHarpoonWindows();
            if (index < 0 || index >= HarpoonWindows.Count)
            {
                Log($"Numpad slot {index + 1} has no bookmarked window.");
                return;
            }

            _altSessionActive = false;
            SwitchableWindows.Clear();
            _currentIndex = -1;
            var target = HarpoonWindows[index];
            var activated = ActivateWindow(target);
            CenterMouseOnWindow(HarpoonWindows[index]);
            Log($"Activated bookmarked slot {index + 1}: {GetWindowTitle(target)} ({(activated ? "activated" : "activation failed")}).");
        }
    }

    private static bool ActivateWindow(IntPtr hWnd)
    {
        var foregroundWindow = GetForegroundWindow();
        var foregroundThreadId = foregroundWindow == IntPtr.Zero
            ? 0u
            : GetWindowThreadProcessId(foregroundWindow, out _);
        var currentThreadId = GetCurrentThreadId();
        var attached = foregroundThreadId != 0 && foregroundThreadId != currentThreadId &&
            AttachThreadInput(currentThreadId, foregroundThreadId, true);

        try
        {
            // Restore only minimized windows. Calling SW_RESTORE on a maximized
            // window would unexpectedly return it to its normal size.
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            BringWindowToTop(hWnd);
            return SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    private static void CenterMouseOnWindow(IntPtr hWnd)
    {
        if (GetWindowRect(hWnd, out var rect))
        {
            var centerX = rect.Left + ((rect.Right - rect.Left) / 2);
            var centerY = rect.Top + ((rect.Bottom - rect.Top) / 2);
            _ = SetCursorPos(centerX, centerY);
        }
    }

    private static int GetNumpadSlot(KbdLlHookStruct keyboardData)
    {
        if ((keyboardData.flags & 0x01) != 0)
        {
            return -1;
        }

        return keyboardData.scanCode switch
        {
            0x4F => 0,
            0x50 => 1,
            0x51 => 2,
            0x4B => 3,
            0x4C => 4,
            0x4D => 5,
            0x47 => 6,
            0x48 => 7,
            0x49 => 8,
            _ => -1
        };
    }

    private static void RefreshWindowList()
    {
        SwitchableWindows.Clear();

        EnumWindows((hWnd, _) =>
        {
            if (HarpoonWindows.Contains(hWnd) && IsEligibleWindow(hWnd))
            {
                SwitchableWindows.Add(hWnd);
            }

            return true;
        }, IntPtr.Zero);
    }

    private static bool RemoveForegroundWindow()
    {
        bool removed;
        lock (StateLock)
        {
            removed = HarpoonWindows.Remove(GetForegroundWindow());
        }

        if (removed)
        {
            BookmarksChanged?.Invoke();
        }

        return removed;
    }

    private static void RemoveInvalidHarpoonWindows()
    {
        HarpoonWindows.RemoveAll(handle => !IsEligibleWindow(handle));
    }

    private static bool IsEligibleWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || hWnd == GetShellWindow() || !IsWindowVisible(hWnd))
        {
            return false;
        }

        if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
        {
            return false;
        }

        var exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        return (exStyle & WS_EX_TOOLWINDOW) != WS_EX_TOOLWINDOW &&
            !string.IsNullOrWhiteSpace(GetWindowTitle(hWnd));
    }

    private static bool IsExternalWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(hWnd, out var processId);
        return processId != Environment.ProcessId;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var titleLength = GetWindowTextLength(hWnd);
        if (titleLength <= 0)
        {
            return string.Empty;
        }

        var titleBuilder = new StringBuilder(titleLength + 1);
        _ = GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
        return titleBuilder.ToString().Trim();
    }

    private static bool IsAltDown()
    {
        return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
    }

    private static bool IsShiftDown()
    {
        return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
    }

    private static bool IsCtrlDown()
    {
        return (GetAsyncKeyState(0x11) & 0x8000) != 0;
    }

    private static void HideConsoleWindow()
    {
        var handle = GetConsoleWindow();
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, SW_HIDE);
        }
    }

    private static void Log(string message)
    {
        var formattedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (StateLock)
        {
            LogHistory.Add(formattedMessage);
            if (LogHistory.Count > 250)
            {
                LogHistory.RemoveAt(0);
            }
        }

        if (_debugConsole)
        {
            Console.WriteLine(formattedMessage);
        }

        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinPoon");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(Path.Combine(logDirectory, "winpoon.log"), formattedMessage + Environment.NewLine);
        }
        catch
        {
            // Logging must never prevent the keyboard hook or UI from running.
        }

        LogWritten?.Invoke(formattedMessage);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public Point pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
        {
            return GetWindowLongPtr64(hWnd, nIndex);
        }

        return new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage([In] ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage([In] ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

}
