// aislop-worker
#:package G3MLib@1.1.2
#:property TreatWarningsAsErrors=false
#:property PublishAot=false
#:property AnalysisLevel=none
#:property EnforceCodeStyleInBuild=false
// Builds G3MStudio.Core/Resources/gml-reference.json from src/*.txt and checks it against the compiler tables of G3MLib.
// Usage: dotnet run generate.cs -- <output json>
using System.Text.Json;
using System.Text.Json.Serialization;
using G3MLib.DataFile.Compiler;

var here = AppContext.BaseDirectory;
var source = Path.Combine(Directory.GetCurrentDirectory(), "src");
var list = new BuiltinList();
var vars = list.GlobalVars.Keys.Concat(list.GlobalArrayVars.Keys).Concat(list.InstanceVars.Keys).Concat(list.InstanceLimitedVars.Keys).ToHashSet(StringComparer.Ordinal);

var groups = new Dictionary<string, (string Folder, string Category)>();
var entries = new List<Entry>();
var consts = new List<(string Pattern, string Category, string Summary, string Page)>();
var seen = new HashSet<string>(StringComparer.Ordinal);
var problems = new List<string>();
var unchecked_ = 0;

foreach (var file in Directory.GetFiles(source, "*.txt").Order(StringComparer.Ordinal))
{
    foreach (var raw in File.ReadAllLines(file))
    {
        var line = raw.TrimEnd();
        if (line.Length == 0 || line[0] == '#') continue;
        if (line.StartsWith("@group ", StringComparison.Ordinal))
        {
            var g = line[7..].Split('|');
            groups[g[0]] = (g[1], g[2]);
        }
        else if (line.StartsWith("@var ", StringComparison.Ordinal))
        {
            var f = line[5..].Split('|');
            if (!seen.Add(f[0])) continue;
            if (!vars.Contains(f[0]) && f[0] is not ("layer" or "GM_version" or "pi" or "undefined" or "noone" or "all" or "self" or "other" or "global")) problems.Add("variable not in the compiler tables: " + f[0]);
            entries.Add(new Entry { N = f[0], K = "v", C = f[1], S = f[3], R = f[2], Ro = f.Length > 4 && f[4].Contains('R') ? 1 : null });
        }
        else if (line.StartsWith("@const ", StringComparison.Ordinal))
        {
            var f = line[7..].Split('|');
            consts.Add((f[0], f[1], f[2], f.Length > 3 ? f[3] : ""));
        }
        else
        {
            var f = line.Split('|');
            if (f.Length < 5) { problems.Add("bad line: " + line); continue; }
            if (!seen.Add(f[0])) continue;
            var (folder, category) = groups[f[1]];
            var flags = f.Length > 5 ? f[5] : "";
            var isVar = flags.Contains('V');
            var parameters = f[3].Length == 0 ? [] : f[3].Split(';').Select(p => p.Split(':', 3)).Select(p => new[] { p[0], p[1], p.Length > 2 ? p[2] : "" }).ToList();
            if (!isVar)
            {
                // Newer functions (cameras, time sources, ...) are read from the data file at runtime, not from the built-in table: kept, but unchecked.
                if (!list.Functions.TryGetValue(f[0], out var info)) { unchecked_++; goto add; }
                var required = parameters.Count(p => !p[0].StartsWith('?') && !p[0].StartsWith("...", StringComparison.Ordinal));
                var rest = parameters.Any(p => p[0].StartsWith("...", StringComparison.Ordinal));
                if (info.MinArguments > 0 && required != info.MinArguments) problems.Add($"{f[0]}: required {required}, tables say {info.MinArguments}..{info.MaxArguments}");
            }

        add:
            var page = f[0].Replace("color", "colour", StringComparison.Ordinal).Replace("randomize", "randomise", StringComparison.Ordinal);
            entries.Add(new Entry { N = f[0], K = isVar ? "v" : "f", C = category, S = f[2], P = parameters, R = f[4], D = flags.Contains('D') ? 1 : null, U = folder.Length == 0 ? null : folder + "/" + page + ".htm" });
        }
    }
}

// Constant groups: one pattern entry each, so hover and the panel can describe c_red or vk_space without 900 rows.
foreach (var (pattern, category, summary, pageOf) in consts)
{
    var prefix = pattern.TrimEnd('*');
    var star = pattern.EndsWith('*');
    var count = list.Constants.Keys.Count(k => star ? k.StartsWith(prefix, StringComparison.Ordinal) : k == prefix);
    if (count == 0 && !pattern.StartsWith("ev_gesture") && !pattern.StartsWith("layerelement") && !pattern.StartsWith("tile_") && !pattern.StartsWith("spritespeed") && !pattern.StartsWith("time_source") && !pattern.StartsWith("sequence") && !pattern.StartsWith("seqtrack") && pattern is not ("pi" or "true" or "false" or "undefined" or "noone" or "all" or "self" or "other" or "global")) problems.Add("no constant matches " + pattern);
    var page = pageOf.Length == 0 ? null : entries.FirstOrDefault(e => e.K == "f" && e.N == pageOf)?.U;
    if (pageOf.Length > 0 && page is null) problems.Add("no page for the constants of " + pattern + ": " + pageOf);
    entries.Add(new Entry { N = pattern, K = "c", C = category, S = summary, U = page });
}

var functions = entries.Count(e => e.K == "f");
var options = new JsonSerializerOptions { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var doc = new { manual = "https://manual.gamemaker.io/lts/en/GameMaker_Language/GML_Reference/", entries = entries.OrderBy(e => e.N, StringComparer.Ordinal).ToList() };
var json = JsonSerializer.Serialize(doc, options).Replace("},{\"n\"", "},\n{\"n\"", StringComparison.Ordinal);
File.WriteAllText(args[0], json);
Console.WriteLine($"{unchecked_} functions not in the built-in table. {entries.Count} entries, {functions} functions, {entries.Count(e => e.K == "v")} variables, {entries.Count(e => e.K == "c")} constant groups, {json.Length} bytes");
foreach (var p in problems) Console.WriteLine("PROBLEM " + p);

sealed class Entry
{
    [JsonPropertyName("n")] public string N { get; set; } = "";
    [JsonPropertyName("k")] public string K { get; set; } = "";
    [JsonPropertyName("c")] public string C { get; set; } = "";
    [JsonPropertyName("s")] public string S { get; set; } = "";
    [JsonPropertyName("p")] public List<string[]>? P { get; set; }
    [JsonPropertyName("r")] public string? R { get; set; }
    [JsonPropertyName("d")] public int? D { get; set; }
    [JsonPropertyName("ro")] public int? Ro { get; set; }
    [JsonPropertyName("u")] public string? U { get; set; }
}
