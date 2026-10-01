using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SupplierPurchases.Controls;
using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases;

/// <summary>محرر الطلبية: إدخال المنتجات والعمليات الجماعية والإسناد التلقائي.</summary>
public partial class MainWindow
{
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
            var match = _ruleEngine.FindMatch(item.ProductName);
            if (match is null)
            {
                continue;
            }

            var changed = false;
            if (item.SupplierName != match.SupplierName)
            {
                item.SupplierName = match.SupplierName;
                changed = true;
            }

            if (match.Unit.Length > 0 && item.Unit != match.Unit)
            {
                item.Unit = match.Unit;
                changed = true;
            }

            item.RuleCheckedProduct = AppDatabase.CleanName(item.ProductName);
            if (changed)
            {
                applied++;
            }
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

        _productUnits.TryGetValue(AppDatabase.NormalizeName(productName), out var rememberedUnit);
        var applied = OrderItemAutoFill.Apply(row, productName, _ruleEngine.FindMatch(productName), rememberedUnit);

        if (applied.Count > 0)
        {
            SetStatus($"تم ضبط {string.Join(" و", applied)} للمنتج \"{productName}\" تلقائيًا.");
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
}
