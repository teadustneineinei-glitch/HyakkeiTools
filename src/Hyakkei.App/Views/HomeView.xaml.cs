using System.Windows.Controls;

namespace Hyakkei.App.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        ToolCountText.Text = $"{App.Tools.Tools.Count} 个";
    }
}
