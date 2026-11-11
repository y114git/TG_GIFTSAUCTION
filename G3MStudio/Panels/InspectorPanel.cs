using Avalonia;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Panels;

/// <summary>What the Explorer has selected: its type and name, and how many places use it. Find Usages lists them.</summary>
public sealed class InspectorPanel : ScrollViewer
{
    // A control theme is looked up by the exact type: without this the scroll viewer template is missing and nothing scrolls.
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    private readonly StackPanel _stack = new() { Margin = new Thickness(12), Spacing = 6 };
    private readonly ContentControl _context = new() { Name = "InspectorContext" };
    private readonly TextBlock _title = new() { Name = "InspectorName", FontWeight = Avalonia.Media.FontWeight.Bold, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _type = new() { Name = "InspectorType", Classes = { "muted" } };
    private readonly TextBlock _uses = new() { Name = "InspectorUses", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _find = new Button { Name = "InspectorFindUsages", IsVisible = false }.Tr(ContentControl.ContentProperty, "cmd.edit.findUsages");
    private readonly Button _findUses = new Button { Name = "InspectorFindUses", IsVisible = false }.Tr(ContentControl.ContentProperty, "cmd.edit.findUses");
    private int _version;

    public InspectorPanel(Action findUsages, Action? findUses = null)
    {
        Name = "InspectorPanel";
        Focusable = true; // F6 can reach it and the arrow keys scroll it, even when it holds no button
        _find.Click += (_, _) => findUsages();
        _findUses.Click += (_, _) => findUses?.Invoke();
        _stack.Children.Add(_context);
        _stack.Children.Add(_title);
        _stack.Children.Add(_type);
        _stack.Children.Add(_uses);
        _stack.Children.Add(_find);
        _stack.Children.Add(_findUses);
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
            _title.Tr(TextBlock.TextProperty, "inspector.none");
            (_type.Text, _uses.Text) = ("", "");
            _find.IsVisible = _findUses.IsVisible = false;
            return;
        }

        _title.Text = name;
        _type.Tr(TextBlock.TextProperty, "explorer.type." + type);
        _uses.Text = "";
        _find.IsVisible = _findUses.IsVisible = true;
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
