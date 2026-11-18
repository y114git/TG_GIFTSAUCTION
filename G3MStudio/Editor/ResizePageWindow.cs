using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// Asks for a new size of a texture page and shows, while the numbers change, what happens to the pictures on it: a larger page keeps them in place,
/// a smaller one moves the ones outside it (see <see cref="Core.Workspace.PageResizeCommand"/>).
/// </summary>
public sealed class ResizePageWindow : Window
{
    private readonly NumericUpDown _width = new() { Name = "PageWidth", Minimum = 1, Maximum = Core.Workspace.TextureFit.MaxPageSide, Increment = 64, FormatString = "0", Width = 150 };
    private readonly NumericUpDown _height = new() { Name = "PageHeight", Minimum = 1, Maximum = Core.Workspace.TextureFit.MaxPageSide, Increment = 64, FormatString = "0", Width = 150 };
    private readonly TextBlock _fit = new() { Name = "PageFit", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Func<(int Width, int Height), FitOptions, string> _plan;
    private readonly FitOptionsPanel _advanced;

    // plan: what a size does to the pictures, with the Advanced choices; pages and automaticMax fill those choices (the pages moved pictures may go to first).
    public ResizePageWindow(string page, (int Width, int Height) size, Func<(int Width, int Height), FitOptions, string> plan, IReadOnlyList<string>? pages = null, int automaticMax = 2048)
    {
        _plan = plan;
        _advanced = new FitOptionsPanel([.. (pages ?? []).Where(p => p != page)], automaticMax);
        _advanced.Changed += ShowFit;
        Title = $"{Loc.Current["pages.resizeTitle"]}: {page}";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        (_width.Value, _height.Value) = (size.Width, size.Height);
        _width.ValueChanged += (_, _) => ShowFit();
        _height.ValueChanged += (_, _) => ShowFit();
        // Pages are powers of two as a rule: the two buttons double or halve both sides.
        var twice = new Button { Name = "PageDouble", Content = "x2" };
        twice.Click += (_, _) => (_width.Value, _height.Value) = (Math.Min(_width.Maximum, (_width.Value ?? 1) * 2), Math.Min(_height.Maximum, (_height.Value ?? 1) * 2));
        var half = new Button { Name = "PageHalf", Content = "/2" };
        half.Click += (_, _) => (_width.Value, _height.Value) = (Math.Max(1, Math.Floor((_width.Value ?? 2) / 2)), Math.Max(1, Math.Floor((_height.Value ?? 2) / 2)));
        var ok = DialogFrame.Action(this, Loc.Current["common.ok"], "PageResizeOk", () => Close(true), DialogRole.Primary);
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(DialogFrame.Form((Loc.Current["image.p.width"], _width), (Loc.Current["image.p.height"], _height)));
        body.Children.Add(new WrapPanel { ItemSpacing = 6, Children = { twice, half } });
        body.Children.Add(_advanced);
        body.Children.Add(DialogFrame.Form((Loc.Current["pages.fitLabel"], _fit)));
        PlanFit();
        DialogFrame.Show(this, body, [ok, DialogFrame.Cancel(this, () => Close(false), "PageResizeCancel")], scroll: DialogScroll.Vertical, dirty: DialogFrame.Snapshot([_width, _height, .. _advanced.Fields]));
    }

    public (int Width, int Height) Size => ((int)(_width.Value ?? 1), (int)(_height.Value ?? 1));

    /// <summary>The plan for the current size, or the reason the size is refused.</summary>
    public string FitText => _fit.Text ?? "";

    private bool _fitQueued;

    // Planning reads the whole page set under the lock, so a held arrow key or typed digits plan once per dispatcher pass, not once per value.
    private void ShowFit()
    {
        if (_fitQueued) return;
        _fitQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(PlanFit, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void PlanFit()
    {
        _fitQueued = false;
        try
        {
            _fit.Text = _plan(Size, Fit);
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            _fit.Text = e.Message;
        }
    }

    /// <summary>What the Advanced part holds now; the line under the fields is planned with it.</summary>
    public FitOptions Fit => _advanced.Options;

    /// <summary>The size and the Advanced choices, or null on Cancel.</summary>
    public async Task<(int Width, int Height, FitOptions Fit)?> AskAsync(Window owner)
    {
        if (await ShowDialog<bool?>(owner) != true) return null;
        _advanced.Remember();
        return (Size.Width, Size.Height, Fit);
    }
}
