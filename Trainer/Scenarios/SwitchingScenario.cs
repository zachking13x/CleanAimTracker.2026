using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// Multi-target switching scenario — one lit target at a time.
    /// Variants:
    ///   4-Target   — 4 static targets (default)
    ///   6-Target   — 6 static targets for more complex switching paths
    ///   Speed Rush — 6 targets; auto-advances with a miss penalty if not clicked within 1.5 s
    /// </summary>
    public class SwitchingScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas = null!;
        private Random _rng    = null!;

        private double _targetSize;

        private readonly List<Ellipse> _targets = new();
        private int _activeIndex;

        private readonly Stopwatch _reactionTimer    = new();
        private readonly Stopwatch _autoAdvanceTimer = new(); // Speed Rush only
        private const double SpeedRushTimeoutMs = 1500;

        private double _totalReactionMs;
        private int _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak      { get; private set; }

        public SwitchingScenario(string variant = "4-Target")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas     = canvas;
            _rng        = rng;
            _targetSize = targetSize;

            int count = (_variant is "6-Target" or "Speed Rush") ? 6 : 4;
            SpawnTargets(count);
            SetActiveTarget(0);

            if (_variant == "Speed Rush") _autoAdvanceTimer.Restart();
            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            // Speed Rush: auto-advance if the player is too slow
            if (_variant == "Speed Rush" && _autoAdvanceTimer.ElapsedMilliseconds >= SpeedRushTimeoutMs)
            {
                Misses++;
                _streak = 0;
                PickNewActiveTarget();
                _autoAdvanceTimer.Restart();
            }
        }

        public bool HandleClick(Point clickPos)
        {
            if (_targets.Count == 0) return false;

            var active = _targets[_activeIndex];
            double left = Canvas.GetLeft(active);
            double top  = Canvas.GetTop(active);
            double size = active.Width;
            double cx   = left + size / 2;
            double cy   = top  + size / 2;
            double dx   = clickPos.X - cx;
            double dy   = clickPos.Y - cy;

            bool hit = (dx * dx + dy * dy) <= (size / 2) * (size / 2);

            if (hit)
            {
                Hits++;
                _streak++;
                MaxStreak = Math.Max(MaxStreak, _streak);

                double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
                _totalReactionMs += reaction;
                if (reaction < BestReactionMs) BestReactionMs = reaction;
                _reactionTimer.Restart();

                if (_variant == "Speed Rush") _autoAdvanceTimer.Restart();
                PickNewActiveTarget();
            }
            else
            {
                Misses++;
                _streak = 0;
            }

            return hit;
        }

        public void Stop(Canvas canvas)
        {
            foreach (var t in _targets)
                canvas.Children.Remove(t);
            _targets.Clear();
        }

        // ── Helpers ──────────────────────────────────────────────────
        private void SpawnTargets(int count)
        {
            _targets.Clear();
            for (int i = 0; i < count; i++)
            {
                double x = _rng.NextDouble() * Math.Max(1, _canvas.ActualWidth  - _targetSize);
                double y = _rng.NextDouble() * Math.Max(1, _canvas.ActualHeight - _targetSize);
                var target = TargetFactory.CreateInactiveSwitchTarget(_targetSize, x, y);
                _targets.Add(target);
                _canvas.Children.Add(target);
            }
        }

        private void SetActiveTarget(int index)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                double cx = Canvas.GetLeft(_targets[i]) + _targetSize / 2;
                double cy = Canvas.GetTop(_targets[i])  + _targetSize / 2;
                _canvas.Children.Remove(_targets[i]);
                _targets[i] = i == index
                    ? TargetFactory.CreateActiveSwitchTarget(_targetSize, cx, cy)
                    : TargetFactory.CreateInactiveSwitchTarget(_targetSize, cx, cy);
                _canvas.Children.Add(_targets[i]);
            }
            _activeIndex = index;
        }

        private void PickNewActiveTarget()
        {
            if (_targets.Count <= 1) return;
            int next;
            do { next = _rng.Next(_targets.Count); }
            while (next == _activeIndex);
            SetActiveTarget(next);
        }
    }
}
