using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>
/// Every editor, dock panel, settings page and the home page in each of the seven languages and at narrow widths: no button, box or check is cut off or squeezed to
/// nothing (it lies inside what clips it, or a scroll viewer can bring it into view), and no caption of a button is clipped. Editors are hosted the way the dock
/// hosts them, directly in a narrow area; panels at the narrowest a docked panel gets.
/// </summary>
public sealed class LanguageLayoutAuditTests
{
    public static TheoryData<string> Languages => ["en", "ru", "es", "ja", "ko", "zh_cn", "zh_tw"];

    private static readonly string[] Types = ["general", "sprites", "tileSets", "sounds", "paths", "scripts", "shaders", "fonts", "timelines", "objects", "rooms", "extensions", "sequences", "animationCurves", "particleSystems", "texturePageItems", "embeddedTextures", "uiLayers"];

    private static readonly double[] EditorWidths = [640, 420, 260];

    private static readonly double[] PanelWidths = [220, 160];

    private static IEnumerable<string> Problems(Window window) => LayoutAudit.Overflows(window).Concat(LayoutAudit.CutText(window));

    private static async Task Hosted(List<string> problems, string tag, Control view, double width, Action<Editor.CodeEditorTab>? state = null)
    {
        var window = await LayoutAudit.Host(view, width);
        if (view is Editor.CodeEditorTab code)
        {
            await code.Ready;
            state?.Invoke(code);
            await LayoutAudit.Settle();
        }

        problems.AddRange(Problems(window).Concat(await LayoutAudit.DropDowns(window)).Select(p => $"{tag} at {width}: {p}"));
        window.Content = null;
        window.Close();
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public async Task EveryEditorFitsANarrowAreaInEveryLanguage(string language)
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                Loc.Current.SetLanguage(language);
                var data = window.Workspaces.Current!.Data;
                var tabs = new List<TabInfo>();
                foreach (var type in Types)
                {
                    if (ResourceTypes.All.First(t => t.Key == type).Items(data)?.FirstOrDefault() is { } item) tabs.Add(new TabInfo("tab" + tabs.Count, "resource", ResourceTypes.KeyOf(type, ResourceTypes.NameOf(item))));
                }

                tabs.Add(new TabInfo("tab" + tabs.Count, "code", ResourceTypes.KeyOf("code", data.Code.First().Name.Content!)));
                if (data.Sprites.FirstOrDefault(s => s.Textures.Count > 0) is { } sprite) tabs.Add(new TabInfo("tab" + tabs.Count, "image", ResourceTypes.KeyOf("sprites", sprite.Name.Content!)));
                var entry = data.Code.First().Name.Content!;
                var workspace = window.Workspaces.Current!;
                foreach (var width in EditorWidths)
                {
                    foreach (var tab in tabs)
                    {
                        if (window.CreateEditor(tab) is { } view) await Hosted(problems, $"{language} {tab.Kind} {tab.Key}", view, width);
                    }

                    // The states of the code editor that add controls: an error with its copy button, the banner of a failed decompile, the assembly view, the diff.
                    await Hosted(problems, $"{language} code with an error", new Editor.CodeEditorTab(workspace, entry), width, code =>
                    {
                        code.Editor.Text = "if (";
                        code.Apply();
                    });
                    await Hosted(problems, $"{language} code with the fallback banner", new Editor.CodeEditorTab(workspace, entry, () => new CodeLoad("nop", CodeMode.Assembly, "boom", "scr_parent")), width);
                }
            }
            finally { window.Close(); }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public async Task EveryPanelFitsADockedWidthInEveryLanguage(string language)
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                Loc.Current.SetLanguage(language);
                foreach (var id in (string[])["Explorer", "Inspector", "History", "Git", "Problems", "Search", "Usages", "Reference", "Output"])
                {
                    var widths = id == "Reference" ? [360] : PanelWidths; // the panel host never makes the reference panel narrower than 360
                    foreach (var width in widths)
                    {
                        if (window.PanelContent(id) is not { } panel) continue;
                        var dockHost = panel.Parent as Decorator;
                        dockHost?.Child = null; // a panel is one control for the whole session: it leaves the dock for the audit and goes back
                        problems.AddRange((await Panel(panel, width)).Select(p => $"{language} panel {id} at {width}: {p}"));
                        dockHost?.Child = panel;
                    }
                }
            }
            finally { window.Close(); }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    private static async Task<List<string>> Panel(Control panel, double width)
    {
        var holder = new Decorator { Child = panel };
        var hosted = await LayoutAudit.Host(holder, width, 520);
        var found = Problems(hosted).ToList();
        holder.Child = null;
        hosted.Close();
        return found;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public async Task HomeAndEverySettingsPageFitTheSmallestWindowInEveryLanguage(string language)
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Loc.Current.SetLanguage(language);
                (window.Width, window.Height) = (window.MinWidth, window.MinHeight);
                await LayoutAudit.Settle();
                problems.AddRange(Problems(window).Select(p => $"{language} home: {p}"));
                window.Registry.Execute("settings.open");
                await LayoutAudit.Settle();
                var nav = window.GetVisualDescendants().OfType<ListBox>().First(l => l.Name == "SettingsNav");
                for (var i = 0; i < nav.ItemCount; i++)
                {
                    nav.SelectedIndex = i;
                    await LayoutAudit.Settle();
                    problems.AddRange(Problems(window).Select(p => $"{language} settings page {i}: {p}"));
                }
            }
            finally { window.Close(); }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }
}
