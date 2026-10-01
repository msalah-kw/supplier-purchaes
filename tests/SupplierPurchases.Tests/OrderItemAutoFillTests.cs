using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases.Tests;

public class OrderItemAutoFillTests
{
    [Fact]
    public void Apply_EmptySupplier_FillsSupplierFromRule()
    {
        var row = new OrderItemRow();

        OrderItemAutoFill.Apply(row, "اير بار", new RuleMatch("البيرق", ""), null);

        Assert.Equal("البيرق", row.SupplierName);
    }

    [Fact]
    public void Apply_ExistingSupplier_KeepsManualSupplier()
    {
        var row = new OrderItemRow { SupplierName = "مورد يدوي" };

        OrderItemAutoFill.Apply(row, "اير بار", new RuleMatch("البيرق", ""), null);

        Assert.Equal("مورد يدوي", row.SupplierName);
    }

    [Fact]
    public void Apply_NewProduct_SetsUnitFromRule()
    {
        var row = new OrderItemRow();

        var applied = OrderItemAutoFill.Apply(row, "اير بار", new RuleMatch("البيرق", OrderItemRow.UnitKarz), null);

        Assert.Equal(OrderItemRow.UnitKarz, row.Unit);
        Assert.Equal(2, applied.Count);
    }

    [Fact]
    public void Apply_SameProductAgain_KeepsManualUnit()
    {
        var row = new OrderItemRow();
        var match = new RuleMatch("البيرق", OrderItemRow.UnitKarz);
        OrderItemAutoFill.Apply(row, "اير بار", match, null);

        row.Unit = OrderItemRow.UnitHaba;
        OrderItemAutoFill.Apply(row, "اير بار", match, null);

        Assert.Equal(OrderItemRow.UnitHaba, row.Unit);
    }

    [Fact]
    public void Apply_RuleWithoutUnit_UsesRememberedUnit()
    {
        var row = new OrderItemRow();

        OrderItemAutoFill.Apply(row, "اير بار", new RuleMatch("البيرق", ""), OrderItemRow.UnitKarz);

        Assert.Equal(OrderItemRow.UnitKarz, row.Unit);
    }

    [Fact]
    public void Apply_NoRule_UsesRememberedUnit()
    {
        var row = new OrderItemRow();

        OrderItemAutoFill.Apply(row, "منتج", null, OrderItemRow.UnitKarz);

        Assert.Equal(OrderItemRow.UnitKarz, row.Unit);
        Assert.Equal(string.Empty, row.SupplierName);
    }

    [Fact]
    public void Apply_RuleUnitBeatsRememberedUnit()
    {
        var row = new OrderItemRow { Unit = OrderItemRow.UnitKarz };

        OrderItemAutoFill.Apply(row, "اير بار", new RuleMatch("البيرق", OrderItemRow.UnitHaba), OrderItemRow.UnitKarz);

        Assert.Equal(OrderItemRow.UnitHaba, row.Unit);
    }

    [Fact]
    public void Apply_NothingToChange_ReturnsEmpty()
    {
        var row = new OrderItemRow { SupplierName = "البيرق" };

        var applied = OrderItemAutoFill.Apply(row, "منتج", null, null);

        Assert.Empty(applied);
    }
}
