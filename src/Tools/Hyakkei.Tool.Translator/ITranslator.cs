namespace Hyakkei.Tool.Translator;

/// <summary>翻译设置（持久化到配置 Tools.translator 节）。</summary>
public sealed class TranslatorSettings
{
    /// <summary>Auto（中英互译）| Zh（强制译中）| En（强制译英）</summary>
    public string TargetMode { get; set; } = "Auto";

    /// <summary>Auto（谷歌→百度(有Key)→腾讯 依次回退）| Google | Baidu | TranSmart（强制单一服务商）</summary>
    public string Provider { get; set; } = "Auto";

    /// <summary>百度翻译开放平台 APP ID；与密钥都填上后自动改用百度接口。</summary>
    public string BaiduAppId { get; set; } = "";

    public string BaiduSecret { get; set; } = "";
}

/// <summary>翻译服务抽象。lang 取 "zh" / "en" / "fr"。</summary>
public interface ITranslator
{
    string Name { get; }

    Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct);
}
