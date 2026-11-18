using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>
/// The image editor keeps its canvas: at every interface scale, language, editor size and tool the canvas has at least 60% of the editor height and 50% of its width,
/// and every control is either on the bar, in a side column or in the "..." drop-down, where nothing is cut. The scale is a layout transform, so an area of
/// W x H pixels at scale s is a layout of W/s x H/s.
/// </summary>
public sealed class ImageEditorLayoutTests
{
    public static TheoryData<string> Languages => ["en", "ru", "es"];

    private static readonly double[] Scales = [1.0, 1.25, 1.5];

    private static readonly (double Width, double Height)[] Areas = [(900, 600), (640, 480)];

    private static readonly ImageTool[] Tools = [ImageTool.Pencil, ImageTool.Rectangle, ImageTool.Fill, ImageTool.Text, ImageTool.Hand];

    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheCanvasKeepsMostOfTheEditorAndEveryControlStaysReachable(string language)
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(Corpus.Files(Corpus.Fixtures).First(), cancellationToken: TestContext.Current.CancellationToken);
        var sprite = workspace.Data.Sprites.First(s => s.Textures.Count > 0).Name.Content!;
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage(language);
            foreach (var scale in Scales)
            {
                foreach (var (width, height) in Areas)
                {
                    using var view = new ImageEditorView(new WorkspaceImageSession(workspace, sprite));
                    var window = await LayoutAudit.Host(view, width / scale, height / scale);
                    foreach (var tool in Tools)
                    {
                        view.SetTool(tool);
                        await LayoutAudit.Settle();
                        var tag = $"{language} {scale * 100}% {width}x{height} {tool}";
                        var share = (view.Canvas.Bounds.Width / view.Bounds.Width, view.Canvas.Bounds.Height / view.Bounds.Height);
                        if (share.Item1 < 0.5 || share.Item2 < 0.6) problems.Add($"{tag}: canvas {view.Canvas.Bounds.Width:0}x{view.Canvas.Bounds.Height:0} of {view.Bounds.Width:0}x{view.Bounds.Height:0}");
                        problems.AddRange(LayoutAudit.Overflows(window).Concat(LayoutAudit.CutText(window)).Concat(LayoutAudit.Misaligned(window)).Select(p => $"{tag}: {p}"));
                        problems.AddRange(Unreached(view).Select(p => $"{tag}: {p}"));
                        problems.AddRange((await LayoutAudit.DropDowns(window)).Select(p => $"{tag}: {p}"));
                    }

                    window.Content = null;
                    window.Close();
                }
            }

            Loc.Current.SetLanguage("en"); // the language is global to the test run
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    // Every group of the bar that is shown sits in a row or in the drop-down.
    private static List<string> Unreached(ImageEditorView view)
    {
        var found = new List<string>();
        var bar = view.GetVisualDescendants().OfType<G3MStudio.Theme.OverflowBar>().Single();
        foreach (var group in bar.Groups.Where(g => g.IsVisible))
        {
            if (!(bar.IsSpilled(group) || group.IsEffectivelyVisible && group.Bounds.Width > 0)) found.Add($"group {group.GetType().Name} at {group.Bounds} is neither on the bar nor in the drop-down");
        }

        return found;
    }
}
