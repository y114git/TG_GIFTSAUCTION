using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// What a number in the code can stand for, at the top of the context menu: each resource with that index (a click goes to it) and the colour of a colour value.
public sealed partial class CodeEditorTab
{
    internal List<Control> NumberItems(Workspace workspace, int offset)
    {
        var items = new List<Control>();
        if (NumberAssets.LiteralAt(Editor.Text, offset) is not { } literal) return items;
        if (literal.Value <= int.MaxValue)
            foreach (var (type, name) in NumberAssets.Find(workspace.Data, (int)literal.Value))
            {
                var item = new MenuItem { Header = Loc.Current["explorer.type." + type] + ": " + name };
                item.Click += (_, _) => GoToRequested?.Invoke(ResourceTypes.KeyOf(type, name));
                items.Add(item);
            }

        // A decimal number is a colour only when it is a named one (16777215 is c_white); six hex digits are always a colour.
        var colour = literal.Value <= 0xFFFFFF ? GmlColors.NameOf(literal.Value) : null;
        if (literal.Value <= 0xFFFFFF && (colour is not null || literal is { Hex: true, Digits: 6 }))
        {
            var (r, g, b) = GmlColors.FromBgr((int)literal.Value);
            var swatch = new Border { Width = 12, Height = 12, Margin = new Avalonia.Thickness(0, 0, 6, 0), Background = new SolidColorBrush(Color.FromRgb(r, g, b)), VerticalAlignment = VerticalAlignment.Center };
            var text = Loc.Current.Format("code.menu.colour", ("name", colour ?? GmlColors.Describe((r, g, b))));
            items.Add(new MenuItem { Header = new StackPanel { Orientation = Orientation.Horizontal, Children = { swatch, new TextBlock { Text = text } } }, IsEnabled = false });
        }

        if (items.Count > 0) items.Add(new Separator());
        return items;
    }
}
