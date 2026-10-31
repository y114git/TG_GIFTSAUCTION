using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Particles;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// A live preview of a particle system above its emitters: the simulation steps 60 times a second while the tab is shown and starts again when no
/// particle is left for a while, so a system made of bursts keeps playing. It is an approximation, see <see cref="ParticleSimulation"/>.
/// </summary>
public sealed class ParticlePreviewView : StackPanel
{
    private readonly ParticleCanvas _canvas;

    public ParticlePreviewView(GameMakerParticleSystem system)
    {
        Name = "ParticlePreview";
        Spacing = 4;
        _canvas = new ParticleCanvas(system);
        var restart = new Button { Name = "ParticleRestart", Content = Loc.Current["particles.restart"] };
        restart.Click += (_, _) => Restart();
        Children.Add(_canvas);
        Children.Add(restart);
    }

    public ParticleSimulation Simulation => _canvas.Simulation;

    public void Restart() => _canvas.Restart();

    /// <summary>Runs one simulation step and redraws; the timer calls this, and a test can too.</summary>
    public void Advance() => _canvas.Advance();

    private sealed class ParticleCanvas : Control
    {
        private const int QuietSteps = 45, StillSteps = 40;

        private readonly GameMakerParticleSystem _system;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private int _quiet;

        public ParticleCanvas(GameMakerParticleSystem system)
        {
            _system = system;
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
            Simulation = new ParticleSimulation(_system);
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
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(24, 24, 28)), new Rect(Bounds.Size));
            var origin = new Point((Bounds.Width / 2) + _system.OriginX, (Bounds.Height * 0.6) + _system.OriginY);
            var guide = new Pen(new SolidColorBrush(Colors.White, 0.2), 1);
            context.DrawLine(guide, origin - new Vector(8, 0), origin + new Vector(8, 0));
            context.DrawLine(guide, origin - new Vector(0, 8), origin + new Vector(0, 8));
            foreach (var dot in Simulation.Dots())
            {
                var radius = Math.Clamp(dot.Size * 6, 1, 80);
                context.DrawEllipse(new ImmutableSolidColorBrush(Color.FromArgb(dot.A, dot.R, dot.G, dot.B)), null, new Point(origin.X + dot.X, origin.Y + dot.Y), radius, radius);
            }
        }
    }
}
