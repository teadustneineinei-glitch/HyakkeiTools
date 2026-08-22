using System.Runtime.InteropServices;
using System.Windows;

namespace Hyakkei.Core;

/// <summary>
/// 划词取词：备份剪贴板文本 → 模拟 Ctrl+C → 等待剪贴板变化 → 读取 → 恢复。
/// 注意：仅恢复文本内容；若原剪贴板是图片等非文本会丢失（v1 取舍）。
/// </summary>
public static class ClipboardCapture
{
    private const byte VkControl = 0x11;
    private const byte VkC = 0x43;

    /// <summary>返回选中文字；没有选中/取词失败返回 null。须在 STA（UI）线程调用。</summary>
    public static string? CaptureSelection()
    {
        string? backup = null;
        try
        {
            if (Clipboard.ContainsText())
                backup = Clipboard.GetText();
        }
        catch
        {
            // 剪贴板被占用，放弃备份
        }

        var seqBefore = GetClipboardSequenceNumber();

        InputSimulator.KeyDown(VkControl);
        InputSimulator.KeyPress(VkC);
        InputSimulator.KeyUp(VkControl);

        // 等待目标应用完成复制（最多 ~450ms）
        for (var i = 0; i < 15 && GetClipboardSequenceNumber() == seqBefore; i++)
            Thread.Sleep(30);

        if (GetClipboardSequenceNumber() == seqBefore)
            return null; // 没有产生新内容（无选中）

        string? text = null;
        try
        {
            if (Clipboard.ContainsText())
                text = Clipboard.GetText();
        }
        catch (Exception ex)
        {
            Log.Error("读取剪贴板失败", ex);
        }

        if (backup is not null)
        {
            try
            {
                Clipboard.SetText(backup);
            }
            catch
            {
                // 恢复失败无碍
            }
        }

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
