namespace SupplierPurchases.Models;

public sealed class OrderDetail
{
    public int Id { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public string Notes { get; init; } = string.Empty;
    public List<OrderItemRow> Items { get; init; } = [];
}
