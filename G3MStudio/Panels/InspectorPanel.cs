using Avalonia;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Panels;

/// <summary>What the Explorer has selected: its type, ID and name, and how many places use it (for a folder or group: what it is and how many entries it holds). Find Usages lists them.</summary>
public sealed class InspectorPanel : ScrollViewer
{
    // A control theme is looked up by the exact type: without this the scroll viewer template is missing and nothing scrolls.
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    private readonly StackPanel _stack = new() { Margin = new Thickness(12), Spacing = 6 };
    private readonly ContentControl _context = new() { Name = "InspectorContext" };
    private readonly TextBlock _title = new() { Name = "InspectorName", FontWeight = Avalonia.Media.FontWeight.Bold, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _realName = new() { Name = "InspectorRealName", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false };
    private readonly TextBlock _type = new() { Name = "InspectorType", Classes = { "muted" } };
    private readonly TextBlock _uses = new() { Name = "InspectorUses", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _find = new Button { Name = "InspectorFindUsages", IsVisible = false }.Tr(ContentControl.ContentProperty, "cmd.edit.findUsages");
    private readonly Button _findUses = new Button { Name = "InspectorFindUses", IsVisible = false }.Tr(ContentControl.ContentProperty, "cmd.edit.findUses");
    private readonly CheckBox _lock = new CheckBox { Name = "InspectorLock", IsVisible = false }.Tr(ContentControl.ContentProperty, "inspector.lock").Tr(ToolTip.TipProperty, "inspector.lockTip");
    private int _version;
    private Workspace? _workspace;
    private string? _key;
    private int? _count;
    private int? _id;
    private ExplorerRow? _row;

    public InspectorPanel(Action findUsages, Action? findUses = null)
    {
        Name = "InspectorPanel";
        Focusable = true; // F6 can reach it and the arrow keys scroll it, even when it holds no button
        _find.Click += (_, _) => findUsages();
        _findUses.Click += (_, _) => findUses?.Invoke();
        _lock.IsCheckedChanged += (_, _) =>
        {
            if (_workspace is not null && _key is not null && _lock.IsChecked is { } on && on != _workspace.Locks.IsLocked(_key)) _workspace.Locks.Set(_key, on);
        };
        _stack.Children.Add(_context);
        _stack.Children.Add(_title);
        _stack.Children.Add(_realName);
        _stack.Children.Add(_type);
        _stack.Children.Add(_uses);
        _stack.Children.Add(_find);
        _stack.Children.Add(_findUses);
        _stack.Children.Add(_lock);
        Content = _stack;
        LanguageWatch.Follow(this, OnLanguageChanged);
        _ = ShowAsync(null, null);
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Loc.Current.Language)) return;
        if (_count is { } count) _uses.Text = Loc.Current.Format("inspector.uses", ("count", count));
        if (_row is { } row) _type.Text = KindOf(row);
        else if (_key is null) _title.Text = Loc.Current["inspector.none"];
        else if (ResourceTypes.SplitKey(_key) is var (type, _)) _type.Text = TypeLine(type);
    }

    // The ID is the one the status bar and the hover card show.
    private string TypeLine(string type) =>
        _id is { } id ? Loc.Current.Format("hoverCard.typeId", ("type", Loc.Current["explorer.type." + type]), ("id", id)) : Loc.Current["explorer.type." + type];

    private static string KindOf(ExplorerRow row) => Loc.Current[row.Kind switch
    {
        ExplorerRowKind.Folder => "inspector.kindFolder",
        ExplorerRowKind.Collection => "inspector.kindCollection",
        ExplorerRowKind.Search => "inspector.kindSearch",
        ExplorerRowKind.Group => "inspector.kindGroup",
        _ => "inspector.kindType",
    }];

    /// <summary>Shows a selected folder, collection, group or type row: its name, what it is and how many entries it holds.</summary>
    public void ShowRow(ExplorerRow row)
    {
        _version++;
        (_workspace, _key, _count, _id, _row) = (null, null, null, null, row);
        (_title.Text, _realName.IsVisible, _type.Text) = (row.Text, false, KindOf(row));
        _uses.Text = Loc.Current.Format("explorer.count", ("count", row.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)), ("n", row.Count));
        _find.IsVisible = _findUses.IsVisible = _lock.IsVisible = false;
    }

    /// <summary>The tools of the tab in front (the layers of a room), shown above the resource summary; null when the tab has none.</summary>
    public void SetContext(Control? content) => _context.Content = content;

    /// <summary>Shows the entry with this Explorer key, or the empty hint when there is none.</summary>
    public async Task ShowAsync(Workspace? workspace, string? key)
    {
        var version = ++_version;
        (_count, _id, _row) = (null, null, null);
        (_workspace, _key) = (null, null); // the checkbox is set below without writing a lock
        var split = key is null ? null : ResourceTypes.SplitKey(key);
        if (workspace is null || split is not var (type, name) || ResourceTypes.Find(workspace.Data, key ?? "") is not { } resource)
        {
            _title.Text = Loc.Current["inspector.none"]; // not a binding: a binding would put this text back over a name on the next language change
            (_type.Text, _uses.Text, _realName.IsVisible) = ("", "", false);
            _find.IsVisible = _findUses.IsVisible = _lock.IsVisible = false;
            return;
        }

        // The nickname is the title; the real name, the one code uses, stays visible under it.
        var nickname = workspace.Nicknames.Of(key ?? "");
        var general = key == ResourceTypes.KeyOf("general", ResourceTypes.GeneralInfoName); // the internal name stays; the title is in the program language
        var shown = nickname ?? (general ? Loc.Current["explorer.generalName"] : workspace.Nicknames.CodeLabel(name));
        (_title.Text, _realName.Text, _realName.IsVisible) = (shown, name, shown != name && !general);
        _id = ResourceTypes.IdOf(workspace.Data, key ?? "");
        _type.Text = TypeLine(type);
        _uses.Text = "";
        _find.IsVisible = _findUses.IsVisible = _lock.IsVisible = true;
        _lock.IsChecked = workspace.Locks.IsLocked(key ?? "");
        (_workspace, _key) = (workspace, key);
        try
        {
            var count = await UsageSearch.CountAsync(workspace, resource);
            if (version != _version) return; // a newer selection replaced this count otherwise
            _count = count;
            _uses.Text = Loc.Current.Format("inspector.uses", ("count", count));
        }
        catch (InvalidOperationException)
        {
            // The file was closed while counting; the panel is about to be reset.
        }
    }
}
