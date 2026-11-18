using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class RoomKeysTests
{
    [Fact]
    public async Task CtrlVOnTheRoomCanvasIsNotTakenByAnImageCommand()
    {
        using var dir = new TempDir();
        string path;
        using (var data = CopyResourcesTests.Source())
        {
            new EditHistory(data).Execute(ResourceTemplates.New(data, "rooms", "rm_a"));
            path = TestData.Write(dir.File("data.win"), data);
        }

        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("resource", "rooms:rm_a", false);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var preview = window.GetVisualDescendants().OfType<RoomPreview>().Single();
                await preview.Ready;
                var view = preview.View;
                var seen = new List<string>();
                view.CopyRequested += _ => seen.Add("copy");
                view.PasteRequested += _ => seen.Add("paste");
                view.DuplicateRequested += (_, _) => seen.Add("duplicate");
                view.Focus();
                Dispatcher.UIThread.RunJobs();
                var keys = new[] { (Key.V, PhysicalKey.V, "v", "paste") };
                foreach (var (key, physical, text, name) in keys)
                {
                    window.KeyPress(key, HeadlessApp.Command, physical, text);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Contains(name, seen);
                }
            }
            finally { window.Close(); }
        });
    }
}
