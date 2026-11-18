using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ObjectEventTests
{
    [Fact]
    public async Task AnEventAddedToAnObjectSurvivesAWriteAndARead_AndAddAndRemoveUndo()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var owner = data.GameObjects.First(o => o.Events[(int)EventType.Alarm].All(e => e.EventSubtype != 11));
        var name = owner.Name.Content!;
        var codeBefore = data.Code.Count;
        var add = EditCommand.Create(ObjectEventCommand.Name, new ObjectEventCommand(name, ObjectEventCommand.Add, (uint)EventType.Alarm, 11).Args);
        add.Apply(data);
        var added = Assert.Single(owner.Events[(int)EventType.Alarm], e => e.EventSubtype == 11);
        Assert.Equal(ObjectEventCommand.CodeName(name, (uint)EventType.Alarm, 11), Assert.Single(added.Actions).CodeId?.Name.Content);
        Assert.Equal(codeBefore + 1, data.Code.Count);
        Assert.Throws<InvalidOperationException>(() => new ObjectEventCommand(name, ObjectEventCommand.Add, (uint)EventType.Alarm, 11).Apply(data));

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var again = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var read = again.GameObjects.First(o => o.Name.Content == name).Events[(int)EventType.Alarm].Single(e => e.EventSubtype == 11);
        Assert.NotNull(read.Actions.Single().CodeId);

        var remove = new ObjectEventCommand(name, ObjectEventCommand.Remove, (uint)EventType.Alarm, 11);
        remove.Apply(data);
        Assert.DoesNotContain(owner.Events[(int)EventType.Alarm], e => e.EventSubtype == 11);
        Assert.Equal(codeBefore, data.Code.Count);
        remove.Revert(data);
        Assert.Equal(codeBefore + 1, data.Code.Count);
        add.Revert(data);
        Assert.Equal(codeBefore, data.Code.Count);
        Assert.DoesNotContain(owner.Events[(int)EventType.Alarm], e => e.EventSubtype == 11);
    }

    [Fact]
    public async Task TheEventsViewAddsAndRemovesAnEventAndOpensItsCode()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                var owner = workspace.Data.GameObjects.First(o => o.Events[(int)EventType.Alarm].All(e => e.EventSubtype != 11));
                var opened = new List<string>();
                var view = new Editor.ObjectEventsView(owner, opened.Add, workspace);
                var host = new Avalonia.Controls.Window { Content = view };
                host.Show();
                var before = view.Rows.Count;
                view.AddEvent((uint)EventType.Alarm, 11);
                Assert.Equal(before + 1, view.Rows.Count);
                Assert.Contains(view.Rows, r => r.Event == "Alarm 11");
                Assert.EndsWith("_Alarm_11", Assert.Single(opened), StringComparison.Ordinal);
                Assert.True(view.RemoveEvent((uint)EventType.Alarm, 11));
                Assert.Equal(before, view.Rows.Count);
                workspace.History.Undo();
                Assert.Equal(before + 1, view.Rows.Count);
                host.Close();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MouseEventsCanBeAddedAndNamed()
    {
        Assert.Contains(EventType.Mouse, EventNames.Types);
        Assert.NotEmpty(EventNames.Subtypes(EventType.Mouse, null));
        Assert.StartsWith("Mouse", EventNames.Label((uint)EventType.Mouse, 0, null), StringComparison.Ordinal);
    }
}
