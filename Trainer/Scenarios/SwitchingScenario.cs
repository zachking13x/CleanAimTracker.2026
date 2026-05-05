using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    public class SwitchingScenario : IAimScenario
    {
        private Canvas _canvas;
        private Random _rng;

        private double _targetSize;
        private double _moveSpeed; // unused but required by interface

        private readonly List<Ellipse> _targets = new();
        private int _activeIndex = 0;

        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;

        private int _streak;

        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int MaxStreak { get; private set; }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas = canvas;
            _rng = rng;
            _targetSize = targetSize;
            _moveSpeed = moveSpeed;

            SpawnTargets(4); // 4 switching targets
            SetActiveTarget(0);

            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            // Switching scenario: no movement
        }

        public bool HandleClick(Point clickPos)
        {
            if (_targets.Count == 0)
                return false;

            Ellipse active = _targets[_activeIndex];

            double x = Canvas.GetLeft(active);
            double y = Canvas.GetTop(active);
            double size = active.Width;

            // Manual hit detection
            double centerX = x + size / 2;
            double centerY = y + size / 2;

            double dx = clickPos.X - centerX;
            double dy = clickPos.Y - centerY;

            bool hit = (dx * dx + dy * dy) <= (size / 2) * (size / 2);

            if (hit)
            {
                Hits++;
                _streak++;
                MaxStreak = Math.Max(MaxStreak, _streak);

                double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
                _totalReactionMs += reaction;

                if (reaction < BestReactionMs)
                    BestReactionMs = reaction;

                _reactionTimer.Restart();

                // Pick a new active target
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

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────
        private void SpawnTargets(int count)
        {
            _targets.Clear();

            for (int i = 0; i < count; i++)
            {
                double x = _rng.NextDouble() * Math.Max(1, _canvas.ActualWidth - _targetSize);
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
                if (i == index)
                {
                    // Active target = bright cyan
                    double x = Canvas.GetLeft(_targets[i]) + _targetSize / 2;
                    double y = Canvas.GetTop(_targets[i]) + _targetSize / 2;

                    _canvas.Children.Remove(_targets[i]);
                    _targets[i] = TargetFactory.CreateActiveSwitchTarget(_targetSize, x, y);
                    _canvas.Children.Add(_targets[i]);
                }
                else
                {
                    // Inactive = gray
                    double x = Canvas.GetLeft(_targets[i]) + _targetSize / 2;
                    double y = Canvas.GetTop(_targets[i]) + _targetSize / 2;

                    _canvas.Children.Remove(_targets[i]);
                    _targets[i] = TargetFactory.CreateInactiveSwitchTarget(_targetSize, x, y);
                    _canvas.Children.Add(_targets[i]);
                }
            }

            _activeIndex = index;
        }

        private void PickNewActiveTarget()
        {
            if (_targets.Count <= 1)
                return;

            int newIndex;
            do
            {
                newIndex = _rng.Next(_targets.Count);
            }
            while (newIndex == _activeIndex);

            SetActiveTarget(newIndex);
        }
    }
}
