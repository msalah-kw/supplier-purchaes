using System.Windows;
using System.Windows.Controls;
using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases;

/// <summary>شاشة إدارة الموردين.</summary>
public partial class MainWindow
{
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
}
