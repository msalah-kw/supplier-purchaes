using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SupplierPurchases.Data;

namespace SupplierPurchases.Controls;

/// <summary>
/// حقل كتابة مع قائمة اقتراحات منسدلة يمكن التنقل فيها بالكيبورد
/// (سهم لأعلى/لأسفل للتنقل، Enter للاختيار، Esc للإغلاق) دون أن يعترض الجدول ضغطات المفاتيح.
/// </summary>
public partial class AutoCompleteTextBox : UserControl
{
    private const int PageStep = 8;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(AutoCompleteTextBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(SuggestionSource),
        typeof(AutoCompleteTextBox),
        new PropertyMetadata(null));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder),
        typeof(string),
        typeof(AutoCompleteTextBox),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty OpenOnFocusProperty = DependencyProperty.Register(
        nameof(OpenOnFocus),
        typeof(bool),
        typeof(AutoCompleteTextBox),
        new PropertyMetadata(true));

    private bool _suppressFiltering;

    public AutoCompleteTextBox()
    {
        InitializeComponent();
        UpdatePlaceholder();
    }

    /// <summary>يُرفع عند تثبيت قيمة الحقل (اختيار اقتراح أو مغادرة الحقل بعد تغييره).</summary>
    public event EventHandler<string>? ValueCommitted;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public SuggestionSource? Source
    {
        get => (SuggestionSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>فتح قائمة الاقتراحات تلقائيًا بمجرد الدخول إلى الحقل.</summary>
    public bool OpenOnFocus
    {
        get => (bool)GetValue(OpenOnFocusProperty);
        set => SetValue(OpenOnFocusProperty, value);
    }

    public void FocusInput()
    {
        InputBox.Focus();
        InputBox.SelectAll();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = MoveSelection(1);
                return;

            case Key.Up:
                e.Handled = MoveSelection(-1);
                return;

            case Key.PageDown:
                e.Handled = MoveSelection(PageStep);
                return;

            case Key.PageUp:
                e.Handled = MoveSelection(-PageStep);
                return;

            case Key.Enter:
                if (SuggestionPopup.IsOpen && SuggestionList.SelectedItem is string selected)
                {
                    Accept(selected);
                    e.Handled = true;
                    return;
                }

                ClosePopup();
                RaiseValueCommitted();
                InputBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
                return;

            case Key.Escape:
                if (SuggestionPopup.IsOpen)
                {
                    ClosePopup();
                    e.Handled = true;
                    return;
                }

                break;

            case Key.Tab:
                if (SuggestionPopup.IsOpen)
                {
                    if (SuggestionList.SelectedItem is string highlighted)
                    {
                        Accept(highlighted);
                    }
                    else
                    {
                        ClosePopup();
                    }
                }

                break;
        }

        base.OnPreviewKeyDown(e);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AutoCompleteTextBox)d).UpdatePlaceholder();
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePlaceholder();

        if (_suppressFiltering || !InputBox.IsKeyboardFocusWithin)
        {
            return;
        }

        RefreshSuggestions(openIfAny: true);
    }

    private void InputBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        InputBox.SelectAll();

        if (OpenOnFocus)
        {
            RefreshSuggestions(openIfAny: true);
        }
    }

    private void InputBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ClosePopup();
        RaiseValueCommitted();
    }

    private void SuggestionList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // العناصر غير قابلة للتركيز (Focusable=False) حتى لا يفقد حقل الكتابة تركيز الكيبورد،
        // لذا نحدّد العنصر المضغوط يدويًا بدل الاعتماد على تحديد الـ ListBox الافتراضي.
        if (ItemFromMouseEvent(e) is { } item)
        {
            SuggestionList.SelectedIndex = SuggestionList.ItemContainerGenerator.IndexFromContainer(item);
            e.Handled = true;
        }
    }

    private void SuggestionList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // اختيار العنصر الذي أُفلت الزر فوقه مباشرة (يدعم الماوس)، مع الإبقاء على SelectedItem كاحتياط.
        var value = ItemFromMouseEvent(e)?.Content as string ?? SuggestionList.SelectedItem as string;
        if (value is not null)
        {
            Accept(value);
            e.Handled = true;
        }
    }

    private void SuggestionList_MouseMove(object sender, MouseEventArgs e)
    {
        // إبراز العنصر الذي يمر فوقه الماوس ليتصرف كقائمة منسدلة حقيقية.
        if (ItemFromMouseEvent(e) is { } item)
        {
            var index = SuggestionList.ItemContainerGenerator.IndexFromContainer(item);
            if (index >= 0 && index != SuggestionList.SelectedIndex)
            {
                SuggestionList.SelectedIndex = index;
            }
        }
    }

    private ListBoxItem? ItemFromMouseEvent(MouseEventArgs e)
    {
        return e.OriginalSource is DependencyObject source
            ? ItemsControl.ContainerFromElement(SuggestionList, source) as ListBoxItem
            : null;
    }

    private void RefreshSuggestions(bool openIfAny)
    {
        var source = Source;
        if (source is null || source.Count == 0)
        {
            ClosePopup();
            return;
        }

        var matches = source.Filter(InputBox.Text);
        SuggestionList.ItemsSource = matches;
        SuggestionList.SelectedIndex = -1;

        if (matches.Count == 0)
        {
            ClosePopup();
            return;
        }

        if (openIfAny && !SuggestionPopup.IsOpen)
        {
            SuggestionPopup.IsOpen = true;
        }
    }

    /// <summary>ينقل التحديد داخل قائمة الاقتراحات، ويعيد false إذا لم تكن هناك قائمة لتترك المفتاح للجدول.</summary>
    private bool MoveSelection(int offset)
    {
        if (!SuggestionPopup.IsOpen)
        {
            RefreshSuggestions(openIfAny: true);
            if (!SuggestionPopup.IsOpen)
            {
                return false;
            }
        }

        var count = SuggestionList.Items.Count;
        if (count == 0)
        {
            return false;
        }

        var index = SuggestionList.SelectedIndex;
        index = index < 0
            ? (offset > 0 ? 0 : count - 1)
            : Math.Clamp(index + offset, 0, count - 1);

        SuggestionList.SelectedIndex = index;
        SuggestionList.ScrollIntoView(SuggestionList.Items[index]);
        return true;
    }

    private void Accept(string value)
    {
        _suppressFiltering = true;
        try
        {
            Text = value;
            InputBox.CaretIndex = InputBox.Text.Length;
        }
        finally
        {
            _suppressFiltering = false;
        }

        ClosePopup();
        RaiseValueCommitted();
    }

    private void ClosePopup()
    {
        if (SuggestionPopup.IsOpen)
        {
            SuggestionPopup.IsOpen = false;
        }

        SuggestionList.SelectedIndex = -1;
    }

    private void RaiseValueCommitted()
    {
        ValueCommitted?.Invoke(this, Text ?? string.Empty);
    }

    private void UpdatePlaceholder()
    {
        if (PlaceholderText is null)
        {
            return;
        }

        PlaceholderText.Visibility = string.IsNullOrEmpty(InputBox.Text) && !string.IsNullOrEmpty(Placeholder)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
