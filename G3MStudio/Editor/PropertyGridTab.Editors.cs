using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

public sealed partial class PropertyGridTab
{
    // A swatch that opens three sliders (red, green, blue) and the number itself in a box beside it (UTMT-CTRL-05). The picked color is committed when the flyout closes, so a drag is one undo step; the top byte of the stored value is kept.
    private static readonly string[] ColourNames = ["red", "green", "blue"];

    private WrapPanel ColorEditor(PropertyRow row, uint color)
    {
        var (r, g, b) = PropertyModel.Rgb(color);
        var sliders = new[] { r, g, b }.Select((v, n) => new Slider { Minimum = 0, Maximum = 255, Value = v, Width = 220, Name = $"Color{"RGB"[n]}_{row.Path}" }.Labeled("organize.colour." + ColourNames[n])).ToArray();
        var swatch = new Border { Width = 40, Height = 22, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), BorderBrush = (Avalonia.Media.IBrush?)Application.Current?.FindResource("Border.Subtle") };
        Avalonia.Media.IBrush Brush() => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb((byte)sliders[0].Value, (byte)sliders[1].Value, (byte)sliders[2].Value));
        swatch.Background = Brush();
        foreach (var slider in sliders) slider.ValueChanged += (_, _) => swatch.Background = Brush();
        var pick = new Button { Name = "Picker_" + row.Path, Content = swatch, Padding = new Thickness(3), IsEnabled = !row.ReadOnly }.Labeled("cmd.img.tool.picker");
        var flyout = new Flyout { Content = new StackPanel { Spacing = 4, Children = { sliders[0], sliders[1], sliders[2] } } };
        flyout.Closed += (_, _) =>
        {
            var next = PropertyModel.WithRgb(color, (byte)sliders[0].Value, (byte)sliders[1].Value, (byte)sliders[2].Value);
            if (next != color) Commit(row.Path, next.ToString(System.Globalization.CultureInfo.InvariantCulture));
        };
        pick.Flyout = flyout;
        var box = new TextBox { Name = "Prop_" + row.Path, Text = row.Value, IsReadOnly = row.ReadOnly, Width = 120 };
        void Done()
        {
            if (!row.ReadOnly && box.Text != row.Value && !Commit(row.Path, box.Text ?? "")) box.Text = row.Value;
        }

        box.LostFocus += (_, _) => Done();
        box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Done(); };
        return new WrapPanel { ItemSpacing = 8, Children = { pick, box } };
    }

    // A reference to another entry: its name is a link that goes there (any entry type opens in a tab), a button picks another target from a list with previews,
    // and a cross clears an optional one. A settable one also takes an Explorer item dropped on it (UTMT-CTRL-01). A link the library cannot set says so in a tooltip.
    private WrapPanel ReferenceEditor(PropertyRow row)
    {
        // The link shows the nickname of the entry it points at; the tip keeps the real name.
        var nickname = row.Target is { } to ? _workspace.Nicknames.Of(to) : null;
        var panel = new WrapPanel { ItemSpacing = 8, Background = Avalonia.Media.Brushes.Transparent };
        var locked = _workspace.Locks.Covers(_name);
        if (row.Target is { } target)
        {
            if (ResourceTypes.SplitKey(target) is { } split && ResourceThumbnails.CanHavePicture(split.Type)) panel.Children.Add(LinkThumb.Make(target, "Thumb_" + row.Path));
            var link = new Button { Name = "Open_" + row.Path, Content = nickname ?? row.Value, Classes = { "link" }, HorizontalAlignment = HorizontalAlignment.Left };
            HoverCards.Attach(link, target, row.Value);
            link.Click += (_, _) => Raise(LinkAction.Open, target);
            // A button ignores the middle button, which opens the entry in a tab of its own like in the Explorer.
            link.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Middle) Raise(LinkAction.Open, target);
            };
            var find = new MenuItem { Header = Loc.Current["cmd.edit.findUsages"] };
            find.Click += (_, _) => Raise(LinkAction.FindUsages, target);
            link.ContextMenu = new ContextMenu { Items = { find } };
            if (ResourceTypes.SplitKey(target) is var (type, _) && type is "sprites" or "tileSets" or "fonts" or "texturePageItems")
            {
                var edit = new MenuItem { Header = Loc.Current["textureItem.edit"] };
                edit.Click += (_, _) => Raise(LinkAction.EditImage, target);
                link.ContextMenu.Items.Add(edit);
            }

            if (!row.Reference) ToolTip.SetTip(link, Loc.Current["link.readOnly"]);
            panel.Children.Add(link);
        }
        else
        {
            // Nothing to go to: plain text, not a button that does nothing.
            panel.Children.Add(new TextBlock { Name = "Value_" + row.Path, Text = row.Value.Length > 0 ? row.Value : Loc.Current["explorer.noValue"], Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center });
        }

        if (!row.Reference) return panel;
        if (row.TargetType is { } kind)
        {
            var set = new Button { Name = "Set_" + row.Path, Content = Loc.Current[row.Target is null ? "link.set" : "link.change"] };
            set.Click += (_, _) => ResourcePicker.Show(set, _workspace, kind, row.Target, key => Commit(row.Path, key));
            panel.Children.Add(set.Gate(locked ? Loc.Current["link.locked"] : null));
        }

        // Only a link that has a value shows the cross; an empty one has nothing to clear.
        var clear = new Button { Name = "Clear_" + row.Path, Content = G3MStudio.Theme.GlyphIcons.Close(), Classes = { "icon" }, IsVisible = row.Target is not null || row.Value.Length > 0 }.Labeled("link.clear");
        clear.Click += (_, _) => Commit(row.Path, "");
        panel.Children.Add(clear.Gate(locked ? Loc.Current["link.locked"] : null));
        Avalonia.Input.DragDrop.SetAllowDrop(panel, true);
        panel.AddHandler(Avalonia.Input.DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.DataTransfer.TryGetValue(ResourceDrag.Keys) is null ? Avalonia.Input.DragDropEffects.None : Avalonia.Input.DragDropEffects.Move);
        panel.AddHandler(Avalonia.Input.DragDrop.DropEvent, (_, e) =>
        {
            if (e.DataTransfer.TryGetValue(ResourceDrag.Keys)?.Split('\n', StringSplitOptions.RemoveEmptyEntries) is not [var key, ..]) return;
            Commit(row.Path, key);
            e.Handled = true;
        });
        return panel;
    }

    /// <summary>A link was used: the events of this grid when someone listens, else the window through the routed event (grids nested in other editors have no listener of their own).</summary>
    private void Raise(LinkAction action, string key)
    {
        var handler = action switch { LinkAction.Open => OpenRequested, LinkAction.FindUsages => FindUsagesRequested, _ => EditImageRequested };
        if (handler is not null) handler(key);
        else RaiseEvent(new LinkRequestedEventArgs(LinkContext.RequestedEvent, action, key) { Source = this });
    }
}
