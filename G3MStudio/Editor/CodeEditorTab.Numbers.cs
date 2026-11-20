using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// What a number in the code can stand for, at the top of the context menu: each resource with that index (a click goes to it) and the colour of a colour value.
public sealed partial class CodeEditorTab
{
    /// <summary>The numbers of a text that are assets, by the function they are passed to and the types the settings allow.</summary>
    private IReadOnlyList<AssetNumberEdit> AssetNumbersIn(string text)
    {
        var types = EditorOptions.AssetNumberTypes;
        using (_workspace.Lock.Read()) return AssetNumberReplacer.Find(text, new AssetNames(_workspace.Data), AssetNumbersApply.ParametersFor(_workspace), types.Contains);
    }

    /// <summary>Writes the names for the numbers of the selection (the whole entry when nothing is selected) as one undo step; returns how many were replaced.</summary>
    internal int ReplaceAssetNumbers()
    {
        if (Editor.IsReadOnly) return 0;
        var (start, end) = Editor.SelectionLength == 0 ? (0, Editor.Text.Length) : (Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength);
        var edits = AssetNumbersIn(Editor.Text).Where(e => e.Start >= start && e.Start + e.Length <= end).ToList();
        ApplyAssetNumbers(edits);
        return edits.Count;
    }

    // A plugin action returns the whole new text; it is written as one change (one undo step).
    private void RunPluginAction(Func<G3MStudio.Core.Plugins.CodeRequest, string?> run)
    {
        var text = Editor.Text;
        if (run(new G3MStudio.Core.Plugins.CodeRequest(Entry, text, Editor.SelectionStart, Editor.SelectionLength)) is not { } changed || changed == text) return;
        var document = Editor.Document;
        document.BeginUpdate();
        try
        {
            document.Replace(0, document.TextLength, changed);
        }
        finally
        {
            document.EndUpdate();
        }
    }

    private void ApplyAssetNumbers(IReadOnlyList<AssetNumberEdit> edits)
    {
        var document = Editor.Document;
        document.BeginUpdate();
        try
        {
            foreach (var edit in edits.OrderByDescending(e => e.Start)) document.Replace(edit.Start, edit.Length, edit.Name);
        }
        finally
        {
            document.EndUpdate();
        }
    }

    /// <summary>For the Auto setting: the text of a freshly decompiled entry with its asset numbers written as names. It is not an edit, so the entry stays unchanged until the user edits it.</summary>
    private CodeLoad WithAssetNames(CodeLoad load)
    {
        if (EditorOptions.AssetNumbers != G3MStudio.Core.Settings.AssetNumberMode.Auto || load.Mode != CodeMode.Decompiled) return load;
        var edits = AssetNumbersIn(load.Text);
        return edits.Count == 0 ? load : load with { Text = AssetNumberReplacer.Apply(load.Text, edits) };
    }

    internal List<Control> NumberItems(Workspace workspace, int offset)
    {
        var items = new List<Control>();
        if (NumberAssets.LiteralAt(Editor.Text, offset) is not { } literal) return items;
        // The function the number is an argument of decides which asset it is: that one is offered as a replacement first.
        if (EditorOptions.AssetNumbers != G3MStudio.Core.Settings.AssetNumberMode.Off && !Editor.IsReadOnly && !literal.Hex
            && AssetNumbersIn(Editor.Text).FirstOrDefault(e => offset >= e.Start && offset <= e.Start + e.Length) is { } edit)
        {
            var replace = new MenuItem { Header = Loc.Current.Format("code.menu.replaceWithAsset", ("name", edit.Name)) };
            replace.Click += (_, _) => ApplyAssetNumbers([edit]);
            items.Add(replace);
        }

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
