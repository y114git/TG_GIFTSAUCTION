using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class MaskTests
{
    [Fact]
    public void AMaskSurvivesAPngRoundTrip()
    {
        const int width = 11;
        const int height = 3;
        var stride = MaskPicture.Stride(width);
        var mask = new byte[stride * height];
        foreach (var (x, y) in new[] { (0, 0), (10, 0), (5, 1), (8, 2), (10, 2) }) mask[y * stride + x / 8] |= (byte)(0x80 >> (x % 8));

        var png = MaskPicture.ToPng(mask, width, height);
        Assert.Equal(mask, MaskPicture.FromPicture(png, width, height));
        Assert.Throws<InvalidDataException>(() => MaskPicture.FromPicture(png, width + 1, height));
    }

    [Fact]
    public async Task ImportingAMaskUndoesAndRoundTripsThroughTheJournal()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sprite = data.Sprites.First(s => s.CollisionMasks.Count > 0 && s.CollisionMasks[0].Width > 0 && s.CollisionMasks[0].Height > 0);
        var entry = sprite.CollisionMasks[0];
        var before = entry.Data.ToArray();
        var inverted = before.Select(b => (byte)~b).ToArray();

        var import = new ImportMaskCommand(sprite.Name.Content ?? string.Empty, 0, inverted);
        import.Apply(data);
        Assert.Equal(inverted, entry.Data);
        import.Revert(data);
        Assert.Equal(before, entry.Data);

        var replay = EditCommand.Create(import.Kind, import.Args);
        replay.Apply(data);
        Assert.Equal(inverted, entry.Data);
        Assert.Throws<InvalidDataException>(() => new ImportMaskCommand(sprite.Name.Content ?? string.Empty, 0, [1]).Apply(data));
    }

    [Fact]
    public async Task TheBoundingBoxModeIsEditedByName()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sprite = data.Sprites[0];
        var row = PropertyModel.Describe(sprite).Single(r => r.Path == "BBoxMode");
        Assert.Equal(PropertyKind.Enum, row.Kind);
        Assert.Equal(["Automatic", "FullImage", "Manual"], row.Choices);

        var set = new SetPropertyCommand("sprites", sprite.Name.Content ?? string.Empty, "BBoxMode", "Manual");
        set.Apply(data);
        Assert.Equal(2u, sprite.BBoxMode);
        Assert.Equal("Manual", PropertyModel.Describe(sprite).Single(r => r.Path == "BBoxMode").Value);
        set.Revert(data);
    }

    [Fact]
    public async Task EditingAStringChangesItForEveryUseAndUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var old = data.Strings[3].Content;
        var edit = EditCommand.Create(EditStringCommand.Name, new EditStringCommand(3, "changed text").Args);
        edit.Apply(data);
        Assert.Equal("changed text", data.Strings[3].Content);
        edit.Revert(data);
        Assert.Equal(old, data.Strings[3].Content);
        Assert.Throws<InvalidOperationException>(() => new EditStringCommand(data.Strings.Count, "x").Apply(data));
    }

    [Fact]
    public async Task AShaderSourceIsEditedThroughThePropertyCommand()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var shader = data.Shaders.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.GLSL_Vertex?.Content));
            if (shader is null) continue;
            var old = shader.GLSL_Vertex.Content;
            var set = new SetPropertyCommand("shaders", shader.Name.Content ?? string.Empty, "GLSL_Vertex", "// edited");
            set.Apply(data);
            Assert.Equal("// edited", shader.GLSL_Vertex.Content);
            set.Revert(data);
            Assert.Equal(old, shader.GLSL_Vertex.Content);
            return;
        }

        Assert.Skip("No shader in the corpus.");
    }

    [Fact]
    public async Task TheGlyphListFiltersByCharacterAndCode()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var font = data.Fonts.FirstOrDefault(f => f.Glyphs.Count > 0);
            if (font is null) continue;
            await HeadlessApp.Run(() =>
            {
                var view = new Editor.FontGlyphsView(font);
                Assert.Equal(font.Glyphs.Count, view.Shown);
                var filter = view.Children.OfType<Avalonia.Controls.TextBox>().Single();
                var first = font.Glyphs[0].Character;
                filter.Text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"U+{first:X4}");
                Assert.Equal(font.Glyphs.Count(g => g.Character == first), view.Shown);
                filter.Text = ((int)first).ToString(System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal(font.Glyphs.Count(g => g.Character == first), view.Shown);

                var old = font.Glyphs[0].Shift;
                var set = new SetPropertyCommand("fonts", font.Name.Content ?? string.Empty, "Glyphs[0].Shift", (old + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                set.Apply(data);
                Assert.Equal(old + 1, font.Glyphs[0].Shift);
                set.Revert(data);
                Assert.Equal(old, font.Glyphs[0].Shift);
                return Task.CompletedTask;
            });
            return;
        }

        Assert.Skip("No font with glyphs in the corpus.");
    }

    [Fact]
    public async Task LockingTheSpriteOriginMakesTheOriginBoxesReadOnly()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sprite = workspace.Data.Sprites.First(sp => sp.Textures.Count > 0);
        await HeadlessApp.Run(() =>
        {
            using var textures = new Rooms.TextureBitmaps();
            var view = new Editor.SpriteFramesView(sprite, textures, workspace.History);
            var tab = new Editor.PropertyGridTab(workspace, "sprites", sprite.Name.Content ?? "", view);
            var window = new Avalonia.Controls.Window { Content = tab, Width = 800, Height = 700 };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Avalonia.Controls.TextBox Box() => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TextBox>().Single(b => b.Name == "Prop_OriginX");
            Assert.True(Box().IsEnabled);
            Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(b => b.Name == "SpriteLockOrigin").IsChecked = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(Box().IsEnabled);
            window.Close();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void AGlyphRangeIsParsedFromCharactersOrCodes()
    {
        Assert.Equal([65, 66, 67], Editor.FontGlyphsView.ParseRange("A-C"));
        Assert.Equal([32, 33], Editor.FontGlyphsView.ParseRange("U+0020-U+0021"));
        Assert.Equal([65], Editor.FontGlyphsView.ParseRange("A"));
        Assert.Equal([45], Editor.FontGlyphsView.ParseRange("-"));
        Assert.Null(Editor.FontGlyphsView.ParseRange("C-A"));
        Assert.Null(Editor.FontGlyphsView.ParseRange("0-5000"));
        Assert.Null(Editor.FontGlyphsView.ParseRange("nonsense"));
    }

    [Fact]
    public async Task AParticleSystemListsOneLinePerEmitter()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var system = data.ParticleSystems?.FirstOrDefault(p => p.Emitters?.Count > 0);
            if (system is null) continue;
            Assert.Equal(system.Emitters.Count, new Editor.ParticleEmittersView(system).Lines.Count);
            return;
        }

        Assert.Skip("No particle system with emitters in the corpus.");
    }

    [Fact]
    public async Task ASequenceListsItsTracksAsATree()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var sequence = data.Sequences?.FirstOrDefault(q => q.Tracks?.Count > 0);
            if (sequence is null) continue;
            var view = new Editor.SequenceTracksView(sequence);
            Assert.True(view.Lines.Count >= sequence.Tracks.Count); // one line per track, nested ones too
            var timeline = new Editor.SequenceTimelineView(sequence);
            Assert.True(timeline.Rows.Count >= view.Lines.Count); // a row per track, plus moments and messages when there are some
            Assert.True(timeline.Length >= 1);
            return;
        }

        Assert.Skip("No sequence with tracks in the corpus.");
    }

    [Fact]
    public async Task ATimelineMomentStepIsEditedThroughItsPath()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var timeline = data.Timelines?.FirstOrDefault(t => t.Moments.Count > 0);
            if (timeline is null) continue;
            var old = timeline.Moments[0].Step;
            var set = new SetPropertyCommand("timelines", timeline.Name.Content ?? string.Empty, "Moments[0].Step", (old + 7).ToString(System.Globalization.CultureInfo.InvariantCulture));
            set.Apply(data);
            Assert.Equal(old + 7, timeline.Moments[0].Step);
            set.Revert(data);
            Assert.Equal(old, timeline.Moments[0].Step);
            return;
        }

        Assert.Skip("No timeline with moments in the corpus.");
    }

    [Fact]
    public async Task AMomentStepPathResolvesOnATimelineBuiltInCode()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        using var timeline = new G3MLib.DataFile.Models.GameMakerTimeline();
        using var moment = new G3MLib.DataFile.Models.GameMakerTimeline.GameMakerTimelineMoment { Step = 3 };
        timeline.Moments.Add(moment);
        PropertyModel.Set(data, timeline, "Moments[0].Step", "11");
        Assert.Equal(11u, moment.Step);
        Assert.Equal("11", PropertyModel.Read(timeline, "Moments[0].Step"));
    }

    [Fact]
    public async Task AddingAndRemovingATimelineMomentUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        using var timeline = new G3MLib.DataFile.Models.GameMakerTimeline { Name = data.Strings.MakeString("tl_test", out _) };
        data.Timelines.Add(timeline);
        var add = EditCommand.Create(TimelineMomentCommand.Name, new TimelineMomentCommand("tl_test", TimelineMomentCommand.Add, -1, 5).Args);
        add.Apply(data);
        Assert.Equal(5u, Assert.Single(timeline.Moments).Step);
        var remove = new TimelineMomentCommand("tl_test", TimelineMomentCommand.Remove, 0);
        remove.Apply(data);
        Assert.Empty(timeline.Moments);
        remove.Revert(data);
        Assert.Single(timeline.Moments);
        add.Revert(data);
        Assert.Empty(timeline.Moments);
        Assert.Throws<InvalidOperationException>(() => new TimelineMomentCommand("tl_test", TimelineMomentCommand.Remove, 3).Apply(data));
        data.Timelines.Remove(timeline);
    }

    [Fact]
    public async Task AnEmitterAndATrackAreEditedThroughTheirPaths()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var checkedEmitter = false;
        var checkedTrack = false;
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            if (!checkedEmitter && data.ParticleSystems?.FirstOrDefault(p => p.Emitters?.Count > 0) is { } system)
            {
                var old = system.Emitters[0].Resource.EmitCount;
                var set = new SetPropertyCommand("particleSystems", system.Name.Content ?? string.Empty, "Emitters[0].Resource.EmitCount", (old + 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
                set.Apply(data);
                Assert.Equal(old + 2, system.Emitters[0].Resource.EmitCount);
                set.Revert(data);
                Assert.Equal(old, system.Emitters[0].Resource.EmitCount);
                checkedEmitter = true;
            }

            if (!checkedTrack && data.Sequences?.FirstOrDefault(q => q.Tracks?.Count > 0) is { } sequence)
            {
                var old = sequence.Tracks[0].IsCreationTrack;
                var set = new SetPropertyCommand("sequences", sequence.Name.Content ?? string.Empty, "Tracks[0].IsCreationTrack", (!old).ToString());
                set.Apply(data);
                Assert.Equal(!old, sequence.Tracks[0].IsCreationTrack);
                set.Revert(data);
                Assert.Equal(old, sequence.Tracks[0].IsCreationTrack);
                checkedTrack = true;
            }

            if (checkedEmitter && checkedTrack) return;
        }

        Assert.True(checkedEmitter || checkedTrack, "Neither a particle system nor a sequence with tracks was in the corpus.");
    }

    [Fact]
    public async Task AddingAGlyphKeepsTheListSortedAndRemovingItUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var font = data.Fonts.FirstOrDefault(f => f.Glyphs.Count > 2);
            if (font is null) continue;
            var name = font.Name.Content ?? string.Empty;
            List<ushort> before = [.. font.Glyphs.Select(g => g.Character)];
            var free = (ushort)Enumerable.Range(before[1] + 1, ushort.MaxValue - before[1]).First(c => !before.Contains((ushort)c));
            var add = EditCommand.Create(FontGlyphCommand.Name, new FontGlyphCommand(name, FontGlyphCommand.Add, -1, free).Args);
            add.Apply(data);
            List<ushort> after = [.. font.Glyphs.Select(g => g.Character)];
            Assert.Equal(before.Count + 1, after.Count);
            Assert.Equal([.. after.Order()], after);
            Assert.Throws<InvalidOperationException>(() => new FontGlyphCommand(name, FontGlyphCommand.Add, -1, free).Apply(data));
            var remove = new FontGlyphCommand(name, FontGlyphCommand.Remove, 1);
            remove.Apply(data);
            Assert.Equal(before.Count, font.Glyphs.Count);
            remove.Revert(data);
            add.Revert(data);
            Assert.Equal(before, [.. font.Glyphs.Select(g => g.Character)]);
            return;
        }

        Assert.Skip("No font with three glyphs in the corpus.");
    }

    [Fact]
    public async Task AnAddedEmitterSurvivesAWriteAndAReadAndUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var system = data.ParticleSystems?.FirstOrDefault(p => p.Emitters?.Count > 0);
            if (system is null) continue;
            var name = system.Name.Content ?? string.Empty;
            var (emitters, table) = (system.Emitters.Count, data.ParticleSystemEmitters.Count);
            var add = EditCommand.Create(ParticleEmitterCommand.Name, new ParticleEmitterCommand(name, ParticleEmitterCommand.Add).Args);
            add.Apply(data);
            Assert.Equal((emitters + 1, table + 1), (system.Emitters.Count, data.ParticleSystemEmitters.Count));

            using var stream = new MemoryStream();
            await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
            stream.Position = 0;
            using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(emitters + 1, back.ParticleSystems.First(p => p.Name.Content == name).Emitters.Count);

            var remove = new ParticleEmitterCommand(name, ParticleEmitterCommand.Remove, 0);
            remove.Apply(data);
            Assert.Equal(emitters, system.Emitters.Count);
            remove.Revert(data);
            add.Revert(data);
            Assert.Equal((emitters, table), (system.Emitters.Count, data.ParticleSystemEmitters.Count));
            return;
        }

        Assert.Skip("No particle system with emitters in the corpus.");
    }

    [Fact]
    public async Task RemovingASequenceTrackUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var sequence = data.Sequences?.FirstOrDefault(q => q.Tracks?.Count > 0);
            if (sequence is null) continue;
            var count = sequence.Tracks.Count;
            var first = sequence.Tracks[0];
            var remove = EditCommand.Create(SequenceTrackCommand.Name, new SequenceTrackCommand(sequence.Name.Content ?? string.Empty, SequenceTrackCommand.Remove, 0).Args);
            remove.Apply(data);
            Assert.Equal(count - 1, sequence.Tracks.Count);
            remove.Revert(data);
            Assert.Same(first, sequence.Tracks[0]);
            return;
        }

        Assert.Skip("No sequence with tracks in the corpus.");
    }

    [Fact]
    public async Task TheEmitterListHasAVisibleSizeInAWindow()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length))
        {
            using var dir = new TempDir();
            var userData = new UserData(dir.File("user"));
            using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var system = workspace.Data.ParticleSystems?.FirstOrDefault(p => p.Emitters?.Count > 0);
            if (system is null) continue;
            await HeadlessApp.Run(() =>
            {
                var view = new Editor.ParticleEmittersView(system, workspace);
                var tab = new Editor.PropertyGridTab(workspace, "particleSystems", system.Name.Content ?? "", view);
                var window = new Avalonia.Controls.Window { Content = tab, Width = 900, Height = 700 };
                window.Show();
                window.UpdateLayout();
                Assert.True(view.Bounds.Height > 10, $"height {view.Bounds.Height}, desired {view.DesiredSize}, tab {tab.Bounds}, children {string.Join(';', Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).Take(6).Select(d => d.GetType().Name + d.Bounds))}");
                return Task.CompletedTask;
            });
            return;
        }

        Assert.Skip("No particle system in the corpus.");
    }

    [Fact]
    public async Task AnActionAddedToAMomentSurvivesAWriteAndAReadAndUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var code = data.Code.First(c => c.Name.Content is not null).Name.Content ?? string.Empty;
        using var timeline = new G3MLib.DataFile.Models.GameMakerTimeline { Name = data.Strings.MakeString("tl_action_test", out _) };
        data.Timelines.Add(timeline);
        new TimelineMomentCommand("tl_action_test", TimelineMomentCommand.Add, -1, 4).Apply(data);
        var add = EditCommand.Create(TimelineActionCommand.Name, new TimelineActionCommand("tl_action_test", TimelineActionCommand.Add, 0, -1, code).Args);
        add.Apply(data);
        Assert.Equal(code, Assert.Single(timeline.Moments[0].Event).CodeId?.Name.Content);

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var read = back.Timelines.First(t => t.Name.Content == "tl_action_test");
        Assert.Equal((4u, code), (read.Moments[0].Step, Assert.Single(read.Moments[0].Event).CodeId?.Name.Content));

        var remove = new TimelineActionCommand("tl_action_test", TimelineActionCommand.Remove, 0, 0);
        remove.Apply(data);
        Assert.Empty(timeline.Moments[0].Event);
        remove.Revert(data);
        add.Revert(data);
        Assert.Empty(timeline.Moments[0].Event);
        Assert.Throws<InvalidOperationException>(() => new TimelineActionCommand("tl_action_test", TimelineActionCommand.Add, 0, -1, "no_such_code").Apply(data));
        data.Timelines.Remove(timeline);
    }

    [Fact]
    public async Task AnAddedGroupTrackSurvivesAWriteAndAReadAndUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var sequence = data.Sequences?.FirstOrDefault(q => q.Tracks?.Count > 0);
            if (sequence is null) continue;
            var name = sequence.Name.Content ?? string.Empty;
            var count = sequence.Tracks.Count;
            var add = EditCommand.Create(SequenceTrackCommand.Name, new SequenceTrackCommand(name, SequenceTrackCommand.AddGroup, -1, "Test Group").Args);
            add.Apply(data);
            Assert.Equal(count + 1, sequence.Tracks.Count);

            using var stream = new MemoryStream();
            await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
            stream.Position = 0;
            using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
            var read = back.Sequences.First(q => q.Name.Content == name).Tracks;
            Assert.Equal(("Test Group", "GMGroupTrack"), (read[^1].Name.Content, read[^1].ModelName.Content));

            add.Revert(data);
            Assert.Equal(count, sequence.Tracks.Count);
            return;
        }

        Assert.Skip("No sequence with tracks in the corpus.");
    }

    [Fact]
    public async Task AListStartsClosedInThePropertyGridAndOpensWhenToggled()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var dir = new TempDir();
            var userData = new UserData(dir.File("user"));
            using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            // A list whose items the grid edits itself: the first room that has one.
            var room = workspace.Data.Rooms.FirstOrDefault(r => ListOf(PropertyModel.Describe(r)) is not null);
            if (room is null) continue;
            var list = ListOf(PropertyModel.Describe(room))!;
            await HeadlessApp.Run(() =>
            {
                var tab = new Editor.PropertyGridTab(workspace, "rooms", room.Name.Content ?? "");
                var window = new Avalonia.Controls.Window { Content = tab, Width = 700, Height = 600 };
                window.Show();
                window.UpdateLayout();
                bool ItemsShown() => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TextBox>().Any(b => b.Name?.StartsWith("Prop_" + list + "[", StringComparison.Ordinal) == true);
                Assert.False(ItemsShown());
                tab.Toggle(list);
                window.UpdateLayout();
                Assert.True(ItemsShown());
                tab.Toggle(list);
                window.UpdateLayout();
                Assert.False(ItemsShown());
                return Task.CompletedTask;
            });
            return;
        }

        Assert.Skip("No path with points in the corpus.");
    }

    private static string? ListOf(IReadOnlyList<PropertyRow> rows) =>
        rows.Where(r => r.Kind == PropertyKind.List).Select(r => r.Path).FirstOrDefault(list => rows.Any(c => c.Kind == PropertyKind.Whole && !c.ReadOnly && c.Path.StartsWith(list + "[", StringComparison.Ordinal)));
}
