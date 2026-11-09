using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>An extension as an outline above its property list: its files, each file's functions with argument and return types, and its options. The values are edited in the property list below.</summary>
public sealed class ExtensionView : StackPanel
{
    public ExtensionView(GameMakerExtension extension)
    {
        Name = "ExtensionView";
        Spacing = 4;
        Children.Add(new TextBlock { Text = Loc.Current.Format("editors.extensionSummary", ("name", extension.Name?.Content ?? ""), ("version", extension.Version?.Content ?? ""), ("files", extension.Files.Count), ("options", extension.Options.Count)), Classes = { "muted" } });
        Children.Add(new ListBox { Name = "ExtensionOutline", MaxHeight = 220, ItemsSource = Outline(extension) });
    }

    /// <summary>One line per file, function and option: files flush left, functions and options indented.</summary>
    public static IReadOnlyList<string> Outline(GameMakerExtension extension)
    {
        var lines = new List<string>();
        foreach (var file in extension.Files)
        {
            lines.Add($"{file.Filename?.Content} ({file.Kind})");
            foreach (var function in file.Functions)
                lines.Add($"    {function.Name?.Content}({string.Join(", ", function.Arguments.Select(a => a.Type.ToString()))}) : {function.RetType}");
        }

        foreach (var option in extension.Options) lines.Add($"{option.Name?.Content} = {option.Value?.Content}");
        return lines;
    }
}
