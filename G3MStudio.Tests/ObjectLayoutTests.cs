using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;
using G3MStudio.Rooms;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>The top of the object editor: the links, the preview and the events list sit together with no big blank gap, nothing overlaps, and long names never push anything out.</summary>
public sealed class ObjectLayoutTests
{
    private const string Long = "spr_a_very_long_sprite_name_that_keeps_going_and_going_and_going_and_going_and_going";

    private static readonly string[] Kinds = ["Sprite", "Parent", "MaskSprite"];

    private static Rect Where(Control c, Visual root) => c.TransformToVisual(root) is { } m ? new Rect(default, c.Bounds.Size).TransformToAABB(m) : default;

    [Theory]
    [InlineData(100, "en", false)]
    [InlineData(125, "en", true)]
    [InlineData(150, "en", true)]
    [InlineData(100, "ru", true)]
    [InlineData(125, "es", true)]
    [InlineData(150, "ru", false)]
    [InlineData(150, "es", true)]
    public async Task TheLinksThePreviewAndTheEventsStayTogether(int scale, string language, bool longName)
    {
        var width = 1200 * 100.0 / scale;
        var problems = new List<string>();
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(Corpus.Files(Corpus.Fixtures).First(), cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            Loc.Current.SetLanguage(language);
            var data = workspace.Data;
            var sprite = data.Sprites.First(s => s.Textures.Count > 1);
            if (longName) sprite.Name.Content = Long;
            var obj = data.GameObjects[0];
            obj.Sprite = sprite;
            obj.ParentId = data.GameObjects.FirstOrDefault(o => o != obj);
            var links = new ObjectLinksView(obj, workspace, _ => { }, _ => { }, textures) { Margin = new Thickness(8, 8, 0, 0) };
            var events = new ObjectEventsView(obj, _ => { }, workspace);
            var window = await LayoutAudit.Host(new StackPanel { Spacing = 8, Children = { links, events } }, width);
            try
            {
                var preview = links.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ObjectPreview");
                var player = links.GetVisualDescendants().OfType<SpritePreviewPlayer>().First();
                var rowRects = Kinds.Select(k => Where(links.RowOf(k)!, window)).ToList();
                var (box, view, playerBox) = (Where(preview, window), Where(links, window), Where(player, window));
                var bottom = Math.Max(rowRects.Max(r => r.Bottom), playerBox.Bottom);
                var left = rowRects.Min(r => r.Left);
                if (preview.IsEffectivelyVisible)
                {
                    if (left - box.Right is < 0 or > 24) problems.Add($"preview and rows are {left - box.Right:0} px apart");
                    if (rowRects.Any(r => r.Intersects(box)) || playerBox.Intersects(box)) problems.Add("a row or the player overlaps the preview");
                    bottom = Math.Max(bottom, box.Bottom);
                }

                for (var i = 1; i < rowRects.Count; i++)
                {
                    var gap = rowRects[i].Top - rowRects[i - 1].Bottom;
                    if (gap is < 2 or > 16) problems.Add($"rows {i - 1} and {i} are {gap:0} px apart");
                }

                var under = Where(events, window).Top - bottom;
                if (under is < 4 or > 24) problems.Add($"events start {under:0} px under the links");
                if (rowRects.Any(r => r.Right > view.Right + 1)) problems.Add("a row leaves the view");
                problems.AddRange(LayoutAudit.Overflows(window).Concat(LayoutAudit.Touching(window)).Where(p => p.Contains("Object", StringComparison.Ordinal)));
            }
            finally { window.Close(); }
        });
        Assert.True(problems.Count == 0, $"{scale}% {language}: " + string.Join(Environment.NewLine, problems.Distinct()));
    }
}
