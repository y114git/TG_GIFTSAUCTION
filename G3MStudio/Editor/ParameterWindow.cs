using Avalonia.Controls;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>A small dialog that asks for a few numbers (an angle, a size, an amount) and returns them, or null on Cancel.</summary>
public sealed class ParameterWindow : Window
{
    private readonly List<NumericUpDown> _fields = [];

    private ParameterWindow(string title, (string Label, double Value, double Min, double Max)[] fields)
    {
        Title = title;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var rows = new List<(string, Control)>();
        foreach (var (label, value, min, max) in fields)
        {
            var box = new NumericUpDown { Minimum = (decimal)min, Maximum = (decimal)max, Value = (decimal)value, Increment = 1, FormatString = "0.##", Width = 120 };
            _fields.Add(box);
            rows.Add((label, box));
        }

        var ok = DialogFrame.Action(this, Loc.Current["image.ok"], "ParameterOk", () => Close(true), DialogRole.Primary);
        DialogFrame.Show(this, DialogFrame.Form([.. rows]), [ok, DialogFrame.Cancel(this, () => Close(false), "ParameterCancel")], scroll: DialogScroll.Vertical, dirty: DialogFrame.Snapshot([.. _fields]));
    }

    public static async Task<double[]?> AskAsync(Window owner, string title, (string Label, double Value, double Min, double Max)[] fields)
    {
        var window = new ParameterWindow(title, fields);
        return await window.ShowDialog<bool>(owner) ? [.. window._fields.Select(f => (double)(f.Value ?? 0))] : null;
    }
}
