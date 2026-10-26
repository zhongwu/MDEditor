using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace MDEditor;

/// <summary>One place a document can be opened from or saved to.</summary>
public sealed record SourceOption(string Label, string Description);

/// <summary>
/// Small chooser shown by File &gt; Open from... and Save As... so the menu needs one entry per action
/// instead of one per place (this computer, GitHub repository, gist, and later other hosts).
/// </summary>
public partial class SourcePickerDialog : Window
{
    private readonly List<RadioButton> _radios = new();

    /// <summary>Index of the chosen option after the dialog returns true.</summary>
    public int SelectedIndex { get; private set; } = -1;

    public SourcePickerDialog(string title, IReadOnlyList<SourceOption> options)
    {
        InitializeComponent();
        Title = title;

        for (var i = 0; i < options.Count; i++)
        {
            var text = new TextBlock();
            text.Inlines.Add(new Run(options[i].Label) { FontWeight = FontWeights.SemiBold });
            text.Inlines.Add(new LineBreak());
            text.Inlines.Add(new Run(options[i].Description) { FontSize = 12 });

            var radio = new RadioButton
            {
                GroupName = "Source",
                IsChecked = i == 0,
                Margin = new Thickness(0, i == 0 ? 0 : 10, 0, 0),
                Content = text,
            };
            radio.MouseDoubleClick += (_, _) => Accept();
            _radios.Add(radio);
            OptionsPanel.Children.Add(radio);
        }

        Loaded += (_, _) => _radios[0].Focus();
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => Accept();

    private void Accept()
    {
        SelectedIndex = _radios.FindIndex(r => r.IsChecked == true);
        DialogResult = true;
    }
}
