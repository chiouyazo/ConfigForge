using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// The per-instance group heading in the sidebar (<c>.cf-category-group-heading</c> in
/// <c>SidebarNav.razor</c>) must read as a real section heading: bold, and a noticeably larger
/// font size than the <c>.cf-category-item</c> labels listed under it.
/// </summary>
public sealed class SidebarGroupHeadingCssTests
{
    [Fact]
    public void GroupHeadingRule_IsBoldAndLargerThanDefaultCategoryLabelSize()
    {
        string css = File.ReadAllText(ConfigForgeCssPath());

        string headingRule = ExtractRule(css, ".cf-category-group-heading");
        double headingFontSizeRem = ExtractRemFontSize(headingRule);
        int headingFontWeight = ExtractFontWeight(headingRule);

        Assert.True(
            headingFontWeight >= 700,
            $"Expected a bold heading (font-weight >= 700), got {headingFontWeight}."
        );

        // .cf-category-item sets no font-size of its own (font: inherit), so a category label
        // renders at the ambient 1rem body size; the heading must clearly exceed that.
        Assert.True(
            headingFontSizeRem > 1.0,
            $"Expected the heading font-size to exceed the 1rem category label size, got {headingFontSizeRem}rem."
        );
    }

    private static string ExtractRule(string css, string selector)
    {
        int start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Selector '{selector}' not found in configforge.css.");
        int end = css.IndexOf('}', start);
        return css[start..end];
    }

    private static double ExtractRemFontSize(string rule)
    {
        Match match = Regex.Match(rule, @"font-size:\s*([\d.]+)rem");
        Assert.True(match.Success, $"No rem font-size found in rule: {rule}");
        return double.Parse(
            match.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    private static int ExtractFontWeight(string rule)
    {
        Match match = Regex.Match(rule, @"font-weight:\s*(\d+)");
        Assert.True(match.Success, $"No numeric font-weight found in rule: {rule}");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ConfigForgeCssPath([CallerFilePath] string testFilePath = "")
    {
        string testDirectory = Path.GetDirectoryName(testFilePath)!;
        string repoRoot = Path.Combine(testDirectory, "..", "..");
        return Path.GetFullPath(
            Path.Combine(repoRoot, "src", "ConfigForge.Blazor", "wwwroot", "configforge.css")
        );
    }
}
