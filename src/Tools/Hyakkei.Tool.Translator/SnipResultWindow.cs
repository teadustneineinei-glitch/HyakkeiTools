using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Hyakkei.Core;

namespace Hyakkei.Tool.Translator;

/// <summary>
/// 截屏翻译结果卡片：贴在选区下方（放不下则上方）就地显示译文 + 识别原文，
/// 同时用 <see cref="RegionOutlineWindow"/> 给选区描一圈天青边以确认位置。
/// 卡片内可切换目标语言（分段或数字键 1-4）并重译；原文/译文各有复制按钮（原文 = OCR 结果，兼作文字识别）。
/// Esc / 失焦 / 点击外部即关闭；空白处可拖动。岛保持隐藏，不打断当前工作。
/// </summary>
public sealed class SnipResultWindow : Window
{
    private readonly RegionOutlineWindow _outline;
    private readonly Rect _region;
    private readonly TranslatorTool _tool;
    private readonly string _ocrText;
    private readonly SelectableText _result;
    private readonly RadioButton[] _modes;
    private readonly DispatcherTimer _copyRevert = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private Button? _lastCopied;
    private string _lastCopiedLabel = "";
    private CancellationTokenSource? _cts;
    private bool _loading = true;

    public SnipResultWindow(Rect regionDip, string ocrText, TranslatorTool tool)
    {
        _region = regionDip;
        _tool = tool;
        _ocrText = ocrText;
        _outline = new RegionOutlineWindow(regionDip);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = true;
        Width = Math.Clamp(regionDip.Width + 24, 480, 600); // 舒适阅读宽度；+24 = 阴影余量
        Left = regionDip.Left - 12;
        Top = regionDip.Bottom + 6;

        var hasText = !string.IsNullOrWhiteSpace(ocrText);

        _result = MakeText(15, 24, "Island.TextPrimary", 240);
        _result.Text = hasText ? "…" : "未识别到文字";

        var source = MakeText(13, 20, "Island.TextSecondary", 120);
        source.Text = ocrText;

        var hairline = new Rectangle { Height = 1, Margin = new Thickness(0, 14, 0, 12) };
        hairline.SetResourceReference(Shape.FillProperty, "Island.Hairline");

        // 目标语言分段（与面板共用 Settings.TargetMode）
        _modes = [MakeMode("自动", "Auto"), MakeMode("中", "Zh"), MakeMode("EN", "En"), MakeMode("FR", "Fr")];
        var track = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(2), VerticalAlignment = VerticalAlignment.Center };
        track.SetResourceReference(Border.BackgroundProperty, "Island.TrackWash");
        var trackPanel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var m in _modes) trackPanel.Children.Add(m);
        track.Child = trackPanel;

        var copySource = MakeButton("复制原文", "Island.Button", () => source.Text);
        copySource.Margin = new Thickness(0, 0, 8, 0);
        var copyResult = MakeButton("复制译文", "Island.PrimaryButton", () => _result.Text);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(track, 0);
        Grid.SetColumn(copySource, 2);
        Grid.SetColumn(copyResult, 3);
        footer.Children.Add(track);
        footer.Children.Add(copySource);
        footer.Children.Add(copyResult);

        var stack = new StackPanel();
        if (hasText) stack.Children.Add(MakeCaption("译文"));
        stack.Children.Add(_result);
        if (hasText)
        {
            stack.Children.Add(hairline);
            stack.Children.Add(MakeCaption("原文"));
            stack.Children.Add(source);
            footer.Margin = new Thickness(0, 14, 0, 0);
            stack.Children.Add(footer);
        }

        var card = new Border
        {
            Margin = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 14, 18, 14),
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = 0.25, Color = Colors.Black },
            Child = stack,
        };
        card.SetResourceReference(Border.BackgroundProperty, "Island.Bg");
        card.SetResourceReference(Border.BorderBrushProperty, "Island.Hairline");
        // 无边框窗口：按住空白处拖动（文字框/按钮自行处理鼠标，不会进到这里）
        card.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        };
        Content = card;

        _copyRevert.Tick += (_, _) =>
        {
            _copyRevert.Stop();
            if (_lastCopied is not null) _lastCopied.Content = _lastCopiedLabel;
        };

        PreviewKeyDown += OnKey;
        Deactivated += (_, _) => Close();
        Closed += (_, _) =>
        {
            _cts?.Cancel();
            _outline.Close();
        };
        ContentRendered += (_, _) => FixPlacement();
        SourceInitialized += (_, _) => WindowExStyles.Add(this, WindowExStyles.ToolWindow);

        SelectMode(_tool.Settings.TargetMode);
        _loading = false;
        if (hasText)
            Retranslate();
    }

    public void ShowAt()
    {
        _outline.Show();
        Show();
        Activate();
    }

    // ---- 目标语言 ----

    private RadioButton MakeMode(string label, string mode)
    {
        var rb = new RadioButton { Content = label, GroupName = "snipTarget", Tag = mode };
        rb.SetResourceReference(StyleProperty, "Island.Segment");
        rb.Checked += (_, _) =>
        {
            if (_loading) return;
            _tool.Settings.TargetMode = mode;
            _tool.SaveSettings();
            Retranslate();
        };
        return rb;
    }

    private void SelectMode(string mode)
    {
        var hit = _modes.FirstOrDefault(m => (string)m.Tag == mode) ?? _modes[0];
        hit.IsChecked = true;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        var index = e.Key switch
        {
            >= Key.D1 and <= Key.D4 => e.Key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad4 => e.Key - Key.NumPad1,
            _ => -1,
        };
        if (index >= 0)
        {
            _modes[index].IsChecked = true;
            e.Handled = true;
        }
    }

    private void Retranslate()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _result.Text = "…";
        _ = TranslateAsync(cts);
    }

    private async Task TranslateAsync(CancellationTokenSource cts)
    {
        try
        {
            var text = await _tool.TranslateAsync(_ocrText, cts.Token);
            if (!cts.IsCancellationRequested)
                _result.Text = text;
        }
        catch (OperationCanceledException)
        {
            // 已被新的目标语言取代
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                _result.Text = "翻译失败";
                Log.Error("截屏翻译失败", ex);
            }
        }
    }

    // ---- 复制 ----

    private Button MakeButton(string label, string styleKey, Func<string> textProvider)
    {
        var btn = new Button { Content = label };
        btn.SetResourceReference(StyleProperty, styleKey);
        btn.Click += (_, _) =>
        {
            var text = textProvider();
            if (string.IsNullOrWhiteSpace(text) || text == "…") return;
            try
            {
                Clipboard.SetText(text);
                if (_lastCopied is not null) _lastCopied.Content = _lastCopiedLabel;
                _lastCopied = btn;
                _lastCopiedLabel = label;
                btn.Content = "已复制";
                _copyRevert.Stop();
                _copyRevert.Start();
            }
            catch (Exception ex)
            {
                Log.Error("复制失败", ex);
            }
        };
        return btn;
    }

    // ---- 布局 ----

    /// <summary>下方放不下就放到选区上方；左右不出屏。</summary>
    private void FixPlacement()
    {
        var right = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        var bottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

        if (Top + ActualHeight > bottom)
            Top = Math.Max(SystemParameters.VirtualScreenTop, _region.Top - ActualHeight - 6);
        if (Left + Width > right)
            Left = right - Width;
        if (Left < SystemParameters.VirtualScreenLeft)
            Left = SystemParameters.VirtualScreenLeft;
    }

    private static SelectableText MakeText(double fontSize, double lineHeight, string foregroundKey, double maxHeight)
    {
        var box = new SelectableText
        {
            FontSize = fontSize,
            LineHeight = lineHeight,
            MaxHeight = maxHeight,
        };
        box.SetResourceReference(Control.ForegroundProperty, foregroundKey);
        return box;
    }

    private static TextBlock MakeCaption(string text)
    {
        var caption = new TextBlock { Text = text, FontSize = 11, Margin = new Thickness(0, 0, 0, 6) };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "Island.TextTertiary");
        return caption;
    }
}

/// <summary>选区描边：点击穿透、不抢焦点，只负责标出截图来源位置。</summary>
public sealed class RegionOutlineWindow : Window
{
    public RegionOutlineWindow(Rect region)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        IsHitTestVisible = false;
        Left = region.Left - 2;
        Top = region.Top - 2;
        Width = region.Width + 4;
        Height = region.Height + 4;

        var border = new Border
        {
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(3),
            Background = Brushes.Transparent,
        };
        border.SetResourceReference(Border.BorderBrushProperty, "Island.Accent");
        Content = border;

        SourceInitialized += (_, _) => WindowExStyles.Add(this,
            WindowExStyles.ToolWindow | WindowExStyles.Transparent | WindowExStyles.NoActivate);
    }
}

