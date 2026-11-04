using System.Globalization;
using System.Text;

namespace Mekansm.Services;

public static class Slugger
{
    public static string Make(string? input, int max = 80, string fallback = "post")
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;

        var sb = new StringBuilder();
        var lastDash = true;
        foreach (var ch in input.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }

            if (sb.Length >= max) break;
        }

        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? fallback : slug;
    }
}
