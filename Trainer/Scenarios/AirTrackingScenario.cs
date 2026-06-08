using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// Continuous tracking target that follows air-movement style paths.
    /// Clicking the target while tracking it is the primary hit gesture.
    /// Scoring works on a time-on-target model: the player gets a point for
    /// every 300 ms they keep the mouse within the target hitbox, simulated
    /// via click-to-confirm hits.
    ///
    /// Variants:
    ///   Diagonal   — constant diagonal movement, tests X + Y axis balance
    ///   Parabolic  — gravity-like arc; Y velocity accelerates, resets at bottom
    ///   Jetpack    — random vertical speed bursts test vertical tracking stability
    ///   Falling    — downward drift with horizontal oscillation
    /// </summary>
    public class AirTrackingScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas     = null!;
        private Random _rng        = null!;
        private double _targetSize;
        private double _moveSpeed;

        private Ellipse? _target;
        private double   _x, _y;
        private double   _vx, _vy;

        // Parabolic: gravity accumulator
        private const double Gravity = 0.15;

        // Jetpack: burst timer
        private long   _nextBurstTick;
        private static readonly long BurstIntervalTicks = (long)(1.5 * Stopwatch.Frequency);

        // Reaction timer — started when target spawns / after a hit
        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;
        private int    _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak      { get; private set; }

        /// <summary>Canvas-space centre of the tracking target at this frame.</summary>
        public Point CurrentTargetCenter =>
            _target == null
                ? new Point(double.NaN, double.NaN)
                : new Point(_x + _targetSize / 2, _y + _targetSize / 2);

        /// <summary>Canvas-space center of the most recently hit tracking target.</summary>
        public Point LastHitCenter { get; private set; } = new Point(double.NaN, double.NaN);

        public AirTrackingScenario(string variant = "Diagonal")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas    = canvas;
            _rng       = rng;
            _targetSize = targetSize;
            _moveSpeed  = moveSpeed > 0 ? moveSpeed : 3.5;

            double w = Math.Max(1, canvas.ActualWidth  - targetSize);
            double h = Math.Max(1, canvas.ActualHeight - targetSize);
            _x = w / 2;
            _y = h / 4;

            InitVelocity();
            _nextBurstTick = Stopwatch.GetTimestamp() + BurstIntervalTicks;

            _target = TargetFactory.CreateTrackingTarget(targetSize, _x, _y);
            canvas.Children.Add(_target);
            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            if (_target == null) return;

            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;
            long   now = Stopwatch.GetTimestamp();

            switch (_variant)
            {
                case "Parabolic":
                    _vy += Gravity;
                    _x  += _vx;
                    _y  += _vy;

                    if (_x <= 0 || _x + _targetSize >= w)
                    {
                        _vx = -_vx;
                        _x  = Math.Clamp(_x, 0, w - _targetSize);
                    }
                    // Reset when target hits bottom — simulate a new throw
                    if (_y + _targetSize >= h)
                    {
                        _y  = 0;
                        _vy = -Math.Abs(_vy) * 0.8 + _moveSpeed;  // re-launch upward
                        _vy = Math.Max(-_moveSpeed * 3, _vy);
                        _x  = _rng.NextDouble() * Math.Max(1, w - _targetSize);
                    }
                    break;

                case "Jetpack":
                    if (now >= _nextBurstTick)
                    {
                        // Apply a random vertical burst
                        _vy             = -(_moveSpeed * 2.5 + _rng.NextDouble() * _moveSpeed);
                        _nextBurstTick  = now + BurstIntervalTicks;
                    }
                    _vy += Gravity * 0.5;   // lighter gravity between bursts
                    _x  += _vx;
                    _y  += _vy;

                    if (_x <= 0 || _x + _targetSize >= w) { _vx = -_vx; _x = Math.Clamp(_x, 0, w - _targetSize); }
                    if (_y <= 0 || _y + _targetSize >= h) { _vy = -_vy; _y = Math.Clamp(_y, 0, h - _targetSize); }
                    break;

                case "Falling":
                    // Constant downward drift + horizontal oscillation
                    _x += _vx;
                    _y += _moveSpeed * 0.8;   // steady fall

                    if (_x <= 0 || _x + _targetSize >= w) { _vx = -_vx; _x = Math.Clamp(_x, 0, w - _targetSize); }

                    // Wrap bottom to top
                    if (_y + _targetSize >= h)
                    {
                        _y = 0;
                        _x = _rng.NextDouble() * Math.Max(1, w - _targetSize);
                    }
                    break;

                default: // Diagonal
                    _x += _vx;
                    _y += _vy;

                    if (_x <= 0 || _x + _targetSize >= w) { _vx = -_vx; _x = Math.Clamp(_x, 0, w - _targetSize); }
                    if (_y <= 0 || _y + _targetSize >= h) { _vy = -_vy; _y = Math.Clamp(_y, 0, h - _targetSize); }
                    break;
            }

            Canvas.SetLeft(_target, _x);
            Canvas.SetTop(_target, _y);
        }

        public bool HandleClick(Point clickPos)
        {
            if (_target == null) return false;

            double cx = Canvas.GetLeft(_target) + _targetSize / 2;
            double cy = Canvas.GetTop(_target)  + _targetSize / 2;
            double dx = clickPos.X - cx;
            double dy = clickPos.Y - cy;

            if (dx * dx + dy * dy <= (_targetSize / 2) * (_targetSize / 2))
            {
                LastHitCenter = new Point(cx, cy);
                Hits++;
                _streak++;
                MaxStreak = Math.Max(MaxStreak, _streak);

                double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
                _totalReactionMs += reaction;
                if (reaction < BestReactionMs) BestReactionMs = reaction;
                _reactionTimer.Restart();
                return true;
            }

            Misses++;
            _streak = 0;
            return false;
        }

        public void Stop(Canvas canvas)
        {
            if (_target != null)
            {
                canvas.Children.Remove(_target);
                _target = null;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private void InitVelocity()
        {
            switch (_variant)
            {
                case "Diagonal":
                    // 45-degree diagonal
                    double sign = _rng.NextDouble() > 0.5 ? 1 : -1;
                    _vx = _moveSpeed * sign;
                    _vy = _moveSpeed * (_rng.NextDouble() > 0.5 ? 1 : -1);
                    break;

                case "Parabolic":
                    _vx = _moveSpeed * (_rng.NextDouble() > 0.5 ? 1 : -1);
                    _vy = -_moveSpeed * 2;   // initial upward launch
                    break;

                case "Jetpack":
                    _vx = _moveSpeed * (_rng.NextDouble() > 0.5 ? 1 : -1);
                    _vy = 0;
                    break;

                case "Falling":
                    _vx = _moveSpeed * 0.6 * (_rng.NextDouble() > 0.5 ? 1 : -1);
                    _vy = 0;
                    break;

                default:
                    _vx = _moveSpeed;
                    _vy = _moveSpeed * 0.7;
                    break;
            }
        }
    }
}
