using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases;

/// <summary>بناء مستندات قوائم الشراء والتقارير الشهرية وطباعتها أو حفظها كصورة.</summary>
public partial class MainWindow
{
    private static readonly Brush AlternateRowBrush = new SolidColorBrush(Color.FromRgb(243, 245, 248));
    private static readonly Brush HeaderRowBrush = new SolidColorBrush(Color.FromRgb(31, 41, 55));
    private static readonly Brush TotalRowBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
    private static readonly Brush GridLineBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));

    /// <summary>هامش الصفحة حول المحتوى في الصورة والمعاينة.</summary>
    private const double ReportPadding = 32;

    /// <summary>دقة الصورة المحفوظة: ضعف دقة الشاشة حتى تبقى واضحة عند التكبير على الجوال.</summary>
    private const double ImageScale = 2;

    // ترتيب الأعمدة ثابت في كل التقارير: م، اسم المنتج، الكمية، الوحدة، ثم أي أعمدة إضافية.
    // أسطر الإجمالي في أسفل الجدول تعتمد على هذا الترتيب.
    private static readonly ReportColumn[] PurchaseColumns =
    [
        new("م", 46, TextAlignment.Center, item => item.RowNumber.ToString()),
        new("اسم المنتج", 380, TextAlignment.Left, item => item.ProductName),
        new("الكمية", 90, TextAlignment.Center, item => item.QuantityText),
        new("الوحدة", 90, TextAlignment.Center, item => item.Unit)
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
            title: "طلب شراء",
            headerLines:
            [
                ("اسم المورد: ", _currentSupplierName),
                ("تاريخ الطلبيات: ", _currentPurchaseDate.ToString("yyyy/MM/dd"))
            ],
            columns: PurchaseColumns,
            items: PurchaseItems);
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
            items: items);
    }

    private static FlowDocument BuildReportDocument(
        string title,
        IReadOnlyList<(string Label, string Value)> headerLines,
        IReadOnlyList<ReportColumn> columns,
        IReadOnlyList<PurchaseItem> items)
    {
        var document = new FlowDocument
        {
            FlowDirection = FlowDirection.RightToLeft,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 15,
            PagePadding = new Thickness(ReportPadding),
            ColumnWidth = double.PositiveInfinity,
            Foreground = Brushes.Black
        };

        document.Blocks.Add(new Paragraph(new Run(title))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 14)
        });

        // في الاتجاه من اليمين لليسار تعني المحاذاة لليسار بداية السطر أي جهة اليمين في الصفحة
        var headerParagraph = new Paragraph
        {
            TextAlignment = TextAlignment.Left,
            Margin = new Thickness(0, 0, 0, 12),
            LineHeight = 26,
            FontSize = 16
        };

        for (var i = 0; i < headerLines.Count; i++)
        {
            var (label, value) = headerLines[i];
            headerParagraph.Inlines.Add(new Run(label) { FontWeight = FontWeights.Bold });
            headerParagraph.Inlines.Add(new Run(value));
            if (i < headerLines.Count - 1)
            {
                headerParagraph.Inlines.Add(new LineBreak());
            }
        }

        document.Blocks.Add(headerParagraph);

        var table = new Table
        {
            CellSpacing = 0,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1.5),
            Margin = new Thickness(0)
        };

        foreach (var column in columns)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(column.Width) });
        }

        var rowGroup = new TableRowGroup();

        var headerRow = new TableRow();
        foreach (var column in columns)
        {
            headerRow.Cells.Add(CreateTableCell(column.Header, TextAlignment.Center, HeaderRowBrush, Brushes.White, FontWeights.Bold));
        }

        rowGroup.Rows.Add(headerRow);

        var alternate = false;
        foreach (var item in items)
        {
            var row = new TableRow();
            foreach (var column in columns)
            {
                row.Cells.Add(CreateTableCell(column.Value(item), column.Alignment, alternate ? AlternateRowBrush : Brushes.White));
            }

            rowGroup.Rows.Add(row);
            alternate = !alternate;
        }

        // أسطر الإجمالي: العنوان تحت عمودي م واسم المنتج، والقيمة تحت الكمية، والوحدة تحت الوحدة
        foreach (var total in PurchaseTotals.ByUnit(items))
        {
            var row = new TableRow();
            row.Cells.Add(CreateTableCell(total.Label, TextAlignment.Left, TotalRowBrush, fontWeight: FontWeights.Bold, columnSpan: 2));
            row.Cells.Add(CreateTableCell(total.TotalText, TextAlignment.Center, TotalRowBrush, fontWeight: FontWeights.Bold));
            row.Cells.Add(CreateTableCell(total.Unit, TextAlignment.Center, TotalRowBrush, fontWeight: FontWeights.Bold));
            if (columns.Count > 4)
            {
                row.Cells.Add(CreateTableCell(string.Empty, TextAlignment.Center, TotalRowBrush, columnSpan: columns.Count - 4));
            }

            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        document.Blocks.Add(table);

        return document;
    }

    private static TableCell CreateTableCell(
        string text,
        TextAlignment alignment,
        Brush background,
        Brush? foreground = null,
        FontWeight? fontWeight = null,
        int columnSpan = 1)
    {
        return new TableCell(new Paragraph(new Run(text))
        {
            Margin = new Thickness(0),
            TextAlignment = alignment
        })
        {
            ColumnSpan = columnSpan,
            BorderBrush = GridLineBrush,
            BorderThickness = new Thickness(0.75),
            Padding = new Thickness(10, 7, 10, 7),
            FontWeight = fontWeight ?? FontWeights.Normal,
            Foreground = foreground ?? Brushes.Black,
            Background = background
        };
    }

    /// <summary>
    /// يرسم المستند في صورة واحدة بحجم المحتوى الفعلي: العرض حسب أعمدة الجدول،
    /// والارتفاع ينتهي بعد آخر سطر بدون فراغ زائد مهما كان عدد الأصناف.
    /// </summary>
    private static RenderTargetBitmap? RenderDocumentToImage(FlowDocument document)
    {
        var tableWidth = document.Blocks.OfType<Table>().FirstOrDefault()?.Columns.Sum(column => column.Width.Value) ?? 700;
        var pageWidth = Math.Ceiling(tableWidth + (ReportPadding * 2) + 4);

        document.PageWidth = pageWidth;
        document.PagePadding = new Thickness(ReportPadding);
        document.ColumnWidth = double.PositiveInfinity;

        // أقل ارتفاع صفحة يدخل فيه كل المحتوى في صفحة واحدة، بالبحث الثنائي
        IDocumentPaginatorSource source = document;
        var paginator = source.DocumentPaginator;

        int PageCountAt(double height)
        {
            document.PageHeight = height;
            paginator.ComputePageCount();
            return paginator.PageCount;
        }

        var high = 2000.0;
        while (PageCountAt(high) > 1 && high < 400000)
        {
            high *= 2;
        }

        var low = ReportPadding * 2;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            if (PageCountAt(middle) > 1)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        if (PageCountAt(Math.Ceiling(high)) == 0)
        {
            return null;
        }

        var page = paginator.GetPage(0);
        var area = new Rect(0, 0, pageWidth, Math.Ceiling(high));

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, area);
            context.DrawRectangle(new VisualBrush(page.Visual)
            {
                Stretch = Stretch.None,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = area,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = area
            }, null, area);
        }

        var target = new RenderTargetBitmap(
            (int)Math.Ceiling(area.Width * ImageScale),
            (int)Math.Ceiling(area.Height * ImageScale),
            96 * ImageScale,
            96 * ImageScale,
            PixelFormats.Pbgra32);

        target.Render(drawingVisual);
        return target;
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
