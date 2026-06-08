using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// Six simultaneous stationary targets — click any one to replace it.
    /// Trains multi-target scanning, first-shot placement, and confirmation habits.
    ///
    /// Variants:
    ///   Standard     — 6 normal targets distributed randomly
    ///   Micro        — 6 targets at 60 % size to stress small-hitbox accuracy
    ///   Cluster      — 6 targets grouped near canvas centre (40 % area band)
    ///   Confirmation — 3 larger targets; each must be held under cursor 120 ms before click counts
    /// </summary>
    public class StaticClickingScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas     = null!;
        private Random _rng        = null!;
        private double _targetSize;

        private readonly List<Ellipse> _targets = new();

        // Confirmation variant: track hover start per target
        private readonly Dictionary<Ellipse, long> _hoverStart = new();

        // Difficulty-scaled timing (set in Start)
        private double _moveSpeed;
        private long   _spawnDelayTicks;
        private long   _confirmDurationTicks;
        private readonly Queue<long> _pendingSpawns = new();

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

        public StaticClickingScenario(string variant = "Standard")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas     = canvas;
            _rng        = rng;
            _targetSize = _variant == "Micro" ? targetSize * 0.6 : targetSize;

            _moveSpeed = moveSpeed > 0 ? moveSpeed : 2.5;
            double spawnDelayMs   = Math.Max(80,  400 - (_moveSpeed - 1.5) * 100);
            _spawnDelayTicks      = (long)(spawnDelayMs   / 1000.0 * Stopwatch.Frequency);
            double confirmMs      = Math.Max(30,  120 - (_moveSpeed - 1.5) * 30);
            _confirmDurationTicks = (long)(confirmMs      / 1000.0 * Stopwatch.Frequency);

            int count = _variant == "Confirmation" ? 3 : 6;
            for (int i = 0; i < count; i++)
                SpawnOne();

            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            long now = Stopwatch.GetTimestamp();
            while (_pendingSpawns.Count > 0 && _pendingSpawns.Peek() <= now)
            {
                _pendingSpawns.Dequeue();
                SpawnOne();
            }
        }

        public bool HandleClick(Point clickPos)
        {
            long now = Stopwatch.GetTimestamp();

            foreach (var target in _targets.ToList())
            {
                double cx = Canvas.GetLeft(target) + target.Width  / 2;
                double cy = Canvas.GetTop(target)  + target.Height / 2;
                double dx = clickPos.X - cx;
                double dy = clickPos.Y - cy;
                double r  = target.Width / 2;

                if (dx * dx + dy * dy <= r * r)
                {
                    // Confirmation variant: require dwell time before the click counts
                    if (_variant == "Confirmation")
                    {
                        if (!_hoverStart.ContainsKey(target))
                        {
                            // First time hovering — record start
                            _hoverStart[target] = now;
                            return false; // not a confirmed hit yet
                        }
                        if (now - _hoverStart[target] < _confirmDurationTicks)
                            return false; // not yet held long enough
                        _hoverStart.Remove(target);
                    }

                    LastHitCenter = new Point(cx, cy);
                    RegisterHit(target);
                    return true;
                }
            }

            Misses++;
            _streak = 0;
            return false;
        }

        public void Stop(Canvas canvas)
        {
            foreach (var t in _targets)
                canvas.Children.Remove(t);
            _targets.Clear();
            _hoverStart.Clear();
            _pendingSpawns.Clear();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private void RegisterHit(Ellipse target)
        {
            Hits++;
            _streak++;
            MaxStreak = Math.Max(MaxStreak, _streak);

            double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
            _totalReactionMs += reaction;
            if (reaction < BestReactionMs) BestReactionMs = reaction;
            _reactionTimer.Restart();

            _canvas.Children.Remove(target);
            _targets.Remove(target);
            _pendingSpawns.Enqueue(Stopwatch.GetTimestamp() + _spawnDelayTicks);
        }

        private void SpawnOne()
        {
            double w = Math.Max(1, _canvas.ActualWidth  - _targetSize);
            double h = Math.Max(1, _canvas.ActualHeight - _targetSize);

            double x, y;

            if (_variant == "Cluster")
            {
                // Restrict spawn to the central 40 % band
                double bandW = w * 0.4;
                double bandH = h * 0.4;
                double originX = (w - bandW) / 2;
                double originY = (h - bandH) / 2;
                x = originX + _rng.NextDouble() * bandW;
                y = originY + _rng.NextDouble() * bandH;
            }
            else
            {
                x = _rng.NextDouble() * w;
                y = _rng.NextDouble() * h;
            }

            var target = TargetFactory.CreateTarget(_targetSize, x, y);
            _targets.Add(target);
            _canvas.Children.Add(target);
        }
    }
}
