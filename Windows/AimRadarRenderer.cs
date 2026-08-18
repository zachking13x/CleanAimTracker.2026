using CleanAimTracker.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// CAT_AIM_RADAR: the shared radar drawing. Lives here rather than in a code-behind
    /// so the dashboard card and the full-screen breakdown render the SAME shape from
    /// the same data — a detail view that disagreed with the summary it opened from
    /// would undermine the whole point of the artifact.
    /// </summary>
    internal static class AimRadarRenderer
    {
        internal static readonly Color Accent = Color.FromRgb(0x00, 0xD4, 0xFF);

        /// <summary>
        /// Draw the radar into <paramref name="canvas"/>, sized to its current bounds.
        /// Clears the canvas first, so it is safe to call on every SizeChanged.
        /// </summary>
        internal static void Draw(Canvas canvas,
                                  AimRadarService.AimRadar radar,
                                  FrameworkElement resourceHost,
                                  double labelPadding = 30,
                                  double labelFontSize = 9,
                                  bool showScores = true)
        {
            if (canvas == null || radar == null) return;

            double w = canvas.ActualWidth, h = canvas.ActualHeight;
            canvas.Children.Clear();
            if (w < 80 || h < 80) return;

            double cx = w / 2, cy = h / 2;
            double r  = Math.Min(w, h) / 2 - labelPadding;
            if (r < 20) return;

            var axes = radar.Axes;
            int n = axes.Count;
            if (n < 3) return;

            var grid   = (Brush)resourceHost.FindResource("SeparatorBrush");
            var dim    = (Brush)resourceHost.FindResource("SecondaryText");
            var strong = (Brush)resourceHost.FindResource("PrimaryText");

            Point At(int i, double frac)
            {
                double ang = -Math.PI / 2 + 2 * Math.PI * i / n;
                return new Point(cx + Math.Cos(ang) * r * frac, cy + Math.Sin(ang) * r * frac);
            }

            // Reference rings at 25/50/75/100.
            foreach (double frac in new[] { 0.25, 0.5, 0.75, 1.0 })
            {
                var ring = new Polygon
                {
                    Stroke          = grid,
                    StrokeThickness = frac >= 1.0 ? 1.2 : 0.7,
                    Opacity         = frac >= 1.0 ? 0.9 : 0.45,
                    Fill            = Brushes.Transparent,
                };
                for (int i = 0; i < n; i++) ring.Points.Add(At(i, frac));
                canvas.Children.Add(ring);
            }

            for (int i = 0; i < n; i++)
            {
                canvas.Children.Add(new Line
                {
                    X1 = cx, Y1 = cy,
                    X2 = At(i, 1.0).X, Y2 = At(i, 1.0).Y,
                    Stroke = grid, StrokeThickness = 0.7, Opacity = 0.45,
                });
            }

            // The shape. Axes with no sessions are SKIPPED rather than plotted at the
            // centre — a vertex at zero reads as "you are terrible at this" when the
            // truth is "you have never tried this".
            var shape = new Polygon
            {
                Stroke          = new SolidColorBrush(Accent),
                StrokeThickness = 2,
                StrokeLineJoin  = PenLineJoin.Round,
                Fill            = new SolidColorBrush(Color.FromArgb(0x38, Accent.R, Accent.G, Accent.B)),
            };
            for (int i = 0; i < n; i++)
            {
                if (!axes[i].HasData) continue;
                shape.Points.Add(At(i, Math.Clamp(axes[i].Score, 0, 100) / 100.0));
            }
            if (shape.Points.Count >= 2) canvas.Children.Add(shape);

            double labelWidth = Math.Max(64, labelFontSize * 8);

            for (int i = 0; i < n; i++)
            {
                var a = axes[i];

                if (a.HasData)
                {
                    var p = At(i, Math.Clamp(a.Score, 0, 100) / 100.0);
                    double dotSize = labelFontSize < 11 ? 6 : 9;
                    var dot = new Ellipse
                    {
                        Width = dotSize, Height = dotSize,
                        Fill = new SolidColorBrush(Accent),
                        Opacity = a.IsProvisional ? 0.55 : 1.0,
                    };
                    Canvas.SetLeft(dot, p.X - dotSize / 2);
                    Canvas.SetTop(dot, p.Y - dotSize / 2);
                    canvas.Children.Add(dot);
                }

                var lp    = At(i, 1.0);
                var label = new StackPanel { Width = labelWidth };
                label.Children.Add(new TextBlock
                {
                    Text          = a.Name,
                    FontSize      = labelFontSize,
                    FontWeight    = FontWeights.SemiBold,
                    Foreground    = a.HasData ? strong : dim,
                    Opacity       = a.HasData ? 1.0 : 0.55,
                    TextAlignment = TextAlignment.Center,
                });
                if (showScores)
                {
                    label.Children.Add(new TextBlock
                    {
                        Text          = a.HasData ? (a.IsProvisional ? $"{a.Score:F0}*" : $"{a.Score:F0}") : "—",
                        FontSize      = labelFontSize,
                        Foreground    = a.HasData ? new SolidColorBrush(Accent) : dim,
                        Opacity       = a.HasData ? 1.0 : 0.55,
                        TextAlignment = TextAlignment.Center,
                    });
                }
                label.ToolTip = a.HasData
                    ? $"{a.Blurb} — from {a.SessionCount} session{(a.SessionCount == 1 ? "" : "s")}."
                    : $"{a.Blurb} — no sessions yet.";

                double ox = lp.X - cx, oy = lp.Y - cy;
                double len = Math.Max(1e-6, Math.Sqrt(ox * ox + oy * oy));
                Canvas.SetLeft(label, lp.X + ox / len * (labelPadding * 0.4) - labelWidth / 2);
                Canvas.SetTop(label, lp.Y + oy / len * (labelPadding * 0.33) - labelFontSize * 1.2);
                canvas.Children.Add(label);
            }
        }
    }
}
