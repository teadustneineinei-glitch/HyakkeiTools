using System.Windows;

namespace Hyakkei.Core;

/// <summary>
/// 工具模块统一契约：宿主通过此接口发现并挂载各功能模块。
/// 新增功能 = 新建一个实现 ITool 的模块项目，在 App 注册一行即可。
/// </summary>
public interface ITool
{
    /// <summary>唯一标识（用于配置存储等），例如 "auto-clicker"。</summary>
    string Id { get; }

    /// <summary>显示名称，例如 "连点器"。</summary>
    string Name { get; }

    /// <summary>一句话描述。</summary>
    string Description { get; }

    /// <summary>Segoe Fluent Icons 字形，例如 ""。</summary>
    string IconGlyph { get; }

    /// <summary>创建管理中心里的详情/设置视图。宿主负责缓存，同一工具只会调用一次。</summary>
    FrameworkElement CreateView();

    /// <summary>
    /// 创建岛内极简视图。运行在跟随系统主题的命令面板上：配色请用 Island.* 键
    /// （IslandColors.Light/Dark.xaml）；高度尽量控制在 300 以内，只放最高频操作。
    /// </summary>
    FrameworkElement CreateIslandView();
}

/// <summary>
/// 可选实现：模块会话（"隐身挂载"）。进入岛面板 = 激活（此时注册模块热键等）；
/// 岛隐藏不结束会话；Esc 返回列表 / 切换模块 / 程序退出 = 失活（注销热键、停止运行）。
/// </summary>
public interface IToolSession
{
    void OnSessionActivated();

    void OnSessionDeactivated();
}

/// <summary>
/// 可选实现：岛搜索框的"万能输入"。用户在列表态输入的文字若不是模块名，
/// 实现此接口的模块可在列表中提供一行快捷动作（如「翻译」）；选中后岛先展开该模块，再调用 Handle。
/// </summary>
public interface IToolQuickInput
{
    /// <summary>能处理该输入则返回行标签（极简，一两个词），否则 null。</summary>
    string? QuickActionLabel(string input);

    /// <summary>岛已展开本模块面板后调用。</summary>
    void HandleQuickInput(string input);
}
