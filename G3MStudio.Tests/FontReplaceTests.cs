using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class FontReplaceTests
{
    private static readonly string[] SystemFontPaths = [Corpus.Font];

    private static byte[]? SystemFont()
    {
        return SystemFontPaths.FirstOrDefault(File.Exists) is { } path ? File.ReadAllBytes(path) : null;
    }

    [Fact]
    public void OnlyTrueTypeAndOpenTypeBytesAreAccepted()
    {
        Assert.True(FontReplaceCommand.LooksLikeFont([0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
        Assert.False(FontReplaceCommand.LooksLikeFont([.. "MZ-not-a-font-file".Select(c => (byte)c)]));
        Assert.False(FontReplaceCommand.LooksLikeFont([]));
    }

    [Fact]
    public async Task APreparedReplaceGivesTheSameGlyphsAsOneDrawnInsideTheEdit()
    {
        var corpus = Corpus.Folder;
        var font = SystemFont();
        GameMakerData? data = null;
        GameMakerFont? target = null;
        foreach (var file in Corpus.Files(corpus))
        {
            var candidate = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            target = candidate.Fonts.Where(f => f.Texture is not null && f.Glyphs.Count is > 20 and < 200).OrderBy(f => f.Glyphs.Count).FirstOrDefault();
            if (target is not null)
            {
                data = candidate;
                break;
            }

            candidate.Dispose();
        }

        Assert.False(data is null || target is null, "no corpus file has a small font");
        using var open = data;
        data = open;
        var name = target!.Name.Content ?? "";
        var original = target.Texture.TexturePage.TextureData.Image;
        var plain = new FontReplaceCommand(name, font!);
        plain.Apply(data!);
        var expected = target.Glyphs.Select(g => (g.SourceX, g.SourceY, g.SourceWidth, g.Shift, g.Offset)).ToList();
        var drawn = target.Texture.TexturePage.TextureData.Image;
        plain.Revert(data!);
        Assert.Same(original, target.Texture.TexturePage.TextureData.Image);

        var prepared = new FontReplaceCommand(name, font!);
        prepared.Prepare(data!);
        prepared.Apply(data!);
        Assert.Equal(expected, [.. target.Glyphs.Select(g => (g.SourceX, g.SourceY, g.SourceWidth, g.Shift, g.Offset))]);
        Assert.NotSame(original, target.Texture.TexturePage.TextureData.Image);
        Assert.Equal(drawn.Width, target.Texture.TexturePage.TextureData.Image.Width);
        prepared.Revert(data!);
        Assert.Same(original, target.Texture.TexturePage.TextureData.Image);
    }

    [Fact]
    public async Task ReplacingAFontRedrawsItsGlyphsAndUndoPutsTheOldSheetBack()
    {
        var corpus = Corpus.Folder;
        var font = SystemFont();
        GameMakerData? data = null;
        GameMakerFont? target = null;
        foreach (var file in Corpus.Files(corpus))
        {
            var candidate = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            target = candidate.Fonts.Where(f => f.Texture is not null && f.Glyphs.Count is > 20 and < 200).OrderBy(f => f.Glyphs.Count).FirstOrDefault();
            if (target is not null)
            {
                data = candidate;
                break;
            }

            candidate.Dispose();
        }

        Assert.False(data is null || target is null, "no corpus file has a small font");
        using var open = data;
        var name = target!.Name.Content ?? "";
        var before = target.Glyphs.Select(g => (g.SourceX, g.SourceY, g.SourceWidth, g.Shift)).ToList();
        var size = target.EmSize;

        var command = new FontReplaceCommand(name, font!);
        command.Apply(data!);
        var after = target.Glyphs.Select(g => (g.SourceX, g.SourceY, g.SourceWidth, g.Shift)).ToList();
        Assert.NotEqual(before, after);
        Assert.All(target.Glyphs, g => Assert.True(g.SourceX + g.SourceWidth <= target.Texture.SourceWidth && g.SourceY + g.SourceHeight <= target.Texture.SourceHeight));
        Assert.Equal(size, target.EmSize);

        command.Revert(data!);
        Assert.Equal(before, [.. target.Glyphs.Select(g => (g.SourceX, g.SourceY, g.SourceWidth, g.Shift))]);
        command.Apply(data!); // redo gives the same glyphs without drawing again
        Assert.Equal(after, [.. target.Glyphs.Select(g => (g.SourceX, g.SourceY, g.SourceWidth, g.Shift))]);
        Assert.Equal(name, FontReplaceCommand.FromArgs(command.Args).Args["font"]);
    }

    [Fact]
    public async Task AFontWithASpreadGetsPaddedGlyphsAndANegativeOffsetLikeASheetFromTheIde()
    {
        var corpus = Corpus.Folder;
        var font = SystemFont();
        GameMakerData? data = null;
        GameMakerFont? target = null;
        foreach (var file in Corpus.Files(corpus))
        {
            var candidate = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            target = candidate.Fonts.Where(f => f.Texture is not null && f.Glyphs.Count is > 20 and < 200).OrderBy(f => f.Glyphs.Count).FirstOrDefault();
            if (target is not null)
            {
                data = candidate;
                break;
            }

            candidate.Dispose();
        }

        Assert.False(data is null || target is null, "no corpus file has a small font");
        using var open = data;
        target!.SDFSpread = 2;
        new FontReplaceCommand(target.Name.Content ?? "", font!).Apply(data!);
        Assert.All(target.Glyphs, g => Assert.Equal(-2, g.Offset));
        Assert.All(target.Glyphs, g => Assert.True(g.SourceWidth >= 5 && g.SourceHeight >= 5)); // at least a pixel with the spread on each side
        if (Environment.GetEnvironmentVariable("G3MSTUDIO_SHOTS") is { Length: > 0 } shots)
        {
            using var worker = new G3MLib.DataFile.Util.TextureWorker();
            using var page = worker.GetEmbeddedTexture(target.Texture.TexturePage);
            using var channels = new ImageMagick.MagickImageCollection(page.Separate(ImageMagick.Channels.Alpha)); // the distance is in the alpha: show it as gray
            var flat = channels[0];
            await flat.WriteAsync(Path.Combine(shots, "font-sdf-sheet.png"), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task EveryGlyphCellHasTheSameHeightAndLettersShareOneBaseline()
    {
        var corpus = Corpus.Folder;
        var font = SystemFont();
        GameMakerData? data = null;
        GameMakerFont? target = null;
        foreach (var file in Corpus.Files(corpus))
        {
            var candidate = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            target = candidate.Fonts.Where(f => f.Texture is not null && f.Glyphs.Count is > 20 and < 200 && f.Glyphs.Any(g => g.Character == 'H') && f.Glyphs.Any(g => g.Character == 'x')).OrderBy(f => f.Glyphs.Count).FirstOrDefault();
            if (target is not null)
            {
                data = candidate;
                break;
            }

            candidate.Dispose();
        }

        Assert.False(data is null || target is null, "no corpus file has a small font with H and x");
        using var open = data;
        target!.SDFSpread = 0;
        new FontReplaceCommand(target.Name.Content ?? "", font!).Apply(data!);
        Assert.Single(target.Glyphs.Select(g => (int)g.SourceHeight).Distinct()); // one cell height, as in every font the IDE makes
        using var worker = new G3MLib.DataFile.Util.TextureWorker();
        using var page = worker.GetEmbeddedTexture(target.Texture.TexturePage);
        using var pixels = page.GetPixels();
        int InkBottom(char c)
        {
            var g = target.Glyphs.First(x => x.Character == c);
            for (var row = g.SourceHeight - 1; row >= 0; row--)
                for (var col = 0; col < g.SourceWidth; col++)
                    if (pixels.GetPixel(target.Texture.SourceX + g.SourceX + col, target.Texture.SourceY + g.SourceY + row)!.ToArray()[3] > 0) return row;
            return -1;
        }

        int InkTop(char c)
        {
            var g = target.Glyphs.First(x => x.Character == c);
            for (var row = 0; row < g.SourceHeight; row++)
                for (var col = 0; col < g.SourceWidth; col++)
                    if (pixels.GetPixel(target.Texture.SourceX + g.SourceX + col, target.Texture.SourceY + g.SourceY + row)!.ToArray()[3] > 0) return row;
            return -1;
        }

        Assert.InRange(Math.Abs(InkBottom('H') - InkBottom('x')), 0, 2); // the same baseline
        Assert.True(InkTop('x') > InkTop('H')); // and a lower letter starts lower, not at the top of its cell
    }

    [Fact]
    public async Task TheFontEditorReplacesAFontFromAFileAsOneUndoableStep()
    {
        var corpus = Corpus.Folder;
        var fontPath = SystemFontPaths.FirstOrDefault(File.Exists);
        string? file = null;
        string? name = null;
        foreach (var candidate in Corpus.Files(corpus))
        {
            using var probe = await GameMakerIO.ReadAsync(candidate, cancellationToken: TestContext.Current.CancellationToken);
            if (probe.Fonts.Where(f => f.Texture is not null && f.Glyphs.Count is > 20 and < 200).OrderBy(f => f.Glyphs.Count).FirstOrDefault() is { } found)
            {
                (file, name) = (candidate, found.Name.Content);
                break;
            }
        }

        Assert.False(file is null || name is null, "no corpus file has a small font");
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Width = 1500;
                window.Height = 950;
                await window.OpenAsync(file!);
                var workspace = window.Workspaces.Current!;
                var before = workspace.Data.Fonts.First(f => f.Name.Content == name).Glyphs.Sum(g => g.SourceX + g.SourceWidth);
                window.Confirmer = (_, _) => Task.FromResult(true);
                await window.ReplaceFontAsync(name!, fontPath);
                Assert.True(workspace.History.IsDirty, string.Join("; ", window.Notifications.Active.Select(n => n.Text)));
                var after = workspace.Data.Fonts.First(f => f.Name.Content == name).Glyphs.Sum(g => g.SourceX + g.SourceWidth);
                Assert.NotEqual(before, after);
                var shots = Environment.GetEnvironmentVariable("G3MSTUDIO_SHOTS");
                if (!string.IsNullOrEmpty(shots))
                {
                    window.Docking.OpenTab("resource", G3MStudio.Core.Workspace.ResourceTypes.KeyOf("fonts", name!), false);
                    for (var i = 0; i < 6; i++)
                    {
                        await Task.Delay(100);
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    }

                    ScreenshotTests.Shoot(window, Path.Combine(shots, "02-fonts-replaced.png"));
                }

                workspace.Undo();
                Assert.Equal(before, workspace.Data.Fonts.First(f => f.Name.Content == name).Glyphs.Sum(g => g.SourceX + g.SourceWidth));
            }
            finally { window.Close(); }
        });
    }
}
