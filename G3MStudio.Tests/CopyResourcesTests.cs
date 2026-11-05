using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class CopyResourcesTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly UserData _userData;

    public CopyResourcesTests() => _userData = new UserData(_dir.File("user"));

    public void Dispose()
    {
        _dir.Dispose();
    }

    private static readonly (string, string)[] OnlyObject = [("objects", "obj_a")];

    /// <summary>A file with a page, a sprite, a script with code and an object that uses both.</summary>
    internal static GameMakerData Source(bool big = false)
    {
        var data = TestData.Create(1, true);
        TestData.AddPage(data);
        var history = new EditHistory(data);
        history.Execute(ResourceTemplates.New(data, "sprites", "spr_a"));
        history.Execute(ResourceTemplates.New(data, "scripts", "scr_use"));
        history.Execute(new EditCodeCommand("gml_Script_scr_use", "return 7;"));
        history.Execute(ResourceTemplates.New(data, "objects", "obj_a"));
        data.GameObjects.First(o => o.Name.Content == "obj_a").Sprite = data.Sprites.First(s => s.Name.Content == "spr_a");
        history.Execute(new ObjectEventCommand("obj_a", ObjectEventCommand.Add, 0, 0));
        history.Execute(new EditCodeCommand("gml_Object_obj_a_Create_0", "x = scr_use();"));
        if (!big) return data;
        // Hex digits of random ids hardly compress, so the package is larger than the journal's limit for one argument.
        history.Execute(ResourceTemplates.New(data, "scripts", "scr_big"));
        history.Execute(new EditCodeCommand("gml_Script_scr_big", string.Concat(Enumerable.Range(0, 4000).Select(i => $"global.v{i} = \"{Guid.NewGuid():N}\";\n"))));
        return data;
    }

    internal static GameMakerData Target(Action<GameMakerData, EditHistory>? shape = null)
    {
        var data = TestData.Create(0, false);
        TestData.AddPage(data);
        shape?.Invoke(data, new EditHistory(data));
        return data;
    }

    private async Task<Workspace> Open(string name, GameMakerData data)
    {
        using (data) TestData.Write(_dir.File(name), data);
        var workspace = await new WorkspaceService(_userData, RecentFiles.Load(_userData)).OpenAsync(_dir.File(name), cancellationToken: TestContext.Current.CancellationToken);
        return workspace;
    }

    // The written bytes hold a time stamp that changes with the second, so two states are compared by what they contain.
    private static string Signature(GameMakerData d) => string.Join("|",
    [
        string.Join(",", d.Sprites.Select(s => $"{s.Name.Content}:{s.Textures.Count}:{s.Width}")),
        string.Join(",", d.GameObjects.Select(o => $"{o.Name.Content}>{o.Sprite?.Name.Content}/{string.Join(".", o.Events.Select(e => e.Count))}")),
        string.Join(",", d.Scripts.Select(s => s.Name.Content)),
        string.Join(",", d.Code.Select(c => $"{c.Name.Content}:{c.Instructions.Count}")),
        string.Join(",", d.Rooms.Select(r => r.Name.Content)),
        string.Join(",", d.Sounds.Select(s => $"{s.Name.Content}:{s.AudioID}")),
        $"{d.Strings.Count} {d.Variables.Count} {d.Functions.Count} {d.EmbeddedTextures.Count} {d.TexturePageItems.Count} {d.EmbeddedAudio.Count} {d.EmbeddedAudio.Sum(a => a.Data.Length)}",
    ]);

    private static Task<CopyPlan> Plan(Workspace a, Workspace b, IReadOnlyList<(string, string)> items, bool dependencies = true, CollisionMode mode = CollisionMode.Rename, IReadOnlyDictionary<string, CollisionMode>? overrides = null) =>
        ResourceCopier.PlanAsync(a, b, items, dependencies, mode, overrides, TestContext.Current.CancellationToken);

    private static string Code(Workspace w, string entry) => CodeText.Decompile(w.Data, entry);

    [Fact]
    public async Task AnObjectIsCopiedWithItsSpriteAndScriptAndTheSourceStaysTheSame()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target());
        var before = Signature(a.Data);

        var plan = await Plan(a, b, OnlyObject);
        Assert.Equal(["obj_a", "scr_use", "spr_a"], plan.Entries.Select(e => e.Name).Order());
        Assert.Equal([true, true], plan.Entries.Where(e => e.Name != "obj_a").Select(e => e.IsDependency));
        Assert.All(plan.Entries, e => Assert.Equal(CopyAction.Add, e.Action));

        await ResourceCopier.CopyAsync(a, b, plan, null, TestContext.Current.CancellationToken);

        var copy = b.Data.GameObjects.Single(o => o.Name.Content == "obj_a");
        Assert.Same(b.Data.Sprites.Single(s => s.Name.Content == "spr_a"), copy.Sprite);
        Assert.Equal("return 7;", Code(b, "gml_Script_scr_use").Trim());
        Assert.Contains("scr_use", Code(b, "gml_Object_obj_a_Create_0"), StringComparison.Ordinal);
        Assert.Equal(1, b.History.Position);
        Assert.Equal(before, Signature(a.Data));
        Assert.False(a.History.IsDirty);
        Assert.True(b.History.IsDirty);

        // The copy is a valid data file: it writes and reads back.
        using var again = await GameMakerIO.ReadAsync(new MemoryStream(TestData.Bytes(b.Data)), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(again.GameObjects, o => o.Name.Content == "obj_a");
    }

    // Real games found this: a room of a 2022.9 file has no particle list in its asset layers, and a 2023.2 or newer target writes a pointer for it.
    [Fact]
    public async Task ARoomFromAnOlderFileGetsTheLayerListsTheTargetWritesAndSavesReadable()
    {
        using var a = await Open("a.win", Source().Also(d => new EditHistory(d).Execute(ResourceTemplates.New(d, "rooms", "room_a"))));
        using var b = await Open("b.win", Target((d, _) => d.GeneralInfo!.Branch = GameMakerGeneralInfo.BranchType.Post2022_0));
        Assert.True(b.Data.IsNonLTSVersionAtLeast(2023, 2));
        new RoomLayerCommand("room_a", RoomLayerCommand.AddAssets, 0, "Assets_1").Apply(a.Data);
        var assets = a.Data.Rooms.Single(r => r.Name.Content == "room_a").Layers.Select(l => l.Data).OfType<GameMakerRoom.Layer.LayerAssetsData>().Single();
        assets.ParticleSystems = null!;
        assets.TextItems = null!;

        await ResourceCopier.CopyAsync(a, b, await Plan(a, b, [("rooms", "room_a")], dependencies: false), null, TestContext.Current.CancellationToken);

        var copied = b.Data.Rooms.Single(r => r.Name.Content == "room_a").Layers.Select(l => l.Data).OfType<GameMakerRoom.Layer.LayerAssetsData>().Single();
        Assert.NotNull(copied.ParticleSystems);
        Assert.NotNull(copied.TextItems);
        using var again = await GameMakerIO.ReadAsync(new MemoryStream(TestData.Bytes(b.Data)), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(again.Rooms, r => r.Name.Content == "room_a");
    }

    [Fact]
    public async Task OneUndoRevertsTheWholeCopyAndRedoRepeatsIt()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target());
        var original = Signature(b.Data);
        await ResourceCopier.CopyAsync(a, b, await Plan(a, b, OnlyObject), null, TestContext.Current.CancellationToken);
        var copied = Signature(b.Data);
        Assert.NotEqual(original, copied);

        b.Undo();
        Assert.Equal(original, Signature(b.Data));
        Assert.False(b.History.CanUndo);
        b.Redo();
        Assert.Equal(copied, Signature(b.Data));
    }

    [Fact]
    public async Task WithoutDependenciesOnlyTheSelectedItemMoves()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target());
        var plan = await Plan(a, b, OnlyObject, dependencies: false);
        Assert.Equal(["obj_a"], plan.Entries.Select(e => e.Name));
        await ResourceCopier.CopyAsync(a, b, plan, null, TestContext.Current.CancellationToken);
        Assert.Empty(b.Data.Sprites);
        Assert.Null(b.Data.GameObjects.Single(o => o.Name.Content == "obj_a").Sprite);
    }

    [Fact]
    public async Task ARenamedItemKeepsItsLinksToTheOtherCopiedItems()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target((d, h) =>
        {
            h.Execute(ResourceTemplates.New(d, "sprites", "spr_a"));
            h.Execute(ResourceTemplates.New(d, "objects", "obj_a"));
        }));
        var overrides = new Dictionary<string, CollisionMode> { ["sprites:spr_a"] = CollisionMode.Rename };
        var plan = await Plan(a, b, OnlyObject, overrides: overrides);
        Assert.Equal(["obj_a_copy", "scr_use", "spr_a_copy"], plan.Applied.Select(e => e.TargetName).Order());

        await ResourceCopier.CopyAsync(a, b, plan, null, TestContext.Current.CancellationToken);

        Assert.Equal(["obj_a", "obj_a_copy"], b.Data.GameObjects.Select(o => o.Name.Content).Order());
        var copy = b.Data.GameObjects.Single(o => o.Name.Content == "obj_a_copy");
        Assert.Equal("spr_a_copy", copy.Sprite?.Name.Content);
        Assert.Null(b.Data.GameObjects.Single(o => o.Name.Content == "obj_a").Sprite); // the target's own object is untouched
        Assert.Contains("scr_use", Code(b, "gml_Object_obj_a_copy_Create_0"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkipLeavesTheTargetItemAndReplaceChangesItInPlaceAndUndoPutsItBack()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target((d, h) => h.Execute(ResourceTemplates.New(d, "objects", "obj_a"))));
        var overrides = new Dictionary<string, CollisionMode> { ["objects:obj_a"] = CollisionMode.Skip };
        var skipped = await Plan(a, b, OnlyObject, overrides: overrides);
        Assert.Equal(CopyAction.Skip, skipped.Entries.Single(e => e.Name == "obj_a").Action);

        var replace = await Plan(a, b, OnlyObject, mode: CollisionMode.Replace);
        Assert.Equal(CopyAction.Replace, replace.Entries.Single(e => e.Name == "obj_a").Action);
        Assert.Null(b.Data.GameObjects.Single().Sprite);
        var original = Signature(b.Data);
        await ResourceCopier.CopyAsync(a, b, replace, null, TestContext.Current.CancellationToken);

        var replaced = b.Data.GameObjects.Single(o => o.Name.Content == "obj_a");
        Assert.Equal("spr_a", replaced.Sprite?.Name.Content);
        Assert.Contains("scr_use", Code(b, "gml_Object_obj_a_Create_0"), StringComparison.Ordinal);

        b.Undo();
        Assert.Equal(original, Signature(b.Data));
    }

    [Fact]
    public async Task ASoundBringsItsOwnAudioAndLeavesTheTargetAudioAlone()
    {
        using var a = await Open("a.win", Source().Also(d => new EditHistory(d).Execute(ResourceTemplates.New(d, "sounds", "snd_a"))));
        using var b = await Open("b.win", Target((d, h) => h.Execute(ResourceTemplates.New(d, "sounds", "snd_b"))));
        var own = b.Data.EmbeddedAudio[0].Data.ToArray();
        var plan = await Plan(a, b, [("sounds", "snd_a")]);

        await ResourceCopier.CopyAsync(a, b, plan, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, b.Data.EmbeddedAudio.Count);
        Assert.Equal(own, b.Data.EmbeddedAudio[0].Data);
        var sound = b.Data.Sounds.Single(s => s.Name.Content == "snd_a");
        Assert.Same(b.Data.EmbeddedAudio[1], sound.AudioFile);
        Assert.Equal(a.Data.EmbeddedAudio[0].Data, sound.AudioFile?.Data);
    }

    [Fact]
    public async Task ARoomKeepsItsInstancesWithNewIdsThatTheTargetDoesNotUse()
    {
        using var a = await Open("a.win", Source().Also(d =>
        {
            var history = new EditHistory(d);
            history.Execute(ResourceTemplates.New(d, "rooms", "room_a"));
            history.Execute(new RoomInstanceCommand("room_a", RoomInstanceCommand.Add, 0, 5, 6, "obj_a"));
            history.Execute(new RoomInstanceCommand("room_a", RoomInstanceCommand.Add, 0, 7, 8, "obj_a"));
        }));
        using var b = await Open("b.win", Target((d, h) =>
        {
            h.Execute(ResourceTemplates.New(d, "objects", "obj_b"));
            h.Execute(ResourceTemplates.New(d, "rooms", "room_b"));
            h.Execute(new RoomInstanceCommand("room_b", RoomInstanceCommand.Add, 0, 1, 1, "obj_b"));
        }));
        var taken = b.Data.Rooms.SelectMany(RoomInstanceCommand.Lists).SelectMany(l => l).Select(i => i.InstanceID).ToHashSet();
        var plan = await Plan(a, b, [("rooms", "room_a")]);
        Assert.Contains(plan.Entries, e => e.Name == "obj_a" && e.IsDependency);

        await ResourceCopier.CopyAsync(a, b, plan, null, TestContext.Current.CancellationToken);

        var room = b.Data.Rooms.Single(r => r.Name.Content == "room_a");
        var copied = RoomInstanceCommand.Lists(room).SelectMany(l => l).ToList();
        Assert.Equal(2, copied.Count);
        Assert.All(copied, i => Assert.DoesNotContain(i.InstanceID, taken));
        Assert.Equal(2, copied.Select(i => i.InstanceID).Distinct().Count());
        Assert.Equal("obj_a", copied[0].ObjectDefinition?.Name.Content);
        Assert.True(b.Data.GeneralInfo!.LastObj >= copied.Max(i => i.InstanceID));
        b.Undo();
        Assert.DoesNotContain(b.Data.Rooms, r => r.Name.Content == "room_a");
    }

    [Fact]
    public async Task APictureLandsOnANewPageOfTheTargetAndTheSourcePageIsNotUsed()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target());
        var pages = b.Data.EmbeddedTextures.Count;
        await ResourceCopier.CopyAsync(a, b, await Plan(a, b, [("sprites", "spr_a")]), null, TestContext.Current.CancellationToken);
        var sprite = b.Data.Sprites.Single();
        Assert.Equal(pages + 1, b.Data.EmbeddedTextures.Count);
        Assert.Same(b.Data.EmbeddedTextures[^1], sprite.Textures[0].Texture?.TexturePage);
        Assert.Equal(a.Data.Sprites[0].Width, sprite.Width);
    }

    [Fact]
    public async Task AnIncompatibleVersionIsRefusedWithAMessageAndNothingChanges()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target());
        b.Data.GeneralInfo!.BytecodeVersion = (byte)(a.Data.GeneralInfo!.BytecodeVersion - 1);
        var plan = await Plan(a, b, OnlyObject);
        Assert.Contains("bytecode", plan.Refusal, StringComparison.Ordinal);
        Assert.False(plan.CanApply);
        Assert.All(plan.Entries, e => Assert.Equal(CopyAction.Refused, e.Action));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ResourceCopier.BuildAsync(a, b, plan, null, TestContext.Current.CancellationToken));
        Assert.Contains("bytecode", ex.Message, StringComparison.Ordinal);
        Assert.False(b.History.IsDirty);
    }

    [Fact]
    public async Task ACommandRebuiltFromItsArgsDoesTheSameAndTheJournalReplaysIt()
    {
        using var a = await Open("a.win", Source(big: true));
        var b = await Open("b.win", Target());
        var command = await ResourceCopier.CopyAsync(a, b, await Plan(a, b, [("objects", "obj_a"), ("scripts", "scr_big")]), null, TestContext.Current.CancellationToken);
        var codeCount = b.Data.Code.Count;
        var path = b.Path;
        b.Dispose();

        // A rebuilt command on a fresh copy of the file gives the same bytes.
        using var fresh = await Open("b2.win", Target());
        new EditHistory(fresh.Data).Execute(EditCommand.Create(command.Kind, command.Args));
        Assert.Equal(["obj_a"], fresh.Data.GameObjects.Select(o => o.Name.Content));
        Assert.Equal("spr_a", fresh.Data.GameObjects[0].Sprite?.Name.Content);
        Assert.Equal(Code(a, "gml_Script_scr_use"), Code(fresh, "gml_Script_scr_use"));

        // The journal holds the package as a side file, and a reopened file gets the copy back.
        var journal = Journal.FileFor(_userData, path);
        Assert.True(command.Args["package"].Length > 64 * 1024);
        Assert.True(Directory.Exists(journal + ".blobs"));
        using var reopened = await new WorkspaceService(_userData, RecentFiles.Load(_userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, reopened.RecoveredEdits);
        Assert.Equal(codeCount, reopened.Data.Code.Count);
        Assert.Equal(["obj_a"], reopened.Data.GameObjects.Select(o => o.Name.Content));
        Assert.Equal(Code(a, "gml_Script_scr_big"), Code(reopened, "gml_Script_scr_big"));
        Assert.Equal(Code(a, "gml_Object_obj_a_Create_0"), Code(reopened, "gml_Object_obj_a_Create_0"));
    }

    // Real games found these (Deltarune 2023.6): a file with no particle chunk, 2.3 code named gml_GlobalScript_*, and arrays inside a replaced item.
    [Fact]
    public async Task AFileWithoutParticleSystemsCanBeCopiedFrom()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target());
        a.Data.FORM.Chunks.Remove("PSYS");
        Assert.Null(a.Data.ParticleSystems);
        await ResourceCopier.CopyAsync(a, b, await Plan(a, b, OnlyObject), null, TestContext.Current.CancellationToken);
        Assert.Single(b.Data.GameObjects, o => o.Name.Content == "obj_a");
    }

    [Fact]
    public async Task TheGlobalScriptCodeOfARenamedScriptGetsTheNewNameAndTheTargetCodeStays()
    {
        static void Shape(GameMakerData d, string text)
        {
            var history = new EditHistory(d);
            history.Execute(ResourceTemplates.New(d, "scripts", "scr_g"));
            d.Code.ByName("gml_Script_scr_g").Name.Content = "gml_GlobalScript_scr_g";
            history.Execute(new EditCodeCommand("gml_GlobalScript_scr_g", text));
        }

        using var a = await Open("a.win", Source().Also(d => Shape(d, "return 3;")));
        using var b = await Open("b.win", Target((d, _) => Shape(d, "return 9;")));
        var plan = await Plan(a, b, [("scripts", "scr_g")], dependencies: false);
        Assert.Equal("scr_g_copy", plan.Entries.Single().TargetName);

        await ResourceCopier.CopyAsync(a, b, plan, null, TestContext.Current.CancellationToken);

        Assert.Equal("return 9;", Code(b, "gml_GlobalScript_scr_g").Trim());
        Assert.Equal("return 3;", Code(b, "gml_GlobalScript_scr_g_copy").Trim());
    }

    [Fact]
    public async Task UndoTakesTheStartScriptThatTheCompiledGlobalScriptAddedOutOfTheList()
    {
        static void Shape(GameMakerData d)
        {
            var history = new EditHistory(d);
            history.Execute(ResourceTemplates.New(d, "scripts", "scr_g"));
            d.Code.ByName("gml_Script_scr_g").Name.Content = "gml_GlobalScript_scr_g";
            history.Execute(new EditCodeCommand("gml_GlobalScript_scr_g", "return 3;"));
        }

        using var a = await Open("a.win", Source().Also(Shape));
        using var b = await Open("b.win", Target());
        Assert.SkipWhen(b.Data.GlobalInitScripts is null, "the generated file has no start script list");
        var before = b.Data.GlobalInitScripts.Count;
        await ResourceCopier.CopyAsync(a, b, await Plan(a, b, [("scripts", "scr_g")], dependencies: false), null, TestContext.Current.CancellationToken);
        Assert.Equal(before + 1, b.Data.GlobalInitScripts.Count);

        b.Undo();

        Assert.Equal(before, b.Data.GlobalInitScripts.Count);
        Assert.All(b.Data.GlobalInitScripts, i => Assert.Contains(i.Code, b.Data.Code));
    }

    [Fact]
    public void AScriptWhoseCodeEntryNameIsTakenInTheTargetIsRefused()
    {
        using var source = Source();
        new EditHistory(source).Execute(ResourceTemplates.New(source, "scripts", "scr_g"));
        source.Code.ByName("gml_Script_scr_g").Name.Content = "gml_GlobalScript_scr_g";
        using var target = TestData.Create(0, false);
#pragma warning disable CA2000 // the entry belongs to the target, which owns and disposes it
        target.Code.Add(GameMakerCode.CreateEmptyEntry(target, "gml_GlobalScript_scr_g"));
#pragma warning restore CA2000

        var reason = CopyPlanner.ItemRefusal(source, target, "scripts", "scr_g");

        Assert.Contains("gml_GlobalScript_scr_g", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AMementoPutsBackAFixedSizeListInPlaceAndSkipsNumberArrays()
    {
        var names = new[] { "a", "b" };
        var bytes = new byte[] { 1, 2, 3 };
        var memento = new ShallowMemento();
        memento.Capture(names, 2);
        memento.Capture(bytes, 2);
        names[0] = "changed";
        memento.Restore();
        Assert.Equal(["a", "b"], names);
    }

    [Fact]
    public async Task ACancelledCopyLeavesTheTargetAsItWas()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target());
        var original = Signature(b.Data);
        using var cancel = new CancellationTokenSource();
        var command = await ResourceCopier.BuildAsync(a, b, await Plan(a, b, OnlyObject), null, TestContext.Current.CancellationToken);
        await cancel.CancelAsync();
        command.Cancellation = cancel.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => b.EditInBackground(command));
        Assert.Equal(original, Signature(b.Data));
        Assert.False(b.History.IsDirty);
    }

    [Fact]
    public async Task ATargetThatIsReadOnlyOrSavingOrTheSourceIsRefused()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target());
        Assert.NotNull(ResourceCopier.TargetRefusal(a, a));
        Assert.Null(ResourceCopier.TargetRefusal(a, b));
        b.History.Frozen = true;
        Assert.Contains("saved", ResourceCopier.TargetRefusal(a, b), StringComparison.Ordinal);
        b.History.Frozen = false;
        File.SetAttributes(b.Path, FileAttributes.ReadOnly);
        try
        {
            Assert.Contains("read only", ResourceCopier.TargetRefusal(a, b), StringComparison.Ordinal);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ResourceCopier.CopyAsync(a, b, new CopyPlan([], [], null), null, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.SetAttributes(b.Path, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task AnExtensionAndASoundOutsideTheDataFileAreRefusedWithAReason()
    {
        using var a = await Open("a.win", Source());
        using var b = await Open("b.win", Target());
        var plan = await Plan(a, b, [("extensions", "ext_x"), ("objects", "missing")]);
        Assert.All(plan.Entries, e => Assert.Equal(CopyAction.Refused, e.Action));
        Assert.Contains("Extensions", plan.Entries[0].Refusal, StringComparison.Ordinal);
        Assert.Contains("not found", plan.Entries[1].Refusal, StringComparison.Ordinal);
    }
}

public sealed class CopyNamesTests
{
    [Theory]
    [InlineData("..\\evil")]
    [InlineData("/abs")]
    [InlineData("a/../../b")]
    [InlineData("C:\\x")]
    [InlineData("..")]
    [InlineData("")]
    public void ANameThatCouldLeaveItsFolderIsNotAFileName(string name)
    {
        Assert.False(CopyPackage.IsSafeName(name));
        using var data = TestData.Create(1);
        Assert.Contains("file name", CopyPlanner.ItemRefusal(data, data, "objects", name), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => CopyPackage.PathInside(Path.GetTempPath(), "Sounds", name));
        Assert.True(CopyPackage.IsSafeName("snd_a-1"));
    }

    [Fact]
    public async Task AHostileSoundNameWritesNothingOutsideTheCopyFolder()
    {
        using var dir = new TempDir();
        using var source = await OpenWith(dir, "a.win", d =>
        {
            TestData.AddPage(d);
            new EditHistory(d).Execute(ResourceTemplates.New(d, "sounds", "snd_ok"));
        });
        using var target = await OpenWith(dir, "b.win", TestData.AddPage);
        source.Data.Sounds[0].Name.Content = "..\\g3m_evil";
        var entry = new PlanEntry("sounds", "..\\g3m_evil", "..\\g3m_evil", CopyAction.Add, null, new CopyFacts(false, false, false, []));

        await Assert.ThrowsAsync<InvalidOperationException>(() => ResourceCopier.BuildAsync(source, target, new CopyPlan([entry], [], null), null, TestContext.Current.CancellationToken));

        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "g3m_evil")));
        Assert.False(Directory.Exists(Path.Combine(Path.GetTempPath(), "g3m_evil")));
    }

    [Fact]
    public async Task APackageWithAnEntryOutsideItsFolderIsRefusedWhenItRuns()
    {
        using var dir = new TempDir();
        using var target = await OpenWith(dir, "b.win", TestData.AddPage);
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            await using var writer = new StreamWriter(await zip.CreateEntry("../g3m_evil_entry.txt").OpenAsync(TestContext.Current.CancellationToken));
            await writer.WriteAsync("x");
        }

        var command = new CopyResourcesCommand(stream.ToArray(), CopyVersion.Of(target.Data), [], 0);
        Assert.ThrowsAny<Exception>(() => command.Apply(target.Data));

        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "g3m_evil_entry.txt")));
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "g3mstudio-copy-", "..", "g3m_evil_entry.txt")));
    }

    private static async Task<Workspace> OpenWith(TempDir dir, string name, Action<G3MLib.DataFile.GameMakerData> shape)
    {
        using (var data = TestData.Create(0))
        {
            shape(data);
            TestData.Write(dir.File(name), data);
        }

        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(dir.File(name), cancellationToken: TestContext.Current.CancellationToken);
    }
}

internal static class DataExtensions
{
    public static GameMakerData Also(this GameMakerData data, Action<GameMakerData> change)
    {
        change(data);
        return data;
    }
}
