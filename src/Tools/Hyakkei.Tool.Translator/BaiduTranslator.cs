using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hyakkei.Tool.Translator;

/// <summary>百度翻译开放平台标准版（需 AppId + 密钥，config/settings.json 的 Tools.translator 节配置）。</summary>
public sealed class BaiduTranslator : ITranslator
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly Func<TranslatorSettings> _settings;

    public BaiduTranslator(Func<TranslatorSettings> settings)
    {
        _settings = settings;
    }

    public string Name => "百度翻译";

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct)
    {
        var s = _settings();
        var salt = Environment.TickCount64.ToString();
        var sign = Md5Hex(s.BaiduAppId + text + salt + s.BaiduSecret);
        var url = "https://fanyi-api.baidu.com/api/trans/vip/translate" +
                  $"?q={Uri.EscapeDataString(text)}&from=auto&to={MapLang(targetLang)}" +
                  $"&appid={s.BaiduAppId}&salt={salt}&sign={sign}";

        var json = await Http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error_code", out var err))
            throw new InvalidOperationException($"百度接口错误 {err.GetString()}");

        var parts = root.GetProperty("trans_result").EnumerateArray()
            .Select(e => e.GetProperty("dst").GetString() ?? "")
            .ToArray();
        return string.Join("\n", parts).Trim();
    }

    private static string MapLang(string lang) => lang switch
    {
        "fr" => "fra", // Baidu uses "fra" for French
        _ => lang,
    };

    private static string Md5Hex(string input)
        => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(input)));
}
