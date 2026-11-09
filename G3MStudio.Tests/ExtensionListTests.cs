using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data model owns what is added to it
public sealed class ExtensionListTests
{
    private static GameMakerExtension Build(GameMakerData data)
    {
        var extension = new GameMakerExtension { Name = data.Strings.MakeString("ext_a"), Version = data.Strings.MakeString("1.0.0") };
        data.Extensions.Add(extension);
        return extension;
    }

    [Fact]
    public void FilesFunctionsArgumentsAndOptionsAreAddedEditedMovedRemovedAndUndone()
    {
        using var data = TestData.Create(0);
        var extension = Build(data);
        var history = new EditHistory(data);

        history.Execute(new ExtensionListCommand("ext_a", "Files", ExtensionListCommand.Add, -1));
        history.Execute(new ExtensionListCommand("ext_a", "Files", ExtensionListCommand.Add, -1));
        history.Execute(new SetPropertyCommand("extensions", "ext_a", "Files[1].Filename", "b.dll"));
        history.Execute(new ExtensionListCommand("ext_a", "Files", ExtensionListCommand.Move, 1, 0));
        Assert.Equal("b.dll", extension.Files[0].Filename.Content);
        Assert.Equal(GameMakerExtensionKind.Dll, extension.Files[0].Kind);

        history.Execute(new ExtensionListCommand("ext_a", "Files[0].Functions", ExtensionListCommand.Add, -1));
        history.Execute(new ExtensionListCommand("ext_a", "Files[0].Functions", ExtensionListCommand.Add, -1));
        Assert.Equal([1u, 2u], extension.Files[0].Functions.Select(f => f.ID));
        history.Execute(new ExtensionListCommand("ext_a", "Files[0].Functions[0].Arguments", ExtensionListCommand.Add, -1));
        history.Execute(new SetPropertyCommand("extensions", "ext_a", "Files[0].Functions[0].Arguments[0].Type", "String"));
        history.Execute(new SetPropertyCommand("extensions", "ext_a", "Files[0].Functions[0].ExtName", "ext_fn"));
        Assert.Equal(GameMakerExtensionVarType.String, extension.Files[0].Functions[0].Arguments[0].Type);
        history.Execute(new ExtensionListCommand("ext_a", "Options", ExtensionListCommand.Add, -1));
        Assert.Single(extension.Options);

        var remove = new ExtensionListCommand("ext_a", "Files[0].Functions", ExtensionListCommand.Remove, 0);
        history.Execute(remove);
        Assert.Single(extension.Files[0].Functions);
        history.Undo();
        Assert.Equal("ext_fn", extension.Files[0].Functions[0].ExtName.Content);

        // The journal rebuilds the same edit from its arguments.
        var replay = EditCommand.Create(remove.Kind, remove.Args);
        Assert.Equal(remove.Args, replay.Args);
        history.Execute(replay);
        Assert.Single(extension.Files[0].Functions);

        while (history.CanUndo) history.Undo();
        Assert.Empty(extension.Files);
        Assert.Empty(extension.Options);
    }

    [Fact]
    public void AFunctionTakesAtMostSeventeenArguments()
    {
        using var data = TestData.Create(0);
        var extension = Build(data);
        var history = new EditHistory(data);
        history.Execute(new ExtensionListCommand("ext_a", "Files", ExtensionListCommand.Add, -1));
        history.Execute(new ExtensionListCommand("ext_a", "Files[0].Functions", ExtensionListCommand.Add, -1));
        for (var i = 0; i < ExtensionListCommand.MaxArguments; i++) history.Execute(new ExtensionListCommand("ext_a", "Files[0].Functions[0].Arguments", ExtensionListCommand.Add, -1));
        Assert.Throws<InvalidOperationException>(() => history.Execute(new ExtensionListCommand("ext_a", "Files[0].Functions[0].Arguments", ExtensionListCommand.Add, -1)));
        Assert.Equal(ExtensionListCommand.MaxArguments, extension.Files[0].Functions[0].Arguments.Count);
        Assert.Throws<InvalidOperationException>(() => history.Execute(new ExtensionListCommand("ext_a", "Files", ExtensionListCommand.Remove, 5)));
    }

    [Fact]
    public async Task TheViewAddsMovesAndRemovesEntriesAndEachIsOneUndoStep()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                var extension = Build(workspace.Data);
                var view = new ExtensionView(extension, workspace);
                Assert.True(view.Add("Files"));
                Assert.True(view.Add("Files"));
                Assert.True(view.Move("Files", 0, 1));
                Assert.False(view.Move("Files", 0, 5));
                Assert.True(await view.Remove("Files", 0));
                Assert.Single(extension.Files);
                workspace.History.Undo();
                Assert.Equal(2, extension.Files.Count);
                workspace.History.Undo();
                workspace.History.Undo();
                workspace.History.Undo();
                Assert.Empty(extension.Files);
                Dispatcher.UIThread.RunJobs();
            }
            finally { window.Close(); }
        });
    }
}
