using System.Runtime.InteropServices;

namespace Hyakkei.Core;

/// <summary>进程内存辅助：窗口隐藏到托盘后收缩工作集，降低常驻占用。</summary>
public static class ProcessMemory
{
    /// <summary>把可换出的页移出工作集。页面进入待机列表，再次唤起时按需快速取回。</summary>
    public static void TrimWorkingSet()
    {
        try
        {
            EmptyWorkingSet(GetCurrentProcess());
        }
        catch
        {
            // 收缩失败无碍
        }
    }

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
}
