using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// When the decompiler fails the tab shows the assembly (or the parent script) under a banner, never an empty editor.
public sealed partial class CodeEditorTab
{
    private readonly Button _copyDetails = new() { Name = "CopyDetails", IsVisible = false };
    private readonly SelectableTextBlock _bannerText = new() { Name = "FallbackText", TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly Button _retry = new() { Name = "Retry" };
    private readonly Button _showParent = new() { Name = "ShowParent" };
    private readonly Button _childDetails = new() { Name = "ChildDetailsToggle" };
    private readonly SelectableTextBlock _childText = new() { Name = "ChildDetails", IsVisible = false, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button _assembly = new() { Name = "AssemblyToggle" };
    private Border? _banner;


    /// <summary>True while the banner for a failed decompile is shown.</summary>
    public bool ShowsFallback => _banner?.IsVisible == true;

    /// <summary>True while the tab shows the VM assembly on request (the decompiled text is the other view). Applying then assembles the text instead of compiling it.</summary>
    public bool ShowAssembly
    {
        get;
        set
        {
            if (field == value || HasPending) return; // a pending edit is Applied or Reverted first, so a switch never drops typed text
            field = value;
            _original = null; // the change view compares against the first text of this view
            _assembly.Content = Loc.Current[value ? "code.showGml" : "code.showAssembly"];
            Ready = LoadAsync();
        }
    }

    /// <summary>What the tab shows: decompiled text or the assembly.</summary>
    public CodeMode Mode { get; private set; }

    private void RelabelBanner()
    {
        _copyDetails.Content = Loc.Current["code.copyDetails"];
        _retry.Content = Loc.Current["code.retry"];
        _showParent.Content = Loc.Current["code.showParent"];
        _childDetails.Content = Loc.Current[_childText.IsVisible ? "code.child.hideDetails" : "code.child.showDetails"];
        if (_shown?.Child is { } child) _childText.Text = ChildDetails(child);
        _assembly.Content = Loc.Current[ShowAssembly ? "code.showGml" : "code.showAssembly"];
        ToolTip.SetTip(_assembly, Loc.Current["code.switchTip"]);
    }

    private Border BuildBanner()
    {
        RelabelBanner();
        // Switching views needs the typed text in the model first, so a pending edit is saved; a failed save keeps the view.
        _assembly.Click += (_, _) =>
        {
            if (HasPending && !Apply()) return;
            ShowAssembly = !ShowAssembly;
        };
        _retry.Click += (_, _) => Ready = LoadAsync();
        // The parent opens in its own tab, so its assembly, edits and saves are the parent's and never the child's.
        _showParent.Click += (_, _) =>
        {
            if (_shown?.ParentName is { } parent) GoToRequested?.Invoke(ResourceTypes.KeyOf("code", parent));
        };
        _childDetails.Click += (_, _) =>
        {
            _childText.IsVisible = !_childText.IsVisible;
            RelabelBanner();
        };
        var row = new WrapPanel { Children = { _bannerText, _retry, _showParent, _childDetails } };
        _banner = new Border { Name = "FallbackBanner", IsVisible = false, Margin = new Thickness(8, 4), Child = new StackPanel { Children = { row, _childText } } };
        SetDock(_banner, Avalonia.Controls.Dock.Top);
        return _banner;
    }

    private void ShowFallback(CodeLoad load)
    {
        if (_banner is null) return;
        _banner.IsVisible = load.Mode == CodeMode.Assembly && !ShowAssembly;
        _showParent.IsVisible = load.Mode == CodeMode.Assembly && load.ParentName is not null;
        _childDetails.IsVisible = load.Child is not null;
        _assembly.IsVisible = load.Child is null; // a function inside another entry is only shown as assembly, there is no GML to switch to
        if (load.Child is null) _childText.IsVisible = false;
        _bannerText.Text = load.Child is { } child ? Loc.Current.Format(child.Anonymous ? "code.child.bannerAnonymous" : "code.child.banner", ("parent", child.Parent))
            : load.Mode == CodeMode.Assembly ? Loc.Current.Format("code.fallbackAssembly", ("reason", load.Reason ?? "")) : "";
        RelabelBanner();
    }

    private string ChildDetails(CodeChild child) => Loc.Current.Format("code.child.details", ("name", Entry), ("parent", child.Parent),
        ("kind", Loc.Current[child.Anonymous ? "code.child.kindAnonymous" : "code.child.kindNamed"]), ("arguments", child.Arguments), ("locals", child.Locals),
        ("offset", child.Offset), ("length", child.Length));

    private void CopyDetails()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) _ = clipboard.SetTextAsync(_message.Text ?? "");
    }
}
