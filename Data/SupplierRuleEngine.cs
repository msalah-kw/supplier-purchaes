using SupplierPurchases.Models;

namespace SupplierPurchases.Data;

/// <summary>
/// يطبّق قواعد الإسناد التلقائي: أي منتج يحتوي اسمه على كلمة مفتاحية معينة
/// يُسند إلى المورد المرتبط بها. القاعدة ذات الأولوية الأعلى ثم الكلمة الأطول تفوز.
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
                rule.Priority))
            .Where(rule => rule.Keyword.Length > 0 && rule.SupplierName.Length > 0)
            .OrderByDescending(rule => rule.Priority)
            .ThenByDescending(rule => rule.Keyword.Length)
            .ToArray();
    }

    /// <summary>يعيد اسم المورد المطابق لاسم المنتج، أو null إذا لم تنطبق أي قاعدة.</summary>
    public string? FindSupplier(string? productName)
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
                return rule.SupplierName;
            }
        }

        return null;
    }

    private sealed record CompiledRule(string Keyword, string SupplierName, int Priority);
}
