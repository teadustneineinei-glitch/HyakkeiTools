using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Hyakkei.Core;

/// <summary>
/// 全局热键注册（Win32 RegisterHotKey）。挂在窗口句柄上，窗口隐藏时依然生效。
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly IntPtr _handle;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _callbacks = [];
    private int _nextId = 0xB100;

    public GlobalHotkeyService(Window window)
    {
        _handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle)!;
        _source.AddHook(WndProc);
    }

    /// <summary>注册热键，如 "F6"、"Ctrl+Shift+K"。返回热键 id（用于注销）；-1 表示格式错误或已被其他程序占用。</summary>
    public int Register(string gesture, Action callback)
    {
        if (!TryParse(gesture, out var modifiers, out var vk))
        {
            Log.Error($"热键格式无法解析：{gesture}");
            return -1;
        }

        var id = _nextId++;
        if (!RegisterHotKey(_handle, id, modifiers | ModNoRepeat, vk))
        {
            Log.Error($"热键注册失败（可能被其他程序占用）：{gesture}");
            return -1;
        }

        _callbacks[id] = callback;
        return id;
    }

    /// <summary>注销单个热键。</summary>
    public void Unregister(int id)
    {
        if (_callbacks.Remove(id))
            UnregisterHotKey(_handle, id);
    }

    public static bool TryParse(string gesture, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        foreach (var raw in gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= 0x2; break;
                case "alt": modifiers |= 0x1; break;
                case "shift": modifiers |= 0x4; break;
                case "win": modifiers |= 0x8; break;
                default:
                    try
                    {
                        var key = (Key)new KeyConverter().ConvertFromInvariantString(raw)!;
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    }
                    catch
                    {
                        return false;
                    }
                    break;
            }
        }
        return vk != 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _callbacks.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _callbacks.Keys)
            UnregisterHotKey(_handle, id);
        _callbacks.Clear();
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
