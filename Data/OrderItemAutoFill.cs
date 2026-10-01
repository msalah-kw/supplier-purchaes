using SupplierPurchases.Models;

namespace SupplierPurchases.Data;

/// <summary>
/// يملأ المورد والوحدة تلقائيًا لسطر الطلبية عند تثبيت اسم المنتج.
/// المورد من قواعد الإسناد إذا كان فارغًا. الوحدة من القاعدة أولًا، ثم من آخر وحدة استُخدمت للمنتج.
/// </summary>
public static class OrderItemAutoFill
{
    /// <summary>يطبّق التعبئة التلقائية ويعيد وصفًا لكل حقل تغيّر (فارغ إذا لم يتغير شيء).</summary>
    public static List<string> Apply(OrderItemRow row, string productName, RuleMatch? match, string? rememberedUnit)
    {
        // الوحدة تُضبط فقط عند تغيير اسم المنتج، حتى لا يُلغى اختيار الوحدة اليدوي
        var productChanged = row.RuleCheckedProduct != productName;
        row.RuleCheckedProduct = productName;

        var applied = new List<string>();
        if (match is not null && string.IsNullOrWhiteSpace(row.SupplierName))
        {
            row.SupplierName = match.SupplierName;
            applied.Add($"المورد \"{match.SupplierName}\"");
        }

        if (!productChanged)
        {
            return applied;
        }

        var unit = match is { Unit.Length: > 0 }
            ? match.Unit
            : SupplierRuleEngine.NormalizeUnit(rememberedUnit);

        if (unit.Length > 0 && row.Unit != unit)
        {
            row.Unit = unit;
            applied.Add($"الوحدة \"{unit}\"");
        }

        return applied;
    }
}
