using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Localization;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed partial class LocalizationTests
{
    private static readonly string[] SourceFolders = ["G3MStudio", "G3MStudio.Core"];

    private static readonly string LanguagesDir = Path.Combine(RepoRoot(), "G3MStudio.Core", "Languages");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "G3MStudio.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("G3MStudio.slnx not found above the test output folder.");
    }

    private static Dictionary<string, Dictionary<string, string>> LoadAll() =>
        Directory.GetFiles(LanguagesDir, "lang_*.json").ToDictionary(
            f => Path.GetFileNameWithoutExtension(f)["lang_".Length..],
            f => { using var s = File.OpenRead(f); return Localizer.Flatten(s); });

    private static bool IsString(string key) => !key.StartsWith("metadata.", StringComparison.Ordinal);

    private static readonly string[] LayerKeyParts = [.. new[] { "depth", "xoffset", "yoffset", "hspeed", "vspeed" }.Select(k => "layerProp." + k), .. new[] { "addInstances", "addAssets", "addBackground", "addTiles" }.Select(k => "layerKind." + k)];

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    [Fact]
    public void EveryLanguageHasExactlyTheEnglishKeys()
    {
        var all = LoadAll();
        var english = all["en"].Keys.Where(IsString).ToHashSet();
        foreach (var (code, strings) in all)
        {
            var keys = strings.Keys.Where(IsString).ToHashSet();
            Assert.True(english.SetEquals(keys),
                $"{code}: missing [{string.Join(", ", english.Except(keys))}] extra [{string.Join(", ", keys.Except(english))}]");
            Assert.True(strings.ContainsKey("metadata.language_name"), $"{code}: metadata.language_name");
        }
    }

    [Fact]
    public void PlaceholdersMatchEnglish()
    {
        var all = LoadAll();
        foreach (var (code, strings) in all)
        {
            foreach (var (key, text) in strings.Where(p => IsString(p.Key)))
            {
                var expected = Placeholder().Matches(all["en"][key]).Select(m => m.Value).Order();
                var actual = Placeholder().Matches(text).Select(m => m.Value).Order();
                Assert.True(expected.SequenceEqual(actual), $"{code}:{key} placeholders differ from English");
            }
        }
    }

    // A key counts as used when its text appears as a literal in a source file or XAML, or is derived from a command id.
    [Fact]
    public void EveryKeyIsUsedAndEveryUsedKeyExists()
    {
        var english = LoadAll()["en"].Keys.Where(IsString).ToHashSet();
        var sources = SourceFolders
            .SelectMany(d => Directory.EnumerateFiles(Path.Combine(RepoRoot(), d), "*.*", SearchOption.AllDirectories))
            .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".axaml", StringComparison.Ordinal)) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText).ToList();
        var text = string.Join("\n", sources);

        var xamlKeys = TrUse().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();
        var commandIds = AppCommands.Create(new FakeShell()).All.Select(c => c.Id).ToHashSet();
        // A literal that is a command id (Registry.Execute("project.sync")) is not a text key.
        var literals = KeyLiteral().Matches(text).Select(m => m.Groups[1].Value).Where(k => !k.EndsWith('.') && !k.EndsWith(".json", StringComparison.Ordinal) && !k.EndsWith(".exe", StringComparison.Ordinal) && !commandIds.Contains(k)).Concat(xamlKeys).ToHashSet();
        var derived = AppCommands.Create(new FakeShell()).All.Select(c => AppCommands.TitleKey(c.Id)).ToHashSet();
        var triggers = Enum.GetNames<Core.Projects.Trigger>().Select(n => "project.trigger." + n);
        var types = ResourceTypes.All.Select(t => "explorer.type." + t.Key).Append("explorer.type." + ExplorerModel.DataGroup);
        var tools = Enum.GetNames<Rooms.RoomTool>().Select(n => "rooms.tool." + n).Concat(TourWindow.Steps.SelectMany(s => new[] { "tour." + s.Key + ".title", "tour." + s.Key + ".text" }));
        var painterKeys = Enum.GetNames<Editor.PaintTool>().Select(n => "painter.tool." + n);
        var searchKeys = G3MStudio.Editor.SearchPanelTexts.Keys;
        var dockKeys = G3MStudio.Docking.DockTexts.Keys.Select(k => "dock." + k);
        var layerKeys = LayerKeyParts.Select(k => "rooms." + k).Concat(Enum.GetNames<Core.Plugins.PluginState>().Select(n => "plugins.state." + n));
        var submenus = AppCommands.Menus.SelectMany(m => m.Ids).Where(id => id.Split('.').Length > 2).Select(id => "menu.sub." + string.Join('.', id.Split('.')[..2])).Concat(ThemeColors.Editable.Select(k => "colors." + k)).Concat(ThemeColors.Groups.Select(g => g.Group)).Concat(ThemeSizes.All.Select(a => "settings.size." + a.Key));
        var used = literals.Concat(derived).Concat(submenus).Concat(triggers).Concat(types).Concat(tools).Concat(painterKeys).Concat(searchKeys).Concat(dockKeys).Concat(layerKeys).ToHashSet();

        Assert.True(english.IsSubsetOf(used), $"unused keys: {string.Join(", ", english.Except(used))}");
        Assert.True(used.All(english.Contains), $"unknown keys: {string.Join(", ", used.Except(english))}");
    }

    [GeneratedRegex("\"((?:app|object|painter|pages|roomOrder|audio|dock|hex|disassembly|tilesets|entryScripts|textureItem|tileSet|warnings|status|menu|cmd|palette|start|explorer|editor|save|notify|prompt|history|rename|code|project|quick|settings|layouts|onboarding|links|tour|git|properties|about|search|usages|help|transfer|mods|sprites|run|update|sounds|rooms|textures|plugins|strings|fonts|paths|timelines|particles|scripts|sequences|inspector|panel|empty)\\.[A-Za-z.]+)\"")]
    private static partial Regex KeyLiteral();

    [GeneratedRegex(@"\{loc:Tr ([A-Za-z.]+)\}")]
    private static partial Regex TrUse();

    [Fact]
    public async Task SwitchingLanguageRelabelsTheWindow()
    {
        await HeadlessApp.Run(() =>
        {
            var loc = Loc.Current;
            loc.SetLanguage("en");
            var window = new MainWindow();
            window.Show();
            try
            {
                List<string> texts() => [.. window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];
                Assert.Contains("Open a game data file to start.", texts());

                loc.SetLanguage("ru");
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("Откройте файл данных игры, чтобы начать.", texts());
                Assert.DoesNotContain("Open a game data file to start.", texts());
            }
            finally
            {
                loc.SetLanguage("en");
                window.Close();
            }
        });
    }

    [Fact]
    public void MissingKeyFallsBackToEnglishThenToTheKey()
    {
        var loc = Localizer.LoadEmbedded();
        loc.SetLanguage("ru");
        Assert.Equal("no.such.key", loc["no.such.key"]);
    }
}
