using G3MLib.DataFile;
using G3MLib.DataFile.Decompiler;
using G3MLib.Analyzer.Decompiler;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class EditingTests
{
    private sealed class Session(TempDir dir)
    {
        public UserData UserData { get; } = new(dir.File("user"));

        public WorkspaceService Service { get; private set; } = null!;

        public async Task<Workspace> Open(string path)
        {
            Service = new WorkspaceService(UserData, RecentFiles.Load(UserData));
            return await Service.OpenAsync(path);
        }

        public SavePipeline Pipeline => new(UserData);
    }

    private static string Decompile(GameMakerData data, string entry) =>
        new DecompileContext(new GlobalDecompileContext(data), data.Code.ByName(entry), data.ToolInfo.DecompilerSettings).DecompileToString();

    private static RenameResourceCommand Rename(string from, string to) => new("objects", from, to);

    private static EditCodeCommand Code(string text) => new("gml_Script_probe", text);

    [Fact]
    public void RenameUndoRedoAndHistoryList()
    {
        using var data = TestData.Create(2);
        var history = new EditHistory(data);
        var original = TestData.Bytes(data);

        history.Execute(Rename("obj_0", "obj_a"));
        history.Execute(Rename("obj_1", "obj_b"));
        Assert.Equal(["obj_a", "obj_b"], data.GameObjects.Select(o => o.Name.Content));
        Assert.Equal(["explorer", "explorer"], history.Items.Select(c => c.Editor));

        history.MoveTo(0);
        Assert.Equal(["obj_0", "obj_1"], data.GameObjects.Select(o => o.Name.Content));
        Assert.Equal(original, TestData.Bytes(data));
        Assert.False(history.IsDirty);

        history.Redo();
        Assert.Equal("obj_a", data.GameObjects[0].Name.Content);
        history.Execute(Rename("obj_1", "obj_c"));
        Assert.Equal(2, history.Items.Count);
        Assert.False(history.CanRedo);
        Assert.True(history.IsDirty);
    }

    [Fact]
    public void CodeEditUndoRestoresTheEntryAndRedoCompilesItAgain()
    {
        using var data = TestData.Create(1, withCode: true);
        var history = new EditHistory(data);
        var original = TestData.Bytes(data);
        var before = Decompile(data, "gml_Script_probe");

        history.Execute(Code("return 2;"));
        var after = Decompile(data, "gml_Script_probe");
        Assert.NotEqual(before, after);
        Assert.Contains("2", after, StringComparison.Ordinal);

        history.Undo();
        Assert.Equal(before, Decompile(data, "gml_Script_probe"));
        Assert.Equal(original, TestData.Bytes(data));

        history.Redo();
        Assert.Equal(after, Decompile(data, "gml_Script_probe"));
    }

    [Fact]
    public void ACommandThatFailsChangesAndRecordsNothing()
    {
        using var data = TestData.Create(1, withCode: true);
        var history = new EditHistory(data);
        var original = TestData.Bytes(data);
        var recorded = new List<string>();
        history.Recorded += (op, _) => recorded.Add(op);

        Assert.ThrowsAny<Exception>(() => history.Execute(Code("this is ( not gml")));
        Assert.ThrowsAny<Exception>(() => history.Execute(Rename("missing", "x")));
        Assert.ThrowsAny<Exception>(() => history.Execute(Rename("obj_0", "scr_0")));

        Assert.Empty(history.Items);
        Assert.Empty(recorded);
        Assert.Equal(original, TestData.Bytes(data));
    }

    [Fact]
    public void EditsAreRefusedWhileFrozen()
    {
        using var data = TestData.Create(1);
        var history = new EditHistory(data) { Frozen = true };
        Assert.Throws<InvalidOperationException>(() => history.Execute(Rename("obj_0", "x")));
    }

    [Fact]
    public async Task EveryEditReachesTheJournalFileAtOnce()
    {
        using var dir = new TempDir();
        var session = new Session(dir);
        var workspace = await session.Open(TestData.WriteSample(dir));

        workspace.History.Execute(Rename("obj_0", "obj_a"));

        var file = Journal.FileFor(session.UserData, workspace.Path);
        using var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        var lines = (await reader.ReadToEndAsync(cancellationToken: TestContext.Current.CancellationToken)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"rename\"", lines[1], StringComparison.Ordinal);
        session.Service.Close();
    }

    [Fact]
    public async Task ACrashRestoresUnsavedEditsAndTheUndoStack()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var session = new Session(dir);
        var first = await session.Open(path);
        first.History.Execute(Rename("obj_0", "obj_a"));
        first.History.Execute(Code("return 2;"));
        first.History.Execute(Rename("obj_1", "obj_b"));
        first.History.Undo();
        var expected = TestData.Bytes(first.Data);
        session.Service.Close(); // handles closed, journal file stays, like a dead process

        var second = await session.Open(path);

        Assert.Equal(2, second.RecoveredEdits);
        Assert.Null(second.RecoveryProblem);
        Assert.True(second.IsDirty);
        Assert.Equal(expected, TestData.Bytes(second.Data));
        Assert.Equal(3, second.History.Items.Count);
        second.History.Redo();
        Assert.Equal("obj_b", second.Data.GameObjects[1].Name.Content);
        second.History.MoveTo(0);
        Assert.Equal("obj_0", second.Data.GameObjects[0].Name.Content);

        // The recovered journal keeps working: a third session sees the new edit as well.
        second.History.Execute(Rename("obj_2", "obj_c"));
        session.Service.Close();
        var third = await session.Open(path);
        Assert.Equal("obj_c", third.Data.GameObjects[2].Name.Content);
        Assert.Equal(1, third.RecoveredEdits);
        session.Service.Close();
    }

    [Fact]
    public async Task ATornLastJournalLineIsDroppedAndLaterEditsStillReplay()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        var session = new Session(dir);
        var first = await session.Open(path);
        first.History.Execute(Rename("obj_0", "obj_a"));
        session.Service.Close();
        await File.AppendAllTextAsync(Journal.FileFor(session.UserData, path), "{\"Op\":\"do\",\"Ki", TestContext.Current.CancellationToken);

        var second = await session.Open(path);
        Assert.Equal(1, second.RecoveredEdits);
        second.History.Execute(Rename("obj_1", "obj_b"));
        session.Service.Close();

        var third = await session.Open(path);
        Assert.Equal(2, third.RecoveredEdits);
        session.Service.Close();
    }

    [Fact]
    public async Task SaveStartsTheJournalOverAndUndoStillWorksAcrossTheSave()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        var session = new Session(dir);
        var workspace = await session.Open(path);
        workspace.History.Execute(Rename("obj_0", "obj_a"));

        Assert.True((await workspace.SaveAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken)).Saved);

        Assert.False(workspace.IsDirty);
        workspace.History.Undo();
        Assert.True(workspace.IsDirty);
        workspace.History.Redo();
        Assert.False(workspace.IsDirty);
        session.Service.Close();

        var again = await session.Open(path);
        Assert.Equal(0, again.RecoveredEdits);
        Assert.Equal("obj_a", again.Data.GameObjects[0].Name.Content);
        session.Service.Close();
    }

    [Fact]
    public async Task AJournalOfAnOlderFileStateIsNotReplayed()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        var session = new Session(dir);
        var workspace = await session.Open(path);
        workspace.History.Execute(Rename("obj_0", "obj_a"));
        // Killed after the file was replaced and before the journal restarted: only the pipeline ran.
        Assert.True((await session.Pipeline.SaveAsync(workspace.Data, path, cancellationToken: TestContext.Current.CancellationToken)).Saved);
        session.Service.Close();

        var again = await session.Open(path);

        Assert.Equal(0, again.RecoveredEdits);
        Assert.Equal("obj_a", again.Data.GameObjects[0].Name.Content);
        Assert.True(File.Exists(Journal.FileFor(session.UserData, path) + ".stale"));
        session.Service.Close();
    }

    [Fact]
    public async Task AJournalThatCannotFullyReplayKeepsTheEditsThatDid()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        var session = new Session(dir);
        var first = await session.Open(path);
        first.History.Execute(Rename("obj_0", "obj_a"));
        session.Service.Close();
        var file = Journal.FileFor(session.UserData, path);
        await File.AppendAllTextAsync(file, /*lang=json,strict*/ "{\"Op\":\"do\",\"Kind\":\"rename\",\"Args\":{\"type\":\"objects\",\"old\":\"gone\",\"new\":\"x\"}}\n", TestContext.Current.CancellationToken);

        var second = await session.Open(path);

        Assert.Equal(1, second.RecoveredEdits);
        Assert.NotNull(second.RecoveryProblem);
        session.Service.Close();
        Assert.Equal(2, (await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken)).Length);
    }
}
