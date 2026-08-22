using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace Hyakkei.App.Views;

public partial class SettingsView : UserControl
{
    private bool _loading = true;

    public SettingsView()
    {
        InitializeComponent();

        ThemeCombo.SelectedIndex = App.Config.Current.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0,
        };
        IntervalText.Text = $"双击 Ctrl · {App.Config.Current.DoubleCtrlIntervalMs} ms";

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0";
        VersionText.Text = $"百景工具箱 v{version}";

        _loading = false;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeCombo.SelectedItem is not ComboBoxItem item) return;

        var theme = (string)item.Tag;
        App.Config.Current.Theme = theme;
        App.Config.Save();

        var window = Application.Current.MainWindow;
        if (window is not null)
        {
            if (theme == "System")
                SystemThemeWatcher.Watch(window);
            else
                SystemThemeWatcher.UnWatch(window);
        }
        App.ApplyTheme(theme);
    }
}
