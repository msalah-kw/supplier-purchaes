using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases;

/// <summary>شاشة الطلبيات المحفوظة: العرض والتعديل والحذف.</summary>
public partial class MainWindow
{
    private void OrdersFilter_Changed(object? sender, EventArgs e)
    {
        ApplyOrdersFilter();
    }

    private void ClearOrdersFilterButton_Click(object sender, RoutedEventArgs e)
    {
        OrdersSearchBox.Text = string.Empty;
        OrdersFromDatePicker.SelectedDate = null;
        OrdersToDatePicker.SelectedDate = null;
    }

    /// <summary>يعرض الطلبيات المطابقة للبحث ونطاق التاريخ.</summary>
    private void ApplyOrdersFilter()
    {
        // الأحداث تنطلق أثناء تحميل الواجهة قبل إنشاء كل العناصر
        if (OrdersSearchBox is null || OrdersFromDatePicker is null || OrdersToDatePicker is null)
        {
            return;
        }

        var matches = OrderFilter.Apply(
            _allOrders,
            OrdersSearchBox.Text,
            OrdersFromDatePicker.SelectedDate,
            OrdersToDatePicker.SelectedDate);

        ReplaceCollection(Orders, matches);
        OrdersFilterSummaryText.Text = matches.Count == _allOrders.Count
            ? $"عدد الطلبيات: {_allOrders.Count}"
            : $"عدد الطلبيات المطابقة: {matches.Count} من {_allOrders.Count}";
    }

    private void OrdersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (OrdersGrid.SelectedItem is OrderSummary)
        {
            EditOrderButton_Click(sender, e);
        }
    }

    private void EditOrderButton_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not OrderSummary selected)
        {
            ShowInfo("اختر طلبية من الجدول أولًا.");
            return;
        }

        var detail = _database.GetOrderDetail(selected.Id);
        if (detail is null)
        {
            ShowInfo("لم يتم العثور على الطلبية المحددة.");
            LoadOrders();
            return;
        }

        _editingOrderId = detail.Id;
        BranchNameBox.Text = detail.BranchName;
        OrderDatePicker.SelectedDate = detail.OrderDate;
        OrderNotesBox.Text = detail.Notes;
        SetCurrentItems(detail.Items.Count > 0 ? detail.Items : [new OrderItemRow()]);

        SetPage("تعديل طلبية", "عدّل بيانات الفرع والمنتجات ثم احفظ التغييرات.");
        ShowOnly(OrderEditorPanel);
    }

    private void DeleteOrderButton_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not OrderSummary selected)
        {
            ShowInfo("اختر طلبية من الجدول أولًا.");
            return;
        }

        if (!Confirm($"هل تريد حذف طلبية فرع \"{selected.BranchName}\"؟"))
        {
            return;
        }

        _database.DeleteOrder(selected.Id);
        LoadOrders();
        SetStatus("تم حذف الطلبية.");
    }
}
