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
        private double   _moveSpeed;
        private long     _spawnDelayTicks;
        private bool     _waitingToSpawn;
        private long     _waitStartTick;

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

        /// <summary>Canvas-space center of the most recently hit target.</summary>
        public Point LastHitCenter { get; private set; } = new Point(double.NaN, double.NaN);

        /// <summary>
        /// Stopwatch timestamps recorded each time a new target spawns after the first one.
        /// Each spawn forces a new movement direction, making these "direction change" events
        /// for AvgDirectionChangeLagMs telemetry.
        /// </summary>
        public System.Collections.Generic.List<long> DirectionChangeTimestamps { get; } = new();

        public ReactiveScenario(string variant = "Standard")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas     = canvas;
            _rng        = rng;
            _targetSize = targetSize;

            _moveSpeed = moveSpeed > 0 ? moveSpeed : 2.5;

            double baseWindowMs = _variant switch
            {
                "SpeedBurst" => Math.Max(150, 600 - (_moveSpeed - 1.5) * 75),
                _            => Math.Max(250, 900 - (_moveSpeed - 1.5) * 150)
            };
            _exposureWindowTicks = (long)(baseWindowMs / 1000.0 * Stopwatch.Frequency);

            double spawnDelayMs = Math.Max(50, 300 - (_moveSpeed - 1.5) * 75);
            _spawnDelayTicks = (long)(spawnDelayMs / 1000.0 * Stopwatch.Frequency);

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
                if (_waitingToSpawn)
                {
                    if (now >= _waitStartTick + _spawnDelayTicks)
                    {
                        _waitingToSpawn = false;
                        SpawnTarget();
                    }
                    return;
                }

                if (_target != null && now >= _targetSpawnTick + _exposureWindowTicks)
                {
                    // Expired
                    canvas.Children.Remove(_target);
                    _target = null;
                    Misses++;
                    _streak = 0;
                    _waitingToSpawn = true;
                    _waitStartTick  = now;
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
                LastHitCenter = new Point(cx, cy);
                RegisterHit();
                _canvas.Children.Remove(_target);
                _target = null;
                _waitingToSpawn = true;
                _waitStartTick  = Stopwatch.GetTimestamp();
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

            // Every spawn after the first forces a new movement direction.
            // Record it as a direction-change event for AvgDirectionChangeLagMs telemetry.
            if (Hits + Misses > 0)
                DirectionChangeTimestamps.Add(_targetSpawnTick);

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
            double speedFactor = (_moveSpeed - 1.5) * 50;
            double minMs = Math.Max(100, 300 - speedFactor);
            double maxMs = Math.Max(minMs + 100, 600 - speedFactor);
            long   winMs = (long)(minMs + _rng.NextDouble() * (maxMs - minMs));

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
