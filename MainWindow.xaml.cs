using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SupplierPurchases.Controls;
using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases;

public partial class MainWindow : Window
{
    private readonly AppDatabase _database;
    private readonly SupplierRuleEngine _ruleEngine = new();
    private readonly DispatcherTimer _rulesSaveTimer;
    private readonly List<SavedProductRow> _savedProducts = [];

    private int? _editingOrderId;
    private string _currentSupplierName = string.Empty;
    private UIElement? _previewBackPanel;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _database = new AppDatabase();
        _database.EnsureCreated();
        DatabasePathText.Text = _database.DatabasePath;

        _rulesSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _rulesSaveTimer.Tick += RulesSaveTimer_Tick;

        CurrentItems.CollectionChanged += CurrentItems_CollectionChanged;

        SelectCurrentMonthFilters();

        LoadSuggestions();
        LoadRules();
        LoadSupplierRows();
        LoadOrders();

        ShowDashboard();
    }

    public ObservableCollection<OrderItemRow> CurrentItems { get; } = [];

    public ObservableCollection<OrderSummary> Orders { get; } = [];

    public ObservableCollection<SupplierRow> SupplierRows { get; } = [];

    public ObservableCollection<SavedProductRow> FilteredSavedProducts { get; } = [];

    public ObservableCollection<PurchaseItem> PurchaseItems { get; } = [];

    public ObservableCollection<MonthlySupplierSummary> MonthlySuppliers { get; } = [];

    public ObservableCollection<SupplierRuleRow> Rules { get; } = [];

    /// <summary>مصدر اقتراحات أسماء المنتجات المستخدم في كل حقول الإكمال التلقائي.</summary>
    public SuggestionSource ProductSource { get; } = new();

    /// <summary>مصدر اقتراحات أسماء الموردين.</summary>
    public SuggestionSource SupplierSource { get; } = new();

    // ─────────────────────────────  التنقل بين الشاشات  ─────────────────────────────

    private void DashboardButton_Click(object sender, RoutedEventArgs e)
    {
        LoadOrders();
        ShowDashboard();
    }

    private void NewOrderButton_Click(object sender, RoutedEventArgs e)
    {
        _editingOrderId = null;
        BranchNameBox.Text = string.Empty;
        OrderDatePicker.SelectedDate = DateTime.Today;
        OrderNotesBox.Text = string.Empty;
        SetCurrentItems([new OrderItemRow()]);

        SetPage("طلبية جديدة", "أدخل طلبية الفرع وحدد المورد لكل منتج.");
        ShowOnly(OrderEditorPanel);
        BranchNameBox.Focus();
    }

    private void OrdersButton_Click(object sender, RoutedEventArgs e)
    {
        LoadOrders();
        SetPage("الطلبيات", "عرض الطلبيات المحفوظة وتعديلها أو حذفها.");
        ShowOnly(OrdersPanel);
    }

    private void PurchaseButton_Click(object sender, RoutedEventArgs e)
    {
        LoadSupplierRows();
        SetPage("قائمة شراء", "اختر موردًا لإنشاء قائمة شراء مجمعة من كل الطلبيات.");
        ShowOnly(PurchasePanel);

        if (SupplierCombo.SelectedItem is null && SupplierRows.Count > 0)
        {
            SupplierCombo.SelectedIndex = 0;
        }

        PurchaseDatePicker.SelectedDate ??= DateTime.Today;

        // إعادة توليد القائمة دائمًا عند فتح الشاشة حتى تعكس آخر تعديلات الطلبيات.
        RefreshPurchaseListIfVisible();
    }

    private void SuppliersButton_Click(object sender, RoutedEventArgs e)
    {
        LoadSupplierRows();
        SetPage("الموردون", "تعديل أسماء الموردين أو حذف غير المستخدم منهم.");
        ShowOnly(SuppliersPanel);
    }

    private void RulesButton_Click(object sender, RoutedEventArgs e)
    {
        LoadRules();
        SetPage("قواعد الإسناد", "اربط كلمات مفتاحية بموردين ليتم الإسناد تلقائيًا أثناء إدخال الطلبيات.");
        ShowOnly(RulesPanel);
    }

    private void ImportProductsButton_Click(object sender, RoutedEventArgs e)
    {
        ImportProductsTextBox.Text = string.Empty;
        SetPage("استيراد المنتجات", "أدخل أسماء المنتجات الجديدة لتغذية القائمة المنسدلة الذكية.");
        ShowOnly(ImportProductsPanel);
        ImportProductsTextBox.Focus();
    }

    private void MonthlyReportsButton_Click(object sender, RoutedEventArgs e)
    {
        SetPage("التقارير الشهرية", "تقرير مجمع للمشتريات والأصناف المطلوبة من الموردين خلال الشهر.");
        ShowOnly(MonthlyReportsPanel);
        GenerateMonthlyReportList();
    }

    private void ShowDashboard()
    {
        SetPage("الرئيسية", "ملخص سريع لحركة الطلبيات وقوائم الشراء.");
        ShowOnly(DashboardPanel);
    }

    private void ShowOnly(UIElement visiblePanel)
    {
        UIElement[] panels =
        [
            DashboardPanel, OrderEditorPanel, OrdersPanel, PurchasePanel,
            SuppliersPanel, RulesPanel, PreviewPanel, ImportProductsPanel, MonthlyReportsPanel
        ];

        foreach (var panel in panels)
        {
            panel.Visibility = panel == visiblePanel ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ─────────────────────────────  تحميل البيانات  ─────────────────────────────

    private void LoadOrders()
    {
        var stats = _database.GetDashboardStats();
        OrdersCountText.Text = stats.OrderCount.ToString();
        SuppliersCountText.Text = stats.SupplierCount.ToString();
        ProductsCountText.Text = stats.ProductCount.ToString();

        ReplaceCollection(Orders, _database.GetOrderSummaries());
    }

    private void LoadSuggestions()
    {
        LoadProductSuggestions();
        LoadSupplierSuggestions();
    }

    private void LoadProductSuggestions()
    {
        var products = _database.GetSavedProducts();
        ProductSource.SetItems(products);

        _savedProducts.Clear();
        _savedProducts.AddRange(products.Select(name => new SavedProductRow(name)));

        ApplySavedProductsFilter();
    }

    private void LoadSupplierSuggestions()
    {
        SupplierSource.SetItems(_database.GetSavedSuppliers());
    }

    private void LoadSupplierRows()
    {
        var selectedName = (SupplierCombo.SelectedItem as SupplierRow)?.Name;

        ReplaceCollection(SupplierRows, _database.GetSupplierRows());
        LoadSupplierSuggestions();

        if (selectedName is not null)
        {
            SupplierCombo.SelectedItem = SupplierRows.FirstOrDefault(row => row.Name == selectedName);
        }
    }

    private void LoadRules()
    {
        foreach (var rule in Rules)
        {
            rule.PropertyChanged -= Rule_PropertyChanged;
        }

        Rules.Clear();
        foreach (var rule in _database.GetSupplierRules())
        {
            rule.PropertyChanged += Rule_PropertyChanged;
            Rules.Add(rule);
        }

        _ruleEngine.SetRules(Rules);
    }

    // ─────────────────────────────  محرر الطلبية  ─────────────────────────────

    private void AddItemButton_Click(object sender, RoutedEventArgs e)
    {
        var row = new OrderItemRow();
        CurrentItems.Add(row);
        ItemsGrid.ScrollIntoView(row);
    }

    private void RemoveItemsButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = CurrentItems.Where(item => item.IsSelected).ToList();
        if (targets.Count == 0 && ItemsGrid.SelectedItem is OrderItemRow selected)
        {
            targets.Add(selected);
        }

        if (targets.Count == 0)
        {
            ShowInfo("حدد المنتجات المطلوب حذفها من مربعات الاختيار أولًا.");
            return;
        }

        foreach (var item in targets)
        {
            CurrentItems.Remove(item);
        }

        if (CurrentItems.Count == 0)
        {
            CurrentItems.Add(new OrderItemRow());
        }

        SetStatus($"تم حذف {targets.Count} منتج من الطلبية.");
    }

    private void SelectAllItemsButton_Click(object sender, RoutedEventArgs e) => SetAllItemsSelected(true);

    private void ClearItemsSelectionButton_Click(object sender, RoutedEventArgs e) => SetAllItemsSelected(false);

    private void SelectAllItemsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            SetAllItemsSelected(checkBox.IsChecked == true);
        }
    }

    private void SetAllItemsSelected(bool isSelected)
    {
        foreach (var item in CurrentItems)
        {
            item.IsSelected = isSelected;
        }
    }

    private void AssignHabaButton_Click(object sender, RoutedEventArgs e) => AssignUnitToSelection(OrderItemRow.UnitHaba);

    private void AssignKarzButton_Click(object sender, RoutedEventArgs e) => AssignUnitToSelection(OrderItemRow.UnitKarz);

    private void AssignUnitToSelection(string unit)
    {
        var targets = GetSelectedItems();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var item in targets)
        {
            item.Unit = unit;
        }

        SetStatus($"تم ضبط الوحدة \"{unit}\" لعدد {targets.Count} منتج.");
    }

    private void AssignSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        var supplierName = AppDatabase.CleanName(BulkSupplierBox.Text);
        if (supplierName.Length == 0)
        {
            ShowInfo("اكتب اسم المورد في خانة المورد بشريط العمليات أولًا.");
            return;
        }

        var targets = GetSelectedItems();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var item in targets)
        {
            item.SupplierName = supplierName;
        }

        RegisterSupplierName(supplierName);
        SetStatus($"تم إسناد {targets.Count} منتج إلى المورد \"{supplierName}\".");
    }

    /// <summary>يطبق قواعد الإسناد على المنتجات المحددة، أو على كل صف بدون مورد إذا لم يتم تحديد شيء.</summary>
    private void ApplyRulesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_ruleEngine.Count == 0)
        {
            ShowInfo("لا توجد قواعد إسناد معرفة بعد. أضفها من شاشة \"قواعد الإسناد\".");
            return;
        }

        var selected = CurrentItems.Where(item => item.IsSelected).ToList();
        var targets = selected.Count > 0
            ? selected
            : CurrentItems.Where(item => string.IsNullOrWhiteSpace(item.SupplierName)).ToList();

        var applied = 0;
        foreach (var item in targets)
        {
            var supplier = _ruleEngine.FindSupplier(item.ProductName);
            if (supplier is null || item.SupplierName == supplier)
            {
                continue;
            }

            item.SupplierName = supplier;
            applied++;
        }

        SetStatus(applied == 0
            ? "لم تنطبق أي قاعدة على المنتجات المستهدفة."
            : $"تم إسناد {applied} منتج تلقائيًا حسب القواعد.");
    }

    private List<OrderItemRow> GetSelectedItems()
    {
        var targets = CurrentItems.Where(item => item.IsSelected).ToList();
        if (targets.Count == 0)
        {
            ShowInfo("حدد منتجًا واحدًا على الأقل من مربعات الاختيار.");
        }

        return targets;
    }

    private void ProductBox_ValueCommitted(object sender, string value)
    {
        if (sender is not AutoCompleteTextBox box || box.DataContext is not OrderItemRow row)
        {
            return;
        }

        var productName = AppDatabase.CleanName(value);
        if (productName.Length == 0)
        {
            return;
        }

        RegisterProductName(productName);

        if (!string.IsNullOrWhiteSpace(row.SupplierName))
        {
            return;
        }

        var supplier = _ruleEngine.FindSupplier(productName);
        if (supplier is not null)
        {
            row.SupplierName = supplier;
            SetStatus($"تم إسناد \"{productName}\" إلى المورد \"{supplier}\" تلقائيًا.");
        }
    }

    private void SupplierBox_ValueCommitted(object sender, string value)
    {
        RegisterSupplierName(AppDatabase.CleanName(value));
    }

    private void RegisterProductName(string productName)
    {
        if (productName.Length == 0 || ProductSource.Contains(productName))
        {
            return;
        }

        try
        {
            _database.ImportProductNames([productName]);
            LoadProductSuggestions();
            SetStatus($"تم حفظ المنتج \"{productName}\" تلقائيًا.");
        }
        catch (Exception ex)
        {
            SetStatus($"تعذر حفظ المنتج: {ex.Message}");
        }
    }

    private void RegisterSupplierName(string supplierName)
    {
        if (supplierName.Length == 0 || SupplierSource.Contains(supplierName))
        {
            return;
        }

        try
        {
            _database.ImportSupplierNames([supplierName]);
            LoadSupplierSuggestions();
            SetStatus($"تم حفظ المورد \"{supplierName}\" تلقائيًا.");
        }
        catch (Exception ex)
        {
            SetStatus($"تعذر حفظ المورد: {ex.Message}");
        }
    }

    private void SaveOrderButton_Click(object sender, RoutedEventArgs e)
    {
        CommitFocusedEditor();

        try
        {
            _database.SaveOrder(
                _editingOrderId,
                BranchNameBox.Text,
                OrderDatePicker.SelectedDate ?? DateTime.Today,
                OrderNotesBox.Text,
                CurrentItems);

            SetStatus(_editingOrderId.HasValue ? "تم تحديث الطلبية." : "تم حفظ الطلبية.");
            LoadSuggestions();
            OrdersButton_Click(sender, e);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    /// <summary>يثبّت قيمة الحقل الذي به المؤشر حتى لا تضيع آخر كتابة قبل الحفظ.</summary>
    private static void CommitFocusedEditor()
    {
        if (Keyboard.FocusedElement is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }

    private void CurrentItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (OrderItemRow row in e.OldItems)
            {
                row.PropertyChanged -= OrderItem_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (OrderItemRow row in e.NewItems)
            {
                row.PropertyChanged += OrderItem_PropertyChanged;
            }
        }

        UpdateSelectedItemsCount();
    }

    private void OrderItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrderItemRow.IsSelected))
        {
            UpdateSelectedItemsCount();
        }
    }

    private void UpdateSelectedItemsCount()
    {
        var count = CurrentItems.Count(item => item.IsSelected);
        SelectedItemsCountText.Text = count == 0
            ? "لم يتم تحديد أي منتج"
            : $"عدد المنتجات المحددة: {count}";
    }

    private void SetCurrentItems(IEnumerable<OrderItemRow> items)
    {
        foreach (var row in CurrentItems)
        {
            row.PropertyChanged -= OrderItem_PropertyChanged;
        }

        CurrentItems.Clear();
        foreach (var row in items)
        {
            CurrentItems.Add(row);
        }
    }

    // ─────────────────────────────  الطلبيات المحفوظة  ─────────────────────────────

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

    // ─────────────────────────────  قائمة الشراء  ─────────────────────────────

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

    // ─────────────────────────────  التقارير الشهرية  ─────────────────────────────

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
        var currentYear = DateTime.Today.Year.ToString();
        var currentMonth = DateTime.Today.Month.ToString("00");

        foreach (ComboBoxItem item in MonthlyYearCombo.Items)
        {
            item.IsSelected = item.Content?.ToString() == currentYear;
        }

        foreach (ComboBoxItem item in MonthlyMonthCombo.Items)
        {
            item.IsSelected = item.Tag?.ToString() == currentMonth;
        }
    }

    // ─────────────────────────────  إدارة الموردين  ─────────────────────────────

    private void AddSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        var name = ShowInputDialog(this, "إضافة مورد", "أدخل اسم المورد الجديد:");
        var cleanName = AppDatabase.CleanName(name);
        if (cleanName.Length == 0)
        {
            return;
        }

        _database.ImportSupplierNames([cleanName]);
        LoadSupplierRows();
        SetStatus($"تمت إضافة المورد \"{cleanName}\".");
    }

    private void EditSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not SupplierRow supplier)
        {
            return;
        }

        var newName = ShowInputDialog(
            this,
            "تعديل اسم المورد",
            $"الاسم الحالي: {supplier.Name}\nأدخل الاسم الجديد للمورد:",
            supplier.Name);

        var cleanName = AppDatabase.CleanName(newName);
        if (cleanName.Length == 0 || cleanName == supplier.Name)
        {
            return;
        }

        try
        {
            _database.RenameSupplier(supplier.Name, cleanName);
            LoadSupplierRows();
            LoadRules();
            SetStatus($"تم تعديل المورد من \"{supplier.Name}\" إلى \"{cleanName}\" في كل الطلبيات.");
        }
        catch (Exception ex)
        {
            ShowError($"حدث خطأ أثناء تعديل المورد:\n{ex.Message}");
        }
    }

    private void DeleteSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SupplierRow supplier)
        {
            DeleteSuppliers([supplier]);
        }
    }

    private void DeleteSelectedSuppliersButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = SupplierRows.Where(row => row.IsSelected).ToList();
        if (targets.Count == 0)
        {
            ShowInfo("حدد الموردين المطلوب حذفهم من مربعات الاختيار أولًا.");
            return;
        }

        DeleteSuppliers(targets);
    }

    /// <summary>يحذف الموردين غير المستخدمين فقط، حفاظًا على بيانات الطلبيات السابقة.</summary>
    private void DeleteSuppliers(IReadOnlyList<SupplierRow> suppliers)
    {
        var used = suppliers.Where(row => row.IsUsedInOrders).ToList();
        var deletable = suppliers.Where(row => !row.IsUsedInOrders).ToList();

        if (deletable.Count == 0)
        {
            ShowInfo(used.Count == 1
                ? $"لا يمكن حذف المورد \"{used[0].Name}\" لأنه مستخدم في {used[0].ItemCount} سطر من الطلبيات المحفوظة.\nيمكنك تعديل اسمه بدلًا من حذفه."
                : "كل الموردين المحددين مستخدمون في طلبيات محفوظة، ولا يمكن حذفهم حفاظًا على البيانات.");
            return;
        }

        var names = string.Join("، ", deletable.Select(row => row.Name));
        if (!Confirm($"هل تريد حذف الموردين التاليين من القائمة؟\n{names}\n\nسيتم حذف قواعد الإسناد الخاصة بهم أيضًا."))
        {
            return;
        }

        _database.DeleteSuppliers(deletable.Select(row => row.Name));
        LoadSupplierRows();
        LoadRules();

        SetStatus(used.Count == 0
            ? $"تم حذف {deletable.Count} مورد."
            : $"تم حذف {deletable.Count} مورد، وتم تجاهل {used.Count} مورد مستخدم في طلبيات محفوظة.");
    }

    private void SelectAllSuppliersCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
        {
            return;
        }

        foreach (var row in SupplierRows)
        {
            row.IsSelected = checkBox.IsChecked == true;
        }
    }

    // ─────────────────────────────  قواعد الإسناد  ─────────────────────────────

    private void AddRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var keyword = AppDatabase.CleanName(NewRuleKeywordBox.Text);
        var supplierName = AppDatabase.CleanName(NewRuleSupplierBox.Text);

        if (keyword.Length == 0 || supplierName.Length == 0)
        {
            ShowInfo("أدخل الكلمة المفتاحية واسم المورد أولًا.");
            return;
        }

        if (!int.TryParse(NewRulePriorityBox.Text.Trim(), out var priority))
        {
            priority = 0;
        }

        var rule = new SupplierRuleRow { Keyword = keyword, SupplierName = supplierName, Priority = priority };
        rule.PropertyChanged += Rule_PropertyChanged;
        Rules.Add(rule);

        NewRuleKeywordBox.Text = string.Empty;
        NewRuleSupplierBox.Text = string.Empty;
        NewRulePriorityBox.Text = "0";
        NewRuleKeywordBox.Focus();

        SaveRules();
    }

    private void DeleteRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not SupplierRuleRow rule)
        {
            return;
        }

        if (!Confirm($"هل تريد حذف قاعدة \"{rule.Keyword}\"؟"))
        {
            return;
        }

        rule.PropertyChanged -= Rule_PropertyChanged;
        Rules.Remove(rule);
        SaveRules();
    }

    private void DeleteSelectedRulesButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = Rules.Where(rule => rule.IsSelected).ToList();
        if (targets.Count == 0)
        {
            ShowInfo("حدد القواعد المطلوب حذفها من مربعات الاختيار أولًا.");
            return;
        }

        if (!Confirm($"هل تريد حذف {targets.Count} قاعدة؟"))
        {
            return;
        }

        foreach (var rule in targets)
        {
            rule.PropertyChanged -= Rule_PropertyChanged;
            Rules.Remove(rule);
        }

        SaveRules();
    }

    private void SelectAllRulesButton_Click(object sender, RoutedEventArgs e)
    {
        var selectAll = Rules.Any(rule => !rule.IsSelected);
        foreach (var rule in Rules)
        {
            rule.IsSelected = selectAll;
        }
    }

    private void Rule_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SupplierRuleRow.Keyword)
            or nameof(SupplierRuleRow.SupplierName)
            or nameof(SupplierRuleRow.Priority))
        {
            _rulesSaveTimer.Stop();
            _rulesSaveTimer.Start();
        }
    }

    private void RulesSaveTimer_Tick(object? sender, EventArgs e)
    {
        _rulesSaveTimer.Stop();
        SaveRules();
    }

    private void SaveRules()
    {
        _rulesSaveTimer.Stop();

        try
        {
            _database.SaveSupplierRules(Rules);
            _ruleEngine.SetRules(Rules);
            LoadSupplierSuggestions();
            SetStatus($"تم حفظ قواعد الإسناد ({_ruleEngine.Count} قاعدة فعالة).");
        }
        catch (Exception ex)
        {
            ShowError($"حدث خطأ أثناء حفظ القواعد:\n{ex.Message}");
        }
    }

    // ─────────────────────────────  استيراد وإدارة المنتجات  ─────────────────────────────

    private void SaveImportedProductsButton_Click(object sender, RoutedEventArgs e)
    {
        var lines = ImportProductsTextBox.Text
            .Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            ShowInfo("يرجى إدخال أسماء المنتجات أولاً.");
            return;
        }

        try
        {
            _database.ImportProductNames(lines);
            LoadProductSuggestions();
            ImportProductsTextBox.Text = string.Empty;

            SetStatus($"تم استيراد {lines.Count} صنف من المنتجات بنجاح.");
            ShowInfo($"تم استيراد {lines.Count} منتج بنجاح وإضافتهم للقائمة المنسدلة.");
        }
        catch (Exception ex)
        {
            ShowError($"حدث خطأ أثناء الاستيراد:\n{ex.Message}");
        }
    }

    private void EditProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not SavedProductRow product)
        {
            return;
        }

        var newName = ShowInputDialog(
            this,
            "تعديل اسم المنتج",
            $"الاسم الحالي: {product.Name}\nأدخل الاسم الجديد للمنتج:",
            product.Name);

        var cleanName = AppDatabase.CleanName(newName);
        if (cleanName.Length == 0 || cleanName == product.Name)
        {
            return;
        }

        try
        {
            _database.UpdateSavedProduct(product.Name, cleanName);
            LoadProductSuggestions();
            SetStatus($"تم تعديل المنتج من \"{product.Name}\" إلى \"{cleanName}\" بنجاح.");
        }
        catch (Exception ex)
        {
            ShowError($"حدث خطأ أثناء تعديل المنتج:\n{ex.Message}");
        }
    }

    private void DeleteProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SavedProductRow product)
        {
            DeleteProducts([product]);
        }
    }

    private void DeleteSelectedProductsButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = _savedProducts.Where(product => product.IsSelected).ToList();
        if (targets.Count == 0)
        {
            ShowInfo("حدد المنتجات المطلوب حذفها من مربعات الاختيار أولًا.");
            return;
        }

        DeleteProducts(targets);
    }

    private void DeleteProducts(IReadOnlyList<SavedProductRow> products)
    {
        var message = products.Count == 1
            ? $"هل تريد حذف المنتج \"{products[0].Name}\" من قائمة الاقتراحات؟"
            : $"هل تريد حذف {products.Count} منتج من قائمة الاقتراحات؟";

        if (!Confirm(message))
        {
            return;
        }

        try
        {
            _database.DeleteSavedProducts(products.Select(product => product.Name));
            LoadProductSuggestions();
            SetStatus($"تم حذف {products.Count} منتج من قائمة الاقتراحات.");
        }
        catch (Exception ex)
        {
            ShowError($"حدث خطأ أثناء حذف المنتجات:\n{ex.Message}");
        }
    }

    private void SelectAllProductsButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var product in FilteredSavedProducts)
        {
            product.IsSelected = true;
        }
    }

    private void ClearProductsSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var product in _savedProducts)
        {
            product.IsSelected = false;
        }
    }

    private void SearchSavedProductsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySavedProductsFilter();
    }

    private void ApplySavedProductsFilter()
    {
        if (SearchSavedProductsBox is null)
        {
            return;
        }

        var keywords = ArabicText.Normalize(SearchSavedProductsBox.Text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var matches = keywords.Length == 0
            ? _savedProducts
            : _savedProducts
                .Where(product => keywords.All(keyword => product.NormalizedName.Contains(keyword, StringComparison.Ordinal)))
                .ToList();

        ReplaceCollection(FilteredSavedProducts, matches);
        SavedProductsHeaderText.Text = $"المنتجات المسجلة حالياً: {matches.Count} من {_savedProducts.Count}";
    }

    // ─────────────────────────────  أدوات مساعدة  ─────────────────────────────

    private void SetPage(string title, string subtitle)
    {
        PageTitleText.Text = title;
        PageSubtitleText.Text = subtitle;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = $"{message}  |  {DateTime.Now:HH:mm}";
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void ShowInfo(string message)
    {
        MessageBox.Show(this, message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
    }

    private bool Confirm(string message)
    {
        return MessageBox.Show(this, message, "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading) == MessageBoxResult.Yes;
    }

    /// <summary>نافذة إدخال نص بسيطة تُستخدم لتعديل أسماء المنتجات والموردين.</summary>
    private static string? ShowInputDialog(Window owner, string title, string prompt, string defaultValue = "")
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 195,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            FlowDirection = FlowDirection.RightToLeft,
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)),
            ShowInTaskbar = false
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = prompt,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        var textBox = new TextBox
        {
            Text = defaultValue,
            Padding = new Thickness(6),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(textBox, 1);
        grid.Children.Add(textBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(buttonPanel, 2);
        grid.Children.Add(buttonPanel);

        var okButton = new Button
        {
            Content = "حفظ",
            Width = 90,
            Height = 30,
            IsDefault = true,
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            Margin = new Thickness(8, 0, 0, 0)
        };
        var cancelButton = new Button
        {
            Content = "إلغاء",
            Width = 70,
            Height = 30,
            IsCancel = true,
            Style = (Style)Application.Current.FindResource("SecondaryButton")
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        string? result = null;
        okButton.Click += (_, _) =>
        {
            result = textBox.Text;
            dialog.DialogResult = true;
        };

        dialog.Content = grid;
        dialog.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        return dialog.ShowDialog() == true ? result : null;
    }
}
