using Avalonia.Controls;
using Avalonia.Controls.Templates;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Panels;

/// <summary>The Problems panel: compile errors of code entries. A double click asks the window to open the entry at the position.</summary>
public sealed class ProblemsPanel : UserControl
{
    private readonly ListBox _list = new() { Name = "ProblemsList" };
    private ProblemsModel? _model;
    private readonly TextBlock _empty = new() { Name = "ProblemsEmpty", Text = Localization.Loc.Current["empty.problems"], Classes = { "muted" }, Margin = new Avalonia.Thickness(12), IsHitTestVisible = false };

    public ProblemsPanel()
    {
        _list.ItemTemplate = new FuncDataTemplate<Problem>((p, _) => new TextBlock { Text = p is null ? "" : $"{p.Source}({p.Line},{p.Column}) {p.Code}: {p.Message}", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        _list.DoubleTapped += (_, _) => { if (_list.SelectedItem is Problem problem) OpenRequested?.Invoke(problem); };
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

    private void Refresh()
    {
        _list.ItemsSource = _model?.All.ToList() ?? [];
        _empty.IsVisible = _list.ItemCount == 0;
        _empty.Text = Localization.Loc.Current["empty.problems"];
    }
}
