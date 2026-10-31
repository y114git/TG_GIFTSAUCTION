using Avalonia.Media;
using G3MStudio.Fonts;

namespace G3MStudio.Tests;

public sealed class UserFontsTests
{
    /// <summary>Any ordinary font file of the machine; the tests skip when there is none.</summary>
    private static string? FindFontFile()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/dejavu/DejaVuSans.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/System/Library/Fonts/Supplemental/Courier New.ttf",
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    [Fact]
    public async Task AFontFileIsCopiedRegisteredAppliedAndRemoved()
    {
        var source = FindFontFile();
        Assert.SkipUnless(source is not null, "no font file on this machine");
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Registry.Execute("settings.open");
                var page = window.OpenSettings!;
                page.AddFont(source!);

                var family = Assert.Single(window.Fonts.Families);
                Assert.True(File.Exists(Path.Combine(dir.File("user"), "fonts", Path.GetFileName(source!))));
                Assert.Equal(family, window.Workspaces.Settings.Current.FontFamily);
                Assert.NotNull(window.FontFamily.Key);
                Assert.Equal(family, window.FontFamily.Name);
                Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(window.FontFamily), out var glyphs));
                Assert.Equal(family, glyphs.FamilyName);

                // A file that is no font adds nothing and leaves no copy.
                var notFont = dir.File("notes.ttf");
                File.WriteAllText(notFont, "this is not a font");
                page.AddFont(notFont);
                Assert.Single(window.Fonts.Families);
                Assert.False(File.Exists(Path.Combine(dir.File("user"), "fonts", "notes.ttf")));

                window.Fonts.Remove(family);
                Assert.Empty(window.Fonts.Families);
                Assert.False(File.Exists(Path.Combine(dir.File("user"), "fonts", Path.GetFileName(source!))));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task FontsInTheFolderAreRegisteredAtStart()
    {
        var source = FindFontFile();
        Assert.SkipUnless(source is not null, "no font file on this machine");
        using var dir = new TempDir();
        var folder = Directory.CreateDirectory(dir.File("fonts")).FullName;
        File.Copy(source!, Path.Combine(folder, Path.GetFileName(source!)));
        await HeadlessApp.Run(() =>
        {
            var fonts = new UserFonts(folder);
            fonts.LoadAll();
            var family = Assert.Single(fonts.Families);
            Assert.Equal(family, fonts.Resolve(family)!.Name);
            Assert.Null(fonts.Resolve("No Such Family"));
        });
    }
}
