using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;
/// <summary>The vertices of an object's physics shape as a list with X and Y boxes: Add puts a vertex at the end, Set moves the picked one, Remove deletes it (UTMT-OBJ-03).</summary>
public sealed class PhysicsVerticesView : StackPanel
{
    private readonly GameMakerGameObject _object;
    private readonly Workspace _workspace;
    private readonly ListBox _list = new() { Name = "PhysicsVertices", MaxHeight = 120 };
    private readonly TextBox _x = new() { Name = "VertexX", Width = 90 };
    private readonly TextBox _y = new() { Name = "VertexY", Width = 90 };

    public PhysicsVerticesView(GameMakerGameObject obj, Workspace workspace)
    {
        (_object, _workspace) = (obj, workspace);
        Name = "PhysicsVerticesView";
        Spacing = 4;
        _x.PlaceholderText = "X";
        _y.PlaceholderText = "Y";
        var add = new Button { Name = "VertexAdd", Content = Loc.Current["object.vertexAdd"] };
        var set = new Button { Name = "VertexSet", Content = Loc.Current["object.vertexSet"] };
        var remove = new Button { Name = "VertexRemove", Content = Loc.Current["timelines.remove"] };
        add.Click += (_, _) => Edit(PhysicsVertexCommand.Add, -1);
        set.Click += (_, _) => Edit(PhysicsVertexCommand.Set, _list.SelectedIndex);
        remove.Click += (_, _) => Edit(PhysicsVertexCommand.Remove, _list.SelectedIndex, true);
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedIndex is >= 0 and var row && row < _object.PhysicsVertices.Count) (_x.Text, _y.Text) = (Format(_object.PhysicsVertices[row].X), Format(_object.PhysicsVertices[row].Y));
        };
        Children.Add(new TextBlock { Text = Loc.Current["object.vertices"], Classes = { "muted" } });
        Children.Add(_list);
        Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { _x, _y, add, set, remove } });
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    public int Count => _object.PhysicsVertices.Count;

    private static string Format(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>One edit from the boxes; a box that is not a number does nothing. Remove needs no numbers.</summary>
    public bool Edit(string operation, int index, bool needsNoNumbers = false)
    {
        var parsed = float.TryParse(_x.Text, System.Globalization.CultureInfo.InvariantCulture, out var x) & float.TryParse(_y.Text, System.Globalization.CultureInfo.InvariantCulture, out var y);
        if ((!parsed && !needsNoNumbers) || (operation != PhysicsVertexCommand.Add && (index < 0 || index >= Count))) return false;
        _workspace.Edit(new PhysicsVertexCommand(_object.Name.Content ?? "", operation, index, x, y));
        return true;
    }

    public void SetBoxes(string x, string y) => (_x.Text, _y.Text) = (x, y);

    private void Reload()
    {
        _list.ItemsSource = _object.PhysicsVertices.Select((v, i) => $"{i}:  {Format(v.X)}, {Format(v.Y)}").ToList();
        // Without a physics shape the editor only takes room; vertices that already exist stay visible.
        IsVisible = _object.UsesPhysics || Count > 0;
    }
}

/// <summary>The tile layout of a tile set in one line: tile size, columns, count, the border the exporter adds around each tile and the separation between tiles (UTMT-BG-05). Follows edits.</summary>
public sealed class TileSetInfoView : TextBlock
{
    public TileSetInfoView(GameMakerBackground background, Workspace? workspace = null)
    {
        Name = "TileSetInfo";
        Classes.Add("muted");
        Margin = new Thickness(0, 4);
        Text = Describe(background);
        if (workspace is null) return;
        void Reload() => Text = Describe(background);
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
    }

    public static string Describe(GameMakerBackground background) => Loc.Current.Format("tilesets.info",
        ("width", background.GMS2TileWidth), ("height", background.GMS2TileHeight), ("columns", background.GMS2TileColumns), ("count", background.GMS2TileCount),
        ("borderX", background.GMS2OutputBorderX), ("borderY", background.GMS2OutputBorderY), ("separationX", background.GMS2TileSeparationX), ("separationY", background.GMS2TileSeparationY));
}

/// <summary>The emitters of a particle system, one line each: name, on or off, emit mode and count, shape and region size.</summary>
public sealed class ParticleEmittersView : StackPanel
{
    private readonly GameMakerParticleSystem _system;
    private readonly Workspace? _workspace;

    private readonly Action<GameMakerParticleSystemEmitter?>? _solo;

    private readonly Action<string>? _editImage;

    /// <summary>The constructor takes an editImage callback that opens the image editor on an Explorer key; an emitter with a sprite gets an Edit Image button when it is given.</summary>
    public ParticleEmittersView(GameMakerParticleSystem system, Workspace? workspace = null, Action<GameMakerParticleSystemEmitter?>? solo = null, Action<string>? editImage = null)
    {
        (_system, _workspace, _solo, _editImage) = (system, workspace, solo, editImage);
        Name = "ParticleEmitters";
        Lines = [];
        if (workspace is not null)
        {
            AttachedToVisualTree += (_, _) => workspace.History.Changed += Rebuild;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Rebuild;
        }

        Rebuild();
    }

    public IReadOnlyList<string> Lines { get; private set; }

    private void Rebuild()
    {
        Lines = [.. (_system.Emitters ?? []).Select(r => r.Resource).OfType<GameMakerParticleSystemEmitter>().Select(e => string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{e.Name?.Content}  {(e.Enabled ? "on" : "off")}  {e.Mode} x{e.EmitCount}  {e.Shape} {e.RegionX:0.##} x {e.RegionY:0.##}"))];
        if (_workspace is not { } workspace)
        {
            ItemsSource = Lines;
            return;
        }

        // With a workspace: each emitter opens into its property list and has a Remove button; Add makes a new emitter with default settings.
        var name = _system.Name.Content ?? "";
        var items = Lines.Select((line, i) =>
        {
            var remove = new Button { Name = "EmitterRemove_" + i, Content = Loc.Current["timelines.remove"] }.NamedFor("timelines.remove", line);
            remove.Click += (_, _) => workspace.Edit(new ParticleEmitterCommand(name, ParticleEmitterCommand.Remove, i));
            var copy = new Button { Name = "EmitterDuplicate_" + i, Content = Loc.Current["editors.particles.duplicate"] };
            copy.Click += (_, _) => workspace.Edit(new ParticleEmitterCommand(name, ParticleEmitterCommand.Duplicate, i));
            var expander = Expandable(line, workspace, "particleSystems", name, $"Emitters[{i}].Resource");
            var eye = new CheckBox { Name = "EmitterEye_" + i, Content = Loc.Current["editors.particles.eye"], IsChecked = Emitter(i)?.Enabled ?? false };
            eye.IsCheckedChanged += (_, _) =>
            {
                if (eye.IsChecked != (Emitter(i)?.Enabled ?? false)) workspace.Edit(new SetPropertyCommand("particleSystems", name, $"Emitters[{i}].Resource.Enabled", eye.IsChecked == true ? "true" : "false"));
            };
            var lockBox = new CheckBox { Name = "EmitterLock_" + i, Content = Loc.Current["editors.particles.lock"] };
            lockBox.IsCheckedChanged += (_, _) => expander.IsEnabled = lockBox.IsChecked != true; // editor state only, the file has no lock
            var solo = new CheckBox { Name = "EmitterSolo_" + i, Content = Loc.Current["editors.particles.solo"], IsChecked = _soloIndex == i, IsVisible = _solo is not null };
            solo.IsCheckedChanged += (_, _) =>
            {
                _soloIndex = solo.IsChecked == true ? i : -1;
                _solo?.Invoke(_soloIndex < 0 ? null : Emitter(_soloIndex)); // editor state only; the preview shows just this emitter
                Rebuild();
            };
            var tools = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Margin = new Avalonia.Thickness(12, 0, 40, 0), Children = { eye, solo, lockBox, copy, remove } };
            if (_editImage is { } open && PictureRefs.KeyOf(Emitter(i)) is { } picture)
            {
                var edit = new Button { Name = "EmitterEditImage_" + i, Content = Loc.Current["textureItem.edit"] }.NamedFor("textureItem.edit", line);
                edit.Click += (_, _) => open(picture);
                tools.Children.Insert(0, edit);
            }

            var header = new DockPanel { LastChildFill = true, Children = { tools, new TextBlock { Text = line, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } } };
            DockPanel.SetDock(tools, Avalonia.Controls.Dock.Right);
            expander.Header = header;
            return (object)expander;
        }).ToList();
        var add = new Button { Name = "EmitterAdd", Content = Loc.Current["particles.add"] };
        add.Click += (_, _) => workspace.Edit(new ParticleEmitterCommand(name, ParticleEmitterCommand.Add));
        items.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { add, CopyGmlButton() } });
        ItemsSource = items;
    }

    private int _soloIndex = -1;

    private GameMakerParticleSystemEmitter? Emitter(int index) => _system.Emitters.ElementAtOrDefault(index)?.Resource;

    private Button CopyGmlButton()
    {
        var button = new Button { Name = "ParticleCopyGml", Content = Loc.Current["editors.particles.copyGml"] };
        button.Click += (_, _) => _ = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(G3MStudio.Core.Particles.ParticleGml.Generate(_system));
        return button;
    }

    /// <summary>A header line that opens the property list of one part of a resource, built when first opened.</summary>
    internal static Expander Expandable(string header, Workspace workspace, string type, string name, string subPath)
    {
        var expander = new Expander { Header = header, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        expander.Expanded += (_, _) => expander.Content ??= new PropertyGridTab(workspace, type, name, null, subPath) { MaxHeight = 320 };
        return expander;
    }

    // The list is built from plain controls: an items control in a docked header got no size in the tab.
    private IEnumerable<object> ItemsSource
    {
        set
        {
            Children.Clear();
            foreach (var item in value) Children.Add(item as Control ?? new TextBlock { Text = item.ToString() });
        }
    }
}
