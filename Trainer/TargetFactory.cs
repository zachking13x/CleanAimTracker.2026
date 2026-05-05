using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer
{
    public static class TargetFactory
    {
        public static Ellipse CreateTarget(
            double size,
            double x,
            double y,
            Brush? fill = null,
            double fadeMs = 80)
        {
            fill ??= new SolidColorBrush(Color.FromRgb(0, 229, 255));

            var el = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fill,
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness = 1.5,
                Opacity = 0
            };

            // Position
            Canvas.SetLeft(el, x - size / 2);
            Canvas.SetTop(el, y - size / 2);

            // Fade-in animation
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeMs));
            el.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            return el;
        }

        public static Ellipse CreateTrackingTarget(
            double size,
            double x,
            double y)
        {
            return CreateTarget(
                size,
                x,
                y,
                new SolidColorBrush(Color.FromRgb(255, 165, 0)) // orange tracking target
            );
        }

        public static Ellipse CreateInactiveSwitchTarget(
            double size,
            double x,
            double y)
        {
            return CreateTarget(
                size,
                x,
                y,
                new SolidColorBrush(Color.FromArgb(120, 100, 100, 100))
            );
        }

        public static Ellipse CreateActiveSwitchTarget(
            double size,
            double x,
            double y)
        {
            return CreateTarget(
                size,
                x,
                y,
                new SolidColorBrush(Color.FromRgb(0, 229, 255))
            );
        }
    }
}
