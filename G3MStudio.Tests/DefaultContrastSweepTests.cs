using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Explorer;
using G3MStudio.Settings;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>
/// The default theme (green accent, dark surfaces) walked headless: every text and icon of the main window, each editor kind, Settings, menus, tooltips and the hover card is measured
/// against the fill behind it, and again with every row, button and tab forced into its pointer, selected and disabled states. Text needs 4.5:1, large text and icons 3:1, disabled 3:1.
/// </summary>
public sealed class DefaultContrastSweepTests(ITestOutputHelper output)
{
    private static async Task Pump(int rounds = 4)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(30);
        }
    }

    private void Report(List<string> problems)
    {
        // One line per finding, the surfaces it was seen on counted: the same style fails the same way everywhere.
        var lines = problems.GroupBy(p => p[(p.IndexOf(": ", StringComparison.Ordinal) + 2)..]).Select(g => $"{g.Key}  (x{g.Count()}, first in {g.First()[..g.First().IndexOf(": ", StringComparison.Ordinal)]})").ToList();
        foreach (var line in lines) output.WriteLine(line);
        Assert.True(lines.Count == 0, string.Join(Environment.NewLine, lines));
    }

    [Fact]
    public async Task TheMainWindowAndEveryEditorKeepTheirTextReadableInEveryState()
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                window.Width = 1400;
                window.Height = 900;
                problems.AddRange(await ContrastSweep.Sweep(window, "start page"));
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var data = window.Workspaces.Current!.Data;
                var tabs = new List<(string Kind, string Key)>
                {
                    ("resource", "sprites:" + data.Sprites.First(s => s.Textures.Count > 0).Name.Content),
                    ("image", "sprites:" + data.Sprites.First(s => s.Textures.Count > 0).Name.Content),
                    ("resource", "objects:" + data.GameObjects[0].Name.Content),
                    ("resource", "rooms:" + data.Rooms[0].Name.Content),
                    ("code", "code:" + data.Code[0].Name.Content),
                };
                if (data.Fonts.Count > 0) tabs.Add(("resource", "fonts:" + data.Fonts[0].Name.Content));
                if (data.Sounds.Count > 0) tabs.Add(("resource", "sounds:" + data.Sounds[0].Name.Content));
                if (data.Backgrounds.Count > 0) tabs.Add(("resource", "backgrounds:" + data.Backgrounds[0].Name.Content));
                if (data.Shaders.Count > 0) tabs.Add(("resource", "shaders:" + data.Shaders[0].Name.Content));
                await Pump();
                problems.AddRange(await ContrastSweep.Sweep(window, "main window"));
                foreach (var (kind, key) in tabs)
                {
                    window.Docking.OpenTab(kind, key, false);
                    await Pump(6);
                    problems.AddRange(await ContrastSweep.Sweep(window, kind + " " + key));
                }

                foreach (var top in window.GetVisualDescendants().OfType<Menu>().SelectMany(m => m.Items.OfType<MenuItem>()).ToList())
                {
                    top.IsSubMenuOpen = true;
                    await Pump(2);
                    if (top.GetVisualDescendants().OfType<Popup>().FirstOrDefault(p => p.IsOpen)?.Child is { } child) problems.AddRange(await ContrastSweep.Sweep(child, "menu " + top.Header));
                    top.IsSubMenuOpen = false;
                }
            }
            finally { window.Close(); }
        });
        Report(problems);
    }

    [Fact]
    public async Task EverySettingsPageKeepsItsTextReadableInEveryState()
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                window.Width = 1200;
                window.Height = 800;
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                for (var section = 0; section < G3MStudio.Core.Commands.AppCommands.SettingsPages.Length; section++)
                {
                    window.ShowSettings(section);
                    await Pump(3);
                    problems.AddRange(await ContrastSweep.Sweep(window.GetVisualDescendants().OfType<SettingsPage>().Single(), "settings " + G3MStudio.Core.Commands.AppCommands.SettingsPages[section]));
                }
            }
            finally { window.Close(); }
        });
        Report(problems);
    }

    [Fact]
    public async Task TheHoverCardAndTooltipsKeepTheirTextReadable()
    {
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var button = new Button { Content = "probe" };
            var window = new Window { Content = new StackPanel { Children = { button } }, Width = 400, Height = 300 };
            window.Show();
            HoverCards.Describe = key => new G3MStudio.Core.Workspace.ResourceHoverInfo(key, "sprites", "spr_face_s6", null, 7);
            ToolTip.SetTip(button, new HoverTip("sprites:spr_face_s6", "spr_face_s6", "More text here"));
            ToolTip.SetIsOpen(button, true);
            await Pump(6);
            foreach (var tip in window.GetVisualDescendants().OfType<ToolTip>()) problems.AddRange(await ContrastSweep.Sweep(tip, "hover card"));
            ToolTip.SetIsOpen(button, false);
            ToolTip.SetTip(button, "A plain tooltip with some words");
            ToolTip.SetIsOpen(button, true);
            await Pump(6);
            foreach (var tip in window.GetVisualDescendants().OfType<ToolTip>()) problems.AddRange(await ContrastSweep.Sweep(tip, "tooltip"));
            window.Close();
        });
        Report(problems);
    }

    // A hover card is a surface of its own: whatever state the row, tab, button or item under it is in (its style selectors reach the card through the logical tree), the card keeps the muted colour and 4.5:1.
    [Fact]
    public async Task AHoverCardKeepsItsMutedTextWhateverStateTheControlUnderItIsIn()
    {
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var muted = (ISolidColorBrush)Application.Current!.FindResource("Text.Muted")!;
            var kinds = new (Func<Control> Make, string[] States)[]
            {
                (() => new Button { Content = "probe" }, [":pointerover", ":pressed", ":focus"]),
                (() => new ToggleButton { Content = "probe" }, [":pointerover", ":pressed", ":checked", ":checked:pointerover"]),
                (() => new ListBoxItem { Content = "probe" }, [":pointerover", ":pressed", ":selected", ":selected:pointerover", ":selected:pressed"]),
                (() => new ComboBoxItem { Content = "probe" }, [":pointerover", ":selected", ":selected:pointerover"]),
                (() => new TreeViewItem { Header = "probe" }, [":pointerover", ":selected", ":selected:pointerover"]),
                (() => new TabItem { Header = "probe" }, [":pointerover", ":selected", ":selected:pointerover"]),
                (() => new MenuItem { Header = "probe" }, [":pointerover", ":selected"]),
                (() => new Dock.Avalonia.Controls.DocumentTabStripItem { Content = "probe" }, [":pointerover", ":selected", ":selected:pointerover"]),
                (() => new Dock.Avalonia.Controls.ToolTabStripItem { Content = "probe" }, [":pointerover", ":selected", ":selected:pointerover"]),
            };
            HoverCards.Describe = key => new G3MStudio.Core.Workspace.ResourceHoverInfo(key, "sprites", "spr_face_s6", null, 14);
            foreach (var (make, states) in kinds)
            {
                var control = make();
                var window = new Window { Content = new StackPanel { Children = { control } }, Width = 400, Height = 300 };
                window.Show();
                HoverCards.Attach(control, "sprites:spr_face_s6", "spr_face_s6", "More text here");
                ToolTip.SetIsOpen(control, true);
                await Pump(6);
                foreach (var state in states)
                {
                    var undo = ContrastSweep.Force(control, state);
                    await Pump(6);
                    foreach (var tip in window.GetVisualDescendants().OfType<ToolTip>())
                    {
                        var where = control.GetType().Name + state;
                        problems.AddRange(ContrastSweep.Measure(tip, state).Select(p => where + ": " + p));
                        foreach (var text in tip.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name is "HoverMeta" or "HoverName" && t.IsVisible))
                            if (text.Foreground is not ISolidColorBrush fg || fg.Color != muted.Color) problems.Add(where + ": " + text.Name + " is " + (text.Foreground as ISolidColorBrush)?.Color + ", not the muted " + muted.Color);
                    }

                    undo();
                }

                ToolTip.SetIsOpen(control, false);
                ToolTip.SetTip(control, "A plain tip");
                ToolTip.SetIsOpen(control, true);
                await Pump(6);
                foreach (var state in states)
                {
                    var undo = ContrastSweep.Force(control, state);
                    await Pump(6);
                    foreach (var tip in window.GetVisualDescendants().OfType<ToolTip>()) problems.AddRange(ContrastSweep.Measure(tip, state).Select(p => "plain tip under " + control.GetType().Name + state + ": " + p));
                    undo();
                }

                ToolTip.SetIsOpen(control, false);
                window.Close();
            }
        });
        Report(problems);
    }
}
