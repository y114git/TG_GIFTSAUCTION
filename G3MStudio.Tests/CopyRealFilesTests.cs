#nullable disable
#pragma warning disable CA2025, CA2000
#pragma warning disable CA1849 // the timings call the blocking forms on purpose
using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>
/// Copy to Other Data File on real games. Runs only when G3MSTUDIO_PERF is set. Pairs come from G3MSTUDIO_COPY_PAIRS ("source|target;source|target")
/// or, when that is empty, from G3MSTUDIO_PERF_FILES: every file is copied into the next one in the list. Outputs go to a temp folder; the game
/// files are only read. Numbers go to the test output (use the detailed console logger).
/// </summary>
public sealed partial class CopyRealFilesTests(ITestOutputHelper output)
{
    private static bool On => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("G3MSTUDIO_PERF"));

    public static IEnumerable<(string Source, string Target)> Pairs
    {
        get
        {
            if (!On) return [];
            var given = Environment.GetEnvironmentVariable("G3MSTUDIO_COPY_PAIRS") ?? "";
            if (given.Length > 0)
                return given.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('|')).Where(p => p.Length == 2 && File.Exists(p[0]) && File.Exists(p[1])).Select(p => (p[0], p[1])).ToList();
            var files = (Environment.GetEnvironmentVariable("G3MSTUDIO_PERF_FILES") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Where(File.Exists).ToList();
            return files.Count < 2 ? [] : files.Select((f, i) => (f, files[(i + 1) % files.Count])).ToList();
        }
    }

    [Fact]
    public async Task RealGamesCopyBetweenEachOther()
    {
        Assert.SkipWhen(!Pairs.Any(), "G3MSTUDIO_PERF and G3MSTUDIO_PERF_FILES (or G3MSTUDIO_COPY_PAIRS) are not set");
        var problems = new List<string>();
        foreach (var (source, target) in Pairs)
        {
            Say($"=== {Label(source)} -> {Label(target)}");
            try { await RunPair(source, target, problems); }
            catch (Exception e) { problems.Add($"{Label(source)} -> {Label(target)}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
            GC.Collect();
        }

        foreach (var problem in problems) Say("PROBLEM " + problem);
        Assert.Empty(problems);
    }

    // Also appended to the file in G3MSTUDIO_COPY_LOG, so a long run can be followed before the test ends.
    private void Say(string text)
    {
        output.WriteLine(text);
        if (Environment.GetEnvironmentVariable("G3MSTUDIO_COPY_LOG") is { Length: > 0 } log) File.AppendAllText(log, text + Environment.NewLine);
    }

    private static string Label(string path)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(path));
        return folder.StartsWith("chapter", StringComparison.Ordinal) ? "DELTARUNE " + folder : folder;
    }

    private sealed record Expect(string Type, string Source, string Target, Dictionary<string, string> Map);

    private async Task RunPair(string sourcePath, string targetPath, List<string> problems)
    {
        var token = TestContext.Current.CancellationToken;
        var pair = $"{Label(sourcePath)} -> {Label(targetPath)}";
        void Fail(string text)
        {
            problems.Add($"{pair}: {text}");
            Say("  PROBLEM " + text);
        }
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));

        var watch = Stopwatch.StartNew();
        using var source = await service.OpenDetachedAsync(sourcePath, null, token);
        var target = await service.OpenDetachedAsync(targetPath, null, token);
        Say($"  opened both in {watch.Elapsed.TotalMilliseconds:F0} ms; source {CopyVersion.Of(source.Data)}, target {CopyVersion.Of(target.Data)}");

        var refusal = CopyVersion.Of(source.Data).RefusalFor(CopyVersion.Of(target.Data));
        var sample = Sample(source.Data, target.Data, refusal is not null, out var notes);
        foreach (var note in notes) Say("  sample: " + note);
        Say("  sample: " + string.Join(", ", sample.Select(s => s.Type + ":" + s.Name)));

        var sourceBefore = Signature(source.Data);
        var sourceItems = sample.ToDictionary(s => s.Type + ":" + s.Name, s => Fp(source.Data, s.Type, s.Name, null));
        var sourceHash = WrittenHash(source.Data);

        if (refusal is not null)
        {
            var refused = await ResourceCopier.PlanAsync(source, target, sample, true, CollisionMode.Rename, null, token);
            Say("  refused as expected: " + refusal);
            if (refused.CanApply || refused.Entries.Any(e => e.Action != CopyAction.Refused)) Fail("a pair with different bytecode versions was not refused");
            target.Dispose();
            return;
        }

        var baseline = Verify(target.Data).ToHashSet();
        var loadWarnings = target.Data.LoadWarnings.Count;
        var pipeline = new SavePipeline(userData);

        // The target alone, saved and read again: a failure here is not the copy's.
        var baselineSaved = await SaveAndCheck("baseline", target, source.Data, pipeline, dir.File("saved-baseline.win"), [], baseline, loadWarnings, text => Say("  baseline: " + text), token);
        Action<string> saveFail = baselineSaved ? Fail : text => Say("  (target cannot be saved on its own) " + text);
        Say($"  baseline: the target alone {(baselineSaved ? "saves and reads back" : "does NOT save, later save failures are not caused by the copy")}");
        var graph = CopyGraph.Build(source.Data);
        var codeBefore = new Dictionary<string, object>();
        foreach (var c in target.Data.Code) codeBefore.TryAdd(c.Name.Content, c);
        var expects = new List<Expect>();
        var applied = 0;
        var saved = false;

        // A adds everything (the names are free), B copies the same items again (Rename), C again with Skip (nothing to do), D with Replace.
        foreach (var (step, mode) in new[] { ("A add", CollisionMode.Rename), ("B rename", CollisionMode.Rename), ("C skip", CollisionMode.Skip), ("D replace", CollisionMode.Replace) })
        {
            if (step == "A add" && baselineSaved && Environment.GetEnvironmentVariable("G3MSTUDIO_COPY_BISECT") is { Length: > 0 })
            {
                // Which sample item makes the save fail: copy each item alone into the untouched target, save, undo.
                foreach (var item in sample)
                {
                    var single = await ResourceCopier.PlanAsync(source, target, [item], true, CollisionMode.Rename, null, token);
                    if (!single.CanApply) continue;
                    try { await ResourceCopier.CopyAsync(source, target, single, null, token); }
                    catch (Exception e) { Say($"  bisect {item.Type}:{item.Name}: copy failed {e.Message}"); continue; }
                    SaveResult result;
                    try { result = await target.SaveCopyAsync(pipeline, dir.File("bisect.win"), null, token); }
                    catch (Exception e) { Say($"  bisect {item.Type}:{item.Name}: save threw {e.GetType().Name} {string.Join(" / ", e.Message.Split('\n').Take(3))}"); target.Undo(); continue; }
                    Say($"  bisect {item.Type}:{item.Name} ({single.Applied.Count()} items): save {(result.Saved ? "ok" : "FAILED " + result.Message?.Split('\n')[0])}");
                    target.Undo();
                    if (result.Saved) continue;
                    // Narrow it down by type, without following uses.
                    foreach (var group in single.Applied.GroupBy(e => e.Type))
                    {
                        var part = await ResourceCopier.PlanAsync(source, target, [.. group.Select(e => (e.Type, e.Name))], false, CollisionMode.Rename, null, token);
                        if (!part.CanApply) continue;
                        try
                        {
                            await ResourceCopier.CopyAsync(source, target, part, null, token);
                            var partResult = await target.SaveCopyAsync(pipeline, dir.File("bisect.win"), null, token);
                            Say($"    bisect group {group.Key} ({part.Applied.Count()} items): save {(partResult.Saved ? "ok" : "FAILED")}");
                            if (!partResult.Saved)
                            {
                                var fresh = target.Data.Code.Where(c => !codeBefore.ContainsKey(c.Name.Content)).ToList();
                                Say($"      new code entries: {fresh.Count}; without instructions: {string.Join(", ", fresh.Where(c => c.Instructions.Count == 0).Take(5).Select(c => c.Name.Content + "/children " + c.ChildEntries.Count))}; all: {string.Join(", ", fresh.Take(6).Select(c => c.Name.Content + "=" + c.Instructions.Count))}");
                                var raw = dir.File("raw.win");
                                TestData.Write(raw, target.Data);
                                try { (await GameMakerIO.ReadAsync(raw, new Progress<GameMakerProgress>(), token)).Dispose(); }
                                catch (Exception readError) { Say("      read back: " + string.Join(" || ", readError.ToString().Split('\n').Take(14).Select(l => l.Trim()))); }
                            }
                        }
                        catch (Exception e) { Say($"    bisect group {group.Key}: {e.GetType().Name} {e.Message.Split('\n')[0]}"); }
                        finally { if (target.History.CanUndo) target.Undo(); }
                    }
                }
            }

            var before = Signature(target.Data);
            var beforeCounts = ResourceTypes.Counts(target.Data);
            var wrappersBefore = Wrappers(target.Data);
            var planWatch = Stopwatch.StartNew();
            var plan = await ResourceCopier.PlanAsync(source, target, sample, true, mode, null, token);
            var planMs = planWatch.Elapsed.TotalMilliseconds;
            var summary = string.Join(", ", plan.Entries.GroupBy(e => e.Action).Select(g => g.Count() + " " + g.Key));
            Say($"  [{step}] plan {planMs:F0} ms: {plan.Entries.Count} entries ({summary}; {plan.Entries.Count(e => e.IsDependency)} dependencies), warnings {plan.Warnings.Count}, can apply {plan.CanApply}");
            foreach (var w in plan.Warnings.Take(3)) Say("    warning: " + w);
            foreach (var e in plan.Entries.Where(e => e.Action == CopyAction.Refused).Take(3)) Say($"    refused {e.Type}:{e.Name}: {e.Refusal}");
            Say($"    by type: {string.Join(", ", plan.Applied.GroupBy(e => e.Type).Select(g => g.Count() + " " + g.Key))}");

            if (plan.Refusal is not null) { Fail($"[{step}] whole plan refused: {plan.Refusal}"); continue; }
            foreach (var e in plan.Entries.Where(e => !e.IsDependency && e.Action == CopyAction.Refused)) Fail($"[{step}] selected {e.Type}:{e.Name} refused: {e.Refusal}");
            CheckPlan(step, mode, plan, Fail);
            if (!plan.CanApply)
            {
                if (Signature(target.Data) != before) Fail($"[{step}] a plan with nothing to apply changed the target");
                continue;
            }

            using var peak = new Peak();
            var copyWatch = Stopwatch.StartNew();
            CopyResourcesCommand command;
            try { command = await ResourceCopier.CopyAsync(source, target, plan, null, token); }
            catch (Exception e)
            {
                // Code of a newer GameMaker version can use syntax an older target cannot compile; the copy stops, says so, and changes nothing.
                if (e is InvalidOperationException && e.Message.Contains("in this GameMaker version", StringComparison.Ordinal))
                {
                    Say($"  [{step}] refused at apply, as a version limit: {e.Message}");
                    if (Signature(target.Data) != before) Fail($"[{step}] the refused copy left the target changed");
                    continue;
                }

                Fail($"[{step}] copy failed:{e.GetType().Name}: {e.Message} at {string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(5).Select(l => l.Trim()))}");
                if (Signature(target.Data) != before) Fail($"[{step}] the failed copy left the target changed");
                await CopyOneByOne(source, target, sample, mode, Fail, token);
                continue;
            }

            peak.Stop();
            var changedCode = target.Data.Code.Where(c => codeBefore.TryGetValue(c.Name.Content, out var n) && !ReferenceEquals(n, c)).Select(c => c.Name.Content).ToList();
            Say($"    code entries of the target replaced by new objects: {changedCode.Count} {string.Join(", ", changedCode.Take(5))}");
            foreach (var replaced in changedCode.Take(20)) { var e = plan.Applied.FirstOrDefault(x => graph.OwnedCode(x.Type, x.Name).Contains(replaced)); Say($"      {replaced} is owned by {e?.Type}:{e?.Name} ({e?.Action})"); }
            applied++;
            Say($"  [{step}] copy {copyWatch.Elapsed.TotalMilliseconds:F0} ms, package {command.Args["package"].Length / 1024} KB as text, peak working set +{peak.PeakMb - peak.StartMb:F0} MB (absolute {peak.PeakMb:F0} MB), peak managed +{peak.PeakManagedMb - peak.StartManagedMb:F0} MB");
            var after = Signature(target.Data);

            // One more entry per added or renamed item of each type, none for a replaced one.
            var afterCounts = ResourceTypes.Counts(target.Data);
            foreach (var type in CopyPlanner.Supported.Where(k => k != "code" && beforeCounts.ContainsKey(k)))
            {
                var expected = beforeCounts[type] + plan.Applied.Count(e => e.Type == type && e.Action != CopyAction.Replace);
                // Compiling the code of a 2.3 script adds its gml_Script_ function entries (the planner refuses them as entries of their own).
                var actual = afterCounts.GetValueOrDefault(type) - (type == "scripts" ? Wrappers(target.Data) - wrappersBefore : 0);
                if (actual != expected) Fail($"[{step}] {type}: {actual} after the copy, expected {expected}");
                if (type == "scripts" && Wrappers(target.Data) != wrappersBefore) Say($"    note: {Wrappers(target.Data) - wrappersBefore} gml_Script_ function entries came with the compiled code");
            }

            foreach (var e in plan.Applied)
                if (!CopyPlanner.Exists(target.Data, e.Type, e.TargetName)) Fail($"[{step}] {e.Type}:{e.TargetName} is not in the target (similar: {string.Join(", ", target.Data.Scripts.Select(s => s.Name.Content).Concat(target.Data.Code.Select(c => c.Name.Content)).Where(n => n.Contains(e.Name, StringComparison.Ordinal)).Take(6))})");
            foreach (var added in Verify(target.Data).Where(p => !baseline.Contains(p)).Take(10)) Fail($"[{step}] consistency: {added}");

            var map = plan.Applied.Where(e => e.TargetName != e.Name).ToDictionary(e => e.Name, e => e.TargetName);
            foreach (var e in plan.Applied)
            {
                expects.Add(new Expect(e.Type, e.Name, e.TargetName, map));
                var want = Fp(source.Data, e.Type, e.Name, map);
                var got = Fp(target.Data, e.Type, e.TargetName, null);
                if (want != got) Fail($"[{step}] {e.Type}:{e.Name} differs in the target: source '{Short(want)}', target '{Short(got)}'");
            }

            CompareCode(step, source, target, graph, plan, Fail);

            // Undo puts back the exact counts and lists, redo repeats the copy, and a command rebuilt from its journal arguments does the same.
            var clock = Stopwatch.StartNew();
            target.Undo();
            var undoMs = clock.Elapsed.TotalMilliseconds;
            if (Signature(target.Data) != before) Fail($"[{step}] undo did not restore the target");
            foreach (var stale in Verify(target.Data).Where(p => !baseline.Contains(p)).Take(5)) Fail($"[{step}] after undo: {stale}");
            if (!ResourceTypes.Counts(target.Data).OrderBy(p => p.Key).SequenceEqual(beforeCounts.OrderBy(p => p.Key))) Fail($"[{step}] undo changed the item counts");
            clock.Restart();
            target.Redo();
            var redoMs = clock.Elapsed.TotalMilliseconds;
            if (Signature(target.Data) != after) Fail($"[{step}] redo did not repeat the copy");
            target.Undo();
            clock.Restart();
            if (!target.Edit(EditCommand.Create(command.Kind, command.Args))) Fail($"[{step}] the replayed command was refused");
            var replayMs = clock.Elapsed.TotalMilliseconds;
            if (Signature(target.Data) != after) Fail($"[{step}] the replay from journal arguments gave another state");
            Say($"  [{step}] undo {undoMs:F0} ms, redo {redoMs:F0} ms, replay from arguments {replayMs:F0} ms");

            if (step == "A add") saved = await SaveAndCheck("after A", target, source.Data, pipeline, dir.File("saved-a.win"), expects, baseline, loadWarnings, saveFail, token);
        }

        var finalSignature = Signature(target.Data, false);
        if (applied > 0 && baselineSaved && !saved) Fail("the target was not saved after step A");
        else if (applied > 1) await SaveAndCheck("final", target, source.Data, pipeline, dir.File("saved-final.win"), expects, baseline, loadWarnings, saveFail, token);

        // A crash: the target is dropped with its edits unsaved and opened again; the journal rebuilds them from their arguments.
        target.Dispose();
        var reopenWatch = Stopwatch.StartNew();
        using (var again = await service.OpenDetachedAsync(targetPath, null, token))
        {
            Say($"  journal replay on reopen {reopenWatch.Elapsed.TotalMilliseconds:F0} ms (with the open): {again.RecoveredEdits} edits recovered, {applied} expected");
            if (again.RecoveredEdits != applied) Fail($"the journal recovered {again.RecoveredEdits} edits, expected {applied}");
            if (Signature(again.Data, false) is var replayed && replayed != finalSignature) Fail($"the reopened target (journal replay) differs from the state before it was dropped: before '{finalSignature}', after '{replayed}'");
        }

        // The source stays the same in model terms.
        if (Signature(source.Data) != sourceBefore) Fail("the source model changed");
        foreach (var (key, fp) in sourceItems)
            if (SplitKey(key) is var (type, name) && Fp(source.Data, type, name, null) != fp) Fail($"source item {key} changed");
        if (source.History.IsDirty || source.History.Position != 0) Fail("the source has edits");
        if (WrittenHash(source.Data) != sourceHash) Fail("the source writes different bytes after the copies");
    }

    private static (string, string)? SplitKey(string key) => key.IndexOf(':', StringComparison.Ordinal) is var i and > 0 ? (key[..i], key[(i + 1)..]) : null;

    private static string Short(string text) => text.Length <= 160 ? text : text[..160] + "...";

    private static string WrittenHash(GameMakerData data)
    {
        // The writer stamps the file, so the hash is taken of the bytes without the header's time field only when the stamp is stable.
        return Convert.ToHexString(SHA256.HashData(TestData.Bytes(data)));
    }

    // ---- plan rules ----

    private static void CheckPlan(string step, CollisionMode mode, CopyPlan plan, Action<string> fail)
    {
        foreach (var e in plan.Entries.Where(e => e.Action != CopyAction.Refused))
        {
            var expected = !e.Collides ? CopyAction.Add
                : e.IsDependency ? CopyAction.Skip
                : mode switch { CollisionMode.Skip => CopyAction.Skip, CollisionMode.Replace => e.SameType ? CopyAction.Replace : CopyAction.Rename, _ => CopyAction.Rename };
            if (e.Action != expected) fail($"[{step}] {e.Key}: the plan says {e.Action}, expected {expected}");
            if (e.Action == CopyAction.Rename && !e.TargetName.StartsWith(e.Name + "_copy", StringComparison.Ordinal)) fail($"[{step}] {e.Key} is renamed to '{e.TargetName}'");
        }

        var names = plan.Applied.Select(e => e.TargetName).ToList();
        if (names.Count != names.Distinct().Count()) fail($"[{step}] two items of the plan get the same name in the target");
    }

    // When the whole copy fails, each sample item alone tells which one does it.
    private static async Task CopyOneByOne(Workspace source, Workspace target, List<(string Type, string Name)> sample, CollisionMode mode, Action<string> fail, CancellationToken token)
    {
        var done = 0;
        foreach (var item in sample)
        {
            try
            {
                var plan = await ResourceCopier.PlanAsync(source, target, [item], true, mode, null, token);
                if (!plan.CanApply) continue;
                await ResourceCopier.CopyAsync(source, target, plan, null, token);
                done++;
            }
            catch (Exception e) { fail($"  alone: {item.Type}:{item.Name} failed: {e.GetType().Name}: {e.Message}"); }
        }

        for (; done > 0; done--) target.Undo();
    }

    // ---- sample ----

    private static List<(string Type, string Name)> Sample(GameMakerData d, GameMakerData t, bool unchecked_, out List<string> notes)
    {
        var log = new List<string>();
        notes = log;
        var picked = new List<(string, string)>();
        void Pick(string type, IEnumerable<string> candidates, bool smallest = false)
        {
            var any = false;
            if (smallest && !unchecked_ && Environment.GetEnvironmentVariable("G3MSTUDIO_COPY_BIGROOMS") is not { Length: > 0 })
            {
                // A room can pull in most of a game; the sample takes the one with the fewest dependencies of the first few.
                var best = candidates.Take(6).Select(n => (Name: n, Plan: CopyPlanner.Build(d, t, [(type, n)], true, CollisionMode.Rename, null)))
                    .Where(c => c.Plan.Entries.Any(e => e.Name == c.Name && e.Action != CopyAction.Refused)).OrderBy(c => c.Plan.Entries.Count).FirstOrDefault();
                var (bestName, _) = best;
                if (bestName is not null) { picked.Add((type, bestName)); return; }
            }

            foreach (var name in candidates.Take(12))
            {
                any = true;
                if (unchecked_) { picked.Add((type, name)); return; }
                var one = CopyPlanner.Build(d, t, [(type, name)], false, CollisionMode.Rename, null);
                if (one.Entries[0].Action != CopyAction.Refused) { picked.Add((type, name)); return; }
                log.Add($"{type}:{name} cannot be copied: {one.Entries[0].Refusal}");
            }

            log.Add(any ? $"no {type} item could be used" : $"no {type} item fits (the source has none)");
        }

        static int Instances(GameMakerRoom r) => RoomInstanceCommand.Lists(r).SelectMany(l => l).Distinct().Count();

        Pick("sprites", d.Sprites.Where(s => s?.Name?.Content is not null && s.Textures?.Count >= 3 && !s.IsSpineSprite && !s.IsYYSWFSprite && s.Textures.All(x => x?.Texture?.TexturePage?.TextureData?.Image is not null))
            .OrderBy(s => Math.Abs(s.Textures.Count - 6)).ThenBy(s => (long)s.Width * s.Height).Select(s => s.Name.Content));
        Pick("objects", d.GameObjects.Where(o => o?.Name?.Content is not null && o.Events?.Sum(e => e.Count) > 0)
            .OrderBy(o => o.ParentId is null).ThenBy(o => Math.Abs(o.Events.Sum(e => e.Count) - 4)).Select(o => o.Name.Content));
        Pick("scripts", d.Scripts.Where(s => s?.Name?.Content is not null && s.Code is not null).OrderBy(s => Math.Abs(s.Code.Instructions.Count - 60)).Select(s => s.Name.Content));
        Pick("sounds", d.Sounds.Where(s => s?.Name?.Content is not null && s.AudioFile?.Data?.Length >= 2000).OrderBy(s => Math.Abs(s.AudioFile.Data.Length - 100_000)).Select(s => s.Name.Content));
        Pick("rooms", d.Rooms.Where(r => r?.Name?.Content is not null && Instances(r) >= 5).OrderBy(r => Math.Abs(Instances(r) - 20)).Select(r => r.Name.Content), smallest: true);
        Pick("fonts", d.Fonts.Where(f => f?.Name?.Content is not null && f.Glyphs?.Count > 0).OrderBy(f => f.Glyphs.Count).Select(f => f.Name.Content));
        Pick("paths", d.Paths.Where(p => p?.Name?.Content is not null && p.Points?.Count >= 2).Select(p => p.Name.Content));
        Pick("timelines", d.Timelines.Where(x => x?.Name?.Content is not null && x.Moments?.Count >= 1).Select(x => x.Name.Content));
        return picked;
    }

    // ---- what an item holds, as text ----

    private static string Hash(IEnumerable<string> parts)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var p in parts) h.AppendData(Encoding.UTF8.GetBytes(p + "\n"));
        return Convert.ToHexString(h.GetHashAndReset())[..16];
    }

    // The item's content with the names it refers to passed through map (the names the copy changed).
    private static string Fp(GameMakerData d, string type, string name, Dictionary<string, string> renames)
    {
        string M(string n) => n is not null && renames is not null && renames.TryGetValue(n, out var m) ? m : n;
        if (!CopyPlanner.Exists(d, type, name)) return "missing";
        switch (type)
        {
            case "sprites":
                var s = d.Sprites.First(x => x.Name.Content == name);
                return $"{s.Width}x{s.Height} frames {s.Textures.Count} origin {s.OriginX},{s.OriginY} masks {s.CollisionMasks.Count} margins {s.MarginLeft},{s.MarginRight},{s.MarginTop},{s.MarginBottom} pixels {FrameHashes(s)}";
            case "objects":
                var o = d.GameObjects.First(x => x.Name.Content == name);
                return $"sprite {M(o.Sprite?.Name.Content)} parent {M(o.ParentId?.Name.Content)} mask {M(o.TextureMaskId?.Name.Content)} depth {o.Depth} solid {o.Solid} visible {o.Visible} persistent {o.Persistent} events {string.Join(".", o.Events.Select((l, kind) => string.Join("-", l.Select(e => kind == (int)EventType.Collision && e.EventSubtype < d.GameObjects.Count ? "->" + M(d.GameObjects[(int)e.EventSubtype].Name.Content) : e.EventSubtype.ToString(System.Globalization.CultureInfo.InvariantCulture)))))}";
            case "scripts":
                var sc = d.Scripts.First(x => x.Name.Content == name);
                return $"script constructor {sc.IsConstructor} code {(sc.Code is null ? "none" : "some")}";
            case "sounds":
                var so = d.Sounds.First(x => x.Name.Content == name);
                return $"type {so.Type?.Content} volume {so.Volume} pitch {so.Pitch} bytes {so.AudioFile?.Data.Length} {(so.AudioFile is null ? "" : Convert.ToHexString(SHA256.HashData(so.AudioFile.Data))[..16])}";
            case "rooms":
                var r = d.Rooms.First(x => x.Name.Content == name);
                var inst = RoomInstanceCommand.Lists(r).SelectMany(l => l).Distinct().ToList();
                return $"{r.Width}x{r.Height} instances {inst.Count} layers {r.Layers?.Count} tiles {r.Tiles?.Count} views {r.Views?.Count} backgrounds {r.Backgrounds?.Count} objects {Hash(inst.Select(i => $"{M(i.ObjectDefinition?.Name.Content)} {i.X} {i.Y} {i.ScaleX} {i.ScaleY}").Order(StringComparer.Ordinal))}";
            case "fonts":
                var f = d.Fonts.First(x => x.Name.Content == name);
                return $"glyphs {f.Glyphs.Count} {Hash(f.Glyphs.Select(g => $"{g.Character} {g.SourceWidth} {g.SourceHeight}"))}";
            case "paths":
                var p = d.Paths.First(x => x.Name.Content == name);
                return $"points {p.Points.Count} {Hash(p.Points.Select(x => $"{x.X} {x.Y} {x.Speed}"))}";
            case "timelines":
                return $"moments {d.Timelines.First(x => x.Name.Content == name).Moments.Count}";
            default:
                return type;
        }
    }

    // Hash of the pixels of every frame, cut out of its texture page.
    private static string FrameHashes(GameMakerSprite s)
    {
        var pages = new Dictionary<GMImage, GMImage>();
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var frame in s.Textures)
        {
            var tpe = frame.Texture;
            var image = tpe?.TexturePage?.TextureData?.Image;
            if (image is null) { h.AppendData("none"u8); continue; }
            if (!pages.TryGetValue(image, out var raw)) pages[image] = raw = image.ConvertToRawBgra();
            var data = raw.GetRawImageData();
            for (var y = 0; y < tpe.SourceHeight; y++)
            {
                var start = (((tpe.SourceY + y) * raw.Width) + tpe.SourceX) * 4;
                var length = tpe.SourceWidth * 4;
                if (tpe.SourceY + y >= raw.Height || tpe.SourceX + tpe.SourceWidth > raw.Width) { h.AppendData("oob"u8); break; }
                h.AppendData(data.Slice(start, length));
            }
        }

        return Convert.ToHexString(h.GetHashAndReset())[..16];
    }

    // Code the copy compiled again in the target reads as the source's code (names equal; renamed items are skipped, their text names the old item).
    // A line may differ only where the decompiler shows a name in the source and a number in the target: an asset of the target (its own index, so the
    // compile resolved the name) or a constant of the source's GameMaker version.
    private void CompareCode(string step, Workspace source, Workspace target, CopyGraph graph, CopyPlan plan, Action<string> fail)
    {
        var (same, byIndex, byConstant, unexplained) = (0, 0, 0, new List<string>());
        foreach (var name in plan.Applied.Where(e => e.TargetName == e.Name).SelectMany(e => graph.OwnedCode(e.Type, e.Name)).Distinct().Take(60))
        {
            try
            {
                var left = CodeText.Decompile(source.Data, name).Trim().Split('\n');
                var right = CodeText.Decompile(target.Data, name).Trim().Split('\n');
                if (left.SequenceEqual(right)) { same++; continue; }
                if (left.Length != right.Length) { unexplained.Add($"{name}: {left.Length} lines in the source, {right.Length} in the target"); continue; }
                for (var i = 0; i < left.Length; i++)
                {
                    if (left[i] == right[i]) continue;
                    var explained = false;
                    foreach (Match id in Identifier().Matches(left[i]))
                    {
                        if (right[i].Contains(id.Value, StringComparison.Ordinal)) continue;
                        var index = target.Data.IndexOfByName(id.Value);
                        if (index >= 0 && Regex.IsMatch(right[i], @"\b" + index + @"\b")) { byIndex++; explained = true; break; }
                        if (index < 0) { byConstant++; explained = true; break; }
                    }

                    // The other way round: the source shows a number where the target knows the name.
                    if (!explained)
                    {
                        foreach (Match id in Identifier().Matches(right[i]))
                        {
                            var index = source.Data.IndexOfByName(id.Value);
                            if (!left[i].Contains(id.Value, StringComparison.Ordinal) && index >= 0 && Regex.IsMatch(left[i], @"\b" + index + @"\b")) { byIndex++; explained = true; break; }
                        }
                    }

                    if (!explained) unexplained.Add($"{name} line {i + 1}: '{left[i].Trim()}' / '{right[i].Trim()}'");
                }
            }
            catch (Exception e) { fail($"[{step}] code {name} could not be read back: {e.GetType().Name}: {e.Message}"); }
        }

        Say($"  [{step}] code entries read back: {same} equal; differing lines: {byIndex} asset names shown as the target's index, {byConstant} names of constants, {unexplained.Count} unexplained");
        foreach (var line in unexplained.Take(3)) Say("    unexplained: " + line);
        if (unexplained.Count > 0) fail($"[{step}] {unexplained.Count} code differences are not explained, for example {unexplained[0]}");
    }

    [GeneratedRegex("[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex Identifier();

    // ---- save and open again ----

    private async Task<bool> SaveAndCheck(string label, Workspace target, GameMakerData source, SavePipeline pipeline, string path, List<Expect> expects, HashSet<string> baseline,
        int loadWarnings, Action<string> fail, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var result = await target.SaveCopyAsync(pipeline, path, null, token);
        var saveMs = watch.Elapsed.TotalMilliseconds;
        if (!result.Saved) { fail($"[{label}] save failed: {result}"); return false; }
        watch.Restart();
        using var back = await GameMakerIO.ReadAsync(path, new Progress<GameMakerProgress>(), token);
        Say($"  [{label}] save {saveMs:F0} ms ({new FileInfo(path).Length / 1048576} MB), reopen with the library {watch.Elapsed.TotalMilliseconds:F0} ms, load warnings {back.LoadWarnings.Count} (target as opened: {loadWarnings})");
        if (back.LoadWarnings.Count > loadWarnings) fail($"[{label}] the saved file reads with {back.LoadWarnings.Count} warnings, the target had {loadWarnings}");
        var want = ResourceTypes.Counts(target.Data);
        var got = ResourceTypes.Counts(back);
        foreach (var (type, count) in want)
            if (got.GetValueOrDefault(type) != count) fail($"[{label}] {type}: {count} in the model, {got.GetValueOrDefault(type)} in the saved file");
        foreach (var added in Verify(back).Where(p => !baseline.Contains(p)).Take(10)) fail($"[{label}] consistency of the saved file: {added}");
        foreach (var e in expects.GroupBy(e => (e.Type, e.Target)).Select(g => g.Last()))
        {
            if (!CopyPlanner.Exists(back, e.Type, e.Target)) { fail($"[{label}] {e.Type}:{e.Target} is not in the saved file"); continue; }
            var a = Fp(source, e.Type, e.Source, e.Map);
            var b = Fp(back, e.Type, e.Target, null);
            if (a != b) fail($"[{label}] {e.Type}:{e.Target} differs from the source after save and open: source '{Short(a)}', file '{Short(b)}'");
        }

        File.Delete(path);
        return true;
    }

    // ---- consistency of a model ----

    private static List<string> Verify(GameMakerData d)
    {
        var found = new Dictionary<string, int>();
        void Add(string kind) => found[kind] = found.GetValueOrDefault(kind) + 1;
        var refs = ReferenceEqualityComparer.Instance;
        foreach (var type in ResourceTypes.All)
        {
            // Entries of the page, picture and audio lists are not named in the file.
            if (!CopyPlanner.Supported.Contains(type.Key) || type.Items(d) is not { } items) continue;
            var names = items.Select(ResourceTypes.NameOf).ToList();
            if (names.Any(string.IsNullOrEmpty)) Add($"{type.Key}: an item without a name");
            foreach (var twice in names.Where(n => !string.IsNullOrEmpty(n)).GroupBy(n => n).Where(g => g.Count() > 1).Take(4)) Add($"{type.Key}: duplicate name {twice.Key}");
        }

        var pages = new HashSet<object>(d.EmbeddedTextures, refs);
        var items2 = new HashSet<object>(d.TexturePageItems, refs);
        var audio = new HashSet<object>(d.EmbeddedAudio, refs);
        var code = new HashSet<object>(d.Code, refs);
        foreach (var c in d.Code)
        {
            if (c is null) { Add("code: a null entry in the list"); continue; }
            if (c.ParentEntry is { } parent && (!code.Contains(parent) || !parent.ChildEntries.Contains(c))) Add("code: a child entry whose parent does not list it or is not in the file");
            foreach (var child in c.ChildEntries) if (!ReferenceEquals(child.ParentEntry, c) || !code.Contains(child)) Add("code: a listed child that does not point back or is not in the file");
        }

        var objects = new HashSet<object>(d.GameObjects, refs);
        var sprites = new HashSet<object>(d.Sprites, refs);
        foreach (var tpe in d.TexturePageItems)
        {
            var page = tpe.TexturePage;
            if (page is null || !pages.Contains(page)) { Add("page item whose texture page is not in the file"); continue; }
            if (page.TextureData?.Image is { } image && (tpe.SourceX + tpe.SourceWidth > image.Width || tpe.SourceY + tpe.SourceHeight > image.Height)) Add("page item outside its texture page");
        }

        foreach (var s in d.Sprites)
            foreach (var frame in s.Textures ?? [])
                if (frame?.Texture is null || !items2.Contains(frame.Texture)) Add("sprite frame whose page item is not in the file");
        var used = new HashSet<object>(refs);
        foreach (var sound in d.Sounds)
        {
            if (sound.AudioFile is null) continue;
            if (!audio.Contains(sound.AudioFile)) Add("sound whose audio is not in the file");
            else if (!used.Add(sound.AudioFile)) Add("audio shared by two sounds");
            if (sound.AudioID >= d.EmbeddedAudio.Count) Add("sound audio id out of range");
        }

        foreach (var o in d.GameObjects)
        {
            if (o.Sprite is not null && !sprites.Contains(o.Sprite)) Add("object sprite is not in the file");
            if (o.ParentId is not null && !objects.Contains(o.ParentId)) Add("object parent is not in the file");
            if (o.TextureMaskId is not null && !sprites.Contains(o.TextureMaskId)) Add("object mask is not in the file");
            foreach (var list in o.Events ?? [])
                foreach (var ev in list)
                    foreach (var action in ev.Actions ?? [])
                        if (action.CodeId is not null && !code.Contains(action.CodeId)) Add("event code is not in the file");
        }

        foreach (var init in (d.GlobalInitScripts ?? []).Concat(d.GameEndScripts ?? []))
            if (init?.Code is null || !code.Contains(init.Code)) Add("start or end script whose code is not in the file");
        foreach (var script in d.Scripts)
            if (script.Code is not null && !code.Contains(script.Code)) Add("script code is not in the file");
        foreach (var timeline in d.Timelines)
            foreach (var moment in timeline.Moments ?? [])
                foreach (var action in moment.Event ?? [])
                    if (action.CodeId is not null && !code.Contains(action.CodeId)) Add("timeline code is not in the file");
        var ids = new HashSet<uint>();
        uint highest = 0;
        foreach (var room in d.Rooms)
        {
            foreach (var i in RoomInstanceCommand.Lists(room).SelectMany(l => l).Distinct(refs is IEqualityComparer<GameMakerRoom.GameObject> c ? c : null))
            {
                if (!ids.Add(i.InstanceID)) Add("instance id used twice");
                highest = Math.Max(highest, i.InstanceID);
                if (i.ObjectDefinition is null || !objects.Contains(i.ObjectDefinition)) Add("room instance whose object is not in the file");
                if ((i.CreationCode is not null && !code.Contains(i.CreationCode)) || (i.PreCreateCode is not null && !code.Contains(i.PreCreateCode))) Add("room instance code is not in the file");
            }

            if (room.CreationCodeId is not null && !code.Contains(room.CreationCodeId)) Add("room creation code is not in the file");
        }

        if (d.GeneralInfo is { } info && highest > info.LastObj) Add("instance id above LastObj");
        return [.. found.Select(p => $"{p.Key}: {p.Value}")];
    }

    // ---- state ----

    private static readonly string[] ListNames =
    [
        "Sprites", "Backgrounds", "Sounds", "Paths", "Scripts", "Shaders", "Fonts", "Timelines", "GameObjects", "Rooms", "Extensions", "Sequences",
        "AnimationCurves", "ParticleSystems", "Code", "CodeLocals", "Strings", "Variables", "Functions", "TexturePageItems", "EmbeddedTextures",
        "EmbeddedImages", "EmbeddedAudio", "AudioGroups", "TextureGroupInfo", "GlobalInitScripts", "GameEndScripts",
    ];

    // Counts of every list, the counters, and a hash of the names and strings: two states of one model are equal when this text is.
    private static int Wrappers(GameMakerData d) => d.Scripts.Count(s => s.Name.Content.StartsWith("gml_Script_", StringComparison.Ordinal));

    // A save recomputes the variable counters in the model, so a state compared with a reopened one leaves them out.
    private static string Signature(GameMakerData d, bool counters = true)
    {
        var text = new StringBuilder();
        foreach (var name in ListNames)
            if (typeof(GameMakerData).GetProperty(name)?.GetValue(d) is IList list) text.Append(name).Append('=').Append(list.Count).Append(' ');
        text.Append(System.Globalization.CultureInfo.InvariantCulture, $"counters {(counters ? d.VarCount1 : 0)} {(counters ? d.VarCount2 : 0)} {d.MaxLocalVarCount} {d.GeneralInfo?.LastObj} {d.GeneralInfo?.LastTile} ");
        var parts = new List<string>();
        foreach (var type in ResourceTypes.All)
            if (type.Items(d) is { } items) parts.Add(type.Key + ":" + string.Join(",", items.Select(ResourceTypes.NameOf)));
        parts.Add(string.Join(",", d.Strings.Select(s => s?.Content)));
        return text + Hash(parts);
    }

    // ---- peak memory ----

    private sealed class Peak : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;

        public double StartMb { get; } = Environment.WorkingSet / 1048576.0;

        public double StartManagedMb { get; } = GC.GetTotalMemory(false) / 1048576.0;

        public double PeakMb { get; private set; }

        public double PeakManagedMb { get; private set; }

        public Peak()
        {
            (PeakMb, PeakManagedMb) = (StartMb, StartManagedMb);
            _loop = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(15));
                try
                {
                    while (await timer.WaitForNextTickAsync(_stop.Token))
                    {
                        PeakMb = Math.Max(PeakMb, Environment.WorkingSet / 1048576.0);
                        PeakManagedMb = Math.Max(PeakManagedMb, GC.GetTotalMemory(false) / 1048576.0);
                    }
                }
                catch (OperationCanceledException) { }
            });
        }

        public void Stop()
        {
            if (_stop.IsCancellationRequested) return;
            _stop.Cancel();
            _loop.Wait();
            PeakMb = Math.Max(PeakMb, Environment.WorkingSet / 1048576.0);
        }

        public void Dispose()
        {
            Stop();
            _stop.Dispose();
        }
    }
}
