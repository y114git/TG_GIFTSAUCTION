using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// One UI layer of the data file as a tree of its nodes: flex panels, text items, sprites, game objects and sequences. Add a node under the picked one,
/// delete it, or edit its flex properties in typed fields; each is one undoable edit. A canvas beside the fields draws the flex layout and follows every edit.
/// </summary>
public sealed class UiLayerView : StackPanel
{
    public const double PreviewWidth = 320, PreviewHeight = 180;

    private readonly GameMakerUIRootNode _root;
    private readonly Workspace _workspace;
    private readonly ListBox _tree = new() { Name = "UiLayerTree", MaxHeight = 240 };
    private readonly TextBox _name = new() { Name = "UiLayerNewName", MinWidth = 140, Text = "" };
    private readonly TextBlock _message = new() { Name = "UiLayerMessage", Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _fields = new() { Name = "UiLayerFields", Spacing = 4 };
    private readonly Canvas _canvas = new() { Name = "UiLayerPreview", Width = PreviewWidth, Height = PreviewHeight, ClipToBounds = true };
    private readonly Dictionary<string, ComboBox> _assets = [];
    private List<string> _paths = [];
    private bool _filling;

    public UiLayerView(GameMakerUIRootNode root, Workspace workspace)
    {
        (_root, _workspace) = (root, workspace);
        Name = "UiLayerView";
        Spacing = 4;
        Children.Add(new TextBlock { Text = Loc.Current["uiLayer.title"], FontWeight = FontWeight.SemiBold });
        Children.Add(_tree);
        Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { _name, Command("UiLayerAddPanel", "uiLayer.addPanel", () => Add(UiLayerCommand.AddPanel)), Command("UiLayerAddText", "uiLayer.addText", () => Add(UiLayerCommand.AddText)), Command("UiLayerDelete", "uiLayer.delete", () => DeleteNode()) },
        });
        Children.Add(AssetRow(UiLayerCommand.AddSprite, "Sprite", "uiLayer.addSprite"));
        Children.Add(AssetRow(UiLayerCommand.AddObject, "Object", "uiLayer.addObject"));
        Children.Add(AssetRow(UiLayerCommand.AddSequence, "Sequence", "uiLayer.addSequence"));
        Children.Add(_message);
        Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { _fields, new Border { Child = _canvas, VerticalAlignment = VerticalAlignment.Top, Background = (IBrush?)Application.Current?.FindResource("Bg.Raised") } },
        });
        _tree.SelectionChanged += (_, _) =>
        {
            if (_filling) return;
            FillFields();
            Draw();
        };
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    /// <summary>The path of the picked node (names from the layer down), or the layer itself when nothing is picked.</summary>
    public string Picked => _tree.SelectedIndex >= 0 && _tree.SelectedIndex < _paths.Count ? _paths[_tree.SelectedIndex] : LayerName;

    /// <summary>The boxes of the layout preview, as drawn now.</summary>
    public IReadOnlyList<UiBox> Boxes => UiFlexLayout.Compute(_root, PreviewWidth, PreviewHeight);

    private string LayerName => UiLayerCommand.NameOf(_root.Node) ?? "";

    private static Button Command(string name, string key, Action run)
    {
        var button = new Button { Name = name, Content = Loc.Current[key], VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => run();
        return button;
    }

    private StackPanel AssetRow(string operation, string kind, string key)
    {
        var box = new ComboBox { Name = "UiLayerAsset" + kind, MinWidth = 140, ItemsSource = AssetNames(kind) };
        box.SelectedIndex = box.ItemCount > 0 ? 0 : -1;
        _assets[kind] = box;
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { box, Command("UiLayerAdd" + kind, key, () => AddAsset(operation, box.SelectedItem as string ?? "")) } };
    }

    private List<string> AssetNames(string kind)
    {
        var data = _workspace.Data;
        IEnumerable<string?> names = kind switch
        {
            "Sprite" => data.Sprites.Select(s => s.Name?.Content),
            "Object" => data.GameObjects.Select(o => o.Name?.Content),
            _ => data.Sequences.Select(s => s.Name?.Content),
        };
        return [.. names.OfType<string>()];
    }

    public void Select(string path) => _tree.SelectedIndex = _paths.IndexOf(path);

    /// <summary>Adds a node under the picked one with the name in the box (a free name such as panel_1 when it is empty). False with a message when the edit is refused.</summary>
    public bool Add(string operation)
    {
        var stem = operation == UiLayerCommand.AddPanel ? "panel" : "text";
        return Run(new UiLayerCommand(operation, Picked, FreeName(stem)));
    }

    /// <summary>Adds a sprite, game object or sequence node that shows <paramref name="asset"/>. An object node is named by its object.</summary>
    public bool AddAsset(string operation, string asset) => Run(new UiLayerCommand(operation, Picked, FreeName(asset), asset));

    private string FreeName(string stem)
    {
        var taken = ChildNames(Picked);
        var wanted = (_name.Text ?? "").Trim();
        var name = wanted.Length > 0 ? wanted : stem;
        for (var i = 1; wanted.Length == 0 && taken.Contains(name); i++) name = stem + "_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return name;
    }

    /// <summary>Deletes the picked node; the layer itself is deleted from the explorer.</summary>
    public bool DeleteNode() => _tree.SelectedIndex >= 0 && _paths[_tree.SelectedIndex] != LayerName && Run(new UiLayerCommand(UiLayerCommand.Delete, _paths[_tree.SelectedIndex]));

    /// <summary>Sets one typed property of the picked node from text; false with a message when the value is refused.</summary>
    public bool SetProperty(string property, string text) => Run(new UiLayerCommand(UiLayerCommand.SetProp, Picked, property, "", text));

    private bool Run(UiLayerCommand command)
    {
        try
        {
            _workspace.Edit(command);
            Reload();
            _message.Text = "";
            return true;
        }
        catch (InvalidOperationException e)
        {
            _message.Text = e.Message;
            FillFields();
            return false;
        }
    }

    private HashSet<string?> ChildNames(string path)
    {
        IEnumerable<GameMakerUINode> list = _root.Node is GameMakerUIContainer top ? top.Children : [];
        foreach (var name in path.Split('/').Skip(1))
            list = list.FirstOrDefault(n => UiLayerCommand.NameOf(n.Node) == name) is { } next ? UiLayerCommand.ChildrenOf(next.Node) ?? [] : [];
        return [.. list.Select(n => UiLayerCommand.NameOf(n.Node))];
    }

    private void Reload()
    {
        var picked = Picked;
        var rows = new List<string>();
        _paths = [];
        void Walk(IEnumerable<GameMakerUINode> nodes, string path, int depth)
        {
            foreach (var node in nodes)
            {
                var name = UiLayerCommand.NameOf(node.Node) ?? "?";
                _paths.Add(path + "/" + name);
                rows.Add(new string(' ', depth * 3) + name + "  (" + node.Node.GetType().Name.Replace("GameMakerUI", "", StringComparison.Ordinal) + ")");
                Walk(UiLayerCommand.ChildrenOf(node.Node) ?? [], path + "/" + name, depth + 1);
            }
        }

        var layer = LayerName;
        _paths.Add(layer);
        rows.Add(layer);
        Walk(_root.Node is GameMakerUIContainer container ? container.Children : [], layer, 1);
        _filling = true;
        _tree.ItemsSource = rows;
        _tree.SelectedIndex = Math.Max(0, _paths.IndexOf(picked));
        _filling = false;
        foreach (var (kind, box) in _assets)
        {
            var keep = box.SelectedItem as string;
            box.ItemsSource = AssetNames(kind);
            box.SelectedItem = keep ?? (box.ItemCount > 0 ? box.Items[0] : null);
        }

        FillFields();
        Draw();
    }

    private IGameMakerUINodeData? PickedNode()
    {
        var parts = Picked.Split('/');
        if (parts.Length == 1) return _root.Node;
        IEnumerable<GameMakerUINode> list = _root.Node is GameMakerUIContainer top ? top.Children : [];
        GameMakerUINode? node = null;
        foreach (var part in parts.Skip(1))
        {
            node = list.FirstOrDefault(n => UiLayerCommand.NameOf(n.Node) == part);
            if (node is null) return null;
            list = UiLayerCommand.ChildrenOf(node.Node) ?? [];
        }

        return node?.Node;
    }

    private void FillFields()
    {
        _fields.Children.Clear();
        if (PickedNode() is not { } node) return;
        string? group = null;
        foreach (var prop in UiLayerProps.For(node))
        {
            if (prop.Group != group)
            {
                group = prop.Group;
                _fields.Children.Add(new TextBlock { Text = Loc.Current["uiLayer.group." + group], FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
            }

            _fields.Children.Add(Field(node, prop));
        }
    }

    private StackPanel Field(IGameMakerUINodeData node, UiProp prop)
    {
        var current = UiLayerProps.Get(node, prop.Name);
        // Enter edits and reloads; the old box then loses focus with its old text, which must not edit again.
        bool Differs(string text) => text != UiLayerProps.Get(node, prop.Name);
        Control editor;
        switch (prop.Kind)
        {
            case UiPropKind.Choice:
                var combo = new ComboBox { Name = "UiProp_" + prop.Name, ItemsSource = Enum.GetNames(prop.Choices ?? typeof(bool)), SelectedItem = current, MinWidth = 140 };
                combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string pick && Differs(pick)) SetProperty(prop.Name, pick); };
                editor = combo;
                break;
            case UiPropKind.Toggle:
                var check = new CheckBox { Name = "UiProp_" + prop.Name, IsChecked = current == "True" };
                check.IsCheckedChanged += (_, _) => { if (Differs(check.IsChecked == true ? "True" : "False")) SetProperty(prop.Name, check.IsChecked == true ? "True" : "False"); };
                editor = check;
                break;
            default:
                var box = new TextBox { Name = "UiProp_" + prop.Name, Text = current, MinWidth = 100 };
                box.LostFocus += (_, _) => { if (Differs(box.Text ?? "")) SetProperty(prop.Name, box.Text ?? ""); };
                box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && Differs(box.Text ?? "")) SetProperty(prop.Name, box.Text ?? ""); };
                editor = box;
                break;
        }

        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Loc.Current["uiLayer.prop." + prop.Name], MinWidth = 120, VerticalAlignment = VerticalAlignment.Center }, editor } };
    }

    private void Draw()
    {
        _canvas.Children.Clear();
        var accent = (IBrush?)Application.Current?.FindResource("Text.Accent") ?? Brushes.White;
        var muted = (IBrush?)Application.Current?.FindResource("Text.Muted") ?? Brushes.Gray;
        var fill = (Application.Current?.FindResource("Text.Accent") as ISolidColorBrush)?.Color ?? Colors.SteelBlue;
        var picked = Picked;
        foreach (var box in Boxes)
        {
            var on = box.Path == picked;
            var rect = new Rectangle
            {
                Width = Math.Max(0, box.Width),
                Height = Math.Max(0, box.Height),
                Fill = new SolidColorBrush(fill, Math.Min(0.35, 0.06 + 0.07 * box.Depth)),
                Stroke = on ? accent : null,
                StrokeThickness = on ? 2 : 0,
            };
            Canvas.SetLeft(rect, box.X);
            Canvas.SetTop(rect, box.Y);
            _canvas.Children.Add(rect);
            if (box.Depth == 0) continue;
            var label = new TextBlock { Text = box.Name, Foreground = muted, IsHitTestVisible = false };
            label.TextSize("FontSize.Caption");
            Canvas.SetLeft(label, box.X + 2);
            Canvas.SetTop(label, box.Y + 1);
            _canvas.Children.Add(label);
        }
    }
}
