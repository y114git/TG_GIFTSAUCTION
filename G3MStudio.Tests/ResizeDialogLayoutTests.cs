using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using G3MStudio.Editor;
using G3MStudio.Localization;

#pragma warning disable CA2000 // the windows are closed by the test
namespace G3MStudio.Tests;

/// <summary>The Resize Image/Canvas dialog keeps every field, the unit box included, in one label and value grid: the controls share one left edge at every interface scale.</summary>
public sealed class ResizeDialogLayoutTests
{
    private static readonly string[] Fields = ["ResizeMode", "ResizeUnit", "ResizeWidth", "ResizeHeight", "ResizeKeep", "ResizeMethod", "ResizeFill", "ResizeScaleBox", "ResizeScope", "ResizeGroup"];

    [Theory]
    [InlineData(100, "en")]
    [InlineData(125, "en")]
    [InlineData(150, "en")]
    [InlineData(100, "ru")]
    [InlineData(125, "ru")]
    [InlineData(150, "ru")]
    [InlineData(100, "es")]
    [InlineData(125, "es")]
    [InlineData(150, "es")]
    public async Task EveryFieldOfTheResizeDialogStartsAtTheSameLeftEdge(int scale, string language)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var owner = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                owner.Workspaces.Settings.Update(x => x with { UiScale = scale });
                Loc.Current.SetLanguage(language);
                ResizeChoices.Current = new ResizeChoices();
                var dialog = new ResizeImageWindow((55, 75), 3, ["Default", "Other"], "Default", true, _ => "plan", ["Texture 0"], 2048, true);
                dialog.Show(owner);
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(30);
                    Dispatcher.UIThread.RunJobs();
                }

                var controls = dialog.GetLogicalDescendants().OfType<Control>().ToList();
                Control Named(string name) => controls.Single(c => c.Name == name);
                var fields = Fields.Select(Named).Append(Named("ResizeAnchor0").GetLogicalAncestors().OfType<Grid>().First()).ToList();
                var edges = fields.Select(c => (c.Name ?? "anchors", Left: c.TranslatePoint(default, dialog)!.Value.X)).ToList();
                Assert.All(edges, e => Assert.True(Math.Abs(e.Left - edges[0].Left) < 0.5, $"{e.Item1} starts at {e.Left}, the mode box at {edges[0].Left} ({scale}% {language})"));
                dialog.Close();
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                owner.Close();
            }
        });
    }
}
