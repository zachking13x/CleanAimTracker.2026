using CleanAimTracker.Services;
using System;
using System.Windows;
using System.Windows.Input;

namespace CleanAimTracker.Windows
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();
            Loaded += Overlay_Loaded;
            LocationChanged += Overlay_LocationChanged;
        }

        // Allow dragging the overlay
        private void OverlayDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        // Restore saved position when overlay loads
        private void Overlay_Loaded(object sender, RoutedEventArgs e)
        {
            var s = SettingsService.Load();

            if (s.OverlayLeft >= 0 && s.OverlayTop >= 0)
            {
                Left = s.OverlayLeft;
                Top = s.OverlayTop;
            }
        }

        // Save overlay position when moved
        private void Overlay_LocationChanged(object sender, EventArgs e)
        {
            var s = SettingsService.Load();
            s.OverlayLeft = Left;
            s.OverlayTop = Top;
            SettingsService.Save(s);
        }

        // Start button → call MainWindow's Start logic
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
                main.StartButton_Click(sender, e);
        }

        // Stop button → call MainWindow's Stop logic
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
                main.StopButton_Click(sender, e);
        }

        // Recommend button → call MainWindow's Recommend logic
        private void Recommend_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
                main.OpenRecommendation_Click(sender, e);
        }

        // Close button on the overlay itself
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
