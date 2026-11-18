using Avalonia.Controls;
using Avalonia.Controls.Templates;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Panels;

/// <summary>The Problems panel: compile errors of code entries. A double click asks the window to open the entry at the position.</summary>
public sealed class ProblemsPanel : UserControl
{
    private readonly ListBox _list = new() { Name = "ProblemsList" };
    private ProblemsModel? _model;
    private readonly TextBlock _empty = new TextBlock { Name = "ProblemsEmpty", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, Margin = new Avalonia.Thickness(12), IsHitTestVisible = false }.Tr(TextBlock.TextProperty, "empty.problems");

    /// <summary>The text a problem shows for its source (a code entry gets the nickname of its owner); null shows the source as it is.</summary>
    public Func<string, string>? SourceLabel { get; set; }

    /// <summary>The text a problem shows for its message (a quoted resource name gets its nickname); null shows the message as it is.</summary>
    public Func<string, string>? MessageLabel { get; set; }

    public ProblemsPanel()
    {
        _list.ItemTemplate = new FuncDataTemplate<Problem>((p, _) =>
        {
            var text = new TextBlock { Text = p is null ? "" : $"{SourceLabel?.Invoke(p.Source) ?? p.Source}({p.Line},{p.Column}) {p.Code}: {MessageLabel?.Invoke(p.Message) ?? p.Message}", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            // A thin bar in the colour of the severity, the same colours as the squiggles.
            var bar = new Border { Width = 3, Margin = new Avalonia.Thickness(0, 0, 6, 0), Background = Avalonia.Application.Current?.FindResource(p?.Severity switch { "Error" => "Danger", "Info" => "Info", _ => "Warning" }) as Avalonia.Media.IBrush };
            DockPanel.SetDock(bar, Avalonia.Controls.Dock.Left);
            var row = new DockPanel { Children = { bar, text } };
            // The source of a problem that is not a run error is a code entry: its card has the ID, and the full text for a row that is trimmed.
            if (p is not null) HoverCards.Attach(row, p.Code == "RUN" ? null : ResourceTypes.KeyOf("code", p.Source), text.Text ?? "");
            return row;
        });
        _list.DoubleTapped += (_, _) => { if (_list.SelectedItem is Problem problem) OpenRequested?.Invoke(problem); };
        _list.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && _list.SelectedItem is Problem problem) { e.Handled = true; OpenRequested?.Invoke(problem); } };
        Content = new Grid { Children = { _list, _empty } };
    }

    public IReadOnlyList<Problem> Rows => [.. _list.Items.OfType<Problem>()];

    public event Action<Problem>? OpenRequested;

    public void Open(Problem problem) => OpenRequested?.Invoke(problem);

    public void Bind(ProblemsModel? model)
    {
        _model?.Changed -= Refresh;
        _model = model;
        model?.Changed += Refresh;
        Refresh();
    }

    /// <summary>The nicknames changed: the lines are made again.</summary>
    public void Relabel() => Refresh();

    private void Refresh()
    {
        _list.ItemsSource = _model?.All.ToList() ?? [];
        _empty.IsVisible = _list.ItemCount == 0;
    }
}
