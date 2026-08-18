using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// CAT_VISUAL_JUICE: crisp, SOUND-OFF-FRIENDLY hit/miss feedback on existing hit events.
    /// Pure WPF animation, no new art assets, self-cleaning, non-blocking. Composable so a
    /// caller can take the full hit package or just pieces (drills already draw their own rings).
    /// </summary>
    public static class HitFeedback
    {
        private static readonly Brush MutedRed = new SolidColorBrush(Color.FromRgb(0xE0, 0x5A, 0x4A));

        /// <summary>Full hit package: accent ring + hitmarker + floating score.</summary>
        public static void Hit(Canvas canvas, Point at, Brush accent, int points = 0)
        {
            if (canvas == null) return;
            try
            {
                Ring(canvas, at, accent);
                Hitmarker(canvas, at);
                if (points > 0) FloatScore(canvas, at, accent, points);
            }
            catch { }
        }

        /// <summary>Back-compat: just the expanding accent ring.</summary>
        public static void Pop(Canvas canvas, Point at, Brush color, double size = 40)
            => Ring(canvas, at, color, size);

        /// <summary>Miss cue: a brief muted-red ✕ at the click point — clear, not punishing.</summary>
        public static void Miss(Canvas canvas, Point at)
        {
            if (canvas == null) return;
            try
            {
                var x = new TextBlock
                {
                    Text = "✕", FontSize = 22, FontWeight = FontWeights.Bold,
                    Foreground = MutedRed, IsHitTestVisible = false, Opacity = 0.85,
                };
                x.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(x, at.X - x.DesiredSize.Width / 2);
                Canvas.SetTop(x, at.Y - x.DesiredSize.Height / 2);
                canvas.Children.Add(x);
                var fade = new DoubleAnimation(0.85, 0.0, TimeSpan.FromMilliseconds(320))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
                fade.Completed += (_, _) => { try { canvas.Children.Remove(x); } catch { } };
                x.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            catch { }
        }

        // ── Pieces ───────────────────────────────────────────────────────────
        public static void Ring(Canvas canvas, Point at, Brush color, double size = 40)
        {
            if (canvas == null) return;
            try
            {
                var ring = new Ellipse
                {
                    Width = size, Height = size, Stroke = color, StrokeThickness = 3,
                    Fill = Brushes.Transparent, IsHitTestVisible = false, Opacity = 0.9,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(0.5, 0.5),
                };
                Canvas.SetLeft(ring, at.X - size / 2);
                Canvas.SetTop(ring, at.Y - size / 2);
                canvas.Children.Add(ring);
                var dur = TimeSpan.FromMilliseconds(280);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var scale = (ScaleTransform)ring.RenderTransform;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.5, 1.9, dur) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.5, 1.9, dur) { EasingFunction = ease });
                var fade = new DoubleAnimation(0.9, 0.0, dur) { EasingFunction = ease };
                fade.Completed += (_, _) => { try { canvas.Children.Remove(ring); } catch { } };
                ring.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            catch { }
        }

        /// <summary>The universal "you got it" — four short lines flashing out from the point.</summary>
        public static void Hitmarker(Canvas canvas, Point at)
        {
            if (canvas == null) return;
            try
            {
                var white = new SolidColorBrush(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF));
                (double dx, double dy)[] dirs = { (-1, -1), (1, -1), (-1, 1), (1, 1) };
                const double inner = 5, outer = 13;
                foreach (var (dx, dy) in dirs)
                {
                    var line = new Line
                    {
                        X1 = at.X + dx * inner, Y1 = at.Y + dy * inner,
                        X2 = at.X + dx * outer, Y2 = at.Y + dy * outer,
                        Stroke = white, StrokeThickness = 2.4, StrokeEndLineCap = PenLineCap.Round,
                        IsHitTestVisible = false, Opacity = 0.95,
                    };
                    canvas.Children.Add(line);
                    var fade = new DoubleAnimation(0.95, 0.0, TimeSpan.FromMilliseconds(150))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    fade.Completed += (_, _) => { try { canvas.Children.Remove(line); } catch { } };
                    line.BeginAnimation(UIElement.OpacityProperty, fade);
                }
            }
            catch { }
        }

        /// <summary>KovaaK's-style "+N" that floats up and fades — the score builds even with sound off.</summary>
        public static void FloatScore(Canvas canvas, Point at, Brush brush, int points)
        {
            if (canvas == null || points <= 0) return;
            try
            {
                var tb = new TextBlock
                {
                    Text = "+" + points, FontSize = 16, FontWeight = FontWeights.Bold,
                    Foreground = brush, IsHitTestVisible = false,
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double startY = at.Y - 10, left = at.X - tb.DesiredSize.Width / 2;
                Canvas.SetLeft(tb, left);
                Canvas.SetTop(tb, startY);
                canvas.Children.Add(tb);
                var dur = TimeSpan.FromMilliseconds(650);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var rise = new DoubleAnimation(startY, startY - 34, dur) { EasingFunction = ease };
                var fade = new DoubleAnimation(1.0, 0.0, dur) { EasingFunction = ease };
                fade.Completed += (_, _) => { try { canvas.Children.Remove(tb); } catch { } };
                tb.BeginAnimation(Canvas.TopProperty, rise);
                tb.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            catch { }
        }
    }
}
