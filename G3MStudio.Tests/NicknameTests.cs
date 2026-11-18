using Avalonia.Threading;
using G3MStudio.Core.Navigation;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>Nicknames: shown in place of the name, matched by search, kept per data file, following renames and deletes. The real name stays the identity.</summary>
public sealed class NicknameTests
{
    [Fact]
    public void ALabelIsTheNicknameElseTheNameAndACodeEntryShowsItsOwnersNickname()
    {
        var nicknames = new Nicknames();
        Assert.True(nicknames.Set("objects:obj_hero", "Hero"));
        Assert.Equal("Hero", nicknames.Label("objects", "obj_hero"));
        Assert.Equal("obj_boss", nicknames.Label("objects", "obj_boss"));
        Assert.Equal("Hero", nicknames.Label("objects:obj_hero"));
        Assert.Equal("gml_Object_Hero_Create_0", nicknames.CodeLabel("gml_Object_obj_hero_Create_0"));
        Assert.Equal("gml_Object_obj_hero2_Create_0", nicknames.CodeLabel("gml_Object_obj_hero2_Create_0")); // another owner whose name starts the same
        Assert.Equal("Hero (obj_hero)", nicknames.Both("objects", "obj_hero"));
        Assert.False(nicknames.Set("objects:obj_hero", "Hero")); // nothing changed
        Assert.True(nicknames.Set("objects:obj_hero", "  ")); // empty clears
        Assert.Equal("obj_hero", nicknames.Label("objects", "obj_hero"));
        Assert.False(nicknames.Any);
    }

    [Fact]
    public void ANicknameGivenToACodeEntryItselfIsItsLabel()
    {
        var nicknames = new Nicknames();
        Assert.True(nicknames.Set("scripts:scr_a", "Util"));
        Assert.Equal("gml_Script_Util", nicknames.CodeLabel("gml_Script_scr_a"));
        Assert.True(nicknames.Set("code:gml_Script_scr_a", "Util code"));
        Assert.Equal("Util code", nicknames.CodeLabel("gml_Script_scr_a"));
        Assert.Equal("Util code", nicknames.OwnerLabel("code", "gml_Script_scr_a"));
    }

    [Fact]
    public void TheCodeLabelUsesTheLongestOwnerAndOnlyAnOwnerOfTheRightType()
    {
        var nicknames = new Nicknames();
        nicknames.Set("objects:obj_enemy", "Slime");
        Assert.Equal("gml_Object_Slime_Create_0", nicknames.CodeLabel("gml_Object_obj_enemy_Create_0"));
        nicknames.Set("objects:obj_enemy_boss", "Boss");
        Assert.Equal("gml_Object_Boss_Create_0", nicknames.CodeLabel("gml_Object_obj_enemy_boss_Create_0"));
        Assert.Equal("gml_Object_Slime_Step_1", nicknames.CodeLabel("gml_Object_obj_enemy_Step_1"));
        nicknames.Set("sprites:spr_a", "Pic");
        Assert.Equal("gml_Object_spr_a_Create_0", nicknames.CodeLabel("gml_Object_spr_a_Create_0")); // a sprite does not own object code
        nicknames.Set("scripts:scr_a", "Util");
        Assert.Equal("gml_Script_Util", nicknames.CodeLabel("gml_Script_scr_a"));
        Assert.Equal("gml_GlobalScript_Util", nicknames.CodeLabel("gml_GlobalScript_scr_a"));
    }

    [Fact]
    public void ACodeEntryOfARealLongerOwnerIsNotRelabelledByTheShorterOwnersNickname()
    {
        var nicknames = new Nicknames { IsOwner = key => key is "objects:obj_enemy" or "objects:obj_enemy_boss" };
        nicknames.Set("objects:obj_enemy", "Slime");
        Assert.Equal("gml_Object_obj_enemy_boss_Create_0", nicknames.CodeLabel("gml_Object_obj_enemy_boss_Create_0")); // obj_enemy_boss exists and has no nickname
        Assert.Equal("gml_Object_Slime_Create_0", nicknames.CodeLabel("gml_Object_obj_enemy_Create_0"));
        nicknames.Set("objects:obj_enemy_boss", "Boss");
        Assert.Equal("gml_Object_Boss_Create_0", nicknames.CodeLabel("gml_Object_obj_enemy_boss_Create_0"));
        Assert.Equal("gml_Object_Slime_Step_1", nicknames.CodeLabel("gml_Object_obj_enemy_Step_1"));
    }

    [Fact]
    public void TheCodeLabelOfFiftyThousandEntriesAndThreeHundredNicknamesIsFast()
    {
        var nicknames = new Nicknames();
        for (var i = 0; i < 300; i++) nicknames.Set($"objects:obj_{i}", $"Nick{i}");
        var entries = Enumerable.Range(0, 50_000).Select(i => $"gml_Object_obj_{i % 400}_Other_{i}").ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var changed = entries.Count(e => nicknames.CodeLabel(e) != e);
        watch.Stop();
        Assert.Equal(37_500, changed);
        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void ANicknameAnotherResourceHasOrThatIsAnotherResourcesNameIsRefused()
    {
        var nicknames = new Nicknames();
        static string? OwnerOf(string name) => name == "obj_b" ? "objects:obj_b" : null;
        Assert.True(nicknames.Set("objects:obj_a", "Hero", OwnerOf));
        Assert.Equal("objects:obj_a", nicknames.Conflict("objects:obj_b", "Hero", OwnerOf));
        Assert.False(nicknames.Set("objects:obj_b", "Hero", OwnerOf));
        Assert.Null(nicknames.Of("objects:obj_b"));
        Assert.Equal("objects:obj_b", nicknames.Conflict("objects:obj_a", "obj_b", OwnerOf));
        Assert.False(nicknames.Set("objects:obj_a", "obj_b", OwnerOf));
        Assert.Equal("Hero", nicknames.Of("objects:obj_a")); // a refusal changes nothing
        Assert.Null(nicknames.Conflict("objects:obj_a", "Hero", OwnerOf)); // its own nickname is no clash
        Assert.Null(nicknames.Conflict("objects:obj_b", "obj_b", OwnerOf)); // its own name is no clash
    }

    [Fact]
    public async Task TheRenameDialogShowsWhyANicknameIsRefusedAndRenamesNothing()
    {
        await HeadlessApp.Run(() =>
        {
            var (applied, saved) = (0, "");
            var dialog = new G3MStudio.RenameWindow("obj_a", _ => { applied++; return null; },
                options: new G3MStudio.RenameOptions(null, null, (_, nick) => saved = nick, (_, nick) => nick == "Hero" ? "Hero is taken" : null));
            dialog.Show();
            var parts = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(dialog).ToList();
            var name = parts.OfType<Avalonia.Controls.TextBox>().Single(t => t.Name == "NewName");
            var nick = parts.OfType<Avalonia.Controls.TextBox>().Single(t => t.Name == "Nickname");
            var error = parts.OfType<Avalonia.Controls.TextBlock>().Single(t => t.Name == "Error");
            var ok = parts.OfType<Avalonia.Controls.Button>().Single(b => b.Name == "Rename");
            name.Text = "obj_b";
            nick.Text = "Hero";
            ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(("Hero is taken", 0, ""), (error.Text, applied, saved)); // refused before the name was applied
            nick.Text = "Wyrm";
            ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal((1, "Wyrm"), (applied, saved));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void ANicknameIsOneLineOfWholeCharactersWithAnEmojiAllowed()
    {
        Assert.Equal("a b c", Nicknames.Clean("a\r\nb\tc"));
        Assert.Equal("Boss \U0001F480", Nicknames.Clean(" Boss \U0001F480 "));
        var long64 = string.Concat(Enumerable.Repeat("\U0001F480", Nicknames.MaxLength + 5));
        Assert.Equal(string.Concat(Enumerable.Repeat("\U0001F480", Nicknames.MaxLength)), Nicknames.Clean(long64)); // cut between emoji, never inside one
        var nicknames = new Nicknames();
        Assert.False(nicknames.Set("sprites:spr_a", "spr_a")); // the same as the name is no nickname
        Assert.False(nicknames.Any);
    }

    [Fact]
    public void SearchMatchesTheNameAndTheNicknameAndTheRowShowsTheNickname()
    {
        using var data = TestData.Create(3, withCode: false);
        var nicknames = new Nicknames();
        nicknames.Set("objects:obj_1", "Dragon");
        var model = new ExplorerModel(key => key) { Nicknames = nicknames };
        model.Load(data);
        model.SetFilter("drag");
        var row = Assert.Single(model.Rows, r => r.IsItem);
        Assert.Equal(("obj_1", "Dragon"), (row.Text, row.Label));
        model.SetFilter("obj_1");
        Assert.Single(model.Rows, r => r.IsItem && r.Key == "objects");
        model.SetFilter("obj:drag"); // the type prefix works with a nickname too
        Assert.Single(model.Rows, r => r.IsItem);

        model.SetFilter("");
        nicknames.Set("objects:obj_1", "Wyrm");
        model.Refresh(); // a changed nickname alone rebuilds the rows
        Assert.Equal("Wyrm", model.Rows.Single(r => r.IsItem && r.Text == "obj_1").Label);
        Assert.True(nicknames.Matches("objects", "obj_1", "wyr"));
    }

    [Fact]
    public void GoToFindsAResourceByEitherNameButShowsTheNickname()
    {
        var entry = new GoToEntry(GoToKind.Resource, "Dragon", "objects:obj_1", "objects", "", "obj_1");
        Assert.Single(GoToSearch.Run([entry], "drag", []));
        Assert.Single(GoToSearch.Run([entry], "obj_1", []));
    }

    [Fact]
    public void NicknamesAreKeptPerDataFileAndAdoptedWithItsLayout()
    {
        using var dir = new TempDir();
        var user = new UserData(dir.File("data"));
        var first = new Nicknames(user, "C:/games/old.win");
        first.Set("objects:obj_hero", "Hero \U0001F9B8");
        Assert.Equal("Hero \U0001F9B8", new Nicknames(user, "C:/games/old.win").Of("objects:obj_hero")); // survives a reopen
        Assert.False(new Nicknames(user, "C:/games/other.win").Any); // belongs to this file only

        Assert.Equal(1, user.AdoptFileData("C:/games/old.win", "C:/games/new.win"));
        Assert.Equal("Hero \U0001F9B8", new Nicknames(user, "C:/games/new.win").Of("objects:obj_hero"));
    }

    [Fact]
    public void ARekeyOntoANamedResourceKeepsBothNicknamesAndASwapThroughATemporaryNameKeepsThem()
    {
        var nicknames = new Nicknames();
        nicknames.Set("objects:obj_a", "Alpha");
        nicknames.Set("objects:obj_b", "Beta");
        Assert.Null(nicknames.Rekey("objects:obj_a", "objects:obj_b"));
        Assert.Equal(("Alpha", "Beta"), (nicknames.Of("objects:obj_a"), nicknames.Of("objects:obj_b")));

        nicknames.Rekey("objects:obj_a", "objects:obj_t");
        nicknames.Rekey("objects:obj_b", "objects:obj_a");
        nicknames.Rekey("objects:obj_t", "objects:obj_b");
        Assert.Equal(("Beta", "Alpha"), (nicknames.Of("objects:obj_a"), nicknames.Of("objects:obj_b")));
    }

    [Fact]
    public void ARenameMovesTheNicknameAndUndoMovesItBackAndADeleteClearsItUntilUndo()
    {
        var nicknames = new Nicknames();
        nicknames.Set("objects:obj_a", "Alpha");
        Assert.Equal("Alpha", nicknames.Rekey("objects:obj_a", "objects:obj_b"));
        Assert.Equal("Alpha", nicknames.Label("objects", "obj_b"));
        Assert.Null(nicknames.Of("objects:obj_a"));
        nicknames.Rekey("objects:obj_b", "objects:obj_a");
        Assert.Equal("Alpha", nicknames.Of("objects:obj_a"));
        var gone = nicknames.Rekey("objects:obj_a", null);
        Assert.False(nicknames.Any);
        nicknames.Restore("objects:obj_a", gone);
        Assert.Equal("Alpha", nicknames.Of("objects:obj_a"));
    }

    [Fact]
    public async Task ANicknameNamesTheResourceInCodeViewsButTheNameStaysTheIdentity()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        var service = new WorkspaceService(new UserData(dir.File("user")), RecentFiles.Load(new UserData(dir.File("user"))));
        var workspace = await service.OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        workspace.Nicknames.Set("objects:obj_1", "Dragon");

        Assert.Equal(("objects", "obj_1"), GmlUsages.Resolve(workspace, "Dragon"));
        Assert.Equal(("objects", "obj_1"), GmlUsages.Resolve(workspace, "obj_1"));
        Assert.Equal("obj_1", GmlNames.Real(workspace, "Dragon"));
        Assert.Equal("objects:obj_1", GmlNames.Definitions(workspace)("Dragon"));
        Assert.Equal("obj_1", GmlNames.Aliases(workspace)["Dragon"]);
        Assert.DoesNotContain(GmlNames.Completions(workspace)(), c => c.Key == "Dragon"); // a nickname is never inserted into code
        Assert.Null(GmlUsages.Resolve(workspace, "Hydra"));

        workspace.Nicknames.Set("objects:obj_1", "Two Words"); // not a name that code could hold
        Assert.Empty(GmlNames.Aliases(workspace));
        Assert.Null(GmlUsages.Resolve(workspace, "Two"));
    }

    [Fact]
    public async Task AScriptLeadsToItsCodeButItsHoverCardDescribesTheScript()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true); // the import adds the script "probe" with its code
        var service = new WorkspaceService(new UserData(dir.File("user")), RecentFiles.Load(new UserData(dir.File("user"))));
        var workspace = await service.OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("code:gml_Script_probe", GmlNames.Definitions(workspace)("probe"));
        Assert.Equal("scripts:probe", GmlNames.CardKey(workspace, "probe"));
        Assert.Equal("objects:obj_1", GmlNames.CardKey(workspace, "obj_1"));
        Assert.Null(GmlNames.CardKey(workspace, "show_debug_message"));
    }

    [Fact]
    public async Task TheCodeOwnersFollowARenameItsUndoAndAnImport()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        var service = new WorkspaceService(new UserData(dir.File("user")), RecentFiles.Load(new UserData(dir.File("user"))));
        var workspace = await service.OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var owner = workspace.Nicknames.IsOwner!;
        Assert.True(owner("objects:obj_0"));
        Assert.False(owner("objects:obj_first"));

        workspace.History.Execute(new RenameResourceCommand("objects", "obj_0", "obj_first"));
        Assert.True(owner("objects:obj_first"));
        Assert.False(owner("objects:obj_0"));
        workspace.History.Undo();
        Assert.True(owner("objects:obj_0"));

        await workspace.ApplyBulkAsync(() => workspace.Data.GameObjects.RemoveAt(0));
        Assert.False(owner("objects:obj_0"));
    }

    [Fact]
    public async Task TabsFollowTheNicknameAcrossRenameUndoAndDelete()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                window.Docking.OpenTab("resource", "objects:obj_0", false);
                var id = Assert.Single(window.Docking.Tabs.Tabs).Id;

                workspace.Nicknames.Set("objects:obj_0", "Hero");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Hero", window.Docking.DocumentOf(id)!.Title); // tab title shows the nickname
                Assert.Equal("Hero", window.Explorer.Model.Rows.Single(r => r.IsItem && r.Text == "obj_0").Label);

                workspace.History.Execute(new RenameResourceCommand("objects", "obj_0", "obj_first"));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Hero", workspace.Nicknames.Of("objects:obj_first"));
                Assert.Equal("Hero", window.Docking.DocumentOf(id)!.Title);

                window.Registry.Execute("edit.undo");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Hero", workspace.Nicknames.Of("objects:obj_0"));
                Assert.Null(workspace.Nicknames.Of("objects:obj_first"));

                workspace.Nicknames.Set("objects:obj_0", "");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("obj_0", window.Docking.DocumentOf(id)!.Title);
            }
            finally { window.Close(); }
        });
    }
}
