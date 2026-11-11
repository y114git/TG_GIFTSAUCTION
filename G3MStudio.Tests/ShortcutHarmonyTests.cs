using System.Text.RegularExpressions;
using Avalonia.Input;
using Avalonia.Threading;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The default keymap follows the grouping rules of design/shell.md section 3.4 (one family per F-key, Shift reverses or stops).</summary>
public sealed partial class ShortcutHarmonyTests
{
    [GeneratedRegex(@"^(?:(?:Ctrl|Alt|Shift)\+)*F(\d+)$")]
    private static partial Regex FunctionKey();

    /// <summary>Command ids that may use each F-key, as a prefix or a full id.</summary>
    private static readonly Dictionary<int, string[]> Families = new()
    {
        [1] = ["help.", "code.helpOnWord"],
        [2] = ["edit.rename", "code.toggleBookmark", "code.nextBookmark", "code.previousBookmark"],
        [5] = ["run.", "room.play"],
        [6] = ["view.nextRegion", "view.prevRegion"],
        [8] = ["code.nextProblem", "code.previousProblem"],
        [11] = ["view.fullscreen"],
        [12] = ["code.goToDefinition", "code.peekDefinition", "edit.findUsages"],
    };

    private static readonly string[] RegionKeys = ["view.region.menu", "panel.explorer", "panel.inspector", "view.region.status"];

    private static string Canonical(string gesture) => string.Join('+', gesture.Split('+').Order(StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void NoTwoDefaultsShareAGestureInOneContext()
    {
        var registry = AppCommands.Create(new FakeShell());
        foreach (var command in registry.All)
        {
            foreach (var gesture in command.Gestures)
            {
                Assert.NotNull(Shortcuts.Parse(gesture));
                var clash = registry.All.Where(o => o.Id != command.Id && (o.When is null || command.When is null || o.When == command.When) && o.Gestures.Select(Canonical).Contains(Canonical(gesture), StringComparer.OrdinalIgnoreCase));
                Assert.True(!clash.Any(), $"{gesture} on {command.Id} also on {string.Join(", ", clash.Select(c => c.Id))}");
            }
        }
    }

    [Fact]
    public void EveryFunctionKeyBelongsToOneFamilyAndShiftReversesOrStops()
    {
        var registry = AppCommands.Create(new FakeShell());
        string[] reverses = ["run.stop", "view.prevRegion", "code.previousProblem", "edit.findUsages"];
        foreach (var command in registry.All)
        {
            foreach (var gesture in command.Gestures)
            {
                if (FunctionKey().Match(gesture) is not { Success: true } key) continue;
                var number = int.Parse(key.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(Families.TryGetValue(number, out var family), $"F{number} has no family ({command.Id})");
                Assert.True(family!.Any(f => command.Id.StartsWith(f, StringComparison.Ordinal)), $"{gesture} on {command.Id} is outside the F{number} family");
                if (gesture.StartsWith("Shift+", StringComparison.Ordinal)) Assert.Contains(command.Id, reverses);
            }
        }
    }

    [Fact]
    public void StopIsOnShiftF5WithTheOldKeyKeptAsASelectableAlias()
    {
        var registry = AppCommands.Create(new FakeShell());
        Assert.Equal("Shift+F5", registry.Get("run.stop").Shortcut);
        Assert.Equal("F5", registry.Get("run.game").Shortcut);
        Assert.Equal("F6", registry.Get("view.nextRegion").Shortcut);
        Assert.Equal("Shift+F6", registry.Get("view.prevRegion").Shortcut);
        // The old key is not bound by default (F6 has a new owner), but adding it as an alias in Settings is allowed and the clash is reported.
        Assert.DoesNotContain("F6", registry.Get("run.stop").Gestures);
        registry.SetAliases("run.stop", ["F6"]);
        Assert.Contains("F6", registry.Get("run.stop").Gestures);
        Assert.Equal(["view.nextRegion"], registry.UsersOf("F6", "run.stop").Select(c => c.Id));
    }

    [Fact]
    public void AUserOverrideOfTheStopKeyStillWins()
    {
        var registry = AppCommands.Create(new FakeShell());
        registry.SetShortcut("run.stop", "F6"); // what an old settings file holds
        Assert.Equal("F6", registry.Get("run.stop").Shortcut);
        Assert.Equal("Shift+F5", registry.DefaultShortcut("run.stop"));
    }

    [Fact]
    public void TheRegionCommandsStepTheShell()
    {
        var shell = new FakeShell();
        var registry = AppCommands.Create(shell);
        registry.Execute("view.nextRegion");
        registry.Execute("view.prevRegion");
        Assert.Equal([1, -1], shell.RegionSteps);
    }

    [Fact]
    public async Task F6CyclesTheRegionsAndShiftF6GoesBack()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.OpenStartupFile([Core.Links.StudioLink.ForFile(path)]);
                for (var i = 0; i < 100 && window.Workspaces.Current is null; i++)
                {
                    await Task.Delay(50);
                    Dispatcher.UIThread.RunJobs();
                }

                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                string Step(string id)
                {
                    Assert.True(window.Registry.Execute(id));
                    Dispatcher.UIThread.RunJobs();
                    return window.RegionAnnouncement.Text ?? "";
                }

                var seen = new List<string> { Step("view.nextRegion") };
                while (seen.Count < 10 && (seen.Count == 1 || seen[^1] != seen[0])) seen.Add(Step("view.nextRegion"));
                Assert.Equal(seen[0], seen[^1]); // wrapped around
                Assert.True(seen.Count > 3);
                Assert.Equal(seen.Count - 1, seen.Distinct().Count());
                foreach (var key in RegionKeys)
                    Assert.Contains(Loc.Current.Format("view.regionMoved", ("region", Loc.Current[key])), seen);
                Assert.NotNull(window.FocusManager?.GetFocusedElement());
                Assert.Equal(seen[^2], Step("view.prevRegion")); // one step back from the first region is the last one
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void AUserKeyThatIsAlsoAnotherDefaultIsReportedAndWinsTheKeyPress()
    {
        var registry = AppCommands.Create(new FakeShell());
        Assert.Empty(registry.OverrideClashes());
        registry.SetShortcut("run.stop", "F6"); // view.nextRegion has F6 by default
        var (user, other, gesture) = Assert.Single(registry.OverrideClashes());
        Assert.Equal(("run.stop", "view.nextRegion", "F6"), (user.Id, other.Id, gesture));
        Assert.True(registry.IsOverridden("run.stop"));
        Assert.False(registry.IsOverridden("view.nextRegion"));
    }

    [Fact]
    public void AUserKeyWinsOnlyAsAPrimaryShortcutNotAsAnAlias()
    {
        var registry = AppCommands.Create(new FakeShell());
        registry.SetShortcut("run.stop", "F9");
        registry.SetAliases("run.stop", ["F6"]); // view.nextRegion has F6 as its primary
        static KeyEventArgs Press(Key key) => new() { RoutedEvent = InputElement.KeyDownEvent, Key = key };
        Assert.Equal("view.nextRegion", Shortcuts.Match(registry, Press(Key.F6))?.Id);
        Assert.Equal("run.stop", Shortcuts.Match(registry, Press(Key.F9))?.Id);
    }

    [Fact]
    public void PunctuationKeysAreShownAsTheCharacter()
    {
        Assert.Equal("Ctrl+,", Shortcuts.Display("Ctrl+OemComma", false));
        Assert.Equal("Ctrl+Shift+P", Shortcuts.Display("Ctrl+Shift+P", false));
        Assert.Equal("", Shortcuts.Display(null));
        Assert.Equal("Cmd+Shift+,", Shortcuts.Display("Ctrl+Shift+OemComma", true));
        Assert.Equal("Ctrl+Shift+,", Shortcuts.Display("Ctrl+Shift+OemComma", false));
    }

    [Fact]
    public void NumpadAndNamedKeysAreShownAsReadableNames()
    {
        Assert.Equal("Ctrl+Num +", Shortcuts.Display("Ctrl+Add", false));
        Assert.Equal("Ctrl+Num -", Shortcuts.Display("Ctrl+Subtract", false));
        Assert.Equal("Num 5", Shortcuts.Display("NumPad5", false));
        Assert.Equal("Backspace", Shortcuts.Display("Back", false));
        Assert.Equal("Enter", Shortcuts.Display("Return", false));
        Assert.Equal("Ctrl+.", Shortcuts.Display("Ctrl+OemPeriod", false));
    }
}
