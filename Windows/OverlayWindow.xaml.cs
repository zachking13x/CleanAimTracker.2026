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
            if (Settings.Default.OverlayLeft >= 0 &&
                Settings.Default.OverlayTop >= 0)
            {
                Left = Settings.Default.OverlayLeft;
                Top = Settings.Default.OverlayTop;
            }
        }

        private void Overlay_LocationChanged(object sender, EventArgs e)
        {
            Settings.Default.OverlayLeft = Left;
            Settings.Default.OverlayTop = Top;
            Settings.Default.Save();
        }


        // Start button → call MainWindow's Start logic
        public void Start_Click(object sender, RoutedEventArgs e)
        {
            var main = Application.Current.MainWindow as MainWindow;
            main?.StartButton_Click(sender, e);
        }

        // Stop button → call MainWindow's Stop logic
        public void Stop_Click(object sender, RoutedEventArgs e)
        {
            var main = Application.Current.MainWindow as MainWindow;
            main?.StopButton_Click(sender, e);
        }

        // Recommend button → call MainWindow's Recommend logic
        public void Recommend_Click(object sender, RoutedEventArgs e)
        {
            var main = Application.Current.MainWindow as MainWindow;
            main?.OpenRecommendation_Click(sender, e);
        }
    }
}
