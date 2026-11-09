namespace G3MStudio.Tests;

public sealed class MainWindowTests
{
    [Fact]
    public async Task MainWindowOpens()
    {
        await HeadlessApp.Run(() =>
        {
            var window = new MainWindow();
            window.Show();
            Assert.True(window.IsVisible);
            Assert.Equal("G3MStudio", window.Title);
            window.Close();
        });
    }
}
