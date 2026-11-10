using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Panels;

namespace G3MStudio.Tests;

public sealed class UsagesEmptyTests
{
    [Fact]
    public async Task AnUnusedPanelSaysSoUntilASearchFillsIt()
    {
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var panel = new UsagesPanel();
            var window = new Avalonia.Controls.Window { Content = panel };
            window.Show();
            try
            {
                var hint = panel.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Single(t => t.Name == "UsagesEmpty");
                Assert.True(hint.IsVisible);
                Assert.Contains("Find Usages", hint.Text, StringComparison.Ordinal);
                await panel.ShowAsync("obj_a", Task.FromResult<IReadOnlyList<UsageHit>>([]));
                Assert.False(hint.IsVisible);
                panel.Clear();
                Assert.True(hint.IsVisible);
            }
            finally { window.Close(); }
        });
    }
}
