using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer
{
    public class TargetMover
    {
        private readonly Dictionary<Ellipse, (double Dx, double Dy)> _velocities = new();
        private readonly Random _rng = new();

        public void AddTarget(Ellipse target, double speed)
        {
            double dx = (_rng.NextDouble() * 2 - 1) * speed;
            double dy = (_rng.NextDouble() * 2 - 1) * speed;

            _velocities[target] = (dx, dy);
        }

        public void RemoveTarget(Ellipse target)
        {
            if (_velocities.ContainsKey(target))
                _velocities.Remove(target);
        }

        public void Clear()
        {
            _velocities.Clear();
        }

        public void Update(Canvas canvas)
        {
            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;

            foreach (var kvp in _velocities)
            {
                var target = kvp.Key;
                var (dx, dy) = kvp.Value;

                double size = target.Width;
                double x = Canvas.GetLeft(target) + dx;
                double y = Canvas.GetTop(target) + dy;

                bool bounced = false;

                if (x <= 0 || x + size >= w)
                {
                    dx *= -1;
                    bounced = true;
                }

                if (y <= 0 || y + size >= h)
                {
                    dy *= -1;
                    bounced = true;
                }

                if (bounced)
                    _velocities[target] = (dx, dy);

                x = Math.Clamp(x, 0, w - size);
                y = Math.Clamp(y, 0, h - size);

                Canvas.SetLeft(target, x);
                Canvas.SetTop(target, y);
            }
        }
    }
}
