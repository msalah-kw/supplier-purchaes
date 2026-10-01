using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases;

/// <summary>شاشة قواعد الإسناد التلقائي.</summary>
public partial class MainWindow
{
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

        var unit = (NewRuleUnitCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;

        var rule = new SupplierRuleRow { Keyword = keyword, SupplierName = supplierName, Unit = unit, Priority = priority };
        rule.PropertyChanged += Rule_PropertyChanged;
        Rules.Add(rule);

        NewRuleKeywordBox.Text = string.Empty;
        NewRuleSupplierBox.Text = string.Empty;
        NewRulePriorityBox.Text = "0";
        NewRuleUnitCombo.SelectedIndex = 0;
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
            or nameof(SupplierRuleRow.Unit)
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
}
