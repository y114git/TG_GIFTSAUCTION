using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>The Layers panel under the Toolbox: the list (top layer first) with visibility and lock, the buttons, opacity and blend mode of the active layer.</summary>
public sealed partial class ImageEditorView
{
    private const string EyeIcon = "M12 5C7 5 3 9 2 12c1 3 5 7 10 7s9-4 10-7c-1-3-5-7-10-7zm0 3a4 4 0 100 8 4 4 0 000-8z", LockIcon = "M6 10V8a6 6 0 0112 0v2h2v12H4V10zm2 0h8V8a4 4 0 00-8 0z",
        EyeOffIcon = "M12 5C7 5 3 9 2 12c1 3 5 7 10 7s9-4 10-7c-1-3-5-7-10-7zm0 3a4 4 0 100 8 4 4 0 000-8zM4.3 2.9l16.8 16.8-1.4 1.4L2.9 4.3z"; // the eye with a slash across it

    private readonly StackPanel _layerRows = new() { Name = "ImageLayers", Spacing = 2 };
    private readonly StackPanel _layersPanel = new() { Name = "ImageLayersPanel", Spacing = 2 };
    private readonly NumericUpDown _layerOpacity = new() { Name = "LayerOpacity", Minimum = 0, Maximum = 100, Increment = 5, FormatString = "0", Width = 150 };
    private readonly ComboBox _layerBlend = new() { Name = "LayerBlend", Width = 120 };
    private bool _refreshing;

    /// <summary>The layer whose name is being typed, or -1.</summary>
    public int RenamingLayer { get; private set; } = -1;

    // Ends the rename in progress as if Enter was pressed; null when none is. The rename belongs to the frame shown when it began.
    private Action? _endRename;

    private StackPanel LayersPanel()
    {
        foreach (var blend in Enum.GetValues<LayerBlend>()) _layerBlend.Items.Add(Loc.Current["image.blend." + blend]);
        _layerOpacity.ValueChanged += (_, e) =>
        {
            if (!_refreshing && e.NewValue is { } n && (int)n != Canvas.Layers.Layers[ActiveLayer].Opacity) LayerOp("opacity", ImageOp.Format((int)n), ActiveLayer);
        };
        _layerBlend.SelectionChanged += (_, _) =>
        {
            if (!_refreshing && _layerBlend.SelectedIndex >= 0 && (LayerBlend)_layerBlend.SelectedIndex != Canvas.Layers.Layers[ActiveLayer].Blend) LayerOp("blend", ((LayerBlend)_layerBlend.SelectedIndex).ToString(), ActiveLayer);
        };
        var buttons = new WrapPanel
        {
            ItemSpacing = 4,
            LineSpacing = 4,
            Children =
            {
                Small("LayerAdd", "+", () => Run("img.layerAdd")), Small("LayerDuplicate", Loc.Current["image.layerDuplicate"], () => Run("img.layerDuplicate")),
                Small("LayerDelete", Loc.Current["image.layerDelete"], () => Run("img.layerDelete")), Small("LayerUp", "^", () => Run("img.layerUp")),
                Small("LayerDown", "v", () => Run("img.layerDown")), Small("LayerMerge", Loc.Current["image.layerMergeDown"], () => Run("img.layerMergeDown")),
                Small("LayerRename", Loc.Current["image.layerRename"], () => Run("img.layerRename")).Labeled("cmd.img.layerRename", Tip(Loc.Current["cmd.img.layerRename"], "img.layerRename")),
            },
        };
        _layersPanel.Children.Add(_layerRows);
        _layersPanel.Children.Add(buttons);
        _layersPanel.Children.Add(new WrapPanel { ItemSpacing = 6, LineSpacing = 4, Children = { Label("image.opacity"), _layerOpacity } });
        _layersPanel.Children.Add(new WrapPanel { ItemSpacing = 6, LineSpacing = 4, Children = { Label("image.blendMode"), _layerBlend } });
        return Section("image.layers", _layersPanel);
    }

    private bool RunLayer(string id)
    {
        if (!_session.SupportsLayers || !id.StartsWith("img.layer", StringComparison.Ordinal)) return false;
        var set = Canvas.Layers;
        var at = ActiveLayer;
        // Deleting a locked layer, or merging into or out of one, would change what the lock protects.
        if (id is "img.layerDelete" or "img.layerMergeDown" && (set.Layers[at].Locked || id == "img.layerMergeDown" && at > 0 && set.Layers[at - 1].Locked))
        {
            _status.Text = Loc.Current["image.locked"];
            return true;
        }

        switch (id)
        {
            case "img.layerAdd": LayerOp("add", Loc.Current.Format("image.layerName", ("n", set.Count + 1)), at, at + 1); break;
            case "img.layerDuplicate": LayerOp("dup", Loc.Current.Format("image.layerCopy", ("name", set.Layers[at].Name)), at, at + 1); break;
            case "img.layerDelete" when set.Count > 1: LayerOp("delete", null, at, Math.Max(0, at - 1)); break;
            case "img.layerUp" when at < set.Count - 1: LayerOp("move", "1", at, at + 1); break;
            case "img.layerDown" when at > 0: LayerOp("move", "-1", at, at - 1); break;
            case "img.layerMergeDown" when at > 0: LayerOp("merge", null, at, at - 1); break;
            case "img.layerVisible": LayerOp("visible", set.Layers[at].Visible ? "0" : "1", at); break;
            case "img.layerLock": LayerOp("lock", set.Layers[at].Locked ? "0" : "1", at); break;
            case "img.layerRename": BeginRename(at); break;
        }

        return true;
    }

    /// <summary>Commits one layer operation on a layer as an undoable edit and then shows the layer <paramref name="show"/> (the same one by default).</summary>
    private void LayerOp(string verb, string? extra, int layer, int? show = null)
    {
        if (RefuseStale() || !_session.CommitLayers(Frame, LayerSet.Op(verb, layer, extra))) return;
        ActiveLayer = show ?? layer;
        Reload();
    }

    private void SelectLayer(int layer)
    {
        if (layer == ActiveLayer) return;
        ActiveLayer = layer;
        Reload();
    }

    /// <summary>Puts a text box over the layer's name; Enter or leaving it renames the layer as one undoable edit, Escape keeps the old name.</summary>
    public void BeginRename(int layer)
    {
        if (!_session.SupportsLayers || RenamingLayer >= 0 || layer < 0 || layer >= Canvas.Layers.Count || RowOf(layer) is not { } row) return;
        RenamingLayer = layer;
        var old = Canvas.Layers.Layers[layer].Name;
        var box = new TextBox { Name = "LayerRenameBox", Text = old, Margin = new Thickness(4, 0), VerticalContentAlignment = VerticalAlignment.Center };
        box.Labeled("cmd.img.layerRename");
        var dock = DockOf(row);
        var name = dock.Children[^1];
        name.IsVisible = false;
        dock.Children.Add(box);
        var done = false;
        void Finish(bool keep)
        {
            if (done) return;
            done = true;
            RenamingLayer = -1;
            _endRename = null;
            dock.Children.Remove(box);
            name.IsVisible = true;
            var text = box.Text?.Trim() ?? "";
            if (keep && text.Length > 0 && text != old) LayerOp("rename", text, layer, ActiveLayer);
            else Canvas.Focus();
        }

        _endRename = () => Finish(true);

        box.KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Escape)) return;
            Finish(e.Key == Key.Enter);
            e.Handled = true;
        };
        box.LostFocus += (_, _) => Finish(true);
        box.AttachedToVisualTree += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
    }

    /// <summary>Ends a rename in progress as if Enter was pressed (tests and the window use it).</summary>
    public void CommitRename(string text)
    {
        if (RowOf(RenamingLayer) is not { Child: DockPanel dock } || dock.Children[^1] is not TextBox box) return;
        box.Text = text;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyDownEvent, Key = Key.Enter });
    }

    private static DockPanel DockOf(Border row) => row.Child as DockPanel ?? throw new InvalidOperationException("A layer row holds a dock panel.");

    private Border? RowOf(int layer) => _layerRows.Children.OfType<Border>().FirstOrDefault(r => r.Tag is int i && i == layer);

    // The rows stay while the number of layers stays: hiding, locking, renaming or selecting a layer only updates them, so the panel does not flicker or lose its scroll.
    private void RefreshLayers()
    {
        _layersPanel.IsVisible = _session.SupportsLayers;
        if (!_session.SupportsLayers) return;
        _refreshing = true;
        try
        {
            var set = Canvas.Layers;
            if (_layerRows.Children.Count != set.Count)
            {
                _layerRows.Children.Clear();
                for (var i = set.Count - 1; i >= 0; i--) _layerRows.Children.Add(LayerRow(i));
            }

            foreach (var row in _layerRows.Children.OfType<Border>())
                if (row.Tag is int i) UpdateRow(row, set.Layers[i], i);
            _layerOpacity.Value = set.Layers[ActiveLayer].Opacity;
            _layerBlend.SelectedIndex = (int)set.Layers[ActiveLayer].Blend;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private Border LayerRow(int index)
    {
        var eye = LayerToggle("LayerEye_" + index, "rooms.show", () => LayerOp("visible", Canvas.Layers.Layers[index].Visible ? "0" : "1", index, ActiveLayer));
        var padlock = LayerToggle("LayerLock_" + index, "rooms.lock", () => LayerOp("lock", Canvas.Layers.Layers[index].Locked ? "0" : "1", index, ActiveLayer));
        var name = new Button { Name = "LayerSelect_" + index, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(4, 0) };
        name.Click += (_, _) => SelectLayer(index);
        name.DoubleTapped += (_, e) =>
        {
            BeginRename(index);
            e.Handled = true;
        };
        DockPanel.SetDock(eye, Avalonia.Controls.Dock.Left);
        DockPanel.SetDock(padlock, Avalonia.Controls.Dock.Right);
        return new Border { Name = "LayerRow_" + index, Tag = index, Child = new DockPanel { Children = { eye, padlock, name } } };
    }

    private void UpdateRow(Border row, PaintLayer layer, int index)
    {
        var dock = DockOf(row);
        var (eye, padlock, name) = ((ToggleButton)dock.Children[0], (ToggleButton)dock.Children[1], (Button)dock.Children[2]);
        ShowToggle(eye, layer.Visible, EyeIcon, EyeOffIcon);
        ShowToggle(padlock, layer.Locked, LockIcon);
        if (!Equals(name.Content, layer.Name)) name.Content = layer.Name;
        // The current layer carries the hover fill all the time, like a row under the pointer, in place of an outline (Styles.axaml, Button.layerCurrent).
        name.Classes.Set("layerCurrent", index == ActiveLayer);
    }

    private static ToggleButton LayerToggle(string name, string labelKey, Action toggled)
    {
        var button = new ToggleButton { Name = name, Width = 30, Padding = new Thickness(0), Content = new PathIcon { Width = 14, Height = 14 } }.Labeled(labelKey);
        button.Click += (_, _) => toggled();
        return button;
    }

    private static void ShowToggle(ToggleButton button, bool on, string icon, string? offIcon = null)
    {
        button.IsChecked = on;
        var glyph = button.Content as PathIcon ?? throw new InvalidOperationException("A layer toggle holds a path icon.");
        var data = on ? icon : offIcon ?? icon;
        if (glyph.Tag as string != data)
        {
            glyph.Data = Geometry.Parse(data);
            glyph.Tag = data;
        }

        button.Classes.Set("dim", !on && offIcon is null); // the style dims the glyph (3:1 or more); a disabled one keeps the theme colour
    }
}
