namespace SupplierPurchases.Models;

public sealed class OrderSummary
{
    public int Id { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public string OrderDateText => OrderDate.ToString("yyyy-MM-dd");
    public int ItemCount { get; init; }
    public string Notes { get; init; } = string.Empty;
}
