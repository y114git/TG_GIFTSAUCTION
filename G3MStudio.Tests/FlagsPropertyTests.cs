using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class FlagsPropertyTests
{
    [Fact]
    public void AFlagsFieldIsAFlagsRowWithOneChoicePerSingleFlagAndTheTextReadsBack()
    {
        using var sound = new GameMakerSound { Flags = GameMakerSound.AudioEntryFlags.Regular | GameMakerSound.AudioEntryFlags.IsEmbedded };
        var row = PropertyModel.Describe(sound).Single(r => r.Path == nameof(GameMakerSound.Flags));
        Assert.Equal(PropertyKind.Flags, row.Kind);
        Assert.Contains("IsEmbedded", row.Choices!);
        Assert.DoesNotContain("Regular", row.Choices!); // zero, no bit of its own
        Assert.All(row.Choices!, name => Assert.True(Enum.TryParse<GameMakerSound.AudioEntryFlags>(name, out var value) && ((int)value & ((int)value - 1)) == 0));
        var text = PropertyModel.FlagsText(["IsEmbedded", "IsCompressed"]);
        Assert.Equal(GameMakerSound.AudioEntryFlags.IsCompressed | GameMakerSound.AudioEntryFlags.IsEmbedded, Enum.Parse<GameMakerSound.AudioEntryFlags>(text));
        Assert.Equal("0", PropertyModel.FlagsText([]));
        Assert.Equal((GameMakerSound.AudioEntryFlags)0, Enum.Parse<GameMakerSound.AudioEntryFlags>("0"));
    }

    [Fact]
    public void ColorFieldsAreColorRowsAndTheBytesRoundTripWithTheTopByteKept()
    {
        using var room = new GameMakerRoom { BackgroundColor = 0x00FF8040 };
        var row = PropertyModel.Describe(room).Single(r => r.Path == nameof(GameMakerRoom.BackgroundColor));
        Assert.Equal(PropertyKind.Color, row.Kind);
        Assert.False(PropertyModel.IsColorName("ColorDepth"));
        Assert.Equal((0x40, 0x80, 0xFF), PropertyModel.Rgb(0x00FF8040));
        Assert.Equal(0xAB010203u, PropertyModel.WithRgb(0xAB000000u, 3, 2, 1));
        Assert.Equal(0x00FF8040u, PropertyModel.WithRgb(0x00000000u, 0x40, 0x80, 0xFF));
    }

    [Fact]
    public void TheInfoFlagsOfGeneralInfoAreFlagsRows()
    {
        using var info = new GameMakerGeneralInfo { Info = GameMakerGeneralInfo.InfoFlags.Interpolate };
        var row = PropertyModel.Describe(info).Single(r => r.Path == nameof(GameMakerGeneralInfo.Info));
        Assert.Equal(PropertyKind.Flags, row.Kind);
        Assert.Contains("Interpolate", row.Choices!);
    }
}
