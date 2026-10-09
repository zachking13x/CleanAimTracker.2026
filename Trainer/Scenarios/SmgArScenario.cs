using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// SMG / AR scenario — multiple moving targets with periodic direction changes.
    /// CAT_AUTO_WEAPONS: this is an AUTOMATIC weapon — HOLD the left button to spray
    /// (the host window synthesizes shots at a fixed cadence via IsAutoFire, exactly
    /// like Track the Head). Targets take 3 shots to kill, so accuracy measures
    /// SPRAY CONTROL: staying on a moving target through a burst, not click timing.
    /// Variants:
    ///   Standard — two simultaneous targets, independent random movement
    ///   Spray    — three simultaneous targets (falls back to two on very small canvases)
    ///   Strafe   — two targets moving left-right in sync, fixed vertical rows
    /// </summary>
    public class SmgArScenario : IAimScenario
    {
        private const int ShotsToKill = 3;

        private readonly string _variant;

        private Canvas _canvas    = null!;
        private Random _rng       = null!;
        private double _targetSize;
        private double _moveSpeed;
        private int    _dirChangeMs;   // Standard / Spray: per-target direction change

        // ── Standard / Spray shared target state ─────────────────────
        private readonly List<Ellipse>               _targets    = new();
        private readonly List<(double cx, double cy)> _centers   = new();
        private readonly List<(double dx, double dy)> _velocities = new();
        private readonly List<Stopwatch>             _dirTimers  = new();
        private readonly List<int>                   _hp         = new();

        // ── Strafe-specific ───────────────────────────────────────────
        private double            _strafeDx;        // shared horizontal velocity
        private int               _strafeIntervalMs;
        private readonly Stopwatch _strafeTimer = new();
        private readonly List<double> _fixedYPositions = new();  // one per target row

        private int _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        // CAT_AUTO_WEAPONS honesty: shots fire on a synthetic hold cadence, so a
        // per-shot "reaction" number would be fabricated — same policy as HeadTrack.
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => 0;
        public int    MaxStreak      { get; private set; }
        public bool   IsAutoFire     => true;

        public SmgArScenario(string variant = "Standard")
        {
            _variant = variant;
        }

        // ─────────────────────────────────────────────────────────────
        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas    = canvas;
            _rng       = rng;
            _targetSize = Math.Max(10, targetSize * 1.1);
            _moveSpeed  = moveSpeed;
            _dirChangeMs = moveSpeed < 2 ? 1200 : moveSpeed < 3 ? 1000 : moveSpeed < 5 ? 800 : 600;

            switch (_variant)
            {
                case "Spray":
                {
                    // Three targets unless canvas is too small
                    int count = CanFitThreeTargets(canvas) ? 3 : 2;
                    for (int i = 0; i < count; i++) SpawnFreeTarget();
                    break;
                }
                case "Strafe":
                {
                    _strafeDx          = moveSpeed;
                    _strafeIntervalMs  = moveSpeed < 2 ? 1000 : moveSpeed < 3 ? 700 : moveSpeed < 5 ? 500 : 350;
                    _strafeTimer.Restart();

                    // Two fixed vertical rows: top 35 % and bottom 65 % of canvas
                    double midRow0 = canvas.ActualHeight * 0.35;
                    double midRow1 = canvas.ActualHeight * 0.65;
                    _fixedYPositions.Add(midRow0);
                    _fixedYPositions.Add(midRow1);

                    SpawnStrafeTarget(0);
                    SpawnStrafeTarget(1);
                    break;
                }
                default: // Standard
                    SpawnFreeTarget();
                    SpawnFreeTarget();
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        public void Update(Canvas canvas)
        {
            switch (_variant)
            {
                case "Strafe": UpdateStrafe(canvas); break;
                default:       UpdateFree(canvas);   break;  // Standard + Spray
            }
        }

        // ─────────────────────────────────────────────────────────────
        public bool HandleClick(Point clickPos)
        {
            return _variant == "Strafe"
                ? HandleClickStrafe(clickPos)
                : HandleClickFree(clickPos);
        }

        // ─────────────────────────────────────────────────────────────
        public void Stop(Canvas canvas)
        {
            foreach (var t in _targets) canvas.Children.Remove(t);
            _targets.Clear(); _centers.Clear(); _velocities.Clear(); _dirTimers.Clear();
            _hp.Clear();
            _fixedYPositions.Clear();
        }

        // ══ STANDARD + SPRAY (free movement) ════════════════════════

        private void UpdateFree(Canvas canvas)
        {
            double w    = canvas.ActualWidth;
            double h    = canvas.ActualHeight;
            double half = _targetSize / 2;

            for (int i = 0; i < _targets.Count; i++)
            {
                if (_dirTimers[i].ElapsedMilliseconds >= _dirChangeMs)
                {
                    _velocities[i] = RandomVelocity();
                    _dirTimers[i].Restart();
                }

                var (dx, dy) = _velocities[i];
                var (cx, cy) = _centers[i];
                cx += dx * FrameClock.Scale; cy += dy * FrameClock.Scale;

                if (cx - half <= 0 || cx + half >= w) { dx = -dx; cx = Math.Clamp(cx, half, w - half); }
                if (cy - half <= 0 || cy + half >= h) { dy = -dy; cy = Math.Clamp(cy, half, h - half); }

                _velocities[i] = (dx, dy);
                _centers[i]    = (cx, cy);
                Canvas.SetLeft(_targets[i], cx - half);
                Canvas.SetTop (_targets[i], cy - half);
            }
        }

        private bool HandleClickFree(Point clickPos)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var (cx, cy) = _centers[i];
                double dx = clickPos.X - cx, dy = clickPos.Y - cy, r = _targetSize / 2;

                if (dx * dx + dy * dy <= r * r)
                {
                    RecordHit();

                    // CAT_AUTO_WEAPONS: 3 shots to kill — spray through the target.
                    _hp[i]--;
                    if (_hp[i] > 0)
                    {
                        DamageFlash(_targets[i]);
                        return true;
                    }

                    TargetFactory.Burst(_canvas, new Point(cx, cy), Color.FromRgb(0x00, 0xD4, 0xFF));
                    _canvas.Children.Remove(_targets[i]);
                    _targets.RemoveAt(i); _centers.RemoveAt(i);
                    _velocities.RemoveAt(i); _dirTimers.RemoveAt(i); _hp.RemoveAt(i);

                    SpawnFreeTarget();   // always bring the count back up
                    return true;
                }
            }

            Misses++; _streak = 0;
            return false;
        }

        private void SpawnFreeTarget()
        {
            double w    = Math.Max(1, _canvas.ActualWidth);
            double h    = Math.Max(1, _canvas.ActualHeight);
            double half = _targetSize / 2;

            double cx, cy;
            int attempt = 0;
            do
            {
                cx = half + _rng.NextDouble() * Math.Max(0, w - _targetSize);
                cy = half + _rng.NextDouble() * Math.Max(0, h - _targetSize);
                attempt++;
            }
            while (attempt < 20 && IsOverlapping(cx, cy, 80));

            AddTarget(cx, cy, RandomVelocity());
        }

        // ══ STRAFE ═══════════════════════════════════════════════════

        private void UpdateStrafe(Canvas canvas)
        {
            double w    = canvas.ActualWidth;
            double half = _targetSize / 2;

            // Synchronized direction reversal
            if (_strafeTimer.ElapsedMilliseconds >= _strafeIntervalMs)
            {
                _strafeDx = -_strafeDx;
                _strafeTimer.Restart();
                for (int i = 0; i < _velocities.Count; i++)
                    _velocities[i] = (_strafeDx, 0);
            }

            for (int i = 0; i < _targets.Count; i++)
            {
                var (cx, cy) = _centers[i];
                cx += _strafeDx * FrameClock.Scale;

                // Bounce at horizontal edges (also updates shared direction)
                if (cx - half <= 0)  { cx = half;      _strafeDx =  Math.Abs(_strafeDx); }
                if (cx + half >= w)  { cx = w - half;  _strafeDx = -Math.Abs(_strafeDx); }

                _centers[i]    = (cx, cy);              // Y is immutable for Strafe
                _velocities[i] = (_strafeDx, 0);
                Canvas.SetLeft(_targets[i], cx - half);
                Canvas.SetTop (_targets[i], cy - half);
            }
        }

        private bool HandleClickStrafe(Point clickPos)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var (cx, cy) = _centers[i];
                double dx = clickPos.X - cx, dy = clickPos.Y - cy, r = _targetSize / 2;

                if (dx * dx + dy * dy <= r * r)
                {
                    RecordHit();

                    // CAT_AUTO_WEAPONS: 3 shots to kill — spray through the target.
                    _hp[i]--;
                    if (_hp[i] > 0)
                    {
                        DamageFlash(_targets[i]);
                        return true;
                    }

                    // Find which fixed row this target belongs to
                    int rowIndex = 0;
                    for (int ri = 0; ri < _fixedYPositions.Count; ri++)
                        if (Math.Abs(_fixedYPositions[ri] - cy) < 1.0) { rowIndex = ri; break; }

                    TargetFactory.Burst(_canvas, new Point(cx, cy), Color.FromRgb(0x00, 0xD4, 0xFF));
                    _canvas.Children.Remove(_targets[i]);
                    _targets.RemoveAt(i); _centers.RemoveAt(i);
                    _velocities.RemoveAt(i); _dirTimers.RemoveAt(i); _hp.RemoveAt(i);

                    // Respawn at same vertical row, current group horizontal position
                    double groupCx = _centers.Count > 0 ? _centers[0].cx : _canvas.ActualWidth / 2;
                    SpawnStrafeTarget(rowIndex, groupCx);
                    return true;
                }
            }

            Misses++; _streak = 0;
            return false;
        }

        private void SpawnStrafeTarget(int rowIndex, double? startCx = null)
        {
            double w    = Math.Max(1, _canvas.ActualWidth);
            double half = _targetSize / 2;
            double cy   = rowIndex < _fixedYPositions.Count ? _fixedYPositions[rowIndex] : _canvas.ActualHeight * 0.5;
            double cx   = startCx ?? half + _rng.NextDouble() * Math.Max(0, w - _targetSize);

            cx = Math.Clamp(cx, half, w - half);
            AddTarget(cx, cy, (_strafeDx, 0));
        }

        // ── Shared helpers ───────────────────────────────────────────

        private void AddTarget(double cx, double cy, (double dx, double dy) vel)
        {
            var target = TargetFactory.CreateTarget(_targetSize, cx, cy,
                new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF)));
            _canvas.Children.Add(target);
            _targets.Add(target);
            _centers.Add((cx, cy));
            _velocities.Add(vel);
            var sw = new Stopwatch(); sw.Restart();
            _dirTimers.Add(sw);
            _hp.Add(ShotsToKill);
        }

        private void RecordHit()
        {
            Hits++; _streak++;
            MaxStreak = Math.Max(MaxStreak, _streak);
        }

        /// <summary>Brief bright pulse on a non-lethal spray hit — "damage registered".</summary>
        private static void DamageFlash(Ellipse target)
        {
            try
            {
                var pulse = new System.Windows.Media.Animation.DoubleAnimation(0.45, 1.0,
                    TimeSpan.FromMilliseconds(120));
                target.BeginAnimation(UIElement.OpacityProperty, pulse);
            }
            catch { /* FX never break gameplay */ }
        }

        private bool IsOverlapping(double cx, double cy, double minDist)
        {
            foreach (var (ox, oy) in _centers)
            {
                double dx = cx - ox, dy = cy - oy;
                if (dx * dx + dy * dy < minDist * minDist) return true;
            }
            return false;
        }

        private (double dx, double dy) RandomVelocity()
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            return (_moveSpeed * Math.Cos(angle), _moveSpeed * Math.Sin(angle));
        }

        private bool CanFitThreeTargets(Canvas canvas)
            => canvas.ActualWidth * canvas.ActualHeight > 3 * 80 * 80 * Math.PI;
    }
}
