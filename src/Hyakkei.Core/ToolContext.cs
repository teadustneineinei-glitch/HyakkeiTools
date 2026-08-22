namespace Hyakkei.Core;

/// <summary>
/// 提供给工具模块的共享服务（宿主启动时装配）。
/// 模块通过它取配置与模块热键服务，避免反向依赖 App。
/// </summary>
public static class ToolContext
{
    public static ConfigService Config { get; set; } = null!;

    /// <summary>模块通用热键服务：仅在模块会话激活期间注册，平时不占用热键。</summary>
    public static GlobalHotkeyService ModuleHotkeys { get; set; } = null!;

    /// <summary>唤起岛（会话中的模块直达其面板）。供划词翻译等"热键→弹面板"流程使用。</summary>
    public static Action? SummonIsland { get; set; }
}
