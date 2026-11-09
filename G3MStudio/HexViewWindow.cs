using System.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>Every byte of the data file on disk as hex and text, read on demand, with Go to Offset and Find (hex bytes such as "58 59 5A 57", or text). Read-only.</summary>
public sealed class HexViewWindow : Window, IDisposable
{
    private readonly HexSource _source;
    private readonly ListBox _rows = new() { Name = "HexRows", FontFamily = (Avalonia.Media.FontFamily?)Avalonia.Application.Current?.FindResource("Font.Code") ?? Avalonia.Media.FontFamily.Default };
    private readonly TextBox _goTo = new() { Name = "HexGoTo", Width = 150, PlaceholderText = Loc.Current["hex.goTo"] };
    private readonly TextBox _find = new() { Name = "HexFind", Width = 220, PlaceholderText = Loc.Current["hex.find"] };
    private readonly TextBlock _message = new() { Name = "HexMessage", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };

    public HexViewWindow(string path)
    {
        _source = new HexSource(path);
        Title = Loc.Current.Format("hex.title", ("file", System.IO.Path.GetFileName(path)));
        Width = 760;
        Height = 560;
        MinWidth = 420;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Closed += (_, _) => Dispose();
        _rows.ItemsSource = new Rows(_source);
        ScrollViewer.SetHorizontalScrollBarVisibility(_rows, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto); // a narrow window scrolls the row instead of cutting it
        var go = new Button { Name = "HexGo", Content = Loc.Current["hex.goButton"] };
        var next = new Button { Name = "HexNext", Content = Loc.Current["hex.next"] };
        go.Click += (_, _) => GoTo();
        next.Click += (_, _) => FindNext();
        _goTo.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            GoTo();
            e.Handled = true;
        };
        _find.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            FindNext();
            e.Handled = true;
        };
        var list = _source.Chunks();
        var chunks = new ComboBox { Name = "HexChunks", Width = 140, PlaceholderText = Loc.Current["hex.chunk"], ItemsSource = list.Select(c => c.Name + " " + c.Offset.ToString("X", System.Globalization.CultureInfo.InvariantCulture)).ToList() };
        chunks.SelectionChanged += (_, _) =>
        {
            if (chunks.SelectedIndex is >= 0 and var i) Show(list[i].Offset);
        };
        var bar = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Margin = new Avalonia.Thickness(0, 0, 0, 8), Children = { chunks, _goTo, go, _find, next, _message } };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        var info = new TextBlock { Text = Loc.Current.Format("hex.size", ("bytes", _source.Length.ToString("N0", System.Globalization.CultureInfo.CurrentCulture))), Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        DialogFrame.Show(this, new DockPanel { Children = { bar, _rows } }, [DialogFrame.Close(this)], info);
    }

    public void Dispose() => _source.Dispose();

    private void Show(long offset)
    {
        var row = (int)(offset / HexSource.RowBytes);
        _rows.SelectedIndex = row;
        _rows.ScrollIntoView(row);
    }

    private void GoTo()
    {
        if (!HexSource.TryParseOffset(_goTo.Text, out var offset))
        {
            _message.Text = Loc.Current["hex.badOffset"];
            return;
        }

        if (offset >= _source.Length)
        {
            _message.Text = Loc.Current.Format("hex.beyond", ("offset", offset.ToString("X", System.Globalization.CultureInfo.InvariantCulture)), ("end", _source.Length.ToString("X", System.Globalization.CultureInfo.InvariantCulture)));
            return;
        }

        _message.Text = "";
        Show(offset);
    }

    private void FindNext()
    {
        if (HexSource.ParsePattern(_find.Text) is not { } pattern) return;
        var from = _rows.SelectedIndex >= 0 ? (long)(_rows.SelectedIndex + 1) * HexSource.RowBytes : 0;
        var at = _source.Find(pattern, from);
        if (at < 0 && from > 0) at = _source.Find(pattern, 0); // wrap round to the start
        if (at < 0)
        {
            _message.Text = Loc.Current.Format("hex.noMatch", ("text", _find.Text ?? ""));
            return;
        }

        _message.Text = Loc.Current.Format("hex.foundAt", ("offset", at.ToString("X", System.Globalization.CultureInfo.InvariantCulture)));
        Show(at);
    }

    /// <summary>The rows as a read-only list the list box can virtualize.</summary>
    private sealed class Rows(HexSource source) : IList
    {
        public object? this[int index] { get => source.Row(index); set => throw new NotSupportedException(); }

        public int Count => source.RowCount;

        public bool IsReadOnly => true;

        public bool IsFixedSize => true;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public int Add(object? value) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(object? value) => false;

        public void CopyTo(Array array, int index) => throw new NotSupportedException();

        public IEnumerator GetEnumerator()
        {
            for (var i = 0; i < Count; i++) yield return source.Row(i);
        }

        public int IndexOf(object? value) => -1;

        public void Insert(int index, object? value) => throw new NotSupportedException();

        public void Remove(object? value) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();
    }
}
