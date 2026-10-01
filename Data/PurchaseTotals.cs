using SupplierPurchases.Models;

namespace SupplierPurchases.Data;

/// <summary>إجمالي الكميات لكل وحدة في قائمة الشراء، لعرضه أسفل الفاتورة.</summary>
public static class PurchaseTotals
{
    /// <summary>
    /// يعيد إجمالي كل وحدة. الحبات والكروز تظهر دائمًا وبهذا الترتيب حتى لو كان إجماليها صفرًا،
    /// وأي وحدة أخرى محفوظة في بيانات قديمة تظهر بعدهما.
    /// </summary>
    public static List<UnitTotal> ByUnit(IEnumerable<PurchaseItem> items)
    {
        var totals = items
            .GroupBy(item => item.Unit)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.TotalQuantity));

        var result = new List<UnitTotal>
        {
            new(OrderItemRow.UnitHaba, totals.GetValueOrDefault(OrderItemRow.UnitHaba)),
            new(OrderItemRow.UnitKarz, totals.GetValueOrDefault(OrderItemRow.UnitKarz))
        };

        result.AddRange(totals
            .Where(pair => pair.Key is not (OrderItemRow.UnitHaba or OrderItemRow.UnitKarz))
            .OrderBy(pair => pair.Key)
            .Select(pair => new UnitTotal(pair.Key, pair.Value)));

        return result;
    }
}

public sealed record UnitTotal(string Unit, double Total)
{
    public string Label => Unit switch
    {
        OrderItemRow.UnitHaba => "إجمالي الحبات",
        OrderItemRow.UnitKarz => "إجمالي الكروز",
        _ => $"إجمالي {Unit}"
    };

    public string TotalText => Total % 1 == 0 ? Total.ToString("0") : Total.ToString("0.##");
}
