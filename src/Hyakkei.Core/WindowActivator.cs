using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Hyakkei.Core;

/// <summary>
/// 强制把窗口带到前台。后台进程直接调 Activate() 会被 Windows 前台锁拦截，
/// 启动器类软件通用做法：把当前线程与前台窗口线程 AttachThreadInput 后再 SetForegroundWindow。
/// </summary>
public static class WindowActivator
{
    /// <summary>窗口当前是否为系统前台窗口。</summary>
    public static bool IsForeground(Window window)
        => GetForegroundWindow() == new WindowInteropHelper(window).Handle;

    public static void ForceForeground(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var fgWnd = GetForegroundWindow();
        var ourThread = GetCurrentThreadId();
        uint fgThread = 0;
        if (fgWnd != IntPtr.Zero)
            fgThread = GetWindowThreadProcessId(fgWnd, out _);

        if (fgThread != 0 && fgThread != ourThread)
        {
            AttachThreadInput(fgThread, ourThread, true);
            try
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
                SetFocus(hwnd);
            }
            finally
            {
                AttachThreadInput(fgThread, ourThread, false);
            }
        }
        else
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }

        window.Activate();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
