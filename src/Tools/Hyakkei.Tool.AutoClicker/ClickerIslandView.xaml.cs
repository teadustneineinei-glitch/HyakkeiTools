using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hyakkei.Core;

namespace Hyakkei.Tool.AutoClicker;

public partial class ClickerIslandView : UserControl
{
    private readonly AutoClickerTool _tool;
    private bool _loading = true;
    private bool _capturing;
    private bool _justSelectedKey;
    private Window? _capWindow;

    public ClickerIslandView(AutoClickerTool tool)
    {
        _tool = tool;
        InitializeComponent();

        var s = _tool.Settings;
        LeftBtn.IsChecked = s.Target == "MouseLeft";
        RightBtn.IsChecked = s.Target == "MouseRight";
        KeyBtn.IsChecked = s.Target == "Key";
        ClickModeBtn.IsChecked = s.Mode == "Click";
        HoldModeBtn.IsChecked = s.Mode == "Hold";
        IntervalBox.Text = s.IntervalMs.ToString();
        KeySegLabel.Text = PrettyKey(s.KeyName);
        HotkeyHint.Text = ToolContext.Config.Current.ModuleHotkey;
        UpdateEnabled();

        _tool.Engine.RunningChanged += running => Dispatcher.Invoke(() => UpdateRunning(running));
        IsVisibleChanged += (_, _) => { if (!IsVisible) EndCapture(); };
        _loading = false;
    }

    private void UpdateRunning(bool running)
    {
        StartBtn.Content = running ? "停止" : "开始";
        StatusDot.Fill = running
            ? (Brush)FindResource("Island.Accent")
            : (Brush)FindResource("Island.TextTertiary");
    }

    private void UpdateEnabled()
    {
        var clickMode = ClickModeBtn.IsChecked == true;
        IntervalField.Opacity = clickMode ? 1.0 : 0.4;
        IntervalBox.IsEnabled = clickMode;
        PresetPanel.Opacity = clickMode ? 1.0 : 0.4;
        PresetPanel.IsEnabled = clickMode;
    }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string ms)
        {
            IntervalBox.Text = ms; // TextChanged 写入设置
            _tool.Settings.IntervalMs = int.Parse(ms);
            _tool.SaveSettings();
        }
    }

    private void OnTargetChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var target = LeftBtn.IsChecked == true ? "MouseLeft"
            : RightBtn.IsChecked == true ? "MouseRight" : "Key";
        if (target == "Key" && _tool.Settings.Target != "Key")
            _justSelectedKey = true; // 本次点击是"选中"，不触发捕获
        _tool.Settings.Target = target;
        _tool.SaveSettings();
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _tool.Settings.Mode = HoldModeBtn.IsChecked == true ? "Hold" : "Click";
        _tool.SaveSettings();
        UpdateEnabled();
    }

    private void OnIntervalChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (int.TryParse(IntervalBox.Text, out var ms) && ms > 0)
            _tool.Settings.IntervalMs = ms;
    }

    private void OnIntervalLostFocus(object sender, RoutedEventArgs e)
    {
        if (_tool.Settings.IntervalMs < 10) _tool.Settings.IntervalMs = 10;
        IntervalBox.Text = _tool.Settings.IntervalMs.ToString();
        _tool.SaveSettings();
    }

    private void OnStartClick(object sender, RoutedEventArgs e) => _tool.Engine.Toggle(_tool.Settings);

    // ---- 键盘段：已选中时再点一次 = 捕获新键 ----

    private void OnKeySegClick(object sender, RoutedEventArgs e)
    {
        if (_loading || _capturing) return;
        if (_justSelectedKey)
        {
            _justSelectedKey = false;
            return;
        }
        StartCapture();
    }

    private void StartCapture()
    {
        _capturing = true;
        KeySegLabel.Text = "按键…";
        _capWindow = Window.GetWindow(this);
        // 挂在窗口级（视图自身不持有焦点，避免焦点框）；handledEventsToo=true 以便 Esc 收面板时也能收尾
        _capWindow?.AddHandler(PreviewKeyDownEvent, (KeyEventHandler)OnCaptureKey, handledEventsToo: true);
    }

    private void OnCaptureKey(object sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is not Key.Escape)
        {
            _tool.Settings.KeyVk = KeyInterop.VirtualKeyFromKey(key);
            _tool.Settings.KeyName = key.ToString();
            _tool.SaveSettings();
            e.Handled = true;
        }
        EndCapture();
    }

    private void EndCapture()
    {
        if (!_capturing) return;
        _capturing = false;
        _capWindow?.RemoveHandler(PreviewKeyDownEvent, (KeyEventHandler)OnCaptureKey);
        _capWindow = null;
        KeySegLabel.Text = PrettyKey(_tool.Settings.KeyName);
    }

    private static string PrettyKey(string keyName) => keyName switch
    {
        ['D', var d] when char.IsDigit(d) => d.ToString(),
        "OemMinus" => "-",
        "OemPlus" => "+",
        _ => keyName,
    };
}
