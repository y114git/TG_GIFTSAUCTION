using Avalonia;
using G3MLib.DataFile;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>Renders the window with a real game open into PNG files, so the look of the editors can be checked by eye. The pictures go to G3MSTUDIO_SHOTS, or to a temp folder when it is not set.</summary>
public sealed class ScreenshotTests
{
    [Fact]
    public async Task TheMainEditorsRenderToPictures()
    {
        var folder = Corpus.Shots;
        var corpus = Corpus.Folder;
        Directory.CreateDirectory(folder);
        // The first file, smallest first, that has the resource types worth a picture.
        string? file = null;
        foreach (var candidate in Corpus.Files(corpus))
        {
            using var probe = await G3MLib.DataFile.GameMakerIO.ReadAsync(candidate, cancellationToken: TestContext.Current.CancellationToken);
            if (probe.Fonts?.Count > 0 && probe.Paths?.Count > 0 && probe.Rooms.Count > 0) { file = candidate; break; }
        }

        file ??= Corpus.Files(corpus).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Width = 1500;
                window.Height = 950;
                await Task.Delay(300);
                Dispatcher.UIThread.RunJobs();
                Shoot(window, Path.Combine(folder, "00-start.png"));
                await window.OpenAsync(file);
                Shoot(window, Path.Combine(folder, "01-opened.png"));

                var workspace = window.Workspaces.Current!;
                string[] wanted = ["rooms", "sprites", "objects", "sounds", "fonts", "paths", "scripts", "particleSystems", "sequences", "animationCurves", "shaders", "general", "tileSets"];
                foreach (var type in wanted)
                {
                    var item = type switch
                    {
                        "rooms" => workspace.Data.Rooms.OrderByDescending(r => RoomInstanceCommand.Lists(r).Sum(l => l.Count)).FirstOrDefault()?.Name.Content,
                        "sprites" => workspace.Data.Sprites.FirstOrDefault(s => s.Textures.Count > 1)?.Name.Content,
                        "objects" => workspace.Data.GameObjects.FirstOrDefault(o => o.Events.Any(e => e.Count > 0))?.Name.Content,
                        "sounds" => workspace.Data.Sounds.FirstOrDefault()?.Name.Content,
                        "fonts" => workspace.Data.Fonts.FirstOrDefault()?.Name.Content,
                        "paths" => workspace.Data.Paths.FirstOrDefault()?.Name.Content,
                        "particleSystems" => workspace.Data.ParticleSystems?.FirstOrDefault(p => p.Emitters?.Count > 0)?.Name.Content,
                        "sequences" => workspace.Data.Sequences?.FirstOrDefault(q => Enumerable.Range(0, 40).Any(f => Core.Sequences.SequenceEvaluator.At(q, f).Count > 0))?.Name.Content ?? workspace.Data.Sequences?.FirstOrDefault(q => q.Tracks?.Count > 0)?.Name.Content,
                        "animationCurves" => workspace.Data.AnimationCurves?.FirstOrDefault()?.Name.Content,
                        "shaders" => workspace.Data.Shaders?.FirstOrDefault()?.Name.Content,
                        "general" => ResourceTypes.GeneralInfoName,
                        "tileSets" => workspace.Data.Backgrounds.FirstOrDefault(b => b.GMS2TileCount > 0 && b.Texture is not null)?.Name.Content,
                        _ => workspace.Data.Scripts.FirstOrDefault()?.Name.Content,
                    };
                    if (item is null) continue;
                    window.Docking.OpenTab("resource", ResourceTypes.KeyOf(type, item), false);
                    window.Explorer.Select(ResourceTypes.KeyOf(type, item));
                    for (var i = 0; i < 6; i++)
                    {
                        await Task.Delay(150);
                        Dispatcher.UIThread.RunJobs();
                    }

                    if (type == "rooms" && Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Editor.RoomPreview>().FirstOrDefault() is { } roomView)
                    {
                        roomView.View.Tool = Rooms.RoomTool.Place;
                        window.Explorer.Select(ResourceTypes.KeyOf("objects", workspace.Data.GameObjects[0].Name.Content!));
                        await Task.Delay(300);
                        Dispatcher.UIThread.RunJobs();
                    }

                    // A font tab with its first glyph picked shows the kerning pairs under the list.
                    if (type == "fonts" && Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<ListBox>().FirstOrDefault(l => l.Name == "FontGlyphs") is { } glyphs)
                    {
                        glyphs.SelectedIndex = 0;
                        for (var i = 0; i < 4; i++)
                        {
                            await Task.Delay(100);
                            Dispatcher.UIThread.RunJobs();
                        }
                    }

                    // A sequence canvas shows the sprites at the playhead: move it to the first frame that has some.
                    if (type == "sequences" && Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Editor.SequencePlayerView>().FirstOrDefault() is { } player)
                    {
                        var sequence = workspace.Data.Sequences!.First(q => q.Name.Content == item);
                        player.Frame = Enumerable.Range(0, 40).FirstOrDefault(f => Core.Sequences.SequenceEvaluator.At(sequence, f).Count > 0);
                        for (var i = 0; i < 6; i++)
                        {
                            await Task.Delay(150);
                            Dispatcher.UIThread.RunJobs();
                        }
                    }

                    // A particle preview starts empty and steps with a timer; step it so the picture shows particles.
                    if (type == "particleSystems" && Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Editor.ParticlePreviewView>().FirstOrDefault() is { } preview)
                    {
                        for (var i = 0; i < 40; i++) preview.Advance();
                        Dispatcher.UIThread.RunJobs();
                    }

                    Shoot(window, Path.Combine(folder, $"02-{type}.png"));
                }

                // The tag list and a texture page item use the list entry and bounding box views.
                foreach (var (kind, name, picture) in new[] { ("general", ResourceTypes.TagsName, "tags"), ("texturePageItems", ResourceTypes.NameOf(workspace.Data.TexturePageItems[0]), "pageitem") })
                {
                    var key = ResourceTypes.KeyOf(kind, name);
                    window.Docking.OpenTab("resource", key, false);
                    window.Explorer.Select(key);
                    for (var i = 0; i < 6; i++)
                    {
                        await Task.Delay(150);
                        Dispatcher.UIThread.RunJobs();
                    }

                    Shoot(window, Path.Combine(folder, $"02-{picture}.png"));
                }

                // The corpus has no timeline with moments, so one is built in memory (not through the undo history) to see the tab.
                using var shotTimeline = new G3MLib.DataFile.Models.GameMakerTimeline { Name = workspace.Data.Strings.MakeString("tl_screenshot", out _) };
                workspace.Data.Timelines.Add(shotTimeline);
                var firstCode = workspace.Data.Code.First(c => c.Name.Content is not null).Name.Content ?? "";
                new TimelineMomentCommand("tl_screenshot", TimelineMomentCommand.Add, -1, 30).Apply(workspace.Data);
                new TimelineMomentCommand("tl_screenshot", TimelineMomentCommand.Add, -1, 90).Apply(workspace.Data);
                new TimelineActionCommand("tl_screenshot", TimelineActionCommand.Add, 0, -1, firstCode).Apply(workspace.Data);
                window.Docking.OpenTab("resource", ResourceTypes.KeyOf("timelines", "tl_screenshot"), false);
                for (var i = 0; i < 6; i++)
                {
                    await Task.Delay(150);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "02-timelines.png"));
                workspace.Data.Timelines.Remove(shotTimeline);

                // A code entry with GML in it, and the Settings page.
                var codeName = workspace.Data.Code.Where(c => c.Name.Content is { } n && n.Contains("Step_0", StringComparison.Ordinal)).OrderBy(c => c.Name.Content!.Length).First().Name.Content!;
                window.Docking.OpenTab("code", ResourceTypes.KeyOf("code", codeName), false);
                for (var i = 0; i < 40; i++)
                {
                    await Task.Delay(500);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "02-code.png"));
                window.Registry.Execute("window.layout.code");
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "02-code-preset.png"));
                window.Registry.Execute("window.layout.studio");
                window.Registry.Execute("settings.open");
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "02-settings.png"));
                var nav = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<ListBox>().First(l => l.Name == "SettingsNav");
                nav.SelectedIndex = 1; // Colors
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "02-settings-colors.png"));
                Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Button>().First(b => b.Name == "SettingsClose").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                window.Explorer.Select(ResourceTypes.KeyOf("objects", workspace.Data.GameObjects.First(o => o.Sprite is not null).Name.Content!));
                window.Registry.Execute("edit.findUsages");
                for (var i = 0; i < 20; i++)
                {
                    await Task.Delay(300);
                    Dispatcher.UIThread.RunJobs();
                }

                foreach (var panel in (string[])["git", "problems", "output", "usages"])
                {
                    window.Registry.Execute("view.panel." + panel);
                    for (var i = 0; i < 4; i++)
                    {
                        await Task.Delay(150);
                        Dispatcher.UIThread.RunJobs();
                    }

                    Shoot(window, Path.Combine(folder, $"04-panel-{panel}.png"));
                }

                // The tool windows, shown on their own so each picture is only that window.
                var host = new G3MStudio.Core.Plugins.PluginHost(new UserData(dir.File("plugin-user")), new G3MStudio.Core.Commands.CommandRegistry(), () => null, (_, _) => { });
                host.CreateTemplate("Sample Plugin");
                host.Load();
                var room = workspace.Data.Rooms.OrderByDescending(r => RoomInstanceCommand.Lists(r).Sum(l => l.Count)).First();
                Window[] tools = [new StringsWindow(workspace), new PluginsWindow(host, (_, _) => Task.FromResult(true)), new CreationOrderWindow(room, workspace), new OnboardingWindow([.. Localization.Loc.Current.Languages], "en", "ru", true, new OnboardingActions(_ => { }, _ => { }, _ => { }, () => { })), new TourWindow(_ => { }), new RoomOrderWindow(workspace), new AudioEditorWindow("Edit audio of snd_test", ToneWav(), _ => { }), new LayoutsWindow(() => ["Wide editor", "Debug"], _ => null, _ => { }, _ => { }, (_, _) => null)];
                foreach (var tool in tools)
                {
                    tool.Show();
                    for (var i = 0; i < 4; i++)
                    {
                        await Task.Delay(100);
                        Dispatcher.UIThread.RunJobs();
                    }

                    ShootWindow(tool, Path.Combine(folder, $"03-{tool.GetType().Name}.png"));
                    tool.Close();
                }

                // The same window after the accent and two surface colors changed: every control must follow.
                window.Workspaces.Settings.Update(x => x with { Colors = new Dictionary<string, string> { ["Accent"] = "#2F81F7", ["Bg.Window"] = "#20232A", ["Bg.Surface"] = "#1A1D23" } });
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "05-blue-theme.png"));
                // A real right click on an Explorer entry, taken from the window's own frame so the menu that opens is in the picture.
                window.Workspaces.Settings.Update(x => x with { Colors = new Dictionary<string, string>() });
                var row = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window.Explorer).OfType<ListBoxItem>().First(i => i.DataContext is G3MStudio.Core.Workspace.ExplorerRow { IsItem: true });
                var at = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, at, Avalonia.Input.MouseButton.Right);
                Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, at, Avalonia.Input.MouseButton.Right);
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                }

                Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save(Path.Combine(folder, "06-explorer-menu.png"), new PngBitmapEncoderOptions());
                // The completion popup: the code tab in front, two letters typed at the end of the text.
                Avalonia.Headless.HeadlessWindowExtensions.KeyPress(window, Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
                var editor = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<AvaloniaEdit.TextEditor>().First();
                editor.IsReadOnly = false;
                editor.TextArea.Focus();
                editor.CaretOffset = editor.Document.TextLength;
                foreach (var typed in " ins") Avalonia.Headless.HeadlessWindowExtensions.KeyTextInput(window, typed.ToString());
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                }

                Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save(Path.Combine(folder, "07-completion.png"), new PngBitmapEncoderOptions());
                Avalonia.Headless.HeadlessWindowExtensions.KeyPress(window, Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);

                // The menu of a document tab.
                var tab = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Dock.Avalonia.Controls.DocumentTabStripItem>().First();
                var tabAt = tab.TranslatePoint(new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2), window)!.Value;
                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, tabAt, Avalonia.Input.MouseButton.Right);
                Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, tabAt, Avalonia.Input.MouseButton.Right);
                // The simulated click does not reach the tab strip, so the menu is opened directly.
                if (tab.ContextMenu is { IsOpen: false } tabMenu) tabMenu.Open(tab);
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                }

                Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save(Path.Combine(folder, "08-tab-menu.png"), new PngBitmapEncoderOptions());

                // The size the window opens at without a saved place: the bottom edge (tool tabs, status) must not be cut off.
                window.Width = 1100;
                window.Height = 700;
                Dispatcher.UIThread.RunJobs();
                Shoot(window, Path.Combine(folder, "11-default-size.png"));

                // A room at that size: the canvas should fill the room of the tab, not a strip of it.
                var roomName = workspace.Data.Rooms.OrderByDescending(r => RoomInstanceCommand.Lists(r).Sum(l => l.Count)).First().Name.Content!;
                window.Docking.OpenTab("resource", ResourceTypes.KeyOf("rooms", roomName), false);
                for (var i = 0; i < 8; i++)
                {
                    await Task.Delay(150);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "12-room-default-size.png"));
            }
            finally { window.Close(); }
        });
    }

    /// <summary>One second of a 440 Hz tone as 16 bit mono WAV, for the audio editor picture.</summary>
    internal static byte[] ToneWav()
    {
        const int rate = 8000;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + rate * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(rate * 2);
        for (var i = 0; i < rate; i++) writer.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / rate) * 12000 * (1 - i / (double)rate)));
        return stream.ToArray();
    }

    internal static void Shoot(MainWindow window, string path)
    {
        window.UpdateLayout();
        var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
        using var target = new RenderTargetBitmap(size);
        target.Render(window);
        target.Save(path, new PngBitmapEncoderOptions());
    }

    private static void ShootWindow(Window window, string path)
    {
        window.UpdateLayout();
        var size = new PixelSize(Math.Max(1, (int)window.Bounds.Width), Math.Max(1, (int)window.Bounds.Height));
        using var target = new RenderTargetBitmap(size);
        target.Render(window);
        target.Save(path, new PngBitmapEncoderOptions());
    }
}
