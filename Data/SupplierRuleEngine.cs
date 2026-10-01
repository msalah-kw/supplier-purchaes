using SupplierPurchases.Models;

namespace SupplierPurchases.Data;

/// <summary>
/// يطبّق قواعد الإسناد التلقائي: أي منتج يحتوي اسمه على كلمة مفتاحية معينة
/// يُسند إلى المورد المرتبط بها، وتُضبط وحدته إن حددتها القاعدة. القاعدة ذات الأولوية الأعلى ثم الكلمة الأطول تفوز.
/// </summary>
public sealed class SupplierRuleEngine
{
    private CompiledRule[] _rules = [];

    public int Count => _rules.Length;

    public void SetRules(IEnumerable<SupplierRuleRow> rules)
    {
        _rules = rules
            .Select(rule => new CompiledRule(
                ArabicText.Normalize(rule.Keyword),
                AppDatabase.CleanName(rule.SupplierName),
                NormalizeUnit(rule.Unit),
                rule.Priority))
            .Where(rule => rule.Keyword.Length > 0 && rule.SupplierName.Length > 0)
            .OrderByDescending(rule => rule.Priority)
            .ThenByDescending(rule => rule.Keyword.Length)
            .ToArray();
    }

    /// <summary>يعيد القاعدة المطابقة لاسم المنتج، أو null إذا لم تنطبق أي قاعدة.</summary>
    public RuleMatch? FindMatch(string? productName)
    {
        if (_rules.Length == 0 || string.IsNullOrWhiteSpace(productName))
        {
            return null;
        }

        var normalizedProduct = ArabicText.Normalize(productName);
        foreach (var rule in _rules)
        {
            if (normalizedProduct.Contains(rule.Keyword, StringComparison.Ordinal))
            {
                return new RuleMatch(rule.SupplierName, rule.Unit);
            }
        }

        return null;
    }

    /// <summary>يقبل حبه أو كرز فقط، وأي قيمة أخرى تعني عدم تغيير الوحدة.</summary>
    public static string NormalizeUnit(string? unit)
    {
        var cleaned = AppDatabase.CleanName(unit);
        return cleaned is OrderItemRow.UnitHaba or OrderItemRow.UnitKarz ? cleaned : string.Empty;
    }

    private sealed record CompiledRule(string Keyword, string SupplierName, string Unit, int Priority);
}

/// <summary>نتيجة مطابقة قاعدة: المورد، والوحدة (فارغة إذا لم تحدد القاعدة وحدة).</summary>
public sealed record RuleMatch(string SupplierName, string Unit);
