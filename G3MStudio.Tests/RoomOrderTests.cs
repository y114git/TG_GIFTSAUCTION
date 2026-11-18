using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class RoomOrderTests
{
    private static IEnumerable<string> Names(GameMakerData data) => data.GeneralInfo.RoomOrder.Select(r => r.Resource.Name.Content!);

    [Fact]
    public async Task RoomsAreRemovedMovedAndAddedBackWithUndoAndASurvivingWrite()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var original = Names(data).ToList();
        Assert.False(original.Count < 3, "The smallest corpus game has fewer than three rooms in its order.");

        var move = EditCommand.Create(RoomOrderCommand.Name, new RoomOrderCommand(RoomOrderCommand.Move, index: 0, to: 2).Args);
        move.Apply(data);
        Assert.Equal(original[1], Names(data).First());
        Assert.Equal(original[0], Names(data).ElementAt(2));
        move.Revert(data);
        Assert.Equal(original, Names(data));

        var remove = new RoomOrderCommand(RoomOrderCommand.Remove, index: 1);
        remove.Apply(data);
        Assert.DoesNotContain(original[1], Names(data));
        var add = new RoomOrderCommand(RoomOrderCommand.Add, original[1]);
        add.Apply(data);
        Assert.Equal(original[1], Names(data).Last());
        Assert.Throws<InvalidOperationException>(() => new RoomOrderCommand(RoomOrderCommand.Add, original[1]).Apply(data));
        Assert.Throws<InvalidOperationException>(() => new RoomOrderCommand(RoomOrderCommand.Remove, index: 999).Apply(data));

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var again = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Names(data), Names(again));

        add.Revert(data);
        remove.Revert(data);
        Assert.Equal(original, Names(data));
    }

    [Fact]
    public async Task TheRoomOrderWindowMovesARoomUpAndUndoes()
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
                var original = Names(workspace.Data).ToList();
                Assert.False(original.Count < 2, "The smallest corpus game has fewer than two rooms in its order.");
                Assert.True(window.Registry.Execute("tools.roomOrder"));
                var open = window.OwnedWindows.OfType<RoomOrderWindow>().Single();
                var list = open.GetVisualDescendants().OfType<Avalonia.Controls.ListBox>().Single(l => l.Name == "RoomOrderList");
                list.SelectedIndex = 1;
                open.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Single(b => b.Name == "RoomOrderUp").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Equal(original[1], Names(workspace.Data).First());
                Assert.Equal(0, list.SelectedIndex);
                workspace.History.Undo();
                Assert.Equal(original, Names(workspace.Data));
                open.Close();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
