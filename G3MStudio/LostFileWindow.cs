using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

public enum LostChoiceKind
{
    /// <summary>Closing the window, Escape.</summary>
    Cancel,
    Open,
    Browse,
    Retry,
    RemoveRecent,
    Home,
}

/// <summary>The answer of <see cref="LostFileWindow"/>: <paramref name="Path"/> is the entry for Open, <paramref name="KeepLayout"/> whether the saved layout moves to a file whose content differs.</summary>
public sealed record LostChoice(LostChoiceKind Kind, string? Path = null, bool KeepLayout = true);

/// <summary>What the dialog explains and offers. <paramref name="ChooseLayout"/> shows the keep-layout box (the policy is Ask).</summary>
public sealed record LostRequest(string Path, LostReason Reason, string Detail, IReadOnlyList<Candidate> Entries, bool ChooseLayout, bool KeepByDefault, bool CanRemoveRecent);

/// <summary>
/// The question when a remembered data file is gone, unreadable or not the same as before: why, the files that may be it (same content first), and the ways on:
/// open one, choose another, try again, forget the recent entry, or go Home.
/// </summary>
public sealed class LostFileWindow : Window
{
    private readonly ListBox _list = new() { Name = "LostCandidates", MinHeight = 90, MaxHeight = 220 };

    public LostFileWindow(LostRequest request)
    {
        Title = Loc.Current["lostFile.title"];
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var name = System.IO.Path.GetFileName(request.Path);
        var message = request.Reason switch
        {
            LostReason.Missing => Loc.Current.Format("lostFile.missing", ("name", name), ("path", request.Path)),
            LostReason.Unreadable => Loc.Current.Format("lostFile.unreadable", ("name", name), ("path", request.Path), ("reason", request.Detail)),
            _ => Loc.Current.Format("lostFile.changed", ("name", name)),
        };
        var keep = new CheckBox { Name = "LostKeepLayout", IsChecked = request.KeepByDefault }.Tr(ContentControl.ContentProperty, "lostFile.keep");
        keep.IsVisible = false;
        var open = DialogFrame.Action(this, Loc.Current["lostFile.open"], "LostOpen", () => Done(LostChoiceKind.Open), DialogRole.Primary);
        _list.ItemsSource = request.Entries.Select(e => $"{e.Path}  ({Loc.Current[e.SameHash ? "lostFile.sameHash" : "lostFile.sameName"]})").ToList();
        void Update()
        {
            var at = _list.SelectedIndex;
            open.IsEnabled = at >= 0;
            keep.IsVisible = request.ChooseLayout && at >= 0 && !request.Entries[at].SameHash;
            // A file of other content has a layout of its own: moving the lost file's layout onto it is the user's choice, never the default.
            if (at >= 0) keep.IsChecked = request.KeepByDefault && (request.Entries[at].SameHash || request.Entries[at].Path == request.Path);
        }

        void Done(LostChoiceKind kind)
        {
            var at = _list.SelectedIndex;
            Close(new LostChoice(kind, kind == LostChoiceKind.Open && at >= 0 ? request.Entries[at].Path : null, keep.IsChecked == true));
        }

        _list.SelectionChanged += (_, _) => Update();
        _list.DoubleTapped += (_, _) => { if (_list.SelectedIndex >= 0) Done(LostChoiceKind.Open); };
        // Enter opens the preselected entry, so only the file that is the lost one (same content, or the file itself when it changed) is preselected.
        _list.SelectedIndex = request.Entries.ToList().FindIndex(e => e.SameHash || e.Path == request.Path);
        Update();
        var actions = new List<Control>();
        if (request.Entries.Count > 0) actions.Add(open);
        actions.Add(DialogFrame.Action(this, Loc.Current["lostFile.choose"], "LostBrowse", () => Done(LostChoiceKind.Browse), request.Entries.Count == 0 ? DialogRole.Primary : DialogRole.Normal));
        if (request.Reason is LostReason.Missing or LostReason.Unreadable) actions.Add(DialogFrame.Action(this, Loc.Current["lostFile.retry"], "LostRetry", () => Done(LostChoiceKind.Retry)));
        if (request.CanRemoveRecent) actions.Add(DialogFrame.Action(this, Loc.Current["lostFile.remove"], "LostRemove", () => Done(LostChoiceKind.RemoveRecent)));
        actions.Add(DialogFrame.Action(this, Loc.Current["lostFile.home"], "LostHome", () => Done(LostChoiceKind.Home), DialogRole.Cancel));
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        if (request.Entries.Count > 0)
        {
            body.Children.Add(new TextBlock { Classes = { "accent" } }.Tr(TextBlock.TextProperty, "lostFile.pick"));
            body.Children.Add(_list);
            body.Children.Add(keep);
        }
        else
        {
            body.Children.Add(new TextBlock { Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "lostFile.none"));
        }

        DialogFrame.Show(this, body, actions, scroll: DialogScroll.Vertical);
    }

    /// <summary>The answer; closing the window with its own button counts as Home.</summary>
    public async Task<LostChoice> AskAsync(Window owner) => await ShowDialog<LostChoice?>(owner) ?? new LostChoice(LostChoiceKind.Home);
}
