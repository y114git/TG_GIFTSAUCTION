using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The Inspector at the narrowest a panel can be (the window at its minimum width): its buttons and texts are drawn whole and nothing reaches past the panel, in every language and UI scale.</summary>
public sealed class InspectorFitTests
{
    private static readonly string[] Keys = ["scripts:scr_3", "objects:obj_2"];

    [Theory]
    [InlineData("en", 100, 1.0)]
    [InlineData("ru", 100, 1.0)]
    [InlineData("es", 125, 1.25)]
    [InlineData("ja", 150, 1.5)]
    [InlineData("ko", 200, 2.0)]
    [InlineData("zh_cn", 100, 1.0)]
    [InlineData("zh_tw", 200, 2.0)]
    public async Task TheInspectorButtonsAndTextsFitAtTheNarrowestPanel(string language, int uiScale, double renderScale)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Loc.Current.SetLanguage(language);
                window.Width = 840 * uiScale / 100.0; // the window minimum: the docks are at their narrowest
                window.Height = 560 * uiScale / 100.0;
                await window.OpenAsync(TestData.WriteSample(dir, objects: 5, withCode: true));
                window.Workspaces.Settings.Update(x => x with { UiScale = uiScale });
                Avalonia.Headless.HeadlessWindowExtensions.SetRenderScaling(window, renderScale);
                var inspector = window.GetVisualDescendants().OfType<Panels.InspectorPanel>().Single();
                foreach (var key in Keys)
                {
                    window.Explorer.Select(key);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.True(inspector.GetVisualDescendants().OfType<Button>().Any(b => b.IsEffectivelyVisible), $"{key} shows its buttons");
                    AssertFits(inspector);
                }
            }
            finally { window.Workspaces.Close(); window.Close(); }
        });
    }

    private static void AssertFits(Panels.InspectorPanel inspector)
    {
        foreach (var text in inspector.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
        {
            var natural = new TextBlock { Text = text.Text, FontSize = text.FontSize, FontFamily = text.FontFamily, FontWeight = text.FontWeight, TextWrapping = text.TextWrapping };
            natural.Measure(new Size(text.Bounds.Width, double.PositiveInfinity));
            Assert.True(text.Bounds.Width >= natural.DesiredSize.Width - 1 && text.Bounds.Height >= natural.DesiredSize.Height - 1, $"\"{text.Text}\" is drawn in {text.Bounds} and needs {natural.DesiredSize}");
            var right = text.TranslatePoint(new Point(text.Bounds.Width, 0), inspector)!.Value.X;
            Assert.True(right <= inspector.Viewport.Width + 1, $"\"{text.Text}\" ends at {right} in a viewport of {inspector.Viewport.Width}");
        }
    }
}
