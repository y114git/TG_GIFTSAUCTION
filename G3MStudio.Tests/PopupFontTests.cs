using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>Popup roots are top levels of their own: every surface that opens in one must show the text in the font family and weight of the rest of the app.</summary>
public sealed class PopupFontTests
{
    private static readonly string[] Items = ["one", "two"];

    private static async Task Settle()
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Every text in the tree of a popup that differs from the app font, as "text: family weight".</summary>
    private static List<string> Strays(Visual root, Window app)
    {
        var texts = root.GetSelfAndVisualDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text) && t.IsVisible).ToList();
        var list = texts.Where(t => t.FontFamily != app.FontFamily || t.FontWeight != app.FontWeight).Select(t => $"{t.GetType().Name} '{t.Text}': {t.FontFamily.Name} {t.FontWeight}").ToList();
        if (texts.Count == 0) list.Add("no text found in " + root.GetType().Name);
        return list;
    }

    private static Visual PopupTree(Visual from) => TopLevel.GetTopLevel(from) as Visual ?? from;

    [Fact]
    public Task EveryPopupKindShowsItsTextInTheAppFontAndWeight() => HeadlessApp.Run(async () =>
    {
        Assert.Equal(FontWeight.Bold, (FontWeight)Application.Current!.FindResource("Font.Weight")!);
        var combo = new ComboBox { ItemsSource = Items, SelectedIndex = 0 };
        var button = new Button { Content = "x" };
        ToolTip.SetTip(button, "A tip");
        var menu = new ContextMenu { Items = { new MenuItem { Header = "Cut" }, new MenuItem { Header = "Sub", Items = { new MenuItem { Header = "Inner" } } } } };
        button.ContextMenu = menu;
        var flyout = new MenuFlyout { Items = { new MenuItem { Header = "Flyout item" } } };
        var plain = new Flyout { Content = new TextBlock { Text = "Plain flyout text" } };
        var auto = new AutoCompleteBox { ItemsSource = Items, MinimumPrefixLength = 0, Text = "o" };
        var raw = new Popup { PlacementTarget = button, Child = new TextBlock { Text = "Raw popup text" } };
        var panel = new StackPanel { Children = { combo, button, auto, raw } };
        var window = new Window { Content = panel, Width = 400, Height = 400 };
        window.Show();
        try
        {
            await Settle();
            var found = new List<string>();

            combo.IsDropDownOpen = true;
            await Settle();
            found.AddRange(Strays(PopupTree(combo.ContainerFromIndex(1)!), window).Select(s => "combo " + s));
            combo.IsDropDownOpen = false;

            ToolTip.SetIsOpen(button, true);
            await Settle();
            var tip = window.GetVisualDescendants().OfType<ToolTip>().Single();
            found.AddRange(Strays(tip, window).Select(s => "tooltip " + s));
            ToolTip.SetIsOpen(button, false);

            menu.Open(button);
            await Settle();
            found.AddRange(Strays(PopupTree(menu.Items.OfType<MenuItem>().First()), window).Select(s => "contextmenu " + s));
            ((MenuItem)menu.Items[1]!).IsSubMenuOpen = true;
            await Settle();
            found.AddRange(Strays(PopupTree(((MenuItem)((MenuItem)menu.Items[1]!).Items[0]!)), window).Select(s => "submenu " + s));
            menu.Close();

            flyout.ShowAt(button);
            await Settle();
            found.AddRange(Strays(PopupTree(flyout.Items.OfType<MenuItem>().First()), window).Select(s => "menuflyout " + s));
            flyout.Hide();

            plain.ShowAt(button);
            await Settle();
            found.AddRange(Strays(PopupTree((Visual)plain.Content!), window).Select(s => "flyout " + s));
            plain.Hide();

            auto.IsDropDownOpen = true;
            await Settle();
            found.AddRange(Strays(PopupTree(auto.GetVisualDescendants().OfType<Popup>().First().Child!), window).Select(s => "autocomplete " + s));
            auto.IsDropDownOpen = false;

            raw.IsOpen = true;
            await Settle();
            found.AddRange(Strays(PopupTree(raw.Child!), window).Select(s => "popup " + s));
            raw.IsOpen = false;

            var editor = new AvaloniaEdit.TextEditor { Text = "abc" };
            panel.Children.Add(editor);
            await Settle();
            var completion = new AvaloniaEdit.CodeCompletion.CompletionWindow(editor.TextArea);
            completion.CompletionList.CompletionData.Add(new Entry());
            completion.Show();
            await Settle();
            found.AddRange(Strays(PopupTree(completion.CompletionList), window).Select(s => "completion " + s));
            completion.Close();

            Assert.True(found.Count == 0, string.Join("\n", found));
        }
        finally { window.Close(); }
    });

    private sealed class Entry : AvaloniaEdit.CodeCompletion.ICompletionData
    {
        public Avalonia.Media.IImage? Image => null;
        public string Text => "alpha";
        public object Content => "alpha";
        public object Description => "described";
        public double Priority => 0;
        public void Complete(AvaloniaEdit.Editing.TextArea textArea, AvaloniaEdit.Document.ISegment completionSegment, EventArgs insertionRequestEventArgs) { }
    }
}
