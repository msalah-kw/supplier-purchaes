using System.Windows;
using System.Windows.Controls;
using SupplierPurchases.Models;

namespace SupplierPurchases;

/// <summary>شاشة قائمة الشراء لمورد واحد في تاريخ محدد.</summary>
public partial class MainWindow
{
    private void GeneratePurchaseButton_Click(object sender, RoutedEventArgs e)
    {
        GeneratePurchaseList(showMessageWhenEmpty: true);
    }

    private void PreviewPurchaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsurePurchaseList())
        {
            return;
        }

        _previewBackPanel = PurchasePanel;
        PrintPreviewViewer.Document = BuildPurchaseDocument();
        SetPage("معاينة الطباعة", "الشكل النهائي لقائمة الشراء قبل إرسالها للطابعة.");
        ShowOnly(PreviewPanel);
    }

    private void PreviewBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_previewBackPanel == MonthlyReportsPanel)
        {
            SetPage("التقارير الشهرية", "تقرير مجمع للمشتريات والأصناف المطلوبة من الموردين خلال الشهر.");
            ShowOnly(MonthlyReportsPanel);
            return;
        }

        PurchaseButton_Click(sender, e);
    }

    /// <summary>يعيد توليد قائمة الشراء دائمًا من قاعدة البيانات حتى تعكس أي تعديل حديث على الطلبيات.</summary>
    private bool EnsurePurchaseList()
    {
        return GeneratePurchaseList(showMessageWhenEmpty: true);
    }

    /// <summary>يحدّث قائمة الشراء تلقائيًا إذا كانت شاشتها ظاهرة ومورد محدد، دون رسائل مزعجة.</summary>
    private void RefreshPurchaseListIfVisible()
    {
        if (PurchasePanel.Visibility == Visibility.Visible &&
            !string.IsNullOrWhiteSpace(GetSelectedSupplierName()))
        {
            GeneratePurchaseList(showMessageWhenEmpty: false);
        }
    }

    private void SupplierCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshPurchaseListIfVisible();
    }

    private void PurchaseDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshPurchaseListIfVisible();
    }

    private bool GeneratePurchaseList(bool showMessageWhenEmpty)
    {
        var supplierName = GetSelectedSupplierName();
        if (string.IsNullOrWhiteSpace(supplierName))
        {
            if (showMessageWhenEmpty)
            {
                ShowInfo("اختر موردًا أولًا.");
            }

            return false;
        }

        var date = PurchaseDatePicker.SelectedDate ?? DateTime.Today;
        var items = _database.GetPurchaseList(supplierName, date);
        ReplaceCollection(PurchaseItems, items);
        _currentSupplierName = supplierName;
        _currentPurchaseDate = date;

        PurchaseSummaryText.Text = items.Count == 0
            ? $"لا توجد منتجات مطلوبة من {supplierName}."
            : $"قائمة {supplierName}: {items.Count} صنف، إجمالي الكميات حسب الوحدة.";

        if (items.Count == 0)
        {
            if (showMessageWhenEmpty)
            {
                ShowInfo("لا توجد منتجات لهذا المورد.");
            }

            return false;
        }

        SetStatus($"تم إنشاء قائمة شراء للمورد: {supplierName}");
        return true;
    }

    private string GetSelectedSupplierName()
    {
        return SupplierCombo.SelectedItem is SupplierRow supplier
            ? supplier.Name
            : SupplierCombo.SelectedValue as string ?? string.Empty;
    }
}
