using SupplierPurchases.Data;

namespace SupplierPurchases.Models;

/// <summary>صف منتج محفوظ في شاشة استيراد وإدارة المنتجات.</summary>
public sealed class SavedProductRow : ObservableObject
{
    private bool _isSelected;

    public SavedProductRow(string name)
    {
        Name = name;
        NormalizedName = ArabicText.Normalize(name);
    }

    public string Name { get; }

    public string NormalizedName { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
