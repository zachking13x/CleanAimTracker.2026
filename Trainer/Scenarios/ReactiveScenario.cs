using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// Targets flash on-screen for a brief window — players must react and click before
    /// the target disappears.  Misses are counted for expired targets AND inaccurate clicks.
    ///
    /// Variants:
    ///   Standard   — 500 ms exposure window
    ///   SpeedBurst — 250 ms window (forces fastest possible reaction)
    ///   Blink      — target dims to 30 % opacity on hover, rewarding peripheral vision
    ///   Chaotic    — 3 simultaneous blinking targets with random 300-600 ms windows
    /// </summary>
    public class ReactiveScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas     = null!;
        private Random _rng        = null!;
        private double _targetSize;

        // Single-target state (Standard / SpeedBurst / Blink)
        private Ellipse? _target;
        private long     _targetSpawnTick;
        private long     _exposureWindowTicks;

        // Chaotic variant: 3 independent targets with individual windows
        private const int ChaoticCount = 3;
        private readonly Ellipse?[] _chaoticTargets   = new Ellipse?[ChaoticCount];
        private readonly long[]     _chaoticSpawnTick  = new long[ChaoticCount];
        private readonly long[]     _chaoticWindowTick = new long[ChaoticCount];

        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;
        private int    _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak      { get; private set; }

        public ReactiveScenario(string variant = "Standard")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas     = canvas;
            _rng        = rng;
            _targetSize = targetSize;

            _exposureWindowTicks = _variant == "SpeedBurst"
                ? (long)(0.25 * Stopwatch.Frequency)
                : (long)(0.50 * Stopwatch.Frequency);

            if (_variant == "Chaotic")
            {
                for (int i = 0; i < ChaoticCount; i++)
                    SpawnChaoticTarget(i);
            }
            else
            {
                SpawnTarget();
            }

            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            long now = Stopwatch.GetTimestamp();

            if (_variant == "Chaotic")
            {
                for (int i = 0; i < ChaoticCount; i++)
                {
                    if (_chaoticTargets[i] != null && now >= _chaoticSpawnTick[i] + _chaoticWindowTick[i])
                    {
                        // Expired — count as miss
                        canvas.Children.Remove(_chaoticTargets[i]);
                        _chaoticTargets[i] = null;
                        Misses++;
                        _streak = 0;
                        SpawnChaoticTarget(i);
                    }
                }
            }
            else
            {
                if (_target != null && now >= _targetSpawnTick + _exposureWindowTicks)
                {
                    // Expired
                    canvas.Children.Remove(_target);
                    _target = null;
                    Misses++;
                    _streak = 0;
                    SpawnTarget();
                }
            }
        }

        public bool HandleClick(Point clickPos)
        {
            if (_variant == "Chaotic")
                return HandleChaoticClick(clickPos);

            if (_target == null) return false;

            double cx = Canvas.GetLeft(_target) + _targetSize / 2;
            double cy = Canvas.GetTop(_target)  + _targetSize / 2;
            double dx = clickPos.X - cx;
            double dy = clickPos.Y - cy;

            if (dx * dx + dy * dy <= (_targetSize / 2) * (_targetSize / 2))
            {
                RegisterHit();
                _canvas.Children.Remove(_target);
                _target = null;
                SpawnTarget();
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

            for (int i = 0; i < ChaoticCount; i++)
            {
                if (_chaoticTargets[i] != null)
                {
                    canvas.Children.Remove(_chaoticTargets[i]);
                    _chaoticTargets[i] = null;
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private void SpawnTarget()
        {
            double w = Math.Max(1, _canvas.ActualWidth  - _targetSize);
            double h = Math.Max(1, _canvas.ActualHeight - _targetSize);
            double x = _rng.NextDouble() * w;
            double y = _rng.NextDouble() * h;

            _target          = TargetFactory.CreateTarget(_targetSize, x, y);
            _targetSpawnTick = Stopwatch.GetTimestamp();

            if (_variant == "Blink")
                _target.Opacity = 0.3;

            _canvas.Children.Add(_target);
            _reactionTimer.Restart();
        }

        private void SpawnChaoticTarget(int slot)
        {
            double w    = Math.Max(1, _canvas.ActualWidth  - _targetSize);
            double h    = Math.Max(1, _canvas.ActualHeight - _targetSize);
            double x    = _rng.NextDouble() * w;
            double y    = _rng.NextDouble() * h;
            long   winMs = 300 + (long)(_rng.NextDouble() * 300); // 300-600 ms

            _chaoticTargets[slot]   = TargetFactory.CreateTarget(_targetSize, x, y);
            _chaoticSpawnTick[slot] = Stopwatch.GetTimestamp();
            _chaoticWindowTick[slot] = winMs * Stopwatch.Frequency / 1000;

            _canvas.Children.Add(_chaoticTargets[slot]);
        }

        private bool HandleChaoticClick(Point clickPos)
        {
            for (int i = 0; i < ChaoticCount; i++)
            {
                if (_chaoticTargets[i] == null) continue;

                double cx = Canvas.GetLeft(_chaoticTargets[i]!) + _targetSize / 2;
                double cy = Canvas.GetTop(_chaoticTargets[i]!)  + _targetSize / 2;
                double dx = clickPos.X - cx;
                double dy = clickPos.Y - cy;

                if (dx * dx + dy * dy <= (_targetSize / 2) * (_targetSize / 2))
                {
                    RegisterHit();
                    _canvas.Children.Remove(_chaoticTargets[i]);
                    _chaoticTargets[i] = null;
                    SpawnChaoticTarget(i);
                    return true;
                }
            }

            Misses++;
            _streak = 0;
            return false;
        }

        private void RegisterHit()
        {
            Hits++;
            _streak++;
            MaxStreak = Math.Max(MaxStreak, _streak);

            double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
            _totalReactionMs += reaction;
            if (reaction < BestReactionMs) BestReactionMs = reaction;
            _reactionTimer.Restart();
        }
    }
}
