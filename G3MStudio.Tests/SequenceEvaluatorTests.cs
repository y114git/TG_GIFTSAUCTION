using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Sequences;

namespace G3MStudio.Tests;

public sealed class SequenceEvaluatorTests
{
    private static GameMakerSequence.Track FloatTrack(int interpolation, params (float Key, float Value)[] keys)
    {
        var real = new GameMakerSequence.RealKeyframes { Interpolation = interpolation, List = [] };
        foreach (var (key, value) in keys)
        {
            var keyframe = new GameMakerSequence.Keyframe<GameMakerSequence.RealData> { Key = key, Length = 1 };
            keyframe.Channels.Add(new GameMakerSequence.Keyframe<GameMakerSequence.RealData>.KeyframeChannel { Channel = 0, Value = new GameMakerSequence.RealData { Value = value } });
            real.List.Add(keyframe);
        }

        return new GameMakerSequence.Track { Keyframes = real };
    }

    [Fact]
    public void ValuesAreLinearWhenTheTrackSaysSoSteppedOtherwiseAndHeldOutsideTheKeys()
    {
        var linear = FloatTrack(1, (0, 10), (10, 20));
        Assert.Equal(10f, SequenceEvaluator.Value(linear, 0, -5, 0));
        Assert.Equal(15f, SequenceEvaluator.Value(linear, 0, 5, 0));
        Assert.Equal(20f, SequenceEvaluator.Value(linear, 0, 99, 0));
        var stepped = FloatTrack(0, (0, 10), (10, 20));
        Assert.Equal(10f, SequenceEvaluator.Value(stepped, 0, 5, 0));
        Assert.Equal(7f, SequenceEvaluator.Value(null, 0, 5, 7));
        Assert.Equal(7f, SequenceEvaluator.Value(FloatTrack(1, (0, float.NaN)), 0, 0, 7)); // a value that is not a number falls back
    }

    private static GameMakerSequence.Track GraphicTrack(GameMakerSprite sprite, float key, float length, params GameMakerSequence.Track[] children)
    {
        var graphic = new GameMakerSequence.GraphicKeyframes { List = [] };
        var keyframe = new GameMakerSequence.Keyframe<GameMakerSequence.GraphicKeyframes.Data> { Key = key, Length = length };
        keyframe.Channels.Add(new GameMakerSequence.Keyframe<GameMakerSequence.GraphicKeyframes.Data>.KeyframeChannel
        {
            Value = new GameMakerSequence.GraphicKeyframes.Data { Resource = new GameMakerResourceById<GameMakerSprite, GameMakerChunkSPRT> { Resource = sprite } },
        });
        graphic.List.Add(keyframe);
        var track = new GameMakerSequence.Track { Keyframes = graphic, Tracks = [] };
        foreach (var child in children) track.Tracks.Add(child);
        return track;
    }

    [Fact]
    public void AGraphicShowsOnlyInsideItsKeyframeAndFollowsItsPositionAndImageIndexTracks()
    {
        using var sprite = new GameMakerSprite { Name = new GameMakerString("spr") };
        var position = FloatTrack(0, (0, 4));
        position.BuiltinName = GameMakerSequence.Track.TrackBuiltinName.Position;
        var image = FloatTrack(0, (0, 0), (5, 2));
        image.BuiltinName = GameMakerSequence.Track.TrackBuiltinName.ImageIndex;
        using var sequence = new GameMakerSequence { Name = new GameMakerString("seq"), Tracks = [GraphicTrack(sprite, 2, 10, position, image)] };

        Assert.Empty(SequenceEvaluator.At(sequence, 1));
        var early = Assert.Single(SequenceEvaluator.At(sequence, 3));
        Assert.Equal((4f, 0), (early.X, early.Image));
        Assert.Equal(2, Assert.Single(SequenceEvaluator.At(sequence, 7)).Image);
        Assert.Empty(SequenceEvaluator.At(sequence, 12)); // the keyframe covers frames 2 up to 12
        Assert.Empty(SequenceEvaluator.At(sequence, float.NaN));
    }

    [Fact]
    public void ANestedSequencePlaysInsideItsTrackFromTheFrameOfItsKeyframe()
    {
        using var sprite = new GameMakerSprite { Name = new GameMakerString("spr") };
        using var inner = new GameMakerSequence { Name = new GameMakerString("inner"), Tracks = [GraphicTrack(sprite, 2, 5)] };
        var nested = new GameMakerSequence.SequenceKeyframes { List = [] };
        var keyframe = new GameMakerSequence.Keyframe<GameMakerSequence.SequenceKeyframes.Data> { Key = 10, Length = 20 };
        keyframe.Channels.Add(new GameMakerSequence.Keyframe<GameMakerSequence.SequenceKeyframes.Data>.KeyframeChannel
        {
            Value = new GameMakerSequence.SequenceKeyframes.Data { Resource = new GameMakerResourceById<GameMakerSequence, GameMakerChunkSEQN> { Resource = inner } },
        });
        nested.List.Add(keyframe);
        using var outer = new GameMakerSequence { Name = new GameMakerString("outer"), Tracks = [new GameMakerSequence.Track { Keyframes = nested, Tracks = [] }] };

        Assert.Empty(SequenceEvaluator.At(outer, 11)); // inner frame 1: the sprite starts at 2
        Assert.Single(SequenceEvaluator.At(outer, 13)); // inner frame 3
        Assert.Empty(SequenceEvaluator.At(outer, 19)); // inner frame 9: past the sprite

        // A sequence that contains itself ends at the depth limit instead of looping forever.
        var loop = new GameMakerSequence.SequenceKeyframes { List = [] };
        var again = new GameMakerSequence.Keyframe<GameMakerSequence.SequenceKeyframes.Data> { Key = 0, Length = 100 };
        using var selfish = new GameMakerSequence { Name = new GameMakerString("selfish"), Tracks = [] };
        again.Channels.Add(new GameMakerSequence.Keyframe<GameMakerSequence.SequenceKeyframes.Data>.KeyframeChannel
        {
            Value = new GameMakerSequence.SequenceKeyframes.Data { Resource = new GameMakerResourceById<GameMakerSequence, GameMakerChunkSEQN> { Resource = selfish } },
        });
        loop.List.Add(again);
        selfish.Tracks.Add(new GameMakerSequence.Track { Keyframes = loop, Tracks = [] });
        Assert.Empty(SequenceEvaluator.At(selfish, 5));
    }

    [Fact]
    public async Task ASequenceOfARealGameShowsSpritesAtSomeFrame()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var found = 0;
            foreach (var sequence in (data.Sequences ?? []).Take(200))
            {
                for (var frame = 0; frame <= Math.Min(60, (int)sequence.Length); frame += 5)
                {
                    var sprites = SequenceEvaluator.At(sequence, frame);
                    Assert.All(sprites, s => Assert.True(float.IsFinite(s.X) && float.IsFinite(s.Y) && float.IsFinite(s.ScaleX) && float.IsFinite(s.ScaleY) && float.IsFinite(s.Rotation)));
                    found += sprites.Count;
                }
            }

            if (found == 0) continue;
            return;
        }

        Assert.Skip("No sequence with a sprite in the corpus.");
    }

    [Fact]
    public async Task ThePlayheadMovesTheCanvasAndAClickOnTheRulerMovesThePlayhead()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var sequence = (data.Sequences ?? []).FirstOrDefault(q => Enumerable.Range(0, 60).Any(f => SequenceEvaluator.At(q, f).Count > 0));
            if (sequence is null) continue;
            var visible = Enumerable.Range(0, 60).First(f => SequenceEvaluator.At(sequence, f).Count > 0);
            await HeadlessApp.Run(() =>
            {
                using var textures = new Rooms.TextureBitmaps();
                var tab = Editor.SequencePlayerView.Tab(sequence, textures, null);
                var window = new Avalonia.Controls.Window { Content = tab, Width = 900, Height = 700 };
                window.Show();
                var player = tab.Children.OfType<Editor.SequencePlayerView>().Single();
                var timeline = tab.Children.OfType<Editor.SequenceTimelineView>().Single();
                player.Frame = visible;
                Assert.Equal(visible, timeline.Playhead);
                Assert.NotEmpty(player.Canvas.Sprites);

                window.UpdateLayout();
                var scale = (timeline.Bounds.Width - 170 - 12) / timeline.Length;
                var click = Avalonia.VisualExtensions.TranslatePoint(timeline, new Avalonia.Point(170 + (7 * scale) + (scale / 4), 10), window)!.Value;
                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, click, Avalonia.Input.MouseButton.Left);
                Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, click, Avalonia.Input.MouseButton.Left);
                Assert.Equal(7, player.Frame);
                window.Close();
                return Task.CompletedTask;
            });
            return;
        }

        Assert.Skip("No sequence with a sprite in the corpus.");
    }
}
