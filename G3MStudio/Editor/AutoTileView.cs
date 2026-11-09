using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// The auto-tile rule sets of one tile set (Studio keeps them in its own sidecar, see <see cref="AutoTileStore"/>): pick or create a rule set from the
/// 47, 16 or 256 slot template, click a slot, then click a tile in the picture to assign it. Auto-fill assigns all slots from a tile set drawn like the
/// standard sheet. Rule sets import from and export to JSON. Every change is saved at once.
/// </summary>
public sealed class AutoTileView : StackPanel
{
    private readonly GameMakerBackground _set;
    private readonly string _setName;
    private readonly AutoTileStore _store;
    private readonly ComboBox _sets = new() { Name = "AutoTileSets", MinWidth = 140 };
    private readonly ComboBox _layout = new() { Name = "AutoTileLayout", MinWidth = 110, ItemsSource = new[] { "47", "16", "256" } };
    private readonly TextBox _name = new() { Name = "AutoTileName", MinWidth = 140 };
    private readonly NumericUpDown _terrain = new() { Name = "AutoTileTerrain", Minimum = 1, Maximum = 255, FormatString = "0", Width = 100, Value = 1 };
    private readonly NumericUpDown _first = new() { Name = "AutoTileFirst", Minimum = 1, Maximum = 65535, FormatString = "0", Width = 100, Value = 1 };
    private readonly NumericUpDown _columns = new() { Name = "AutoTileColumns", Minimum = 1, Maximum = 256, FormatString = "0", Width = 100, Value = 8 };
    private readonly WrapPanel _slots = new() { Name = "AutoTileSlots" };
    private readonly TextBlock _message = new() { Name = "AutoTileMessage", Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _arm = new() { Name = "AutoTileArm" };
    private bool _building;

    public AutoTileView(GameMakerBackground set, AutoTileStore store)
    {
        (_set, _store) = (set, store);
        _setName = ResourceTypes.NameOf(set);
        Name = "AutoTileView";
        Spacing = 4;
        Children.Add(new TextBlock { Text = Loc.Current["autoTile.title"], FontWeight = FontWeight.SemiBold });
        _layout.SelectedIndex = (int)RoomOptions.AutoTile.DefaultLayout;
        Children.Add(Row(_sets, Button("AutoTileNew", "autoTile.new", () => New((AutoTileLayout)Math.Max(0, _layout.SelectedIndex))), _layout, Button("AutoTileDelete", "autoTile.delete", Delete)));
        Children.Add(Row(Label("autoTile.name"), _name, Label("autoTile.terrain"), _terrain));
        _arm.Content = Loc.Current["autoTile.arm"];
        Children.Add(_arm);
        Children.Add(_slots);
        Children.Add(Row(Label("autoTile.firstTile"), _first, Label("autoTile.columns"), _columns, Button("AutoTileFill", "autoTile.fill", () => AutoFill((int)(_first.Value ?? 1), (int)(_columns.Value ?? 0)))));
        Children.Add(Row(Button("AutoTileImport", "autoTile.import", () => _ = ImportAsync()), Button("AutoTileExport", "autoTile.export", () => _ = ExportAsync())));
        Children.Add(_message);
        _sets.SelectionChanged += (_, _) => { if (!_building) Choose(_sets.SelectedItem as string); };
        _name.LostFocus += (_, _) => Rename(_name.Text ?? "");
        _terrain.ValueChanged += (_, _) => { if (!_building && Current is { } r && _terrain.Value is { } t && (int)t != r.Terrain) Save(r with { Terrain = (int)t }); };
        AttachedToVisualTree += (_, _) => _store.Changed += Reload;
        DetachedFromVisualTree += (_, _) => _store.Changed -= Reload;
        Reload();
    }

    /// <summary>Replaces the file dialog (title, save or open; null when cancelled). The window sets it; tests set their own.</summary>
    public Func<string, bool, Task<string?>>? FilePicker { get; set; }

    /// <summary>A click on a tile in the picture assigns it to the selected slot; set by a click on a slot, cleared by the check box.</summary>
    public bool Armed => _arm.IsChecked == true;

    /// <summary>The slot waiting for a tile, or -1.</summary>
    public int SelectedSlot { get; private set; } = -1;

    /// <summary>The rule set being edited, or null when the tile set has none.</summary>
    public AutoTileRuleSet? Current => _store.For(_setName).FirstOrDefault(r => r.Name == _current);

    private string? _current;

    private static StackPanel Row(params Control[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var item in items)
        {
            item.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(item);
        }

        return row;
    }

    private static TextBlock Label(string key) => new() { Text = Loc.Current[key], VerticalAlignment = VerticalAlignment.Center };

    private static Button Button(string name, string key, Action run)
    {
        var button = new Button { Name = name, Content = Loc.Current[key] };
        button.Click += (_, _) => run();
        return button;
    }

    /// <summary>Starts a rule set with every slot empty and edits it.</summary>
    public void New(AutoTileLayout layout)
    {
        var rules = AutoTileRuleSet.Empty(_store.FreeName(_setName, Loc.Current["autoTile.defaultName"]), _setName, layout);
        _current = rules.Name;
        _store.Save(rules);
        Choose(rules.Name);
    }

    public void Choose(string? name)
    {
        _current = name;
        SelectedSlot = Current is { } r ? Slots(r)[0] : -1;
        Reload();
    }

    private static IReadOnlyList<int> Slots(AutoTileRuleSet rules) => AutoTiler.Slots(rules.Layout);

    public void Delete()
    {
        if (Current is not { } r) return;
        _store.Remove(_setName, r.Name);
        Choose(_store.At(_setName, 0)?.Name);
    }

    public void SelectSlot(int slot)
    {
        SelectedSlot = slot;
        Reload();
    }

    /// <summary>Gives the selected slot a tile and moves on to the next slot; a tile of 0 clears the slot. False without a rule set, a slot, or when the tile is outside the set.</summary>
    public bool Assign(int tile)
    {
        if (Current is not { } r || SelectedSlot < 0 || tile < 0 || tile >= _set.GMS2TileCount) return false;
        var list = Slots(r);
        var target = SelectedSlot;
        SelectedSlot = list[Math.Min(list.ToList().IndexOf(target) + 1, list.Count - 1)];
        Save(r.With(target, tile));
        return true;
    }

    /// <summary>Assigns every slot from a tile set drawn like the standard sheet, starting at tile <paramref name="first"/>.</summary>
    public void AutoFill(int first, int templateColumns = 0)
    {
        if (Current is not { } r) return;
        Save(AutoTiler.AutoFill(r, first, (int)Math.Max(1, _set.GMS2TileColumns), (int)_set.GMS2TileCount, templateColumns));
    }

    private void Rename(string name)
    {
        name = name.Trim();
        if (_building || Current is not { } r || name.Length == 0 || name == r.Name) return;
        var unique = _store.FreeName(_setName, name);
        _current = unique;
        Save(r with { Name = unique }, r.Name);
    }

    private void Save(AutoTileRuleSet rules, string? oldName = null)
    {
        _current = rules.Name;
        _store.Save(rules, oldName);
    }

    public async Task ExportAsync()
    {
        if (Current is not { } r || FilePicker is null || await FilePicker(Loc.Current["autoTile.export"], true) is not { } path) return;
        try
        {
            UserData.WriteAllTextAtomic(path, AutoTileStore.Export(r));
            _message.Text = "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _message.Text = e.Message;
        }
    }

    /// <summary>Reads a rule set from a JSON file into this tile set. A name already in use gets a number; a damaged file shows a message.</summary>
    public async Task ImportAsync()
    {
        if (FilePicker is null || await FilePicker(Loc.Current["autoTile.import"], false) is not { } path) return;
        try
        {
            if (await AutoTileStore.ImportFileAsync(path) is not { } rules)
            {
                _message.Text = Loc.Current["autoTile.badFile"];
                return;
            }

            var imported = rules with { TileSet = _setName, Name = _store.FreeName(_setName, rules.Name) };
            Save(imported);
            Choose(imported.Name);
            _message.Text = "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _message.Text = e.Message;
        }
    }

    private void Reload()
    {
        _building = true;
        var all = _store.For(_setName);
        if (_current is null || all.All(r => r.Name != _current)) _current = _store.At(_setName, 0)?.Name;
        _sets.ItemsSource = all.Select(r => r.Name).ToList();
        _sets.SelectedItem = _current;
        _slots.Children.Clear();
        var rules = Current;
        _name.Text = rules?.Name ?? "";
        _name.IsEnabled = _terrain.IsEnabled = rules is not null;
        if (rules is not null)
        {
            _terrain.Value = rules.Terrain;
            _columns.Value = AutoTiler.TemplateColumns(rules.Layout);
            foreach (var slot in Slots(rules)) _slots.Children.Add(SlotButton(rules, slot));
        }

        _building = false;
    }

    /// <summary>A button for one slot: the neighbour pattern as three rows of marks and the assigned tile below it.</summary>
    private Button SlotButton(AutoTileRuleSet rules, int slot)
    {
        var tile = rules.Tile(slot);
        var button = new Button
        {
            Name = "AutoTileSlot" + slot.ToString(CultureInfo.InvariantCulture),
            Width = 56,
            Margin = new Thickness(2),
            BorderThickness = new Thickness(slot == SelectedSlot ? 2 : 0),
            Content = new StackPanel { Children = { new TextBlock { Text = Pattern(rules.Layout, slot), FontFamily = new FontFamily("Consolas, Menlo, monospace") }.TextSize("FontSize.Caption"), new TextBlock { Text = tile?.ToString(CultureInfo.InvariantCulture) ?? "-" } } },
        };
        ToolTip.SetTip(button, Loc.Current.Format("autoTile.slotTip", ("slot", slot)));
        button.Click += (_, _) =>
        {
            _arm.IsChecked = true;
            SelectSlot(slot);
        };
        return button;
    }

    /// <summary>Three rows of marks for the neighbours of a slot: # for terrain, . for none, + for the cell itself. A 16 slot shows only the edges.</summary>
    public static string Pattern(AutoTileLayout layout, int slot)
    {
        var mask = layout == AutoTileLayout.Edge16 ? (slot & 1) * AutoTiler.N | (slot >> 1 & 1) * AutoTiler.E | (slot >> 2 & 1) * AutoTiler.S | (slot >> 3 & 1) * AutoTiler.W : slot;
        char C(int bit) => (mask & bit) != 0 ? '#' : '.';
        return $"{C(AutoTiler.NW)}{C(AutoTiler.N)}{C(AutoTiler.NE)}\n{C(AutoTiler.W)}+{C(AutoTiler.E)}\n{C(AutoTiler.SW)}{C(AutoTiler.S)}{C(AutoTiler.SE)}";
    }
}
