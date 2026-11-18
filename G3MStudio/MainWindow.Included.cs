using Avalonia;
using Avalonia.Controls;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>Text files, audio files and embedded audio: each opens in the editor that fits its content (code editor control, sound player) and writes back through one undoable command.</summary>
public sealed partial class MainWindow
{
    private readonly Dictionary<string, TextFileView> _textViews = [];

    /// <summary>Opens a file of the Included Files folder in the editor of its kind: the image editor, the code editor control or the sound player; false for any other kind.</summary>
    public bool OpenIncludedFile(string path)
    {
        var key = ResourceTypes.KeyOf("includedFiles", path);
        var kind = IncludedPictureCommand.IsPicture(path) ? "image" : IncludedTextCommand.IsText(path) ? "text" : IncludedAudioCommand.IsAudio(path) ? "audio" : null;
        if (kind is null) return false;
        Docking.OpenTab(kind, key, false);
        return true;
    }

    /// <summary>A picture file dropped on a picture view replaces the picture of its target: an image tab takes it as a step of its own and Apply writes it, any other view writes it at once. The size rules are the target's.</summary>
    internal async Task ReplacePictureFromAsync(Workspace workspace, string key, string path)
    {
        try
        {
            var picture = FramePicture.FromBytes(await File.ReadAllBytesAsync(path));
            if (Docking.ActiveTabId is { } tab && _pictureSessions.TryGetValue(tab, out var pending) && Docking.Tabs.Get(tab)?.Key == key)
            {
                pending.Session.ReplaceWith(picture);
                Docking.KeepOpen(tab);
                return;
            }

            var frame = Docking.ActiveTabId is { } front && _imageViews.TryGetValue(front, out var view) ? view.Frame : _imageFrame;
            if (ResourceTypes.SplitKey(key) is not var (type, name) || PictureTargets.For(type, name, frame, workspace.Path) is not { } target) return;
            if (await EditOrForceAsync(workspace, force =>
            {
                using (workspace.Lock.Read()) return target.Apply(workspace.Data, picture, force);
            }, target.Title)) Notify(NotificationLevel.Info, Loc.Current.Format("image.applied", ("name", target.Title)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ImageMagick.MagickException or InvalidDataException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sprites.badPicture", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }

    private ContentControl TextFileTab(TabInfo tab, Workspace workspace, string relative)
    {
        var host = new ContentControl { HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch };
        _ = LoadTextAsync();
        return host;

        async Task LoadTextAsync()
        {
            string text;
            try
            {
                text = await File.ReadAllTextAsync(new IncludedFiles(workspace.Path).Resolve(relative));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                host.Content = new TextBlock { Text = e.Message, Classes = { "muted" } };
                return;
            }

            var view = new TextFileView(text);
            view.ApplyRequested += async changed =>
            {
                if (await EditOrForceAsync(workspace, force => new IncludedTextCommand(workspace.Path, relative, changed, force), relative)) view.MarkApplied();
                if (Docking.DocumentOf(tab.Id) is { } doc) doc.IsModified = view.HasPending;
            };
            view.Changed += () =>
            {
                Docking.KeepOpen(tab.Id);
                if (Docking.DocumentOf(tab.Id) is { } doc) doc.IsModified = view.HasPending;
            };
            _textViews[tab.Id] = view;
            host.Content = view;
        }
    }

    /// <summary>The sound player for embedded audio or an audio file of the Included Files folder, with Replace and Export; it is built again after each change of the data.</summary>
    private StackPanel AudioTab(TabInfo tab, Workspace workspace, string type, string name)
    {
        var host = new ContentControl();
        var bar = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Margin = new Thickness(0, 0, 0, 6) };
        Button Make(string id, string key, Func<Task> act)
        {
            var button = new Button { Name = id, Content = Loc.Current[key] };
            button.Click += async (_, _) => await act();
            return button;
        }

        bar.Children.Add(Make("AudioFileReplace", G3MStudio.Core.Commands.AppCommands.TitleKey("edit.replaceAudio"), () => ReplaceAudioBytesAsync(workspace, type, name)));
        bar.Children.Add(Make("AudioFileExport", G3MStudio.Core.Commands.AppCommands.TitleKey("edit.exportAudio"), () => ExportAudioBytesAsync(workspace, type, name)));
        var panel = new StackPanel { Children = { bar, host } };
        var built = 0;
        async Task Build()
        {
            var mine = ++built;
            if (_soundViews.Remove(tab.Id, out var old)) old.StopPlayback();
            // An included file is read off the UI thread; embedded audio is already in memory.
            var read = type == "embeddedAudio" ? AudioBytes(workspace, type, name) : await Task.Run(() => AudioBytes(workspace, type, name));
            if (mine != built) return;
            if (read is not { } bytes)
            {
                host.Content = new TextBlock { Text = Loc.Current.Format("sounds.outside", ("file", name)), Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail };
                return;
            }

            host.Content = RememberSound(tab.Id, new SoundPlayerView(name, bytes, UrlOpener, () => Workspaces.Settings.Current.Audio));
        }

        // The history raises Changed while the workspace is locked for writing, and reading the audio needs the read lock, so the rebuild waits.
        void Rebuild() => Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = Build());
        panel.AttachedToVisualTree += (_, _) => workspace.History.Changed += Rebuild;
        panel.DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Rebuild;
        _ = Build();
        return panel;
    }

    /// <summary>The audio bytes of an embedded audio entry or an included audio file; null when there are none to show.</summary>
    internal static byte[]? AudioBytes(Workspace workspace, string type, string name)
    {
        try
        {
            using (workspace.Lock.Read())
                return type == "embeddedAudio" ? workspace.Data.EmbeddedAudio.FirstOrDefault(e => e.Name.Content == name)?.Data : File.ReadAllBytes(new IncludedFiles(workspace.Path).Resolve(name));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Puts a WAV, OGG or MP3 file into embedded audio or an included audio file; one undoable step. A WAV or OGG goes in as the kind the target holds.</summary>
    internal async Task ReplaceAudioBytesFromAsync(Workspace workspace, string type, string name, string path)
    {
        try
        {
            var file = await File.ReadAllBytesAsync(path);
            var current = await Task.Run(() => AudioBytes(workspace, type, name)) ?? [];
            var stored = SoundAudio.Format(current);
            var bytes = stored is "wav" or "ogg" ? await Task.Run(() => AudioCodec.Convert(file, stored)) : file;
            var done = await EditOrForceAsync(workspace, force => type == "embeddedAudio" ? new ReplaceEmbeddedAudioCommand(name, bytes) : new IncludedAudioCommand(workspace.Path, name, bytes, force), name);
            if (done) Notify(NotificationLevel.Info, Loc.Current.Format("sounds.replaced", ("sound", name), ("kb", bytes.Length / 1024)));
            if (done && stored == "wav") TellDepthConverted(file, name);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sounds.badAudio", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }

    private async Task ReplaceAudioBytesAsync(Workspace workspace, string type, string name)
    {
        if (await PickPatchFile(Loc.Current["sounds.pickAudio"], false) is { } path) await ReplaceAudioBytesFromAsync(workspace, type, name, path);
    }

    private async Task ExportAudioBytesAsync(Workspace workspace, string type, string name)
    {
        if (await Task.Run(() => AudioBytes(workspace, type, name)) is not { Length: > 0 } audio) return;
        if (await PickPatchFile(Loc.Current["sounds.pickAudioSave"], true, "." + SoundAudio.Format(audio)) is not { } path) return;
        try
        {
            await File.WriteAllBytesAsync(path, audio);
            Notify(NotificationLevel.Info, Loc.Current.Format("sounds.exported", ("sound", name), ("path", path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sounds.badAudio", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }
}
