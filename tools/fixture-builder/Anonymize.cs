using System.Text.RegularExpressions;
using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using ImageMagick;
using ImageMagick.Drawing;

/// <summary>
/// Replaces everything of a game in a reduced data file with generated content of the same shape: names, text, code, pictures, sounds and room layouts.
/// The numbers that give the file its structure (counts, sizes, flags, ids) stay. Every random choice comes from one seeded generator, so a run is repeatable.
/// </summary>
static partial class Anonymize
{
    private static readonly (string Key, string Prefix)[] Kinds =
    [
        ("sprites", "spr"), ("tileSets", "ts"), ("sounds", "snd"), ("paths", "path"), ("scripts", "scr"), ("shaders", "shd"), ("fonts", "fnt"), ("timelines", "tl"),
        ("objects", "obj"), ("rooms", "rm"), ("extensions", "ext"), ("sequences", "seq"), ("animationCurves", "ac"), ("particleSystems", "ps"),
    ];

    private static readonly string[] Snippets =
    [
        "var _a = {1};\nvar _b = _a * {2} + {3};\nif (_b > {4})\n{\n    x += _b;\n}\nelse\n{\n    y -= _a;\n}\nshow_debug_message(\"msg_{0}\");",
        "for (var _i = 0; _i < {1}; _i++)\n{\n    hspeed += _i * {2};\n}\nvspeed = {3};",
        "switch (image_index)\n{\n    case 0:\n        speed = {1};\n        break;\n    case 1:\n        speed = {2};\n        break;\n    default:\n        speed = {3};\n}",
        "var _s = \"txt_{0}\";\nvar _n = string_length(_s) + {1};\nwhile (_n > {2})\n{\n    _n -= {3};\n}\nimage_alpha = _n / {4};",
        "var _arr = [{1}, {2}, {3}];\nvar _sum = 0;\nfor (var _k = 0; _k < array_length(_arr); _k++)\n{\n    _sum += _arr[_k];\n}\ndepth = _sum - {4};",
    ];

    [GeneratedRegex(@"^(spr|ts|snd|path|scr|shd|fnt|tl|obj|rm|ext|seq|ac|ps)_[a-z]\d{3}$|^(msg|txt)_\d+$|^gml_(Object|Script|GlobalScript|RoomCC|Room|Timeline)_(obj|scr|rm|tl|seq|ps|path|fnt|spr|ts|snd|shd|ext|ac|anon|fx)|^GM[A-Za-z]+$|^_filter_|^[Dd]efault$|^\.\w+$")]
    private static partial Regex Keep();

    public static void Run(GameMakerData data, Random rng, char tag)
    {
        Rename(data, tag);
        Code(data, rng);
        Layouts(data, rng);
        Binary(data, rng);
    }

    private static void Rename(GameMakerData data, char tag)
    {
        var pairs = new List<(string Old, string New)>();
        foreach (var (key, prefix) in Kinds)
        {
            var type = ResourceTypes.All.First(t => t.Key == key);
            var number = 0;
            foreach (var item in (type.Items(data) ?? []).ToList())
            {
                var name = ResourceTypes.NameOf(item);
                var generated = $"{prefix}_{tag}{number++:000}";
                if (name == generated) continue;
                if (name.Length >= 4) pairs.Add((name, generated));
                if (name.StartsWith("gml_Script_", StringComparison.Ordinal) && name.Length > 15) pairs.Add((name["gml_Script_".Length..], generated));
                try { new RenameResourceCommand(key, name, generated).Apply(data); }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException)
                {
                    Console.WriteLine($"rename {key} {name}: {e.Message}; renamed in place");
                    if (item.GetType().GetProperty("Name")?.GetValue(item) is GameMakerString text) text.Content = generated;
                }
            }
        }

        // names that other names contain, such as the functions of a sequence
        foreach (var text in data.Strings)
            foreach (var (old, generated) in pairs.OrderByDescending(p => p.Old.Length))
                if (text.Content is { } content && content.Contains(old, StringComparison.Ordinal)) text.Content = content.Replace(old, generated, StringComparison.Ordinal);
    }

    private static string Text(Random rng, bool child, bool old)
    {
        var n = new[] { rng.Next(2, 90), rng.Next(2, 90), rng.Next(2, 90), rng.Next(2, 90) };
        if (child) return $"var _f = function(_p)\n{{\n    return _p + {n[0]};\n}};\nimage_index = _f({n[1]});";
        return Snippets[rng.Next(old ? Snippets.Length - 1 : Snippets.Length)].Replace("{0}", rng.Next(1000).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{1}", n[0].ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{2}", n[1].ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{3}", n[2].ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{4}", n[3].ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // Every root entry gets short new GML; a global script keeps one function per child, named as the child.
    private static void Code(GameMakerData data, Random rng)
    {
        var plain = data.GeneralInfo.BytecodeVersion < 17 ? 1 : 0;
        foreach (var code in data.Code.Where(c => c.ParentEntry is null).ToList())
        {
            var name = code.Name.Content ?? "";
            string text;
            if (code.ChildEntries.Count > 0 && name.StartsWith("gml_GlobalScript_", StringComparison.Ordinal))
                text = string.Join("\n\n", code.ChildEntries.Select(c => $"function {(c.Name.Content ?? "").Replace("gml_Script_", "", StringComparison.Ordinal)}(_a)\n{{\n    return _a + {rng.Next(2, 90)};\n}}"));
            else text = Text(rng, code.ChildEntries.Count > 0, plain == 1);
            if (plain == 1 && name.StartsWith("gml_Script_", StringComparison.Ordinal)) text = "return argument0 + " + rng.Next(2, 90).ToString(System.Globalization.CultureInfo.InvariantCulture) + ";";
            var group = new CodeImportGroup(data);
            group.QueueReplace(code, text);
            var result = group.Import(throwOnFailedCompile: false);
            if (result.Successful) continue;
            var fallback = new CodeImportGroup(data);
            fallback.QueueReplace(code, "x = " + rng.Next(2, 90).ToString(System.Globalization.CultureInfo.InvariantCulture) + ";");
            if (!fallback.Import(throwOnFailedCompile: false).Successful) throw new InvalidOperationException($"code {name} did not compile: {result.PrintAllErrors(true)}");
            var why = result.PrintAllErrors(true);
            Console.WriteLine($"code {name}: fallback text; {why[..Math.Min(200, why.Length)]}");
        }
    }

    private static void Layouts(GameMakerData data, Random rng)
    {
        foreach (var room in data.Rooms)
        {
            var (w, h) = (Math.Max(1, (int)room.Width), Math.Max(1, (int)room.Height));
            foreach (var i in room.GameObjects) (i.X, i.Y) = (rng.Next(w), rng.Next(h));
            foreach (var t in room.Tiles) (t.X, t.Y) = (rng.Next(w), rng.Next(h));
            foreach (var layer in room.Layers ?? [])
            {
                foreach (var i in layer.InstancesData?.Instances ?? []) (i.X, i.Y) = (rng.Next(w), rng.Next(h));
                if (layer.TilesData is { TileData: { } rows } tiles)
                {
                    var count = (int)Math.Max(1, tiles.Background?.GMS2TileCount ?? 1);
                    foreach (var row in rows)
                        row.AsSpan().ReplaceWhere(count, rng);
                    tiles.TileDataUpdated();
                }
            }
        }

        foreach (var path in data.Paths ?? [])
            foreach (var p in path.Points) (p.X, p.Y) = (rng.Next(0, 512), rng.Next(0, 512));
    }

    private static void Binary(GameMakerData data, Random rng)
    {
        foreach (var sprite in data.Sprites ?? [])
            foreach (var mask in sprite.CollisionMasks) if (mask.Data is { } bytes) rng.NextBytes(bytes);
        foreach (var shader in data.Shaders ?? [])
        {
            var (vertex, fragment) = ("attribute vec3 in_Position;\nvarying vec2 v_vTexcoord;\nvoid main()\n{\n    gl_Position = vec4(in_Position, 1.0);\n    v_vTexcoord = in_Position.xy;\n}\n",
                "varying vec2 v_vTexcoord;\nvoid main()\n{\n    gl_FragColor = vec4(v_vTexcoord, 0.5, 1.0);\n}\n");
            foreach (var p in typeof(GameMakerShader).GetProperties().Where(p => p.PropertyType == typeof(GameMakerString) && p.CanWrite))
                if (p.GetValue(shader) is GameMakerString s && p.Name.EndsWith("Vertex", StringComparison.Ordinal)) s.Content = vertex;
                else if (p.GetValue(shader) is GameMakerString f && p.Name.EndsWith("Fragment", StringComparison.Ordinal)) f.Content = fragment;
            foreach (var p in typeof(GameMakerShader).GetProperties().Where(p => p.PropertyType == typeof(GameMakerShader.GameMakerRawShaderData)))
                if (p.GetValue(shader) is GameMakerShader.GameMakerRawShaderData { Data: { } raw }) rng.NextBytes(raw);
        }
    }

    // What is left: display text, captions, layer and file names. Names the program reads (type names, filter names, groups) stay.
    public static void Texts(GameMakerData data)
    {
        var number = 0;
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var text in Strings(data, Roots(data)).Concat(GroupNames(data)).Distinct().ToList())
        {
            if (text.Content is not { Length: > 3 } content || Keep().IsMatch(content)) continue;
            if (!seen.TryGetValue(content, out var generated))
            {
                var extension = Path.GetExtension(content);
                seen[content] = generated = (content.StartsWith("gml_", StringComparison.Ordinal) ? $"gml_Script_fx_{number++:0000}" : $"txt_{number++:0000}")
                    + (extension.Length is > 1 and < 6 && extension.Skip(1).All(char.IsAsciiLetterOrDigit) ? extension : "");
            }

            text.Content = generated;
        }
    }

    private static IEnumerable<GameMakerString> GroupNames(GameMakerData data) => (data.TextureGroupInfo ?? []).SelectMany(group => group.GetType().GetProperties()
        .Where(p => p.PropertyType == typeof(GameMakerString)).Select(p => p.GetValue(group)).OfType<GameMakerString>());

    public static IEnumerable<GameMakerString> Strings(GameMakerData data, IEnumerable<object> roots) =>
        Walker.Reach(roots).OfType<GameMakerString>().Where(s => !data.Variables.Any(v => ReferenceEquals(v.Name, s)) && !data.Functions.Any(f => ReferenceEquals(f.Name, s))).Distinct();

    public static IEnumerable<object> Roots(GameMakerData data)
    {
        var skip = new[] { "STRG", "TPAG", "TXTR", "AUDO", "VARI", "FUNC", "CODE" };
        return data.FORM.ChunksTypeDict.Values.Where(c => !skip.Contains(c.GetType().Name[^4..])).Cast<object>().Concat(data.CodeLocals?.Cast<object>() ?? []);
    }

    /// <summary>A picture for one rectangle of a page: a random colour with a border, a diagonal and a box, all opaque.</summary>
    public static MagickImage Picture(Random rng, int width, int height)
    {
        var colour = new MagickColor((byte)rng.Next(40, 230), (byte)rng.Next(40, 230), (byte)rng.Next(40, 230));
        var image = new MagickImage(colour, (uint)Math.Max(1, width), (uint)Math.Max(1, height));
        if (width < 3 || height < 3) return image;
        var other = new MagickColor((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
        new Drawables().StrokeColor(other).FillColor(MagickColors.Transparent).Rectangle(0, 0, width - 1, height - 1).Line(0, 0, width - 1, height - 1)
            .FillColor(other).Rectangle(width / 3.0, height / 3.0, width * 2 / 3.0, height * 2 / 3.0).Draw(image);
        return image;
    }

    /// <summary>A tone in the container of the original, with its channels and sample rate and about its length (between 0.3 and 1.5 seconds).</summary>
    public static byte[] Tone(Random rng, byte[] original)
    {
        var kind = SoundAudio.Format(original);
        var (channels, rate, seconds) = (2, 22050, 0.5);
        try
        {
            var decoded = AudioCodec.Decode(original);
            (channels, rate, seconds) = (decoded.Channels, decoded.SampleRate, Math.Clamp(decoded.Seconds, 0.3, 1.5));
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or NotSupportedException) { }

        var frames = Math.Max(256, (int)(rate * seconds));
        var pitch = 200 + rng.Next(800);
        var samples = new float[frames * channels];
        for (var i = 0; i < frames; i++)
            for (var c = 0; c < channels; c++) samples[(i * channels) + c] = (float)(Math.Sin(i * 2 * Math.PI * (pitch + (c * 90)) / rate) * 0.4);
        return AudioCodec.Encode(new PcmAudio(samples, channels, rate), kind == "wav" ? "wav" : "ogg");
    }
}

static class TileRows
{
    public static void ReplaceWhere(this Span<uint> row, int count, Random rng)
    {
        foreach (ref var cell in row)
            if (cell != 0) cell = (cell & ~0x7FFFFu) | (uint)rng.Next(count);
    }
}
