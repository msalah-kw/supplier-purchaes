using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases.Tests;

public class PurchaseTotalsTests
{
    private static PurchaseItem Item(double quantity, string unit) => new() { TotalQuantity = quantity, Unit = unit };

    [Fact]
    public void ByUnit_SumsEachUnit()
    {
        var totals = PurchaseTotals.ByUnit([Item(5, "حبه"), Item(2.5, "كرز"), Item(3, "حبه")]);

        Assert.Equal([new UnitTotal("حبه", 8), new UnitTotal("كرز", 2.5)], totals);
    }

    [Fact]
    public void ByUnit_AlwaysShowsHabaAndKarz()
    {
        var totals = PurchaseTotals.ByUnit([Item(4, "كرز")]);

        Assert.Equal([new UnitTotal("حبه", 0), new UnitTotal("كرز", 4)], totals);
    }

    [Fact]
    public void ByUnit_OtherUnitsComeAfterHabaAndKarz()
    {
        var totals = PurchaseTotals.ByUnit([Item(1, "كرتون"), Item(2, "حبه")]);

        Assert.Equal(["حبه", "كرز", "كرتون"], totals.Select(total => total.Unit));
    }

    [Theory]
    [InlineData("حبه", "إجمالي الحبات")]
    [InlineData("كرز", "إجمالي الكروز")]
    [InlineData("كرتون", "إجمالي كرتون")]
    public void Label_NamesEachUnit(string unit, string expected)
    {
        Assert.Equal(expected, new UnitTotal(unit, 1).Label);
    }

    [Theory]
    [InlineData(8, "8")]
    [InlineData(2.5, "2.5")]
    public void TotalText_HidesTrailingZeros(double total, string expected)
    {
        Assert.Equal(expected, new UnitTotal("حبه", total).TotalText);
    }
}
