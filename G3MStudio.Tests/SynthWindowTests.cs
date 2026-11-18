using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The sound generator dialog: it previews with the player, makes a new sound or replaces the audio of the selected one as one undoable step, says why it refuses, and fits every language and scale.</summary>
public sealed class SynthWindowTests
{
    private static T Named<T>(Window root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Window root, string name) => Named<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task<SynthWindow> Open(MainWindow window)
    {
        SynthWindow? dialog = null;
        window.SynthOpened += d => dialog = d;
        Assert.True(window.Registry.Execute("edit.generateSound"));
        await AudioPlaybackTests.Until(() => dialog is not null);
        await LayoutAudit.Settle();
        return dialog!;
    }

    [Fact]
    public async Task TheDialogMakesANewSoundThatOneUndoRemovesAndRefusesAnEmptyOrTakenName()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var workspace = window.Workspaces.Current!;
                var existing = workspace.Data.Sounds.Count;
                var dialog = await Open(window);
                dialog.Apply(SoundSynth.Preset(SynthPreset.Coin, 3));
                Assert.Contains(Loc.Current["synth.title"], dialog.Title, StringComparison.Ordinal);
                Assert.False(Named<Button>(dialog, "SynthReplace").IsEnabled); // no sound is selected, and the tip says so
                Assert.Equal(Loc.Current["sounds.noSound"], ToolTip.GetTip(Named<Button>(dialog, "SynthReplace")));

                var name = Named<TextBox>(dialog, "SynthName");
                name.Text = "";
                Click(dialog, "SynthCreate");
                Assert.Equal(Loc.Current["synth.noName"], Named<TextBlock>(dialog, "SynthMessage").Text);
                Assert.Equal(existing, workspace.Data.Sounds.Count);

                name.Text = workspace.Data.Sounds.Count > 0 ? workspace.Data.Sounds[0].Name.Content : "snd_taken";
                if (workspace.Data.Sounds.Count > 0)
                {
                    Click(dialog, "SynthCreate");
                    Assert.False(string.IsNullOrEmpty(Named<TextBlock>(dialog, "SynthMessage").Text)); // the name is taken: the reason stays in the dialog
                    Assert.Equal(existing, workspace.Data.Sounds.Count);
                }

                name.Text = "snd_generated_test";
                Click(dialog, "SynthCreate");
                Assert.False(dialog.IsVisible); // done: the dialog closed
                var sound = workspace.Data.Sounds.Single(s => s.Name.Content == "snd_generated_test");
                Assert.Equal(existing + 1, workspace.Data.Sounds.Count);
                Assert.Equal(SoundSynth.ToWav(SoundSynth.Preset(SynthPreset.Coin, 3)), sound.AudioFile!.Data);
                var format = WavEdit.Read(sound.AudioFile.Data);
                Assert.Equal((1, 44100, 16), (format.Channels, format.SampleRate, format.Bits));
                Assert.Equal(SoundSynth.Preset(SynthPreset.Coin).Duration, sound.AudioLength, 2);
                Assert.Equal(ResourceTypes.KeyOf("sounds", "snd_generated_test"), window.Explorer.SelectedKey); // it is selected and opened

                workspace.History.Undo(); // the sound and its audio are one step
                Assert.DoesNotContain(workspace.Data.Sounds, s => s.Name.Content == "snd_generated_test");
                Assert.Equal(existing, workspace.Data.Sounds.Count);
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheDialogHasTheWholePlayerOfTheSoundTabAndOnlyTheRegionIsKept()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var workspace = window.Workspaces.Current!;
                var dialog = await Open(window);
                Assert.All<string>(["SynthPlay", "AudioStop", "AudioPlayRegion", "AudioClearRegion", "AudioLoop", "ZoomIn", "ZoomOut", "ZoomFit", "AudioVisualizerShow", "AudioSeek", "AudioVolume", "SynthRegionStart", "SynthRegionEnd"], name => Assert.True(dialog.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name), name));
                Assert.NotNull(Named<TextBlock>(dialog, "AudioRegionHint"));
                var (start, end) = (Named<NumericUpDown>(dialog, "SynthRegionStart"), Named<NumericUpDown>(dialog, "SynthRegionEnd"));
                Assert.Equal(Loc.Current["audio.saveRegionHint"], Named<TextBlock>(dialog, "SynthSaveHint").Text); // next to the buttons, outside the scrolled body

                dialog.Apply(SoundSynth.Preset(SynthPreset.Coin, 3));
                var whole = SoundSynth.ToWav(SoundSynth.Preset(SynthPreset.Coin, 3));
                (start.Value, end.Value) = (0.1m, 0.2m);
                Assert.Equal((0.1, 0.2), dialog.Player.Region);
                Named<TextBox>(dialog, "SynthName").Text = "snd_region";
                Click(dialog, "SynthCreate");
                var sound = workspace.Data.Sounds.Single(s => s.Name.Content == "snd_region");
                Assert.Equal(4410, WavEdit.Read(sound.AudioFile!.Data).Frames); // a tenth of a second, not the whole sound
                Assert.True(WavEdit.Read(whole).Frames > 4410);

                var again = await Open(window);
                again.Apply(SoundSynth.Preset(SynthPreset.Coin, 3));
                Named<TextBox>(again, "SynthName").Text = "snd_whole";
                Click(again, "SynthCreate");
                Assert.Equal(whole, workspace.Data.Sounds.Single(s => s.Name.Content == "snd_whole").AudioFile!.Data); // no region: all of it
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheDialogReplacesTheAudioOfTheSelectedSoundInOneUndoStepAndPreviewsThroughThePlayer()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var output = AudioPlaybackTests.UseNullDevice();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var workspace = window.Workspaces.Current!;
                var first = await Open(window);
                first.Apply(SoundSynth.Preset(SynthPreset.Hit));
                Named<TextBox>(first, "SynthName").Text = "snd_to_replace";
                Click(first, "SynthCreate");
                var sound = workspace.Data.Sounds.Single(s => s.Name.Content == "snd_to_replace");
                var before = sound.AudioFile!.Data;

                var count = workspace.Data.Sounds.Count;
                var dialog = await Open(window); // the new sound is selected now
                var replace = Named<Button>(dialog, "SynthReplace");
                Assert.True(replace.IsEnabled);
                Assert.Contains("snd_to_replace", replace.Content as string, StringComparison.Ordinal);
                dialog.Apply(SoundSynth.Preset(SynthPreset.Laser));
                Assert.Contains("0:00.2", dialog.Transport.TimeText, StringComparison.Ordinal); // the 0.25 s of the laser
                await dialog.Transport.PlayPauseAsync(); // the live preview plays the generated clip through the player and the device layer
                Assert.True(output.IsPlaying);
                output.Advance(0.1);
                dialog.Transport.Tick();
                Assert.InRange(dialog.Transport.Position, 0.09, 0.11);
                dialog.Apply(SoundSynth.Preset(SynthPreset.Coin)); // a change stops the preview and the player shows the new length
                Assert.False(output.IsPlaying);
                Assert.Contains("0:00.3", dialog.Transport.TimeText, StringComparison.Ordinal);

                Click(dialog, "SynthReplace");
                await AudioPlaybackTests.Until(() => !dialog.IsVisible);
                Assert.Equal(SoundSynth.ToWav(SoundSynth.Preset(SynthPreset.Coin)), sound.AudioFile.Data);
                Assert.Equal(count, workspace.Data.Sounds.Count); // no sound was added
                workspace.History.Undo();
                Assert.Equal(before, sound.AudioFile.Data); // one step back
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task ReplaceStartsOneEditWhileItWorksAndWorksAgainAfterARefusal()
    {
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            var results = new List<TaskCompletionSource<bool>>();
            var dialog = new SynthWindow(null, _ =>
            {
                var result = new TaskCompletionSource<bool>();
                results.Add(result);
                return result.Task;
            }, "snd", () => new G3MStudio.Core.Settings.AudioOptions());
            dialog.Show();
            try
            {
                Click(dialog, "SynthReplace");
                Click(dialog, "SynthReplace"); // a second click while the first edit works
                Assert.Single(results);
                results[0].SetResult(false); // refused: the dialog stays and the button works again
                await AudioPlaybackTests.Until(() => { Click(dialog, "SynthReplace"); return results.Count == 2; }); // the refusal is handled on the next turn of the UI thread
                Assert.Equal(2, results.Count);
                results[1].SetResult(true);
                await AudioPlaybackTests.Until(() => !dialog.IsVisible);
            }
            finally
            {
                dialog.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task ARunOfChangesMakesTheSoundOnceAndCreateOrPlayAfterAChangeUsesTheLatestValues()
    {
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            byte[]? created = null;
            var dialog = new SynthWindow((_, wav) =>
            {
                created = wav;
                return null;
            }, null, null, () => new G3MStudio.Core.Settings.AudioOptions());
            dialog.Show();
            try
            {
                var frequency = Named<NumericUpDown>(dialog, "SynthFrequency");
                var start = dialog.Renders;
                foreach (var value in (decimal[])[500m, 600m, 700m]) frequency.Value = value;
                Assert.Equal(start, dialog.Renders); // nothing is made while the values still change
                await AudioPlaybackTests.Until(() => dialog.Renders > start);
                await Task.Delay(150);
                Assert.Equal(start + 1, dialog.Renders); // one sound for the whole run

                frequency.Value = 900;
                await dialog.Transport.PlayPauseAsync(); // playing finishes the change first
                Assert.Equal(start + 2, dialog.Renders);
                frequency.Value = 1000;
                Click(dialog, "SynthCreate"); // so does create, before the timer ran
                Assert.Equal(start + 3, dialog.Renders);
                Assert.Equal(1000, dialog.Current().Frequency);
                Assert.Equal(SoundSynth.ToWav(dialog.Current()), created);
            }
            finally
            {
                dialog.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task AnUnusualNameWarnedAboutInTheGeneratorIsForgottenWhenTheDialogEnds()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var workspace = window.Workspaces.Current!;
                var before = workspace.Data.Sounds.Count;
                for (var round = 0; round < 2; round++)
                {
                    var dialog = await Open(window);
                    Named<TextBox>(dialog, "SynthName").Text = "my sound";
                    Click(dialog, "SynthCreate");
                    Assert.False(string.IsNullOrEmpty(Named<TextBlock>(dialog, "SynthMessage").Text)); // the warning, again in the second dialog
                    Assert.Equal(before, workspace.Data.Sounds.Count);
                    Click(dialog, "SynthCancel");
                    await AudioPlaybackTests.Until(() => !dialog.IsVisible);
                    await LayoutAudit.Settle();
                }
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task EveryValueReachesTheSettingsThePresetsAndRandomFillThemAndTheDutyIsOnlyForTheSquare()
    {
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            var dialog = new SynthWindow((_, _) => null, _ => Task.FromResult(true), "snd", () => new G3MStudio.Core.Settings.AudioOptions());
            dialog.Show();
            try
            {
                foreach (var preset in Enum.GetValues<SynthPreset>())
                {
                    Click(dialog, "SynthPreset" + preset);
                    var set = dialog.Current();
                    var wanted = SoundSynth.Preset(preset, set.Seed);
                    Assert.Equal((wanted.Shape, wanted.Frequency, wanted.Slide, wanted.Duration), (set.Shape, Math.Round(set.Frequency), set.Slide, set.Duration));
                    Assert.Equal(wanted.Duty, set.Duty, 2);
                    Assert.Equal(wanted.Volume, set.Volume, 2);
                }

                dialog.Apply(SoundSynth.Random(1234));
                var random = dialog.Current();
                Assert.Equal(1234, random.Seed);
                Assert.Equal(SoundSynth.Random(1234).Shape, random.Shape);
                Assert.Equal(SoundSynth.Random(1234).Frequency, random.Frequency, 0);
                Click(dialog, "SynthRandom");
                Assert.NotEqual(1234, dialog.Current().Seed); // a new seed, shown in its box

                Named<NumericUpDown>(dialog, "SynthDuration").Value = 1.5m;
                Assert.Equal(1.5, dialog.Current().Duration, 3);
                await AudioPlaybackTests.Until(() => dialog.Transport.TimeText.Contains("0:01.5", StringComparison.Ordinal)); // the waveform and the player follow a moment later
                var duty = Named<NumericUpDown>(dialog, "SynthDuty");
                Named<ComboBox>(dialog, "SynthShape").SelectedIndex = 0; // sine
                await AudioPlaybackTests.Until(() => !duty.IsEnabled);
                Assert.False(string.IsNullOrEmpty(ToolTip.GetTip(duty) as string));
                Named<ComboBox>(dialog, "SynthShape").SelectedIndex = 1;
                await AudioPlaybackTests.Until(() => duty.IsEnabled);
            }
            finally
            {
                dialog.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheAudioEditorHasAButtonThatOpensTheGeneratorForReplacingItsSound()
    {
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            SynthWindow? opened = null;
            var editor = new AudioEditorWindow("Edit audio", ScreenshotTests.ToneWav(), _ => { }, null, null, _ => opened = new SynthWindow(null, _ => Task.FromResult(true), "snd", () => new G3MStudio.Core.Settings.AudioOptions()));
            editor.Show();
            try
            {
                await LayoutAudit.Settle();
                Click(editor, "AudioGenerate");
                Assert.NotNull(opened);
                opened.Show();
                Assert.DoesNotContain(opened.GetVisualDescendants().OfType<TextBox>(), t => t.Name == "SynthName"); // only a replace from there: no name to ask for
                Assert.True(Named<Button>(opened, "SynthReplace").IsEnabled);
                Assert.True(Named<Button>(opened, "SynthReplace").IsDefault);
                opened.Close();
            }
            finally
            {
                editor.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheEditorButtonOpensTheGeneratorOnItsSoundAndReplacingClosesTheEditor()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(Corpus.Files(Corpus.Fixtures).First());
                var workspace = window.Workspaces.Current!;
                var maker = await Open(window);
                Named<TextBox>(maker, "SynthName").Text = "snd_in_editor";
                Click(maker, "SynthCreate");
                var sound = workspace.Data.Sounds.Single(s => s.Name.Content == "snd_in_editor");
                var before = sound.AudioFile!.Data;

                AudioEditorWindow? editor = null;
                window.AudioEditorOpened += w => editor = w;
                Assert.True(window.Registry.Execute("edit.editAudio"));
                await AudioPlaybackTests.Until(() => editor is not null);
                SynthWindow? dialog = null;
                window.SynthOpened += d => dialog = d;
                Click(editor!, "AudioGenerate");
                await AudioPlaybackTests.Until(() => dialog is not null);
                await LayoutAudit.Settle();
                var generator = dialog!;
                Assert.DoesNotContain(generator.GetVisualDescendants().OfType<TextBox>(), t => t.Name == "SynthName"); // from the editor it only replaces
                generator.Apply(SoundSynth.Preset(SynthPreset.PowerUp));
                Click(generator, "SynthReplace");
                await AudioPlaybackTests.Until(() => !editor!.IsVisible);
                Assert.Equal(SoundSynth.ToWav(SoundSynth.Preset(SynthPreset.PowerUp)), sound.AudioFile.Data);
                workspace.History.Undo();
                Assert.Equal(before, sound.AudioFile.Data);
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
    }

    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public async Task TheDialogFitsAtEveryScaleInEveryLanguageAtItsOwnSizeAndSqueezed(int scale)
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var owner = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                owner.Workspaces.Settings.Update(x => x with { UiScale = scale });
                foreach (var language in LanguageLayoutAuditTests.Languages.Select(row => row.Data))
                {
                    Loc.Current.SetLanguage(language);
                    AudioPlaybackTests.UseNullDevice();
                    var dialog = new SynthWindow((_, _) => null, _ => Task.FromResult(true), "a_very_long_sound_name_" + language, () => new G3MStudio.Core.Settings.AudioOptions());
                    dialog.Show(owner);
                    await LayoutAudit.Settle();
                    problems.AddRange(DialogLayoutTests.Audit(dialog).Concat(LayoutAudit.CutText(dialog)).Select(p => $"{scale}% {language}: {p}"));
                    dialog.MaxHeight = Math.Max(dialog.MinHeight, 220 * scale / 100.0); // the smallest a window that sizes itself can get
                    await LayoutAudit.Settle();
                    problems.AddRange(DialogLayoutTests.Audit(dialog).Concat(LayoutAudit.CutText(dialog)).Select(p => $"{scale}% {language} (smallest): {p}"));
                    dialog.Close();
                    await LayoutAudit.Settle();
                }
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                owner.Close();
                G3MStudio.Audio.AudioDevices.Shutdown();
            }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }
}
