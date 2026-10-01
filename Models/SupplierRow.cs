namespace SupplierPurchases.Models;

/// <summary>مورد في شاشة إدارة الموردين مع عدد مرات استخدامه في الطلبيات.</summary>
public sealed class SupplierRow : ObservableObject
{
    private bool _isSelected;

    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>عدد أسطر الطلبيات المرتبطة بهذا المورد.</summary>
    public int ItemCount { get; init; }

    public int ProductCount { get; init; }

    public bool IsUsedInOrders => ItemCount > 0;

    public string UsageText => ItemCount > 0 ? ItemCount.ToString() : "غير مستخدم";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
