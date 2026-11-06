using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MonitoringTools.Core
{
    /// <summary>Global shortcuts, pressing keys into the app in front, finding which app that is, and a keyboard hook
    /// that only runs while snippets or the paste queue need it.</summary>
    public static class NativeInput
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags);
        [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] lpKeyState);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // ------------------------------------------------------------------ shortcuts

        /// <summary>Reads "Win+Shift+V" or "Ctrl+Alt+F5" into modifiers and a virtual key. False when it cannot be read.</summary>
        public static bool ParseShortcut(string text, out uint mods, out uint vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (var raw in text.Split('+'))
            {
                var p = raw.Trim().ToLowerInvariant();
                switch (p)
                {
                    case "ctrl": case "control": mods |= MOD_CONTROL; break;
                    case "shift": mods |= MOD_SHIFT; break;
                    case "alt": mods |= MOD_ALT; break;
                    case "win": case "windows": mods |= MOD_WIN; break;
                    default:
                        if (p.Length == 1 && char.IsLetterOrDigit(p[0])) vk = char.ToUpperInvariant(p[0]);
                        else if (p.Length >= 2 && p[0] == 'f' && int.TryParse(p.Substring(1), out var f) && f >= 1 && f <= 24) vk = (uint)(0x6F + f);
                        else if (p == "space") vk = 0x20;
                        else if (p == "insert" || p == "ins") vk = 0x2D;
                        else return false;
                        break;
                }
            }
            return vk != 0 && mods != 0;
        }

        // ------------------------------------------------------------------ which app

        /// <summary>The process name (like "msedge") of the window in front, or null.</summary>
        public static string ForegroundApp() => ProcessOf(GetForegroundWindow());

        /// <summary>The process that put the current data on the clipboard, falling back to the window in front.</summary>
        public static string ClipboardApp() => ProcessOf(GetClipboardOwner()) ?? ForegroundApp();

        public static string ProcessOf(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;
            try
            {
                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == 0 || pid == Environment.ProcessId) return pid == Environment.ProcessId ? "MonitoringTools" : null;
                using var p = Process.GetProcessById((int)pid);
                return p.ProcessName;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------ pressing keys

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion u; }
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        private const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;
        /// <summary>Marks keys this app presses, so the keyboard hook ignores its own typing.</summary>
        private static readonly IntPtr OwnMark = new IntPtr(0x4D54);

        public const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_V = 0x56;

        private static INPUT Key(ushort vk, bool up) => new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0, dwExtraInfo = OwnMark } } };
        private static INPUT Unicode(char c, bool up) => new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0), dwExtraInfo = OwnMark } } };

        private static void Send(List<INPUT> list) { if (list.Count > 0) SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf<INPUT>()); }

        /// <summary>Lets go of modifier keys the person is still holding from a shortcut, so a pasted Ctrl+V is not Ctrl+Shift+V.</summary>
        public static void ReleaseModifiers()
        {
            var list = new List<INPUT>();
            foreach (var vk in new[] { VK_SHIFT, VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN })
                if ((GetAsyncKeyState(vk) & 0x8000) != 0) list.Add(Key(vk, true));
            Send(list);
        }

        public static void PressCtrlV()
        {
            ReleaseModifiers();
            Send(new List<INPUT> { Key(VK_CONTROL, false), Key(VK_V, false), Key(VK_V, true), Key(VK_CONTROL, true) });
        }

        public static void Press(ushort vk, int times = 1)
        {
            var list = new List<INPUT>();
            for (int i = 0; i < times; i++) { list.Add(Key(vk, false)); list.Add(Key(vk, true)); }
            Send(list);
        }

        /// <summary>Types text as key presses, for apps that block pasting. New lines are typed as Enter.</summary>
        public static void TypeText(string text)
        {
            ReleaseModifiers();
            var list = new List<INPUT>();
            foreach (var c in text.Replace("\r\n", "\n"))
            {
                if (c == '\n') { list.Add(Key(VK_RETURN, false)); list.Add(Key(VK_RETURN, true)); }
                else if (c == '\t') { list.Add(Key(VK_TAB, false)); list.Add(Key(VK_TAB, true)); }
                else { list.Add(Unicode(c, false)); list.Add(Unicode(c, true)); }
                if (list.Count >= 200) { Send(list); list.Clear(); System.Threading.Thread.Sleep(5); }
            }
            Send(list);
        }

        // ------------------------------------------------------------------ click-through windows

        public static void SetClickThrough(IntPtr hwnd, bool on)
        {
            const int GWL_EXSTYLE = -20; const long WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;
            var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            ex = on ? ex | WS_EX_TRANSPARENT | WS_EX_LAYERED : ex & ~WS_EX_TRANSPARENT;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
        }

        // ------------------------------------------------------------------ keyboard hook

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        /// <summary>Watches key presses in every app. Only typed characters and Ctrl+V reach the listeners; nothing is kept.</summary>
        public sealed class KeyboardHook : IDisposable
        {
            private IntPtr _hook;
            private readonly LowLevelKeyboardProc _proc;
            /// <summary>A character the person typed, or '\b' for Backspace and '\0' for a key that breaks a word (arrows, Enter, clicks).</summary>
            public event Action<char> Typed;
            /// <summary>Ctrl+V was released.</summary>
            public event Action Pasted;

            public KeyboardHook()
            {
                _proc = Proc;
                _hook = SetWindowsHookEx(13, _proc, GetModuleHandle(null), 0);
                if (_hook == IntPtr.Zero) Log.Warn("Keyboard hook not set: " + Marshal.GetLastWin32Error());
            }

            private IntPtr Proc(int nCode, IntPtr wParam, IntPtr lParam)
            {
                if (nCode >= 0)
                {
                    try
                    {
                        var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                        if (k.dwExtraInfo != OwnMark)
                        {
                            int msg = wParam.ToInt32();
                            bool down = msg == 0x100 || msg == 0x104, up = msg == 0x101 || msg == 0x105;
                            bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                            if (up && k.vkCode == VK_V && ctrl) Pasted?.Invoke();
                            else if (down && !ctrl && (GetAsyncKeyState(VK_MENU) & 0x8000) == 0)
                            {
                                if (k.vkCode == VK_BACK) Typed?.Invoke('\b');
                                else
                                {
                                    var ch = CharFor(k.vkCode, k.scanCode);
                                    Typed?.Invoke(ch);
                                }
                            }
                        }
                    }
                    catch { }
                }
                return CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            private static char CharFor(uint vk, uint scan)
            {
                var state = new byte[256];
                if (!GetKeyboardState(state)) return '\0';
                if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) state[VK_SHIFT] = 0x80;
                var sb = new StringBuilder(4);
                int n = ToUnicode(vk, scan, state, sb, sb.Capacity, 4);
                return n == 1 && !char.IsControl(sb[0]) ? sb[0] : '\0';
            }

            public void Dispose()
            {
                if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            }
        }
    }
}
