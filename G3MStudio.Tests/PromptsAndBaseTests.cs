using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MStudio.Core.ModPack;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class PromptsAndBaseTests
{
    private static T Named<T>(MainWindow window, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    }

    // The source data of the copy tests, plus two timelines; the first has one moment at step 3 that runs a script's code.
    private static GameMakerData WithTimelines()
    {
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(ResourceTemplates.New(data, "timelines", "tl_a"));
        history.Execute(ResourceTemplates.New(data, "timelines", "tl_b"));
        history.Execute(new TimelineMomentCommand("tl_a", TimelineMomentCommand.Add, -1, 3));
        history.Execute(new TimelineActionCommand("tl_a", TimelineActionCommand.Add, 0, -1, "gml_Script_scr_use"));
        return data;
    }

    [Fact]
    public void EveryPromptStartsAsAskAndTheChoiceRoundTrips()
    {
        var s = new AppSettings();
        Assert.All(Prompts.All, p => Assert.Equal(PromptChoice.Ask, Prompts.Get(s, p.Id)));
        s = Prompts.Set(s, Prompts.RevertToSaved, PromptChoice.No);
        s = Prompts.Set(s, Prompts.CloseUnsaved, PromptChoice.Yes);
        s = Prompts.Set(s, Prompts.RiskyAction, PromptChoice.No);
        Assert.Equal(PromptChoice.No, Prompts.Get(s, Prompts.RevertToSaved));
        Assert.False(s.AskOnClose); // the old setting is the same store
        Assert.False(s.AskRiskyActions);
        // A prompt that only picks between two actions cannot be answered No.
        Assert.Equal(s, Prompts.Set(s, Prompts.TimelineCopyCode, PromptChoice.No));
        var reset = Prompts.ResetAll(s);
        Assert.All(Prompts.All, p => Assert.Equal(PromptChoice.Ask, Prompts.Get(reset, p.Id)));
        Assert.Empty(reset.Prompts);
        Assert.True(reset.AskOnClose && reset.AskRiskyActions && !reset.AllowUnsafeSilently);
    }

    [Fact]
    public void DeletePromptsNeverRememberNoAndReadAStoredNoAsAsk()
    {
        var s = new AppSettings();
        Assert.Equal(s, Prompts.Set(s, Prompts.DeleteResource, PromptChoice.No));
        Assert.Equal(PromptChoice.Yes, Prompts.Get(Prompts.Set(s, Prompts.DeleteMany, PromptChoice.Yes), Prompts.DeleteMany));
        var old = s with { Prompts = new Dictionary<string, string> { [Prompts.DeleteResource] = "No", [Prompts.DeleteMany] = "No" } };
        Assert.Equal(PromptChoice.Ask, Prompts.Get(old, Prompts.DeleteResource));
        Assert.Equal(PromptChoice.Ask, Prompts.Get(old, Prompts.DeleteMany));
    }

    [Fact]
    public void ASecurityPromptCannotBeSilencedNotEvenByAHandEditedFile()
    {
        var s = Prompts.Set(new AppSettings(), "trustRunFile", PromptChoice.Yes);
        Assert.Empty(s.Prompts);
        using var dir = new TempDir();
        var user = new UserData(dir.File("user"));
        File.WriteAllText(user.Combine("settings.json"), "{\"prompts\":{\"trustRunFile\":\"Yes\",\"deleteResource\":\"Yes\",\"timelineCopyCode\":\"No\"}}");
        using var store = new SettingsStore(user);
        Assert.Equal(["deleteResource"], store.Current.Prompts.Keys);
    }

    private static string GameName => OperatingSystem.IsWindows() ? "Game.exe" : "Game";

    [Fact]
    public void RunOptionsInStudiosFolderNeedNoApprovalAndALegacyFileDoes()
    {
        using var dir = new TempDir();
        var user = new UserData(dir.File("user"));
        var data = dir.File("data.win");
        File.WriteAllText(dir.File(GameName), "");
        var settings = new AppSettings();
        var written = RunPlan.SaveProject(settings, user, data, new RunProfile("", "", "-own", false));
        Assert.StartsWith(user.Root, written, StringComparison.Ordinal);
        Assert.False(File.Exists(RunPlan.ProjectFile(data)));
        Assert.Null(RunPlan.Unapproved(settings, data, user));
        Assert.Equal("-own", RunPlan.Resolve(settings, data, GameName, "work.tmp", null, user).Spec?.Arguments);

        // A file next to the data file is used only after approval, and migration moves it into Studio's folder.
        var other = dir.File("other.win");
        File.WriteAllText(RunPlan.ProjectFile(other), "{\"Runner\":\"\",\"Arguments\":\"-legacy\"}");
        Assert.NotNull(RunPlan.Unapproved(settings, other, user));
        Assert.NotEqual("-legacy", RunPlan.Resolve(settings, other, GameName, "work.tmp", null, user).Spec?.Arguments);
        Assert.False(RunPlan.Migrate(settings, user, other)); // not approved yet: nothing is copied and nothing becomes trusted
        Assert.NotNull(RunPlan.Unapproved(settings, other, user));
        settings = settings with { TrustedRunFiles = [RunPlan.ProjectHash(other)!] };
        Assert.True(RunPlan.Migrate(settings, user, other));
        Assert.Null(RunPlan.Unapproved(settings, other, user));
        Assert.Equal("-legacy", RunPlan.Resolve(settings, other, GameName, "work.tmp", null, user).Spec?.Arguments);
        Assert.False(RunPlan.Migrate(settings, user, dir.File("none.win")));

        var beside = new AppSettings { RunOptionsLocation = RunOptionsLocation.BesideDataFile };
        Assert.Equal(RunPlan.ProjectFile(data), RunPlan.SaveProject(beside, user, data, new RunProfile("", "", "-beside", false)));
        Assert.NotNull(RunPlan.Unapproved(beside, data, user));
    }

    [Fact]
    public void TwoModBaseInstancesOverOneFolderKeepEveryIndexEntry()
    {
        using var dir = new TempDir();
        var files = Enumerable.Range(0, 8).Select(i => dir.File($"f{i}.win")).ToArray();
        for (var i = 0; i < files.Length; i++) File.WriteAllBytes(files[i], [.. Enumerable.Range(0, 3000).Select(n => (byte)(n * (i + 3) + i))]);
        var caches = new[] { new ModBaseCache(new UserData(dir.File("user"))), new ModBaseCache(new UserData(dir.File("user"))) };
        Parallel.For(0, files.Length, i => caches[i % 2].Remember(files[i], files[i], 1 << 20));
        Assert.All(files, f => Assert.NotNull(caches[0].Find(f)));
    }

    [Fact]
    public void TheModBaseKeepsTheFirstCopyChecksItAndEvictsTheOldest()
    {
        using var dir = new TempDir();
        var cache = new ModBaseCache(new UserData(dir.File("user")));
        var one = dir.File("one.win");
        var two = dir.File("two.win");
        File.WriteAllBytes(one, [.. Enumerable.Range(0, 5000).Select(i => (byte)(i * 7))]);
        File.WriteAllBytes(two, [.. Enumerable.Range(0, 5000).Select(i => (byte)(i * 13 + 1))]);
        var first = cache.Remember(one, one, 1 << 20);
        Assert.NotNull(first);
        File.WriteAllBytes(one, [1, 2, 3]); // the file changes later (a save): the first copy stays
        Assert.Equal(first, cache.Remember(one, one, 1 << 20));
        var folder = dir.File("out");
        Directory.CreateDirectory(folder);
        var back = cache.TryExtract(first, folder);
        Assert.Equal(first.Hash, ModPackBuilder.HashFile(back!));
        Assert.Equal(5000, new FileInfo(back!).Length);

        // The same path remembered again on request replaces the copy.
        Assert.NotEqual(first.Hash, cache.Remember(one, one, 1 << 20, true)!.Hash);

        // A cap smaller than one copy keeps nothing; a cap for one copy drops the older.
        Assert.Null(cache.Remember(two, two, 10));
        var big = cache.Remember(two, two, 1 << 20)!;
        Assert.NotNull(cache.Find(two));
        cache.Trim(new FileInfo(Directory.GetFiles(dir.File("user/modbase"), big.Hash + ".gz")[0]).Length);
        Assert.Null(cache.Find(one));
        Assert.NotNull(cache.Find(two));

        // A damaged copy is detected and dropped.
        File.WriteAllBytes(Directory.GetFiles(dir.File("user/modbase"), big.Hash + ".gz")[0], [0, 1, 2]);
        Assert.Null(cache.TryExtract(big, folder));
        Assert.Null(cache.Find(two));
        cache.Clear();
        Assert.Equal(0, cache.TotalBytes);
    }

    [Fact]
    public void CopyingAMomentWithItsCodeMakesNewEntriesInOneUndoStep()
    {
        using var data = WithTimelines();
        var history = new EditHistory(data);
        var codeBefore = data.Code.Count;
        var source = data.Timelines.First(t => t.Name.Content == "tl_a").Moments[0].Event[0].CodeId;
        history.Execute(new TimelineMomentCopyCommand("tl_a", 0, "tl_b", false, true));
        var copy = data.Timelines.First(t => t.Name.Content == "tl_b").Moments[0].Event[0].CodeId;
        Assert.NotNull(copy);
        Assert.NotSame(source, copy);
        Assert.Equal("gml_Timeline_tl_b_3", copy.Name.Content);
        Assert.Equal("return 7;", CodeText.Decompile(data, "gml_Timeline_tl_b_3").Trim());
        Assert.Equal(codeBefore + 1, data.Code.Count);
        history.Undo();
        Assert.Empty(data.Timelines.First(t => t.Name.Content == "tl_b").Moments);
        Assert.Equal(codeBefore, data.Code.Count);

        // Sharing keeps the one entry.
        history.Execute(new TimelineMomentCopyCommand("tl_a", 0, "tl_b", false));
        Assert.Same(source, data.Timelines.First(t => t.Name.Content == "tl_b").Moments[0].Event[0].CodeId);
        Assert.Equal(codeBefore, data.Code.Count);
    }

    [Fact]
    public async Task TheWindowRemembersAnAnswerPerOptionAndAsksNoMore()
    {
        using var dir = new TempDir();
        string path;
        using (var data = WithTimelines()) path = TestData.Write(dir.File("tl.win"), data);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                var asked = 0;
                window.MomentCopyAsker = () =>
                {
                    asked++;
                    return Task.FromResult<(bool, bool)?>((true, true)); // copy the code, and remember it
                };
                await window.CopyTimelineMomentAsync(workspace, "tl_a", 0, "tl_b", false);
                Assert.Equal(1, asked);
                Assert.Equal(PromptChoice.Yes, Prompts.Get(window.Workspaces.Settings.Current, Prompts.TimelineCopyCode));
                await window.CopyTimelineMomentAsync(workspace, "tl_a", 0, "tl_b", false);
                Assert.Equal(1, asked); // remembered
                var tlb = workspace.Data.Timelines.First(t => t.Name.Content == "tl_b");
                Assert.Equal(["gml_Timeline_tl_b_3", "gml_Timeline_tl_b_3_2"], tlb.Moments.Select(m => m.Event[0].CodeId!.Name.Content));

                // Turning the prompt back on asks again; the other option answers by itself.
                window.Workspaces.Settings.Update(s => Prompts.Set(s, Prompts.TimelineCopyCode, PromptChoice.Ask));
                await window.CopyTimelineMomentAsync(workspace, "tl_a", 0, "tl_b", false);
                Assert.Equal(2, asked);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ConfirmUsesTheRememberedAnswerAndSettingsListsAndResetsEveryPrompt()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var asked = 0;
                window.Confirmer = (_, _) =>
                {
                    asked++;
                    return Task.FromResult(true);
                };
                Assert.True(window.Confirm(Prompts.RevertToSaved, "t", "m").Result);
                Assert.Equal(1, asked);
                window.Workspaces.Settings.Update(s => Prompts.Set(s, Prompts.RevertToSaved, PromptChoice.No));
                Assert.False(window.Confirm(Prompts.RevertToSaved, "t", "m").Result);
                Assert.Equal(1, asked);

                window.Registry.Execute("settings.open");
                var box = Named<ComboBox>(window, "Prompt_" + Prompts.RevertToSaved);
                Assert.Equal(2, box.SelectedIndex); // always no
                foreach (var def in Prompts.All) Assert.NotNull(window.GetVisualDescendants().OfType<Control>().SingleOrDefault(c => c.Name == "Prompt_" + def.Id));
                box.SelectedIndex = 0;
                Assert.Equal(PromptChoice.Ask, Prompts.Get(window.Workspaces.Settings.Current, Prompts.RevertToSaved));

                window.Workspaces.Settings.Update(s => Prompts.Set(Prompts.Set(s, Prompts.DeleteMany, PromptChoice.Yes), Prompts.TempRunWarning, PromptChoice.Yes));
                Named<Button>(window, "ResetPrompts").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.All(Prompts.All, p => Assert.Equal(PromptChoice.Ask, Prompts.Get(window.Workspaces.Settings.Current, p.Id)));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ExportModPackUsesTheRememberedOriginalAndAsksWhenItIsLost()
    {
        using var dir = new TempDir();
        string original;
        using (var data = CopyResourcesTests.Source()) original = TestData.Write(dir.File("game.win"), data);
        var pack = dir.File("m.g3mpack");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            window.Workspaces.Settings.Update(x => x with { ModBaseEnabled = true });
            var asked = 0;
            window.Confirmer = (_, _) => Task.FromResult(++asked > 0); // the file changed since it was remembered: keep the remembered original
            try
            {
                await window.OpenAsync(original); // first open: the original is remembered
                await window.ModBaseTask;
                var cache = new ModBaseCache(window.Workspaces.UserData);
                var entry = cache.Find(original)!;
                using (var modded = CopyResourcesTests.Source())
                {
                    new EditHistory(modded).Execute(new EditCodeCommand("gml_Script_scr_use", "return 9;"));
                    TestData.Write(original, modded); // the file on disk becomes the modded one
                }

                await window.OpenAsync(original);
                Assert.NotEqual(entry.Hash, ModPackBuilder.HashFile(original)); // the file on disk is the modded one now

                var picks = new Queue<string>([pack]);
                window.FilePicker = (_, _) => Task.FromResult<string?>(picks.Dequeue()); // only the output: the base is not asked
                window.PackInfoAsker = (_, name) => Task.FromResult<PackInfo?>(new PackInfo(name, "1.0.0", ["me"]));
                await window.ExportModPackAsync();
                Assert.Equal(entry.Hash, ModPackFile.Read(pack).Manifest.TargetHash);
                Assert.Single(ModPackFile.Read(pack).Entries);
                Assert.Equal(1, asked);

                // The copy is damaged: the window asks for the original, and a wrong file is refused by hash.
                File.WriteAllBytes(Directory.GetFiles(window.Workspaces.UserData.Combine("modbase"), "*.gz")[0], [1]);
                var wrong = dir.File("wrong.win");
                File.WriteAllBytes(wrong, [1, 2, 3]);
                picks = new Queue<string>([wrong]);
                File.Delete(pack);
                await window.ExportModPackAsync();
                Assert.False(File.Exists(pack));
                Assert.Empty(picks);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ModZipAndPatchUseTheRememberedOriginal()
    {
        using var dir = new TempDir();
        string original;
        using (var data = CopyResourcesTests.Source()) original = TestData.Write(dir.File("game.win"), data);
        var (zip, patch) = (dir.File("m.zip"), dir.File("m.g3mpatch"));
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            window.Workspaces.Settings.Update(x => x with { ModBaseEnabled = true });
            var asked = 0;
            window.Confirmer = (_, _) => Task.FromResult(++asked > 0); // the file changed since it was remembered: keep the remembered original
            try
            {
                await window.OpenAsync(original); // first open: the original is remembered
                await window.ModBaseTask;
                using (var modded = CopyResourcesTests.Source())
                {
                    new EditHistory(modded).Execute(new EditCodeCommand("gml_Script_scr_use", "return 9;"));
                    TestData.Write(original, modded);
                }

                await window.OpenAsync(original);
                var picks = new Queue<string>();
                window.FilePicker = (_, _) => Task.FromResult<string?>(picks.Dequeue()); // only outputs: the base is not asked
                picks.Enqueue(zip);
                await window.ExportModAsync();
                picks.Enqueue(patch);
                await window.CreatePatchAsync();
                Assert.Empty(picks);
                Assert.True(File.Exists(zip) && File.Exists(patch), string.Join("; ", window.Notifications.Active.Select(n => n.Text)));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
