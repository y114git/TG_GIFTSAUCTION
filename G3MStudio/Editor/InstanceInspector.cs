using System.Globalization;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Editor;

/// <summary>
/// Number fields and an object picker for the selected instances of a room. Typing a value sets it on every selected instance as one
/// undoable edit. With several selected the fields show the first instance; a field you do not touch is not changed.
/// </summary>
public sealed class InstanceInspector : StackPanel
{
    private static readonly (string Field, string Label)[] Numbers =
    [
        ("x", "rooms.inspector.x"), ("y", "rooms.inspector.y"), ("scalex", "rooms.inspector.scaleX"), ("scaley", "rooms.inspector.scaleY"),
        ("rotation", "rooms.inspector.rotation"), ("imageindex", "rooms.inspector.frame"), ("imagespeed", "rooms.inspector.speed"),
    ];

    private readonly Workspace _workspace;
    private readonly string _room;
    private readonly Dictionary<string, NumericUpDown> _boxes = [];
    private readonly ComboBox _object = new() { Name = "InstanceObject", MinWidth = 160 };
    private readonly TextBlock _title = new() { Name = "InstanceTitle", Classes = { "muted" } };
    private IReadOnlyList<GameObject> _selection = [];
    private bool _building;

    public InstanceInspector(Workspace workspace, string room)
    {
        (_workspace, _room) = (workspace, room);
        Name = "InstanceInspector";
        Spacing = 4;
        Children.Add(_title);
        foreach (var (field, label) in Numbers)
        {
            var box = new NumericUpDown { Name = "Instance_" + field, FormatString = field is "x" or "y" or "imageindex" ? "0" : "0.###", Increment = field is "x" or "y" or "imageindex" ? 1 : 0.1m, MinWidth = 120 };
            box.ValueChanged += (_, e) =>
            {
                if (!_building && e.NewValue is { } value) Set(field, value.ToString(CultureInfo.InvariantCulture));
            };
            _boxes[field] = box;
            Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Loc.Current[label], MinWidth = 70, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, box } });
        }

        _object.ItemsSource = workspace.Data.GameObjects.Select(o => o.Name.Content).OfType<string>().ToList();
        _object.SelectionChanged += (_, _) =>
        {
            if (!_building && _object.SelectedItem is string name) Set("object", name);
        };
        Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Loc.Current["rooms.inspector.object"], MinWidth = 70, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, _object } });
        Show([]);
    }

    /// <summary>Fills the fields from the first selected instance; with nothing selected they are disabled.</summary>
    public void Show(IReadOnlyList<GameObject> selection)
    {
        _building = true;
        _selection = selection;
        var first = selection.Count > 0 ? selection[0] : null;
        _title.Text = first is null ? Loc.Current["rooms.inspector.none"] : Loc.Current.Format("rooms.inspector.title", ("id", first.InstanceID), ("count", selection.Count));
        foreach (var (field, box) in _boxes)
        {
            box.IsEnabled = first is not null;
            box.Value = first is null ? null : (decimal)Read(first, field);
        }

        _object.IsEnabled = first is not null;
        _object.SelectedItem = first?.ObjectDefinition?.Name.Content;
        _building = false;
    }

    private static double Read(GameObject i, string field) => field switch
    {
        "x" => i.X,
        "y" => i.Y,
        "scalex" => i.ScaleX,
        "scaley" => i.ScaleY,
        "rotation" => i.Rotation,
        "imageindex" => i.ImageIndex,
        _ => i.ImageSpeed,
    };

    /// <summary>Sets one field on every selected instance; values that are already right are skipped.</summary>
    public void Set(string field, string value)
    {
        var commands = _selection.Select(i => new RoomInstanceCommand(_room, RoomInstanceCommand.Set, i.InstanceID, objectName: field, value: value)).ToList();
        if (commands.Count > 0) _workspace.Edit(commands.Count == 1 ? commands[0] : new GroupCommand(commands));
    }
}
