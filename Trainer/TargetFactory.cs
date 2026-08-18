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
            ApplySpawnAndBreathe(el, fadeMs);
            return el;
        }

        /// <summary>
        /// CAT_GAME_FEEL: targets scale in with a slight overshoot, then breathe
        /// (±3% scale) so the field never looks frozen. Hit detection uses
        /// Width/Canvas position, which RenderTransform never touches — zero
        /// gameplay impact.
        /// </summary>
        public static void ApplySpawnAndBreathe(FrameworkElement el, double spawnMs = 150)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            var scale = new ScaleTransform(0.45, 0.45);
            el.RenderTransform = scale;

            var pop = new DoubleAnimation(0.45, 1.0, TimeSpan.FromMilliseconds(Math.Max(90, spawnMs)))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.55 }
            };
            pop.Completed += (_, _) =>
            {
                var breathe = new DoubleAnimation(1.0, 1.03, TimeSpan.FromMilliseconds(1100))
                {
                    AutoReverse    = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, breathe);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, breathe);
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }

        /// <summary>
        /// CAT_GAME_FEEL: kill burst — six arc shards flying outward from the kill
        /// point plus a brief center flash. Gold for headshots, accent otherwise.
        /// Self-cleaning; safe to call from any scenario or the host window.
        /// </summary>
        public static void Burst(Canvas canvas, Point at, Color accent, bool headshot = false)
        {
            if (canvas == null) return;
            try
            {
                var c = headshot ? Color.FromRgb(0xF5, 0xC8, 0x42) : accent;

                // Center flash
                var flash = new Ellipse
                {
                    Width = 18, Height = 18, IsHitTestVisible = false,
                    Fill = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                    Effect = new DropShadowEffect { Color = c, BlurRadius = 26, ShadowDepth = 0, Opacity = 0.9 }
                };
                Canvas.SetLeft(flash, at.X - 9);
                Canvas.SetTop (flash, at.Y - 9);
                canvas.Children.Add(flash);
                var flashFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180));
                flashFade.Completed += (_, _) => { try { canvas.Children.Remove(flash); } catch { } };
                flash.BeginAnimation(UIElement.OpacityProperty, flashFade);

                // Six shards
                var rng = _burstRng;
                double baseAngle = rng.NextDouble() * Math.PI / 3;
                for (int i = 0; i < 6; i++)
                {
                    double a  = baseAngle + Math.PI / 3 * i;
                    double dx = Math.Cos(a), dy = Math.Sin(a);

                    var shard = new System.Windows.Shapes.Line
                    {
                        X1 = at.X + dx * 8,  Y1 = at.Y + dy * 8,
                        X2 = at.X + dx * 16, Y2 = at.Y + dy * 16,
                        Stroke = new SolidColorBrush(c), StrokeThickness = 3.2,
                        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                        IsHitTestVisible = false, Opacity = 0.95,
                    };
                    canvas.Children.Add(shard);

                    var dur  = TimeSpan.FromMilliseconds(220);
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    shard.BeginAnimation(System.Windows.Shapes.Line.X1Property, new DoubleAnimation(shard.X1, at.X + dx * 26, dur) { EasingFunction = ease });
                    shard.BeginAnimation(System.Windows.Shapes.Line.Y1Property, new DoubleAnimation(shard.Y1, at.Y + dy * 26, dur) { EasingFunction = ease });
                    shard.BeginAnimation(System.Windows.Shapes.Line.X2Property, new DoubleAnimation(shard.X2, at.X + dx * 40, dur) { EasingFunction = ease });
                    shard.BeginAnimation(System.Windows.Shapes.Line.Y2Property, new DoubleAnimation(shard.Y2, at.Y + dy * 40, dur) { EasingFunction = ease });
                    var fade = new DoubleAnimation(0.95, 0.0, dur) { EasingFunction = ease };
                    fade.Completed += (_, _) => { try { canvas.Children.Remove(shard); } catch { } };
                    shard.BeginAnimation(UIElement.OpacityProperty, fade);
                }
            }
            catch { /* FX are never allowed to break gameplay */ }
        }

        private static readonly Random _burstRng = new();

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
