using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// Follow a moving target.
    /// Variants:
    ///   Smooth    — single target, smooth bounce (default)
    ///   Evasive   — single target, sharp random direction changes every ~800 ms
    ///   Two-Track — two targets moving simultaneously; click either for a hit
    /// </summary>
    public class TrackingScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas = null!;
        private Random _rng    = null!;

        private double _targetSize;
        private double _moveSpeed;

        private readonly List<Ellipse> _targets = new();
        private readonly TargetMover   _mover   = new();

        private readonly Stopwatch _reactionTimer = new();
        private readonly Stopwatch _evasiveTimer  = new();
        private double _totalReactionMs;
        private int _streak;

        public int    Hits            { get; private set; }
        public int    Misses          { get; private set; }
        public double BestReactionMs  { get; private set; } = double.MaxValue;
        public double AvgReactionMs   => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak       { get; private set; }

        public TrackingScenario(string variant = "Smooth")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas     = canvas;
            _rng        = rng;
            _targetSize = targetSize;
            _moveSpeed  = _variant == "Evasive" ? moveSpeed * 1.45 : moveSpeed;

            int count = _variant == "Two-Track" ? 2 : 1;

            for (int i = 0; i < count; i++)
            {
                // Stagger start positions so Two-Track targets don't overlap
                double x = canvas.ActualWidth  / 2 + (i == 1 ?  targetSize * 3 : 0);
                double y = canvas.ActualHeight / 2 + (i == 1 ? -targetSize * 2 : 0);

                var target = TargetFactory.CreateTrackingTarget(_targetSize, x, y);
                _targets.Add(target);
                canvas.Children.Add(target);
                _mover.AddTarget(target, _moveSpeed);
            }

            if (_variant == "Evasive") _evasiveTimer.Restart();
            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            _mover.Update(canvas);

            // Evasive: randomise velocity every 800 ms
            if (_variant == "Evasive" && _evasiveTimer.ElapsedMilliseconds >= 800)
            {
                foreach (var t in _targets)
                    _mover.RandomizeVelocity(t, _moveSpeed);
                _evasiveTimer.Restart();
            }
        }

        public bool HandleClick(Point clickPos)
        {
            foreach (var target in _targets)
            {
                double left = Canvas.GetLeft(target);
                double top  = Canvas.GetTop(target);
                double size = target.Width;
                double cx   = left + size / 2;
                double cy   = top  + size / 2;
                double dx   = clickPos.X - cx;
                double dy   = clickPos.Y - cy;

                if (dx * dx + dy * dy <= (size / 2) * (size / 2))
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
            }

            Misses++;
            _streak = 0;
            return false;
        }

        public void Stop(Canvas canvas)
        {
            foreach (var t in _targets)
            {
                canvas.Children.Remove(t);
                _mover.RemoveTarget(t);
            }
            _targets.Clear();
        }
    }
}
