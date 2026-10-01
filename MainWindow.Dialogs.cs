using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupplierPurchases;

/// <summary>رسائل التنبيه والتأكيد ونافذة إدخال النص.</summary>
public partial class MainWindow
{
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
