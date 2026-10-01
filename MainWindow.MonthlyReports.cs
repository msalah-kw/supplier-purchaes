using System.Windows;
using System.Windows.Controls;
using SupplierPurchases.Models;

namespace SupplierPurchases;

/// <summary>شاشة التقارير الشهرية للموردين.</summary>
public partial class MainWindow
{
    private void GenerateMonthlyReportButton_Click(object sender, RoutedEventArgs e)
    {
        GenerateMonthlyReportList();
    }

    private void GenerateMonthlyReportList()
    {
        if (!TryGetSelectedYearMonth(out var year, out var month))
        {
            ShowInfo("يرجى اختيار السنة والشهر أولاً.");
            return;
        }

        var data = _database.GetMonthlySuppliersSummary($"{year}-{month}");
        ReplaceCollection(MonthlySuppliers, data);

        var monthName = GetMonthName(month);
        MonthlyReportsSummaryText.Text = data.Count == 0
            ? $"لا توجد مشتريات مسجلة لشهر {monthName} {year}."
            : $"ملخص شهر {monthName} {year}: تم التعامل مع {data.Count} موردين.";

        SetStatus($"تم تحديث تقرير شهر {monthName} {year}");
    }

    private void PreviewMonthlySupplierButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not MonthlySupplierSummary selected ||
            !TryGetSelectedYearMonth(out var year, out var month))
        {
            return;
        }

        var yearMonth = $"{year}-{month}";
        var items = _database.GetMonthlyPurchaseList(selected.SupplierName, yearMonth);
        if (items.Count == 0)
        {
            ShowInfo("لا توجد تفاصيل مشتريات مسجلة لهذا المورد في الشهر المحدد.");
            return;
        }

        _currentSupplierName = selected.SupplierName;
        _previewBackPanel = MonthlyReportsPanel;
        PrintPreviewViewer.Document = BuildMonthlyPurchaseDocument(selected.SupplierName, yearMonth, items);

        SetPage("معاينة التقرير الشهري",
            $"الشكل المجمع لمشتريات المورد {selected.SupplierName} لشهر {GetMonthName(month)} {year}.");
        ShowOnly(PreviewPanel);
    }

    private bool TryGetSelectedYearMonth(out string year, out string month)
    {
        year = (MonthlyYearCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        month = (MonthlyMonthCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
        return year.Length > 0 && month.Length > 0;
    }

    private void SelectCurrentMonthFilters()
    {
        LoadReportYears();

        var currentMonth = DateTime.Today.Month.ToString("00");
        foreach (ComboBoxItem item in MonthlyMonthCombo.Items)
        {
            item.IsSelected = item.Tag?.ToString() == currentMonth;
        }
    }

    /// <summary>يملأ قائمة السنوات من الطلبيات المحفوظة مع السنة الحالية، ويحافظ على السنة المختارة.</summary>
    private void LoadReportYears()
    {
        var selectedYear = (MonthlyYearCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
                           ?? DateTime.Today.Year.ToString();

        var years = _database.GetOrderYears()
            .Append(DateTime.Today.Year)
            .Distinct()
            .OrderByDescending(year => year);

        MonthlyYearCombo.Items.Clear();
        foreach (var year in years)
        {
            var text = year.ToString();
            MonthlyYearCombo.Items.Add(new ComboBoxItem { Content = text, IsSelected = text == selectedYear });
        }

        if (MonthlyYearCombo.SelectedIndex < 0 && MonthlyYearCombo.Items.Count > 0)
        {
            MonthlyYearCombo.SelectedIndex = 0;
        }
    }
}
