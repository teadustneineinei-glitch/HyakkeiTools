using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Hyakkei.Core;
using Hyakkei.Tool.AutoClicker;
using Hyakkei.Tool.Translator;
using Wpf.Ui.Appearance;

namespace Hyakkei.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private TaskbarIcon? _tray;
    private KeyboardHookService? _keyboardHook;
    private MainWindow? _mainWindow;
    private IslandWindow? _island;

    public static ConfigService Config { get; } = new();
    public static ToolRegistry Tools { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例：重复启动时改为唤出已运行实例的岛
        _mutex = new Mutex(true, @"Local\HyakkeiTools.SingleInstance", out var isNew);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\HyakkeiTools.ShowSignal");
        if (!isNew)
        {
            _showSignal.Set();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("未处理异常", args.Exception);
            args.Handled = true;
        };

        // 岛的天青配色随主题换浅/深两套画刷（含系统主题变化）
        ApplicationThemeManager.Changed += (theme, _) => ApplyIslandColors(theme);
        ApplyTheme(Config.Current.Theme);
        ApplyIslandColors(ApplicationThemeManager.GetAppTheme());

        // 注册工具模块（新增工具在此追加一行）
        ToolContext.Config = Config;
        Tools.Register(new AutoClickerTool());
        Tools.Register(new TranslatorTool());

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _island = new IslandWindow();
        ToolContext.ModuleHotkeys = new GlobalHotkeyService(_island);
        ToolContext.SummonIsland = () => _island?.ShowIsland();
        _island.EnableHomeHotkey(); // 无模块会话时 F6 归岛：取词填入搜索框

        // 双击 Ctrl 唤起 / 收起岛
        _keyboardHook = new KeyboardHookService { TapIntervalMs = Config.Current.DoubleCtrlIntervalMs };
        _keyboardHook.DoubleCtrlTapped += () => _island?.Toggle();

        CreateTrayIcon();
        ListenShowSignal();

        if (!Config.Current.FirstRunDone)
        {
            // 首次运行：打开管理中心认识一下
            Config.Current.FirstRunDone = true;
            Config.Save();
            _mainWindow.Show();
        }
        // 平时静默启动：只进托盘，不弹岛（需要时双击 Ctrl）
        Log.Info("启动完成");
    }

    private static void ApplyIslandColors(ApplicationTheme theme)
    {
        var uri = new Uri(theme == ApplicationTheme.Dark
            ? "pack://application:,,,/IslandColors.Dark.xaml"
            : "pack://application:,,,/IslandColors.Light.xaml");
        var dicts = Current.Resources.MergedDictionaries;
        for (var i = 0; i < dicts.Count; i++)
        {
            if (dicts[i].Source?.OriginalString.Contains("IslandColors") == true)
            {
                if (dicts[i].Source != uri)
                    dicts[i] = new ResourceDictionary { Source = uri };
                return;
            }
        }
    }

    public static void ApplyTheme(string theme)
    {
        switch (theme)
        {
            case "Light": ApplicationThemeManager.Apply(ApplicationTheme.Light); break;
            case "Dark": ApplicationThemeManager.Apply(ApplicationTheme.Dark); break;
            default: ApplicationThemeManager.ApplySystemTheme(); break;
        }
    }

    public void ShowManagementCenter() => _mainWindow?.ShowFromTray();

    private void CreateTrayIcon()
    {
        var menu = new ContextMenu();
        var island = new MenuItem { Header = "显示 / 隐藏岛" };
        island.Click += (_, _) => _island?.Toggle();
        var manage = new MenuItem { Header = "管理中心" };
        manage.Click += (_, _) => ShowManagementCenter();
        var exit = new MenuItem { Header = "退出" };
        exit.Click += (_, _) => ExitApp();
        menu.Items.Add(island);
        menu.Items.Add(manage);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);

        _tray = new TaskbarIcon
        {
            ToolTipText = "百景工具箱 · 双击 Ctrl 唤起",
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.ico")),
            ContextMenu = menu,
        };
        _tray.TrayLeftMouseUp += (_, _) => _island?.Toggle();
        // 纯代码构造（无 XAML 宿主）必须显式创建托盘图标；
        // 关闭 EfficiencyMode，避免进程被降到 EcoQoS 影响唤起速度
        _tray.ForceCreate(enablesEfficiencyMode: false);
    }

    /// <summary>等待第二实例的唤醒信号。</summary>
    private void ListenShowSignal()
    {
        var thread = new Thread(() =>
        {
            while (_showSignal is not null && _showSignal.WaitOne())
                Dispatcher.Invoke(() => _island?.ShowIsland());
        })
        { IsBackground = true };
        thread.Start();
    }

    public void ExitApp()
    {
        _island?.EndActiveSession(); // 释放长按中的键、注销模块热键
        Config.Save();
        _tray?.Dispose();
        _keyboardHook?.Dispose();
        ToolContext.ModuleHotkeys?.Dispose();
        _island?.Close();
        _mainWindow?.ForceClose();
        Log.Info("退出");
        Shutdown();
    }
}
