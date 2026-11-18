using System.Diagnostics;
using G3MLib.Analyzer.Compiler;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class GmlLintTests(ITestOutputHelper output)
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "draw_text", "show_debug_message", "array_length", "instance_create_layer", "x", "y", "fps", "c_red", "obj_player", "spr_wall", "scr_move",
        "draw_background", "max", "irandom", "hp", "speed",
    };

    private static readonly Dictionary<string, (int Min, int Max)> Ranges = new(StringComparer.Ordinal)
    {
        ["draw_text"] = (3, 3),
        ["array_length"] = (1, 1),
        ["max"] = (0, int.MaxValue),
        ["instance_create_layer"] = (4, 5),
    };

    private static readonly GmlLintContext Context = new(
        Known.Contains,
        name => Ranges.TryGetValue(name, out var range) ? range : null,
        name => name is "fps" or "c_red",
        name => name == "draw_background",
        Known,
        _ => false,
        _ => false,
        _ => null,
        _ => false);

    private static IReadOnlyList<LintIssue> Lint(string text, GmlLintContext? context = null) => GmlLint.Analyze(GmlDoc.Parse(text), context ?? Context);

    private static IReadOnlyList<string> Codes(string text, GmlLintContext? context = null) => [.. Lint(text, context).Select(i => i.Diagnostic.Code)];

    private static string Marked(string text, LintIssue issue) => text[issue.Diagnostic.Span.StartOffset..issue.Diagnostic.Span.EndOffset];

    [Fact]
    public void AnUnknownFunctionIsReportedWithTheNearestNameAndAFix()
    {
        const string text = "draw_txt(1, 2, \"a\");";
        var issue = Assert.Single(Lint(text));
        Assert.Equal(GmlLint.UnknownFunction, issue.Diagnostic.Code);
        Assert.Equal("draw_txt", Marked(text, issue));
        Assert.Contains("draw_text", issue.Diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal("draw_text(1, 2, \"a\");", GmlSymbols.Apply(text, issue.Fix!.Edits));
    }

    [Theory]
    [InlineData("draw_text(1, 2, \"a\");")]
    [InlineData("function helper(a) { return a; }\nhelper(1);")]
    [InlineData("var f = function() { return 1; };\nf();")]
    [InlineData("callback = function() {};\ncallback();")]
    [InlineData("other.anything(1);")]
    [InlineData("var v = new Vector(1);\nfunction Vector(n) constructor { value = n; }\nshow_debug_message(v.value);")]
    [InlineData("scr_move();")]
    public void KnownAndDeclaredCallsAreNotReported(string text) => Assert.Empty(Codes(text));

    [Fact]
    public void ReadingANameSetNowhereIsReported()
    {
        const string text = "x = hpp;";
        var issue = Assert.Single(Lint(text));
        Assert.Equal(GmlLint.UnknownVariable, issue.Diagnostic.Code);
        Assert.Equal("hpp", Marked(text, issue));
        Assert.Equal("x = hp;", GmlSymbols.Apply(text, issue.Fix!.Edits));
    }

    [Theory]
    [InlineData("score = 1;\nx = score;")]
    [InlineData("x = global.points;")]
    [InlineData("x = obj_player.hp;")]
    [InlineData("x = Thing.member;")]
    [InlineData("x = argument0 + argument[1] + argument_count;")]
    [InlineData("try { x = 1; } catch (err) { show_debug_message(err); }")]
    [InlineData("enum Dir { up, down = 3 }\nx = Dir.down;")]
    [InlineData("#macro SPEED 4\nx = SPEED;")]
    [InlineData("#macro Desktop:SIZE 2\nx = SIZE;")]
    [InlineData("var s = { width: 1, height: 2 };\nx = s.width + s.height;")]
    [InlineData("#region Setup things\nx = 1;\n#endregion")]
    [InlineData("count++;\nx = count;")]
    [InlineData("x = self.y + other.y + noone + undefined + pi;")]
    [InlineData("globalvar lives;\nlives = 3;\nx = lives;")]
    [InlineData("x = c_red;")]
    public void NamesTheTextOrTheGameDefinesAreNotReported(string text) => Assert.DoesNotContain(GmlLint.UnknownVariable, Codes(text));

    [Fact]
    public void AWrongArgumentCountOfABuiltInIsReportedOverTheWholeCall()
    {
        const string text = "draw_text(1, 2);";
        var issue = Assert.Single(Lint(text));
        Assert.Equal(GmlLint.ArgumentCount, issue.Diagnostic.Code);
        Assert.Equal("draw_text(1, 2)", Marked(text, issue));
        Assert.Contains("3", issue.Diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(GmlLint.ArgumentCount, Assert.Single(Lint("array_length();")).Diagnostic.Code);
        Assert.Equal(GmlLint.ArgumentCount, Assert.Single(Lint("instance_create_layer(1, 2, \"a\", obj_player, {}, 6);")).Diagnostic.Code);
    }

    [Theory]
    [InlineData("draw_text(max(1, 2, 3), array_length([1, 2]), \"a\");")]
    [InlineData("x = max();\nx = max(1, 2, 3, 4, 5, 6, 7);")]
    [InlineData("instance_create_layer(1, 2, \"a\", obj_player);\ninstance_create_layer(1, 2, \"a\", obj_player, { hp: 1 });")]
    [InlineData("var draw_text = function(a) {};\ndraw_text(1);")]
    [InlineData("function draw_text(a) {}\ndraw_text(1);")]
    [InlineData("other.draw_text(1);")]
    public void RightCountsAndShadowedNamesAreNotReported(string text) => Assert.DoesNotContain(GmlLint.ArgumentCount, Codes(text));

    [Fact]
    public void AssigningAReadOnlyBuiltInIsReportedButComparingIsNot()
    {
        Assert.Equal([GmlLint.ReadOnly], Codes("fps = 30;"));
        Assert.Equal([GmlLint.ReadOnly], Codes("if (x) c_red += 1;"));
        Assert.DoesNotContain(GmlLint.ReadOnly, Codes("if (fps = 30) x = 1;"));
        Assert.Empty(Codes("var fps = 30;\nx = fps;"));
        Assert.Empty(Codes("x = obj_player.fps;"));
    }

    [Fact]
    public void ALocalThatIsNeverReadIsAHint()
    {
        var issue = Assert.Single(Lint("var unused = 1;\nunused = 2;"));
        Assert.Equal(GmlLint.UnusedLocal, issue.Diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Info, issue.Diagnostic.Severity);
        Assert.Equal(1, issue.Diagnostic.Span.StartLine);
        Assert.Empty(Codes("var used = 1;\nx = used;"));
        Assert.Empty(Codes("var n = 1;\nn += 1;"));
        Assert.Empty(Codes("var shown = 1;\nshow_debug_message($\"value {shown}\");"));
        Assert.Empty(Codes("for (var i = 0; i < 3; i++) {}"));
        Assert.Empty(Codes("var _ = 1;"));
        Assert.DoesNotContain(GmlLint.UnusedLocal, Codes("function f(unusedParameter) {}")); // a parameter has its own hint
    }

    [Fact]
    public void CodeAfterAJumpInTheSameBlockIsUnreachable()
    {
        const string text = "if (x)\n{\n    return 1;\n    y = 2;\n}";
        var issue = Assert.Single(Lint(text));
        Assert.Equal(GmlLint.Unreachable, issue.Diagnostic.Code);
        Assert.Equal("y = 2;", Marked(text, issue));
        Assert.Equal([GmlLint.Unreachable], Codes("exit;\nx = 1;"));
        Assert.Equal([GmlLint.Unreachable], Codes("while (x) { break; y = 1; }"));
    }

    [Theory]
    [InlineData("if (x) return 1;\ny = 2;")]
    [InlineData("if (x) { y = 1; } else return;\ny = 2;")]
    [InlineData("switch (x) { case 1: y = 1; break; case 2: y = 2; break; default: exit; }")]
    [InlineData("exit;\nfunction later() {}")]
    [InlineData("return\n    1 + 2;")]
    [InlineData("while (x) { if (y) continue; y = 1; }")]
    [InlineData("return 1;\n#region more\n#endregion")]
    public void CodeThatRunsIsNotUnreachable(string text) => Assert.DoesNotContain(GmlLint.Unreachable, Codes(text));

    [Fact]
    public void ADeprecatedFunctionIsAHint()
    {
        var issue = Assert.Single(Lint("draw_background(spr_wall, 0, 0);"));
        Assert.Equal(GmlLint.Deprecated, issue.Diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Info, issue.Diagnostic.Severity);
    }

    [Fact]
    public void AnEqualsSignInAConditionGetsAFixToDoubleIt()
    {
        const string text = "if (x = 1) y = 2;";
        var issue = Assert.Single(Lint(text));
        Assert.Equal(GmlLint.AssignInCondition, issue.Diagnostic.Code);
        Assert.Equal("if (x == 1) y = 2;", GmlSymbols.Apply(text, issue.Fix!.Edits));
        Assert.Empty(Codes("if (x == 1 && max(y = 2) > 0) y = 2;"));
        Assert.Empty(Codes("while (x != 1) { x = 1; }"));
    }

    [Fact]
    public void AnUnknownEscapeInAStringIsMarked()
    {
        const string text = "x = \"C:\\data\\new\";";
        var issue = Assert.Single(Lint(text));
        Assert.Equal(GmlLint.UnknownEscape, issue.Diagnostic.Code);
        Assert.Equal("\\d", Marked(text, issue));
        Assert.Empty(Codes("x = \"a\\n\\t\\\"\\\\\\x41\\u00e9\";"));
        Assert.Empty(Codes("x = @\"C:\\data\";"));
        Assert.Empty(Codes("x = \"C:\\data\";", Context with { Escapes = false }));
    }

    [Fact]
    public void ASemicolonRightAfterAConditionEmptiesTheBody()
    {
        const string text = "if (x > 1);\n    y = 2;";
        var issue = Assert.Single(Lint(text));
        Assert.Equal(GmlLint.EmptyBody, issue.Diagnostic.Code);
        Assert.Equal("if (x > 1)\n    y = 2;", GmlSymbols.Apply(text, issue.Fix!.Edits));
        Assert.Empty(Codes("if (x > 1) {}\nwith (obj_player) { y = 2; }\nrepeat (3) y += 1;"));
    }

    [Fact]
    public void ARepeatedCaseValueIsReported()
    {
        const string text = "switch (x) { case 1: y = 1; break; case \"a\": break; case 1: break; }";
        var issue = Assert.Single(Lint(text));
        Assert.Equal(GmlLint.DuplicateCase, issue.Diagnostic.Code);
        Assert.Equal("1", Marked(text, issue));
        Assert.Empty(Codes("switch (x) { case 1: break; case -1: break; case 2: switch (y) { case 1: break; } break; }"));
    }

    [Fact]
    public void ArgumentRangesKeepTheWiderOfTheReferenceAndTheCompilerList()
    {
        Assert.Equal((2, 3), GmlLint.Combine("f", (2, 3), null));
        Assert.Equal((1, 3), GmlLint.Combine("f", (2, 3), (1, 2)));
        Assert.Equal((2, 3), GmlLint.Combine("f", (2, 3), (0, int.MaxValue)));
        Assert.Equal((1, int.MaxValue), GmlLint.Combine("string", (1, 1), (1, 1)));
        Assert.Null(GmlLint.Combine("f", null, (1, 1)));
    }

    // Typical hand-written and decompiled code: every construct the check knows must pass without a single finding.
    private const string Clean = """
        /// Moves the player.
        #macro GRAVITY 0.5
        enum State { idle, walk, jump = 5 }
        globalvar lives;
        lives = 3;
        hp = 10;
        state = State.idle;
        var dx = 0, dy = 0;
        for (var i = 0; i < 4; i++)
        {
            dx += i;
            if (i == 2) continue;
            dy = dx * GRAVITY;
        }

        with (obj_player)
        {
            x += other.x;
            speed = max(speed, 1, 2);
        }

        switch (state)
        {
            case State.idle:
                y += dy;
                break;
            case State.walk:
                x -= 1;
                break;
            default:
                exit;
        }

        function Vector(vx, vy) constructor
        {
            self.vx = vx;
            self.vy = vy;
            static length = function() { return sqrt(vx * vx + vy * vy); };
        }

        var v = new Vector(dx, dy);
        var list = [1, 2, 3];
        var total = 0;
        for (var k = 0; k < array_length(list); k++) total += list[k];
        var info = { name: "hero", level: total };
        show_debug_message($"Total {total} for {info.name}");
        try
        {
            draw_text(x, y, "Lives: " + string(lives));
        }
        catch (problem)
        {
            show_debug_message(problem.message);
        }

        if (v.length() > 0 && irandom(3) == 1) global.best = v.length();
        return hp;
        """;

    [Fact]
    public void CleanCodeHasNoFindings()
    {
        var known = Known.Concat(["sqrt", "string"]).ToHashSet(StringComparer.Ordinal);
        var issues = Lint(Clean, Context with { IsKnown = known.Contains, Names = known });
        Assert.True(issues.Count == 0, string.Join("\n", issues.Select(i => $"{i.Diagnostic.Code} {i.Diagnostic.Message} at {Marked(Clean, i)}")));
    }

    [Fact]
    public void AHugeScriptIsCheckedQuickly()
    {
        var known = Known.Concat(["sqrt", "string"]).ToHashSet(StringComparer.Ordinal);
        var context = Context with { IsKnown = known.Contains, Names = known };
        // About 25 000 lines, far past what one code entry holds in a real game.
        var text = string.Concat(Enumerable.Repeat(Clean.Replace("return hp;", "", StringComparison.Ordinal), 400)) + "missing_name(1);\n";
        var doc = GmlDoc.Parse(text);
        GmlLint.Analyze(GmlDoc.Parse(Clean), context, TestContext.Current.CancellationToken); // warm up
        var watch = Stopwatch.StartNew();
        var issues = GmlLint.Analyze(doc, context, TestContext.Current.CancellationToken);
        watch.Stop();
        output.WriteLine($"{text.Length} chars, {doc.Tokens.Count} tokens: {watch.ElapsedMilliseconds} ms, {issues.Count} findings");
        Assert.Contains(issues, i => i.Diagnostic.Code == GmlLint.UnknownFunction);
        Assert.True(watch.ElapsedMilliseconds < 3000, $"took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void ACancelledCheckStops()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => GmlLint.Analyze(GmlDoc.Parse(Clean), Context, cancel.Token));
    }
}
