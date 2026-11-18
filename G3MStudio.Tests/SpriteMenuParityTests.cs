using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using G3MStudio.Core.Commands;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The Sprite menu offers what the buttons of the sprite editor do, through the same commands; the right-click menus of a sprite keep only Resize Image/Canvas of them.</summary>
public sealed class SpriteMenuParityTests
{
    // Every button of the sprite editor and the command that does the same from the Sprite menu.
    private static readonly Dictionary<string, string> ButtonCommands = new()
    {
        ["SpritePlay"] = "edit.spritePlay",
        ["SpriteLoop"] = "edit.spriteLoop",
        ["SpriteOnion"] = "edit.spriteOnion",
        ["SpriteLockOrigin"] = "edit.spriteLockOrigin",
        ["SpriteFrameAdd"] = "edit.addFrame",
        ["SpriteFrameDuplicate"] = "edit.duplicateFrame",
        ["SpriteFrameDelete"] = "edit.removeFrame",
        ["SpriteFrameImport"] = "edit.importSheet",
        ["SpriteEditImage"] = "edit.editImage",
        ["SpriteResize"] = "edit.resizeSprite",
    };

    private static readonly string[] Toggles = ["edit.spritePlay", "edit.spriteLoop", "edit.spriteOnion", "edit.spriteLockOrigin"];

    [Fact]
    public async Task EveryButtonOfTheSpriteEditorHasACommandInTheSpriteMenuAndTheRightClickMenusLeaveOutTheToggles()
    {
        using var dir = new TempDir();
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("resource", "sprites:spr_a", false);
                await CommandSweep.Until(() => window.Registry.HasContext(AppCommands.SpriteTab));
                await CommandSweep.Pump(5);
                var frames = window.GetVisualDescendants().OfType<SpriteFramesView>().Single();
                var buttons = frames.GetVisualDescendants().OfType<Button>().Where(b => b.Name is { Length: > 0 } && !b.Name.StartsWith("PART_", StringComparison.Ordinal)).Select(b => b.Name!).Distinct().ToList();
                Assert.NotEmpty(buttons);
                var menu = AppCommands.Menus.Single(m => m.TitleKey == "menu.sprite").Ids;
                var explorer = window.ResourceMenuFor("sprites:spr_a").OfType<MenuItem>().Select(i => i.Header as string).ToList();
                foreach (var name in buttons)
                {
                    Assert.True(ButtonCommands.TryGetValue(name, out var id), $"the sprite editor button {name} has no command in the table of this test");
                    Assert.Contains(id, menu);
                    Assert.NotNull(window.Registry.Find(id));
                }

                // The Explorer menu and the tab menu add Resize Image/Canvas; playback, loop, onion skin and origin lock stay in the Sprite menu.
                var tab = window.TabMenu(window.Docking.ActiveTabId!).Items.OfType<MenuItem>().Select(i => i.Header as string).ToList();
                foreach (var list in new[] { explorer, tab })
                {
                    Assert.Contains(Loc.Current[AppCommands.TitleKey("edit.resizeSprite")], list);
                    foreach (var toggle in Toggles) Assert.DoesNotContain(Loc.Current[AppCommands.TitleKey(toggle)], list);
                }

                // The menu entry runs the button's code: the toggle flips.
                var onion = frames.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "SpriteOnion");
                Assert.True(window.Registry.Execute("edit.spriteOnion"));
                Assert.True(onion.IsChecked);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
