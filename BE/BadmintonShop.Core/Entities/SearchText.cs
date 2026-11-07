using System.Globalization;
using System.Text;

namespace BadmintonShop.Core.Entities;

public static class SearchText
{
    public static string Normalize(string value) =>
        string.Concat(value.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC);
}
