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

    /// <summary>آخر وحدة استُخدمت لكل منتج، بمفتاح الاسم الموحّد.</summary>
    private Dictionary<string, string> _productUnits = [];

    /// <summary>كل الطلبيات المحفوظة قبل الفلترة. الجدول يعرض <see cref="Orders"/> بعد الفلترة.</summary>
    private List<OrderSummary> _allOrders = [];

    private int? _editingOrderId;
    private string _currentSupplierName = string.Empty;
    private DateTime _currentPurchaseDate = DateTime.Today;
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
        LoadReportYears();
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

        _allOrders = _database.GetOrderSummaries();
        ApplyOrdersFilter();
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
        _productUnits = _database.GetProductDefaultUnits();

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
}
