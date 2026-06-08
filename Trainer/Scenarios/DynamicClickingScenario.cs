using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// A single moving target — trains prediction, leading, and click timing.
    ///
    /// Variants:
    ///   Standard     — linear movement with wall bouncing
    ///   Bounce       — speed increases slightly after each bounce
    ///   Arc          — sinusoidal arc path (horizontal sine + vertical drift)
    ///   Accelerating — speed ramps up every 5 seconds
    /// </summary>
    public class DynamicClickingScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas     = null!;
        private Random _rng        = null!;
        private double _targetSize;
        private double _baseSpeed;

        private Ellipse? _target;

        // Position (canvas-space left/top, not centre — TargetFactory corrects internally)
        private double _x, _y;
        private double _vx, _vy;

        // Arc variant: time accumulator for sine path
        private double _arcTime;
        private double _arcAmplitude;
        private double _arcFrequency;
        private double _arcBaseY;

        // Accelerating variant
        private double _speedMult = 1.0;
        private long   _lastSpeedUpTick;
        private static readonly long SpeedUpIntervalTicks = 5L * Stopwatch.Frequency;
        private const double SpeedUpFactor      = 1.2;

        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;
        private int    _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak      { get; private set; }

        /// <summary>Canvas-space center of the most recently hit target.</summary>
        public Point LastHitCenter { get; private set; } = new Point(double.NaN, double.NaN);

        public DynamicClickingScenario(string variant = "Standard")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas    = canvas;
            _rng       = rng;
            _targetSize = targetSize;
            _baseSpeed  = moveSpeed > 0 ? moveSpeed : 4.0;

            double w = Math.Max(1, canvas.ActualWidth  - targetSize);
            double h = Math.Max(1, canvas.ActualHeight - targetSize);

            _x = _rng.NextDouble() * w;
            _y = _rng.NextDouble() * h;

            double angle = _rng.NextDouble() * Math.PI * 2;
            _vx = Math.Cos(angle) * _baseSpeed;
            _vy = Math.Sin(angle) * _baseSpeed;

            if (_variant == "Arc")
            {
                _arcBaseY     = _y;
                _arcAmplitude = h * 0.25;
                _arcFrequency = 0.03;
                _arcTime      = 0;
                _vy           = 0;
            }

            _lastSpeedUpTick = Stopwatch.GetTimestamp();
            _speedMult       = 1.0;

            SpawnTarget();
            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            if (_target == null) return;

            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;

            if (_variant == "Arc")
            {
                _arcTime += 1;
                _x += _vx;
                _y = _arcBaseY + Math.Sin(_arcTime * _arcFrequency) * _arcAmplitude;

                // Bounce left/right only; vertical is driven by sine
                if (_x <= 0 || _x + _targetSize >= w)
                {
                    _vx *= -1;
                    _x   = Math.Clamp(_x, 0, w - _targetSize);
                }

                // Arc base slowly drifts
                _arcBaseY += 0.2;
                if (_arcBaseY + _arcAmplitude >= h || _arcBaseY - _arcAmplitude <= 0)
                    _arcBaseY = Math.Clamp(_arcBaseY, _arcAmplitude, h - _arcAmplitude);
            }
            else
            {
                double speed = _baseSpeed * _speedMult;

                if (_variant == "Accelerating")
                {
                    long now = Stopwatch.GetTimestamp();
                    if (now - _lastSpeedUpTick >= SpeedUpIntervalTicks)
                    {
                        _speedMult       *= SpeedUpFactor;
                        _lastSpeedUpTick  = now;
                    }
                }

                double nx = _x + _vx * (_speedMult > 1.0 ? _speedMult : 1.0);
                double ny = _y + _vy * (_speedMult > 1.0 ? _speedMult : 1.0);
                bool bounced = false;

                if (nx <= 0 || nx + _targetSize >= w)
                {
                    _vx = -_vx;
                    nx  = Math.Clamp(nx, 0, w - _targetSize);
                    if (_variant == "Bounce")
                    {
                        _vx *= 1.03;  // slight speed increase on each bounce
                        _vy *= 1.03;
                    }
                    bounced = true;
                }

                if (ny <= 0 || ny + _targetSize >= h)
                {
                    _vy     = -_vy;
                    ny      = Math.Clamp(ny, 0, h - _targetSize);
                    bounced = true;
                    if (_variant == "Bounce" && !bounced)
                    {
                        _vx *= 1.03;
                        _vy *= 1.03;
                    }
                }

                _x = nx;
                _y = ny;
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

                // Teleport to new position on hit
                RespawnTarget();
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

        private void SpawnTarget()
        {
            double w = Math.Max(1, _canvas.ActualWidth  - _targetSize);
            double h = Math.Max(1, _canvas.ActualHeight - _targetSize);
            _target  = TargetFactory.CreateTarget(_targetSize, _x, _y);
            _canvas.Children.Add(_target);
        }

        private void RespawnTarget()
        {
            if (_target != null)
            {
                _canvas.Children.Remove(_target);
                _target = null;
            }

            double w = Math.Max(1, _canvas.ActualWidth  - _targetSize);
            double h = Math.Max(1, _canvas.ActualHeight - _targetSize);
            _x = _rng.NextDouble() * w;
            _y = _rng.NextDouble() * h;

            double angle = _rng.NextDouble() * Math.PI * 2;
            _vx = Math.Cos(angle) * _baseSpeed;
            _vy = Math.Sin(angle) * _baseSpeed;

            if (_variant == "Arc")
            {
                _arcBaseY = _y;
                _arcTime  = 0;
                _vy       = 0;
            }

            SpawnTarget();
        }
    }
}
