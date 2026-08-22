using System.Windows;
using Hyakkei.Core;

namespace Hyakkei.Tool.Translator;

/// <summary>
/// 翻译：输入即翻 + 划词翻译（会话热键 F6 取词后弹岛）+ 截屏翻译。
/// 服务商回退链：谷歌 → 百度（有 Key）→ 腾讯交互翻译兜底。
/// </summary>
public sealed class TranslatorTool : ITool, IToolSession, IToolQuickInput
{
    public const string ToolId = "translator";

    private readonly TranSmartTranslator _transmart = new();
    private readonly GoogleTranslator _google = new();
    private readonly BaiduTranslator _baidu;
    private TranslatorIslandView? _view;
    private int _hotkeyId = -1;

    public TranslatorTool()
    {
        Settings = ToolContext.Config.GetToolConfig<TranslatorSettings>(ToolId);
        _baidu = new BaiduTranslator(() => Settings);
    }

    public TranslatorSettings Settings { get; }

    public string Id => ToolId;
    public string Name => "翻译";
    public string Description => "输入即翻 · 划词按 F6";
    public string IconGlyph => ""; // 文A（Characters）

    public FrameworkElement CreateView() => new TranslatorInfoView();
    public FrameworkElement CreateIslandView() => _view = new TranslatorIslandView(this);

    private bool HasBaiduKeys =>
        !string.IsNullOrWhiteSpace(Settings.BaiduAppId) && !string.IsNullOrWhiteSpace(Settings.BaiduSecret);

    /// <summary>按 Provider 设置返回服务商顺序（Auto = 谷歌 → 百度(有Key) → 腾讯 逐级回退）。</summary>
    private ITranslator[] ProviderChain() => Settings.Provider switch
    {
        "Google" => [_google],
        "Baidu" => [_baidu],
        "TranSmart" => [_transmart],
        _ => HasBaiduKeys ? [_google, _baidu, _transmart] : [_google, _transmart],
    };

    /// <summary>按设置决定方向并翻译，服务商失败自动回退。返回译文。</summary>
    public async Task<string> TranslateAsync(string text, CancellationToken ct)
    {
        var source = LanguageDetect.Detect(text);
        var target = Settings.TargetMode switch
        {
            "Zh" => "zh",
            "En" => "en",
            "Fr" => "fr",
            _ => source == "en" ? "zh" : "en", // 自动：中/法 → 英，英 → 中
        };
        if (source == target)
            return text; // 目标与原文同语种，无需翻译

        var chain = ProviderChain();
        Exception? last = null;
        foreach (var provider in chain)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await provider.TranslateAsync(text, source, target, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
                Log.Error($"{provider.Name} 失败，尝试回退", ex);
            }
        }
        throw last ?? new InvalidOperationException("无可用翻译服务");
    }

    public void SaveSettings() => ToolContext.Config.SetToolConfig(ToolId, Settings);

    // ---- 万能输入：列表态直接打字 → 「翻译」行 → 回车出译文 ----

    public string? QuickActionLabel(string input) => "翻译";

    public void HandleQuickInput(string input) => _view?.TranslateExternal(input);

    public void OnSessionActivated()
    {
        var gesture = ToolContext.Config.Current.ModuleHotkey;
        _hotkeyId = ToolContext.ModuleHotkeys.Register(gesture, OnHotkey);
        Log.Info(_hotkeyId >= 0 ? $"翻译热键已挂载：{gesture}" : $"翻译热键挂载失败：{gesture}");
    }

    public void OnSessionDeactivated()
    {
        if (_hotkeyId >= 0)
        {
            ToolContext.ModuleHotkeys.Unregister(_hotkeyId);
            _hotkeyId = -1;
        }
        Log.Info("翻译热键已解除");
    }

    /// <summary>
    /// 热键一键两用：当前前台有选中文字 → 取词、唤岛、翻译；没有选中 → 直接进入截屏翻译。
    /// </summary>
    private void OnHotkey()
    {
        string? captured = null;
        try
        {
            captured = ClipboardCapture.CaptureSelection();
        }
        catch (Exception ex)
        {
            Log.Error("取词失败", ex);
        }

        if (captured is not null)
        {
            ToolContext.SummonIsland?.Invoke();
            _view?.TranslateExternal(captured);
        }
        else
        {
            _view?.StartSnip();
        }
    }

}
