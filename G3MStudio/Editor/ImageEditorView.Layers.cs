using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
            Children =
            {
                Small("LayerAdd", "+", () => Run("img.layerAdd")), Small("LayerDuplicate", Loc.Current["image.layerDuplicate"], () => Run("img.layerDuplicate")),
                Small("LayerDelete", Loc.Current["image.layerDelete"], () => Run("img.layerDelete")), Small("LayerUp", "^", () => Run("img.layerUp")),
                Small("LayerDown", "v", () => Run("img.layerDown")), Small("LayerMerge", Loc.Current["image.layerMergeDown"], () => Run("img.layerMergeDown")),
            },
        };
        _layersPanel.Children.Add(_layerRows);
        _layersPanel.Children.Add(buttons);
        _layersPanel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Label("image.opacity"), _layerOpacity } });
        _layersPanel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Label("image.blendMode"), _layerBlend } });
        return Section("image.layers", _layersPanel);
    }

    private bool RunLayer(string id)
    {
        if (!_session.SupportsLayers || !id.StartsWith("img.layer", StringComparison.Ordinal)) return false;
        var set = Canvas.Layers;
        var at = ActiveLayer;
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
        }

        return true;
    }

    /// <summary>Commits one layer operation on a layer as an undoable edit and then shows the layer <paramref name="show"/> (the same one by default).</summary>
    private void LayerOp(string verb, string? extra, int layer, int? show = null)
    {
        if (!_session.CommitLayers(Frame, LayerSet.Op(verb, layer, extra))) return;
        ActiveLayer = show ?? layer;
        Reload();
    }

    private void SelectLayer(int layer)
    {
        if (layer == ActiveLayer) return;
        ActiveLayer = layer;
        Reload();
    }

    private void RefreshLayers()
    {
        _layersPanel.IsVisible = _session.SupportsLayers;
        if (!_session.SupportsLayers) return;
        _refreshing = true;
        try
        {
            var set = Canvas.Layers;
            _layerRows.Children.Clear();
            for (var i = set.Count - 1; i >= 0; i--) _layerRows.Children.Add(LayerRow(set.Layers[i], i));
            _layerOpacity.Value = set.Layers[ActiveLayer].Opacity;
            _layerBlend.SelectedIndex = (int)set.Layers[ActiveLayer].Blend;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private Border LayerRow(PaintLayer layer, int index)
    {
        var eye = LayerToggle("LayerEye_" + index, EyeIcon, "rooms.show", layer.Visible, on => LayerOp("visible", on ? "1" : "0", index, ActiveLayer), EyeOffIcon);
        var padlock = LayerToggle("LayerLock_" + index, LockIcon, "rooms.lock", layer.Locked, on => LayerOp("lock", on ? "1" : "0", index, ActiveLayer));
        var name = new Button { Name = "LayerSelect_" + index, Content = layer.Name, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
        name.Click += (_, _) => SelectLayer(index);
        DockPanel.SetDock(eye, Avalonia.Controls.Dock.Left);
        DockPanel.SetDock(padlock, Avalonia.Controls.Dock.Right);
        return new Border { Name = "LayerRow_" + index, BorderThickness = new Thickness(index == ActiveLayer ? 2 : 0), BorderBrush = G3MStudio.Theme.CanvasTheme.Point, Child = new DockPanel { Children = { eye, padlock, name } } };
    }

    private static ToggleButton LayerToggle(string name, string icon, string labelKey, bool on, Action<bool> changed, string? offIcon = null)
    {
        var button = new ToggleButton { Name = name, IsChecked = on, Width = 30, Padding = new Thickness(0), Content = new PathIcon { Data = Geometry.Parse(on ? icon : offIcon ?? icon), Width = 14, Height = 14, Opacity = on || offIcon is not null ? 1 : 0.35 } }.Labeled(labelKey);
        button.Click += (_, _) => changed(button.IsChecked == true);
        return button;
    }
}
