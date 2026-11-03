using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class ExplorerLabelTests
{
    [Fact]
    public async Task AGameMakerStudio1GameCallsTheTileSetGroupBackgrounds()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            using var data = TestData.Create(1);
            var view = new ExplorerView();
            view.Load("new.win", data);
            Assert.Contains(view.Model.Rows, r => r.Key == "tileSets" && r.Text == "Tile Sets");

            data.GeneralInfo.Major = 1;
            view.Load("old.win", data);
            Assert.Contains(view.Model.Rows, r => r.Key == "tileSets" && r.Text == "Backgrounds");
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheVariablesChunkHeaderCountersAreShownInTheUsersLanguage()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            using var data = TestData.Create(1);
            data.VarCount1 = 7;
            data.VarCount2 = 9;
            data.MaxLocalVarCount = 11;
            var text = MainWindow.VariableCounts(data);
            Assert.Contains("7 instance", text, StringComparison.Ordinal);
            Assert.Contains("9 global", text, StringComparison.Ordinal);
            Assert.Contains("11 local", text, StringComparison.Ordinal);
            return Task.CompletedTask;
        });
    }
}
