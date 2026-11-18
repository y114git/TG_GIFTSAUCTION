using System.Collections;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using G3MStudio.Core.Workspace;
using ImageMagick;

// Builds the small test fixtures from real game files: dotnet run --project tools/fixture-builder -- <game folder> <output folder> [timeline] [wav]
// Every kind of resource is kept once per distinct feature; everything nothing points at is dropped.

if (args[0] == "verify")
{
    Environment.ExitCode = await Verify.Run(args[1], args[2]);
    return;
}

if (args[0] == "names")
{
    using var nd = await GameMakerIO.ReadAsync(args[1]);
    Console.WriteLine(string.Join(" ", nd.Variables.Select(v => v.Name.Content)));
    Console.WriteLine(string.Join(" ", nd.Functions.Select(v => v.Name.Content)));
    return;
}

if (args[0] == "check")
{
    foreach (var f in args.Skip(1))
        try { using var d = await GameMakerIO.ReadAsync(f); Console.WriteLine("ok " + f + " code " + d.Code.Count); }
        catch (Exception e) { Console.WriteLine("FAIL " + f + " " + e.Message); }
    return;
}

var (source, target) = (args[0], args[1]);
var addTimeline = args.Contains("timeline");
Directory.CreateDirectory(target);
using var data = await GameMakerIO.ReadAsync(Path.Combine(source, "data.win"));
Console.WriteLine($"read {data.Sprites?.Count} sprites {data.GameObjects?.Count} objects {data.Rooms?.Count} rooms");

var originalCode = data.Code.ToList();
var originalLocals = data.CodeLocals?.ToList() ?? [];
var keep = new HashSet<object>(ReferenceEqualityComparer.Instance);
void Take<T>(IList<T>? list, Func<T, long> cost, params Func<T, object?>[] keys) where T : class
{
    if (list is null) return;
    foreach (var key in keys)
        foreach (var group in list.Where(i => i is not null).GroupBy(key))
            keep.Add(group.OrderBy(cost).First());
}

static long Len(GameMakerCode? c) => c?.Instructions?.Count ?? 0;
Take(data.Sprites, s => (long)s.Width * s.Height * Math.Max(1, s.Textures?.Count ?? 1) + (s.SpineJSON?.Length ?? 0),
    s => (s.Textures?.Count ?? 0) switch { 0 => 0, 1 => 1, _ => 2 }, s => s.CollisionMasks?.Count > 0, s => s.SepMasks, s => s.V3NineSlice?.Enabled == true,
    s => s.SSpriteType, s => s.BBoxMode, s => s.IsSpecialType, s => s.OriginX != 0, s => s.GMS2PlaybackSpeedType);
Take(data.Backgrounds, b => b.GMS2TileCount + 1, b => b.GMS2TileWidth > 0, b => b.Transparent);
Take(data.Sounds, s => (s.AudioFile?.Data?.Length ?? 0) + 1, s => s.Flags, s => s.GroupID, s => s.Type?.Content, s => s.AudioFile?.Data is { } d ? (SoundAudio.Format(d), d.Length > 2000) : null);
Take(data.Paths, p => p.Points?.Count ?? 0, p => p.IsSmooth, p => p.IsClosed);
Take(data.Scripts?.Where(s => s.Name.Content?.StartsWith("GMLive", StringComparison.Ordinal) != true).ToList(), s => Len(s.Code), s => s.Code?.ChildEntries?.Count > 0, s => s.Code is null);
Take(data.Shaders, s => (s.GLSL_Vertex?.Content?.Length ?? 0) + (s.GLSL_Fragment?.Content?.Length ?? 0), s => s.Type);
Take(data.Fonts, f => (f.Glyphs?.Count ?? 0) * 10L, f => f.Bold, f => f.Italic, f => f.EmSizeIsFloat, f => f.SDFSpread > 0, f => f.Glyphs?.Any(g => g.Kerning?.Count > 0) == true, f => f.AntiAliasing, f => f.Charset);
Take(data.Timelines, t => t.Moments?.Count ?? 0, t => t.Moments?.Count > 0);
Take(data.Extensions, e => e.Files?.Count ?? 0, e => true);
Take(data.Sequences, q => (q.Tracks?.Count ?? 0) * 10L + (q.Moments?.Count ?? 0), q => q.Tracks?.Count > 0, q => q.Moments?.Count > 0, q => q.BroadcastMessages?.Count > 0, q => q.Playback);
Take(data.ParticleSystems, p => p.Emitters?.Count ?? 0, p => p.Emitters?.Count > 0);
Take(data.AnimationCurves, c => c.Channels?.Count ?? 0, c => c.GraphType, c => c.Channels?.Count > 0);
var eventKinds = data.GameObjects is { Count: > 0 } ? data.GameObjects.Max(o => o.Events.Count) : 0;
var eventKeys = Enumerable.Range(0, eventKinds).Select<int, Func<GameMakerGameObject, object?>>(i => o => o.Events.Count > i && o.Events[i].Count > 0).ToArray();
Take(data.GameObjects, o => o.Events.SelectMany(e => e).SelectMany(e => e.Actions).Sum(a => Len(a.CodeId)) + o.Events.Sum(e => e.Count) + (o.PhysicsVertices?.Count ?? 0),
    [o => o.Sprite is not null, o => o.ParentId is not null, o => o.Persistent, o => o.Solid, o => o.Visible, o => o.UsesPhysics, o => o.TextureMaskId is not null, o => o.CollisionShape, o => o.IsSensor, .. eventKeys]);
long RoomCost(GameMakerRoom r) => (r.GameObjects?.Count ?? 0) * 3L + (r.Tiles?.Count ?? 0) + (r.Layers?.Sum(l => 5 + (l.InstancesData?.Instances?.Count ?? 0) * 3) ?? 0) + Len(r.CreationCodeId);
var layerTypes = data.Rooms?.SelectMany(r => r.Layers ?? []).Select(l => l.LayerType).Distinct().ToList() ?? [];
Take(data.Rooms, RoomCost, [r => r.Views?.Any(v => v.Enabled) == true, r => r.Backgrounds?.Any(b => b.BackgroundDefinition is not null) == true, r => r.Tiles?.Count > 0, r => r.GameObjects?.Count > 0, r => r.CreationCodeId is not null,
    r => r.World, r => r.Persistent, r => r.Flags, .. layerTypes.Select(t => (Func<GameMakerRoom, object?>)(r => r.Layers?.Any(l => l.LayerType == t) == true))]);
Take(data.Scripts?.Where(s => s.Name.Content?.StartsWith("GMLive", StringComparison.Ordinal) != true).ToList(), s => Len(s.Code), s => Simple(s));
Take(data.GameObjects, o => o.Events.SelectMany(e => e).SelectMany(e => e.Actions).Sum(a => Len(a.CodeId)),
    o => o.Events.Sum(e => e.Count) is > 1 and < 5 && o.Events.SelectMany(e => e).All(e => e.Actions.Count == 1 && e.Actions[0].CodeId is not null));
// one script that code calls, with the object whose event calls it
var usageIndex = G3MLib.DataFile.References.ReferenceIndex.Build(data, default);
GameMakerScript? calledScript = null;
GameMakerGameObject? caller = null;
foreach (var script in data.Scripts.Where(s => s is not null).Take(600).OrderBy(s => Len(s.Code)))
{
    var owner = usageIndex.FindUsages(script).Where(u => u.InstructionOffset >= 0).Select(u => u.OwnerName).FirstOrDefault(n => n.StartsWith("gml_Object_", StringComparison.Ordinal));
    if (owner is null || data.GameObjects.FirstOrDefault(o => owner.StartsWith("gml_Object_" + o.Name.Content + "_", StringComparison.Ordinal)) is not { } found) continue;
    (calledScript, caller) = (script, found);
    break;
}

if (calledScript is not null && caller is not null) { keep.Add(calledScript); keep.Add(caller); }
// a collision event names its partner by position
foreach (var o in data.GameObjects.Where(keep.Contains).ToList())
    foreach (var e in o.Events[(int)EventType.Collision]) if (e.EventSubtype < data.GameObjects.Count) keep.Add(data.GameObjects[(int)e.EventSubtype]);
Console.WriteLine($"chosen {keep.Count} roots");

// close over what the chosen resources point at (parents, sprites, tile sets, sequences ...)
var reached = Walker.Reach(keep);
var types = ResourceTypes.All.Where(t => !t.IsData && t.Key != "general").ToList();
foreach (var t in types) foreach (var item in t.Items(data) ?? []) if (reached.Contains(item)) keep.Add(item);

// delete the rest through the same commands the editor uses; references to deleted items are cleared
foreach (var key in new[] { "rooms", "objects", "sprites", "tileSets", "sounds", "paths", "scripts", "shaders", "fonts", "timelines", "extensions", "sequences", "animationCurves", "particleSystems" })
{
    var type = types.First(t => t.Key == key);
    var before = type.Items(data)?.Count() ?? 0;
    if (before == 0) continue;
    foreach (var item in (type.Items(data) ?? []).Where(i => !keep.Contains(i)).ToList())
    {
        var name = ResourceTypes.NameOf(item);
        new ClearReferencesCommand(key, name, true).Apply(data);
        new ResourceListCommand(key, ResourceListCommand.Delete, name, true).Apply(data);
    }

    Console.WriteLine($"{key}: {before} -> {(type.Items(data) ?? []).Count()}");
}

// global init lists may point at code that left with its script
foreach (var list in new[] { data.GlobalInitScripts, data.GameEndScripts })
    if (list is not null) Refill(list, list.Where(g => g.Code is not null && data.Code.Contains(g.Code)).Take(2).Cast<object>().ToList());

// the games keep their sounds as OGG; one is stored as WAV so the WAV editing tests have a sound to read
if (args.Contains("wav") && data.Sounds.Where(s => s.AudioFile?.Data is { } d && SoundAudio.Format(d) == "ogg").OrderBy(s => s.AudioFile?.Data.Length).FirstOrDefault() is { AudioFile: { } clip } oggSound)
{
    clip.Data = AudioCodec.Convert(clip.Data, "wav");
    Console.WriteLine($"{oggSound.Name.Content} stored as WAV, {clip.Data.Length} bytes");
}

if (addTimeline && data.Timelines is { Count: 0 })
{
    var timeline = new GameMakerTimeline { Name = data.Strings.MakeString("timeline_fixture") };
    timeline.Moments.Add(new GameMakerTimeline.GameMakerTimelineMoment(0, []));
    timeline.Moments.Add(new GameMakerTimeline.GameMakerTimelineMoment(30, []));
    data.Timelines.Add(timeline);
}

var listChunks = new[] { "STRG", "TPAG", "TXTR", "AUDO", "VARI", "FUNC", "CODE" };
IEnumerable<object> Roots() => data.FORM.ChunksTypeDict.Values.Where(c => !listChunks.Contains(c.GetType().Name[^4..])).Cast<object>().Concat(data.CodeLocals?.Cast<object>() ?? []);
HashSet<object> live = [];

// code: keep what a kept resource points at (with its parents and children); pictures, audio, strings, variables and functions that nothing uses any more
void Prune(List<GameMakerCode> candidates, List<GameMakerCodeLocals> candidateLocals)
{
    live = Walker.Reach(Roots().Where(r => r is not GameMakerCodeLocals));
    var codeKeep = candidates.Where(live.Contains).ToList();
    Refill(data.Code, codeKeep.Cast<object>());
    if (data.CodeLocals is { } locals)
    {
        var names = codeKeep.Select(c => c.Name.Content).ToHashSet();
        foreach (var c in codeKeep) if (locals.All(l => l.Name.Content != c.Name.Content) && candidateLocals.FirstOrDefault(l => l.Name.Content == c.Name.Content) is { } l0) locals.Add(l0);
        Refill(locals, locals.Where(l => names.Contains(l.Name.Content)).Cast<object>().ToList());
    }

    foreach (var (name, list) in new (string, IEnumerable<object>?)[] { ("texturePageItems", data.TexturePageItems), ("embeddedAudio", data.EmbeddedAudio), ("embeddedTextures", data.EmbeddedTextures), ("functions", data.Functions), ("variables", data.Variables), ("strings", data.Strings) })
    {
        if (list is null) continue;
        live = Walker.Reach(Roots());
        foreach (var group in data.TextureGroupInfo ?? [])
            foreach (var p in group.GetType().GetProperties().Where(p => p.PropertyType == typeof(GameMakerString)))
                if (p.GetValue(group) is { } text) live.Add(text);
        var keepers = list.Where(live.Contains).ToList();
        Console.WriteLine($"{name}: {list.Count()} -> {keepers.Count}");
        Refill(list, keepers);
    }

    // texture groups list sprites and pages by reference
    var all = types.SelectMany(t => t.Items(data) ?? []).Concat(data.EmbeddedTextures).Cast<object>().ToHashSet(ReferenceEqualityComparer.Instance);
    foreach (var group in data.TextureGroupInfo ?? [])
        foreach (var p in group.GetType().GetProperties().Where(p => typeof(IList).IsAssignableFrom(p.PropertyType) && p.GetIndexParameters().Length == 0))
            if (p.GetValue(group) is IList list)
                Refill(list, list.Cast<object>().Where(i => all.Contains(i) || i.GetType().GetProperty("Resource")?.GetValue(i) is { } r && all.Contains(r)).ToList());
}

Prune(originalCode, originalLocals); // a kept child may belong to the entry of a deleted script

// everything of the game is replaced by generated content of the same shape, then what the new code and text no longer use is dropped again
var rng = new Random(20261107);
Anonymize.Run(data, rng, new DirectoryInfo(target).Name[0]);
Prune(data.Code.ToList(), data.CodeLocals?.ToList() ?? []);
Anonymize.Texts(data);

// new pictures for every rectangle that a kept item shows; the rest of a page stays transparent
foreach (var page in data.EmbeddedTextures)
{
    if (page.TextureData?.Image is not { } old) continue;
    using var blank = new MagickImage(MagickColors.Transparent, (uint)old.Width, (uint)old.Height);
    foreach (var item in data.TexturePageItems.Where(i => ReferenceEquals(i.TexturePage, page) && i.SourceWidth > 0 && i.SourceHeight > 0))
    {
        using var piece = Anonymize.Picture(rng, item.SourceWidth, item.SourceHeight);
        blank.Composite(piece, item.SourceX, item.SourceY, CompositeOperator.Copy);
    }

    if (old.Format == GMImage.ImageFormat.Png)
    {
        blank.Quality = 99;
        page.TextureData.Image = GMImage.FromPng(blank.ToByteArray(MagickFormat.Png32));
    }
    else page.TextureData.Image = GMImage.FromMagickImage(blank).ConvertToFormat(old.Format);
    Console.WriteLine($"page {page.Name?.Content} {old.Format} {old.Width}x{old.Height} -> {page.TextureData.Image.Format} {old.GetData().Length} -> {page.TextureData.Image.GetData().Length}");
}

foreach (var embedded in data.EmbeddedAudio) embedded.Data = Anonymize.Tone(rng, embedded.Data);
var output = Path.Combine(target, "data.win");

// the audio group file the kept sounds use: only their entries
foreach (var group in data.Sounds?.Where(s => s.GroupID > 0 && s.AudioFile is null).GroupBy(s => s.GroupID).ToList() ?? [])
{
    var groupFile = Directory.EnumerateFiles(source, $"audiogroup{group.Key}.dat").FirstOrDefault();
    if (groupFile is null) continue;
    using var audio = await GameMakerIO.ReadAsync(groupFile);
    var entries = group.Select(s => audio.EmbeddedAudio[s.AudioID]).Distinct().ToList();
    foreach (var s in group) s.AudioID = entries.IndexOf(audio.EmbeddedAudio[s.AudioID]);
    foreach (var entry in entries) entry.Data = Anonymize.Tone(rng, entry.Data);
    Refill(audio.EmbeddedAudio, entries.Cast<object>());
    await GameMakerIO.WriteAsync(Path.Combine(target, $"audiogroup{group.Key}.dat"), audio);
    Console.WriteLine($"wrote audiogroup{group.Key}.dat, {entries.Count} entries");
}

await GameMakerIO.WriteAsync(output, data);
Console.WriteLine($"wrote {output}: {new FileInfo(output).Length} bytes");

static void Refill(dynamic list, IEnumerable<object> items)
{
    list.Clear();
    foreach (dynamic i in items) list.Add(i);
}

// A script whose code is short plain text, for the copy test
bool Simple(GameMakerScript s)
{
    if (s.Code is not { ParentEntry: null, ChildEntries.Count: 0, Name.Content: { Length: > 0 } name } c || c.Instructions.Count is < 7 or > 150) return false;
    try { return CodeText.Decompile(data, name) is { Length: > 10 and < 600 } text && !text.Contains("function", StringComparison.Ordinal); }
    catch (Exception e) when (e is not OutOfMemoryException) { return false; }
}
