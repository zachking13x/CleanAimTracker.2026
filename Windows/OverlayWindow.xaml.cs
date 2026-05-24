using CleanAimTracker.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CleanAimTracker.Windows
{
    public partial class OverlayWindow : Window
    {
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _positionSaveTimer;   // debounces overlay drag saves
        private bool _isCollapsed = false;

        private static readonly SolidColorBrush GreenBrush  = new(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(0xFF, 0xD7, 0x00));
        private static readonly SolidColorBrush RedBrush    = new(Color.FromRgb(0xEF, 0x53, 0x50));
        private static readonly SolidColorBrush CyanBrush   = new(Color.FromRgb(0x00, 0xD4, 0xFF));
        private static readonly SolidColorBrush DimBrush    = new(Color.FromRgb(0x44, 0x44, 0x44));
        private static readonly SolidColorBrush MutedBrush  = new(Color.FromRgb(0xAA, 0xAA, 0xAA));

        public OverlayWindow()
        {
            InitializeComponent();
            Loaded           += Overlay_Loaded;
            LocationChanged  += Overlay_LocationChanged;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _refreshTimer.Tick += RefreshStats;
            _refreshTimer.Start();

            // Debounce position saves: only write to disk 300 ms after the drag stops.
            // Without this, every pixel dragged triggers a full settings Load+Save cycle,
            // which races with XP/challenge saves and overwrites them.
            _positionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _positionSaveTimer.Tick += (_, _) =>
            {
                _positionSaveTimer.Stop();
                var s = SettingsService.Load();
                s.OverlayLeft = Left;
                s.OverlayTop  = Top;
                SettingsService.Save(s);
            };
        }

        // ── STATS REFRESH ──────────────────────────────────────────────
        private void RefreshStats(object? sender, EventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow main) return;

            bool tracking = main.IsTracking;

            // Status indicator
            StatusDot.Fill = tracking ? RedBrush : DimBrush;
            StatusLabel.Foreground = tracking
                ? new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50))
                : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            StatusLabel.Text = tracking ? "LIVE" : "READY";

            // Timer
            var elapsed = main.SessionElapsed;
            TimerText.Text = tracking
                ? $"{elapsed:mm\\:ss}"
                : "00:00";
            TimerText.Foreground = tracking ? MutedBrush : DimBrush;

            if (_isCollapsed) return;

            if (!tracking)
            {
                QualityText.Text      = "—";
                QualityText.Foreground = DimBrush;
                VelocityText.Text     = "—";
                FlicksText.Text       = "—";
                SmoothnessText.Text   = "—";
                return;
            }

            // Quality score — color coded
            double q = main.LiveQuality;
            QualityText.Text = $"{q:F0}";
            QualityText.Foreground = q >= 80 ? GreenBrush
                                   : q >= 60 ? YellowBrush
                                   :           RedBrush;

            // Mini stats
            double vel = main.LiveVelocity;
            VelocityText.Text      = $"{vel:F0}";
            VelocityText.Foreground = vel > 150 ? CyanBrush : MutedBrush;

            FlicksText.Text        = main.LiveFlicks.ToString();
            FlicksText.Foreground  = MutedBrush;

            double sm = main.LiveSmoothness;
            SmoothnessText.Text      = $"{sm:F0}";
            SmoothnessText.Foreground = sm >= 80 ? GreenBrush
                                       : sm >= 60 ? YellowBrush
                                       :            RedBrush;
        }

        // ── OPACITY FADE ───────────────────────────────────────────────
        private void Border_MouseEnter(object sender, MouseEventArgs e)
            => OuterBorder.Opacity = 0.97;

        private void Border_MouseLeave(object sender, MouseEventArgs e)
            => OuterBorder.Opacity = 0.82;

        // ── COLLAPSE TOGGLE ────────────────────────────────────────────
        private void Collapse_Click(object sender, RoutedEventArgs e)
        {
            _isCollapsed = !_isCollapsed;
            StatsSection.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
            CollapseBtn.Content     = _isCollapsed ? "▼" : "▲";
        }

        // ── DRAG ───────────────────────────────────────────────────────
        private void OverlayDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        // ── POSITION SAVE / RESTORE ────────────────────────────────────
        private void Overlay_Loaded(object sender, RoutedEventArgs e)
        {
            var s = SettingsService.Load();
            if (s.OverlayLeft >= 0 && s.OverlayTop >= 0)
            {
                // Guard against off-screen position (e.g. saved on a monitor that is now disconnected).
                double vw = SystemParameters.VirtualScreenWidth;
                double vh = SystemParameters.VirtualScreenHeight;
                double vl = SystemParameters.VirtualScreenLeft;
                double vt = SystemParameters.VirtualScreenTop;

                double clampedLeft = Math.Max(vl, Math.Min(s.OverlayLeft, vl + vw - 80));
                double clampedTop  = Math.Max(vt, Math.Min(s.OverlayTop,  vt + vh - 40));

                Left = clampedLeft;
                Top  = clampedTop;
            }
        }

        private void Overlay_LocationChanged(object sender, EventArgs e)
        {
            // Reset the debounce timer on every move event.
            // The actual save fires 300 ms after dragging stops.
            _positionSaveTimer.Stop();
            _positionSaveTimer.Start();
        }

        // ── CONTROLS ───────────────────────────────────────────────────
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
                main.StartButton_Click(sender, e);
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
                main.StopButton_Click(sender, e);
        }

        private void Recommend_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
                main.OpenRecommendation_Click(sender, e);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer.Stop();
            _positionSaveTimer.Stop();
            base.OnClosed(e);
        }
    }
}
