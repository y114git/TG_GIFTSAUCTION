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
        var row = new WrapPanel { Margin = new Thickness(8, 4), Children = { _bannerText, _retry, _showParent } };
        _banner = new Border { Name = "FallbackBanner", IsVisible = false, Child = row };
        SetDock(_banner, Avalonia.Controls.Dock.Top);
        return _banner;
    }

    private void ShowFallback(CodeLoad load)
    {
        if (_banner is null) return;
        _banner.IsVisible = load.Mode == CodeMode.Assembly && !ShowAssembly;
        _showParent.IsVisible = load.Mode == CodeMode.Assembly && load.ParentName is not null;
        _bannerText.Text = load.Mode == CodeMode.Assembly ? Loc.Current.Format("code.fallbackAssembly", ("reason", load.Reason ?? "")) : "";
    }

    private void CopyDetails()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) _ = clipboard.SetTextAsync(_message.Text ?? "");
    }
}
