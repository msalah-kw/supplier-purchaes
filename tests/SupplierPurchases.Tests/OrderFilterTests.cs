using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases.Tests;

public class OrderFilterTests
{
    private static readonly OrderSummary[] Orders =
    [
        new() { Id = 1, BranchName = "فرع الأحمدي", OrderDate = new DateTime(2026, 9, 1), Notes = "عاجل" },
        new() { Id = 2, BranchName = "فرع السالمية", OrderDate = new DateTime(2026, 9, 15) },
        new() { Id = 3, BranchName = "فرع الجهراء", OrderDate = new DateTime(2026, 10, 1) }
    ];

    private static int[] Ids(IEnumerable<OrderSummary> orders) => orders.Select(order => order.Id).ToArray();

    [Fact]
    public void Apply_NoFilters_ReturnsAll()
    {
        Assert.Equal([1, 2, 3], Ids(OrderFilter.Apply(Orders, "", null, null)));
    }

    [Fact]
    public void Apply_SearchIgnoresHamzaVariants()
    {
        Assert.Equal([1], Ids(OrderFilter.Apply(Orders, "احمدي", null, null)));
    }

    [Fact]
    public void Apply_SearchMatchesNotes()
    {
        Assert.Equal([1], Ids(OrderFilter.Apply(Orders, "عاجل", null, null)));
    }

    [Fact]
    public void Apply_AllKeywordsMustMatch()
    {
        Assert.Empty(OrderFilter.Apply(Orders, "السالمية عاجل", null, null));
    }

    [Fact]
    public void Apply_DateRangeIsInclusive()
    {
        var result = OrderFilter.Apply(Orders, null, new DateTime(2026, 9, 15), new DateTime(2026, 10, 1));

        Assert.Equal([2, 3], Ids(result));
    }
}
