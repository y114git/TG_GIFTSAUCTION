using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>
/// Every command must survive the journal: rebuilt from its Args it does the same to a fresh copy of the data, and doing it again after an
/// undo gives the same result as the first time (redo).
/// </summary>
public sealed class CommandReplayTests
{

    private static byte[] Written(GameMakerData data) => TestData.Bytes(data);

    private static readonly (string Name, Func<GameMakerData, EditCommand?> Build)[] Cases =
    [
        ("rename object", d => d.GameObjects.Count > 0 ? new RenameResourceCommand("objects", d.GameObjects[0].Name.Content!, "obj_renamed") : null),
        ("new object", d => new ResourceListCommand("objects", ResourceListCommand.New, "obj_replay_new")),
        ("new sprite", d => new ResourceListCommand("sprites", ResourceListCommand.New, "spr_replay_new")),
        ("new sound", d => new ResourceListCommand("sounds", ResourceListCommand.New, "snd_replay_new")),
        ("new room", d => new ResourceListCommand("rooms", ResourceListCommand.New, "room_replay_new")),
        ("delete script", d => d.Scripts.Count > 0 ? new ResourceListCommand("scripts", ResourceListCommand.Delete, d.Scripts[^1].Name.Content!) : null),
        ("room layer visible", d => FirstLayer(d) is var (room, layer) ? new RoomLayerCommand(room, RoomLayerCommand.Visible, layer, "0") : null),
        ("room layer rename", d => FirstLayer(d) is var (room, layer) ? new RoomLayerCommand(room, RoomLayerCommand.Rename, layer, "renamed") : null),
        ("room layer depth", d => FirstLayer(d) is var (room, layer) ? new RoomLayerCommand(room, RoomLayerCommand.Depth, layer, "42", 42) : null),
        ("room layer delete", d => FirstLayer(d) is var (room, layer) ? new RoomLayerCommand(room, RoomLayerCommand.Delete, layer) : null),
        ("room layer add instances", d => Pick(d.Rooms, r => r.Layers?.Count > 0) is { } r ? new RoomLayerCommand(r.Name.Content!, RoomLayerCommand.AddInstances, 0, "Replay") : null),
        ("room instance add", d => Pick(d.Rooms, r => r.Layers?.Count > 0) is { } r && d.GameObjects.Count > 0 ? new RoomInstanceCommand(r.Name.Content!, RoomInstanceCommand.Add, 0, 5, 6, d.GameObjects[0].Name.Content!) : null),
        ("room instance move", d => FirstInstance(d) is var (room, id) ? new RoomInstanceCommand(room, RoomInstanceCommand.Move, id, 7, 9) : null),
        ("room instance delete", d => FirstInstance(d) is var (room, id) ? new RoomInstanceCommand(room, RoomInstanceCommand.Delete, id) : null),
        ("room instance flip", d => FirstInstance(d) is var (room, id) ? new RoomInstanceCommand(room, RoomInstanceCommand.FlipX, id) : null),
        ("room layer duplicate", d => FirstLayer(d) is var (room, layer) ? RoomLayerCommand.DuplicateOf(room, layer, "Copy_replay", 5) : null),
        ("room layer add path", d => Pick(d.Rooms, r => r.Layers?.Count > 0) is { } r ? new RoomLayerCommand(r.Name.Content!, RoomLayerCommand.AddPath, 0, "PathReplay") : null),
        ("room layer add effect", d => Pick(d.Rooms, r => r.Layers?.Count > 0) is { } r ? new RoomLayerCommand(r.Name.Content!, RoomLayerCommand.AddEffect, 0, "FxReplay", 0, "_filter_blur") : null),
        ("new texture group", d => d.TextureGroupInfo is not null ? new GroupListCommand(false, GroupListCommand.New, "zz_replay_group") : null),
        ("add string", d => new StringAddCommand("zz_replay_string")),
        ("code entry", d => new CodeEntryCommand("zz_replay_entry")),
        ("room creation code", d => Pick(d.Rooms, r => r.CreationCodeId is null) is { } r ? new RoomCreationCodeCommand(r.Name.Content!, 0) : null),
        ("instance creation code", d => d.Rooms.SelectMany(r => RoomInstanceCommand.Lists(r).SelectMany(l => l).Where(i => i.CreationCode is null).Take(1).Select(i => (Room: r.Name.Content!, i.InstanceID))).FirstOrDefault() is { Room: not null } hit ? new RoomCreationCodeCommand(hit.Room, hit.InstanceID) : null),
        ("room order remove", d => d.Rooms.Count > 0 ? new RoomOrderCommand(RoomOrderCommand.Remove, d.Rooms[0].Name.Content!, 0) : null),
        ("sprite frame duplicate", d => Pick(d.Sprites, s => s.Textures?.Count > 0) is { } s ? new FrameListCommand(s.Name.Content!, FrameListCommand.Duplicate, 0) : null),
        ("sprite frame remove", d => Pick(d.Sprites, s => s.Textures?.Count > 1) is { } s ? new FrameListCommand(s.Name.Content!, FrameListCommand.Remove, 0) : null),
        ("font glyph add", d => Pick(d.Fonts, _ => true) is { } f ? new FontGlyphCommand(f.Name.Content!, FontGlyphCommand.Add, 0, 0x2603) : null),
        ("font glyph remove", d => Pick(d.Fonts, f => f.Glyphs?.Count > 0) is { } f ? new FontGlyphCommand(f.Name.Content!, FontGlyphCommand.Remove, 0) : null),
        ("font kerning set", d => Pick(d.Fonts, f => f.Glyphs?.Count > 0) is { } f ? new FontKerningCommand(f.Name.Content!, 0, FontKerningCommand.Set, 65, 2) : null),
        ("particle emitter add", d => Pick(d.ParticleSystems, _ => true) is { } p ? new ParticleEmitterCommand(p.Name.Content!, ParticleEmitterCommand.Add) : null),
        ("particle emitter remove", d => Pick(d.ParticleSystems, p => p.Emitters?.Count > 0) is { } p ? new ParticleEmitterCommand(p.Name.Content!, ParticleEmitterCommand.Remove, 0) : null),
        ("sequence track remove", d => Pick(d.Sequences, s => s.Tracks?.Count > 0) is { } s ? new SequenceTrackCommand(s.Name.Content!, SequenceTrackCommand.Remove, 0) : null),
        ("sequence track group", d => Pick(d.Sequences, _ => true) is { } q ? new SequenceTrackCommand(q.Name.Content!, SequenceTrackCommand.AddGroup, -1, "Group") : null),
        ("timeline moment add", d => Pick(d.Timelines, _ => true) is { } t ? new TimelineMomentCommand(t.Name.Content!, TimelineMomentCommand.Add, 0, 12) : null),
        ("timeline moment remove", d => Pick(d.Timelines, t => t.Moments?.Count > 0) is { } t ? new TimelineMomentCommand(t.Name.Content!, TimelineMomentCommand.Remove, 0) : null),
        ("object event add", d => d.GameObjects.Count > 0 ? new ObjectEventCommand(d.GameObjects[0].Name.Content!, ObjectEventCommand.Add, 3, 5) : null),
        ("path point add", d => Pick(d.Paths, _ => true) is { } pa ? new PathPointsCommand(pa.Name.Content!, PathPointsCommand.Add, 0, 3, 4) : null),
        ("path point delete", d => Pick(d.Paths, p => p.Points?.Count > 1) is { } p ? new PathPointsCommand(p.Name.Content!, PathPointsCommand.Delete, 0) : null),
        ("path point move", d => Pick(d.Paths, p => p.Points?.Count > 0) is { } p ? new PathPointsCommand(p.Name.Content!, PathPointsCommand.Move, 0, 8, 9) : null),
    ];

    // Entries and lists can be null in files of some versions; a case without a target is skipped.
    private static T? Pick<T>(IEnumerable<T>? list, Func<T, bool> where) where T : class => list?.FirstOrDefault(x => x is not null && where(x));

    private static (string Room, uint Layer)? FirstLayer(GameMakerData d) =>
        d.Rooms.Select(r => (Room: r, Layer: r.Layers?.FirstOrDefault())).FirstOrDefault(x => x.Layer is not null) is { Layer: { } l } x ? (x.Room.Name.Content!, l.LayerId) : null;

    private static (string Room, uint Id)? FirstInstance(GameMakerData d) =>
        d.Rooms.SelectMany(r => RoomInstanceCommand.Lists(r).SelectMany(l => l).Take(1).Select(i => (Room: r.Name.Content!, Id: i.InstanceID))).FirstOrDefault() is { Room: not null } hit ? hit : null;

    private static readonly string[] FixtureFiles = ["pizza/data.win", "uty/data.win", "deltarune/data.win", "undertale/data.win"];

    public static TheoryData<string> Names => [.. Cases.Select(c => c.Name)];

    // A case runs on every fixture that has a target for it; at least one must.
    [Theory]
    [MemberData(nameof(Names))]
    public async Task ACommandRebuiltFromItsArgsDoesTheSameAndRedoRepeatsIt(string name)
    {
        var build = Cases.First(c => c.Name == name).Build;
        var ran = 0;
        foreach (var file in FixtureFiles)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
            using var first = await GameMakerIO.ReadAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            using var second = await GameMakerIO.ReadAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            if (build(first) is not { } command) continue;

            var history = new EditHistory(first);
            history.Execute(command);
            var applied = Written(first);

            var replayed = EditCommand.Create(command.Kind, command.Args);
            new EditHistory(second).Execute(replayed);
            Assert.Equal(applied, Written(second));

            history.Undo();
            history.Redo();
            Assert.Equal(applied, Written(first));
            ran++;
        }

        Assert.True(ran > 0, "no fixture has a target for this command");
    }
}
