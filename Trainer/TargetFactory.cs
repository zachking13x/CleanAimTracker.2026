using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer
{
    /// <summary>
    /// Three-layer target system:
    ///   Layer 1 — outer glow via DropShadowEffect (ShadowDepth=0, BlurRadius=size)
    ///   Layer 2 — main body: RadialGradient white core → scenario accent at edge
    ///   Layer 3 — bright highlight: simulated by gradient center stop (near-white at 0.0)
    ///
    /// Return type remains Ellipse — no breaking changes to IAimScenario callers.
    /// Hit detection (target.Width, Canvas.GetLeft/Top) is unaffected.
    /// </summary>
    public static class TargetFactory
    {
        // ── Core builder ─────────────────────────────────────────────────────────
        private static Ellipse Build(double size, double x, double y, Color accent,
                                     double glowOpacity = 0.55, double fadeMs = 80)
        {
            // Layer 2+3: RadialGradient — near-white core → accent body → slightly deeper edge
            var highlight = Color.FromArgb(255,
                (byte)Math.Min(255, accent.R + 90),
                (byte)Math.Min(255, accent.G + 90),
                (byte)Math.Min(255, accent.B + 90));

            var fill = new RadialGradientBrush();
            fill.GradientStops.Add(new GradientStop(highlight,                                  0.0));
            fill.GradientStops.Add(new GradientStop(accent,                                     0.55));
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(210, accent.R, accent.G,
                                                                         accent.B),             1.0));

            // Layer 1: glow ring via centered drop shadow
            var el = new Ellipse
            {
                Width           = size,
                Height          = size,
                Fill            = fill,
                StrokeThickness = 0,
                Opacity         = 0,
                Effect          = new DropShadowEffect
                {
                    Color       = accent,
                    BlurRadius  = size,
                    ShadowDepth = 0,
                    Opacity     = glowOpacity
                }
            };

            Canvas.SetLeft(el, x - size / 2);
            Canvas.SetTop (el, y - size / 2);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeMs));
            el.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            return el;
        }

        // ── Public API — same signatures as before ────────────────────────────────

        /// <summary>General-purpose target. Defaults to AccentPrimary cyan.</summary>
        public static Ellipse CreateTarget(
            double size, double x, double y,
            Brush? fill = null, double fadeMs = 80)
        {
            // Extract color from brush if provided, otherwise default to spec cyan
            var c = (fill as SolidColorBrush)?.Color ?? Color.FromRgb(0x00, 0xD4, 0xFF);
            return Build(size, x, y, c, 0.55, fadeMs);
        }

        /// <summary>Tracking target — AccentWarm orange.</summary>
        public static Ellipse CreateTrackingTarget(double size, double x, double y)
            => Build(size, x, y, Color.FromRgb(0xFF, 0xB3, 0x47), 0.50);

        /// <summary>Inactive switch target — muted grey, reduced opacity.</summary>
        public static Ellipse CreateInactiveSwitchTarget(double size, double x, double y)
        {
            var el = new Ellipse
            {
                Width           = size,
                Height          = size,
                Fill            = new SolidColorBrush(Color.FromArgb(80, 100, 100, 100)),
                StrokeThickness = 0,
                Opacity         = 0
            };
            Canvas.SetLeft(el, x - size / 2);
            Canvas.SetTop (el, y - size / 2);
            var fadeIn = new DoubleAnimation(0, 0.45, TimeSpan.FromMilliseconds(80));
            el.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            return el;
        }

        /// <summary>Active switch target — AccentPrimary cyan.</summary>
        public static Ellipse CreateActiveSwitchTarget(double size, double x, double y)
            => Build(size, x, y, Color.FromRgb(0x00, 0xD4, 0xFF), 0.55);
    }
}
