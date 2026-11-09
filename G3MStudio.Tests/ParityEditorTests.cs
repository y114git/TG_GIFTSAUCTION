using Avalonia.Controls;
using Avalonia.LogicalTree;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Particles;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data model owns what is added to it
public sealed class ParityEditorTests
{
    private static GameMakerData WithStepEvent()
    {
        var data = TestData.Create(3, withCode: true);
        new ObjectEventCommand("obj_0", ObjectEventCommand.Add, (uint)EventType.Step, 0).Apply(data);
        new EditCodeCommand(ObjectEventCommand.CodeName("obj_0", (uint)EventType.Step, 0), "x = 7;").Apply(data);
        return data;
    }

    [Fact]
    public void ChangingTheTypeMovesTheEventAndItsCodeAndUndoRestores()
    {
        using var data = WithStepEvent();
        var owner = data.GameObjects[0];
        var history = new EditHistory(data);
        history.Execute(EditCommand.Create(ObjectEventRetypeCommand.Name, new ObjectEventRetypeCommand("obj_0", (uint)EventType.Step, 0, (uint)EventType.Alarm, 3).Args));
        Assert.Empty(owner.Events[(int)EventType.Step]);
        var moved = Assert.Single(owner.Events[(int)EventType.Alarm]);
        Assert.Equal(3u, moved.EventSubtype);
        Assert.Contains("x = 7", CodeText.Decompile(data, ObjectEventCommand.CodeName("obj_0", (uint)EventType.Alarm, 3)), StringComparison.Ordinal);
        history.Undo();
        Assert.Empty(owner.Events[(int)EventType.Alarm]);
        Assert.Single(owner.Events[(int)EventType.Step]);
    }

    [Fact]
    public void InheritedEventsListTheAncestorsEventsNotOverriddenAndSurviveALoop()
    {
        using var data = WithStepEvent();
        new ObjectEventCommand("obj_1", ObjectEventCommand.Add, (uint)EventType.Create, 0).Apply(data);
        new ObjectEventCommand("obj_1", ObjectEventCommand.Add, (uint)EventType.Step, 0).Apply(data);
        new ObjectParentCommand("obj_2", "obj_1").Apply(data);
        new ObjectParentCommand("obj_1", "obj_0").Apply(data);
        var child = data.GameObjects[2];
        var inherited = InheritedEvents.Of(child);
        Assert.Equal([("obj_1", (uint)EventType.Create), ("obj_1", (uint)EventType.Step)], inherited.Select(i => (i.Owner, i.Type)));
        new ObjectEventCommand("obj_2", ObjectEventCommand.Add, (uint)EventType.Step, 0).Apply(data);
        Assert.Single(InheritedEvents.Of(child));
    }

    [Fact]
    public void TheAddEventMenuHasEveryTypeAndAlarmsCascadeToTwelve()
    {
        using var data = TestData.Create(1);
        var picked = new List<(uint, uint)>();
        var menu = ObjectEventsView.BuildEventMenu(data, (t, s) => picked.Add((t, s)));
        var items = menu.Items.OfType<MenuItem>().ToList();
        Assert.Equal(EventNames.Types.Length, items.Count);
        var alarm = items.Single(i => i.Name == "EventMenu_Alarm");
        Assert.Equal(12, alarm.Items.Count);
    }

    [Fact]
    public void ShiftingMovesTheMomentsFromAStepAndUndoRestores()
    {
        using var data = TestData.Create(0);
        var timeline = new GameMakerTimeline { Name = data.Strings.MakeString("tl_a") };
        foreach (var step in new uint[] { 1, 5, 9 }) timeline.Moments.Add(new GameMakerTimeline.GameMakerTimelineMoment(step, []));
        data.Timelines.Add(timeline);
        var history = new EditHistory(data);
        history.Execute(EditCommand.Create(TimelineShiftCommand.Name, new TimelineShiftCommand("tl_a", 5, 3).Args));
        Assert.Equal([1u, 8u, 12u], timeline.Moments.Select(m => m.Step));
        history.Execute(new TimelineShiftCommand("tl_a", 0, -20));
        Assert.Equal([0u, 7u, 11u], timeline.Moments.Select(m => m.Step));
        history.Undo();
        history.Undo();
        Assert.Equal([1u, 5u, 9u], timeline.Moments.Select(m => m.Step));
    }

    [Fact]
    public void AShiftToTheLeftStopsAtTheMomentInFront()
    {
        using var data = TestData.Create(0);
        var timeline = new GameMakerTimeline { Name = data.Strings.MakeString("tl_b") };
        foreach (var step in new uint[] { 2, 10, 14 }) timeline.Moments.Add(new GameMakerTimeline.GameMakerTimelineMoment(step, []));
        data.Timelines.Add(timeline);
        var history = new EditHistory(data);
        history.Execute(new TimelineShiftCommand("tl_b", 10, -50));
        Assert.Equal([2u, 3u, 7u], timeline.Moments.Select(m => m.Step));
        history.Undo();
        Assert.Equal([2u, 10u, 14u], timeline.Moments.Select(m => m.Step));
    }

    [Fact]
    public void ACopiedMomentKeepsTheTimelineSorted()
    {
        using var data = TestData.Create(0);
        var from = new GameMakerTimeline { Name = data.Strings.MakeString("tl_from") };
        from.Moments.Add(new GameMakerTimeline.GameMakerTimelineMoment(5, []));
        var to = new GameMakerTimeline { Name = data.Strings.MakeString("tl_to") };
        foreach (var step in new uint[] { 1, 9 }) to.Moments.Add(new GameMakerTimeline.GameMakerTimelineMoment(step, []));
        data.Timelines.Add(from);
        data.Timelines.Add(to);
        var history = new EditHistory(data);
        history.Execute(new TimelineMomentCopyCommand("tl_from", 0, "tl_to", false));
        Assert.Equal([1u, 5u, 9u], to.Moments.Select(m => m.Step));
        history.Undo();
        Assert.Equal([1u, 9u], to.Moments.Select(m => m.Step));
    }

    [Fact]
    public void ALineDiffIgnoresTheLineEndingStyle()
    {
        Assert.All(G3MStudio.Core.ModPack.LineDiff.Compute("a\r\nb\r\nc", "a\nb\nc"), l => Assert.Equal(G3MStudio.Core.ModPack.DiffKind.Same, l.Kind));
    }

    [Fact]
    public void ACurvePresetReplacesThePointsAndUndoRestoresThem()
    {
        using var data = TestData.Create(0);
        var channel = new GameMakerAnimationCurve.Channel { Name = data.Strings.MakeString("c"), Points = [] };
        channel.Points.Add(new GameMakerAnimationCurve.Channel.Point { X = 0, Value = 4 });
        data.AnimationCurves.Add(new GameMakerAnimationCurve { Name = data.Strings.MakeString("curve_p"), Channels = [channel] });
        var history = new EditHistory(data);
        history.Execute(new CurvePointCommand("curve_p", 0, CurvePointCommand.Preset, 0, new CurveValue(1)));
        Assert.Equal(11, channel.Points.Count);
        Assert.Equal(0.25f, channel.Points[5].Value * 1f, 0.2f);
        Assert.Equal(1f, channel.Points[^1].Value);
        history.Undo();
        var back = Assert.Single(channel.Points);
        Assert.Equal(4f, back.Value);
        Assert.Throws<InvalidOperationException>(() => CurvePresets.Points(99).ToList());
    }

    [Fact]
    public void DuplicatingAnEmitterCopiesItsSettingsUnderANewNameAndUndoRemovesIt()
    {
        using var data = TestData.Create(0);
        var system = new GameMakerParticleSystem { Name = data.Strings.MakeString("ps_a") };
        var emitter = new GameMakerParticleSystemEmitter { Name = data.Strings.MakeString("e0"), EmitCount = 9, SizeMinX = 2, SizeMinY = 5, SpeedMax = 7 };
        data.ParticleSystemEmitters.Add(emitter);
        system.Emitters.Add(new GameMakerResourceById<GameMakerParticleSystemEmitter, GameMakerChunkPSEM>(emitter));
        data.ParticleSystems.Add(system);
        var history = new EditHistory(data);
        history.Execute(new ParticleEmitterCommand("ps_a", ParticleEmitterCommand.Duplicate, 0));
        var copy = system.Emitters[1].Resource;
        Assert.NotSame(emitter, copy);
        Assert.NotEqual(emitter.Name.Content, copy.Name.Content);
        Assert.Equal((9, 2f, 5f, 7f), (copy.EmitCount, copy.SizeMinX, copy.SizeMinY, copy.SpeedMax));
        history.Undo();
        Assert.Single(system.Emitters);
    }

    [Fact]
    public void CopyGmlNamesTheSystemTypesAndEmitters()
    {
        var emitter = new GameMakerParticleSystemEmitter { Name = new GameMakerString("fire"), Mode = GameMakerParticleSystemEmitter.EmitMode.Burst, EmitCount = 4, LifetimeMin = 30, LifetimeMax = 60 };
        using var system = new GameMakerParticleSystem { Name = new GameMakerString("ps_a") };
        system.Emitters.Add(new GameMakerResourceById<GameMakerParticleSystemEmitter, GameMakerChunkPSEM> { Resource = emitter });
        var gml = ParticleGml.Generate(system);
        Assert.Contains("part_system_create()", gml, StringComparison.Ordinal);
        Assert.Contains("part_type_life(_type_fire, 30, 60);", gml, StringComparison.Ordinal);
        Assert.Contains("part_emitter_burst(_system, _emitter_fire, _type_fire, 4);", gml, StringComparison.Ordinal);
    }

    [Fact]
    public void ADoubleClickShowsTheEventCodeInThePaneBesideTheList()
    {
        using var data = WithStepEvent();
        var opened = new List<string>();
        var view = new ObjectEventsView(data.GameObjects[0], opened.Add, null, code => new TextBlock { Text = code });
        view.ShowCode("gml_Object_obj_0_Step_0");
        var pane = view.GetLogicalDescendants().OfType<ContentControl>().Single(c => c.Name == "EventCodePane");
        Assert.Equal("gml_Object_obj_0_Step_0", Assert.IsType<TextBlock>(pane.Content).Text);
        Assert.Empty(opened);
    }

    [Fact]
    public void ACopiedMomentKeepsItsStepAndCodeAndACutRemovesTheOriginal()
    {
        using var data = TestData.Create(0, withCode: true);
        GameMakerTimeline Make(string name, params uint[] steps)
        {
            var t = new GameMakerTimeline { Name = data.Strings.MakeString(name) };
            foreach (var step in steps) t.Moments.Add(new GameMakerTimeline.GameMakerTimelineMoment(step, []));
            data.Timelines.Add(t);
            return t;
        }

        var (a, b) = (Make("tl_a", 3, 8), Make("tl_b"));
        new TimelineActionCommand("tl_a", TimelineActionCommand.Add, 0, -1, "gml_Script_probe").Apply(data);
        var history = new EditHistory(data);
        history.Execute(EditCommand.Create(TimelineMomentCopyCommand.Name, new TimelineMomentCopyCommand("tl_a", 0, "tl_b", false).Args));
        Assert.Equal(2, a.Moments.Count);
        var copy = Assert.Single(b.Moments);
        Assert.Equal(3u, copy.Step);
        Assert.Equal("gml_Script_probe", Assert.Single(copy.Event).CodeId?.Name.Content);
        history.Execute(new TimelineMomentCopyCommand("tl_a", 0, "tl_b", true));
        Assert.Equal([8u], a.Moments.Select(m => m.Step));
        Assert.Equal(2, b.Moments.Count);
        history.Undo();
        Assert.Equal([3u, 8u], a.Moments.Select(m => m.Step));
        history.Undo();
        Assert.Empty(b.Moments);
    }

    [Fact]
    public void MomentsAndMessagesAreAddedEditedAndDeletedAndUndoRestores()
    {
        using var data = TestData.Create(0);
        var sequence = new GameMakerSequence { Name = data.Strings.MakeString("seq_a"), Moments = [], BroadcastMessages = [], Tracks = [] };
        data.Sequences.Add(sequence);
        var history = new EditHistory(data);
        history.Execute(EditCommand.Create(SequenceEventCommand.Name, new SequenceEventCommand("seq_a", SequenceEventCommand.Moments, SequenceEventCommand.Add, -1, 10, "fn_a\nfn_b").Args));
        history.Execute(new SequenceEventCommand("seq_a", SequenceEventCommand.Moments, SequenceEventCommand.Add, -1, 4, "fn_c"));
        Assert.Equal([4f, 10f], sequence.Moments.Select(m => m.Key));
        Assert.Throws<InvalidOperationException>(() => new SequenceEventCommand("seq_a", SequenceEventCommand.Moments, SequenceEventCommand.Add, -1, 4).Apply(data));
        history.Execute(new SequenceEventCommand("seq_a", SequenceEventCommand.Moments, SequenceEventCommand.SetText, 1, 0, "fn_z"));
        Assert.Equal(["fn_z"], sequence.Moments[1].Channels[0].Value.Events.Select(e => e.Content));
        history.Undo();
        Assert.Equal(["fn_a", "fn_b"], sequence.Moments[1].Channels[0].Value.Events.Select(e => e.Content));
        history.Execute(new SequenceEventCommand("seq_a", SequenceEventCommand.Messages, SequenceEventCommand.Add, -1, 2, "hello"));
        history.Execute(new SequenceEventCommand("seq_a", SequenceEventCommand.Messages, SequenceEventCommand.Delete, 0));
        Assert.Empty(sequence.BroadcastMessages);
        history.Undo();
        Assert.Equal("hello", sequence.BroadcastMessages[0].Channels[0].Value.Messages[0].Content);
    }

    [Fact]
    public void TheGraphFindsNumberTracksAndKeepsAKeyBetweenItsNeighbours()
    {
        using var data = TestData.Create(0);
        var keys = new GameMakerSimpleList<GameMakerSequence.Keyframe<GameMakerSequence.RealData>>();
        foreach (var (key, value) in new[] { (0f, 1f), (10f, 5f), (20f, 2f) })
        {
            var frame = new GameMakerSequence.Keyframe<GameMakerSequence.RealData> { Key = key, Length = 1 };
            frame.Channels.Add(new GameMakerSequence.Keyframe<GameMakerSequence.RealData>.KeyframeChannel { Channel = 0, Value = new GameMakerSequence.RealData { Value = value } });
            keys.Add(frame);
        }

        var track = new GameMakerSequence.Track { Name = data.Strings.MakeString("position"), ModelName = data.Strings.MakeString("GMRealTrack"), Tracks = [], Keyframes = new GameMakerSequence.RealKeyframes { List = keys } };
        var sequence = new GameMakerSequence { Name = data.Strings.MakeString("seq_g"), Moments = [], BroadcastMessages = [], Tracks = [track] };
        data.Sequences.Add(sequence);
        var found = Assert.Single(SequenceGraphView.Collect(sequence));
        Assert.Equal("Tracks[0].Keyframes.List", found.Path);
        var points = SequenceGraphView.Points(found, 0);
        Assert.Equal([1f, 5f, 2f], points.Select(p => p.Value));
        Assert.Equal(20f, SequenceGraphView.ClampKey(points, 1, 99f));
        Assert.Equal(0f, SequenceGraphView.ClampKey(points, 1, -5f));
        var history = new EditHistory(data);
        history.Execute(new SetPropertyCommand("sequences", "seq_g", "Tracks[0].Keyframes.List[1].Channels[0].Value.Value", "7.5"));
        Assert.Equal(7.5f, keys[1].Channels[0].Value.Value);
        history.Undo();
        Assert.Equal(5f, keys[1].Channels[0].Value.Value);
    }

    [Fact]
    public void ABezierPresetSetsTheTypeAndTheHandlesAndUndoRestores()
    {
        using var data = TestData.Create(0);
        var channel = new GameMakerAnimationCurve.Channel { Name = data.Strings.MakeString("c"), Points = [] };
        data.AnimationCurves.Add(new GameMakerAnimationCurve { Name = data.Strings.MakeString("curve_b"), Channels = [channel] });
        var history = new EditHistory(data);
        history.Execute(new CurvePointCommand("curve_b", 0, CurvePointCommand.Preset, 0, new CurveValue(8)));
        Assert.Equal(GameMakerAnimationCurve.Channel.CurveType.Bezier, channel.Curve);
        Assert.Equal(2, channel.Points.Count);
        Assert.Equal((0.42f, -0.42f), (channel.Points[0].BezierX1, channel.Points[1].BezierX0));
        history.Undo();
        Assert.Empty(channel.Points);
    }

    [Fact]
    public async Task SoloRunsOnlyOneEmitterAndARegionCanBeFoundUnderAPoint()
    {
        static GameMakerParticleSystemEmitter Make(string name, float x) => new() { Name = new GameMakerString(name), EmitCount = 2, LifetimeMin = 50, LifetimeMax = 50, RegionX = x, RegionWidth = 20, RegionHeight = 20 };
        var (a, b) = (Make("a", -100), Make("b", 100));
        using var system = new GameMakerParticleSystem { Name = new GameMakerString("ps_s") };
        foreach (var e in new[] { a, b }) system.Emitters.Add(new GameMakerResourceById<GameMakerParticleSystemEmitter, GameMakerChunkPSEM> { Resource = e });
        var simulation = new ParticleSimulation(system) { Solo = b };
        simulation.Step();
        Assert.Equal(2, simulation.Count);
        Assert.All(simulation.Dots(), dot => Assert.True(dot.X > 0));
        await HeadlessApp.Run(() =>
        {
            var view = new ParticlePreviewView(system);
            Assert.Equal(1, view.RegionAt(100, 0));
            Assert.Equal(0, view.RegionAt(-95, 5));
            Assert.Equal(-1, view.RegionAt(0, 0));
            a.Enabled = false;
            Assert.Equal(-1, view.RegionAt(-100, 0));
            Assert.False(view.MoveRegion(0, 1, 1)); // no workspace, nothing to edit
        });
    }
}
