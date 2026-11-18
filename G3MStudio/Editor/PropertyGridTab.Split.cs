using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class PropertyGridTab
{
    /// <summary>The header (a room, sprite, font, sound player, path or object tab) and the properties share the tab through a splitter at any window size.</summary>
    private static ScrollViewer Split(Control header, string type, DockPanel body, PaneMemory? memory)
    {
        var tall = header is RoomPreview or SpriteEditorView || type is "fonts" or "uiLayers"; // a font's glyph list and a UI layer's tree, fields and preview need most of the tab, with a splitter instead of a cap
        var top = new RowDefinition(tall ? new GridLength(header is RoomPreview ? 13 : header is SpriteEditorView ? 5 : 3, GridUnitType.Star) : GridLength.Auto) { MinHeight = tall ? (header is SpriteEditorView ? 320 : header is RoomPreview ? 150 : 200) : 48 };
        var bottom = new RowDefinition(header is RoomPreview ? 7 : 2, GridUnitType.Star) { MinHeight = header is RoomPreview ? 130 : 120 };
        var split = new Grid { RowDefinitions = { top, new RowDefinition(GridLength.Auto), bottom } };
        var splitter = new GridSplitter { Name = "PropertySplitter", Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows };
        ToolTip.SetTip(splitter, Loc.Current["fonts.dragResize"]);
        // A header that is not tall takes the room its content needs, at most 60% of the tab, and scrolls beyond that, until the user drags the splitter (a sound player is short: no cap).
        // The cap is also set on the scroller itself: a row limit alone leaves the scroller measured at its full height with nothing to scroll.
        // It never scrolls sideways (the content wraps or ends in an ellipsis instead of being cut), and the right padding keeps the overlay scroll bar off the content.
        Control? stacked = header is RoomPreview or SpriteEditorView ? null : new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 14, 0), Content = header };
        var page = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = split };
        // The property list keeps the height the user gave it and the header takes what is left; the height is kept per editor type.
        var (key, sized) = ("properties." + type, false);
        // Below the room the rows need, the whole tab scrolls instead of cutting the lower part (the header and the list keep their minimum height).
        double Needed() => tall || sized ? top.MinHeight + 5 + bottom.MinHeight : header is SoundPlayerView ? header.DesiredSize.Height + 5 + bottom.MinHeight : bottom.MinHeight / 0.4;
        void Fit() => split.Height = Math.Max(page.Bounds.Height, Needed());
        void Give(double height)
        {
            sized = true;
            top.MaxHeight = double.PositiveInfinity;
            stacked?.MaxHeight = double.PositiveInfinity;
            (bottom.Height, top.Height) = (new GridLength(height), GridLength.Star);
            Fit();
        }

        if (memory?.Get(key) is { } saved) Give(saved);
        splitter.DragCompleted += (_, _) =>
        {
            if (bottom.ActualHeight < 1) return;
            var height = Math.Round(bottom.ActualHeight);
            Give(height);
            memory?.Set(key, height);
        };
        split.SizeChanged += (_, e) =>
        {
            if (sized) bottom.MaxHeight = Math.Max(bottom.MinHeight, e.NewSize.Height - top.MinHeight - 5);
            else if (!tall && header is not SoundPlayerView) top.MaxHeight = (stacked ?? header).MaxHeight = e.NewSize.Height * 0.6;
        };
        // The room's toolbar wraps onto more rows in a narrow tab: its row keeps the room the toolbar and a usable canvas need, and the tab scrolls beyond that.
        if (header is RoomPreview room)
            room.LayoutUpdated += (_, _) =>
            {
                var needed = Math.Max(150, Math.Ceiling(room.MinimumHeight));
                if (Math.Abs(top.MinHeight - needed) < 0.5) return;
                top.MinHeight = needed;
                Fit();
            };
        Grid.SetRow(splitter, 1);
        Grid.SetRow(body, 2);
        split.Children.Add(stacked ?? header);
        split.Children.Add(splitter);
        split.Children.Add(body);
        page.SizeChanged += (_, _) => Fit();
        if (header is SoundPlayerView) header.SizeChanged += (_, _) => Fit();
        return page;
    }
}
