using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using G3MStudio.Core.Localization;

namespace G3MStudio.Localization;

public static class Loc
{
    public static Localizer Current { get; } = Localizer.LoadEmbedded();
}

/// <summary>XAML: <c>Text="{loc:Tr app.welcome}"</c>. Rebinds when the language changes.</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => LocBinding.To(Key);
}

/// <summary>The same binding for code: <c>new Button().Tr(ContentControl.ContentProperty, "git.commit")</c> relabels when the language changes.</summary>
public static class LocBinding
{
    public static Binding To(string key) => new($"[{key}]") { Source = Loc.Current, Mode = BindingMode.OneWay };

    public static T Tr<T>(this T target, AvaloniaProperty property, string key) where T : AvaloniaObject
    {
        target.Bind(property, To(key));
        return target;
    }
}
