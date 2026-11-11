using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

public sealed partial class PropertyGridTab
{
    // A swatch that opens three sliders (red, green, blue) and the number itself in a box beside it (UTMT-CTRL-05). The picked color is committed when the flyout closes, so a drag is one undo step; the top byte of the stored value is kept.
    private static readonly string[] ColourNames = ["red", "green", "blue"];

    private StackPanel ColorEditor(PropertyRow row, uint color)
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
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { pick, box } };
    }

    // A reference to another entry: its name is a link that opens it. A settable one takes an Explorer item dropped on it and has a button that clears it (UTMT-CTRL-01).
    private StackPanel ReferenceEditor(PropertyRow row)
    {
        var link = new Button { Name = "Open_" + row.Path, Content = row.Value.Length > 0 ? row.Value : Loc.Current["explorer.noValue"], Classes = { "link" }, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = row.Target is not null };
        if (row.Target is { } target)
        {
            link.Click += (_, _) => OpenRequested?.Invoke(target);
            // A button ignores the middle button, which opens the entry in a tab of its own like in the Explorer.
            link.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Middle) OpenRequested?.Invoke(target);
            };
            var find = new MenuItem { Header = Loc.Current["cmd.edit.findUsages"] };
            find.Click += (_, _) => FindUsagesRequested?.Invoke(target);
            link.ContextMenu = new ContextMenu { Items = { find } };
            if (ResourceTypes.SplitKey(target) is var (type, _) && type is "sprites" or "tileSets" or "fonts" or "texturePageItems")
            {
                var edit = new MenuItem { Header = Loc.Current["textureItem.edit"] };
                edit.Click += (_, _) => EditImageRequested?.Invoke(target);
                link.ContextMenu.Items.Add(edit);
            }
        }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Background = Avalonia.Media.Brushes.Transparent, Children = { link } };
        if (!row.Reference) return panel;
        var clear = new Button { Name = "Clear_" + row.Path, Content = "×", Classes = { "icon" }, IsEnabled = row.Target is not null };
        Avalonia.Automation.AutomationProperties.SetName(clear, Loc.Current["timelines.remove"]);
        clear.Click += (_, _) => Commit(row.Path, "");
        panel.Children.Add(clear);
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
}
