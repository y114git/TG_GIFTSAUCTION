using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using G3MLib.DataFile.Models;
using Avalonia.Input;
using G3MStudio.Core.Particles;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// A live preview of a particle system above its emitters: the simulation steps 60 times a second while the tab is shown and starts again when no
/// particle is left for a while, so a system made of bursts keeps playing. It is an approximation, see <see cref="ParticleSimulation"/>.
/// </summary>
public sealed class ParticlePreviewView : StackPanel
{
    private readonly ParticleCanvas _canvas;

    public ParticlePreviewView(GameMakerParticleSystem system, Workspace? workspace = null)
    {
        Name = "ParticlePreview";
        Spacing = 4;
        _canvas = new ParticleCanvas(system, workspace);
        if (workspace is not null)
        {
            // An emitter added, removed or duplicated changes what the simulation holds.
            AttachedToVisualTree += (_, _) => workspace.History.Changed += _canvas.Rebuild;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= _canvas.Rebuild;
        }

        var restart = new Button { Name = "ParticleRestart", Content = Loc.Current["particles.restart"] };
        restart.Click += (_, _) => Restart();
        Children.Add(_canvas);
        Children.Add(restart);
    }

    public ParticleSimulation Simulation => _canvas.Simulation;

    public void Restart() => _canvas.Restart();

    /// <summary>The only emitter that runs in the preview, or null for all enabled ones. An editor setting, not stored in the file.</summary>
    public GameMakerParticleSystemEmitter? Solo
    {
        get => _canvas.Solo;
        set => _canvas.Solo = value;
    }

    /// <summary>Moves the region of an emitter (its centre, relative to the system origin) as one undoable edit. False without a workspace.</summary>
    public bool MoveRegion(int emitter, float x, float y) => _canvas.MoveRegion(emitter, x, y);

    /// <summary>The emitter whose region contains a point given relative to the system origin, the last one drawn first; -1 for none.</summary>
    public int RegionAt(float x, float y) => _canvas.RegionAt(x, y);

    /// <summary>Runs one simulation step and redraws; the timer calls this, and a test can too.</summary>
    public void Advance() => _canvas.Advance();

    private sealed class ParticleCanvas : Control
    {
        private const int QuietSteps = 45, StillSteps = 40;

        private readonly GameMakerParticleSystem _system;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private int _quiet;

        private readonly Workspace? _workspace;
        private (int Emitter, Point From)? _drag;
        private Point _pointer;

        public ParticleCanvas(GameMakerParticleSystem system, Workspace? workspace)
        {
            (_system, _workspace) = (system, workspace);
            Simulation = new ParticleSimulation(system);
            Height = 280;
            ClipToBounds = true;
            _timer.Tick += (_, _) => Advance();
            AttachedToVisualTree += (_, _) =>
            {
                UiMotion.Changed += Retime;
                Retime();
            };
            DetachedFromVisualTree += (_, _) =>
            {
                UiMotion.Changed -= Retime;
                _timer.Stop();
            };
        }

        public ParticleSimulation Simulation { get; private set; }

        public GameMakerParticleSystemEmitter? Solo
        {
            get;
            set
            {
                field = value;
                Simulation.Solo = value;
            }
        }

        private IEnumerable<GameMakerParticleSystemEmitter?> Emitters() => (_system.Emitters ?? []).Select(r => r.Resource);

        public void Rebuild() => Restart();

        public int RegionAt(float x, float y)
        {
            var list = Emitters().ToList();
            for (var i = list.Count - 1; i >= 0; i--)
                if (list[i] is { Enabled: true } e && Math.Abs(x - e.RegionX) <= Math.Max(4, e.RegionWidth / 2) && Math.Abs(y - e.RegionY) <= Math.Max(4, e.RegionHeight / 2)) return i;
            return -1;
        }

        public bool MoveRegion(int emitter, float x, float y)
        {
            if (_workspace is null || emitter < 0 || emitter >= _system.Emitters.Count) return false;
            var name = _system.Name.Content ?? "";
            static string Text(float v) => Math.Round(v, 2).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            _workspace.Edit(new SetPropertyCommand("particleSystems", name, $"Emitters[{emitter}].Resource.RegionX", Text(x)));
            _workspace.Edit(new SetPropertyCommand("particleSystems", name, $"Emitters[{emitter}].Resource.RegionY", Text(y)));
            return true;
        }

        private Point Origin => new((Bounds.Width / 2) + _system.OriginX, (Bounds.Height * 0.6) + _system.OriginY);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (_workspace is null) return;
            var at = e.GetPosition(this) - Origin;
            var hit = RegionAt((float)at.X, (float)at.Y);
            if (hit < 0) return;
            (_drag, _pointer) = ((hit, new Point(at.X, at.Y)), e.GetPosition(this));
            e.Pointer.Capture(this);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_drag is null) return;
            _pointer = e.GetPosition(this);
            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_drag is not { } drag) return;
            _drag = null;
            e.Pointer.Capture(null);
            var shift = e.GetPosition(this) - (drag.From + Origin);
            if (Emitters().ElementAtOrDefault(drag.Emitter) is { } moved) MoveRegion(drag.Emitter, moved.RegionX + (float)shift.X, moved.RegionY + (float)shift.Y);
            InvalidateVisual();
        }

        /// <summary>With Reduce motion on the preview does not run: it shows the system as it looks after a short while.</summary>
        private void Retime()
        {
            if (UiMotion.Reduced)
            {
                _timer.Stop();
                Restart();
            }
            else
            {
                _timer.Start();
            }
        }

        public void Restart()
        {
            Simulation = new ParticleSimulation(_system) { Solo = Solo };
            _quiet = 0;
            if (UiMotion.Reduced)
                for (var i = 0; i < StillSteps; i++) Simulation.Step();
            InvalidateVisual();
        }

        public void Advance()
        {
            Simulation.Step();
            _quiet = Simulation.Count == 0 ? _quiet + 1 : 0;
            if (_quiet > QuietSteps) Restart();
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(G3MStudio.Theme.CanvasTheme.Stage, new Rect(Bounds.Size));
            var origin = new Point((Bounds.Width / 2) + _system.OriginX, (Bounds.Height * 0.6) + _system.OriginY);
            var guide = new Pen(G3MStudio.Theme.CanvasTheme.Soft(G3MStudio.Theme.CanvasTheme.Handle, 0.2), 1);
            context.DrawLine(guide, origin - new Vector(8, 0), origin + new Vector(8, 0));
            context.DrawLine(guide, origin - new Vector(0, 8), origin + new Vector(0, 8));
            var outline = new Pen(G3MStudio.Theme.CanvasTheme.Soft(G3MStudio.Theme.CanvasTheme.Handle, 0.35), 1);
            var list = Emitters().ToList();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not { Enabled: true } e) continue;
                var shift = _drag is { } d && d.Emitter == i ? _pointer - (d.From + origin) : default;
                var centre = new Point(origin.X + e.RegionX + shift.X, origin.Y + e.RegionY + shift.Y);
                context.DrawRectangle(null, e == Solo ? new Pen(G3MStudio.Theme.CanvasTheme.Point, 2) : outline, new Rect(centre.X - (e.RegionWidth / 2), centre.Y - (e.RegionHeight / 2), e.RegionWidth, e.RegionHeight));
            }

            foreach (var dot in Simulation.Dots())
            {
                var radius = Math.Clamp(dot.Size * 6, 1, 80);
                context.DrawEllipse(new ImmutableSolidColorBrush(Color.FromArgb(dot.A, dot.R, dot.G, dot.B)), null, new Point(origin.X + dot.X, origin.Y + dot.Y), radius, radius);
            }
        }
    }
}
