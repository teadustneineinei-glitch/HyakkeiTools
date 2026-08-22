using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Hyakkei.Core;

/// <summary>
/// 低级键盘钩子（WH_KEYBOARD_LL）：检测"双击 Ctrl"。
/// 判定：Ctrl 单独按下并抬起（期间无其他键，否则视为 Ctrl+X 组合键），
/// 在间隔窗口内再次按下 Ctrl 时触发。事件在安装钩子的线程（UI 线程）上回调。
/// 该钩子基础设施后续也供连点器等模块复用。
/// </summary>
public sealed class KeyboardHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;
    private const int VkControl = 0x11; // 合成输入可能直接发通用码
    private const int VkLcontrol = 0xA2;
    private const int VkRcontrol = 0xA3;

    private enum TapState { Idle, FirstDown, Armed }

    private readonly LowLevelKeyboardProc _proc; // 持有引用防止被 GC 回收
    private readonly IntPtr _hook;
    private TapState _state;
    private long _armedAt;
    private bool _ctrlHeld;

    /// <summary>双击 Ctrl 时触发（在第二次按下的瞬间，响应最快）。</summary>
    public event Action? DoubleCtrlTapped;

    /// <summary>第一次抬起到第二次按下的最大间隔（毫秒）。</summary>
    public int TapIntervalMs { get; set; } = 400;

    public KeyboardHookService()
    {
        _proc = HookProc;
        using var module = Process.GetCurrentProcess().MainModule!;
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(module.ModuleName), 0);
        if (_hook == IntPtr.Zero)
            Log.Error("低级键盘钩子安装失败");
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode
            var msg = wParam.ToInt32();
            var isCtrl = vk is VkControl or VkLcontrol or VkRcontrol;
            var isDown = msg is WmKeydown or WmSyskeydown;
            var isUp = msg is WmKeyup or WmSyskeyup;

            if (isCtrl && isDown)
            {
                // 仅响应"新按下"。按住 Ctrl 的自动重复 KEYDOWN 必须完全忽略：
                // 否则 Ctrl+C（松开 C 后 Ctrl 仍按住）期间的重复事件会把状态机
                // 重新推到 FirstDown，紧接的 Ctrl+V 就被误判成双击。
                if (!_ctrlHeld)
                {
                    _ctrlHeld = true;
                    if (_state == TapState.Armed && Environment.TickCount64 - _armedAt <= TapIntervalMs)
                    {
                        _state = TapState.Idle;
                        DoubleCtrlTapped?.Invoke();
                    }
                    else
                    {
                        _state = TapState.FirstDown;
                    }
                }
            }
            else if (isCtrl && isUp)
            {
                _ctrlHeld = false;
                if (_state == TapState.FirstDown)
                {
                    _state = TapState.Armed;
                    _armedAt = Environment.TickCount64;
                }
            }
            else if (isDown)
            {
                // 其他任意键按下 → 取消判定（Ctrl+C 之类的组合键不算）
                _state = TapState.Idle;
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
            UnhookWindowsHookEx(_hook);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
