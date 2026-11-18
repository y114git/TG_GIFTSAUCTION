using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using G3MStudio.Audio;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>Where the recorder is reached (command, Sound menu, audio editor) and what it does to the data: a new sound or the audio of a sound, each one undo step, with the microphone chosen remembered in the settings.</summary>
public sealed class RecorderEntryTests
{
    private static T Named<T>(Window root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Window root, string name) => Named<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task<RecorderWindow> Open(MainWindow window)
    {
        RecorderWindow? dialog = null;
        window.RecorderOpened += d => dialog = d;
        Assert.True(window.Registry.Execute("edit.recordSound"));
        await AudioPlaybackTests.Until(() => dialog is not null);
        await dialog!.Listing;
        await LayoutAudit.Settle();
        return dialog;
    }

    private static async Task Record(RecorderWindow dialog, FakeCaptureSource source, double seconds)
    {
        Click(dialog, "RecordStart");
        await AudioPlaybackTests.Until(() => dialog.IsRecording);
        source.Device!.Tone(seconds);
        var frames = (int)Math.Round(seconds * 44100);
        await AudioPlaybackTests.Until(() => dialog.Take.Frames == frames);
        Click(dialog, "RecordStop");
        await AudioPlaybackTests.Until(() => dialog.HasTake);
    }

    private static async Task Finish(MainWindow window)
    {
        foreach (var open in window.OwnedWindows.ToList()) open.Close();
        window.Close();
        AudioDevices.Shutdown();
        AudioCaptureDevices.Shutdown();
        await LayoutAudit.Settle();
    }

    [Fact]
    public void TheCommandIsInTheSoundMenuNextToTheGeneratorWithoutAShortcut()
    {
        var sound = AppCommands.Menus.Single(m => m.TitleKey == "menu.sound").Ids.ToList();
        Assert.Equal(sound.IndexOf("edit.generateSound") + 1, sound.IndexOf("edit.recordSound"));
        Assert.Equal("Record Sound...", Loc.Current["cmd.edit.recordSound"]);
    }

    [Fact]
    public async Task TheCommandMakesANewSoundOfTheTakeThatOneUndoRemovesAndTheMicrophoneChosenIsRemembered()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var source = RecorderWindowTests.Mics("Desk Mic", "Headset");
            AudioPlaybackTests.UseNullDevice();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var workspace = window.Workspaces.Current!;
                var existing = workspace.Data.Sounds.Count;
                var dialog = await Open(window);
                Assert.Contains(Loc.Current["record.title"], dialog.Title, StringComparison.Ordinal);
                var changes = 0;
                window.Workspaces.Settings.Changed += () => changes++;
                Named<ComboBox>(dialog, "RecordDevice").SelectedIndex = 1;
                Assert.Equal("Headset", window.Workspaces.Settings.Current.Audio.CaptureDevice); // chosen in the dialog, kept in the settings
                Assert.Equal(0, changes); // and nothing re-applies the settings for it
                Assert.False(Named<Button>(dialog, "RecordReplace").IsEnabled);
                Assert.Equal(Loc.Current["record.takeFirst"], ToolTip.GetTip(Named<Button>(dialog, "RecordReplace")));

                await Record(dialog, source, 0.5);
                Assert.False(Named<Button>(dialog, "RecordReplace").IsEnabled); // no sound is selected, and the tip says so
                Assert.Equal(Loc.Current["sounds.noSound"], ToolTip.GetTip(Named<Button>(dialog, "RecordReplace")));
                Assert.Equal(["Headset"], source.Opened); // the remembered microphone is the one opened
                var name = Named<TextBox>(dialog, "RecordName");
                name.Text = "";
                Click(dialog, "RecordCreate");
                Assert.Equal(Loc.Current["synth.noName"], Named<TextBlock>(dialog, "RecordMessage").Text);
                name.Text = "snd_recorded_test";
                Click(dialog, "RecordCreate");
                Click(dialog, "RecordCreate"); // a second click while the dialog closes does nothing more
                await AudioPlaybackTests.Until(() => !dialog.IsVisible);
                var sound = workspace.Data.Sounds.Single(s => s.Name.Content == "snd_recorded_test");
                Assert.Equal(existing + 1, workspace.Data.Sounds.Count);
                var format = WavEdit.Read(sound.AudioFile!.Data);
                Assert.Equal((1, 44100, 16, 22050), (format.Channels, format.SampleRate, format.Bits, format.Frames));
                Assert.Equal(0.5, sound.AudioLength, 2);
                Assert.Equal(ResourceTypes.KeyOf("sounds", "snd_recorded_test"), window.Explorer.SelectedKey); // selected and opened like a generated sound

                workspace.History.Undo(); // the sound and its audio are one step
                Assert.DoesNotContain(workspace.Data.Sounds, s => s.Name.Content == "snd_recorded_test");
                Assert.Equal(existing, workspace.Data.Sounds.Count);
                await AudioPlaybackTests.Until(() => source.Device!.Disposed);
            }
            finally
            {
                await Finish(window);
            }
        });
    }

    [Fact]
    public async Task AnOpenSoundTabShowsTheNewAudioAfterTheRecorderReplacedItAndAfterUndo()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var source = RecorderWindowTests.Mics("Mic");
            AudioPlaybackTests.UseNullDevice();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var workspace = window.Workspaces.Current!;
                var first = await Open(window);
                await Record(first, source, 0.25);
                Named<TextBox>(first, "RecordName").Text = "snd_shown";
                Click(first, "RecordCreate");
                await AudioPlaybackTests.Until(() => !first.IsVisible);
                var tab = window.Docking.OpenTab("resource", "sounds:snd_shown", false)!;
                await LayoutAudit.Settle();
                string Shown() => window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "SoundStatus") is TextBlock text ? text.Text ?? "" : "";
                Assert.Contains("0.2 s", Shown(), StringComparison.Ordinal);

                var dialog = await Open(window);
                await Record(dialog, source, 0.75);
                Click(dialog, "RecordReplace");
                await AudioPlaybackTests.Until(() => !dialog.IsVisible);
                await AudioPlaybackTests.Until(() => Shown().Contains("0.8 s", StringComparison.Ordinal));
                workspace.History.Undo();
                await AudioPlaybackTests.Until(() => Shown().Contains("0.2 s", StringComparison.Ordinal));
            }
            finally
            {
                await Finish(window);
            }
        });
    }

    [Fact]
    public async Task TheDialogReplacesTheAudioOfTheSelectedSoundInOneUndoStep()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var source = RecorderWindowTests.Mics("Mic");
            AudioPlaybackTests.UseNullDevice();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var workspace = window.Workspaces.Current!;
                var first = await Open(window);
                await Record(first, source, 0.25);
                Named<TextBox>(first, "RecordName").Text = "snd_to_replace";
                Click(first, "RecordCreate");
                await AudioPlaybackTests.Until(() => !first.IsVisible);
                var sound = workspace.Data.Sounds.Single(s => s.Name.Content == "snd_to_replace");
                var before = sound.AudioFile!.Data;

                var count = workspace.Data.Sounds.Count;
                var dialog = await Open(window); // the new sound is selected now
                var replace = Named<Button>(dialog, "RecordReplace");
                Assert.False(replace.IsEnabled); // nothing recorded yet
                await Record(dialog, source, 0.75);
                Assert.True(replace.IsEnabled);
                Assert.Contains("snd_to_replace", replace.Content as string, StringComparison.Ordinal);
                Click(dialog, "RecordReplace");
                Click(dialog, "RecordReplace"); // one edit for a double click
                await AudioPlaybackTests.Until(() => !dialog.IsVisible);
                Assert.Equal(count, workspace.Data.Sounds.Count);
                Assert.Equal(0.75, sound.AudioLength, 2);
                Assert.NotEqual(before, sound.AudioFile.Data);
                workspace.History.Undo(); // one step back, not two
                Assert.Equal(before, sound.AudioFile.Data);
                Assert.Equal(0.25, sound.AudioLength, 2);
            }
            finally
            {
                await Finish(window);
            }
        });
    }

    [Fact]
    public async Task TheAudioEditorHasARecordButtonThatOpensTheRecorderOnItsSoundAndReplacingClosesTheEditor()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var source = RecorderWindowTests.Mics("Mic");
            AudioPlaybackTests.UseNullDevice();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var workspace = window.Workspaces.Current!;
                var maker = await Open(window);
                await Record(maker, source, 0.25);
                Named<TextBox>(maker, "RecordName").Text = "snd_in_editor";
                Click(maker, "RecordCreate");
                await AudioPlaybackTests.Until(() => !maker.IsVisible);
                var sound = workspace.Data.Sounds.Single(s => s.Name.Content == "snd_in_editor");
                var before = sound.AudioFile!.Data;

                AudioEditorWindow? editor = null;
                window.AudioEditorOpened += w => editor = w;
                Assert.True(window.Registry.Execute("edit.editAudio"));
                await AudioPlaybackTests.Until(() => editor is not null);
                RecorderWindow? dialog = null;
                window.RecorderOpened += d => dialog = d;
                var button = Named<Button>(editor!, "AudioRecord");
                Assert.Equal(Loc.Current["record.editorTip"], ToolTip.GetTip(button));
                Click(editor!, "AudioRecord");
                await AudioPlaybackTests.Until(() => dialog is not null);
                await dialog!.Listing;
                await LayoutAudit.Settle();
                Assert.DoesNotContain(dialog.GetVisualDescendants().OfType<TextBox>(), t => t.Name == "RecordName"); // from the editor it only replaces
                await Record(dialog, source, 0.5);
                Click(dialog, "RecordReplace");
                await AudioPlaybackTests.Until(() => !editor!.IsVisible);
                Assert.Equal(0.5, sound.AudioLength, 2);
                workspace.History.Undo();
                Assert.Equal(before, sound.AudioFile.Data);
            }
            finally
            {
                await Finish(window);
            }
        });
    }
}
