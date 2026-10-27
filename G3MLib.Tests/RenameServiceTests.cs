using System.Collections;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.References;

namespace G3MLib.Tests;

public class RenameServiceTests
{
    private static GameMakerData Load(string fixture)
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        return GameMakerIO.Read(input);
    }

    private static byte[] Save(GameMakerData data)
    {
        using MemoryStream output = new();
        GameMakerIO.Write(output, data);
        return output.ToArray();
    }

    private static readonly (string Type, Func<GameMakerData, IList> List, Func<GameMakerData, string, GameMakerNamedResource> Create)[] Types =
    [
        ("Sprite", d => (IList)d.Sprites, (d, n) => new GameMakerSprite { Name = d.Strings.MakeString(n) }),
        ("Object", d => (IList)d.GameObjects, (d, n) => new GameMakerGameObject { Name = d.Strings.MakeString(n) }),
        ("Room", d => (IList)d.Rooms, (d, n) => new GameMakerRoom { Name = d.Strings.MakeString(n), Caption = d.Strings.MakeString("") }),
        ("Script", d => (IList)d.Scripts, (d, n) => new GameMakerScript { Name = d.Strings.MakeString(n) }),
        ("Sound", d => (IList)d.Sounds, (d, n) => new GameMakerSound { Name = d.Strings.MakeString(n) }),
        ("Font", d => (IList)d.Fonts, (d, n) => new GameMakerFont { Name = d.Strings.MakeString(n) }),
        ("Path", d => (IList)d.Paths, (d, n) => new GameMakerPath { Name = d.Strings.MakeString(n) }),
        ("Timeline", d => (IList)d.Timelines, (d, n) => new GameMakerTimeline { Name = d.Strings.MakeString(n) }),
        ("Shader", d => (IList)d.Shaders, (d, n) => new GameMakerShader { Name = d.Strings.MakeString(n) }),
        ("Sequence", d => (IList)d.Sequences, (d, n) => new GameMakerSequence { Name = d.Strings.MakeString(n) }),
        ("AnimationCurve", d => (IList)d.AnimationCurves, (d, n) => new GameMakerAnimationCurve { Name = d.Strings.MakeString(n) }),
        ("ParticleSystem", d => (IList)d.ParticleSystems, (d, n) => new GameMakerParticleSystem { Name = d.Strings.MakeString(n) }),
        ("Background", d => (IList)d.Backgrounds, (d, n) => new GameMakerBackground { Name = d.Strings.MakeString(n) }),
    ];

    public static IEnumerable<object[]> TypeNames => Types.Select(t => new object[] { t.Type });

    private static GameMakerNamedResource Pick(GameMakerData data, string type)
    {
        var entry = Types.Single(t => t.Type == type);
        IList list = entry.List(data);
        if (list.OfType<GameMakerNamedResource>().FirstOrDefault(r => r.Name?.Content is not null) is { } existing)
        {
            return existing;
        }
        GameMakerNamedResource created = entry.Create(data, "probe_" + type);
        list.Add(created);
        return created;
    }

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void RenamesEachResourceType(string type)
    {
        using GameMakerData data = Load("data-2026.win");
        GameMakerNamedResource resource = Pick(data, type);
        string oldName = resource.Name.Content;
        // A resource added bare by Pick cannot be written, so only fixture resources are saved
        bool writable = resource.Name.Content != "probe_" + type;
        byte[] before = writable ? Save(data) : [];

        RenameChangeSet changes = RenameService.RenameResource(data, resource, "renamed_probe");

        Assert.Equal("renamed_probe", resource.Name.Content);
        Assert.Contains(changes, c => c.OldValue == oldName && c.NewValue == "renamed_probe" && c.Location.Contains(oldName));
        if (writable)
        {
            using MemoryStream stream = new(Save(data));
            using GameMakerData reread = GameMakerIO.Read(stream);
            Assert.Contains(reread.Strings, s => s.Content == "renamed_probe");
        }

        changes.Undo();
        Assert.Equal(oldName, resource.Name.Content);
        if (writable)
        {
            Assert.Equal(before, Save(data));
        }
        changes.Redo();
        Assert.Equal("renamed_probe", resource.Name.Content);
    }

    [Fact]
    public void RenameThenRenameBackIsByteIdentical()
    {
        using GameMakerData data = Load("data-2026.win");
        byte[] before = Save(data);
        foreach (string type in new[] { "Object", "Script", "Room", "Sprite" })
        {
            GameMakerNamedResource resource = Pick(data, type);
            string name = resource.Name.Content;
            RenameService.RenameResource(data, resource, "tmp_probe_name");
            RenameService.RenameResource(data, resource, name);
        }
        Assert.Equal(before, Save(data));
    }

    [Fact]
    public void ObjectRenameUpdatesEventCodeNames()
    {
        using GameMakerData data = Load("data-2026.win");
        GameMakerGameObject obj = data.GameObjects.First(o => data.Code.Any(c => c.Name.Content.StartsWith("gml_Object_" + o.Name.Content + "_Create")));
        string old = obj.Name.Content;
        List<GameMakerCode> owned = data.Code.Where(c => c.Name.Content.StartsWith("gml_Object_" + old + "_Create")).ToList();

        RenameChangeSet changes = RenameService.RenameResource(data, obj, "obj_renamed_probe");

        Assert.All(owned, c => Assert.StartsWith("gml_Object_obj_renamed_probe_Create", c.Name.Content));
        Assert.Contains(changes, c => c.Location.StartsWith("Code"));
        changes.Undo();
        Assert.All(owned, c => Assert.StartsWith("gml_Object_" + old + "_Create", c.Name.Content));
    }

    [Fact]
    public void ScriptRenameUpdatesCodeAndFunctionNames()
    {
        using GameMakerData data = Load("data-2026.win");
        GameMakerScript script = data.Scripts.First(s => data.Functions.Any(f => f.Name.Content == "gml_Script_" + s.Name.Content));
        string old = script.Name.Content;
        GameMakerFunction function = data.Functions.First(f => f.Name.Content == "gml_Script_" + old);

        RenameService.RenameResource(data, script, "scr_renamed_probe");

        Assert.Equal("gml_Script_scr_renamed_probe", function.Name.Content);
        Assert.DoesNotContain(data.Code, c => c.Name.Content == "gml_Script_" + old || c.Name.Content == "gml_GlobalScript_" + old);
    }

    [Fact]
    public void RoomRenameUpdatesCreationCodeNames()
    {
        using GameMakerData data = Load("data-2026.win");
        GameMakerRoom room = data.Rooms[0];
        string old = room.Name.Content;
        GameMakerCode.CreateEmptyEntry(data, "gml_Room_" + old + "_Create");
        GameMakerCode.CreateEmptyEntry(data, "gml_RoomCC_" + old + "_100001_Create");

        RenameService.RenameResource(data, room, "rm_renamed_probe");

        Assert.Contains(data.Code, c => c.Name.Content.StartsWith("gml_Room_rm_renamed_probe_"));
        Assert.DoesNotContain(data.Code, c => c.Name.Content.StartsWith("gml_Room_" + old + "_"));
        Assert.Contains(data.Code, c => c.Name.Content == "gml_RoomCC_rm_renamed_probe_100001_Create");
    }

    [Fact]
    public void CodeLiteralsEqualToTheNameAreRenamedAndListed()
    {
        using GameMakerData data = Load("data-2026.win");
        GameMakerNamedResource named = data.Sprites[0];
        GameMakerInstruction literal = data.Code.SelectMany(c => c.Instructions).First(i => i.ValueString is not null);
        literal.ValueString.Resource = named.Name;
        ReferenceIndex index = ReferenceIndex.Build(data);

        RenameChangeSet changes = RenameService.RenameResource(data, named, "literal_probe", index);

        Assert.Contains(changes.Uses, u => u.Member == "String");
        Assert.All(changes.Uses.Where(u => u.Member == "String"), u => Assert.Equal("literal_probe", ((GameMakerString)u.Target).Content));
    }

    [Fact]
    public void RenamesAVariableInEveryEntry()
    {
        using GameMakerData data = Load("data-2026.win");
        GameMakerVariable variable = data.Variables.First(v => v.InstanceType == GameMakerInstruction.InstanceType.Global);
        string old = variable.Name.Content;
        int count = data.Variables.Count(v => v.Name.Content == old);

        RenameChangeSet changes = RenameService.RenameVariable(data, old, "var_renamed_probe");

        Assert.Equal(count, data.Variables.Count(v => v.Name.Content == "var_renamed_probe"));
        Assert.DoesNotContain(data.Variables, v => v.Name.Content == old);
        changes.Undo();
        Assert.Equal(count, data.Variables.Count(v => v.Name.Content == old));
    }

    [Fact]
    public void RenamesAGmlFunctionAndItsCodeEntries()
    {
        using GameMakerData data = Load("data-2026.win");
        GameMakerFunction function = data.Functions.First(f => f.Name.Content.StartsWith("gml_Script_", StringComparison.Ordinal) && !f.Name.Content.StartsWith("gml_Script_anon", StringComparison.Ordinal)
            && !data.Scripts.Any(s => s.Name.Content == f.Name.Content["gml_Script_".Length..]));
        string old = function.Name.Content["gml_Script_".Length..];

        RenameChangeSet changes = RenameService.RenameFunction(data, old, "fn_renamed_probe");

        Assert.Equal("gml_Script_fn_renamed_probe", function.Name.Content);
        Assert.DoesNotContain(data.Code, c => c.Name.Content == "gml_Script_" + old);
        changes.Undo();
        Assert.Equal("gml_Script_" + old, function.Name.Content);
    }

    [Fact]
    public void RefusesInvalidAndCollidingNames()
    {
        using GameMakerData data = Load("data-2026.win");
        GameMakerSprite sprite = data.Sprites[0];
        string other = data.GameObjects[0].Name.Content;
        string[] names = data.Variables.Select(v => v.Name.Content).Where(n => n.All(ch => char.IsLetterOrDigit(ch) || ch == '_')).Distinct().ToArray();
        string firstVariable = names[0];
        string otherVariable = names[1];

        Assert.Equal(RenameFailure.InvalidName, Assert.Throws<RenameException>(() => RenameService.RenameResource(data, sprite, "1bad")).Reason);
        Assert.Equal(RenameFailure.InvalidName, Assert.Throws<RenameException>(() => RenameService.RenameResource(data, sprite, "has space")).Reason);
        Assert.Equal(RenameFailure.InvalidName, Assert.Throws<RenameException>(() => RenameService.RenameResource(data, sprite, "")).Reason);
        Assert.Equal(RenameFailure.InvalidName, Assert.Throws<RenameException>(() => RenameService.RenameResource(data, sprite, "while")).Reason);
        RenameException collision = Assert.Throws<RenameException>(() => RenameService.RenameResource(data, sprite, other));
        Assert.Equal(RenameFailure.NameInUse, collision.Reason);
        Assert.Contains(other, collision.Message);
        Assert.Equal(RenameFailure.NameInUse, Assert.Throws<RenameException>(() => RenameService.RenameVariable(data, firstVariable, otherVariable)).Reason);
        Assert.Equal(RenameFailure.NotFound, Assert.Throws<RenameException>(() => RenameService.RenameVariable(data, "no_such_variable_here", "fine_name")).Reason);
        Assert.Equal(RenameFailure.Builtin, Assert.Throws<RenameException>(() => RenameService.RenameFunction(data, data.Functions.First(f => !f.Name.Content.StartsWith("gml_Script_") && data.Scripts.All(s => s.Name.Content != f.Name.Content)).Name.Content, "fine_name")).Reason);
    }

    [Fact]
    public void RefusedRenameChangesNothing()
    {
        using GameMakerData data = Load("data-2026.win");
        byte[] before = Save(data);
        Assert.Throws<RenameException>(() => RenameService.RenameResource(data, data.Sprites[0], data.Rooms[0].Name.Content));
        Assert.Equal(before, Save(data));
    }

    [Fact]
    public void UndoRefusesWhenTheNameChangedSince()
    {
        using GameMakerData data = Load("data-2026.win");
        RenameChangeSet changes = RenameService.RenameResource(data, data.Sprites[0], "undo_probe");
        data.Sprites[0].Name.Content = "edited_elsewhere";
        Assert.Throws<InvalidOperationException>(changes.Undo);
    }

    public static IEnumerable<object[]> ExternalFiles()
    {
        string? configured = Environment.GetEnvironmentVariable("G3MLIB_RENAME_DATA_FILES");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return [["-"]];
        }
        return configured.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => new object[] { p });
    }

    [Theory]
    [MemberData(nameof(ExternalFiles))]
    public void RenameAndBackKeepsExternalFilesByteIdentical(string path)
    {
        if (path == "-")
        {
            return;
        }
        using GameMakerData data = GameMakerIO.Read(File.OpenRead(path));
        byte[] before = Save(data);
        int renamed = 0;
        foreach (string type in new[] { "Object", "Script", "Room", "Sprite", "Sound", "Font", "Timeline" })
        {
            IList list = Types.Single(t => t.Type == type).List(data);
            if (list is null || list.Count == 0)
            {
                continue;
            }
            GameMakerNamedResource resource = (GameMakerNamedResource)list[list.Count / 2];
            string name = resource.Name.Content;
            RenameService.RenameResource(data, resource, "tmp_probe_name");
            Assert.Equal("tmp_probe_name", resource.Name.Content);
            RenameService.RenameResource(data, resource, name);
            renamed++;
        }
        string variableName = data.Variables.First(v => v.InstanceType == GameMakerInstruction.InstanceType.Global).Name.Content;
        RenameService.RenameVariable(data, variableName, "tmp_probe_var");
        RenameService.RenameVariable(data, "tmp_probe_var", variableName);

        Assert.True(renamed > 0);
        Assert.Equal(before, Save(data));
    }
}
