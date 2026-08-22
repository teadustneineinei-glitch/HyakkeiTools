using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Hyakkei.Core;

namespace Hyakkei.Tool.Translator;

public partial class TranslatorIslandView : UserControl
{
    private readonly TranslatorTool _tool;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer _copyRevert = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private CancellationTokenSource? _cts;
    private bool _loading = true;

    public TranslatorIslandView(TranslatorTool tool)
    {
        _tool = tool;
        InitializeComponent();

        AutoBtn.IsChecked = _tool.Settings.TargetMode == "Auto";
        ZhBtn.IsChecked = _tool.Settings.TargetMode == "Zh";
        EnBtn.IsChecked = _tool.Settings.TargetMode == "En";
        FrBtn.IsChecked = _tool.Settings.TargetMode == "Fr";
        HotkeyHint.Text = ToolContext.Config.Current.ModuleHotkey;

        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            _ = TranslateNowAsync();
        };
        _copyRevert.Tick += (_, _) =>
        {
            _copyRevert.Stop();
            CopyBtn.Content = "复制";
        };

        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                InputBox.Focus();
                Keyboard.Focus(InputBox);
            }
        };

        _loading = false;
    }

    /// <summary>划词入口：填入取到的文字并立即翻译。</summary>
    public void TranslateExternal(string text)
    {
        _loading = true;
        InputBox.Text = text;
        _loading = false;
        _debounce.Stop();
        _ = TranslateNowAsync();
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _debounce.Stop();
            _ = TranslateNowAsync();
            e.Handled = true;
        }
    }

    private void OnTargetChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _tool.Settings.TargetMode = ZhBtn.IsChecked == true ? "Zh"
            : EnBtn.IsChecked == true ? "En"
            : FrBtn.IsChecked == true ? "Fr" : "Auto";
        _tool.SaveSettings();
        _debounce.Stop();
        _ = TranslateNowAsync();
    }

    private bool _snipping;

    private void OnSnipClick(object sender, RoutedEventArgs e) => StartSnip();

    /// <summary>截屏翻译入口（面板按钮与热键共用）：拉框 → OCR → 选区旁就地出译文卡片。</summary>
    public async void StartSnip()
    {
        if (_snipping) return;
        _snipping = true;
        try
        {
            var host = Window.GetWindow(this);
            if (host?.IsVisible == true)
            {
                host.Hide();
                await Task.Delay(180); // 等桌面把岛的位置刷掉，避免截到自己
            }

            var overlay = new SnipOverlay();
            var ok = overlay.ShowDialog() == true && overlay.PixelSelection is not null;

            if (!ok)
                return; // 取消拉框：保持隐身，不打扰

            var r = overlay.PixelSelection!.Value;
            string text;
            using (var bmp = new System.Drawing.Bitmap(r.Width, r.Height,
                       System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(r.X, r.Y, 0, 0, new System.Drawing.Size(r.Width, r.Height));
                }
                text = await OcrService.RecognizeAsync(bmp);
            }

            // 译文就地显示在选区旁（岛保持隐藏），便于对照位置
            new SnipResultWindow(overlay.DipSelection!.Value, text, _tool).ShowAt();
        }
        catch (Exception ex)
        {
            Log.Error("截屏翻译失败", ex);
            ToolContext.SummonIsland?.Invoke();
            ResultPanel.Visibility = Visibility.Visible;
            ResultBox.Text = "识别失败";
        }
        finally
        {
            _snipping = false;
        }
    }

    /// <summary>Ctrl+1-4 切换目标语言；裸数字永远是输入。</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            var index = e.Key switch
            {
                >= Key.D1 and <= Key.D4 => e.Key - Key.D1,
                >= Key.NumPad1 and <= Key.NumPad4 => e.Key - Key.NumPad1,
                _ => -1,
            };
            if (index >= 0)
            {
                RadioButton[] modes = [AutoBtn, ZhBtn, EnBtn, FrBtn];
                modes[index].IsChecked = true;
                e.Handled = true;
                return;
            }
        }
        base.OnPreviewKeyDown(e);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ResultBox.Text)) return;
        try
        {
            Clipboard.SetText(ResultBox.Text);
            CopyBtn.Content = "已复制";
            _copyRevert.Stop();
            _copyRevert.Start();
        }
        catch (Exception ex)
        {
            Log.Error("复制译文失败", ex);
        }
    }

    private async Task TranslateNowAsync()
    {
        var text = InputBox.Text.Trim();
        _cts?.Cancel();

        if (string.IsNullOrEmpty(text))
        {
            ResultPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var cts = new CancellationTokenSource();
        _cts = cts;
        ResultPanel.Visibility = Visibility.Visible;
        ResultBox.Text = "…";

        try
        {
            var result = await _tool.TranslateAsync(text, cts.Token);
            if (!cts.IsCancellationRequested)
                ResultBox.Text = result;
        }
        catch (OperationCanceledException)
        {
            // 已被更新的输入取代
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                ResultBox.Text = "翻译失败";
                Log.Error("翻译失败", ex);
            }
        }
    }
}
