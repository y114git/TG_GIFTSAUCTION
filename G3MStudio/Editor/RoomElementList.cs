using System.Globalization;
using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Editor;

/// <summary>The elements of one layer (or of the whole room) as a list: instances with their object and place, and asset-layer elements by kind and name. Picking a row selects it in the room.</summary>
public sealed class RoomElementList : ListBox
{
    private static readonly string[] Kinds = [RoomElements.Sprite, RoomElements.Sequence, RoomElements.Particle, RoomElements.Text];

    private readonly GameMakerRoom _room;
    private bool _building;

    public RoomElementList(GameMakerRoom room)
    {
        _room = room;
        Name = "RoomElements";
        Height = 110;
        SelectionMode = SelectionMode.Multiple;
        SelectionChanged += (_, _) =>
        {
            if (!_building) PickedChanged?.Invoke([.. SelectedItems?.OfType<ListBoxItem>().Select(i => i.Tag).OfType<object>() ?? []]);
        };
    }

    public event Action<IReadOnlyList<object>>? PickedChanged;

    public IReadOnlyList<object> Elements { get; private set; } = [];

    /// <summary>Lists the elements of the layer, or of every layer when none is given.</summary>
    public void Show(Layer? layer)
    {
        Elements = [.. (layer is null ? _room.Layers ?? [] : [layer]).SelectMany(Of)];
        Refill([]);
    }

    /// <summary>Marks the rows of the elements selected in the room without telling the room again.</summary>
    public void Mark(IEnumerable<object> selected)
    {
        var set = selected.ToHashSet(ReferenceEqualityComparer.Instance);
        Refill(set);
    }

    public static string Describe(object element)
    {
        var place = string.Create(CultureInfo.InvariantCulture, $"({RoomElements.X(element)}, {RoomElements.Y(element)})");
        return element is GameObject o
            ? string.Create(CultureInfo.InvariantCulture, $"inst_{o.InstanceID:X8} : {o.ObjectDefinition?.Name.Content ?? "?"}  {place}")
            : $"{RoomElements.KindOf(element)} {RoomElements.NameOf(element)}  {place}";
    }

    internal static IEnumerable<object> Of(Layer layer)
    {
        foreach (var instance in layer.InstancesData?.Instances ?? []) yield return instance;
        foreach (var kind in Kinds)
            foreach (var element in RoomElements.ListOf(layer, kind) ?? new List<object>()) yield return element;
    }

    private void Refill(HashSet<object> selected)
    {
        _building = true;
        ItemsSource = Elements.Select(e => new ListBoxItem { Content = Describe(e), Tag = e }).ToList();
        SelectedItems?.Clear();
        foreach (var item in Items.OfType<ListBoxItem>().Where(i => i.Tag is { } tag && selected.Contains(tag))) SelectedItems?.Add(item);
        _building = false;
    }
}
