using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class PhysicsVertexTests
{
    [Fact]
    public async Task VerticesAreAddedMovedAndRemovedFromTheViewAndEachStepUndoes()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                var obj = workspace.Data.GameObjects[0];
                var view = new PhysicsVerticesView(obj, workspace);
                var start = obj.PhysicsVertices.Count;

                view.SetBoxes("4", "-2.5");
                Assert.True(view.Edit(PhysicsVertexCommand.Add, -1));
                Assert.Equal(start + 1, view.Count);
                Assert.Equal((4f, -2.5f), (obj.PhysicsVertices[start].X, obj.PhysicsVertices[start].Y));

                view.SetBoxes("7", "8");
                Assert.True(view.Edit(PhysicsVertexCommand.Set, start));
                Assert.Equal((7f, 8f), (obj.PhysicsVertices[start].X, obj.PhysicsVertices[start].Y));
                workspace.History.Undo();
                Assert.Equal((4f, -2.5f), (obj.PhysicsVertices[start].X, obj.PhysicsVertices[start].Y));

                Assert.True(view.Edit(PhysicsVertexCommand.Remove, start, true));
                Assert.Equal(start, view.Count);
                workspace.History.Undo();
                Assert.Equal(start + 1, view.Count);
                workspace.History.Undo();
                Assert.Equal(start, view.Count);

                view.SetBoxes("x", "1");
                Assert.False(view.Edit(PhysicsVertexCommand.Add, -1)); // not a number: nothing happens
                Assert.False(view.Edit(PhysicsVertexCommand.Set, 99));
                Dispatcher.UIThread.RunJobs();
            }
            finally { window.Close(); }
        });
    }
}
