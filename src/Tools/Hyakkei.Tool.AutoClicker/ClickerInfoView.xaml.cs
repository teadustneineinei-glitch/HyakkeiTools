using System.Windows.Controls;
using Hyakkei.Core;

namespace Hyakkei.Tool.AutoClicker;

public partial class ClickerInfoView : UserControl
{
    public ClickerInfoView()
    {
        InitializeComponent();
        HotkeyRun.Text = $" {ToolContext.Config.Current.ModuleHotkey}";
    }
}
