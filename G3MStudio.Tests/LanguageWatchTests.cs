using Avalonia.Controls;
using G3MStudio.Core.Localization;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class LanguageWatchTests
{
    [Fact]
    public async Task AViewThatWasHiddenDuringALanguageChangeIsRelabelledWhenShownAgain()
    {
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var view = new Border();
            var calls = 0;
            LanguageWatch.Follow(view, (_, e) => { if (e.PropertyName == nameof(Localizer.Language)) calls++; });
            var window = new Window { Content = view };
            window.Show();
            try
            {
                Loc.Current.SetLanguage("ru");
                Assert.Equal(1, calls); // shown: the event arrives at once
                window.Content = null; // a background tab is detached
                Loc.Current.SetLanguage("ja");
                Assert.Equal(1, calls);
                window.Content = view;
                Assert.Equal(2, calls); // shown again: the missed change is replayed once
                window.Content = null;
                window.Content = view;
                Assert.Equal(2, calls); // nothing changed since: no replay
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                window.Close();
            }

            await Task.CompletedTask;
        });
    }
}
