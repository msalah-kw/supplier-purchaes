namespace SupplierPurchases.Models;

public sealed class PurchaseItem
{
    public int RowNumber { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public double TotalQuantity { get; init; }
    public string QuantityText => TotalQuantity % 1 == 0 ? TotalQuantity.ToString("0") : TotalQuantity.ToString("0.##");
    public string Unit { get; init; } = string.Empty;
    /// <summary>تفصيل الكميات لكل فرع، مثل: "فرع أ: 5، فرع ب: 3".</summary>
    public string Sources { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}
