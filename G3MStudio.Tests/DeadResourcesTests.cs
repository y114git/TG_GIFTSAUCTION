using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class DeadResourcesTests
{
    private static async Task<(WorkspaceService Service, Workspace Workspace)> Open(TempDir dir, Action<GameMakerData> build)
    {
        using (var data = TestData.Create(6))
        {
            build(data);
            TestData.Write(dir.File("data.win"), data);
        }

        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));
        await service.OpenAsync(dir.File("data.win"), cancellationToken: TestContext.Current.CancellationToken);
        return (service, Assert.IsType<Workspace>(service.Current));
    }

    [Fact]
    public async Task DeadMaybeUsedAndUsedResourcesAreTold()
    {
        using var dir = new TempDir();
        var (_, workspace) = await Open(dir, data =>
        {
            // obj_1 is the parent of obj_2; obj_4 is named in a string of code; room_a is in the room order.
            data.GameObjects[2].ParentId = data.GameObjects[1];
            var group = new CodeImportGroup(data);
            group.QueueReplace("gml_Script_scr_0", "return \"obj_4\";");
            group.Import();
#pragma warning disable CA2000 // the data model owns what is added to it
            var room = new GameMakerRoom { Name = data.Strings.MakeString("room_a", out _) };
            data.Rooms.Add(room);
            data.Rooms.Add(new GameMakerRoom { Name = data.Strings.MakeString("room_b", out _) });
            data.GeneralInfo.RoomOrder.Add(new GameMakerResourceById<GameMakerRoom, GameMakerChunkROOM>(room));
#pragma warning restore CA2000
        });
        workspace.Nicknames.Set("objects:obj_3", "boss");

        var found = DeadResources.Find(workspace, QuickAccess.Empty.AddTag("objects:obj_5", "keep"), TestContext.Current.CancellationToken);
        DeadReason? Reason(string type, string name) => found.FirstOrDefault(d => d.Type == type && d.Name == name)?.Reason;

        Assert.Equal(DeadReason.None, Reason("objects", "obj_0"));
        Assert.Null(Reason("objects", "obj_1")); // its child refers to it
        Assert.Equal(DeadReason.None, Reason("objects", "obj_2"));
        Assert.Equal(DeadReason.Nickname, Reason("objects", "obj_3"));
        Assert.Equal(DeadReason.NameInCode, Reason("objects", "obj_4"));
        Assert.Equal(DeadReason.Tag, Reason("objects", "obj_5"));
        Assert.Null(Reason("rooms", "room_a")); // the room order refers to it
        Assert.Equal(DeadReason.None, Reason("rooms", "room_b"));
        Assert.All(found.Where(d => d.Reason != DeadReason.None), d => Assert.False(d.IsSafe));
    }

    [Fact]
    public async Task AFileWithTwoHundredThousandStringsIsScannedQuickly()
    {
        using var dir = new TempDir();
        var (_, workspace) = await Open(dir, data =>
        {
            for (var i = 0; i < 200_000; i++) data.Strings.Add(new GameMakerString("text_" + i));
            var group = new CodeImportGroup(data);
            group.QueueReplace("gml_Script_scr_0", "return \"obj_4\";");
            group.Import();
        });
        Assert.True(workspace.Data.Strings.Count > 200_000);
        ReferenceCache.Of(workspace.Data).Get(workspace.Data, TestContext.Current.CancellationToken);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var found = DeadResources.Find(workspace, QuickAccess.Empty, TestContext.Current.CancellationToken);
        watch.Stop();
        Assert.Equal(DeadReason.NameInCode, found.First(d => d.Name == "obj_4").Reason);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task AShortNameInCodeIsSeenAndOlderBytecodeLeavesNothingSafe()
    {
        using var dir = new TempDir();
        var (_, workspace) = await Open(dir, data =>
        {
#pragma warning disable CA2000 // the data model owns what is added to it
            data.Sounds.Add(new GameMakerSound { Name = data.Strings.MakeString("s1", out _) });
#pragma warning restore CA2000
            var group = new CodeImportGroup(data);
            group.QueueReplace("gml_Script_scr_0", "return asset_get_index(\"s1\");");
            group.Import();
        });

        var found = DeadResources.Find(workspace, QuickAccess.Empty, TestContext.Current.CancellationToken);
        Assert.Equal(DeadReason.NameInCode, found.Single(d => d.Name == "s1").Reason);
        Assert.Equal(DeadReason.None, found.Single(d => d.Name == "obj_0").Reason);

        // sprite_index = spr_x compiles to a plain number before GameMaker 2023.8, so an unreferenced asset may still be used.
        workspace.Data.SetGMS2Version(2023, 6);
        found = DeadResources.Find(workspace, QuickAccess.Empty, TestContext.Current.CancellationToken);
        Assert.Equal(DeadReason.NameInCode, found.Single(d => d.Name == "s1").Reason);
        Assert.Equal(DeadReason.NumericRefs, found.Single(d => d.Name == "obj_0").Reason);
        Assert.DoesNotContain(found, d => d.IsSafe);
    }

    [Fact]
    public async Task AOneOrTwoLetterStringIsANamePrefixOnlyWhenCodeJoinsTextToIt()
    {
        // "s_" + kind can name s_enemy at run time. A plain "o" in code joins nothing: counting it would list every o... name as maybe used, so obj_0 stays safe.
        using var dir = new TempDir();
        var (_, workspace) = await Open(dir, data =>
        {
#pragma warning disable CA2000 // the data model owns what is added to it
            data.Sounds.Add(new GameMakerSound { Name = data.Strings.MakeString("s_enemy", out _) });
#pragma warning restore CA2000
            var group = new CodeImportGroup(data);
            group.QueueReplace("gml_Script_scr_0", "var kind = \"enemy\"; return asset_get_index(\"s_\" + kind);");
            group.QueueReplace("gml_Script_scr_1", "return \"o\";");
            group.Import();
        });

        var found = DeadResources.Find(workspace, QuickAccess.Empty, TestContext.Current.CancellationToken);
        Assert.Equal(DeadReason.NamePrefixInCode, found.Single(d => d.Name == "s_enemy").Reason);
        Assert.Equal(DeadReason.None, found.Single(d => d.Name == "obj_0").Reason);
    }

    [Fact]
    public async Task DeleteAllSafeRemovesOnlyTheCertainOnesAsOneUndoableEdit()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            window.Confirmer = (_, _) => Task.FromResult(true);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                workspace.Nicknames.Set("objects:obj_2", "keep");
                window.Registry.Execute("organize.deadResources");
                var tool = Avalonia.Application.Current is null ? null : window.OwnedWindows.OfType<DeadResourcesWindow>().Single();
                Assert.NotNull(tool);
                await tool.RefreshAsync();
                Assert.DoesNotContain(tool.Checked, d => d.Name == "obj_2");
                Assert.Contains(tool.Checked, d => d.Name == "obj_0");

                await tool.DeleteAsync();
                Assert.DoesNotContain(workspace.Data.GameObjects, o => o.Name.Content == "obj_0");
                Assert.Contains(workspace.Data.GameObjects, o => o.Name.Content == "obj_2");
                workspace.Undo();
                Assert.Contains(workspace.Data.GameObjects, o => o.Name.Content == "obj_0");
                tool.Close();
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ARescanWaitsForTheScanRunningAndAFailedScanSaysWhy()
    {
        await HeadlessApp.Run(async () =>
        {
            var scans = 0;
            var pending = new TaskCompletionSource<IReadOnlyList<DeadResource>>();
            var tool = new DeadResourcesWindow(new Nicknames(), () =>
            {
                scans++;
                return pending.Task;
            }, _ => Task.FromResult<string?>(null));
            var first = tool.RefreshAsync();
            var second = tool.RefreshAsync();
            Assert.Equal(1, scans);
            pending.SetException(new InvalidOperationException("the file is closed"));
            await Task.WhenAll(first, second);
            var summary = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(tool).OfType<Avalonia.Controls.TextBlock>().Single(t => t.Name == "DeadSummary");
            Assert.Equal("the file is closed", summary.Text);
            tool.Close();
        });
    }
}
