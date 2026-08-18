using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer
{
    /// <summary>Which zone of a bot a shot landed in.</summary>
    public enum BotHitZone { None, Body, Head }

    /// <summary>
    /// CAT_BOT_DRILLS: a featureless capsule bot — body pill + head sphere —
    /// the KovaaK's/Range visual language. The head is the crit zone.
    ///
    /// Positioned on a Canvas via Canvas.SetLeft/SetTop like every other target;
    /// hit testing is pure arithmetic on the stored layout, so RenderTransform
    /// animations (spawn pop, breathe, damage flash) never affect gameplay.
    ///
    /// Scale simulates distance: 1.0 = near, ~0.55 = far.
    /// </summary>
    public class BotTarget : Canvas
    {
        public double Scale { get; }
        public double HeadRadius { get; }
        public double BodyWidth { get; }
        public double BodyHeight { get; }
        private readonly double _gap;

        /// <summary>Remaining body shots before this bot dies (head is always lethal).</summary>
        public int BodyHp { get; set; } = 1;

        private readonly Ellipse _head;
        private readonly Border _body;
        private readonly Color _accent;

        /// <summary>Total element size (used by movers/spawn clamping).</summary>
        public double TotalWidth  => Math.Max(BodyWidth, HeadRadius * 2);
        public double TotalHeight => HeadRadius * 2 + _gap + BodyHeight;

        public BotTarget(double baseSize, double scale, Color accent)
        {
            Scale   = scale;
            _accent = accent;

            HeadRadius = baseSize * 0.42 * scale;
            BodyWidth  = baseSize * 1.15 * scale;
            BodyHeight = baseSize * 2.30 * scale;
            _gap       = baseSize * 0.12 * scale;

            Width  = TotalWidth;
            Height = TotalHeight;
            IsHitTestVisible = false;   // clicks land on the drill canvas, zones resolved in code

            var glow = new DropShadowEffect { Color = accent, BlurRadius = baseSize * scale, ShadowDepth = 0, Opacity = 0.45 };

            // Body capsule
            _body = new Border
            {
                Width           = BodyWidth,
                Height          = BodyHeight,
                CornerRadius    = new CornerRadius(BodyWidth / 2),
                Background      = new SolidColorBrush(Color.FromArgb(0x2E, accent.R, accent.G, accent.B)),
                BorderBrush     = new SolidColorBrush(accent),
                BorderThickness = new Thickness(Math.Max(2.0, baseSize * 0.09 * scale)),
                Effect          = glow,
            };
            SetLeft(_body, (Width - BodyWidth) / 2);
            SetTop(_body, HeadRadius * 2 + _gap);
            Children.Add(_body);

            // Head — brighter, white-hot core: this is the crit zone
            var headFill = new RadialGradientBrush();
            headFill.GradientStops.Add(new GradientStop(Color.FromRgb(0xD2, 0xF5, 0xFF), 0.0));
            headFill.GradientStops.Add(new GradientStop(accent, 0.75));
            headFill.GradientStops.Add(new GradientStop(Color.FromArgb(0xE0, accent.R, accent.G, accent.B), 1.0));

            _head = new Ellipse
            {
                Width  = HeadRadius * 2,
                Height = HeadRadius * 2,
                Fill   = headFill,
                Effect = new DropShadowEffect { Color = accent, BlurRadius = HeadRadius * 2.2, ShadowDepth = 0, Opacity = 0.65 },
            };
            SetLeft(_head, (Width - HeadRadius * 2) / 2);
            SetTop(_head, 0);
            Children.Add(_head);

            TargetFactory.ApplySpawnAndBreathe(this);
        }

        /// <summary>Canvas-space center of the head (for burst FX / telemetry).</summary>
        public Point HeadCenter(double left, double top)
            => new Point(left + Width / 2, top + HeadRadius);

        /// <summary>Canvas-space center of mass (body middle).</summary>
        public Point BodyCenter(double left, double top)
            => new Point(left + Width / 2, top + HeadRadius * 2 + _gap + BodyHeight / 2);

        /// <summary>
        /// Resolve a canvas-space click against this bot placed at (left, top).
        /// Head is checked first — a click in the head/body overlap counts as head.
        /// </summary>
        public BotHitZone HitTest(Point click, double left, double top)
        {
            double cx = left + Width / 2;

            // Head: circle
            double hy = top + HeadRadius;
            double hdx = click.X - cx, hdy = click.Y - hy;
            if (hdx * hdx + hdy * hdy <= HeadRadius * HeadRadius)
                return BotHitZone.Head;

            // Body: capsule = rectangle + rounded caps ≈ rounded-rect test via
            // clamped-point distance (exact for the capsule shape)
            double bTop = top + HeadRadius * 2 + _gap;
            double r    = BodyWidth / 2;
            double coreTop = bTop + r, coreBottom = bTop + BodyHeight - r;
            double nearestY = Math.Clamp(click.Y, coreTop, coreBottom);
            double bdx = click.X - cx, bdy = click.Y - nearestY;
            if (bdx * bdx + bdy * bdy <= r * r)
                return BotHitZone.Body;

            return BotHitZone.None;
        }

        /// <summary>Brief white flash on a non-lethal body hit — "damage registered".</summary>
        public void DamageFlash()
        {
            try
            {
                var white = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));
                var back  = new SolidColorBrush(Color.FromArgb(0x2E, _accent.R, _accent.G, _accent.B));
                _body.Background = white;
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
                t.Tick += (s, _) => { ((System.Windows.Threading.DispatcherTimer)s!).Stop(); _body.Background = back; };
                t.Start();
            }
            catch { }
        }

        /// <summary>
        /// Faint motion-trail ghost at the bot's current position — a soft ellipse
        /// at the body center that fades out. Cheap enough to drop every ~90 ms.
        /// </summary>
        public static void TrailGhost(Canvas canvas, Point bodyCenter, double size, Color accent)
        {
            try
            {
                var g = new Ellipse
                {
                    Width = size, Height = size * 1.6, IsHitTestVisible = false,
                    Fill = new SolidColorBrush(Color.FromArgb(0x16, accent.R, accent.G, accent.B)),
                };
                Canvas.SetLeft(g, bodyCenter.X - size / 2);
                Canvas.SetTop (g, bodyCenter.Y - size * 0.8);
                canvas.Children.Add(g);
                var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(260));
                fade.Completed += (_, _) => { try { canvas.Children.Remove(g); } catch { } };
                g.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            catch { }
        }
    }
}
