using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>A small dialog that asks for a few numbers (an angle, a size, an amount) and returns them, or null on Cancel.</summary>
public sealed class ParameterWindow : Window
{
    private readonly List<NumericUpDown> _fields = [];

    private ParameterWindow(string title, (string Label, double Value, double Min, double Max)[] fields)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var body = new StackPanel { Spacing = 6, Margin = new Thickness(12) };
        foreach (var (label, value, min, max) in fields)
        {
            var box = new NumericUpDown { Minimum = (decimal)min, Maximum = (decimal)max, Value = (decimal)value, Increment = 1, FormatString = "0.##", Width = 120 };
            _fields.Add(box);
            body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = label, Width = 110, VerticalAlignment = VerticalAlignment.Center }, box } });
        }

        var ok = new Button { Name = "ParameterOk", IsDefault = true, Content = Loc.Current["image.ok"], Classes = { "primary" } };
        var cancel = new Button { Name = "ParameterCancel", IsCancel = true, Content = Loc.Current["explorer.cancel"] };
        ok.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, ok } });
        Content = body;
    }

    public static async Task<double[]?> AskAsync(Window owner, string title, (string Label, double Value, double Min, double Max)[] fields)
    {
        var window = new ParameterWindow(title, fields);
        return await window.ShowDialog<bool>(owner) ? [.. window._fields.Select(f => (double)(f.Value ?? 0))] : null;
    }
}
