using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Hyakkei.App.Views;
using Wpf.Ui.Appearance;

namespace Hyakkei.App;

/// <summary>侧栏导航项。</summary>
public sealed record NavEntry(string Id, string Name, string Glyph, Func<FrameworkElement> Factory);

public partial class MainWindow
{
    private readonly Dictionary<string, FrameworkElement> _viewCache = [];
    private bool _syncingSelection;
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();

        if (App.Config.Current.Theme == "System")
            SystemThemeWatcher.Watch(this);

        BuildNavigation();
    }

    private void BuildNavigation()
    {
        var items = new List<NavEntry>
        {
            new("home", "首页", "", () => new HomeView()),
            new("modules", "模块", "", () => new ModulesView()),
        };
        foreach (var tool in App.Tools.Tools)
            items.Add(new NavEntry(tool.Id, tool.Name, tool.IconGlyph, tool.CreateView));

        NavList.ItemsSource = items;
        FooterList.ItemsSource = new List<NavEntry>
        {
            new("settings", "设置", "", () => new SettingsView()),
        };

        NavList.SelectedIndex = 0;
    }

    private void OnNavSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingSelection || NavList.SelectedItem is not NavEntry entry) return;
        _syncingSelection = true;
        FooterList.SelectedIndex = -1;
        _syncingSelection = false;
        Navigate(entry);
    }

    private void OnFooterSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingSelection || FooterList.SelectedItem is not NavEntry entry) return;
        _syncingSelection = true;
        NavList.SelectedIndex = -1;
        _syncingSelection = false;
        Navigate(entry);
    }

    private void Navigate(NavEntry entry)
    {
        if (!_viewCache.TryGetValue(entry.Id, out var view))
        {
            view = entry.Factory();
            _viewCache[entry.Id] = view;
        }
        ToolHost.Content = view;
    }

    public void HideToTray()
    {
        Hide();
        Hyakkei.Core.ProcessMemory.TrimWorkingSet();
    }

    public void ShowFromTray()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Show();
        Activate();
        Focus();
    }

    public void ForceClose()
    {
        _exiting = true;
        Close();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideToTray();
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 点关闭按钮 = 隐藏到托盘；真正退出走托盘菜单
        if (!_exiting)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }
}
