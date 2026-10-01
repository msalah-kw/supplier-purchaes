using SupplierPurchases.Data;

namespace SupplierPurchases.Tests;

public class ArabicTextTests
{
    [Theory]
    [InlineData("أحمد", "احمد")]
    [InlineData("إبراهيم", "ابراهيم")]
    [InlineData("آمنة", "امنه")]
    [InlineData("مستشفى", "مستشفي")]
    [InlineData("مُحَمَّد", "محمد")]
    [InlineData("عـــلي", "علي")]
    [InlineData("١٢٣", "123")]
    [InlineData("  اير   بار  ", "اير بار")]
    public void Normalize_FoldsArabicSpellingVariants(string input, string expected)
    {
        Assert.Equal(expected, ArabicText.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyInput_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, ArabicText.Normalize(input));
    }

    [Fact]
    public void Normalize_LatinText_IsCaseInsensitive()
    {
        Assert.Equal(ArabicText.Normalize("Air Bar"), ArabicText.Normalize("AIR bar"));
    }
}
