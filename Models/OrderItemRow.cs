namespace SupplierPurchases.Models;

public sealed class OrderItemRow : ObservableObject
{
    public const string UnitHaba = "حبه";
    public const string UnitKarz = "كرز";

    private int _id;
    private bool _isSelected;
    private string _productName = string.Empty;
    private double _quantity = 1;
    private string _unit = UnitHaba;
    private string _supplierName = string.Empty;
    private string _notes = string.Empty;

    public int Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    /// <summary>تحديد الصف عبر مربع الاختيار لتنفيذ عمليات جماعية عليه.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string ProductName
    {
        get => _productName;
        set => SetField(ref _productName, value);
    }

    public double Quantity
    {
        get => _quantity;
        set => SetField(ref _quantity, value);
    }

    public string Unit
    {
        get => _unit;
        set
        {
            if (!SetField(ref _unit, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsHaba));
            OnPropertyChanged(nameof(IsKarz));
        }
    }

    public string SupplierName
    {
        get => _supplierName;
        set => SetField(ref _supplierName, value);
    }

    public bool IsHaba
    {
        get => _unit == UnitHaba;
        set
        {
            if (value)
            {
                Unit = UnitHaba;
            }
        }
    }

    public bool IsKarz
    {
        get => _unit == UnitKarz;
        set
        {
            if (value)
            {
                Unit = UnitKarz;
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
    }
}
