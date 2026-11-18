using System.Net;
using G3MStudio.Core.Links;

namespace G3MStudio.Tests;

public sealed class LinkDownloaderTests
{
    private sealed class Stub(byte[] body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
    }

    [Fact]
    public async Task AFileIsSavedUnderANameThatCannotLeaveTheFolder()
    {
        using var dir = new TempDir();
        using var stub = new Stub([1, 2, 3]);
        using var http = new HttpClient(stub, false);
        var path = await LinkDownloader.DownloadAsync(http, "https://example.com/a/..%2F..%2Fdata.win", dir.Path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(dir.Path, Path.GetDirectoryName(path));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.part"));
    }

    [Fact]
    public async Task OnlyHttpsIsFetchedAndTheSizeLimitStopsADownloadWithoutLeavingAFile()
    {
        using var dir = new TempDir();
        using var stub = new Stub(new byte[100]);
        using var http = new HttpClient(stub, false);
        await Assert.ThrowsAsync<ArgumentException>(() => LinkDownloader.DownloadAsync(http, "http://example.com/x.win", dir.Path, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<IOException>(() => LinkDownloader.DownloadAsync(http, "https://example.com/x.win", dir.Path, limit: 10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public void AnAddressWithNoFileNameGetsAPlainName() => Assert.Equal("download", LinkDownloader.FileNameOf(new Uri("https://example.com/")));
}
