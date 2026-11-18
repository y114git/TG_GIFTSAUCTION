using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>Code the check used to misread: comments between tokens, literals the tokenizer splits, accessors, locals of other functions.</summary>
public sealed class GmlLintEdgeTests
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "show_debug_message", "draw_text", "draw_set_colour", "ds_grid_create", "ds_map_create", "x", "y", "real", "string", "obj_enemy", "image_blend", "speed",
    };

    private const string Commented = """
        /// @description Spawns a wave
        // Settings
        #macro WAVE_SIZE 4 // per wave
        enum Phase {
            idle, // waiting
            run, /* moving */
            done // finished
        }

        function Wave(_size) constructor {
            size = _size; // how many
            static count = 0; // shared
            static spawn = function() { // one more
                count += 1;
                return count;
            };
        }

        function make_wave(n) {
            // a struct with comments
            var info = {
                hp: 10, // health
                mp: 5, /* mana */
                tint: #00FF00 // green
            };
            var grid = ds_grid_create(2, 2); // cells
            var map = ds_map_create();
            map[? "k"] = 1; // key
            grid[# 0, 1] = info.hp;
            var wave = new Wave(n); // the wave
            with (obj_enemy) { // every enemy
                // reset
                image_blend = $FF00FF;
                speed = 0;
            }
            switch (wave.size) {
                case 1: return Phase.idle; // one
                case 2: // two
                    return Phase.run;
                default:
                    break;
            }
            draw_set_colour(#FF8000); // orange
            draw_text(x, map[? "k"], string(grid[# 0, 1] + 0x10)); // done
            return Phase.done;
        }

        // go
        make_wave(WAVE_SIZE);
        """;

    [Fact]
    public void RealisticCommentedCodeHasNoFindings()
    {
        Assert.DoesNotContain(LiveCheck.Parse(Commented), d => d.Severity == G3MLib.Analyzer.Compiler.DiagnosticSeverity.Error);
        Assert.Empty(GmlLint.Analyze(GmlDoc.Parse(Commented), Context, TestContext.Current.CancellationToken).Select(i => $"{i.Diagnostic.Code} {Commented[i.Diagnostic.Span.StartOffset..i.Diagnostic.Span.EndOffset]}"));
    }

    private static readonly GmlLintContext Context = new(
        Known.Contains,
        name => name == "draw_text" ? (3, 3) : name == "draw_set_colour" ? (1, 1) : null,
        _ => false,
        _ => false,
        Known,
        _ => false,
        _ => false,
        _ => null,
        _ => false);

    private static IReadOnlyList<string> Codes(string text, GmlLintContext? context = null) => [.. GmlLint.Analyze(GmlDoc.Parse(text), context ?? Context).Select(i => i.Diagnostic.Code)];

    [Theory]
    [InlineData("function f() { return 1; // done\n}\nf();")]
    [InlineData("function f() { return 1; /* done */ }\nf();")]
    [InlineData("switch (x) {\n    case 1: break; // one\n    case 2: break;\n}")]
    [InlineData("enum E {\n    A, // idle\n    B /* busy */\n}\nx = E.B;")]
    [InlineData("var s = { hp: 1, // health\n    mp: 2 };\nshow_debug_message(s);")]
    [InlineData("// setup\ntimer = 0;\nshow_debug_message(timer);\ntimer = 1;")]
    [InlineData("if (x > 0) // only right\n{\n    y = 1;\n}")]
    [InlineData("draw_text(x, // left\n    y, \"a\");")]
    public void CommentsBetweenTokensChangeNothing(string text) => Assert.Empty(Codes(text));

    [Fact]
    public void ALocalOnlyWrittenAfterACommentIsStillUnused()
    {
        Assert.Contains(GmlLint.UnusedLocal, Codes("var t = 0;\n// reset\nt = 1;"));
        Assert.Contains(GmlLint.UnusedLocal, Codes("var t = 0;\nt = 1;"));
    }

    [Theory]
    [InlineData("draw_set_colour(#00FF00);")]
    [InlineData("draw_set_colour(#0A0B0C);")]
    [InlineData("x = 0b1010;")]
    [InlineData("x = 1_000;")]
    [InlineData("x = 1e3 + 2E-4;")]
    public void LiteralsTheTokenizerSplitsAreNoNames(string text)
    {
        Assert.Empty(Codes(text));
        Assert.Empty(GmlDoc.Parse(text).Named("FF00"));
    }

    [Fact]
    public void AWordAfterANumberWithASpaceIsStillAName() => Assert.Contains(GmlLint.UnknownVariable, Codes("x = 1 + e3;"));

    [Theory]
    [InlineData("var grid = ds_grid_create(2, 2);\ndraw_text(x, grid[# 1, 0], \"s\");")]
    [InlineData("var map = ds_map_create();\ndraw_text(x, map[? \"k\"], \"s\");")]
    [InlineData("var list = [1];\ndraw_text(x, list[| 0], \"s\");")]
    [InlineData("var st = { k: 1 };\ndraw_text(x, st[$ \"k\"], \"s\");")]
    [InlineData("var arr = [1];\ndraw_text(x, arr[@ 0], \"s\");")]
    public void AccessorsCountAsOneArgument(string text) => Assert.Empty(Codes(text));

    [Fact]
    public void ANameAfterAnAccessorInAVarIsDeclared()
    {
        const string text = "var map = ds_map_create();\nvar a = map[? \"k\"], b;\nshow_debug_message(a);";
        var doc = GmlDoc.Parse(text);
        Assert.True(GmlScopes.Declarations(text, doc.Tokens).Locals.ContainsKey("b"));
        var close = doc.PairOf(doc.TokenStartingAt(text.IndexOf("[?", StringComparison.Ordinal)));
        Assert.Equal(text.IndexOf(']', StringComparison.Ordinal), doc.Start(close));
        Assert.Contains(GmlLint.UnusedLocal, Codes(text));
    }

    [Fact]
    public void ALocalOfOneFunctionGivesNoTypeToTheSameNameInAnother()
    {
        Assert.DoesNotContain(GmlLint.StringNumberCompare, Codes("function a() { var result = \"\"; return result; }\nfunction b(result) { if (result == 0) return 1; return 0; }\na();\nb(1);"));
        Assert.DoesNotContain(GmlLint.StringNumberCompare, Codes("function a() { var result = \"\"; return result; }\nfunction b() { var result = 0; if (result == 0) return 1; return 0; }\na();\nb();"));
        Assert.Contains(GmlLint.StringNumberCompare, Codes("function a() { var result = \"\"; if (result == 0) return 1; return 0; }\na();"));
    }

    [Fact]
    public void AReadBeforeTheFirstAssignmentIsJudgedInScriptsOnly()
    {
        const string step = "if (timer > 0) timer -= 1;\n";
        Assert.Contains(GmlLint.ReadBeforeAssign, Codes(step));
        Assert.DoesNotContain(GmlLint.ReadBeforeAssign, Codes(step, Context with { InstanceCode = true }));
    }

    [Fact]
    public void CodeAfterAReturnIsStillUnreachableWithACommentBetween() =>
        Assert.Contains(GmlLint.Unreachable, Codes("function f() {\n    return 1; // done\n    show_debug_message(2);\n}\nf();"));
}
