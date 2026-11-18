using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Localization;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The names the object, sound, font and tile set editors show come from the data model, so they must come from the language files too.</summary>
public sealed class FieldTranslationTests
{
    // Spelled the same in every language the files cover (letters, a technical name, a loan word).
    private static readonly string[] SameInEveryLanguage = ["X", "Y", "ID", "Sprite", "Visible", "Kerning", "Tpag", "Xor"];

    // One language that spells a word like English.
    private static readonly string[] SameInOneLanguage = ["es properties.choice.Manual", "es properties.choice.Auto", "es properties.choice.Automatic"];

    private static Dictionary<string, Dictionary<string, string>>? _all;

    private static Dictionary<string, Dictionary<string, string>> All() => _all ??= Directory.GetFiles(Path.Combine(Root(), "G3MStudio.Core", "Languages"), "lang_*.json").ToDictionary(
        f => Path.GetFileNameWithoutExtension(f)["lang_".Length..],
        f => { using var s = File.OpenRead(f); return Localizer.Flatten(s); });

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "G3MStudio.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("G3MStudio.slnx not found above the test output folder.");
    }

    /// <summary>The keys that still hold the English text in a language, one "language key" per line. A translation step removes its lines; the test fails for a key that is English and not listed, and for a listed key that is translated.</summary>
    private static HashSet<string> Untranslated() => [.. File.ReadAllLines(Path.Combine(Root(), "G3MStudio.Tests", "UntranslatedKeys.txt")).Where(l => l.Length > 0 && !l.StartsWith('#'))];

    [Fact]
    public void EveryFieldAndChoiceOfEveryResourceHasATextInEveryLanguage()
    {
        var (fields, choices, flags) = ModelKeys.Model();
        var listed = Untranslated();
        var checkedKeys = new HashSet<string>();
        var missing = new List<string>();
        foreach (var (code, strings) in All())
        {
            foreach (var field in fields)
                Check(missing, checkedKeys, listed, code, strings, "properties.label." + field, field);
            foreach (var choice in choices.Concat(flags))
                Check(missing, checkedKeys, listed, code, strings, "properties.choice." + choice, choice);
            Check(missing, checkedKeys, listed, code, strings, "properties.special", "properties.special");
            Check(missing, checkedKeys, listed, code, strings, "settings.shortcutNum", "settings.shortcutNum");
        }

        var problems = missing.Concat(Stale(listed, checkedKeys, "events.")).ToList();
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    // A listed key that no test checks any more (a renamed field, a typo).
    private static IEnumerable<string> Stale(HashSet<string> listed, HashSet<string> checkedKeys, params string[] otherGroups) =>
        listed.Where(l => !checkedKeys.Contains(l) && !otherGroups.Any(g => l[(l.IndexOf(' ', StringComparison.Ordinal) + 1)..].StartsWith(g, StringComparison.Ordinal))).Select(l => $"UntranslatedKeys.txt lists {l}, which no test checks");

    private static void Check(List<string> missing, HashSet<string> checkedKeys, HashSet<string> listed, string code, Dictionary<string, string> strings, string key, string english)
    {
        checkedKeys.Add($"{code} {key}");
        if (!strings.TryGetValue(key, out var text)) missing.Add($"{code}: {key} is missing");
        else if (code != "en")
        {
            var same = text == All()["en"][key];
            var isListed = listed.Contains($"{code} {key}");
            if (same && !isListed && !SameInEveryLanguage.Contains(english) && !SameInOneLanguage.Contains($"{code} {key}")) missing.Add($"{code}: {key} is still English ({text})");
            else if (!same && isListed) missing.Add($"{code}: {key} is translated, remove it from UntranslatedKeys.txt");
        }
    }

    [Fact]
    public void EveryEventTypeAndSubtypeHasATextInEveryLanguage()
    {
        var listed = Untranslated();
        var checkedKeys = new HashSet<string>();
        var missing = new List<string>();
        foreach (var (code, strings) in All())
        {
            foreach (var type in EventNames.Types.Select(t => t.ToString()).Append("Trigger"))
                Check(missing, checkedKeys, listed, code, strings, "events.type." + type, type);
            foreach (var name in ModelKeys.Subtypes())
                Check(missing, checkedKeys, listed, code, strings, "events.sub." + name, name);
            Check(missing, checkedKeys, listed, code, strings, "events.alarm", "alarm");
        }

        var problems = missing.Concat(Stale(listed, checkedKeys, "properties.", "settings.")).ToList();
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    private static readonly GameMakerData Data = new();

    [Fact]
    public void TheGridAndTheEventListUseTheLanguageOfTheWindow()
    {
        var ru = Localizer.LoadEmbedded();
        ru.SetLanguage("ru");
        Assert.Equal("Создание", EventNames.Label((uint)EventType.Create, 0, null, ru));
        Assert.Equal("Шаг: Начало шага", EventNames.Label((uint)EventType.Step, (uint)EventSubtypeStep.BeginStep, Data, ru));
        Assert.Equal("Будильник 3", EventNames.Label((uint)EventType.Alarm, 3, Data, ru));
        Assert.Equal("Alarm 3", EventNames.Label((uint)EventType.Alarm, 3, Data));

        try
        {
            Loc.Current.SetLanguage("ja");
            Assert.Equal("名前", PropertyGridTab.Label("Name"));
            Assert.Equal("埋め込み", PropertyGridTab.FlagLabel("IsEmbedded"));
            Assert.Equal("円", PropertyGridTab.ChoiceLabel("Circle"));
        }
        finally { Loc.Current.SetLanguage("en"); }
    }

    [Fact]
    public async Task AnOpenObjectTabTakesANewLanguageAtOnce()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                Assert.NotNull(window.Docking.OpenTab("resource", "objects:obj_1", false));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal("Add event...", window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "AddEventMenu").Content);

                Loc.Current.SetLanguage("ru");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal(Loc.Current["editors.object.addEventMenu"], window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "AddEventMenu").Content);
                Assert.NotEqual("Add event...", window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "AddEventMenu").Content);
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ARoomTabKeepsItsCanvasViewWhenTheLanguageChanges()
    {
        using var dir = new TempDir();
        string path;
        using (var data = CopyResourcesTests.Source())
        {
            new EditHistory(data).Execute(ResourceTemplates.New(data, "rooms", "rm_a"));
            path = TestData.Write(dir.File("data.win"), data);
        }

        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("resource", "rooms:rm_a", false);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var before = window.GetVisualDescendants().OfType<RoomPreview>().Single();
                await before.Ready;
                before.View.Zoom = 2.5;

                Loc.Current.SetLanguage("ru");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var after = window.GetVisualDescendants().OfType<RoomPreview>().Single();
                Assert.NotSame(before, after);
                for (var i = 0; i < 100 && Math.Abs(after.View.Zoom - 2.5) > 0.001; i++) await Task.Delay(20, TestContext.Current.CancellationToken);
                Assert.Equal(2.5, after.View.Zoom, 3);
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ASpriteTabTakesTheNewLanguageAndKeepsItsZoomAndFrame()
    {
        using var dir = new TempDir();
        string path;
        using (var data = CopyResourcesTests.Source())
        {
            new EditHistory(data).Execute(ResourceTemplates.New(data, "sprites", "spr_new_qa"));
            path = TestData.Write(dir.File("data.win"), data);
        }

        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("resource", "sprites:spr_new_qa", false);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var before = window.GetVisualDescendants().OfType<SpriteEditorView>().Single();
                await before.Frames.Ready;
                before.Tools.Canvas.Zoom = 5;

                Loc.Current.SetLanguage("ru");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var after = window.GetVisualDescendants().OfType<SpriteEditorView>().Single();
                Assert.NotSame(before, after);
                Assert.Equal(Loc.Current["sprites.addFrame"], after.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SpriteFrameAdd").Content);
                for (var i = 0; i < 100 && after.Tools.Canvas.Zoom != 5; i++) await Task.Delay(20, TestContext.Current.CancellationToken);
                Assert.Equal(5, after.Tools.Canvas.Zoom);
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
    }
}
