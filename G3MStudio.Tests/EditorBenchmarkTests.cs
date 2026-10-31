using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using G3MLib.Analyzer.Compiler;
using G3MLib.Analyzer.Decompiler;
using G3MLib.DataFile;
using G3MLib.DataFile.Decompiler;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>
/// Code editor spike: open time and scroll frame cost of the GML view. Numbers go to the test output (use -showLiveOutput).
/// The corpus case runs only when G3MSTUDIO_CORPUS_DIR names a folder that holds data.win files.
/// </summary>
public sealed class EditorBenchmarkTests(ITestOutputHelper output)
{
    private const int ScrollFrames = 240;
    private const int WarmupFrames = 10;

    [Fact]
    public Task GeneratedMegabyteOpensHighlightsAndScrolls() => Measure("generated 1 MB", GenerateGml(1_000_000));

    [Fact]
    public Task LargestCorpusEntryOpensHighlightsAndScrolls()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var (name, text) = LargestEntry(folder);
        return Measure(name, text);
    }

    private async Task Measure(string label, string text)
    {
        output.WriteLine($"{label}: {text.Length:N0} chars, {text.Count(c => c == '\n') + 1:N0} lines");
        var watch = Stopwatch.StartNew();
        var tokens = GmlTokenizer.Tokenize(text);
        output.WriteLine($"tokenize: {watch.Elapsed.TotalMilliseconds:F1} ms, {tokens.Count:N0} tokens");

        double open = 0, plainOpen = 0;
        double[] colored = [], plain = [];
        await HeadlessApp.Run(() =>
        {
            Run(() => GmlColorizer.CreateEditor(text), 3);
            (plainOpen, plain) = Run(() => Plain(text), ScrollFrames);
            (open, colored) = Run(() => GmlColorizer.CreateEditor(text), ScrollFrames);
        });
        output.WriteLine($"open (tokens, document, first frame): {open:F1} ms; without highlighting: {plainOpen:F1} ms");
        Report("scroll frame, highlighted", colored);
        Report("scroll frame, plain", plain);
        Budget.AssertUnder(Percentile(colored, 0.95), 100, "p95 scroll frame cost", output);
    }

    private static TextEditor Plain(string text) => new() { IsReadOnly = true, ShowLineNumbers = true, FontFamily = new Avalonia.Media.FontFamily("Cascadia Mono,Consolas,Menlo,DejaVu Sans Mono,monospace"), Text = text };

    private static (double Open, double[] Frames) Run(Func<TextEditor> create, int count)
    {
        var watch = Stopwatch.StartNew();
        var editor = create();
        var window = new Window { Width = 1100, Height = 700, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        var open = watch.Elapsed.TotalMilliseconds;
        var step = editor.TextArea.TextView.DefaultLineHeight * 3;
        var viewer = editor.GetVisualDescendants().OfType<ScrollViewer>().First();
        var frames = new List<double>();
        var seen = new HashSet<string>();
        for (var i = 0; i < count + WarmupFrames; i++)
        {
            viewer.Offset = new Avalonia.Vector(0, step * (i + 1));
            watch.Restart();
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame()!;
            var ms = watch.Elapsed.TotalMilliseconds;
            if (i >= WarmupFrames) frames.Add(ms);
            using var png = new MemoryStream();
            frame.Save(png, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            seen.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png.ToArray())));
        }
        // Every scroll step must have produced a different picture, or the numbers measure nothing.
        Assert.True(seen.Count > (count + WarmupFrames) * 0.9, $"only {seen.Count} distinct frames");
        Assert.True(editor.VerticalOffset > step * 10, "the editor did not scroll");
        window.Close();
        return (open, frames.ToArray());
    }

    private void Report(string label, double[] frames) =>
        output.WriteLine($"{label}: mean {frames.Average():F2} ms, p95 {Percentile(frames, 0.95):F2} ms, max {frames.Max():F2} ms over {frames.Length} frames");

    private static double Percentile(double[] values, double p) => values.Order().ElementAt((int)(values.Length * p));

    private static string GenerateGml(int size)
    {
        string[] lines =
        [
            "// update the player state each step",
            "if (hp <= 0 && !global.dead) { global.dead = true; scr_game_over(\"player_died\", 3.5); }",
            "var _speed = point_distance(x, y, obj_player.x, obj_player.y) * 0.25 + irandom_range(1, 12);",
            "/* block comment that is long enough to look like documentation of the next statement */",
            "for (var i = 0; i < array_length(items); i++) { items[i].timer -= 1; if (items[i].timer == 0) instance_destroy(items[i]); }",
            "switch (state) { case 0: sprite_index = spr_idle; break; case 1: sprite_index = spr_walk; image_speed = 0.2; break; default: break; }",
            "draw_text_ext(x + 10, y - 24, $\"HP {hp}/{max_hp} at {room_get_name(room)}\", 14, 200);",
            "#macro SCREEN_SCALE 2",
            "global.save[0][3] = ds_map_find_value(global.flags, \"seen_intro\") ?? false;",
        ];
        var builder = new StringBuilder(size + 200);
        for (var i = 0; builder.Length < size; i++) builder.Append(lines[i % lines.Length]).Append(i % 5 == 0 ? "\n\n" : "\n");
        return builder.ToString();
    }

    // Picks the code entry with the most instructions across all data.win files and decompiles it.
    private static (string Name, string Text) LargestEntry(string folder)
    {
        var best = (Name: "", Text: "", Instructions: -1);
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var stream = File.OpenRead(file);
            using var data = GameMakerIO.Read(stream);
            var code = data.Code.OrderByDescending(c => c.Instructions.Count).First();
            if (code.Instructions.Count <= best.Instructions) continue;
            var text = new DecompileContext(new GlobalDecompileContext(data), code).DecompileToString();
            best = ($"{Path.GetFileName(Path.GetDirectoryName(file))} {code.Name.Content} ({code.Instructions.Count:N0} instructions)", text, code.Instructions.Count);
        }
        Assert.NotEqual(-1, best.Instructions);
        return (best.Name, best.Text);
    }
}
