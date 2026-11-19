using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Editor;

/// <summary>The elements of one layer (or of the whole room) as a list: instances with their object and place, and asset-layer elements by kind and name. Picking a row selects it in the room.</summary>
public sealed class RoomElementList : ListBox
{
    // Without this a subclass finds no control theme and draws nothing.
    protected override Type StyleKeyOverride => typeof(ListBox);

    private static readonly string[] Kinds = [RoomElements.Sprite, RoomElements.Sequence, RoomElements.Particle, RoomElements.Text];

    private readonly GameMakerRoom _room;
    private readonly Nicknames? _nicknames;
    private bool _building;

    public RoomElementList(GameMakerRoom room, Nicknames? nicknames = null)
    {
        (_room, _nicknames) = (room, nicknames);
        Name = "RoomElements";
        MinHeight = 76;
        SelectionMode = SelectionMode.Multiple; // a click selects one row, Ctrl+click toggles one, Shift+click takes a range
        ItemTemplate = new FuncDataTemplate<object>((element, _) => element is null ? new StackPanel() : Row(element)); // a row is built from its element once, so it is never reused for another
        // The row of an element is made when it scrolls into view, so a room with thousands of instances builds at once.
        ContainerPrepared += (_, e) =>
        {
            if (e.Container is ListBoxItem { DataContext: { } element } item) HoverCards.Attach(item, HoverKey(element), Describe(element, _nicknames));
        };
        SelectionChanged += (_, _) =>
        {
            if (!_building) PickedChanged?.Invoke([.. Picked()]);
        };
    }

    public event Action<IReadOnlyList<object>>? PickedChanged;

    private readonly HashSet<object> _hidden = new(ReferenceEqualityComparer.Instance);

    /// <summary>The elements whose check box is cleared. They are hidden in the editor view only; nothing is written to the room file and the set is lost when the room closes.</summary>
    public IReadOnlyCollection<object> Hidden => _hidden;

    /// <summary>A check box, Show all or Hide all changed <see cref="Hidden"/>.</summary>
    public event Action? HiddenChanged;

    /// <summary>Ticks every element in the room (<paramref name="shown"/>) or clears every box, including rows the search filter hides.</summary>
    public void SetAllShown(bool shown)
    {
        if (shown) _hidden.Clear();
        else _hidden.UnionWith(Elements);
        Refill(Picked().ToHashSet(ReferenceEqualityComparer.Instance));
        HiddenChanged?.Invoke();
    }

    public IReadOnlyList<object> Elements { get; private set; } = [];

    /// <summary>Only rows whose text holds this (ignoring case) are listed; empty lists all.</summary>
    public string Filter
    {
        get;
        set
        {
            field = value;
            Refill(Picked().ToHashSet(ReferenceEqualityComparer.Instance));
        }
    } = "";

    // Rows whose description holds the filter text, with the real names or with the nicknames; all rows when it is empty.
    public IEnumerable<object> Shown => Elements.Where(e => Filter.Length == 0 || Describe(e).Contains(Filter, StringComparison.OrdinalIgnoreCase) || Describe(e, _nicknames).Contains(Filter, StringComparison.OrdinalIgnoreCase));

    /// <summary>The rows are made again, after a nickname changed.</summary>
    public void Relabel() => Refill(Picked().ToHashSet(ReferenceEqualityComparer.Instance));

    /// <summary>Lists the elements of the layer, or of every layer when none is given.</summary>
    public void Show(Layer? layer)
    {
        Elements = _room.Layers is null ? [.. _room.GameObjects ?? []] : [.. (layer is null ? _room.Layers : [layer]).SelectMany(Of)];
        IsVisible = Elements.Count > 0; // an empty list would only be a blank band under the canvas
        Refill([]);
    }

    /// <summary>Marks the rows of the elements selected in the room without telling the room again.</summary>
    public void Mark(IEnumerable<object> selected)
    {
        var set = selected.ToHashSet(ReferenceEqualityComparer.Instance);
        Refill(set);
    }

    /// <summary>The row text of an element; with <paramref name="nicknames"/> the object, sprite, sequence or particle system shows its nickname.</summary>
    public static string Describe(object element, Nicknames? nicknames = null)
    {
        var place = string.Create(CultureInfo.InvariantCulture, $"({RoomElements.X(element)}, {RoomElements.Y(element)})");
        if (element is GameObject o)
            return string.Create(CultureInfo.InvariantCulture, $"inst_{o.InstanceID:X8} : {Label(nicknames, "objects", o.ObjectDefinition?.Name.Content ?? "?")}  {place}");
        var (kind, name) = (RoomElements.KindOf(element), RoomElements.NameOf(element));
        var type = kind == RoomElements.Sprite ? "sprites" : kind == RoomElements.Sequence ? "sequences" : kind == RoomElements.Particle ? "particleSystems" : "";
        // The element's own name stays unless the sprite, sequence or particle system it uses has a nickname.
        var shown = type.Length > 0 && ResourceOf(element) is { } resource ? nicknames?.Of(type, resource) ?? name : name;
        return $"{kind} {shown}  {place}";
    }

    /// <summary>The Explorer key of the resource an element shows, for its hover card: the object of an instance, the sprite, sequence or particle system of the others.</summary>
    internal static string? HoverKey(object element) => element switch
    {
        GameObject o when o.ObjectDefinition?.Name?.Content is { } name => ResourceTypes.KeyOf("objects", name),
        SpriteInstance s when s.Sprite?.Name?.Content is { } name => ResourceTypes.KeyOf("sprites", name),
        SequenceInstance q when q.Sequence?.Name?.Content is { } name => ResourceTypes.KeyOf("sequences", name),
        ParticleSystemInstance p when p.ParticleSystem?.Name?.Content is { } name => ResourceTypes.KeyOf("particleSystems", name),
        _ => null,
    };

    private static string? ResourceOf(object element) => element switch
    {
        SpriteInstance s => s.Sprite?.Name?.Content,
        SequenceInstance q => q.Sequence?.Name?.Content,
        ParticleSystemInstance p => p.ParticleSystem?.Name?.Content,
        _ => null,
    };

    private static string Label(Nicknames? nicknames, string type, string name) => nicknames?.Label(type, name) ?? name;

    internal static IEnumerable<object> Of(Layer layer)
    {
        foreach (var instance in layer.InstancesData?.Instances ?? []) yield return instance;
        foreach (var kind in Kinds)
            foreach (var element in RoomElements.ListOf(layer, kind) ?? new List<object>()) yield return element;
    }

    private IEnumerable<object> Picked() => SelectedItems?.OfType<object>() ?? [];

    private StackPanel Row(object element)
    {
        // Ticked = shown in the editor, like the check box of an instance in GameMaker. It is not the selection (the row highlight is) and not a room property.
        var box = new CheckBox { Name = "ElementCheck", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, IsChecked = !_hidden.Contains(element) };
        ToolTip.SetTip(box, Loc.Current["rooms.elementShown"]);
        box.IsCheckedChanged += (_, _) =>
        {
            if (_building || (box.IsChecked != true) == _hidden.Contains(element)) return;
            if (box.IsChecked == true) _hidden.Remove(element);
            else _hidden.Add(element);
            HiddenChanged?.Invoke();
        };
        var line = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        line.Children.Add(box);
        // The picture of the object (or sprite) the element shows, from the shared cache; only rows in view ask for it.
        if (HoverKey(element) is { } key) line.Children.Add(LinkThumb.Make(key, "ElementThumb"));
        line.Children.Add(new TextBlock { Text = Describe(element, _nicknames), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        return line;
    }

    private void Refill(HashSet<object> selected)
    {
        _building = true;
        var shown = Shown.ToList();
        ItemsSource = shown;
        SelectedItems?.Clear();
        foreach (var element in shown.Where(selected.Contains)) SelectedItems?.Add(element);
        _building = false;
    }
}
