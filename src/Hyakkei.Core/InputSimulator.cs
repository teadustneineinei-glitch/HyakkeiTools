using System.Runtime.InteropServices;

namespace Hyakkei.Core;

public enum SimMouseButton { Left, Right }

/// <summary>
/// SendInput 封装：鼠标在当前光标位置按/放/点，键盘走扫描码（游戏兼容性更好）。
/// 供连点器等模块复用。
/// </summary>
public static class InputSimulator
{
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfRightdown = 0x0008;
    private const uint MouseeventfRightup = 0x0010;
    private const uint KeyeventfExtendedkey = 0x0001;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfScancode = 0x0008;

    public static void MouseDown(SimMouseButton b)
        => SendMouse(b == SimMouseButton.Left ? MouseeventfLeftdown : MouseeventfRightdown);

    public static void MouseUp(SimMouseButton b)
        => SendMouse(b == SimMouseButton.Left ? MouseeventfLeftup : MouseeventfRightup);

    public static void MouseClick(SimMouseButton b)
    {
        MouseDown(b);
        MouseUp(b);
    }

    public static void KeyDown(ushort vk) => SendKey(vk, up: false);

    public static void KeyUp(ushort vk) => SendKey(vk, up: true);

    public static void KeyPress(ushort vk)
    {
        KeyDown(vk);
        KeyUp(vk);
    }

    private static void SendMouse(uint flags)
    {
        var input = new Input
        {
            Type = 0, // INPUT_MOUSE
            U = new InputUnion { Mi = new MouseInput { DwFlags = flags } },
        };
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void SendKey(ushort vk, bool up)
    {
        var scan = (ushort)MapVirtualKey(vk, 0); // MAPVK_VK_TO_VSC
        var flags = KeyeventfScancode;
        if (up) flags |= KeyeventfKeyup;
        if (IsExtendedKey(vk)) flags |= KeyeventfExtendedkey;

        var input = new Input
        {
            Type = 1, // INPUT_KEYBOARD
            U = new InputUnion { Ki = new KeybdInput { WScan = scan, DwFlags = flags } },
        };
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static bool IsExtendedKey(int vk) => vk is
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 // PgUp/PgDn/End/Home/方向键
        or 0x2D or 0x2E   // Insert / Delete
        or 0x5B or 0x5C   // Win
        or 0xA3 or 0xA5   // 右Ctrl / 右Alt
        or 0x6F;          // 小键盘除号

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mi;
        [FieldOffset(0)] public KeybdInput Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort WVk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
