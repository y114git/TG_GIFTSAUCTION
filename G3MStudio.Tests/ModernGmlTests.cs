using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class ModernGmlTests
{
    [Theory]
    [InlineData("var n = 3;\nvar s = $\"WAVE {n}\";")]
    [InlineData("var a = undefined;\na ??= 5;")]
    [InlineData("var h = variable_get_hash(\"hp\");")]
    public void TheCompilerTakesTheModernSyntaxOfTheNewestVersion(string source)
    {
        using var data = GameMakerData.CreateNew2026LTS();
        var group = new CodeImportGroup(data);
        group.QueueReplace("gml_Script_probe", source);
        Assert.True(group.Import(throwOnFailedCompile: false).Successful);
    }

    [Fact]
    public void TheNameListFollowsTheGameMakerVersionOfTheData()
    {
        using var data = GameMakerData.CreateNew2026LTS();
        Assert.Equal("Function", GmlNames.BuiltinFor(data).GetValueOrDefault("variable_get_hash"));
        data.GeneralInfo.Major = 1; // GameMaker Studio 1.x has no structs and no hashes
        Assert.DoesNotContain("variable_get_hash", GmlNames.BuiltinFor(data).Keys);
    }
}
