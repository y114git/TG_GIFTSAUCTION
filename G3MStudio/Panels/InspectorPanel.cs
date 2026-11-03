using Avalonia;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Panels;

/// <summary>What the Explorer has selected: its type and name, and how many places use it. Find Usages lists them.</summary>
public sealed class InspectorPanel : ScrollViewer
{
    private readonly StackPanel _stack = new() { Margin = new Thickness(12), Spacing = 6 };
    private readonly ContentControl _context = new() { Name = "InspectorContext" };
    private readonly TextBlock _title = new() { Name = "InspectorName", FontWeight = Avalonia.Media.FontWeight.Bold, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _type = new() { Name = "InspectorType", Classes = { "muted" } };
    private readonly TextBlock _uses = new() { Name = "InspectorUses", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _find = new() { Name = "InspectorFindUsages", Content = Loc.Current["cmd.edit.findUsages"], IsVisible = false };
    private int _version;

    public InspectorPanel(Action findUsages)
    {
        Name = "InspectorPanel";
        _find.Click += (_, _) => findUsages();
        _stack.Children.Add(_context);
        _stack.Children.Add(_title);
        _stack.Children.Add(_type);
        _stack.Children.Add(_uses);
        _stack.Children.Add(_find);
        Content = _stack;
        _ = ShowAsync(null, null);
    }

    /// <summary>The tools of the tab in front (the layers of a room), shown above the resource summary; null when the tab has none.</summary>
    public void SetContext(Control? content) => _context.Content = content;

    /// <summary>Shows the entry with this Explorer key, or the empty hint when there is none.</summary>
    public async Task ShowAsync(Workspace? workspace, string? key)
    {
        var version = ++_version;
        var split = key is null ? null : ResourceTypes.SplitKey(key);
        if (workspace is null || split is not var (type, name) || ResourceTypes.Find(workspace.Data, key ?? "") is not { } resource)
        {
            (_title.Text, _type.Text, _uses.Text) = (Loc.Current["inspector.none"], "", "");
            _find.IsVisible = false;
            return;
        }

        _title.Text = name;
        _type.Text = Loc.Current["explorer.type." + type];
        _uses.Text = "";
        _find.IsVisible = true;
        try
        {
            var count = await UsageSearch.CountAsync(workspace, resource);
            if (version == _version) _uses.Text = Loc.Current.Format("inspector.uses", ("count", count)); // a newer selection replaced this count otherwise
        }
        catch (InvalidOperationException)
        {
            // The file was closed while counting; the panel is about to be reset.
        }
    }
}
