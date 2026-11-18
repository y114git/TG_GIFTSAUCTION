#nullable disable
#pragma warning disable CA2000, CA5394, CA1849, IDE0305
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

/// <summary>
/// Opens one tab of every editor type on a generated fixture, then fires random keys, clicks, commands, tab closes, language and animation switches at the window.
/// Nothing may throw out of the UI thread, nothing may block it, and the window must stay usable. The seed is in the failure text; G3MSTUDIO_FUZZ_STEPS sets the length.
/// </summary>
public sealed class FuzzTests(ITestOutputHelper output)
{
    private static readonly Key[] Keys =
    [
        Key.Z, Key.Y, Key.X, Key.C, Key.V, Key.A, Key.Escape, Key.Enter, Key.Tab, Key.Delete, Key.Back, Key.Home, Key.End, Key.PageUp, Key.PageDown,
        Key.Left, Key.Right, Key.Up, Key.Down, Key.Space, Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12,
        Key.D0, Key.D5, Key.A, Key.B, Key.OemMinus, Key.OemPlus, Key.Add, Key.Subtract,
    ];

    private static readonly RawInputModifiers[] Mods =
    [
        RawInputModifiers.None, RawInputModifiers.None, RawInputModifiers.Control, RawInputModifiers.Control, RawInputModifiers.Shift,
        RawInputModifiers.Alt, RawInputModifiers.Control | RawInputModifiers.Shift,
    ];

    // Ends the application or replaces the file under the fuzz, so they are not drawn.
    private static readonly string[] Skipped = [.. CommandSweep.Special, "file.revert", "run.game", "run.restart", "run.withRuntime"];

    private static int Steps => int.TryParse(Environment.GetEnvironmentVariable("G3MSTUDIO_FUZZ_STEPS"), out var n) ? n : 150;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RandomInputOnEveryEditorTypeThrowsNothingAndBlocksNothing(int seed)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        var random = new Random(seed);
        var path = Path.Combine(Corpus.Fixtures, "deltarune", "data.win");
        var log = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                await window.OpenAsync(path);
                await CommandSweep.Pump(3);
                window.Width = 1500;
                window.Height = 900;
                OpenOneOfEach(window, probe);
                for (var step = 0; step < Steps && window.IsVisible; step++)
                {
                    var what = Act(window, probe, random);
                    log.Add(what);
                    probe.Where = $"seed {seed} step {step} {what}";
                    await CommandSweep.Pump();
                    await CommandSweep.CloseDialogs(window, probe);
                    // A click can land on the Close item of the open File menu: the file is gone, so the run goes on with it opened again.
                    if (window.Workspaces.Current is null) await window.OpenAsync(path);
                    if (window.Docking.Documents().Count() < 3) OpenOneOfEach(window, probe);
                }

                output.WriteLine($"seed {seed}: {log.Count} steps, {window.Docking.Documents().Count()} tabs open at the end");
            }
            finally
            {
                Motion.TimedInHeadless = false;
                Motion.SetLevel(MotionPolicy.Medium);
                Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
        SweepProbe.Flush();
        var errors = probe.Errors.Where(e => !e.Contains(nameof(ResourceLockedException), StringComparison.Ordinal)).ToList();
        Assert.True(errors.Count == 0, $"seed {seed}, last steps: {string.Join(" | ", log.TakeLast(5))}\n{string.Join("\n", errors.Take(5))}");
    }

    private static void OpenOneOfEach(MainWindow window, SweepProbe probe)
    {
        var data = window.Workspaces.Current.Data;
        foreach (var type in ResourceTypes.All)
        {
            if (type.Key is "strings" or "variables" or "functions" or "codeLocals") continue;
            var item = type.Items(data)?.FirstOrDefault();
            if (item is null) continue;
            var key = ResourceTypes.KeyOf(type.Key, ResourceTypes.NameOf(item));
            probe.Where = "open " + key;
            CommandSweep.Step(probe, probe.Where, () => window.Docking.OpenTab(type.Key == "code" ? "code" : "resource", key, false));
        }
    }

    private static string Act(MainWindow window, SweepProbe probe, Random random)
    {
        var roll = random.Next(100);
        string what;
        Action action;
        switch (roll)
        {
            case < 45:
                {
                    var (key, mods) = (Keys[random.Next(Keys.Length)], Mods[random.Next(Mods.Length)]);
                    what = $"key {mods}+{key} in {Focus(window, random)}";
                    action = () =>
                    {
                        window.KeyPress(key, mods, PhysicalKey.None, null);
                        window.KeyRelease(key, mods, PhysicalKey.None, null);
                    };
                    break;
                }

            case < 55:
                {
                    var text = new[] { "a", "7", "-", ".", "é", "漢", "{", " ", "\t" }[random.Next(9)];
                    what = $"type '{text}' in {Focus(window, random)}";
                    action = () => window.KeyTextInput(text);
                    break;
                }

            case < 80:
                {
                    window.UpdateLayout();
                    var target = Pick<Visual>(window, v => v is Control { IsEffectivelyVisible: true, Bounds: { Width: > 2, Height: > 2 } }, random);
                    what = "click " + (target?.GetType().Name ?? "nothing");
                    var right = random.Next(5) == 0;
                    action = () =>
                    {
                        if (target is not Control control || control.TranslatePoint(new Point(control.Bounds.Width * random.NextDouble(), control.Bounds.Height * random.NextDouble()), window) is not { } at) return;
                        var button = right ? MouseButton.Right : MouseButton.Left;
                        window.MouseDown(at, button);
                        if (random.Next(4) == 0) window.MouseMove(at + new Point(random.Next(-80, 80), random.Next(-80, 80)));
                        window.MouseUp(at, button);
                    };
                    break;
                }

            case < 90:
                {
                    var commands = window.Registry.All.Where(c => !Skipped.Contains(c.Id) && window.Registry.IsEnabled(c)).ToList();
                    var command = commands[random.Next(commands.Count)];
                    what = "command " + command.Id;
                    action = () => window.Registry.Execute(command.Id);
                    break;
                }

            case < 94:
                {
                    var tabs = window.Docking.Tabs.Tabs.ToList();
                    var tab = tabs.Count == 0 ? null : tabs[random.Next(tabs.Count)];
                    what = "close tab " + tab?.Id;
                    action = () => { if (tab is not null) window.Docking.CloseTab(tab.Id); };
                    break;
                }

            case < 97:
                {
                    var codes = Loc.Current.Languages.Keys.ToList();
                    var code = codes[random.Next(codes.Count)];
                    what = "language " + code;
                    action = () => Loc.Current.SetLanguage(code);
                    break;
                }

            default:
                {
                    var level = MotionPolicy.Levels[random.Next(MotionPolicy.Levels.Length)];
                    var timed = random.Next(2) == 0;
                    what = $"animations {level} timed {timed}";
                    action = () =>
                    {
                        Motion.TimedInHeadless = timed;
                        window.Workspaces.Settings.Update(s => s with { Animations = level });
                    };
                    break;
                }
        }

        CommandSweep.Step(probe, what, action);
        return what;
    }

    /// <summary>Gives the keyboard focus to a random control of the kinds the editors are made of, and names it.</summary>
    private static string Focus(MainWindow window, Random random)
    {
        var kinds = new Func<Visual, bool>[]
        {
            v => v is TextBox, v => v is NumericUpDown, v => v is TextEditor, v => v is ListBox, v => v is TreeView,
            v => v is Control { Focusable: true } and not (TextBox or Button), // canvases and custom views
        };
        window.UpdateLayout();
        var target = Pick<Visual>(window, v => v is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } && kinds[random.Next(kinds.Length)](v), random);
        if (target is Control { Focusable: true } control) control.Focus();
        return target?.GetType().Name ?? "nothing";
    }

    private static T Pick<T>(Visual root, Func<T, bool> test, Random random) where T : class
    {
        var all = root.GetVisualDescendants().OfType<T>().Where(test).ToList();
        return all.Count == 0 ? null : all[random.Next(all.Count)];
    }

    [Fact]
    public async Task ManyTabsOpenedAndClosedLeaveNothingBehind()
    {
        using var dir = new TempDir();
        var path = Path.Combine(Corpus.Fixtures, "deltarune", "data.win");
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            await window.OpenAsync(path);
            await CommandSweep.Pump(3);
            var weak = new List<WeakReference>();
            var data = window.Workspaces.Current.Data;
            var items = ResourceTypes.All.Where(t => t.Key is not ("strings" or "variables" or "functions" or "codeLocals")).SelectMany(t => (t.Items(data) ?? []).Take(2).Select(i => (t, i))).ToList();
            var watch = Stopwatch.StartNew();
            for (var round = 0; round < 30; round++)
            {
                var (type, item) = items[round % items.Count];
                var key = ResourceTypes.KeyOf(type.Key, ResourceTypes.NameOf(item));
                var tab = window.Docking.OpenTab(type.Key == "code" ? "code" : "resource", key, false);
                Dispatcher.UIThread.RunJobs();
                if (tab is not null && window.Docking.DocumentOf(tab.Id) is { } document) weak.Add(new WeakReference(document.Content));
                if (tab is not null) window.Docking.CloseTab(tab.Id);
                Dispatcher.UIThread.RunJobs();
            }

            output.WriteLine($"30 tabs opened and closed in {watch.Elapsed.TotalMilliseconds:F0} ms");
            for (var i = 0; i < 4; i++)
            {
                await Task.Delay(30);
                Dispatcher.UIThread.RunJobs();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            var alive = weak.Count(w => w.IsAlive);
            window.Workspaces.Close();
            window.Close();
            Assert.True(alive <= 3, $"{alive} of {weak.Count} closed tabs are still reachable");
        });
    }
}
