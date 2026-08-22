using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Hyakkei.Tool.Translator;

/// <summary>
/// 可选中复制、带行距的只读文本块（基于 RichTextBox）。
/// WPF 的 TextBox 无法设行高，中文正文默认行距过紧，阅读费眼；本控件用 FlowDocument.LineHeight 解决。
/// </summary>
public sealed class SelectableText : RichTextBox
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SelectableText), new PropertyMetadata("", OnTextChanged));

    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight), typeof(double), typeof(SelectableText), new PropertyMetadata(22.0, OnLineHeightChanged));

    public SelectableText()
    {
        Style = null;
        IsReadOnly = true;
        IsDocumentEnabled = false;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            LineHeight = LineHeight,
        };
        SetResourceReference(SelectionBrushProperty, "Island.AccentWash");
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (SelectableText)d;
        self.Document.Blocks.Clear();
        self.Document.Blocks.Add(new Paragraph(new Run((string)e.NewValue ?? "")) { Margin = new Thickness(0) });
    }

    private static void OnLineHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SelectableText)d).Document.LineHeight = (double)e.NewValue;
}
