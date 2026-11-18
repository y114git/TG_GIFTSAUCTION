using G3MStudio.Audio;
using G3MStudio.Core.Audio;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>The microphone list and the recording itself: record, pause, stop, the timer that reads the device, and giving the device back.</summary>
public sealed partial class RecorderWindow
{
    // The remembered microphone when it is present, else the default one (a missing one stays remembered, so it is used again once it is back), else the first.
    private async Task RefreshAsync()
    {
        if (_listing) return;
        _listing = true;
        Update();
        CaptureDevices list;
        try
        {
            list = await Task.Run(AudioCaptureDevices.List);
        }
        finally
        {
            _listing = false;
        }

        if (_closed) return;
        _building = true;
        (_names, _default) = ([.. list.Names], list.Default);
        _device.ItemsSource = _names.Select(n => n == _default ? Loc.Current.Format("record.defaultMark", ("name", n)) : n).ToList();
        var wanted = _audio().CaptureDevice;
        var at = wanted.Length > 0 ? _names.IndexOf(wanted) : -1;
        if (at < 0) at = _names.IndexOf(list.Default);
        _device.SelectedIndex = at >= 0 ? at : _names.Count > 0 ? 0 : -1;
        _building = false;
        _problem = list.Problem is { } p ? Failure(p) : _names.Count == 0 ? Failure(CaptureFailure.NoDevice) : null;
        Denied = list.Problem == CaptureFailure.AccessDenied;
        if (_phase == Phase.Idle) _message.Text = _problem ?? ""; // a take on show keeps its own note (a lost microphone, the limit, a refused name)
        Update();
    }

    private void Remember()
    {
        if (_building || _device.SelectedIndex < 0 || _device.SelectedIndex >= _names.Count) return;
        var chosen = _names[_device.SelectedIndex];
        _remember(chosen == _default ? "" : chosen); // the default one stays "the default": it follows the system when that changes
    }

    private async Task RecordAsync()
    {
        if (_working || (_phase == Phase.Idle && Blocked) || _phase is not (Phase.Idle or Phase.Stopped)) return;
        _working = true;
        try
        {
            Update();
            if (_listing) await Listing; // the remembered microphone is only known once the list is read
            if (_closed || (_phase == Phase.Idle && Blocked)) return;
            var kept = _phase == Phase.Stopped;
            if (kept && !await DialogFrame.ConfirmDiscardAsync(this)) return;
            if (_closed) return;
            SetPhase(Phase.Opening);
            IAudioCapture capture;
            var name = ChosenName;
            try
            {
                capture = await Task.Run(() => AudioCaptureDevices.Open(name));
            }
            catch (CaptureException e)
            {
                Fail(e.Failure, kept ? Phase.Stopped : Phase.Idle); // the take that was asked to go stays until a microphone is open
                return;
            }

            if (_closed)
            {
                ReleaseLater(capture);
                return;
            }

            _capture = capture;
            Discard();
            if (_countdown.IsChecked == true)
            {
                SetPhase(Phase.Countdown);
                for (var left = CountdownSeconds; left > 0; left--)
                {
                    _state.Text = Loc.Current.Format("record.stateCountdown", ("seconds", left));
                    await Task.Delay(CountdownStep);
                    if (_phase != Phase.Countdown || _closed) return; // Stop or the window ended it, and gave the microphone back
                }

                SetPhase(Phase.Opening);
            }

            if (!await StartDeviceAsync(false)) return;
            SetPhase(Phase.Recording);
        }
        finally
        {
            _working = false;
            if (!_closed) Update();
        }
    }

    // Starting a device can take a while with some drivers, so it runs off the UI thread. False: it failed (the message is shown and the microphone is back) or Stop or the window took the microphone
    // meanwhile (then that already settled the phase, and this must not touch it). A failed first start gives up; a failed resume keeps what was taken, as a lost microphone does.
    private async Task<bool> StartDeviceAsync(bool resume)
    {
        var capture = _capture;
        if (capture is null) return false;
        AudioDevices.StopPlaying(); // the microphone must not record what the program plays
        try
        {
            await Task.Run(capture.Start);
        }
        catch (CaptureException e) when (Owns(capture))
        {
            if (resume) End(Failure(e.Failure));
            else Fail(e.Failure);
            return false;
        }
        catch (Exception e) when ((e is CaptureException or ObjectDisposedException) && !Owns(capture))
        {
            return false;
        }

        return Owns(capture);
    }

    private bool Owns(IAudioCapture capture) => !_closed && ReferenceEquals(_capture, capture);

    private async Task PauseOrResumeAsync()
    {
        if (_working) return;
        if (_phase == Phase.Recording)
        {
            _capture?.Stop();
            try
            {
                Drain(); // after the stop, so the samples that arrived up to it are in this segment and not the next
            }
            catch (CaptureException e)
            {
                End(Failure(e.Failure));
                return;
            }

            SetPhase(Phase.Paused);
        }
        else if (_phase == Phase.Paused)
        {
            _working = true;
            Update();
            try
            {
                if (await StartDeviceAsync(true)) SetPhase(Phase.Recording);
            }
            finally
            {
                _working = false;
                if (!_closed) Update();
            }
        }
    }

    /// <summary>Stops recording (or the countdown) and gives the microphone back; what was taken becomes the take on show, with the player.</summary>
    private void End(string? note)
    {
        if (_phase == Phase.Countdown)
        {
            ReleaseCapture();
            SetPhase(Phase.Idle);
            return;
        }

        if (_phase is not (Phase.Recording or Phase.Paused)) return;
        _capture?.Stop();
        try
        {
            if (_phase == Phase.Recording) Drain(); // after the stop: nothing that arrived before it is lost
        }
        catch (CaptureException e)
        {
            note ??= Failure(e.Failure);
        }
        catch (OutOfMemoryException)
        {
            note ??= Loc.Current["record.memoryStop"];
        }

        note ??= Take.Full ? Loc.Current["record.capReached"] : null;
        ReleaseCapture();
        if (Take.Frames == 0)
        {
            _message.Text = note ?? Loc.Current["record.empty"];
            SetPhase(Phase.Idle);
            return;
        }

        if (!MakeClip())
        {
            Take.Clear();
            _message.Text = Loc.Current.Format("memory.failed", ("name", Loc.Current["memory.opRecord"]));
            SetPhase(Phase.Idle);
            return;
        }

        _player.Clear();
        Transport.Replace(_clip);
        ShowInfo();
        SetPhase(Phase.Stopped);
        _message.Text = note ?? "";
    }

    // The clip is the take from here on; its chunks are let go as they are copied, so a long take is not in memory twice. A refused allocation loses nothing, so it is tried once more after the owner freed what it could.
    private bool MakeClip()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                _clip = Take.ToAudio(release: true);
                return true;
            }
            catch (OutOfMemoryException)
            {
                OutOfMemory?.Invoke();
            }
        }

        return false;
    }

    // Reads what the microphone has delivered since the last call into the take.
    private void Drain()
    {
        if (_capture is null) return;
        int got;
        do
        {
            got = _capture.Read(_block);
            if (got > 0) Take.Append(_block.AsSpan(0, got));
        }
        while (got == _block.Length && !Take.Full);
    }

    private void Tick()
    {
        if (_phase == Phase.Recording)
        {
            try
            {
                Drain();
                if (Take.Full) End(null);
                else if (HasMemoryFor?.Invoke(Take.Frames * (long)sizeof(float)) == false) StopForMemory(); // the clip of the take needs a float per frame
            }
            catch (CaptureException e)
            {
                End(Failure(e.Failure)); // unplugged: what was recorded until then is kept
            }
            catch (OutOfMemoryException)
            {
                StopForMemory();
            }
        }

        var recording = _phase == Phase.Recording;
        var tail = recording ? Take.Last(4096) : null; // the meter reads the last moments of the take while recording, as the player's one reads the playing position, and only decays otherwise
        _meter.Step(tail, Math.Max(0, (tail?.Seconds ?? 0) - 0.025), recording, _pump.Interval.TotalSeconds);
        _clipped.IsVisible = Take.Clipped;
        ShowTime();
        if (!recording && _meter.Settled) _pump.Stop();
    }

    private void StopForMemory()
    {
        OutOfMemory?.Invoke();
        End(Loc.Current["record.memoryStop"]);
    }

    private void SetPhase(Phase phase)
    {
        _phase = phase;
        if (phase == Phase.Recording && !_pump.IsEnabled) _pump.Start();
        Update();
    }
}
