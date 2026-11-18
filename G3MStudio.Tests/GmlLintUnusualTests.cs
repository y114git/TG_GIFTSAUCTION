using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>Valid GML written in unusual ways must give no finding of any kind: the code editor never shows a problem that is not one.</summary>
public sealed class GmlLintUnusualTests
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "show_debug_message", "show_error", "draw_text", "method", "string", "real", "array_length", "is_undefined", "x", "y", "speed", "image_blend", "obj_enemy", "script_execute",
        "variable_instance_set", "variable_struct_get", "irandom", "min", "max", "instance_destroy", "id", "method_get_self", "is_struct",
    };

    private static readonly GmlLintContext Context = new(
        Known.Contains,
        _ => null,
        _ => false,
        _ => false,
        Known,
        IsBuiltinFunction: name => name is "show_debug_message" or "show_error" or "draw_text" or "method" or "string" or "real" or "array_length" or "is_undefined" or "irandom" or "min" or "max" or "variable_instance_set",
        IsBuiltinVariable: name => name is "x" or "y" or "speed" or "image_blend" or "id",
        OwnRequired: _ => null,
        IsOwnConstructor: name => name is "Base" or "Derived" or "Point");

    private static readonly string[] Cases =
    [
        "var s = { a: { b: [1, 2, { c: 3 }] }, d: function() { return 1; } };\nshow_debug_message(s.a.b[2].c);\nshow_debug_message(s.d());",
        "var n = 3;\nshow_debug_message($\"value {n} and {n * 2} {string(n)}\");",
        "var i = 0;\nwith (obj_enemy) { x += 1; }\nrepeat (3) { i++; }\nshow_debug_message(i);",
        "var i = irandom(3);\nswitch (i) { case 1: show_debug_message(\"a\"); break; case 2: case 3: show_debug_message(\"b\"); break; default: break; }",
        "var i = 0;\ntry { show_error(\"x\", false); } catch (e) { show_debug_message(e.message); } finally { i = 1; }\nshow_debug_message(i);",
        "var v = undefined;\nvar r = v ?? 5;\nvar q = (x > 1) ? 2 : 3;\nv ??= 1;\nshow_debug_message(r + q + v);",
        "var c = 0xFF00FF;\nvar d = $FF8800;\nvar e = #FF00FF;\nimage_blend = c + d + e;",
        "var p = @\"C:\\games\\x\";\nshow_debug_message(p);",
        "#region Setup\n#macro LIMIT 10\nenum E { a = 1, b = a + 1, c = 1 << 3 }\nvar v = E.b + E.c + LIMIT;\nshow_debug_message(v);\n#endregion",
        "function f() {\n    var sum = 0;\n    for (var i = 0; i < argument_count; i++) sum += argument[i];\n    return sum + argument[0] + argument0;\n}",
        "function f() {\n    static count = 0;\n    count++;\n    return count;\n}",
        "function Base(_x) constructor {\n    px = _x;\n    static get = function() { return px; };\n}\nfunction Derived(_x, _y) : Base(_x) constructor {\n    py = _y;\n}\nvar d = new Derived(1, 2);\nshow_debug_message(d.get() + d.py);",
        "var m = method(self, function() { return 1; });\nshow_debug_message(m());",
        "global.score = 1;\nwith (obj_enemy) { self.x = global.score + other.x; }\nvar me = self;\nshow_debug_message(me.x);",
        "function f(a, b = 2, c = a + b) {\n    return a + b + c;\n}\nshow_debug_message(f(1) + f(1, 2) + f(1, 2, 3));",
        "var a = [1, 2, 3];\nvar total = 0;\nfor (var i = 0; i < array_length(a); i++) { total += a[i]; }\nvar b = a[@ 0];\nshow_debug_message(total + b);",
        "var o = { x: 1, y: 2 };\nvar t = o.x > 0 ? (o.y > 0 ? 1 : 2) : 3;\nshow_debug_message(t);",
        "var i = 0;\ndo { i++; if (i > 5) break; } until (i > 10);\nwhile (i < 20) { i += 2; continue; }\nshow_debug_message(i);",
        "var n = 5;\nvar r = n div 2 + n mod 2;\nvar b = n >= 1 and n <= 9 or not (n == 3) xor false;\nshow_debug_message(r + b);",
        "var s = \"tab\\there \\\"q\\\" \\\\ \\n\";\nshow_debug_message(s);",
        "function f(n) {\n    if (n > 1) { return 1; } else { return 2; }\n}\nshow_debug_message(f(1));",
        "function f(n) {\n    switch (n) { case 1: return 1; default: return 2; }\n}\nshow_debug_message(f(1));",
        "function f(n) {\n    try { return n; } catch (e) { return 2; }\n}\nshow_debug_message(f(1));",
        "function f(n) {\n    do { return 1; } until (n);\n}\nshow_debug_message(f(1));",
        "var a = 1e3 + 1.5e-3 + .5 + 5.;\nshow_debug_message(a);",
        "var fn = function(a) { return a * 2; };\nshow_debug_message(fn(2));\nshow_debug_message(script_execute(fn, 3));",
        "var arr = [];\narr[array_length(arr)] = 1;\nvar st = { list: arr };\nst.list[0] = 2;\nshow_debug_message(st);",
        "var k = \"name\";\nvar st = {};\nst[$ k] = 1;\nvar v = st[$ k];\nshow_debug_message(v);",
        "function Point(_x, _y) constructor {\n    x = _x; y = _y;\n    static add = function(o) { return new Point(x + o.x, y + o.y); };\n}\nvar p = new Point(1, 2).add(new Point(3, 4));\nshow_debug_message(p.x);",
        "var a = 5;\nvar b = a++ + ++a;\nvar c = -a + +a - ~a;\nshow_debug_message(b + c + (a & 3) + (a | 4) + (a ^ 1) + (a << 1) + (a >> 1));",
        "var done = false;\nif (!done && x != 3) { done = true; }\nelse if (x == 4) { done = false; }\nshow_debug_message(done);",
        "/* block comment */\nvar a = 1; // trailing\n/// @param a\nfunction f(_a) { return _a; }\nshow_debug_message(f(a));",
    ];

    [Fact]
    public void ValidCodeHasNoFinding()
    {
        var report = new List<string>();
        foreach (var text in Cases)
            foreach (var issue in GmlLint.Analyze(GmlDoc.Parse(text), Context, TestContext.Current.CancellationToken))
                report.Add($"{issue.Diagnostic.Code} {issue.Diagnostic.Severity} '{text[issue.Diagnostic.Span.StartOffset..issue.Diagnostic.Span.EndOffset]}' {issue.Diagnostic.Message} in: {text.ReplaceLineEndings(" ")}");
        Assert.True(report.Count == 0, string.Join(Environment.NewLine, report));
    }
}
