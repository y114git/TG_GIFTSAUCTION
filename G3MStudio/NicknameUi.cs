using Avalonia.Controls;
using Avalonia.Controls.Templates;
using G3MStudio.Core.Workspace;

namespace G3MStudio;

/// <summary>Pickers that list resources by name show the nickname instead; the selected value stays the real name.</summary>
public static class NicknameUi
{
    /// <summary>A template for a list of resource names of one type: each shows its nickname when it has one.</summary>
    public static IDataTemplate Template(Nicknames nicknames, string type) =>
        new FuncDataTemplate<string>((name, _) => Row(name is null ? "" : nicknames.Label(type, name), name is null ? "" : nicknames.Both(type, name)));

    /// <summary>A template for a list of names whose type is not known (the members of a group): a name with a nickname shows the nickname.</summary>
    public static IDataTemplate ShownTemplate(Nicknames nicknames) =>
        new FuncDataTemplate<string>((name, _) => Row(name is null ? "" : nicknames.Shown(name), name is null ? "" : nicknames.BothNamed(name)));

    // The row shows the nickname; the tip adds the real name behind it.
    private static TextBlock Row(string text, string both)
    {
        var row = new TextBlock { Text = text, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
        if (both != text) ToolTip.SetTip(row, both);
        return row;
    }

    /// <summary>Gives the combo box the nickname template and makes its list again (a nickname changed), keeping the selected name.</summary>
    public static void Relabel(ComboBox box, Nicknames nicknames, string type)
    {
        var selected = box.SelectedItem;
        box.ItemTemplate = Template(nicknames, type);
        if (box.ItemsSource is not IEnumerable<string> names) return;
        var list = names.ToList();
        box.ItemsSource = null;
        box.ItemsSource = list;
        box.SelectedItem = selected;
    }
}
