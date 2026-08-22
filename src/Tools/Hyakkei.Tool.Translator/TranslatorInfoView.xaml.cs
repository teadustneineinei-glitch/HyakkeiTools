using System.Windows.Controls;
using Hyakkei.Core;

namespace Hyakkei.Tool.Translator;

public partial class TranslatorInfoView : UserControl
{
    public TranslatorInfoView()
    {
        InitializeComponent();
        HotkeyRun.Text = $" {ToolContext.Config.Current.ModuleHotkey} ";
        ProviderText.Text = "默认使用腾讯交互翻译（免费）。在 config/settings.json 的 Tools.translator 节填入 BaiduAppId 与 BaiduSecret 后自动切换百度翻译。";
    }
}
