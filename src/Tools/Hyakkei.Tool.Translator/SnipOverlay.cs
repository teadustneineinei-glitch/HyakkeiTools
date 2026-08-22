using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Hyakkei.Tool.Translator;

/// <summary>
/// 截屏选区遮罩：覆盖虚拟屏幕，鼠标拉框选择区域，Esc 取消。
/// 结果 PixelSelection 为屏幕物理像素坐标（已按 DPI 换算），供 CopyFromScreen 使用。
/// </summary>
public sealed class SnipOverlay : Window
{
    private readonly Canvas _canvas;
    private readonly Rectangle _marquee;
    private Point _start;
    private bool _dragging;

    /// <summary>选区（屏幕物理像素）；null = 取消或选区过小。</summary>
    public Int32Rect? PixelSelection { get; private set; }

    /// <summary>选区（WPF 逻辑坐标 DIP，屏幕绝对位置），供结果卡片就地定位。</summary>
    public Rect? DipSelection { get; private set; }

    public SnipOverlay()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        Cursor = Cursors.Cross;
        Background = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0));

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _marquee = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF5)),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
            Visibility = Visibility.Collapsed,
        };
        _canvas = new Canvas();
        _canvas.Children.Add(_marquee);
        Content = _canvas;

        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _start = e.GetPosition(_canvas);
        _marquee.Visibility = Visibility.Visible;
        UpdateMarquee(_start);
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_dragging)
            UpdateMarquee(e.GetPosition(_canvas));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        var p = e.GetPosition(_canvas);
        var dipRect = new Rect(_start, p);
        if (dipRect.Width < 5 || dipRect.Height < 5)
        {
            DialogResult = false;
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        DipSelection = new Rect(Left + dipRect.X, Top + dipRect.Y, dipRect.Width, dipRect.Height);
        PixelSelection = new Int32Rect(
            (int)Math.Round((Left + dipRect.X) * dpi.DpiScaleX),
            (int)Math.Round((Top + dipRect.Y) * dpi.DpiScaleY),
            (int)Math.Round(dipRect.Width * dpi.DpiScaleX),
            (int)Math.Round(dipRect.Height * dpi.DpiScaleY));
        DialogResult = true;
    }

    private void UpdateMarquee(Point p)
    {
        var r = new Rect(_start, p);
        Canvas.SetLeft(_marquee, r.X);
        Canvas.SetTop(_marquee, r.Y);
        _marquee.Width = r.Width;
        _marquee.Height = r.Height;
    }
}
