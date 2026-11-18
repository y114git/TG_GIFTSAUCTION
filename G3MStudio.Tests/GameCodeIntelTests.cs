using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using G3MStudio.Commands;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Editor;
using G3MStudio.Panels;

namespace G3MStudio.Tests;

public sealed class GameCodeIntelTests
{
    private const string Declared = "/// Adds two numbers.\n/// @param {real} a The first one\n/// @param [b] The second one\n/// @returns {real} The sum\nfunction add(a, b = 1)\n{\n    return a + b;\n}\nvar total = add(1, 2);\n";

    [Fact]
    public void ADocCommentGivesTheDescriptionParameterTypesAndTheReturnType()
    {
        var info = DocComment.Before(Declared, Declared.IndexOf("function", StringComparison.Ordinal))!;
        Assert.Equal("Adds two numbers.", info.Summary);
        Assert.Equal("real", info.Returns);
        Assert.Equal(new DocParam("a", "real", "The first one", false), info.Parameters[0]);
        Assert.Equal(new DocParam("b", "", "The second one", true), info.Parameters[1]);
        Assert.Null(DocComment.Before("var x = 1;\nfunction f() {}", 11));
    }

    [Fact]
    public void AGameFunctionIsDescribedLikeABuiltInOne()
    {
        var entry = GameFunctions.FromDoc(GmlDoc.Parse(Declared), "add", "code:probe")!;
        Assert.True(entry.Game);
        Assert.Equal("add(a, [b])", entry.Signature);
        Assert.Equal("real", entry.Parameters[0].Type);
        var text = GmlHelp.Describe(entry, "add");
        Assert.Contains("add(a, [b])  ->  real", text, StringComparison.Ordinal);
        Assert.Contains("a: real - The first one", text, StringComparison.Ordinal);
        Assert.Contains("code: probe", text, StringComparison.Ordinal);
        Assert.Null(GameFunctions.FromDoc(GmlDoc.Parse("var add = 1;"), "add", null));
    }

    [Fact]
    public void TheVariableCardReadsTheTypeFromTheFirstAssignment()
    {
        var doc = GmlDoc.Parse("global.score = 0;\nvar name = \"x\";\nvar list = [1];\nvar o = new Thing();\n");
        Assert.Equal("real", GmlHover.ValueType(doc, GmlHover.FirstAssignment(doc, "score", global: true)));
        Assert.Equal("string", GmlHover.ValueType(doc, GmlHover.FirstAssignment(doc, "name", global: false)));
        Assert.Equal("array", GmlHover.ValueType(doc, GmlHover.FirstAssignment(doc, "list", global: false)));
        Assert.Equal("Thing", GmlHover.ValueType(doc, GmlHover.FirstAssignment(doc, "o", global: false)));
        Assert.Equal(-1, GmlHover.FirstAssignment(doc, "score", global: false));
    }

    [Fact]
    public async Task HoverShowsCardsForGameFunctionsVariablesAndBuiltIns()
    {
        await CodeIntelUiTests.WithTab(async (tab, _, _) =>
        {
            await CodeIntelUiTests.Settle(() => tab.Doc.Tokens.Count > 0 && ((IOverlayHost)tab).FreshDoc is not null);
            var call = tab.HoverTextAt(tab.Text.LastIndexOf("add", StringComparison.Ordinal) + 1) ?? "";
            Assert.Contains("add(a, [b])", call, StringComparison.Ordinal);
            Assert.Contains("Adds two numbers.", call, StringComparison.Ordinal);
            Assert.Contains("2 uses in this code", call, StringComparison.Ordinal);
            var variable = tab.HoverTextAt(tab.Text.IndexOf("total", StringComparison.Ordinal) + 1) ?? "";
            Assert.Contains("local variable total", variable, StringComparison.Ordinal);
            Assert.Contains("line 9", variable, StringComparison.Ordinal);
            Assert.Contains("keyword return", tab.HoverTextAt(tab.Text.IndexOf("return a", StringComparison.Ordinal) + 1) ?? "", StringComparison.Ordinal);
            Assert.Null(tab.HoverTextAt(tab.Text.IndexOf("1, 2", StringComparison.Ordinal)));
        }, Declared);
    }

    [Fact]
    public async Task CtrlSAppliesPendingChangesAndOtherwiseLeavesTheKeyAlone()
    {
        await CodeIntelUiTests.WithTab((tab, _, _) =>
        {
            var button = tab.GetVisualDescendants().OfType<Button>().First(b => b.Name == "Apply");
            Assert.Equal("Save changes", button.Content);
            Assert.Contains(Shortcuts.Display("Ctrl+S"), ToolTip.GetTip(button)?.ToString(), StringComparison.Ordinal);
            Assert.True(tab.Apply());
            bool press()
            {
                var e = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = Shortcuts.Command };
                tab.Editor.TextArea.RaiseEvent(e);
                return e.Handled;
            }

            Assert.False(press()); // nothing to apply: the window saves the file
            tab.Editor.Text = "global.total = 2;\n";
            Assert.True(tab.HasPending);
            Assert.True(press());
            Assert.False(tab.HasPending);
            return Task.CompletedTask;
        }, "global.total = 1;\n");
    }

    [Fact]
    public async Task TheCodeSaveKeyFollowsTheGestureBoundToFileSave()
    {
        await CodeIntelUiTests.WithTab((tab, _, _) =>
        {
            var registry = G3MStudio.Core.Commands.AppCommands.Create(new FakeShell());
            registry.SetShortcut("file.save", "Ctrl+Alt+S");
            tab.Registry = registry;
            var button = tab.GetVisualDescendants().OfType<Button>().First(b => b.Name == "Apply");
            Assert.Contains(Shortcuts.Display("Ctrl+Alt+S"), ToolTip.GetTip(button)?.ToString(), StringComparison.Ordinal);
            bool press(KeyModifiers modifiers)
            {
                var e = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = modifiers };
                tab.Editor.TextArea.RaiseEvent(e);
                return e.Handled;
            }

            tab.Editor.Text = "global.total = 2;\n";
            Assert.False(press(Shortcuts.Command));
            Assert.True(press(Shortcuts.Command | KeyModifiers.Alt));
            return Task.CompletedTask;
        }, "global.total = 1;\n");
    }

    [Fact]
    public void StarsKeepFavouritesAndTheListPutsThemFirstInTheirOwnCategory()
    {
        var before = EditorOptions.Favourites.ToList();
        try
        {
            EditorOptions.SetFavourites([]);
            EditorOptions.ToggleFavourite("draw_text");
            Assert.True(EditorOptions.IsFavourite("draw_text"));
            EditorOptions.ToggleFavourite("draw_text");
            Assert.False(EditorOptions.IsFavourite("draw_text"));
            EditorOptions.SetHoverDelay(9000);
            Assert.Equal(EditorOptions.MaxHoverDelayMs, EditorOptions.HoverDelayMs);
            EditorOptions.SetHoverDelay(1000);
        }
        finally
        {
            EditorOptions.SetFavourites(before);
        }
    }

    [Fact]
    public async Task TheReferencePanelListsFavouritesAndGameFunctionsInTheirOwnCategories()
    {
        var before = EditorOptions.Favourites.ToList();
        await HeadlessApp.Run(() =>
        {
            var game = new RefEntry("scr_hit", RefKind.Function, GameFunctions.Category, "", [new RefParam("", "", "", false, true)], "") { Game = true, Where = "scripts:scr_hit" };
            var full = new RefEntry("scr_hit", RefKind.Function, GameFunctions.Category, "Hits it.", [new RefParam("power", "real", "How hard", false, false)], "") { Game = true, Where = "scripts:scr_hit" };
            var panel = new ReferencePanel(GmlReference.Default) { GameSource = () => [game], GameDetail = _ => full };
            var window = new Window { Content = panel, Width = 600, Height = 600 };
            window.Show();
            try
            {
                EditorOptions.SetFavourites([]);
                T Find<T>(string name) where T : Control => panel.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
                var category = Find<ComboBox>("ReferenceCategory");
                Assert.Equal(["Favourites", "Game functions"], category.Items.OfType<string>().Skip(1).Take(2));
                category.SelectedIndex = 2;
                Assert.Equal(["scr_hit"], panel.Shown.Select(e => e.Name));
                panel.Show("scr_hit");
                Assert.Contains("scr_hit(power)", panel.DetailText, StringComparison.Ordinal);
                Assert.Contains("power: real - How hard", panel.DetailText, StringComparison.Ordinal);
                string? inserted = null;
                panel.InsertRequested += t => inserted = t;
                Find<Button>("ReferenceInsert").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("scr_hit(power)", inserted);
                Find<Button>("ReferenceStar").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.True(EditorOptions.IsFavourite("scr_hit"));
                EditorOptions.ToggleFavourite("string_length");
                panel.Show("");
                category.SelectedIndex = 1;
                Assert.Equal(["scr_hit", "string_length"], panel.Shown.Select(e => e.Name).Order(StringComparer.Ordinal));
            }
            finally
            {
                EditorOptions.SetFavourites(before);
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheReferencePanelListsGameFunctionsFirstAndOnlyThisFileKeepsTheNamesTheCodeUses()
    {
        await HeadlessApp.Run(() =>
        {
            var game = new RefEntry("scr_hit", RefKind.Function, GameFunctions.Category, "", [new RefParam("", "", "", false, true)], "") { Game = true, Where = "scripts:scr_hit" };
            var other = game with { Name = "scr_other" };
            var code = "scr_hit(1); var n = string_length(\"a\");";
            var panel = new ReferencePanel(GmlReference.Default) { GameSource = () => [game, other], CurrentCode = () => code };
            var window = new Window { Content = panel, Width = 600, Height = 600 };
            window.Show();
            try
            {
                Assert.Equal(["scr_hit", "scr_other"], panel.Shown.Take(2).Select(e => e.Name)); // game functions are in the first list too
                panel.OnlyThisFile = true;
                Assert.Equal(["scr_hit", "string_length"], panel.Shown.Select(e => e.Name).Order(StringComparer.Ordinal));
                code = "scr_other();";
                panel.RefreshForCode();
                Assert.Equal(["scr_other"], panel.Shown.Select(e => e.Name));
                panel.OnlyThisFile = false;
                Assert.True(panel.Shown.Count > 2);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheReferencePanelListsGameFunctionsWithTheirArgumentsAndFindsUsagesAndDefinitions()
    {
        await HeadlessApp.Run(() =>
        {
            var game = new RefEntry("scr_hit", RefKind.Function, GameFunctions.Category, "", [new RefParam("", "", "", false, true)], "") { Game = true, Where = "code:gml_GlobalScript_scr_hit" };
            var full = new RefEntry("scr_hit", RefKind.Function, GameFunctions.Category, "Hits it.", [new RefParam("power", "real", "How hard", false, false)], "") { Game = true, Where = "code:gml_GlobalScript_scr_hit" };
            var panel = new ReferencePanel(GmlReference.Default) { GameSource = () => [game], GameDetail = _ => full };
            var window = new Window { Content = panel, Width = 360, Height = 400 };
            window.Show();
            try
            {
                T Find<T>(string name) where T : Control => panel.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
                Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "scr_hit(power)"); // the row shows the real argument, not (...)
                string? usages = null;
                (string, string)? definition = null;
                panel.FindUsagesRequested += name => usages = name;
                panel.DefinitionRequested += (where, name) => definition = (where, name);
                panel.Show("scr_hit");
                Assert.True(Find<Button>("ReferenceDefinition").IsVisible);
                Find<Button>("ReferenceUsages").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Find<Button>("ReferenceDefinition").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("scr_hit", usages);
                Assert.Equal(("code:gml_GlobalScript_scr_hit", "scr_hit"), definition);
                panel.Show("string_length"); // a built-in has usages but no definition to go to
                Assert.False(Find<Button>("ReferenceDefinition").IsVisible);
                Assert.True(Find<Button>("ReferenceUsages").IsEnabled);
                // The header never lets the controls touch: the search box is capped and the category and the filter keep a gap.
                var (search, category, only) = (Find<TextBox>("ReferenceSearch"), Find<ComboBox>("ReferenceCategory"), Find<CheckBox>("ReferenceOnlyFile"));
                Assert.InRange(search.Bounds.Width, 1, 281.5);
                Assert.True(only.Bounds.Y >= category.Bounds.Bottom || only.Bounds.X - category.Bounds.Right >= 8);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
