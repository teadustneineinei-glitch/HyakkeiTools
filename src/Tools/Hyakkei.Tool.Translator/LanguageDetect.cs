namespace Hyakkei.Tool.Translator;

/// <summary>本地语种启发式：中文（CJK）/ 法语（重音字符或常见虚词）/ 其余视为英语。供翻译方向判定与 OCR 引擎选择共用。</summary>
public static class LanguageDetect
{
    private const string FrenchAccents = "éèêëàâçùûüôîïœÉÈÊÀÇ";

    private static readonly string[] FrenchMarkers =
        [" le ", " la ", " les ", " des ", " une ", " est ", " et ", " pour ", " que ", " vous ", " nous ", " avec ", " dans ", " pas "];

    /// <summary>返回 "zh" / "fr" / "en"。</summary>
    public static string Detect(string text)
    {
        if (ContainsCjk(text)) return "zh";
        if (LooksFrench(text)) return "fr";
        return "en";
    }

    public static bool ContainsCjk(string text)
        => text.Any(c => c is >= '一' and <= '鿿' or >= '㐀' and <= '䶿');

    public static bool LooksFrench(string text)
    {
        if (text.Any(FrenchAccents.Contains)) return true;
        var padded = " " + text.ToLowerInvariant() + " ";
        return FrenchMarkers.Count(padded.Contains) >= 2;
    }
}
