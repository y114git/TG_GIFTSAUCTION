using G3MLib.DataFile;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class BypassTests
{
    [Fact]
    public void RiskPolicyFollowsTheTwoSettings()
    {
        Assert.Equal(RiskChoice.Ask, RiskPolicy.Of(new AppSettings()));
        Assert.Equal(RiskChoice.Refuse, RiskPolicy.Of(new AppSettings { AskRiskyActions = false }));
        Assert.Equal(RiskChoice.Allow, RiskPolicy.Of(new AppSettings { AllowUnsafeSilently = true }));
        Assert.Equal(RiskChoice.Allow, RiskPolicy.Of(new AppSettings { AskRiskyActions = false, AllowUnsafeSilently = true }));
    }

    [Fact]
    public async Task ADeleteUsedInCodeIsRefusedThenAllowedAndTheFileStillWritesAndUndoes()
    {
        using var data = TestData.Create(2, true);
        var history = new EditHistory(data);
        history.Execute(new EditCodeCommand("gml_Script_probe", "return scr_1();"));
        var scripts = data.Scripts.Count;

        Assert.Throws<CodeUseException>(() => new ClearReferencesCommand("scripts", "scr_1").Apply(data));
        Assert.Equal(scripts, data.Scripts.Count);

        var forced = new GroupCommand([new ClearReferencesCommand("scripts", "scr_1", true), new ResourceListCommand("scripts", ResourceListCommand.Delete, "scr_1")]);
        forced.Apply(data);
        Assert.Equal(scripts - 1, data.Scripts.Count);
        using (var stream = new MemoryStream())
        {
            await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken); // the file still writes
            stream.Position = 0;
            using var reread = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(scripts - 1, reread.Scripts.Count);
        }

        forced.Revert(data);
        Assert.Equal(scripts, data.Scripts.Count);

        var replayed = ClearReferencesCommand.FromArgs(new ClearReferencesCommand("scripts", "scr_1", true).Args); // the journal keeps the choice
        replayed.Apply(data);
        Assert.Equal(scripts, data.Scripts.Count);
    }

    [Theory]
    [InlineData("my sprite")]
    [InlineData("spr-é")]
    [InlineData("1abc")]
    public void AnUnusualNameIsRefusedThenAllowedAndStaysUndoable(string name)
    {
        using var data = TestData.Create(0);
        var history = new EditHistory(data);
        Assert.Throws<UnusualNameException>(() => ResourceTemplates.New(data, "paths", name).Apply(data));
        history.Execute(ResourceTemplates.New(data, "paths", name, true));
        Assert.Contains(data.Paths, p => p.Name.Content == name);
        var replay = ResourceListCommand.FromArgs(ResourceTemplates.New(data, "paths", "x y", true).Args);
        Assert.True(replay.Args.ContainsKey("force")); // the journal keeps the choice
        history.Undo();
        Assert.DoesNotContain(data.Paths, p => p.Name.Content == name);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("")]
    public void ANameThatIsNotAFileNameStaysRefusedEvenWhenAllowed(string name)
    {
        using var data = TestData.Create(0);
        var error = Assert.Throws<InvalidOperationException>(() => ResourceTemplates.New(data, "paths", name, true).Apply(data));
        Assert.IsNotType<UnusualNameException>(error);
        Assert.Contains("file name", error.Message, StringComparison.Ordinal);
    }
}
