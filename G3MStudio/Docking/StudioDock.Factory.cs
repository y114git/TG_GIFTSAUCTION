using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using G3MStudio.Core.Settings;

namespace G3MStudio.Docking;

public sealed partial class StudioDock
{
    private sealed partial class StudioFactory : Factory
    {
        public StudioDock? Home { get; init; }

        // The menu of a floated panel needs the same preparation as a docked one.
        private FloatHost FloatWindow()
        {
            var window = new FloatHost();
            if (Home is { } home)
            {
                ToolMenu.Hook(window, home);
                window.Closing += (_, _) => home.ReturnPanels(window.Window);
                home.FloatCreated?.Invoke(window);
            }

            return window;
        }

        public override IRootDock CreateLayout() => Build("studio");

        public IRootDock CreatePreset(string preset) => Build(preset);

        // Left, right and bottom shares per preset. Minimal folds the right and bottom docks (a share of 0, the old share kept for unfolding).
        private static (double Left, double Right, double Bottom) Shares(string preset) => preset switch
        {
            "wide" => (0.12, 0.12, 0.16),
            "code" => (0.18, 0.14, 0.28),
            "canvas" => (0.13, 0.15, 0.1),
            "minimal" => (0.15, FoldedShare, FoldedShare),
            _ => (UiSizing.ExplorerShare, UiSizing.InspectorShare, UiSizing.BottomShare),
        };

        private IRootDock Build(string preset)
        {
            var (left, right, bottom) = Shares(preset);
            var bottomPanels = preset == "code" ? new[] { "Problems", "Output", "Usages", "Reference", "History", "Git" } : ["History", "Git", "Problems", "Output", "Usages", "Reference"];
            Tool Panel(string id) => new() { Id = id, Title = PanelTitle(id) };
            ToolDock Tools(string id, Alignment side, double size, params Tool[] tools) =>
                new() { Id = id, Alignment = side, Proportion = size, VisibleDockables = CreateList<IDockable>(tools), ActiveDockable = tools[0] };

            var editors = new DocumentDock { Id = "Editors", CanCreateDocument = false, IsCollapsable = false, Proportion = 1 - bottom, VisibleDockables = CreateList<IDockable>() };
            // The bottom dock sits under the centre only; the side docks run the full height (like GameMaker).
            var centre = new ProportionalDock
            {
                Id = "Centre",
                Proportion = 1 - left - right,
                Orientation = Orientation.Vertical,
                VisibleDockables = CreateList<IDockable>(editors, new ProportionalDockSplitter(), Tools("BottomDock", Alignment.Bottom, bottom, [.. bottomPanels.Select(Panel)])),
            };
            var main = new ProportionalDock
            {
                Id = "Main",
                Orientation = Orientation.Horizontal,
                VisibleDockables = CreateList<IDockable>(
                    Tools("LeftDock", Alignment.Left, left, Panel("Explorer"), Panel("Search")),
                    new ProportionalDockSplitter(),
                    centre,
                    new ProportionalDockSplitter(),
                    Tools("RightDock", Alignment.Right, right, Panel("Inspector"))),
            };
            var root = CreateRootDock();
            root.Id = "Root";
            root.VisibleDockables = CreateList<IDockable>(main);
            root.ActiveDockable = main;
            root.DefaultDockable = main;
            return root;
        }

        public override void InitLayout(IDockable layout)
        {
            HostWindowLocator = new Dictionary<string, Func<IHostWindow?>> { [nameof(IDockWindow)] = FloatWindow };
            base.InitLayout(layout);
        }
    }
}
