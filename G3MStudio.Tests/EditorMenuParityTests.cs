using Avalonia.VisualTree;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The Room, Sound, Font and Tile Set menus offer the useful buttons of their editors through the command registry, and each command runs the code of its button.</summary>
public sealed class EditorMenuParityTests
{
    private static readonly string[] Added =
    [
        "room.addLayer", "room.duplicateLayer", "room.toggleGrid", "room.toggleSnap", "room.zoomFit", "room.zoomActual",
        "edit.playSound", "edit.stopSound", "edit.loopSound", "edit.replaceFont",
    ];

    [Fact]
    public void EveryAddedCommandIsRegisteredTitledAndInAMenuThatShowsOnlyWithItsEditor()
    {
        var registry = AppCommands.Create(new FakeShell());
        foreach (var id in Added)
        {
            Assert.NotNull(registry.Find(id));
            Assert.Contains(AppCommands.Menus, m => m.Ids.Contains(id));
            Assert.NotEqual(AppCommands.TitleKey(id), Loc.Current[AppCommands.TitleKey(id)]);
        }

        foreach (var (menu, context) in new[] { ("menu.font", AppCommands.FontTab), ("menu.tileSet", AppCommands.TileSetTab), ("menu.sound", AppCommands.SoundTab), ("menu.room", AppCommands.RoomTab) })
        {
            Assert.Equal(context, AppCommands.MenuContexts[menu]);
            Assert.NotEqual(menu, Loc.Current[menu]);
        }

        // no command twice in one menu, and a command that works on one editor is disabled (with its context) elsewhere
        Assert.All(AppCommands.Menus, m => Assert.Equal(m.Ids.Count(i => i != "-"), m.Ids.Where(i => i != "-").Distinct().Count()));
    }

    [Fact]
    public async Task TheRoomMenuCommandsRunTheCodeOfTheRoomButtonsAndAreOffInOtherEditors()
    {
        using var dir = new TempDir();
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source())
        {
            new EditHistory(data).Execute(ResourceTemplates.New(data, "rooms", "rm_menu"));
            TestData.Write(path, data);
        }
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                await window.OpenAsync(path);
                Assert.False(window.Registry.HasContext(AppCommands.RoomTab));
                window.Docking.OpenTab("resource", "rooms:rm_menu", false);
                await CommandSweep.Until(() => window.Registry.HasContext(AppCommands.RoomTab));
                await CommandSweep.Pump(5);
                Assert.False(window.Registry.HasContext(AppCommands.FontTab));
                var room = window.GetVisualDescendants().OfType<RoomPreview>().Single();
                await room.Ready;
                var (grid, snap) = (room.View.ShowGrid, room.View.SnapToGrid);
                Assert.True(window.Registry.Execute("room.toggleGrid"));
                Assert.True(window.Registry.Execute("room.toggleSnap"));
                Assert.Equal((!grid, !snap), (room.View.ShowGrid, room.View.SnapToGrid));
                Assert.True(window.Registry.Execute("room.zoomActual"));
                Assert.Equal(1, room.View.Zoom);
                var layers = window.Workspaces.Current!.Data.Rooms.Single(r => r.Name.Content == "rm_menu").Layers.Count;
                Assert.True(window.Registry.Execute("room.addLayer"));
                await CommandSweep.Pump(5);
                Assert.Equal(layers + 1, window.Workspaces.Current!.Data.Rooms.Single(r => r.Name.Content == "rm_menu").Layers.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
