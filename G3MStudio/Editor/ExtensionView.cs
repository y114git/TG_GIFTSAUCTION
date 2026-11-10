using Avalonia;
using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// An extension as an editable outline above its property list (UTMT-AST-04 to 06): each file, its functions with their arguments, and the options open into a property list;
/// every list can add, remove and move entries. A remove that code depends on goes through the edit delegate, which asks the owner and builds the edit again with its force flag.
/// </summary>
public sealed class ExtensionView : StackPanel
{
    private readonly GameMakerExtension _extension;
    private readonly Workspace _workspace;
    private readonly Func<Func<bool, EditCommand>, string, Task<bool>>? _edit;
    private readonly List<(TextBlock Label, Func<string> Text)> _labels = [];
    private string _shape = "";
    private string _fileKind = nameof(GameMakerExtensionKind.Dll); // the kind a new file starts as

    public ExtensionView(GameMakerExtension extension, Workspace workspace, Func<Func<bool, EditCommand>, string, Task<bool>>? edit = null)
    {
        (_extension, _workspace, _edit) = (extension, workspace, edit);
        Name = "ExtensionView";
        Spacing = 4;
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Changed;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Changed;
        Rebuild();
    }

    private string ExtensionName => _extension.Name?.Content ?? "";

    /// <summary>Counts of every list: a change of count rebuilds the outline, a plain value edit only refreshes the labels (so the property list being typed in stays).</summary>
    private string Shape() => string.Join('/', _extension.Files.Select(f => $"{f.Functions.Count}:{string.Join(',', f.Functions.Select(x => x.Arguments.Count))}").Prepend(_extension.Files.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("o" + _extension.Options.Count));

    private void Changed()
    {
        if (Shape() != _shape) Rebuild();
        else foreach (var (label, text) in _labels) label.Text = text();
    }

    private TextBlock Label(Func<string> text)
    {
        var block = new TextBlock { Text = text(), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        _labels.Add((block, text));
        return block;
    }

    private void Rebuild()
    {
        _shape = Shape();
        _labels.Clear();
        Children.Clear();
        var summary = new TextBlock { Classes = { "muted" } };
        _labels.Add((summary, () => Loc.Current.Format("editors.extensionSummary", ("name", ExtensionName), ("version", _extension.Version?.Content ?? ""), ("files", _extension.Files.Count), ("options", _extension.Options.Count))));
        summary.Text = _labels[^1].Text();
        Children.Add(summary);
        Children.Add(Heading("editors.extFiles"));
        if (_extension.Files.Count == 0) Children.Add(new TextBlock { Name = "ExtFilesEmpty", Text = Loc.Current["editors.extNoFiles"], Classes = { "muted" } });
        for (var i = 0; i < _extension.Files.Count; i++) Children.Add(FileBlock(i));
        var kinds = Enum.GetNames<GameMakerExtensionKind>();
        var kind = new ComboBox { Name = "ExtFileKind", ItemsSource = kinds.Select(KindText).ToList(), SelectedIndex = Math.Max(0, Array.IndexOf(kinds, _fileKind)) };
        kind.SelectionChanged += (_, _) => _fileKind = kind.SelectedIndex >= 0 ? kinds[kind.SelectedIndex] : _fileKind;
        Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { AddButton("ExtFileAdd", "editors.extAddFile", "Files"), kind } });
        Children.Add(Heading("editors.extOptions"));
        if (_extension.Options.Count == 0) Children.Add(new TextBlock { Name = "ExtOptionsEmpty", Text = Loc.Current["editors.extNoOptions"], Classes = { "muted" } });
        for (var i = 0; i < _extension.Options.Count; i++)
        {
            var at = i;
            Children.Add(Block(Label(() => $"{_extension.Options.ElementAtOrDefault(at)?.Name?.Content} = {_extension.Options.ElementAtOrDefault(at)?.Value?.Content}"), new ListRow("Options", i, _extension.Options.Count, "ExtOption"), $"Options[{i}]"));
        }

        Children.Add(AddButton("ExtOptionAdd", "editors.extAddOption", "Options"));
    }

    private static TextBlock Heading(string key) => new TextBlock { Text = Loc.Current[key], Margin = new Thickness(0, 8, 0, 0), Classes = { "accent" } }.TextSize("FontSize.Body");

    /// <summary>The kind as a person writes it: DLL in capitals.</summary>
    private static string KindText(string kind) => kind == "Dll" ? "DLL" : kind;

    private Expander FileBlock(int i)
    {
        var path = $"Files[{i}]";
        var file = _extension.Files[i];
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new PropertyGridTab(_workspace, "extensions", ExtensionName, null, path));
        for (var j = 0; j < file.Functions.Count; j++) body.Children.Add(FunctionBlock(path, j, file.Functions.Count));
        body.Children.Add(AddButton("ExtFunctionAdd_" + i, "editors.extAddFunction", path + ".Functions"));
        return Block(Label(() => $"{_extension.Files.ElementAtOrDefault(i)?.Filename?.Content} ({KindText(_extension.Files.ElementAtOrDefault(i)?.Kind.ToString() ?? "")})"), new ListRow("Files", i, _extension.Files.Count, "ExtFile"), null, body);
    }

    private Expander FunctionBlock(string filePath, int j, int count)
    {
        var path = $"{filePath}.Functions[{j}]";
        var function = (PropertyModel.FindRoot(_workspace.Data, "extensions", ExtensionName) is { } root ? PropertyModel.ReadRaw(root, path) : null) as GameMakerExtensionFunction;
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new PropertyGridTab(_workspace, "extensions", ExtensionName, null, path));
        var args = path + ".Arguments";
        for (var k = 0; function is not null && k < function.Arguments.Count; k++)
        {
            var at = k;
            body.Children.Add(Buttons(Label(() => $"[{at}] {(PropertyModel.FindRoot(_workspace.Data, "extensions", ExtensionName) is { } r && PropertyModel.ReadRaw(r, args) is System.Collections.IList l && at < l.Count && l[at] is GameMakerExtensionFunctionArg arg ? arg.Type : null)}"), args, k, function.Arguments.Count, "ExtArgument"));
        }

        body.Children.Add(AddButton("ExtArgumentAdd_" + j, "editors.extAddArgument", args));
        return Block(Label(() => function is null ? "" : $"{function.Name?.Content}({string.Join(", ", function.Arguments.Select(a => a.Type))}) : {function.RetType}"), new ListRow($"{filePath}.Functions", j, count, "ExtFunction"), null, body);
    }

    /// <summary>One entry of an extension list: the list path, its place, the list size and the prefix of its control names.</summary>
    private readonly record struct ListRow(string List, int Index, int Count, string Name);

    /// <summary>A header with move and remove buttons; the body is the given content, or the property list of <paramref name="subPath"/>.</summary>
    private Expander Block(TextBlock label, ListRow row, string? subPath, Control? body = null)
    {
        var expander = new Expander { Header = Buttons(label, row.List, row.Index, row.Count, row.Name), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 2) };
        if (body is not null) expander.Content = body;
        else if (subPath is not null) expander.Expanded += (_, _) => expander.Content ??= new PropertyGridTab(_workspace, "extensions", ExtensionName, null, subPath);
        return expander;
    }

    private DockPanel Buttons(TextBlock label, string list, int index, int count, string name)
    {
        var up = new Button { Name = $"{name}Up_{index}", Content = Loc.Current["editors.extUp"], IsEnabled = index > 0 };
        var down = new Button { Name = $"{name}Down_{index}", Content = Loc.Current["editors.extDown"], IsEnabled = index < count - 1 };
        var remove = new Button { Name = $"{name}Remove_{index}", Content = Loc.Current["timelines.remove"] };
        // The row label is part of each name, so a screen reader can tell the rows apart; the label text changes with the data.
        void Name()
        {
            Avalonia.Automation.AutomationProperties.SetName(up, $"{Loc.Current["editors.extUp"]}: {label.Text}");
            Avalonia.Automation.AutomationProperties.SetName(down, $"{Loc.Current["editors.extDown"]}: {label.Text}");
            Avalonia.Automation.AutomationProperties.SetName(remove, $"{Loc.Current["timelines.remove"]}: {label.Text}");
        }

        label.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) Name(); };
        Name();
        up.Click += (_, _) => Move(list, index, index - 1);
        down.Click += (_, _) => Move(list, index, index + 1);
        remove.Click += async (_, _) => await Remove(list, index);
        var tools = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Margin = new Thickness(12, 0, 24, 0), Children = { up, down, remove } };
        DockPanel.SetDock(tools, Avalonia.Controls.Dock.Right);
        return new DockPanel { LastChildFill = true, Children = { tools, label } };
    }

    private Button AddButton(string name, string key, string list)
    {
        var button = new Button { Name = name, Content = Loc.Current[key] };
        button.Click += (_, _) => Add(list);
        return button;
    }

    /// <summary>Adds a new entry at the end of the list; false when the edit is refused (more than 17 arguments).</summary>
    public bool Add(string list) => Run(new ExtensionListCommand(ExtensionName, list, ExtensionListCommand.Add, -1) { FileKind = list == "Files" ? _fileKind : "" });

    public bool Move(string list, int from, int to) => Run(new ExtensionListCommand(ExtensionName, list, ExtensionListCommand.Move, from, to));

    /// <summary>Removes an entry; code that calls a removed function asks the owner first.</summary>
    public async Task<bool> Remove(string list, int index)
    {
        if (_edit is { } edit) return await edit(force => new ExtensionListCommand(ExtensionName, list, ExtensionListCommand.Remove, index, force: force), ExtensionName);
        return Run(new ExtensionListCommand(ExtensionName, list, ExtensionListCommand.Remove, index));
    }

    private bool Run(ExtensionListCommand command)
    {
        try
        {
            _workspace.Edit(command);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
