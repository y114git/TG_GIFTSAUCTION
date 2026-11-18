using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Audio;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>The FFT, the level meter and the spectrum of the visualizer, and the control that shows them: it follows the playing position, falls to zero after a stop or pause and does no work while hidden.</summary>
public sealed class AudioVisualizerTests
{
    private const int Rate = 44100;

    private static float[] Sine(double hertz, double amplitude, int frames, int rate = Rate) => [.. Enumerable.Range(0, frames).Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * hertz * i / rate)))];

    [Fact]
    public void ASineOnABinCentreHasItsPeakInThatBinAndMatchesTheNaiveTransform()
    {
        const int n = 1024, bin = 64;
        var re = Sine((double)bin * Rate / n, 1, n);
        var im = new float[n];
        var copy = (float[])re.Clone();
        Fft.Transform(re, im);
        var magnitude = re.Zip(im, (r, i) => Math.Sqrt(r * r + i * i)).Take(n / 2).ToArray();
        Assert.Equal(bin, Array.IndexOf(magnitude, magnitude.Max()));
        Assert.Equal(n / 2.0, magnitude[bin], 1); // an amplitude 1 sine gives n / 2
        Assert.True(magnitude.Where((_, k) => k != bin).All(m => m < 1e-2));

        const int small = 16; // against the definition
        var (real, imaginary) = (Sine(3.3, 1, small, 16).Zip(Sine(1.1, 0.5, small, 16), (a, b) => a + b).ToArray(), new float[small]);
        var input = (float[])real.Clone();
        Fft.Transform(real, imaginary);
        for (var k = 0; k < small; k++)
        {
            double sumRe = 0, sumIm = 0;
            for (var t = 0; t < small; t++)
            {
                sumRe += input[t] * Math.Cos(2 * Math.PI * k * t / small);
                sumIm -= input[t] * Math.Sin(2 * Math.PI * k * t / small);
            }

            Assert.Equal(sumRe, real[k], 3);
            Assert.Equal(sumIm, imaginary[k], 3);
        }

        Assert.NotEqual(copy, re); // the transform works in place
        Assert.Throws<ArgumentException>(() => Fft.Transform(new float[100], new float[100]));
    }

    [Fact]
    public void TheHannWindowIsZeroAtTheEndsAndOneInTheMiddle()
    {
        var window = Fft.Hann(1025);
        Assert.Equal(0, window[0], 6);
        Assert.Equal(0, window[^1], 6);
        Assert.Equal(1, window[512], 6);
    }

    [Fact]
    public void TheLevelMeterGivesPeakAndRmsPerChannelAndSurvivesBadSamples()
    {
        var tone = Sine(440, 0.5, Rate);
        var mono = AudioAnalysis.Levels(new PcmAudio(tone, 1, Rate), 0.5);
        Assert.Equal(0.5, mono[0].Peak, 2);
        Assert.Equal(0.5 / Math.Sqrt(2), mono[0].Rms, 2); // a sine: RMS is the peak over the root of two
        Assert.Equal(mono[0], mono[1]); // a mono clip fills both

        var stereo = new float[Rate * 2];
        for (var i = 0; i < Rate; i++) stereo[i * 2] = tone[i];
        var split = AudioAnalysis.Levels(new PcmAudio(stereo, 2, Rate), 0.5);
        Assert.Equal(0.5, split[0].Peak, 2);
        Assert.Equal((0f, 0f), split[1]);

        var bad = AudioAnalysis.Levels(new PcmAudio([float.NaN, float.PositiveInfinity, 9f, -9f], 1, Rate), 0, 1);
        Assert.All(bad, l => Assert.True(float.IsFinite(l.Peak) && float.IsFinite(l.Rms) && l.Peak <= 1));
        Assert.Equal((0f, 0f), AudioAnalysis.Levels(new PcmAudio([], 1, Rate), 3)[0]); // no audio, any position
        Assert.Equal((0f, 0f), AudioAnalysis.Levels(new PcmAudio(tone, 1, Rate), 99)[0]); // past the end is silence
        Assert.Equal(0, AudioAnalysis.Unit(0));
        Assert.Equal(1, AudioAnalysis.Unit(1), 6);
        Assert.Equal(0.5, AudioAnalysis.Unit(Math.Pow(10, AudioAnalysis.FloorDb / 40)), 6); // half way down the scale in decibels
    }

    [Fact]
    public void ASineShowsItsPeakInTheBinAndTheLogarithmicBarOfItsFrequency()
    {
        var bin = 46; // a bin centre of the 2048 point transform, 990 Hz
        var hertz = (double)bin * Rate / AudioAnalysis.FftSize;
        var clip = new PcmAudio(Sine(hertz, 0.5, Rate), 1, Rate);
        var bins = AudioAnalysis.Bins(clip, 0.5);
        Assert.Equal(bin, Array.IndexOf(bins, bins.Max()));
        Assert.Equal(0.5, bins[bin], 2); // a full scale sine is 1

        foreach (var (frequency, bars) in new[] { (hertz, 32), (200.0, 32), (4000.0, 24), (12000.0, 48) })
        {
            var spectrum = AudioAnalysis.Spectrum(new PcmAudio(Sine(frequency, 0.5, Rate), 1, Rate), 0.5, bars);
            var expected = (int)(Math.Log(frequency / 40) / Math.Log(16000 / 40.0) * bars);
            Assert.InRange(Array.IndexOf(spectrum, spectrum.Max()), expected - 1, expected + 1);
            Assert.All(spectrum, v => Assert.InRange(v, 0f, 1f));
        }

        Assert.All(AudioAnalysis.Spectrum(new PcmAudio(new float[Rate], 1, Rate), 0.5, 32), v => Assert.Equal(0f, v)); // silence is flat
        Assert.All(AudioAnalysis.Spectrum(new PcmAudio([float.NaN, 1e30f, -1e30f], 1, Rate), 0, 16), v => Assert.True(float.IsFinite(v)));
        Assert.Empty(AudioAnalysis.Spectrum(new PcmAudio(Sine(440, 1, 100), 1, Rate), 0, 0));
        Assert.All(AudioAnalysis.Spectrum(new PcmAudio(Sine(440, 1, 100), 1, Rate), -5, 16), v => Assert.True(float.IsFinite(v))); // before the start, and a clip shorter than the transform
    }

    [Fact]
    public void TheDisplayJumpsUpFallsToExactlyZeroAndNeverGoesNaN()
    {
        var state = new VisualizerState();
        state.Update(0.03, 8, [(1f, 0.5f), (0.2f, 0.1f)], [.. Enumerable.Repeat(0.9f, 8)]);
        Assert.Equal(0.9f, state.Bars[0]);
        Assert.False(state.Settled);

        var before = state.Bars[0];
        state.Update(0.1, 8, null, null);
        Assert.InRange(state.Bars[0], 0.001f, before - 0.1f); // falls, but not at once
        for (var i = 0; i < 40 && !state.Settled; i++) state.Update(0.05, 8, null, null);
        Assert.True(state.Settled);
        Assert.All(state.Bars.Concat(state.Peak).Concat(state.Rms), v => Assert.Equal(0f, v)); // exactly zero: nothing stays lit

        state.Update(double.NaN, 8, [(float.NaN, float.PositiveInfinity), (-3f, 7f)], [float.NaN, 5f, -5f]);
        Assert.All(state.Bars.Concat(state.Peak).Concat(state.Rms), v => Assert.True(float.IsFinite(v) && v is >= 0 and <= 1));
        state.Update(1e9, 3, null, null); // a huge step is limited; a new bar count restarts the bars
        Assert.Equal(3, state.Bars.Length);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The transport lives as long as the test window.")]
    private static (Window Window, AudioTransport Transport, NullAudioOutput Output) Open(AudioOptions? options = null, double width = 700)
    {
        var output = AudioPlaybackTests.UseNullDevice();
        var transport = new AudioTransport(1, () => new PcmAudio(Sine(1000, 0.5, 8000, 8000), 1, 8000), () => options ?? new AudioOptions());
        var window = new Window { Width = width, Height = 700, Content = transport };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, transport, output);
    }

    [Fact]
    public async Task ThePlayerFeedsTheVisualizerFromThePlayingPositionAndItFallsToZeroAfterStopAndPause()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, transport, output) = Open();
            try
            {
                var viz = transport.Visualizer;
                Assert.True(viz.IsVisible);
                Assert.True(viz.Settled);
                await transport.PlayPauseAsync();
                output.Advance(0.2);
                transport.Tick();
                Assert.False(viz.Settled);
                Assert.True(viz.State.Peak[0] > 0.5f && viz.State.Rms[0] > 0.3f);
                Assert.True(viz.State.Bars.Max() > 0.6f); // 1000 Hz is in the spectrum

                await transport.PlayPauseAsync(); // pause: the bars sink
                for (var i = 0; i < 60 && !viz.Settled; i++)
                {
                    await Task.Delay(30);
                    transport.Tick();
                }

                Assert.True(viz.Settled);
                await transport.PlayPauseAsync();
                output.Advance(0.1);
                transport.Tick();
                Assert.False(viz.Settled);
                transport.Stop(); // stop: the timer runs on until nothing is lit
                for (var i = 0; i < 60 && !viz.Settled; i++)
                {
                    await Task.Delay(30);
                    transport.Tick();
                }

                Assert.True(viz.Settled);
                Assert.All(viz.State.Bars, v => Assert.Equal(0f, v));
            }
            finally
            {
                transport.Close();
                window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheTimerFollowsThePlaybackStateAndRunsAgainOnResume()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, transport, output) = Open();
            try
            {
                var viz = transport.Visualizer;
                await transport.PlayPauseAsync();
                output.Advance(0.2);
                transport.Tick();
                Assert.True(transport.Ticking);
                await transport.PlayPauseAsync(); // pause: the bars sink, and the timer stops once they have
                Assert.True(transport.Ticking);
                for (var i = 0; i < 60 && !viz.Settled; i++)
                {
                    await Task.Delay(30);
                    transport.Tick();
                }

                Assert.True(viz.Settled);
                Assert.False(transport.Ticking); // paused and nothing lit: nothing to update
                await transport.PlayPauseAsync(); // resume: the timer runs again
                Assert.True(transport.Ticking);
                Assert.True(transport.IsPlaying);

                AudioPlaybackTests.Find<CheckBox>(transport, "AudioVisualizerShow").IsChecked = false;
                transport.Tick();
                Assert.True(transport.Ticking); // playing: the cursor still moves
                await transport.PlayPauseAsync();
                transport.Tick();
                Assert.False(transport.Ticking); // paused with the visualizer hidden
            }
            finally
            {
                transport.Close();
                window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task ShowingTheVisualizerAgainAfterAStopLetsTheLitBarsFall()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, transport, output) = Open();
            try
            {
                var toggle = AudioPlaybackTests.Find<CheckBox>(transport, "AudioVisualizerShow");
                await transport.PlayPauseAsync();
                output.Advance(0.2);
                transport.Tick();
                Assert.False(transport.Visualizer.Settled);
                toggle.IsChecked = false;
                transport.Stop();
                transport.Tick();
                Assert.False(transport.Ticking); // hidden and stopped: the timer rests with the bars still lit
                toggle.IsChecked = true;
                Assert.True(transport.Ticking);
                for (var i = 0; i < 60 && !transport.Visualizer.Settled; i++)
                {
                    await Task.Delay(30);
                    transport.Tick();
                }

                Assert.True(transport.Visualizer.Settled);
                Assert.False(transport.Ticking);
            }
            finally
            {
                transport.Close();
                window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task AHiddenVisualizerDoesNoWorkAndItsToggleIsRemembered()
    {
        await HeadlessApp.Run(async () =>
        {
            var remembered = new List<bool>();
            AudioTransport.RememberVisualizer = remembered.Add;
            var (window, transport, output) = Open();
            try
            {
                var toggle = AudioPlaybackTests.Find<CheckBox>(transport, "AudioVisualizerShow");
                Assert.True(toggle.IsChecked);
                toggle.IsChecked = false;
                Assert.False(transport.Visualizer.IsVisible);
                Assert.Equal([false], remembered);
                await transport.PlayPauseAsync();
                output.Advance(0.2);
                transport.Tick();
                Assert.True(transport.Visualizer.Settled); // nothing was analysed
                toggle.IsChecked = true;
                Assert.Equal([false, true], remembered);

                // A tab that is not in front is not visible either: the values fall and nothing is drawn.
                transport.Tick();
                Assert.False(transport.Visualizer.Settled);
                window.Content = null;
                var away = new StackPanel { IsVisible = false, Children = { transport } };
                window.Content = away;
                transport.Visualizer.Step(new PcmAudio(Sine(1000, 0.5, 8000, 8000), 1, 8000), 0.1, true, 0.05);
                Assert.False(transport.Visualizer.IsEffectivelyVisible);
                for (var i = 0; i < 30; i++) transport.Visualizer.Step(new PcmAudio(Sine(1000, 0.5, 8000, 8000), 1, 8000), 0.1, true, 0.05);
                Assert.True(transport.Visualizer.Settled);
            }
            finally
            {
                AudioTransport.RememberVisualizer = null;
                transport.Close();
                window.Close();
                AudioDevices.Shutdown();
            }
        });

        await HeadlessApp.Run(() =>
        {
            var (window, transport, _) = Open(new AudioOptions { Visualizer = false }); // the remembered choice
            try
            {
                Assert.False(transport.Visualizer.IsVisible);
                Assert.False(AudioPlaybackTests.Find<CheckBox>(transport, "AudioVisualizerShow").IsChecked);
            }
            finally
            {
                window.Close();
                AudioDevices.Shutdown();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheVisualizerResizesWithItsContainer()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, transport, _) = Open(width: 700);
            try
            {
                var wide = transport.Visualizer.BarCount;
                Assert.True(wide > 20);
                window.Width = 220;
                await LayoutAudit.Settle();
                var narrow = transport.Visualizer.BarCount;
                Assert.InRange(narrow, 4, wide - 10);
                Assert.True(transport.Visualizer.Bounds.Width <= 220);
            }
            finally
            {
                window.Close();
                AudioDevices.Shutdown();
            }
        });
    }
}
