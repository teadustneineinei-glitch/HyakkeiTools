using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Hyakkei.Tool.Translator;

/// <summary>
/// 腾讯交互翻译（transmart.qq.com）免费接口：无需注册与 Key，国内直连。
/// 2026-08-14 实测可用；若失效，切换 ITranslator 其他实现即可。
/// </summary>
public sealed class TranSmartTranslator : ITranslator
{
    private static readonly HttpClient Http = CreateClient();

    private static readonly string ClientKey =
        $"browser-chrome-110.0.0-Windows_10-{Guid.NewGuid()}-{DateTimeOffset.Now.ToUnixTimeMilliseconds()}";

    public string Name => "腾讯交互翻译";

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct)
    {
        var payload = new
        {
            header = new { fn = "auto_translation", client_key = ClientKey },
            type = "plain",
            model_category = "normal",
            source = new { lang = sourceLang, text_list = new[] { text } },
            target = new { lang = targetLang },
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await Http.PostAsync("https://transmart.qq.com/api/imt", content, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var retCode = root.GetProperty("header").GetProperty("ret_code").GetString();
        if (retCode != "succ")
            throw new InvalidOperationException($"TranSmart 返回 {retCode}");

        var parts = root.GetProperty("auto_translation").EnumerateArray()
            .Select(e => e.GetString() ?? "")
            .ToArray();
        return string.Join("\n", parts).Trim();
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Referrer = new Uri("https://transmart.qq.com/zh-CN/index");
        return http;
    }
}
