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

    /// <summary>What the tab shows: decompiled text, the assembly, or the parent script.</summary>
    public CodeMode Mode { get; private set; }

    private Border BuildBanner()
    {
        _copyDetails.Content = Loc.Current["code.copyDetails"];
        _retry.Content = Loc.Current["code.retry"];
        _showParent.Content = Loc.Current["code.showParent"];
        _assembly.Content = Loc.Current["code.showAssembly"];
        _assembly.Click += (_, _) => ShowAssembly = !ShowAssembly;
        _retry.Click += (_, _) => Ready = LoadAsync();
        _showParent.Click += (_, _) => Ready = LoadAsync(parent: true);
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { _retry, _showParent } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, Margin = new Thickness(8, 4), Children = { _bannerText, buttons } };
        Grid.SetColumn(buttons, 1);
        _banner = new Border { Name = "FallbackBanner", IsVisible = false, Child = grid };
        SetDock(_banner, Avalonia.Controls.Dock.Top);
        return _banner;
    }

    private void ShowFallback(CodeLoad load)
    {
        if (_banner is null) return;
        _banner.IsVisible = load.Mode != CodeMode.Decompiled && !(load.Mode == CodeMode.Assembly && ShowAssembly);
        _showParent.IsVisible = load.Mode == CodeMode.Assembly && load.ParentName is not null;
        _bannerText.Text = load.Mode switch
        {
            CodeMode.Assembly => Loc.Current.Format("code.fallbackAssembly", ("reason", load.Reason ?? "")),
            CodeMode.Parent => Loc.Current.Format("code.fallbackParent", ("reason", load.Reason ?? ""), ("parent", load.ParentName ?? "")),
            _ => "",
        };
    }

    private void CopyDetails()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) _ = clipboard.SetTextAsync(_message.Text ?? "");
    }
}
