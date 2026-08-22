using System.Windows;
using Hyakkei.Core;

namespace Hyakkei.Tool.AutoClicker;

/// <summary>
/// 连点器：鼠标/键盘 连点与长按。实现 IToolSession——进入岛面板时挂载模块热键，
/// 岛隐藏不解除（游戏里照常可用），Esc 退出面板即解除并停止。
/// </summary>
public sealed class AutoClickerTool : ITool, IToolSession
{
    public const string ToolId = "auto-clicker";

    private int _hotkeyId = -1;

    public AutoClickerTool()
    {
        Settings = ToolContext.Config.GetToolConfig<ClickerSettings>(ToolId);
    }

    public ClickerEngine Engine { get; } = new();

    public ClickerSettings Settings { get; }

    public string Id => ToolId;
    public string Name => "连点器";
    public string Description => "鼠标 / 键盘 · 连点与长按";
    public string IconGlyph => "";

    public FrameworkElement CreateView() => new ClickerInfoView();
    public FrameworkElement CreateIslandView() => new ClickerIslandView(this);

    public void OnSessionActivated()
    {
        var gesture = ToolContext.Config.Current.ModuleHotkey;
        _hotkeyId = ToolContext.ModuleHotkeys.Register(gesture, () => Engine.Toggle(Settings));
        Log.Info(_hotkeyId >= 0 ? $"连点器热键已挂载：{gesture}" : $"连点器热键挂载失败：{gesture}");
    }

    public void OnSessionDeactivated()
    {
        if (_hotkeyId >= 0)
        {
            ToolContext.ModuleHotkeys.Unregister(_hotkeyId);
            _hotkeyId = -1;
        }
        Engine.Stop();
        Log.Info("连点器热键已解除");
    }

    public void SaveSettings() => ToolContext.Config.SetToolConfig(ToolId, Settings);
}
