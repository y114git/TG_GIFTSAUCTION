using G3MStudio.Core.Settings;

namespace G3MStudio.Tests;

public sealed class CrashScrubTests
{
    [Fact]
    public void TheUserFolderBecomesAPlaceholderAndTheShapeOfThePathStays()
    {
        var text = CrashScrub.Text(@"Cannot open C:\Users\Ann\Games\Undertale\data.win and D:\mods\secret.zip", @"C:\Users\Ann");
        Assert.Equal(@"Cannot open C:\Users\<user>\<dir>\<dir>\data.win and D:\<dir>\<file>.zip", text);
    }

    [Theory]
    [InlineData(@"C:\Users\Ann\Documents\My Games\Quest Z9\save1.sav", @"C:\Users\<user>\Documents\<dir>\<dir>\<file>.sav")]
    [InlineData("/home/zoe/.config/G3MStudio/settings.json", "/home/<user>/.config/G3MStudio/settings.json")]
    [InlineData("/Users/zoe/Library/Application Support/Hero/art.png", "/Users/<user>/Library/Application Support/<dir>/<file>.png")]
    [InlineData(@"\\NAS01\games\hero\a.win", @"\\<host>\<dir>\<dir>\<file>.win")]
    [InlineData(@"C:\Windows\System32\drivers\x\kernel32.dll", @"C:\Windows\System32\<dir>\<dir>\<file>.dll")]
    [InlineData(@"C:\Windows\Fonts\arial.ttf", @"C:\Windows\<dir>\<file>.ttf")]
    [InlineData("/usr/share/dotnet/shared/x.dll", "/usr/share/<dir>/<dir>/<file>.dll")]
    [InlineData(@"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.0\System.Private.CoreLib.dll", @"C:\Program Files\dotnet\<dir>\<dir>\<dir>\<file>.dll")]
    public void PathsKeepTheirStructure(string path, string expected) => Assert.Equal(expected, CrashScrub.HidePath(path, "/nowhere"));

    [Fact]
    public void ThePathOfTheCurrentHomeFolderIsHiddenWhateverItIsCalled()
    {
        Assert.Equal(@"E:\profiles\<user>\<dir>\<file>.png", CrashScrub.HidePath(@"E:\profiles\Zed\art\a.png", @"E:\profiles\Zed"));
        Assert.Equal("/data/<user>/<file>.txt", CrashScrub.HidePath("/data/zed/a.txt", "/data/zed"));
    }

    [Fact]
    public void ThePathsOfAMessageAreHiddenAndTheRestStays()
    {
        Assert.Equal("Index was out of range", CrashScrub.Text("Index was out of range", "/home/zoe"));
        Assert.Equal("and/or a path /home/<user>/<file>.win here", CrashScrub.Text("and/or a path /home/zoe/a.win here", "/nowhere"));
        Assert.Equal("", CrashScrub.Text(null));
        Assert.Equal(CrashScrub.Text("/home/zoe/a.win", "/nowhere"), CrashScrub.Text("/home/zoe/a.win", "/nowhere"));
    }

    [Fact]
    public void QuotedNamesAreHiddenAndApostrophesStay()
    {
        Assert.Equal("Cannot find <name> and <name> in <name>; it can't load and isn't ready", CrashScrub.Clean("Cannot find 'spr_hero' and \"obj_a\" in 「room1」; it can't load and isn't ready", "/nowhere"));
        Assert.Equal("Cannot find <name>", CrashScrub.Clean("Cannot find 'hero'", "/nowhere"));
        var path = CrashScrub.Clean("Cannot open '/home/zoe/a.win'", "/nowhere");
        Assert.Equal("Cannot open '/home/<user>/<file>.win'", path);
    }

    [Theory]
    [InlineData("'spr_x'을 찾을 수 없습니다")]
    [InlineData("「spr_x」を削除")]
    [InlineData("删除'spr_x'")]
    [InlineData("«spr_x» no existe")]
    [InlineData("‘spr_x’ failed")]
    [InlineData("spr_x could not be read")]
    public void QuotedAndPrefixedNamesNextToCjkTextOrOtherQuotesAreHidden(string text) =>
        Assert.DoesNotContain("spr_x", CrashScrub.Clean(text, "/nowhere"), StringComparison.Ordinal);

    [Fact]
    public void AnExceptionTextIsKeptWithOnlyItsPersonalPartsReplaced()
    {
        Assert.Equal("Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')", CrashScrub.Exception("Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')", "/nowhere"));
        Assert.Equal("Unable to cast object of type 'System.String' to type 'System.Int32'.", CrashScrub.Exception("Unable to cast object of type 'System.String' to type 'System.Int32'.", "/nowhere"));
        Assert.Equal("<name> could not be read", CrashScrub.Exception("spr_hero could not be read", "/nowhere"));
        Assert.Equal("Could not read the sprite sheet because the page is 4096 wide", CrashScrub.Exception("Could not read the sprite sheet because the page is 4096 wide", "/nowhere"));
        Assert.Equal("", CrashScrub.Exception(null, "/nowhere"));
    }

    [Theory]
    [InlineData("mail ann.lee+x@example.co.uk now", "mail <email> now")]
    [InlineData("peer 192.168.0.12 refused", "peer <ip> refused")]
    [InlineData("peer 2001:db8::1 refused", "peer <ip> refused")]
    [InlineData("version 10.0.26300.1 and Version=1.0.0.0", "version 10.0.26300.1 and Version=1.0.0.0")]
    [InlineData("at 12:30:45 it failed", "at 12:30:45 it failed")]
    [InlineData("send to https://0123abcd@o1.ingest.sentry.io/42 failed", "send to <url> failed")]
    [InlineData("failed, token=abc123def and Password: hunter2", "failed, token=<secret> and Password: <secret>")]
    [InlineData("header Bearer abcdef.ghijkl-123", "header <secret>")]
    [InlineData(@"key HKEY_CURRENT_USER\Software\Vendor\App\Value1 missing", "key <registry> missing")]
    public void PersonalValuesAreReplacedAndLookalikesStay(string text, string expected) => Assert.Equal(expected, CrashScrub.Clean(text, "/nowhere"));

    [Fact]
    public void AKeyOfAnyKindDoesNotSurvive()
    {
        var hex = "0123456789abcdef0123456789abcdef";
        var blob = "QWxhZGRpbjpvcGVuIHNlc2FtZVF1aWNrQnJvd25Gb3hKdW1wcw";
        var text = CrashScrub.Clean($"dsn {hex} and {blob}", "/nowhere");
        Assert.DoesNotContain(hex, text, StringComparison.Ordinal);
        Assert.DoesNotContain(blob, text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheUserNameAndTheMachineNameAreReplacedWhereverTheyStand()
    {
        var text = CrashScrub.Clean($"{Environment.UserName} failed on {Environment.MachineName}", "/nowhere");
        if (Environment.UserName.Length >= 4) Assert.DoesNotContain(Environment.UserName, text, StringComparison.OrdinalIgnoreCase);
        if (Environment.MachineName.Length >= 4) Assert.DoesNotContain(Environment.MachineName, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheTextIsCleanedBeforeItIsCutAndAnOpenQuoteAtTheCutIsAName()
    {
        var path = CrashScrub.Clean(string.Concat(Enumerable.Repeat("aaa ", (CrashScrub.MaxLength - 20) / 4)) + @" C:\Users\FakePerson\game\data.win tail", @"C:\Users\Nobody");
        Assert.DoesNotContain("FakePerson", path, StringComparison.Ordinal);
        var filler = string.Concat(Enumerable.Repeat("word ", (CrashScrub.MaxLength - 12) / 5));
        foreach (var quote in new[] { "'obj_secret_name' is bad", "\"obj_secret_name\" is bad", "「obj_secret_name」 is bad" })
        {
            var cut = CrashScrub.Clean(filler + quote, "/nowhere");
            Assert.True(cut.Length <= CrashScrub.MaxLength + 8);
            Assert.DoesNotContain("obj_", cut, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", cut, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ALongRunOfLettersIsScrubbedFastAndLongQuotedNamesAreHidden()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        CrashScrub.Clean(new string('a', 200_000), "/nowhere");
        Assert.True(watch.ElapsedMilliseconds < 2000);
        var quoted = string.Join(", ", Enumerable.Range(0, 20).Select(i => "nameNumber" + i));
        Assert.DoesNotContain("nameNumber19", CrashScrub.Clean("Missing '" + quoted + "'", "/nowhere"), StringComparison.Ordinal);
    }

    public static TheoryData<string> TrickyPaths()
    {
        var data = new TheoryData<string>();
        string[] roots = [@"C:\Users\FakePerson\Desktop\", @"D:\", @"\\NAS\share\", "C:/Users/FakePerson/Documents/", "/home/fakeperson/", "/Users/fakeperson/Library/Application Support/"];
        string[] names = ["Big Game", "Mike's Game", "O'Brien Co", "Zoë Müller", "日本 語 ゲーム", "a b c d e f g", "Game (v2) final", "Dot.Name v1.2 beta", "Mr. Smith's Cut", "It's Mike's O'Neil's", "semi;comma,name", "Ünï Çödé"];
        string[] files = ["data.win", "x.gmx", "Big Game.gmx", "Mike's Game.v2.gmx", "readme", "ゲーム 設定.json"];
        foreach (var root in roots)
            foreach (var name in names)
                foreach (var file in files)
                    data.Add(root + name + (root.Contains('/', StringComparison.Ordinal) ? "/" : "\\") + file);
        return data;
    }

    // Every personal part of a path must go, whatever spaces, apostrophes, parentheses or sentence text surround it.
    [Theory]
    [MemberData(nameof(TrickyPaths))]
    public void NoPartOfATrickyPathSurvivesInAnyContext(string path)
    {
        var parts = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Skip(1).SelectMany(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        string[] wrappers = ["{0}", "Could not find {0}", "Could not find '{0}'.", "Error ({0})", "{0}: access denied", "{0} was locked", "at X.Y() in {0}:line 12", "読込{0}", "{0} and {0}"];
        foreach (var wrapper in wrappers)
        {
            var clean = CrashScrub.Clean(wrapper.Replace("{0}", path, StringComparison.Ordinal), "/nowhere");
            foreach (var part in parts)
            {
                var piece = part.Trim('\'', '(', ')', ';', ',', '.');
                if (piece.Length < 3 || piece is "Users" or "home" or "Desktop" or "Documents" or "Library" or "Application" or "Support" or "data" or "win" or "gmx" or "json" or "data.win" or "was" or "and") continue;
                Assert.DoesNotContain(piece, clean, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData(@"C:\Users\Ann\Desktop\Big Game.gmx", @"C:\Users\<user>\Desktop\<file>.gmx")]
    [InlineData(@"C:\Users\Ann\Desktop\Mike's Game\data.win", @"C:\Users\<user>\Desktop\<dir>\data.win")]
    [InlineData(@"Cannot open C:\Users\Ann\Desktop\Big Game.gmx and D:\mods\secret.zip", @"Cannot open C:\Users\<user>\Desktop\<file>.gmx and D:\<dir>\<file>.zip")]
    [InlineData(@"at F() in C:\Users\Ann\a b\File.cs:line 12", @"at F() in C:\Users\<user>\<dir>\<file>.cs:line 12")]
    [InlineData(@"(see C:\Users\Ann\a b\File.cs)", @"(see C:\Users\<user>\<dir>\<file>.cs)")]
    [InlineData(@"読込C:\Users\Ann\Desktop\x.gmx", @"読込C:\Users\<user>\Desktop\<file>.gmx")]
    public void SpacesAndApostrophesDoNotEndAPath(string text, string expected) => Assert.Equal(expected, CrashScrub.Text(text, "/nowhere"));

    public static TheoryData<string, string[]> Corpus => new()
    {
        { @"Could not find file 'C:\Users\FakePerson\Documents\My Games\Hero Quest\data.win'.", ["FakePerson", "Hero", "Quest", "My Games"] },
        { @"The process cannot access the file 'D:\Backups\FakePerson-old\hero_final.win' because it is being used by another process.", ["FakePerson", "hero_final", "Backups"] },
        { "Sprite \"spr_boss_idle\" has 0 frames in room \"rm_secret_lab\"", ["boss_idle", "secret_lab"] },
        { "Cannot load texture page for 'obj_player' (script scr_damage_calc, sound snd_boom_01)", ["player", "damage_calc", "boom_01"] },
        { "Send failed for fake.person@example.com via 203.0.113.77 and 2001:db8:85a3::8a2e:370:7334", ["fake.person", "example.com", "203.0.113", "8a2e"] },
        { "POST https://fakeuser:s3cretPass@git.example.org/repo.git failed", ["fakeuser", "s3cretPass", "git.example.org"] },
        { "https://0123456789abcdef0123456789abcdef@o4500.ingest.sentry.io/123 rejected", ["0123456789abcdef", "o4500"] },
        { "Access denied: /home/fakeperson/projects/hero-game/rooms/rm_start.json", ["fakeperson", "hero-game", "rm_start"] },
        { "/Users/fakeperson/Library/Application Support/HeroGame/save1.sav missing", ["fakeperson", "HeroGame", "save1"] },
        { @"\\HOME-NAS\games\fakeperson\hero.win not reachable", ["HOME-NAS", "fakeperson", "hero"] },
        { @"Registry value HKEY_CURRENT_USER\Software\FakeVendor\HeroGame\LicenseKey could not be read", ["FakeVendor", "HeroGame", "LicenseKey"] },
        { "Auth failed: password=hunter22 token: ghp_abcdefghijklmnopqrstuvwxyz0123456789ABCD", ["hunter22", "ghp_abcdef"] },
        { "Font 'Comic Sans FakeFoundry' missing for string \"Welcome back, FakePerson!\"", ["FakeFoundry", "FakePerson", "Welcome"] },
        { "「勇者の剣」の読み込みに失敗: C:\\Users\\FakePerson\\ゲーム\\data.win", ["勇者", "FakePerson", "ゲーム"] },
    };

    [Theory]
    [MemberData(nameof(Corpus))]
    public void NothingPersonalSurvivesARealisticMessage(string text, string[] personal)
    {
        var clean = CrashScrub.Clean(text, @"C:\Users\Nobody");
        foreach (var part in personal) Assert.DoesNotContain(part, clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ALongTextIsCut() => Assert.Equal(CrashScrub.MaxLength, CrashScrub.Clean(string.Concat(Enumerable.Repeat("word ", 1000)), "/nowhere").Length);

    [Fact]
    public void ARealisticMessageKeepsItsDiagnosisAndLosesEveryPersonalPart()
    {
        var text = CrashScrub.Clean(@"Could not read 'spr_hero' from C:\Users\FakePerson\game\data.win: chunk SPRT ends at 4096 past the end (mail fake@example.org, 10.1.2.3)", @"C:\Users\FakePerson");
        Assert.Equal("Could not read <name> from C:\\Users\\<user>\\<dir>\\data.win: chunk SPRT ends at 4096 past the end (mail <email>, <ip>)", text);
    }
}

[Collection(CrashReportingGroup.Name)]
public sealed class CrashReportingTests
{
    [Fact]
    public void ABuildWithoutAKeyStaysSilentWhateverTheSetting()
    {
        Assert.Null(G3MStudio.CrashReporting.Dsn);
        G3MStudio.CrashReporting.Apply(true);
        G3MStudio.CrashReporting.Apply(false);
    }

    [Theory]
    [InlineData("not a dsn")]
    [InlineData("https://abc@example.invalid/1")]
    [InlineData("")]
    public void AKeyThatIsWrongOrUnreachableNeverBreaksTheApp(string key)
    {
        G3MStudio.CrashReporting.Apply(true, key);
        G3MStudio.CrashReporting.Apply(true, key);
        G3MStudio.CrashReporting.Apply(false);
    }

    [Fact]
    public void ReportsAreOnByDefaultAndTheSwitchIsKept()
    {
        Assert.True(new AppSettings().CrashReports);
        Assert.False((new AppSettings() with { CrashReports = false }).CrashReports);
    }
}
