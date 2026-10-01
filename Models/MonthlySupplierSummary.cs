namespace SupplierPurchases.Models;

public sealed class MonthlySupplierSummary
{
    public string SupplierKey { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public int ProductCount { get; init; }
    public double TotalQuantity { get; init; }
    public string TotalQuantityText => TotalQuantity % 1 == 0 ? TotalQuantity.ToString("0") : TotalQuantity.ToString("0.##");
}
