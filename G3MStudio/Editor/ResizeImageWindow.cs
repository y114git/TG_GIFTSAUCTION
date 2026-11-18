using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>What the Resize dialog chose; <see cref="Fit"/> holds the Advanced choices, null when the dialog has none.</summary>
public sealed record ResizeChoice(ResizeSpec Spec, bool AllFrames, string? Group, FitOptions? Fit = null);

/// <summary>
/// The Resize Image/Canvas dialog of the image editor. Resize image stretches the picture and the canvas together (nearest or bilinear); Resize canvas changes only the canvas:
/// the pixels stay at the anchor and new area is filled. Pixels or percent, the aspect kept on request, this frame or every frame of the sprite, and the texture group.
/// The options and the percents (not the pixel sizes, which start at the current size) come back as they were last applied. Every field sits in one label and value grid, so the values share one left edge.
/// </summary>
public sealed class ResizeImageWindow : Window
{
    private readonly (int Width, int Height) _size;
    private readonly ComboBox _mode = new() { Name = "ResizeMode", MinWidth = 200 };
    private readonly ComboBox _method = new() { Name = "ResizeMethod", MinWidth = 200 };
    private readonly ComboBox _fill = new() { Name = "ResizeFill", MinWidth = 200 };
    private readonly ComboBox _unit = new() { Name = "ResizeUnit", MinWidth = 200 };
    private readonly NumericUpDown _width = new() { Name = "ResizeWidth", Minimum = 1, Maximum = ResizeSpec.MaxSide, Increment = 1, FormatString = "0", Width = 150 };
    private readonly NumericUpDown _height = new() { Name = "ResizeHeight", Minimum = 1, Maximum = ResizeSpec.MaxSide, Increment = 1, FormatString = "0", Width = 150 };
    private readonly CheckBox _keep = new() { Name = "ResizeKeep" };
    private readonly RadioButton _scopeFrame = new() { Name = "ResizeScopeFrame", GroupName = "ResizeScope" };
    private readonly RadioButton _scopeAll = new() { Name = "ResizeScopeAll", GroupName = "ResizeScope" };
    private readonly TextBlock _warn = new() { Name = "ResizeScopeWarning", TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontWeight = Avalonia.Media.FontWeight.SemiBold };
    private readonly IReadOnlyList<(int Width, int Height)> _frameSizes;
    private readonly int _frames;
    private readonly int _frame;
    private readonly Button _ok;
    private readonly CheckBox _scaleBox = new() { Name = "ResizeScaleBox" };
    private readonly bool _hasBox;
    private readonly ResizeChoices _last = ResizeChoices.Current;
    private readonly ComboBox _group = new() { Name = "ResizeGroup", MinWidth = 200 };
    private readonly RadioButton[] _anchors = new RadioButton[9];
    private readonly IReadOnlyList<string> _groups;
    private readonly TextBlock _fit = new() { Name = "ResizeFit", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Func<ResizeChoice, string?>? _plan;
    private readonly FitOptionsPanel? _advanced;
    private bool _busy;

    // scale: the mode the entry that opened the dialog names (Resize image or Resize canvas); null takes the one chosen last time, Resize image at first.
    // plan: what the choice does on the texture pages, shown under the fields while the numbers change; null shows nothing.
    // pages and automaticMax: with a plan, the Advanced choices offer these pages and name the largest page side without a choice.
    // box: the picture is a whole sprite, so Resize image may leave its collision box and origin alone.
    public ResizeImageWindow((int Width, int Height) size, int frames, IReadOnlyList<string> groups, string? group, bool? scale, Func<ResizeChoice, string?>? plan = null,
        IReadOnlyList<string>? pages = null, int automaticMax = 0, bool box = false, int frame = 0, IReadOnlyList<(int Width, int Height)>? frameSizes = null)
    {
        (_size, _groups, _plan, _hasBox, _frames, _frame) = (size, groups, plan, box, frames, frame);
        _frameSizes = frameSizes is { Count: > 0 } ? frameSizes : [.. Enumerable.Repeat(size, Math.Max(1, frames))];
        Title = Loc.Current["image.resizeTitle"];
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var last = _last;
        _mode.ItemsSource = new[] { Loc.Current["image.resizeScale"], Loc.Current["image.resizeCanvas"] };
        _mode.SelectedIndex = (scale ?? last.Scale) ? 0 : 1;
        _method.ItemsSource = new[] { Loc.Current["image.methodNearest"], Loc.Current["image.methodBilinear"] };
        _method.SelectedIndex = last.Smooth ? 1 : 0;
        _fill.ItemsSource = new[] { Loc.Current["image.fillClear"], Loc.Current["image.fillBlack"], Loc.Current["image.fillWhite"] };
        _fill.SelectedIndex = last.Fill;
        _unit.ItemsSource = new[] { Loc.Current["image.unitPixels"], Loc.Current["image.unitPercent"] };
        _unit.SelectedIndex = last.Percent ? 1 : 0;
        _keep.Content = Loc.Current["image.keepRatio"];
        _keep.IsChecked = last.Keep;
        // The scope is the selected frame every time the dialog opens: an all-frames resize is never carried over from an earlier dialog.
        _scopeFrame.Content = new TextBlock { Text = Loc.Current.Format("image.scopeFrame", ("n", frame + 1)), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        _scopeAll.Content = new TextBlock { Text = Loc.Current.Format("image.scopeAll", ("n", frames)), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        _scopeFrame.IsChecked = true;
        _warn.Foreground = Avalonia.Application.Current?.FindResource("Warning") as Avalonia.Media.IBrush;
        _scaleBox.Content = Loc.Current["image.scaleBox"];
        _scaleBox.IsChecked = last.Box;
        _group.ItemsSource = groups;
        _group.SelectedItem = group;
        SetValues(size.Width, size.Height);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), HorizontalAlignment = HorizontalAlignment.Left };
        for (var i = 0; i < 9; i++)
        {
            var anchor = _anchors[i] = new RadioButton { Name = "ResizeAnchor" + i, GroupName = "ResizeAnchor", IsChecked = i == last.Anchor, Margin = new Thickness(2) };
            Grid.SetRow(anchor, i / 3);
            Grid.SetColumn(anchor, i % 3);
            grid.Children.Add(anchor);
        }

        var ok = _ok = DialogFrame.Action(this, Loc.Current["common.ok"], "ResizeOk", () => Close(true), DialogRole.Primary);
        _mode.SelectionChanged += (_, _) => ApplyMode(grid);
        ApplyMode(grid);
        _unit.SelectionChanged += (_, _) => SetValues(_size.Width, _size.Height);
        _width.ValueChanged += (_, _) => Follow(true);
        _height.ValueChanged += (_, _) => Follow(false);
        _width.ValueChanged += (_, _) => { ShowFit(); ShowScope(); };
        _height.ValueChanged += (_, _) => { ShowFit(); ShowScope(); };
        _unit.SelectionChanged += (_, _) => ShowScope();
        _scaleBox.IsCheckedChanged += (_, _) => ShowFit();
        _mode.SelectionChanged += (_, _) => ShowFit();
        _method.SelectionChanged += (_, _) => ShowFit();
        _fill.SelectionChanged += (_, _) => ShowFit();
        _unit.SelectionChanged += (_, _) => ShowFit();
        _scopeAll.IsCheckedChanged += (_, _) => { ShowFit(); ShowScope(); };
        _group.SelectionChanged += (_, _) => ShowFit();
        var body = new StackPanel { Spacing = 8 };
        ToolTip.SetTip(_mode, Loc.Current["image.modeTip"]);
        ToolTip.SetTip(_keep, Loc.Current["image.keepRatioTip"]);
        ToolTip.SetTip(_unit, Loc.Current["image.unitTip"]);
        // One grid for every field: labels in the left column, controls (and the check boxes) on one left edge in the right one. A row that does not apply is left out, so it leaves no gap.
        List<(string, Control)> rows = [(Loc.Current["image.mode"], _mode), (Loc.Current["image.unit"], _unit), (Loc.Current["image.p.width"], _width), (Loc.Current["image.p.height"], _height), ("", _keep),
            (Loc.Current["image.method"], _method), (Loc.Current["image.fill"], _fill), (Loc.Current["image.anchor"], grid)];
        if (box) rows.Add(("", _scaleBox));
        if (frames > 1) rows.Add((Loc.Current["image.scopeLabel"], new StackPanel { Name = "ResizeScope", Spacing = 4, Children = { _scopeFrame, _scopeAll, _warn } }));
        if (groups.Count > 0) rows.Add((Loc.Current["image.textureGroup"], _group));
        body.Children.Add(DialogFrame.Form([.. rows]));
        if (plan is not null && pages is not null)
        {
            _advanced = new FitOptionsPanel(pages, automaticMax);
            _advanced.Changed += ShowFit;
            body.Children.Add(_advanced);
        }

        if (plan is not null) body.Children.Add(DialogFrame.Form((Loc.Current["pages.fitLabel"], _fit)));
        PlanFit();
        ShowScope();
        DialogFrame.Show(this, body, [ok, DialogFrame.Cancel(this, () => Close(false), "ResizeCancel")], scroll: DialogScroll.Vertical,
            dirty: DialogFrame.Snapshot([_mode, _method, _fill, _unit, _width, _height, _keep, _scaleBox, _group, .. _advanced?.Fields ?? []]));
    }

    private bool _fitQueued;

    // Every frame is never silent: the warning names how many frames change, to what size, and what they are now; the button names the scope too.
    private void ShowScope()
    {
        var all = AllFrames;
        _warn.IsVisible = all && _frames > 1;
        if (_frames > 1)
        {
            var spec = Choice.Spec;
            _warn.Text = all ? ResizeScope.Warning(_frameSizes, spec.Width, spec.Height) : "";
            _ok.Content = Loc.Current.Format(all ? "image.resizeOkAll" : "image.resizeOkFrame", ("n", all ? _frames : _frame + 1));
        }
    }

    private bool AllFrames => _frames <= 1 || _scopeAll.IsChecked == true;

    // Each option works in one mode only: in the other it is off and its tooltip says why, so no choice is silently ignored.
    private void ApplyMode(Grid anchors)
    {
        var scale = _mode.SelectedIndex == 0;
        anchors.IsEnabled = !scale;
        ToolTip.SetTip(anchors, scale ? Loc.Current["image.anchorOff"] : Loc.Current["image.anchorTip"]);
        _method.GateKey(scale ? null : "image.methodOff");
        _scaleBox.GateKey(scale ? null : "image.scaleBoxOff");
        _fill.GateKey(scale ? "image.fillOff" : null);
        if (scale)
        {
            ToolTip.SetTip(_method, Loc.Current["image.methodTip"]);
            ToolTip.SetTip(_scaleBox, Loc.Current["image.scaleBoxTip"]);
        }
        else ToolTip.SetTip(_fill, Loc.Current["image.fillTip"]);
        ToolTip.SetShowOnDisabled(anchors, true);
    }

    private bool Percent => _unit.SelectedIndex == 1;

    /// <summary>The texture page line: what the choice will do to the pages.</summary>
    public string FitText => _fit.Text ?? "";

    // Planning reads the whole page set under the lock, so a held arrow key or typed digits plan once per dispatcher pass, not once per value.
    private void ShowFit()
    {
        if (_plan is null || _busy || _fitQueued) return;
        _fitQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(PlanFit, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void PlanFit()
    {
        _fitQueued = false;
        if (_plan is null) return;
        try
        {
            _fit.Text = _plan(Choice) ?? "";
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            _fit.Text = e.Message;
        }
    }

    private void SetValues(int w, int h)
    {
        _busy = true;
        (_width.Value, _height.Value) = Percent ? (_last.PercentW, _last.PercentH) : (w, h);
        _busy = false;
    }

    // With the aspect kept, the other side follows the one the user changed.
    private void Follow(bool widthChanged)
    {
        if (_busy || _keep.IsChecked != true) return;
        _busy = true;
        if (widthChanged) _height.Value = Percent ? _width.Value : Math.Clamp((decimal)Math.Round((double)(_width.Value ?? 1) * _size.Height / _size.Width), 1, ResizeSpec.MaxSide);
        else _width.Value = Percent ? _height.Value : Math.Clamp((decimal)Math.Round((double)(_height.Value ?? 1) * _size.Width / _size.Height), 1, ResizeSpec.MaxSide);
        _busy = false;
    }

    /// <summary>What the dialog holds now.</summary>
    public ResizeChoice Choice
    {
        get
        {
            var (w, h) = ((double)(_width.Value ?? 1), (double)(_height.Value ?? 1));
            var (pw, ph) = Percent ? (ResizeSpec.Percent(_size.Width, w), ResizeSpec.Percent(_size.Height, h)) : ((int)w, (int)h);
            var anchor = Math.Max(0, Array.FindIndex(_anchors, a => a.IsChecked == true));
            return new ResizeChoice(new ResizeSpec(_mode.SelectedIndex == 0, pw, ph, anchor % 3, anchor / 3) { Smooth = _method.SelectedIndex == 1, Fill = ResizeChoices.FillColour(_fill.SelectedIndex), ScaleBox = !_hasBox || _scaleBox.IsChecked == true }, AllFrames, _groups.Count > 0 ? _group.SelectedItem as string : null, _advanced?.Options);
        }
    }

    /// <summary>Keeps the options (not the sizes) for the next dialog; an applied dialog calls this.</summary>
    public void Remember()
    {
        _advanced?.Remember();
        ResizeChoices.Current = ResizeChoices.Current with
        {
            Scale = _mode.SelectedIndex == 0,
            Smooth = _method.SelectedIndex == 1,
            Keep = _keep.IsChecked == true,
            Percent = Percent,
            Anchor = Math.Max(0, Array.FindIndex(_anchors, a => a.IsChecked == true)),
            Fill = _fill.SelectedIndex,
            Box = _hasBox ? _scaleBox.IsChecked == true : ResizeChoices.Current.Box,
            PercentW = Percent ? Math.Clamp((int)Math.Round(_width.Value ?? 100), 1, ResizeSpec.MaxSide) : ResizeChoices.Current.PercentW,
            PercentH = Percent ? Math.Clamp((int)Math.Round(_height.Value ?? 100), 1, ResizeSpec.MaxSide) : ResizeChoices.Current.PercentH,
        };
    }

    /// <summary>The choice, or null on Cancel.</summary>
    public async Task<ResizeChoice?> AskAsync(Window owner)
    {
        if (await ShowDialog<bool?>(owner) != true) return null;
        Remember();
        return Choice;
    }
}
