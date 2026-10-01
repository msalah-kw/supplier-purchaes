using System.Windows;
using System.Windows.Controls;
using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases;

/// <summary>شاشة استيراد المنتجات وإدارتها.</summary>
public partial class MainWindow
{
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
}
