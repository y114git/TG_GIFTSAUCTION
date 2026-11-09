using Avalonia.Media;
using G3MStudio.Fonts;

namespace G3MStudio.Tests;

public sealed class UserFontsTests
{
    /// <summary>The small open-license font of the fixtures (VT323, SIL OFL 1.1).</summary>
    private static string FindFontFile() => Corpus.Font;

    [Fact]
    public async Task AFontFileIsCopiedRegisteredAppliedAndRemoved()
    {
        var source = FindFontFile();
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
