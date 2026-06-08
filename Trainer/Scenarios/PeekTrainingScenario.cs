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
    /// A target that moves in and out from behind a wall on a timed cycle.
    /// Players must click during the exposure window to score a hit.
    /// PeekTimingOffsets records how early (negative) or late (positive) each click was
    /// relative to the centre of the exposure window, in milliseconds.
    ///
    /// Variants:
    ///   WideSwing      — target swings out slowly from the left wall; 600 ms window
    ///   Jiggle         — rapid oscillation (peek-back-peek); 200 ms window
    ///   JumpPeek       — target arcs upward from behind bottom cover; 400 ms window
    ///   CounterStrafe  — target moves then pauses briefly; players must click in the pause
    /// </summary>
    public class PeekTrainingScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas     = null!;
        private Random _rng        = null!;
        private double _targetSize;
        private double _moveSpeed;

        // Wall cover (Rectangle drawn over the target position)
        private Rectangle? _wall;

        // Target element
        private Ellipse?  _target;
        private double    _targetHomeX;  // hidden X (behind wall)
        private double    _targetHomeY;
        private double    _targetPeekX;  // exposed X
        private double    _targetPeekY;

        // State machine
        private enum PeekState { Hidden, Exposing, Exposed, Retreating }
        private PeekState _state = PeekState.Hidden;
        private long      _stateEnteredTick;

        // Timing windows in Stopwatch ticks
        private long _hiddenDurationTicks;
        private long _exposedDurationTicks;
        private long _transitionDurationTicks;

        // Peek timing: signed ms offset from window centre
        public List<double> PeekTimingOffsets { get; } = new();

        // CounterStrafe: movement then pause
        private double _strafeX, _strafeY, _strafeVx;

        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;
        private int    _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak      { get; private set; }

        public PeekTrainingScenario(string variant = "WideSwing")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas    = canvas;
            _rng       = rng;
            _targetSize = targetSize;

            _moveSpeed = moveSpeed > 0 ? moveSpeed : 2.5;
            double diffMult = Math.Max(0.4, 1.0 - (_moveSpeed - 1.5) * 0.12);

            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;

            // Wall position — left side cover
            double wallW = w * 0.15;
            double wallH = h;

            _wall = new Rectangle
            {
                Width   = wallW,
                Height  = wallH,
                Fill    = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x44)),
                RadiusX = 4,
                RadiusY = 4
            };
            Canvas.SetLeft(_wall, 0);
            Canvas.SetTop(_wall, 0);
            Canvas.SetZIndex(_wall, 5);
            canvas.Children.Add(_wall);

            // Target starts behind the wall
            _targetHomeX = wallW / 2 - targetSize / 2;   // centred on wall, hidden
            _targetHomeY = h / 2 - targetSize / 2;
            _targetPeekX = wallW + targetSize;             // just to the right of the wall
            _targetPeekY = _targetHomeY;

            switch (_variant)
            {
                case "Jiggle":
                    _hiddenDurationTicks      = TicksFromMs(400 * diffMult);
                    _exposedDurationTicks     = TicksFromMs(Math.Max(80, 200 * diffMult));
                    _transitionDurationTicks  = TicksFromMs(80 * diffMult);
                    break;
                case "JumpPeek":
                    _hiddenDurationTicks      = TicksFromMs(600 * diffMult);
                    _exposedDurationTicks     = TicksFromMs(Math.Max(80, 400 * diffMult));
                    _transitionDurationTicks  = TicksFromMs(300 * diffMult);
                    // JumpPeek: arc from bottom cover
                    _targetHomeY  = h - targetSize * 2;
                    _targetPeekY  = h / 2 - targetSize;
                    _targetHomeX  = w / 2 - targetSize / 2;
                    _targetPeekX  = _targetHomeX;
                    break;
                case "CounterStrafe":
                    _hiddenDurationTicks      = TicksFromMs(500 * diffMult);
                    _exposedDurationTicks     = TicksFromMs(Math.Max(80, 350 * diffMult));
                    _transitionDurationTicks  = TicksFromMs(200 * diffMult);
                    _strafeX = wallW + targetSize;
                    _strafeY = _targetHomeY;
                    _strafeVx = moveSpeed > 0 ? moveSpeed : 3.5;
                    break;
                default: // WideSwing
                    _hiddenDurationTicks      = TicksFromMs(700 * diffMult);
                    _exposedDurationTicks     = TicksFromMs(Math.Max(80, 600 * diffMult));
                    _transitionDurationTicks  = TicksFromMs(400 * diffMult);
                    break;
            }

            _target = TargetFactory.CreateTarget(targetSize, _targetHomeX, _targetHomeY);
            Canvas.SetZIndex(_target, 4);  // below wall
            canvas.Children.Add(_target);

            EnterState(PeekState.Hidden);
            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            if (_target == null) return;

            long now     = Stopwatch.GetTimestamp();
            long elapsed = now - _stateEnteredTick;

            switch (_state)
            {
                case PeekState.Hidden:
                    if (elapsed >= _hiddenDurationTicks)
                        EnterState(PeekState.Exposing);
                    break;

                case PeekState.Exposing:
                    double expT = Math.Clamp((double)elapsed / _transitionDurationTicks, 0, 1);
                    MoveTargetLerp(expT);
                    if (elapsed >= _transitionDurationTicks)
                    {
                        MoveTargetLerp(1.0);
                        EnterState(PeekState.Exposed);
                        _reactionTimer.Restart();
                    }
                    break;

                case PeekState.Exposed:
                    if (_variant == "CounterStrafe")
                        UpdateCounterStrafe(canvas, elapsed);

                    if (elapsed >= _exposedDurationTicks)
                    {
                        // Window expired without a hit — miss
                        Misses++;
                        _streak = 0;
                        // Timing offset: click was (after window ended) = +half window as "very late"
                        PeekTimingOffsets.Add(_exposedDurationTicks / (Stopwatch.Frequency / 1000.0) / 2.0 + 100);
                        EnterState(PeekState.Retreating);
                    }
                    break;

                case PeekState.Retreating:
                    double retT = Math.Clamp((double)elapsed / _transitionDurationTicks, 0, 1);
                    MoveTargetLerp(1.0 - retT);
                    if (elapsed >= _transitionDurationTicks)
                    {
                        MoveTargetLerp(0.0);
                        EnterState(PeekState.Hidden);
                    }
                    break;
            }
        }

        public bool HandleClick(Point clickPos)
        {
            if (_target == null || _state != PeekState.Exposed) return false;

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

                // Timing offset: ms from centre of exposure window (negative = early, positive = late)
                long   elapsedTicks   = Stopwatch.GetTimestamp() - _stateEnteredTick;
                double elapsedMs      = elapsedTicks / (Stopwatch.Frequency / 1000.0);
                double windowCentreMs = _exposedDurationTicks / (Stopwatch.Frequency / 1000.0) / 2.0;
                PeekTimingOffsets.Add(elapsedMs - windowCentreMs);

                EnterState(PeekState.Retreating);
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
            if (_wall != null)
            {
                canvas.Children.Remove(_wall);
                _wall = null;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private void EnterState(PeekState state)
        {
            _state            = state;
            _stateEnteredTick = Stopwatch.GetTimestamp();

            if (state == PeekState.Hidden && _target != null)
                Canvas.SetZIndex(_target, 4);  // keep behind wall
            if (state == PeekState.Exposed && _target != null)
                Canvas.SetZIndex(_target, 6);  // in front of wall
        }

        private void MoveTargetLerp(double t)
        {
            if (_target == null) return;

            if (_variant == "JumpPeek")
            {
                // Arc motion — use sine for smooth peek
                double arcT = Math.Sin(t * Math.PI / 2);
                double px   = _targetHomeX + (_targetPeekX - _targetHomeX) * t;
                double py   = _targetHomeY + (_targetPeekY - _targetHomeY) * arcT;
                Canvas.SetLeft(_target, px);
                Canvas.SetTop(_target, py);
            }
            else
            {
                double px = _targetHomeX + (_targetPeekX - _targetHomeX) * t;
                double py = _targetHomeY + (_targetPeekY - _targetHomeY) * t;
                Canvas.SetLeft(_target, px);
                Canvas.SetTop(_target, py);
            }
        }

        private void UpdateCounterStrafe(Canvas canvas, long elapsedInState)
        {
            if (_target == null) return;
            double w = canvas.ActualWidth;

            // Move right during first half, then stop (the "pause" is the shoot window)
            double halfWindowMs  = _exposedDurationTicks / (Stopwatch.Frequency / 1000.0) / 2.0;
            double elapsedMs     = elapsedInState / (Stopwatch.Frequency / 1000.0);

            if (elapsedMs < halfWindowMs)
            {
                _strafeX += _strafeVx;
                _strafeX  = Math.Clamp(_strafeX, 0, w - _targetSize);
                Canvas.SetLeft(_target, _strafeX);
                Canvas.SetTop(_target, _strafeY);
            }
            // else: paused — target stays still (optimal fire window)
        }

        private static long TicksFromMs(double ms) =>
            (long)(ms / 1000.0 * Stopwatch.Frequency);
    }
}
