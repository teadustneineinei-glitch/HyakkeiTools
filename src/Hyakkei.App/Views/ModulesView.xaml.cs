using System.Windows;
using System.Windows.Controls;
using Hyakkei.Core;

namespace Hyakkei.App.Views;

public sealed class ModuleRow
{
    public required ITool Tool { get; init; }
    public string Glyph => Tool.IconGlyph;
    public string Name => Tool.Name;
    public string Description => Tool.Description;
    public bool IsEnabled { get; set; }
}

public partial class ModulesView : UserControl
{
    public ModulesView()
    {
        InitializeComponent();
        var disabled = App.Config.Current.DisabledTools;
        ModuleList.ItemsSource = App.Tools.Tools
            .Select(t => new ModuleRow { Tool = t, IsEnabled = !disabled.Contains(t.Id) })
            .ToList();
    }

    private void OnToggleClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ModuleRow row) return;

        var disabled = App.Config.Current.DisabledTools;
        if (row.IsEnabled)
            disabled.Remove(row.Tool.Id);
        else if (!disabled.Contains(row.Tool.Id))
            disabled.Add(row.Tool.Id);
        App.Config.Save();
        // 岛每次唤起时重建图标行，无需额外通知
    }
}
