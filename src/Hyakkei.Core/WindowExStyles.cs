using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Hyakkei.Core;

/// <summary>窗口扩展样式（需在 SourceInitialized 之后调用，此时才有 HWND）。</summary>
public static class WindowExStyles
{
    /// <summary>不出现在 Alt+Tab 与任务栏。</summary>
    public const int ToolWindow = 0x00000080;

    /// <summary>鼠标点击穿透。</summary>
    public const int Transparent = 0x00000020;

    /// <summary>显示时不抢焦点。</summary>
    public const int NoActivate = 0x08000000;

    private const int GwlExstyle = -20;

    public static void Add(Window window, int flags)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        _ = SetWindowLong(handle, GwlExstyle, GetWindowLong(handle, GwlExstyle) | flags);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
