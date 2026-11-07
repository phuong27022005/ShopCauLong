using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BadmintonShop.Api.Catalog;

public static partial class SlugGenerator
{
    public static async Task<string> CreateAsync(
        string name, string fallback, Func<string, Task<bool>> exists)
    {
        var normalized = name.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var withoutMarks = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                withoutMarks.Append(character);
        }

        var stem = Separators().Replace(withoutMarks.ToString(), "-").Trim('-');
        if (stem.Length == 0) stem = fallback;

        // Chọn hậu tố nhỏ nhất chưa được sử dụng.
        var candidate = stem;
        for (var suffix = 2; await exists(candidate); suffix++)
            candidate = FormattableString.Invariant($"{stem}-{suffix}");

        return candidate;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Separators();
}
