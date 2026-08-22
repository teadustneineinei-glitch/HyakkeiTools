using System.Net.Http;
using System.Text.Json;

namespace Hyakkei.Tool.Translator;

/// <summary>
/// 谷歌翻译免费接口（translate.googleapis.com, client=gtx）。
/// 需要网络可直连谷歌（用户在国外/有代理时优先使用）；失败由调用方回退到其他服务商。
/// </summary>
public sealed class GoogleTranslator : ITranslator
{
    private static readonly HttpClient Http = CreateClient();

    public string Name => "谷歌翻译";

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct)
    {
        var sl = "auto"; // let Google detect the source language (more reliable than local heuristics)
        var tl = MapLang(targetLang);
        var url = "https://translate.googleapis.com/translate_a/single" +
                  $"?client=gtx&sl={sl}&tl={tl}&dt=t&q={Uri.EscapeDataString(text)}";

        var json = await Http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);

        // 结构：[[["译文段", "原文段", ...], ...], null, "src", ...]
        var segments = doc.RootElement[0].EnumerateArray()
            .Select(seg => seg[0].GetString() ?? "")
            .ToArray();
        return string.Concat(segments).Trim();
    }

    private static string MapLang(string lang) => lang switch
    {
        "zh" => "zh-CN",
        _ => lang,
    };

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) }; // 短超时，回退更快
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        return http;
    }
}
