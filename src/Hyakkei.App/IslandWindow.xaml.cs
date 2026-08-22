using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Hyakkei.Core;

namespace Hyakkei.App;

/// <summary>结果列表里的一行：模块入口，或由输入触发的快捷动作（打开链接 / 算式 / 模块快捷输入）。</summary>
public sealed class IslandChip
{
    public ITool? Tool { get; init; }
    public string Glyph { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>交给 IToolQuickInput 模块处理的输入。</summary>
    public string? QuickInput { get; init; }

    /// <summary>内置动作（打开链接、复制算式结果等）。</summary>
    public Action? Action { get; init; }

    /// <summary>F6 可复制的值（算式结果、链接地址）。</summary>
    public string? CopyValue { get; init; }

    public int Number { get; set; }
    public string NumberHint => Number is >= 1 and <= 9 ? Number.ToString() : "";

    public static IslandChip ForTool(ITool tool) => new() { Tool = tool, Glyph = tool.IconGlyph, Name = tool.Name };
}

/// <summary>
/// 岛（命令面板形态）：双击 Ctrl 在屏幕上三分之一居中弹出，跟随系统主题 + 亚克力背景。
/// 列表态 = 搜索框 + 模块/快捷动作行（↑↓ 选择 / Ctrl+数字直达 / 回车进入 / 点击进入）；
/// 展开态 = 模块极简面板。Esc 逐级返回，Alt 在面板与列表间往返，失焦即隐藏。
/// F6 = 动作键：列表态复制当前行的值；岛隐藏时有选中文字则取词唤岛填入搜索框，无选中无反应（唤起只归双击 Ctrl）；
/// 进入模块面板时 F6 移交给模块。
/// </summary>
public partial class IslandWindow
{
    private const double MaxExpandedContent = 340;
    private const double MinWindowHeight = 58;

    private readonly Dictionary<string, FrameworkElement> _islandViewCache = [];
    private readonly ForegroundWatcher _fgWatcher = new();
    private List<IslandChip> _allChips = [];
    private List<IslandChip> _visibleChips = [];
    private ITool? _expandedTool;
    private ITool? _sessionTool;
    private ITool? _lastTool; // 最近一次离开的模块面板，供 Alt 在列表里一键回去
    private bool _hiding;
    private bool _ownedForeground;
    private bool _heightAnimating;
    private bool _altTapPending;
    private int _homeHotkeyId = -1;

    public IslandWindow()
    {
        InitializeComponent();

        // 展开态内容尺寸变化（如译文出现/变长）时自动调整窗口高度
        LayoutUpdated += OnLayoutUpdatedAdjustHeight;

        // 失焦即隐：不依赖 WPF Deactivated（AttachThreadInput 抢前台后可能不触发）。
        // 规则：本次显示期间岛"曾拿到过前台"，之后前台变成别人 → 隐藏；
        // 从未拿到过（抢前台被系统拒绝）则保持可见，等用户点击岛获得前台后规则生效。
        _fgWatcher.ForegroundChanged += _ =>
        {
            if (!IsVisible || _hiding) return;
            if (WindowActivator.IsForeground(this))
                _ownedForeground = true;
            else if (_ownedForeground)
                HideIsland();
        };
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _ownedForeground = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _fgWatcher.Dispose();
        base.OnClosed(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowExStyles.Add(this, WindowExStyles.ToolWindow); // 不出现在 Alt+Tab
    }

    // ---- 显示 / 隐藏 ----

    public void Toggle()
    {
        if (IsVisible) HideIsland();
        else ShowIsland();
    }

    public void ShowIsland()
    {
        _hiding = false;
        _ownedForeground = false;
        RebuildChips();

        // 会话中的模块被停用则顺带结束会话
        if (_sessionTool is not null && App.Config.Current.DisabledTools.Contains(_sessionTool.Id))
            EndActiveSession();

        if (_sessionTool is not null)
        {
            Expand(_sessionTool); // 隐身挂载：唤起时直接回到会话模块的面板
        }
        else
        {
            SwitchToCompact(animated: false);
            SearchBox.Text = "";
        }

        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Top + wa.Height * 0.22;

        Root.Opacity = 0;
        Show();
        WindowActivator.ForceForeground(this);
        if (WindowActivator.IsForeground(this))
            _ownedForeground = true;
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);

        RunAnimation(fromOpacity: 0, toOpacity: 1, fromY: -8, toY: 0, ms: 140);
        Log.Info("岛已唤起");
    }

    public void HideIsland()
    {
        if (_hiding || !IsVisible) return;
        _hiding = true;

        // onCompleted 必须在 Storyboard.Begin 之前挂好（RunAnimation 内部保证）
        RunAnimation(fromOpacity: 1, toOpacity: 0, fromY: 0, toY: -6, ms: 110, onCompleted: () =>
        {
            if (!_hiding) return; // 隐藏动画期间又被唤起
            _hiding = false;
            Hide();
            ProcessMemory.TrimWorkingSet();
            Log.Info("岛已隐藏");
        });
    }

    // ---- 主页 F6（无模块会话时归岛） ----

    /// <summary>启用主页 F6。须在 ToolContext.ModuleHotkeys 就绪后调用。</summary>
    public void EnableHomeHotkey() => RegisterHomeHotkey();

    private void RegisterHomeHotkey()
    {
        if (_homeHotkeyId >= 0) return;
        _homeHotkeyId = ToolContext.ModuleHotkeys.Register(App.Config.Current.ModuleHotkey, OnHomeHotkey);
    }

    private void UnregisterHomeHotkey()
    {
        if (_homeHotkeyId < 0) return;
        ToolContext.ModuleHotkeys.Unregister(_homeHotkeyId);
        _homeHotkeyId = -1;
    }

    private void OnHomeHotkey()
    {
        if (IsVisible)
        {
            CopySelectedRowValue();
            return;
        }

        string? text = null;
        try
        {
            text = ClipboardCapture.CaptureSelection();
        }
        catch (Exception ex)
        {
            Log.Error("主页取词失败", ex);
        }
        if (text is null) return;

        ShowIsland();
        var single = text.ReplaceLineEndings(" ").Trim();
        SearchBox.Text = single;
        SearchBox.CaretIndex = single.Length;
    }

    // ---- 列表态 ----

    private void RebuildChips()
    {
        var disabled = App.Config.Current.DisabledTools;
        _allChips = App.Tools.Tools
            .Where(t => !disabled.Contains(t.Id))
            .Select(IslandChip.ForTool)
            .ToList();
        ApplyFilter("");
    }

    private void ApplyFilter(string text)
    {
        var query = text.Trim();
        var rows = string.IsNullOrEmpty(query)
            ? new List<IslandChip>(_allChips)
            : _allChips.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrEmpty(query))
            rows.AddRange(BuildQuickActions(query));

        for (var i = 0; i < rows.Count; i++)
            rows[i].Number = i + 1;

        _visibleChips = rows;
        ResultList.ItemsSource = _visibleChips;
        ResultList.SelectedIndex = _visibleChips.Count > 0 ? 0 : -1;
        EmptyHint.Visibility = _visibleChips.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_expandedTool is null)
            UpdateHeight(animated: false); // 过滤时即时调整，回车/点击进入时才播动画
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        Watermark.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilter(SearchBox.Text);
    }

    private void OnRowMouseUp(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IslandChip chip)
            Activate(chip);
    }

    /// <summary>执行一行：内置动作 / 模块快捷输入 / 进入模块面板。</summary>
    private void Activate(IslandChip chip)
    {
        if (chip.Action is not null)
        {
            chip.Action();
            return;
        }
        if (chip.Tool is null) return;

        Expand(chip.Tool);
        if (chip.QuickInput is not null && chip.Tool is IToolQuickInput quick)
            quick.HandleQuickInput(chip.QuickInput);
    }

    // ---- 展开态 / 会话 ----

    /// <summary>结束当前模块会话（注销其热键、停止其运行），并把 F6 交还给主页。岛隐藏不调用此方法。</summary>
    public void EndActiveSession()
    {
        (_sessionTool as IToolSession)?.OnSessionDeactivated();
        _sessionTool = null;
        RegisterHomeHotkey();
    }

    private void Expand(ITool tool)
    {
        if (!ReferenceEquals(_sessionTool, tool))
        {
            (_sessionTool as IToolSession)?.OnSessionDeactivated();
            _sessionTool = tool;
            if (tool is IToolSession session)
            {
                UnregisterHomeHotkey(); // F6 移交给模块
                session.OnSessionActivated();
            }
        }

        _expandedTool = tool;
        if (!_islandViewCache.TryGetValue(tool.Id, out var view))
        {
            view = tool.CreateIslandView();
            _islandViewCache[tool.Id] = view;
        }
        IslandHost.Content = view;
        HeaderGlyph.Text = tool.IconGlyph;
        HeaderTitle.Text = tool.Name;

        CompactPanel.Visibility = Visibility.Collapsed;
        ExpandedPanel.Visibility = Visibility.Visible;
        UpdateHeight(animated: true);
        // 不聚焦视图容器：会画出焦点框（DESIGN.md §5）；按键交互走窗口级 PreviewKeyDown
    }

    private void SwitchToCompact(bool animated)
    {
        if (_expandedTool is not null)
            _lastTool = _expandedTool;
        EndActiveSession();
        _expandedTool = null;
        ExpandedPanel.Visibility = Visibility.Collapsed;
        CompactPanel.Visibility = Visibility.Visible;
        UpdateHeight(animated);

        if (animated)
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => SwitchToCompact(animated: true);

    // ---- 尺寸 ----

    private void UpdateHeight(bool animated)
    {
        var target = Math.Max(MeasureContentHeight(), MinWindowHeight);
        if (animated)
        {
            AnimateHeight(target);
        }
        else
        {
            BeginAnimation(HeightProperty, null);
            Height = target;
        }
    }

    private double MeasureContentHeight()
    {
        var limit = new Size(Width, double.PositiveInfinity);
        if (_expandedTool is null)
        {
            CompactPanel.Measure(limit);
            return CompactPanel.DesiredSize.Height;
        }
        ExpandedPanel.Measure(limit);
        return Math.Min(ExpandedPanel.DesiredSize.Height, MaxExpandedContent);
    }

    private void AnimateHeight(double target)
    {
        _heightAnimating = true;
        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        anim.Completed += (_, _) =>
        {
            BeginAnimation(HeightProperty, null);
            Height = target;
            _heightAnimating = false;
        };
        BeginAnimation(HeightProperty, anim);
    }

    private void OnLayoutUpdatedAdjustHeight(object? sender, EventArgs e)
    {
        if (_expandedTool is null || !IsVisible || _hiding || _heightAnimating) return;
        var target = Math.Max(MeasureContentHeight(), MinWindowHeight);
        if (Math.Abs(target - Height) > 2)
            AnimateHeight(target);
    }

    // ---- 键盘 ----

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 单击 Alt（期间无其他键）：面板 → 列表；列表 → 回到最近离开的面板。按下时先压住 WPF 的菜单模式
        if (e.Key == Key.System && e.SystemKey is Key.LeftAlt or Key.RightAlt)
        {
            if (_expandedTool is not null || _lastTool is not null)
            {
                _altTapPending = true;
                e.Handled = true;
            }
            return;
        }
        _altTapPending = false;

        if (e.Key == Key.Escape)
        {
            if (_expandedTool is not null) SwitchToCompact(animated: true);
            else HideIsland();
            e.Handled = true;
            return;
        }

        if (_expandedTool is not null) return;

        switch (e.Key)
        {
            case Key.Down when _visibleChips.Count > 0:
                ResultList.SelectedIndex = (ResultList.SelectedIndex + 1) % _visibleChips.Count;
                e.Handled = true;
                return;
            case Key.Up when _visibleChips.Count > 0:
                ResultList.SelectedIndex = (ResultList.SelectedIndex - 1 + _visibleChips.Count) % _visibleChips.Count;
                e.Handled = true;
                return;
            case Key.Enter when _visibleChips.Count > 0:
                Activate(ResultList.SelectedItem as IslandChip ?? _visibleChips[0]);
                e.Handled = true;
                return;
        }

        // Ctrl+数字直达；裸数字永远是输入（万能输入可能以数字开头，如算式）
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            var index = e.Key switch
            {
                >= Key.D1 and <= Key.D9 => e.Key - Key.D1,
                >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad1,
                _ => -1,
            };
            if (index >= 0 && index < _visibleChips.Count)
            {
                Activate(_visibleChips[index]);
                e.Handled = true;
            }
        }
    }

    private void OnWindowPreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is not (Key.LeftAlt or Key.RightAlt) || !_altTapPending) return;

        _altTapPending = false;
        if (_expandedTool is not null)
        {
            SwitchToCompact(animated: true);
            e.Handled = true;
        }
        else if (_lastTool is not null && !App.Config.Current.DisabledTools.Contains(_lastTool.Id))
        {
            Expand(_lastTool);
            e.Handled = true;
        }
    }

    private void OnWindowDeactivated(object sender, EventArgs e)
    {
        _altTapPending = false;
        HideIsland();
    }

    // ---- 动画 ----

    private void RunAnimation(double fromOpacity, double toOpacity, double fromY, double toY, int ms, Action? onCompleted = null)
    {
        var sb = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(ms);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fade = new DoubleAnimation(fromOpacity, toOpacity, duration) { EasingFunction = ease };
        Storyboard.SetTarget(fade, Root);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
        sb.Children.Add(fade);

        var slide = new DoubleAnimation(fromY, toY, duration) { EasingFunction = ease };
        Storyboard.SetTarget(slide, RootTranslate);
        Storyboard.SetTargetProperty(slide, new PropertyPath(TranslateTransform.YProperty));
        sb.Children.Add(slide);

        if (onCompleted is not null)
            sb.Completed += (_, _) => onCompleted();
        sb.Begin();
    }
}
