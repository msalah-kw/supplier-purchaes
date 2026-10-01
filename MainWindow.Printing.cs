using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SupplierPurchases.Models;

namespace SupplierPurchases;

/// <summary>بناء مستندات قوائم الشراء والتقارير الشهرية وطباعتها أو حفظها كصورة.</summary>
public partial class MainWindow
{
    private static readonly Brush AlternateRowBrush = new SolidColorBrush(Color.FromRgb(245, 245, 245));

    private static readonly ReportColumn[] PurchaseColumns =
    [
        new("م", 40, TextAlignment.Center, item => item.RowNumber.ToString()),
        new("اسم المنتج", 280, TextAlignment.Left, item => item.ProductName),
        new("الكمية", 80, TextAlignment.Center, item => item.QuantityText),
        new("الوحدة", 80, TextAlignment.Center, item => item.Unit),
        new("ملاحظات", 170, TextAlignment.Left, item => item.Notes)
    ];

    private static readonly ReportColumn[] MonthlyColumns =
    [
        new("م", 40, TextAlignment.Center, item => item.RowNumber.ToString()),
        new("اسم المنتج", 200, TextAlignment.Left, item => item.ProductName),
        new("الكمية", 70, TextAlignment.Center, item => item.QuantityText),
        new("الوحدة", 60, TextAlignment.Center, item => item.Unit),
        new("الفروع الطالبة وتفاصيلها", 180, TextAlignment.Left, item => item.Sources),
        new("ملاحظات مجمعة", 100, TextAlignment.Left, item => item.Notes)
    ];

    private void PrintPurchaseButton_Click(object sender, RoutedEventArgs e)
    {
        var document = ResolveCurrentDocument();
        if (document is null)
        {
            return;
        }

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            document.PageWidth = printDialog.PrintableAreaWidth;
            document.PageHeight = printDialog.PrintableAreaHeight;
            document.PagePadding = new Thickness(42);
            printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "طباعة تقرير المشتريات");
            SetStatus("تم إرسال المستند للطباعة.");
        }
        catch (Exception ex)
        {
            ShowError($"حدث خطأ أثناء الاتصال بالطابعة. يرجى التأكد من توصيل الطابعة وتكرار المحاولة.\nتفاصيل الخطأ: {ex.Message}");
        }
    }

    private void SavePurchaseAsPngButton_Click(object sender, RoutedEventArgs e)
    {
        var document = ResolveCurrentDocument();
        if (document is null)
        {
            return;
        }

        var isMonthlyReport = PreviewPanel.Visibility == Visibility.Visible && _previewBackPanel == MonthlyReportsPanel;
        var defaultFileName = isMonthlyReport && TryGetSelectedYearMonth(out var year, out var month)
            ? $"تقرير_شهري_{_currentSupplierName}_{year}_{month}.png"
            : $"قائمة_شراء_{_currentSupplierName}_{DateTime.Today:yyyy-MM-dd}.png";

        var image = RenderDocumentToImage(document);
        if (image is null)
        {
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            FileName = defaultFileName
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = File.Create(saveDialog.FileName);
            encoder.Save(stream);

            SetStatus("تم حفظ التقرير كصورة PNG بنجاح.");
            ShowInfo("تم حفظ الصورة بنجاح.");
        }
        catch (Exception ex)
        {
            ShowError($"حدث خطأ أثناء حفظ الصورة:\n{ex.Message}");
        }
    }

    /// <summary>يعيد مستند المعاينة الحالي، أو ينشئ قائمة شراء جديدة إذا لم تكن المعاينة مفتوحة.</summary>
    private FlowDocument? ResolveCurrentDocument()
    {
        if (PreviewPanel.Visibility == Visibility.Visible && PrintPreviewViewer.Document is FlowDocument document)
        {
            return document;
        }

        return EnsurePurchaseList() ? BuildPurchaseDocument() : null;
    }

    private FlowDocument BuildPurchaseDocument()
    {
        return BuildReportDocument(
            title: null,
            headerLines:
            [
                ("اسم المورد: ", _currentSupplierName),
                ("التاريخ: ", DateTime.Today.ToString("yyyy/MM/dd"))
            ],
            columns: PurchaseColumns,
            items: PurchaseItems,
            footer: $"إجمالي عدد الأصناف: {PurchaseItems.Count} صنف");
    }

    private static FlowDocument BuildMonthlyPurchaseDocument(string supplierName, string yearMonth, List<PurchaseItem> items)
    {
        var parts = yearMonth.Split('-');
        var year = parts[0];
        var monthName = GetMonthName(parts.Length > 1 ? parts[1] : string.Empty);

        return BuildReportDocument(
            title: "التقرير المجمع الشهري لحركة المشتريات",
            headerLines:
            [
                ("اسم المورد: ", supplierName),
                ("شهر التقرير: ", $"{monthName} {year}"),
                ("تاريخ استخراج التقرير: ", DateTime.Today.ToString("yyyy/MM/dd"))
            ],
            columns: MonthlyColumns,
            items: items,
            footer: $"إجمالي عدد الأصناف المطلوبة طوال الشهر: {items.Count} صنف");
    }

    private static FlowDocument BuildReportDocument(
        string? title,
        IReadOnlyList<(string Label, string Value)> headerLines,
        IReadOnlyList<ReportColumn> columns,
        IReadOnlyList<PurchaseItem> items,
        string footer)
    {
        var document = new FlowDocument
        {
            FlowDirection = FlowDirection.RightToLeft,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            PagePadding = new Thickness(48),
            ColumnWidth = double.PositiveInfinity
        };

        // ── رأس التقرير ──
        // في الاتجاه من اليمين لليسار تعني المحاذاة لليسار بداية السطر أي جهة اليمين في الصفحة
        var headerParagraph = new Paragraph
        {
            FlowDirection = FlowDirection.RightToLeft,
            TextAlignment = TextAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10),
            LineHeight = 24
        };

        if (!string.IsNullOrEmpty(title))
        {
            headerParagraph.Inlines.Add(new Run(title + "\n") { FontWeight = FontWeights.Bold, FontSize = 18, Foreground = Brushes.Black });
        }

        var headerFontSize = title is null ? 16 : 15;
        for (var i = 0; i < headerLines.Count; i++)
        {
            var (label, value) = headerLines[i];
            var suffix = i == headerLines.Count - 1 ? string.Empty : "\n";
            headerParagraph.Inlines.Add(new Run(label) { FontWeight = FontWeights.Bold, FontSize = headerFontSize, Foreground = Brushes.Black });
            headerParagraph.Inlines.Add(new Run(value + suffix) { FontSize = headerFontSize, Foreground = Brushes.Black });
        }

        document.Blocks.Add(headerParagraph);

        // ── خط فاصل ──
        document.Blocks.Add(new BlockUIContainer(new Border
        {
            Height = 3.5,
            Background = Brushes.Black,
            Margin = new Thickness(0, 5, 0, 20)
        }));

        // ── جدول المنتجات ──
        var table = new Table
        {
            CellSpacing = 0,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1.5),
            Margin = new Thickness(0, 0, 0, 10)
        };

        foreach (var column in columns)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(column.Width) });
        }

        var rowGroup = new TableRowGroup();

        var headerRow = new TableRow();
        foreach (var column in columns)
        {
            headerRow.Cells.Add(CreateTableCell(column.Header, isHeader: true, TextAlignment.Center));
        }

        rowGroup.Rows.Add(headerRow);

        var alternate = false;
        foreach (var item in items)
        {
            var row = new TableRow();
            foreach (var column in columns)
            {
                row.Cells.Add(CreateTableCell(column.Value(item), isHeader: false, column.Alignment, alternate));
            }

            rowGroup.Rows.Add(row);
            alternate = !alternate;
        }

        table.RowGroups.Add(rowGroup);
        document.Blocks.Add(table);

        document.Blocks.Add(new Paragraph(new Run(footer))
        {
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            TextAlignment = TextAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0)
        });

        return document;
    }

    private static TableCell CreateTableCell(
        string text,
        bool isHeader = false,
        TextAlignment alignment = TextAlignment.Right,
        bool alternate = false)
    {
        return new TableCell(new Paragraph(new Run(text))
        {
            Margin = new Thickness(0),
            TextAlignment = alignment,
            FlowDirection = FlowDirection.RightToLeft
        })
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
            Foreground = Brushes.Black,
            Background = !isHeader && alternate ? AlternateRowBrush : Brushes.White
        };
    }

    /// <summary>يرسم المستند في صفحة واحدة طويلة حتى تظهر كل الأصناف داخل صورة واحدة.</summary>
    private static RenderTargetBitmap? RenderDocumentToImage(FlowDocument document)
    {
        document.PageWidth = 816;
        document.PagePadding = new Thickness(42);
        document.ColumnWidth = double.PositiveInfinity;

        var rowCount = CountTableRows(document);
        var pageHeight = 320 + (rowCount * 42);

        IDocumentPaginatorSource source = document;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            document.PageHeight = pageHeight;
            var paginator = source.DocumentPaginator;
            paginator.ComputePageCount();

            if (paginator.PageCount <= 1)
            {
                break;
            }

            pageHeight += 200;
        }

        var documentPaginator = source.DocumentPaginator;
        documentPaginator.ComputePageCount();
        if (documentPaginator.PageCount == 0)
        {
            return null;
        }

        var page = documentPaginator.GetPage(0);
        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            var area = new Rect(new Point(), page.Size);
            context.DrawRectangle(Brushes.White, null, area);
            context.DrawRectangle(new VisualBrush(page.Visual), null, area);
        }

        var target = new RenderTargetBitmap(
            (int)Math.Ceiling(page.Size.Width),
            (int)Math.Ceiling(page.Size.Height),
            96,
            96,
            PixelFormats.Pbgra32);

        target.Render(drawingVisual);
        return target;
    }

    private static int CountTableRows(FlowDocument document)
    {
        foreach (var block in document.Blocks)
        {
            if (block is Table table && table.RowGroups.Count > 0)
            {
                return Math.Max(table.RowGroups[0].Rows.Count - 1, 0);
            }
        }

        return 10;
    }

    private static string GetMonthName(string monthNumber) => monthNumber switch
    {
        "01" => "يناير",
        "02" => "فبراير",
        "03" => "مارس",
        "04" => "أبريل",
        "05" => "مايو",
        "06" => "يونيو",
        "07" => "يوليو",
        "08" => "أغسطس",
        "09" => "سبتمبر",
        "10" => "أكتوبر",
        "11" => "نوفمبر",
        "12" => "ديسمبر",
        _ => monthNumber
    };

    private sealed record ReportColumn(string Header, double Width, TextAlignment Alignment, Func<PurchaseItem, string> Value);
}
