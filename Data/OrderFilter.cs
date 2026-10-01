using SupplierPurchases.Models;

namespace SupplierPurchases.Data;

/// <summary>فلترة قائمة الطلبيات بالبحث في اسم الفرع والملاحظات، وبنطاق تاريخ.</summary>
public static class OrderFilter
{
    /// <summary>
    /// كل كلمات البحث يجب أن تظهر في اسم الفرع أو الملاحظات، مع تجاهل فروق الكتابة العربية.
    /// حدود التاريخ شاملة، وأي حد فارغ يعني بدون قيد.
    /// </summary>
    public static List<OrderSummary> Apply(IEnumerable<OrderSummary> orders, string? searchText, DateTime? fromDate, DateTime? toDate)
    {
        var keywords = ArabicText.Normalize(searchText).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return orders
            .Where(order => fromDate is null || order.OrderDate.Date >= fromDate.Value.Date)
            .Where(order => toDate is null || order.OrderDate.Date <= toDate.Value.Date)
            .Where(order =>
            {
                if (keywords.Length == 0)
                {
                    return true;
                }

                var text = ArabicText.Normalize($"{order.BranchName} {order.Notes}");
                return keywords.All(keyword => text.Contains(keyword, StringComparison.Ordinal));
            })
            .ToList();
    }
}
