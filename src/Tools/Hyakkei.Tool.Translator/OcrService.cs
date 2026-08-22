using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Hyakkei.Core;

namespace Hyakkei.Tool.Translator;

/// <summary>
/// Windows 内置 OCR（Windows.Media.Ocr，离线免费）。引擎按语言分模型，
/// 故按内容自动选择：先跑中文引擎，若结果无汉字则改用英文/法文引擎（需系统装有对应 OCR 语言包：
/// 设置 → 语言和区域 → 添加语言 → 勾选「光学字符识别」，或管理员 PowerShell
/// Add-WindowsCapability -Online -Name Language.OCR~~~en-US~0.0.1.0）。
/// </summary>
public static class OcrService
{
    private static bool _hintLogged;

    public static async Task<string> RecognizeAsync(Bitmap bmp)
    {
        using var soft = ToSoftwareBitmap(bmp);

        var zh = TryEngine("zh-Hans-CN");
        var en = TryEngine("en-US") ?? TryEngine("en-GB");
        var fr = TryEngine("fr-FR");

        if (zh is null && en is null && fr is null)
        {
            var any = OcrEngine.TryCreateFromUserProfileLanguages()
                      ?? throw new InvalidOperationException("系统无可用 OCR 语言");
            return Clean(await any.RecognizeAsync(soft), any.RecognizerLanguage.LanguageTag);
        }

        // 1) 中文引擎：结果含汉字即采用
        if (zh is not null)
        {
            var zhResult = await zh.RecognizeAsync(soft);
            var zhText = Clean(zhResult, "zh-Hans-CN");
            if (LanguageDetect.ContainsCjk(zhText) || (en is null && fr is null))
            {
                if (en is null && !_hintLogged)
                {
                    _hintLogged = true;
                    Log.Info("未安装英文 OCR 语言包，拉丁文字识别精度受限（见 OcrService 注释）");
                }
                return zhText;
            }
        }

        // 2) 拉丁文字：英文引擎；看起来像法语且有法语引擎则改用法语引擎
        if (en is not null)
        {
            var enText = Clean(await en.RecognizeAsync(soft), "en");
            if (fr is not null && LanguageDetect.LooksFrench(enText))
                return Clean(await fr.RecognizeAsync(soft), "fr");
            return enText;
        }

        return Clean(await fr!.RecognizeAsync(soft), "fr");
    }

    private static OcrEngine? TryEngine(string tag)
    {
        try
        {
            return OcrEngine.TryCreateFromLanguage(new Language(tag));
        }
        catch
        {
            return null;
        }
    }

    private static string Clean(OcrResult result, string langTag)
    {
        Log.Info($"OCR 引擎：{langTag}");
        var lines = result.Lines.Select(l =>
        {
            var text = l.Text;
            // 中文 OCR 会在字间插空格，去掉；拉丁文字行保留空格
            return LanguageDetect.ContainsCjk(text) ? text.Replace(" ", "") : text;
        });
        return string.Join("\n", lines).Trim();
    }

    private static SoftwareBitmap ToSoftwareBitmap(Bitmap bmp)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        byte[] bytes;
        try
        {
            bytes = new byte[Math.Abs(data.Stride) * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        return SoftwareBitmap.CreateCopyFromBuffer(
            bytes.AsBuffer(), BitmapPixelFormat.Bgra8, bmp.Width, bmp.Height, BitmapAlphaMode.Premultiplied);
    }

}
