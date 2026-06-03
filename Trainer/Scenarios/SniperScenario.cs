using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// Sniper scenario — small distant targets.
    /// Variants:
    ///   Standard  — stationary 1.8 s then slow-glide reposition (outer 40 % spawn)
    ///   Moving    — slow continuous movement, direction change every 2 s (outer spawn)
    ///   Wind      — constant drift in one direction, bounces at edges (anywhere spawn)
    /// Suppresses reaction-speed coaching in AiCoachService.
    /// </summary>
    public class SniperScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas    = null!;
        private Random _rng       = null!;
        private double _targetSize;
        private double _rawMoveSpeed;   // original DifficultyConfig.MoveSpeed (unscaled)

        private Ellipse? _target;
        private double   _cx, _cy;     // current target centre

        // ── Standard — state machine ─────────────────────────────────
        private double   _startX, _startY, _destX, _destY;

        private enum MoveState { Still, Moving, MissDelay }
        private MoveState      _state = MoveState.Still;
        private readonly Stopwatch _stateTimer = new();

        private const double StillMs     = 1800;
        private const double MoveMs      = 800;
        private const double MissDelayMs = 500;

        // ── Moving / Wind — continuous velocity ──────────────────────
        private double _moveDx, _moveDy;
        private readonly Stopwatch _moveDirTimer   = new();
        private const double MovingDirChangeMs = 2000;

        // ── Reaction tracking ─────────────────────────────────────────
        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;
        private int    _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak      { get; private set; }

        public SniperScenario(string variant = "Standard")
        {
            _variant = variant;
        }

        // ─────────────────────────────────────────────────────────────
        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas       = canvas;
            _rng          = rng;
            _rawMoveSpeed = moveSpeed;
            _targetSize   = Math.Max(10, targetSize * 0.7);

            switch (_variant)
            {
                case "Moving":
                    SpawnAtOuter();
                    SetRandomVelocity(_rawMoveSpeed * 0.4);
                    _moveDirTimer.Restart();
                    break;

                case "Wind":
                {
                    double angle = _rng.NextDouble() * 2 * Math.PI;
                    _moveDx = Math.Cos(angle) * (_rawMoveSpeed * 0.25);
                    _moveDy = Math.Sin(angle) * (_rawMoveSpeed * 0.25);
                    SpawnAnywhere();
                    break;
                }

                default: // Standard
                    SpawnAtOuter();
                    _state = MoveState.Still;
                    _stateTimer.Restart();
                    break;
            }

            _reactionTimer.Restart();
        }

        // ─────────────────────────────────────────────────────────────
        public void Update(Canvas canvas)
        {
            if (_target == null) return;

            switch (_variant)
            {
                case "Moving":
                    if (_moveDirTimer.ElapsedMilliseconds >= MovingDirChangeMs)
                    {
                        SetRandomVelocity(_rawMoveSpeed * 0.4);
                        _moveDirTimer.Restart();
                    }
                    ApplyVelocity(canvas);
                    break;

                case "Wind":
                    ApplyVelocity(canvas);
                    break;

                default: // Standard
                    UpdateStandard();
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        public bool HandleClick(Point clickPos)
        {
            if (_target == null) return false;

            double dx = clickPos.X - _cx;
            double dy = clickPos.Y - _cy;
            double r  = _targetSize / 2;

            if (dx * dx + dy * dy <= r * r)
            {
                Hits++;
                _streak++;
                MaxStreak = Math.Max(MaxStreak, _streak);

                double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
                _totalReactionMs += reaction;
                if (reaction < BestReactionMs) BestReactionMs = reaction;

                _canvas.Children.Remove(_target);

                switch (_variant)
                {
                    case "Moving":
                        (_cx, _cy) = GetOuterCenter();
                        _target    = MakeTarget(_cx, _cy);
                        _canvas.Children.Add(_target);
                        SetRandomVelocity(_rawMoveSpeed * 0.4);
                        _moveDirTimer.Restart();
                        break;

                    case "Wind":
                        // New spawn anywhere; drift direction unchanged
                        (_cx, _cy) = GetAnyCenter();
                        _target    = MakeTarget(_cx, _cy);
                        _canvas.Children.Add(_target);
                        break;

                    default: // Standard
                        (_cx, _cy) = GetOuterCenter();
                        _target    = MakeTarget(_cx, _cy);
                        _canvas.Children.Add(_target);
                        _state = MoveState.Still;
                        _stateTimer.Restart();
                        break;
                }

                _reactionTimer.Restart();
                return true;
            }

            Misses++;
            _streak = 0;

            // Standard: target stays briefly then repositions
            // Moving / Wind: target continues moving, no delay
            if (_variant == "Standard")
            {
                _state = MoveState.MissDelay;
                _stateTimer.Restart();
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────
        public void Stop(Canvas canvas)
        {
            if (_target != null)
            {
                canvas.Children.Remove(_target);
                _target = null;
            }
        }

        // ── Standard state machine ────────────────────────────────────
        private void UpdateStandard()
        {
            switch (_state)
            {
                case MoveState.Still:
                    if (_stateTimer.ElapsedMilliseconds >= StillMs)
                    {
                        (_destX, _destY) = GetOuterCenter();
                        _startX = _cx; _startY = _cy;
                        _state  = MoveState.Moving;
                        _stateTimer.Restart();
                    }
                    break;

                case MoveState.Moving:
                {
                    double t = Math.Clamp(_stateTimer.ElapsedMilliseconds / MoveMs, 0.0, 1.0);
                    _cx = _startX + (_destX - _startX) * t;
                    _cy = _startY + (_destY - _startY) * t;
                    Canvas.SetLeft(_target!, _cx - _targetSize / 2);
                    Canvas.SetTop (_target!, _cy - _targetSize / 2);
                    if (t >= 1.0)
                    {
                        _cx = _destX; _cy = _destY;
                        _state = MoveState.Still;
                        _stateTimer.Restart();
                        _reactionTimer.Restart(); // new still window
                    }
                    break;
                }

                case MoveState.MissDelay:
                    if (_stateTimer.ElapsedMilliseconds >= MissDelayMs)
                    {
                        (_cx, _cy) = GetOuterCenter();
                        Canvas.SetLeft(_target!, _cx - _targetSize / 2);
                        Canvas.SetTop (_target!, _cy - _targetSize / 2);
                        _state = MoveState.Still;
                        _stateTimer.Restart();
                        _reactionTimer.Restart();
                    }
                    break;
            }
        }

        // ── Continuous movement (Moving + Wind) ───────────────────────
        private void ApplyVelocity(Canvas canvas)
        {
            double w    = canvas.ActualWidth;
            double h    = canvas.ActualHeight;
            double half = _targetSize / 2;

            _cx += _moveDx;
            _cy += _moveDy;

            if (_cx - half <= 0 || _cx + half >= w)
            {
                _moveDx = -_moveDx;
                _cx = Math.Clamp(_cx, half, w - half);
            }
            if (_cy - half <= 0 || _cy + half >= h)
            {
                _moveDy = -_moveDy;
                _cy = Math.Clamp(_cy, half, h - half);
            }

            Canvas.SetLeft(_target!, _cx - half);
            Canvas.SetTop (_target!, _cy - half);
        }

        private void SetRandomVelocity(double speed)
        {
            double angle = _rng.NextDouble() * 2 * Math.PI;
            _moveDx = Math.Cos(angle) * speed;
            _moveDy = Math.Sin(angle) * speed;
        }

        // ── Spawn helpers ─────────────────────────────────────────────
        private void SpawnAtOuter()
        {
            (_cx, _cy) = GetOuterCenter();
            _target    = MakeTarget(_cx, _cy);
            _canvas.Children.Add(_target);
        }

        private void SpawnAnywhere()
        {
            (_cx, _cy) = GetAnyCenter();
            _target    = MakeTarget(_cx, _cy);
            _canvas.Children.Add(_target);
        }

        private Ellipse MakeTarget(double cx, double cy)
            => TargetFactory.CreateTarget(_targetSize, cx, cy,
                   new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xA0)));

        /// <summary>Outer 30 % band on both axes.</summary>
        private (double x, double y) GetOuterCenter()
        {
            double w    = Math.Max(1, _canvas.ActualWidth);
            double h    = Math.Max(1, _canvas.ActualHeight);
            double half = _targetSize / 2;

            double x = _rng.NextDouble() < 0.5
                ? half + _rng.NextDouble() * Math.Max(0, w * 0.3 - half)
                : w * 0.7 + _rng.NextDouble() * Math.Max(0, w * 0.3 - half);

            double y = _rng.NextDouble() < 0.5
                ? half + _rng.NextDouble() * Math.Max(0, h * 0.3 - half)
                : h * 0.7 + _rng.NextDouble() * Math.Max(0, h * 0.3 - half);

            return (Math.Clamp(x, half, w - half), Math.Clamp(y, half, h - half));
        }

        private (double x, double y) GetAnyCenter()
        {
            double w    = Math.Max(1, _canvas.ActualWidth);
            double h    = Math.Max(1, _canvas.ActualHeight);
            double half = _targetSize / 2;
            return (half + _rng.NextDouble() * Math.Max(0, w - _targetSize),
                    half + _rng.NextDouble() * Math.Max(0, h - _targetSize));
        }
    }
}
