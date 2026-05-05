using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    public class TrackingScenario : IAimScenario
    {
        private Canvas _canvas;
        private Ellipse _target;
        private Random _rng;

        private double _targetSize;
        private double _moveSpeed;

        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;

        private int _streak;

        private readonly TargetMover _mover = new();

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

            // Spawn in the center
            double x = canvas.ActualWidth / 2;
            double y = canvas.ActualHeight / 2;

            _target = TargetFactory.CreateTrackingTarget(_targetSize, x, y);
            canvas.Children.Add(_target);

            _mover.AddTarget(_target, _moveSpeed);

            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            _mover.Update(canvas);
        }

        public bool HandleClick(Point clickPos)
        {
            if (_target == null)
                return false;

            double x = Canvas.GetLeft(_target);
            double y = Canvas.GetTop(_target);
            double size = _target.Width;

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
            if (_target != null)
            {
                canvas.Children.Remove(_target);
                _mover.RemoveTarget(_target);
            }

            _target = null;
        }
    }
}
