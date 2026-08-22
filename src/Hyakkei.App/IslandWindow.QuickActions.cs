using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using Hyakkei.Core;

namespace Hyakkei.App;

/// <summary>万能输入：搜索框内容不是模块名时提供的快捷行（打开链接 / 算式 / 模块快捷输入）。</summary>
public partial class IslandWindow
{
    private static readonly Regex UrlPattern = new(
        @"^(https?://\S+|www\.\S+|[\w\-]+(\.[\w\-]+)+(/\S*)?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MathPattern = new(
        @"^[\d\s\.\+\-\*/%\(\)]+$", RegexOptions.Compiled);

    private IEnumerable<IslandChip> BuildQuickActions(string query)
    {
        if (UrlPattern.IsMatch(query))
        {
            var url = query.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? query : "https://" + query;
            yield return new IslandChip
            {
                Glyph = "\uE774", // Globe
                Name = "打开链接",
                CopyValue = url,
                Action = () =>
                {
                    HideIsland();
                    try
                    {
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Log.Error("打开链接失败", ex);
                    }
                },
            };
            yield break;
        }

        if (TryEvaluate(query, out var result))
        {
            yield return new IslandChip
            {
                Glyph = "\uE8EF", // Calculator
                Name = "= " + result,
                CopyValue = result,
                Action = () =>
                {
                    CopyToClipboard(result);
                    HideIsland();
                },
            };
            yield break;
        }

        var disabled = App.Config.Current.DisabledTools;
        foreach (var tool in App.Tools.Tools)
        {
            if (disabled.Contains(tool.Id) || tool is not IToolQuickInput quick) continue;
            var label = quick.QuickActionLabel(query);
            if (label is null) continue;
            yield return new IslandChip { Tool = tool, Glyph = tool.IconGlyph, Name = label, QuickInput = query };
        }
    }

    /// <summary>列表态 F6：复制当前行的值（算式结果 / 链接地址），岛不关闭。</summary>
    private void CopySelectedRowValue()
    {
        if (_expandedTool is not null) return;
        var chip = ResultList.SelectedItem as IslandChip ?? _visibleChips.FirstOrDefault();
        if (chip?.CopyValue is not null)
            CopyToClipboard(chip.CopyValue);
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            Log.Error("复制失败", ex);
        }
    }

    private static bool TryEvaluate(string expr, out string result)
    {
        result = "";
        if (!MathPattern.IsMatch(expr) || !expr.Any("+-*/%".Contains) || !expr.Any(char.IsDigit))
            return false;
        try
        {
            var value = Convert.ToDouble(new DataTable().Compute(expr, ""));
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            result = value.ToString("G15");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
