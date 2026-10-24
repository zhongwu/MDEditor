using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MDEditor.Themes;

namespace MDEditor.Editor;

/// <summary>
/// Lightweight completion list anchored to the editor caret.
/// Keyboard handling lives in MainWindow; this class only displays,
/// tracks the selection and raises <see cref="ItemAccepted"/>.
/// </summary>
public sealed class CompletionPopup : Popup
{
    private readonly ListBox _list;
    private readonly Border _border;

    public event EventHandler<CompletionItem>? ItemAccepted;

    public CompletionPopup()
    {
        AllowsTransparency = true;
        // A click outside a non-sticky popup is swallowed to dismiss it, which
        // makes the next click in the editor fail to place the caret or start a
        // selection. Keep the popup sticky and close it explicitly instead.
        StaysOpen = true;
        Placement = PlacementMode.Relative;
        PopupAnimation = PopupAnimation.Fade;
        Focusable = false;

        _list = new ListBox
        {
            MaxHeight = 240,
            MinWidth = 300,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Focusable = false,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            DisplayMemberPath = nameof(CompletionItem.Display)
        };

        _list.PreviewMouseLeftButtonDown += (_, e) =>
        {
            // A ListBox only updates SelectedItem on mouse-up, so trusting
            // SelectedItem here would always accept the previously selected row
            // (usually the first one). Resolve the row under the pointer instead.
            if (FindItem(e.OriginalSource as DependencyObject) is { } item)
                Accept(item);
            else
                Hide();

            e.Handled = true;
        };

        _border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2),
            Child = _list
        };

        _border.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 8,
            ShadowDepth = 2,
            Opacity = 0.25
        };

        Child = _border;

        ApplyTheme(ThemeService.IsDark);
        ThemeService.ThemeChanged += isDark => ApplyTheme(isDark);
    }

    private void ApplyTheme(bool isDark)
    {
        _border.Background = new SolidColorBrush(isDark ? Color.FromRgb(0x21, 0x26, 0x2D) : Color.FromRgb(0xFE, 0xFE, 0xFE));
        _border.BorderBrush = new SolidColorBrush(isDark ? Color.FromRgb(0x30, 0x36, 0x3D) : Color.FromRgb(0xC8, 0xC8, 0xC8));
        _list.Foreground = new SolidColorBrush(isDark ? Colors.White : Colors.Black);
    }

    public bool HasItems => _list.Items.Count > 0;

    public CompletionItem? SelectedItem => _list.SelectedItem as CompletionItem;

    /// <summary>Shows the popup next to the caret rectangle (relative to the editor).</summary>
    public void Show(IReadOnlyList<CompletionItem> items, UIElement placementTarget, Rect caretRect)
    {
        _list.ItemsSource = items;
        _list.SelectedIndex = 0;

        PlacementTarget = placementTarget;
        HorizontalOffset = Math.Max(0, caretRect.Left);
        VerticalOffset = caretRect.Bottom + 2;

        // Keep the popup inside the window horizontally.
        if (placementTarget is FrameworkElement element && element.ActualWidth > 0)
        {
            double maxLeft = Math.Max(0, element.ActualWidth - 320);
            if (HorizontalOffset > maxLeft)
                HorizontalOffset = maxLeft;
        }

        if (!IsOpen)
            IsOpen = true;
    }

    public void MoveSelection(int delta)
    {
        if (_list.Items.Count == 0)
            return;

        int index = _list.SelectedIndex + delta;
        index = Math.Clamp(index, 0, _list.Items.Count - 1);
        _list.SelectedIndex = index;
        _list.ScrollIntoView(_list.SelectedItem);
    }

    public void Hide()
    {
        IsOpen = false;
        _list.ItemsSource = null;
    }

    private void Accept(CompletionItem item)
    {
        ItemAccepted?.Invoke(this, item);
        Hide();
    }

    /// <summary>Walks up from a hit-tested element to the row it belongs to.</summary>
    private static CompletionItem? FindItem(DependencyObject? source)
    {
        while (source is not null and not ListBoxItem)
        {
            source = source is Visual
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return (source as ListBoxItem)?.DataContext as CompletionItem;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // The editor keeps focus; keys are handled by MainWindow.
        e.Handled = true;
    }
}
