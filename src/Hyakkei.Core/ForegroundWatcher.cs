using System.Runtime.InteropServices;

namespace Hyakkei.Core;

/// <summary>
/// 系统级前台窗口变化监听（EVENT_SYSTEM_FOREGROUND）。
/// 用途：岛的"失焦即隐"。WPF 的 Deactivated 依赖窗口曾被 WPF 认定激活，
/// 而 AttachThreadInput 抢来的前台有时不触发该状态，导致点击外部不隐藏；
/// 前台变化事件不依赖自身激活状态，稳定可靠。
/// 必须在有消息循环的线程（UI 线程）上创建，回调也在该线程。
/// </summary>
public sealed class ForegroundWatcher : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WineventOutofcontext = 0x0000;

    private readonly WinEventDelegate _proc; // 持有引用防止被 GC 回收
    private readonly IntPtr _hook;

    /// <summary>前台窗口变化，参数为新前台窗口句柄。</summary>
    public event Action<IntPtr>? ForegroundChanged;

    public ForegroundWatcher()
    {
        _proc = OnEvent;
        _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground,
            IntPtr.Zero, _proc, 0, 0, WineventOutofcontext);
        if (_hook == IntPtr.Zero)
            Log.Error("前台监听安装失败");
    }

    private void OnEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        => ForegroundChanged?.Invoke(hwnd);

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
            UnhookWinEvent(_hook);
    }

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint ev, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
}
