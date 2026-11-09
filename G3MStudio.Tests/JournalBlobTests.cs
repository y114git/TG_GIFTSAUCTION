using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class JournalBlobTests
{
    private static readonly DataFileIdentity Identity = new(Path.GetFullPath("blob-test.win"), 10, "abc");

    [Fact]
    public void LargeArgumentsGoToASideFileAndComeBackOnRecovery()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var big = new string('A', Journal.BlobThreshold * 3);
        var file = Journal.FileFor(userData, Identity.Path);
        var (journal, _) = Journal.Open(userData, Identity);
        journal.Append(new JournalRecord("do", "replaceAudio", new() { ["sound"] = "snd_a", ["audio"] = big }));
        journal.Append(new JournalRecord("do", "replaceAudio", new() { ["sound"] = "snd_b", ["audio"] = big }));
        journal.Dispose();

        Assert.True(new FileInfo(file).Length < 2000, "the journal line holds a reference, not the value");
        Assert.Single(Directory.GetFiles(file + ".blobs"));
        var (again, recovered) = Journal.Open(userData, Identity);
        Assert.Equal([big, big], recovered.Select(r => r.Args!["audio"]));
        Assert.Equal(["snd_a", "snd_b"], recovered.Select(r => r.Args!["sound"]));

        again.Reset(Identity, []);
        Assert.Empty(Directory.GetFiles(file + ".blobs"));
        again.Dispose();
    }

    [Fact]
    public void AJournalWrittenBeforeSideFilesStillReplays()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var file = Journal.FileFor(userData, Identity.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var big = new string('B', Journal.BlobThreshold * 2);
        File.WriteAllLines(file,
        [
            System.Text.Json.JsonSerializer.Serialize(Identity),
            System.Text.Json.JsonSerializer.Serialize(new JournalRecord("do", "replaceAudio", new() { ["sound"] = "snd_a", ["audio"] = big })),
        ]);

        var (journal, recovered) = Journal.Open(userData, Identity);
        journal.Dispose();

        Assert.Equal(big, Assert.Single(recovered).Args!["audio"]);
    }

    [Fact]
    public void ARecordWhoseSideFileIsMissingEndsTheReplay()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var file = Journal.FileFor(userData, Identity.Path);
        var (journal, _) = Journal.Open(userData, Identity);
        journal.Append(new JournalRecord("undo"));
        journal.Append(new JournalRecord("do", "replaceAudio", new() { ["sound"] = "snd_a", ["audio"] = new string('C', Journal.BlobThreshold * 2) }));
        journal.Dispose();
        foreach (var blob in Directory.GetFiles(file + ".blobs")) File.Delete(blob);

        var (again, recovered) = Journal.Open(userData, Identity);
        again.Dispose();

        Assert.Equal(["undo"], recovered.Select(r => r.Op));
    }

    [Fact]
    public void AReferenceThatIsNotAHashIsNeverFollowedAsAPath()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var secret = dir.File("secret.txt");
        File.WriteAllText(secret, "private");
        var file = Journal.FileFor(userData, Identity.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllLines(file,
        [
            System.Text.Json.JsonSerializer.Serialize(Identity),
            System.Text.Json.JsonSerializer.Serialize(new JournalRecord("undo")),
            System.Text.Json.JsonSerializer.Serialize(new JournalRecord("do", "replaceAudio", new() { ["sound"] = "snd_a" }, new() { ["audio"] = secret })),
            System.Text.Json.JsonSerializer.Serialize(new JournalRecord("do", "replaceAudio", new() { ["sound"] = "snd_a" }, new() { ["audio"] = "..\\..\\secret.txt" })),
        ]);

        var (journal, recovered) = Journal.Open(userData, Identity);
        journal.Dispose();

        Assert.Equal(["undo"], recovered.Select(r => r.Op));
    }

    [Fact]
    public void AnOrdinaryValueThatLooksLikeAReferenceIsKeptAsItIs()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var secret = dir.File("secret.txt");
        File.WriteAllText(secret, "private");
        var (journal, _) = Journal.Open(userData, Identity);
        journal.Append(new JournalRecord("do", "property", new() { ["text"] = "g3mblob:" + secret }));
        journal.Dispose();

        var (again, recovered) = Journal.Open(userData, Identity);
        again.Dispose();

        Assert.Equal("g3mblob:" + secret, Assert.Single(recovered).Args!["text"]);
    }
}
