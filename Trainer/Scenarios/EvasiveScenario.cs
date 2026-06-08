using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// A target that actively attempts to evade the cursor.
    /// The cursor position is approximated from the last click location; between clicks
    /// the target drifts away from where it last saw the cursor.
    ///
    /// Variants:
    ///   Standard   — moderate evasion speed, bounces off walls
    ///   Aggressive — higher evasion speed and tighter direction changes
    ///   Predictive — randomises evasion every 0.8 s to simulate unpredictable play
    ///   Teleport   — teleports to the farthest canvas quadrant when "touched" (cursor near)
    /// </summary>
    public class EvasiveScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas     = null!;
        private Random _rng        = null!;
        private double _targetSize;
        private double _moveSpeed;

        private Ellipse? _target;
        private double   _x, _y;
        private double   _vx, _vy;

        // Last known cursor position (updated on click)
        private double _cursorX = -1, _cursorY = -1;

        // Predictive: re-randomise direction timer
        private long _nextDirectionChangeTick;
        private static readonly long DirectionChangeInterval = (long)(0.8 * Stopwatch.Frequency);

        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;
        private int    _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak      { get; private set; }

        public EvasiveScenario(string variant = "Standard")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas    = canvas;
            _rng       = rng;
            _targetSize = targetSize;
            _moveSpeed  = _variant == "Aggressive"
                ? moveSpeed * 1.6
                : moveSpeed > 0 ? moveSpeed : 4.0;

            double w = Math.Max(1, canvas.ActualWidth  - targetSize);
            double h = Math.Max(1, canvas.ActualHeight - targetSize);
            _x = w / 2;
            _y = h / 2;

            // Start with a random direction
            double angle = _rng.NextDouble() * Math.PI * 2;
            _vx = Math.Cos(angle) * _moveSpeed;
            _vy = Math.Sin(angle) * _moveSpeed;

            _nextDirectionChangeTick = Stopwatch.GetTimestamp() + DirectionChangeInterval;

            _target = TargetFactory.CreateTarget(targetSize, _x, _y);
            canvas.Children.Add(_target);
            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            if (_target == null) return;

            double w   = canvas.ActualWidth;
            double h   = canvas.ActualHeight;
            long   now = Stopwatch.GetTimestamp();

            // If we know cursor position, steer away from it
            if (_cursorX >= 0)
            {
                double cx    = _x + _targetSize / 2;
                double cy    = _y + _targetSize / 2;
                double awayX = cx - _cursorX;
                double awayY = cy - _cursorY;
                double dist  = Math.Sqrt(awayX * awayX + awayY * awayY);

                if (dist > 0.01)
                {
                    double blendT = _variant == "Aggressive" ? 0.25 : 0.12;
                    double targetVx = (awayX / dist) * _moveSpeed;
                    double targetVy = (awayY / dist) * _moveSpeed;
                    _vx += (targetVx - _vx) * blendT;
                    _vy += (targetVy - _vy) * blendT;

                    // Renormalise speed
                    double spd = Math.Sqrt(_vx * _vx + _vy * _vy);
                    if (spd > 0.01)
                    {
                        _vx = _vx / spd * _moveSpeed;
                        _vy = _vy / spd * _moveSpeed;
                    }

                    // Teleport variant: if cursor gets very close, warp to far quadrant
                    if (_variant == "Teleport" && dist < _targetSize * 2)
                    {
                        TeleportToFarQuadrant(w, h, cx, cy);
                        return;
                    }
                }
            }

            // Predictive: randomise direction periodically
            if (_variant == "Predictive" && now >= _nextDirectionChangeTick)
            {
                double angle = _rng.NextDouble() * Math.PI * 2;
                _vx = Math.Cos(angle) * _moveSpeed;
                _vy = Math.Sin(angle) * _moveSpeed;
                _nextDirectionChangeTick = now + DirectionChangeInterval;
            }

            // Move and bounce
            double nx = _x + _vx;
            double ny = _y + _vy;

            if (nx <= 0 || nx + _targetSize >= w) { _vx = -_vx; nx = Math.Clamp(nx, 0, w - _targetSize); }
            if (ny <= 0 || ny + _targetSize >= h) { _vy = -_vy; ny = Math.Clamp(ny, 0, h - _targetSize); }

            _x = nx;
            _y = ny;

            Canvas.SetLeft(_target, _x);
            Canvas.SetTop(_target, _y);
        }

        public bool HandleClick(Point clickPos)
        {
            // Track cursor position for evasion steering
            _cursorX = clickPos.X;
            _cursorY = clickPos.Y;

            if (_target == null) return false;

            double cx = Canvas.GetLeft(_target) + _targetSize / 2;
            double cy = Canvas.GetTop(_target)  + _targetSize / 2;
            double dx = clickPos.X - cx;
            double dy = clickPos.Y - cy;

            if (dx * dx + dy * dy <= (_targetSize / 2) * (_targetSize / 2))
            {
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

        private void TeleportToFarQuadrant(double w, double h, double cursorX, double cursorY)
        {
            if (_target == null) return;

            // Choose the quadrant diagonally opposite the cursor
            double newX = cursorX < w / 2
                ? w * 0.6 + _rng.NextDouble() * w * 0.3
                : _rng.NextDouble() * w * 0.3;
            double newY = cursorY < h / 2
                ? h * 0.6 + _rng.NextDouble() * h * 0.3
                : _rng.NextDouble() * h * 0.3;

            _x = Math.Clamp(newX, 0, w - _targetSize);
            _y = Math.Clamp(newY, 0, h - _targetSize);

            Canvas.SetLeft(_target, _x);
            Canvas.SetTop(_target, _y);
        }
    }
}
