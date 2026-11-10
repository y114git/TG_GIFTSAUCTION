using System.Text.Json;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The names the shortcuts list and the palette show for every command.</summary>
public sealed class CommandTitleTests
{
    private static readonly string[] SmallWords = ["a", "an", "and", "or", "of", "to", "the", "in", "on", "for", "with", "by", "at", "from", "as"];

    [Fact]
    public void EveryCommandHasATitleNotAKey()
    {
        var loc = Loc.Current;
        loc.SetLanguage("en");
        foreach (var command in AppCommands.Create(new FakeShell()).All)
        {
            var title = MainWindow.PaletteTitle(command.Id, loc);
            Assert.DoesNotContain("cmd.", title, StringComparison.Ordinal);
            Assert.DoesNotContain("panel.", title, StringComparison.Ordinal);
            Assert.NotEqual(command.Id, title);
        }

        Assert.Equal("Show Explorer", MainWindow.PaletteTitle("view.panel.explorer", loc));
        Assert.StartsWith("Image > ", MainWindow.PaletteTitle("img.nextFrame", loc), StringComparison.Ordinal);
    }

    [Fact]
    public void EnglishCommandTitlesUseTitleCase()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "G3MStudio.Core", "Languages", "lang_en.json");
        var cmd = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(path))).RootElement.GetProperty("cmd");
        var bad = new List<string>();
        void Walk(JsonElement node, string key)
        {
            if (node.ValueKind == JsonValueKind.Object) { foreach (var p in node.EnumerateObject()) Walk(p.Value, key + "." + p.Name); return; }
            if (key.Contains(".newDataPick", StringComparison.Ordinal) || key.Contains(".newDataFailed", StringComparison.Ordinal)) return; // messages, not titles
            var words = node.GetString()!.Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                var letters = words[i].TrimStart('(');
                if (letters.Length == 0 || !char.IsLetter(letters[0])) continue;
                var small = i > 0 && SmallWords.Contains(words[i].TrimEnd('.'));
                if (char.IsUpper(letters[0]) == small) bad.Add($"{key}: {node.GetString()}");
            }
        }

        Walk(cmd, "cmd");
        Assert.True(bad.Count == 0, string.Join("; ", bad.Distinct()));
    }

    [Fact]
    public void SelectionCommandsAreOffUntilAResourceIsSelected()
    {
        var registry = AppCommands.Create(new FakeShell());
        registry.SetContext(AppCommands.WorkspaceOpen, true);
        string[] ids = ["edit.rename", "edit.duplicate", "edit.moveUp", "edit.moveDown", "edit.delete", "edit.copyName"];
        Assert.All(ids, id => Assert.False(registry.IsEnabled(registry.Get(id)), id));
        registry.SetContext(AppCommands.ResourceSelected, true);
        Assert.All(ids, id => Assert.True(registry.IsEnabled(registry.Get(id)), id));
    }

    [Theory]
    [InlineData("Ctrl+D0", "Ctrl+0")]
    [InlineData("Ctrl+D2", "Ctrl+2")]
    [InlineData("Ctrl+OemPlus", "Ctrl++")]
    [InlineData("Ctrl+Down", "Ctrl+Down")]
    public void DigitKeysShowAsDigits(string shortcut, string shown) => Assert.Equal(shown, Shortcuts.Display(shortcut, false));
}
