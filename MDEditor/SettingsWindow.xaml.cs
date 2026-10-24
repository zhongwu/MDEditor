using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Themes;

namespace MDEditor;

/// <summary>
/// Non-modal, single-instance settings window (tracked on <see cref="MainWindow"/>). Every
/// control applies and persists its change immediately - no OK/Cancel, matching how every other
/// toggle in this app already works.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly MainWindow _owner;
    private readonly ISettingsService _settingsService;

    // Guards against the owner-update callbacks firing while controls are being populated from
    // settings on Loaded (programmatic IsChecked/SelectedItem assignment still raises the
    // Checked/SelectionChanged events in WPF).
    private bool _loading;

    public SettingsWindow(MainWindow owner, ISettingsService settingsService, IMarkdownParser markdownParser)
    {
        InitializeComponent();
        _owner = owner;
        _settingsService = settingsService;

        Loaded += (s, e) => LoadFromSettings();
        BuildShortcutsTab();
    }

    private void BuildShortcutsTab()
    {
        ShortcutsPanel.Children.Clear();

        foreach (var (key, label, gesture) in _owner.GetShortcutRows())
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelBlock = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(labelBlock, 0);

            var gestureBox = new TextBox
            {
                Text = gesture,
                IsReadOnly = true,
                Tag = key,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            gestureBox.PreviewKeyDown += ShortcutBox_PreviewKeyDown;
            Grid.SetColumn(gestureBox, 1);

            var resetButton = new Button { Content = "Reset", Tag = key, Padding = new Thickness(8, 2, 8, 2) };
            resetButton.Click += ResetShortcut_Click;
            Grid.SetColumn(resetButton, 2);

            row.Children.Add(labelBlock);
            row.Children.Add(gestureBox);
            row.Children.Add(resetButton);
            ShortcutsPanel.Children.Add(row);
        }
    }

    private void ShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not string key)
            return;

        e.Handled = true;

        var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (actualKey is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System)
            return; // a bare modifier isn't a complete gesture yet - wait for the real key

        var modifiers = Keyboard.Modifiers;
        bool isFunctionKey = actualKey >= Key.F1 && actualKey <= Key.F24;
        if (modifiers == ModifierKeys.None && !isFunctionKey)
        {
            MessageBox.Show(this, "Shortcuts need at least one modifier key (Ctrl/Shift/Alt), except function keys.",
                "Invalid Shortcut", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var conflictLabel = _owner.FindShortcutConflict(key, modifiers, actualKey);
        if (conflictLabel != null)
        {
            var result = MessageBox.Show(this,
                $"This shortcut is already used by \"{conflictLabel}\". Reassign it to this command instead?",
                "Shortcut Conflict", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
        }

        _owner.SetShortcut(key, modifiers, actualKey);
        RefreshShortcutRow(key, box);
    }

    private void ResetShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string key)
            return;

        _owner.ResetShortcut(key);

        if (button.Parent is Grid row && row.Children[1] is TextBox box)
            RefreshShortcutRow(key, box);
    }

    private void RefreshShortcutRow(string key, TextBox box)
    {
        var updated = _owner.GetShortcutRows();
        foreach (var (rowKey, _, gesture) in updated)
        {
            if (rowKey == key)
            {
                box.Text = gesture;
                break;
            }
        }

        // Move focus off the capture box so further stray key presses don't retrigger it.
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);
        Keyboard.ClearFocus();
    }

    private void LoadFromSettings()
    {
        _loading = true;
        try
        {
            var theme = _settingsService.Get("appearance.theme", "System");
            var themeRadio = theme switch
            {
                "Light" => ThemeLightRadio,
                "Dark" => ThemeDarkRadio,
                _ => ThemeSystemRadio,
            };
            themeRadio.IsChecked = true;

            SelectComboByTag(FontFamilyCombo, _settingsService.Get("editor.fontFamily", "Cascadia Code, Consolas, monospace"));
            SelectComboByTag(FontSizeCombo, _settingsService.Get("editor.fontSize", 14.0).ToString(CultureInfo.InvariantCulture));
            SelectComboByTag(AutoSaveCombo, _settingsService.Get("editor.autoSaveIntervalSeconds", 30).ToString(CultureInfo.InvariantCulture));
            SelectComboByTag(LineEndingCombo, _settingsService.Get("editor.lineEnding", "CRLF"));

            MathCheck.IsChecked = _settingsService.Get("markdown.enableMath", true);
            DiagramsCheck.IsChecked = _settingsService.Get("markdown.enableDiagrams", true);
            EmojiCheck.IsChecked = _settingsService.Get("markdown.enableEmoji", true);
            FootnotesCheck.IsChecked = _settingsService.Get("markdown.enableFootnotes", true);
            TaskListsCheck.IsChecked = _settingsService.Get("markdown.enableTaskLists", true);

            CustomCssBox.Text = _settingsService.Get<string?>("preview.customCss", null) ?? string.Empty;
        }
        finally
        {
            _loading = false;
        }
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (var obj in combo.Items)
        {
            if (obj is ComboBoxItem item && (string)item.Tag == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var theme = sender == ThemeLightRadio ? "Light" : sender == ThemeDarkRadio ? "Dark" : "System";
        _settingsService.Set("appearance.theme", theme);
        ThemeService.Apply(theme); // raises ThemeChanged -> MainWindow re-colors title bar + reloads preview
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || FontFamilyCombo.SelectedItem is not ComboBoxItem item)
            return;

        var family = (string)item.Tag;
        _settingsService.Set("editor.fontFamily", family);
        _owner.SetEditorFontFamily(family);
    }

    private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || FontSizeCombo.SelectedItem is not ComboBoxItem item)
            return;

        var size = double.Parse((string)item.Tag, CultureInfo.InvariantCulture);
        _settingsService.Set("editor.fontSize", size);
        _owner.SetBaseEditorFontSize(size);
    }

    private void AutoSaveCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AutoSaveCombo.SelectedItem is not ComboBoxItem item)
            return;

        var seconds = int.Parse((string)item.Tag, CultureInfo.InvariantCulture);
        _settingsService.Set("editor.autoSaveIntervalSeconds", seconds);
        _owner.SetAutoSaveInterval(seconds);
    }

    private void LineEndingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LineEndingCombo.SelectedItem is not ComboBoxItem item)
            return;

        var ending = (string)item.Tag;
        _settingsService.Set("editor.lineEnding", ending);
        _owner.SetLineEnding(ending);
    }

    private void FlavorCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var options = new MarkdownParserOptions
        {
            EnableMath = MathCheck.IsChecked == true,
            EnableDiagrams = DiagramsCheck.IsChecked == true,
            EnableEmoji = EmojiCheck.IsChecked == true,
            EnableFootnotes = FootnotesCheck.IsChecked == true,
            EnableTaskLists = TaskListsCheck.IsChecked == true,
        };

        _settingsService.Set("markdown.enableMath", options.EnableMath);
        _settingsService.Set("markdown.enableDiagrams", options.EnableDiagrams);
        _settingsService.Set("markdown.enableEmoji", options.EnableEmoji);
        _settingsService.Set("markdown.enableFootnotes", options.EnableFootnotes);
        _settingsService.Set("markdown.enableTaskLists", options.EnableTaskLists);

        _owner.SetMarkdownOptions(options);
    }

    private void CustomCssBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var css = string.IsNullOrWhiteSpace(CustomCssBox.Text) ? null : CustomCssBox.Text;
        _settingsService.Set("preview.customCss", css);
        _owner.SetCustomCss(css);
    }
}
