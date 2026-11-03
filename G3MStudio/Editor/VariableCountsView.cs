using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>The three counters in the header of the variables chunk, shown above every variable and editable (UTMT-AST-10). A wrong value breaks the game, so a box that does not hold a whole number is put back.</summary>
public sealed class VariableCountsView : StackPanel
{
    private readonly Workspace _workspace;
    private readonly TextBox _instance = Box("VarInstance", "explorer.type.varInstance"), _global = Box("VarGlobal", "explorer.type.varGlobal"), _locals = Box("VarLocals", "explorer.type.varLocals");

    public VariableCountsView(Workspace workspace)
    {
        _workspace = workspace;
        Name = "VariableCounts";
        Spacing = 4;
        Children.Add(new TextBlock { Text = Loc.Current["explorer.type.variableCounts"], Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { _instance, _global, _locals } });
        Refresh();
        foreach (var box in new[] { _instance, _global, _locals }) box.LostFocus += (_, _) => Commit();
        workspace.History.Changed += Refresh;
    }

    private static TextBox Box(string name, string label) => new() { Name = name, Width = 210, UseFloatingPlaceholder = true, PlaceholderText = Loc.Current[label] };

    private void Refresh()
    {
        (_instance.Text, _global.Text, _locals.Text) = (_workspace.Data.VarCount1.ToString(System.Globalization.CultureInfo.InvariantCulture), _workspace.Data.VarCount2.ToString(System.Globalization.CultureInfo.InvariantCulture), _workspace.Data.MaxLocalVarCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private void Commit()
    {
        var data = _workspace.Data;
        if (!uint.TryParse(_instance.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var instance)
            || !uint.TryParse(_global.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var global)
            || !uint.TryParse(_locals.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var locals))
        {
            Refresh();
            return;
        }

        if ((instance, global, locals) != (data.VarCount1, data.VarCount2, data.MaxLocalVarCount)) _workspace.Edit(new VariableCountsCommand(instance, global, locals));
    }
}
