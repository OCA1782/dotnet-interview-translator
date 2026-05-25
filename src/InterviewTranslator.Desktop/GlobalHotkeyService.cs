using System.Runtime.InteropServices;
using System.Windows.Input;

namespace InterviewTranslator.Desktop;

// Win32 low-level keyboard hook — pencere odağından bağımsız çalışır
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private readonly Dictionary<Key, Action> _bindings = new();
    private IntPtr _hookHandle = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;

    public GlobalHotkeyService()
    {
        _proc = HookCallback;
    }

    public void Register(Key key, Action action) => _bindings[key] = action;

    public void Start()
    {
        using var module = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            WH_KEYBOARD_LL, _proc,
            NativeMethods.GetModuleHandle(module.ModuleName!), 0);
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == WM_KEYDOWN)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            var key = KeyInterop.KeyFromVirtualKey(vkCode);
            if (_bindings.TryGetValue(key, out var action))
                System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();

    private static class NativeMethods
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
