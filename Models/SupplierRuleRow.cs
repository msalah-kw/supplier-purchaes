namespace SupplierPurchases.Models;

/// <summary>قاعدة إسناد تلقائي: أي منتج يحتوي على الكلمة المفتاحية يُسند إلى المورد المحدد، وإلى الوحدة إن وُجدت.</summary>
public sealed class SupplierRuleRow : ObservableObject
{
    private string _keyword = string.Empty;
    private string _supplierName = string.Empty;
    private string _unit = string.Empty;
    private int _priority;
    private bool _isSelected;

    public string Keyword
    {
        get => _keyword;
        set => SetField(ref _keyword, value);
    }

    public string SupplierName
    {
        get => _supplierName;
        set => SetField(ref _supplierName, value);
    }

    /// <summary>الوحدة التي تُضبط تلقائيًا (حبه أو كرز)، أو نص فارغ لترك الوحدة كما هي.</summary>
    public string Unit
    {
        get => _unit;
        set => SetField(ref _unit, value ?? string.Empty);
    }

    /// <summary>الأولوية الأعلى تُطبَّق أولًا عند تطابق أكثر من قاعدة.</summary>
    public int Priority
    {
        get => _priority;
        set => SetField(ref _priority, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
