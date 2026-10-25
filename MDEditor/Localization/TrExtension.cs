using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using MDEditor.Core.Localization;

namespace MDEditor.Localization;

/// <summary>
/// XAML markup extension for a localized string: <c>Header="{loc:Tr Menu_File}"</c>. It binds to
/// <see cref="LocalizationSource"/>, so the text updates live when the language changes.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = LocalizationSource.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}

/// <summary>
/// Indexer source for <see cref="TrExtension"/> bindings; re-raises <see cref="Loc.LanguageChanged"/>
/// as an indexer change so every bound string refreshes.
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    public static LocalizationSource Instance { get; } = new();

    private LocalizationSource()
    {
        Loc.LanguageChanged += (s, e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
    }

    public string this[string key] => Loc.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}
