using CleanAimTracker.Helpers;
using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Windows;
using System.Windows.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CleanAimTracker.Windows
{
    public sealed partial class MainWindow : Window
    {
        // RAW INPUT + PROFILE DATA
        private readonly RawInputService _rawInput;
        private List<GameProfile> _gameProfiles = new();
        private GameProfile _selectedProfile;

        // MOVEMENT + VELOCITY
        private double _totalDistance = 0;
        private double _currentVelocity = 0;
        private double _peakVelocity = 0;
        private double _averageVelocity = 0;
        private double _previousVelocity = 0;
        private DateTime _lastMoveTime = DateTime.Now;

        // ANGLES + QUALITY
        private double _lastAngle = 0;
        private double _previousAngle = 0;
        private double _angleChangeTotal = 0;
        private double _angleStability = 0;
        private double _smoothnessScore = 100;
        private double _correctionSharpness = 0;
        private double _movementConsistency = 100;
        private double _overallQualityScore = 100;

        // FLICKS + JITTER + DENSITY
        private int _flickCount = 0;
        private int _smallFlicks = 0;
        private int _largeFlicks = 0;
        private DateTime _lastFlickTime = DateTime.MinValue;
        private double _jitterAmount = 0;
        private double _movementDensity = 0;
        private double _rollingDensity = 0;

        // IDLE + BURSTS
        private bool _wasIdle = false;
        private double _idleTimeSeconds = 0;
        private int _idleBurstCount = 0;
        private double _idleTime = 0;

        // EVENT COUNTS
        private int _movementEvents = 0;
        private int _movementCountThisSecond = 0;
        private int _lastMovementCount = 0;
        private int _lastMovementEvents = 0;

        // DISTANCE PER EVENT
        private double _distancePerEventTotal = 0;
        private double _averageDistancePerEvent = 0;
        private double _peakDistancePerEvent = 0;

        // SESSION + TIMER
        private bool _isTracking = false;
        private DateTime _sessionStart;
        private readonly DispatcherTimer _timer = new();
        private double _sessionSeconds = 0;
        private DispatcherTimer? _onboardingTimer;

        // DPI + SENSITIVITY
        private double _dpi = 800;
        private double _sensitivity = 1.0;

        public event Action? StatsUpdated;

        // Live stat properties for the overlay to read
        public bool IsTracking          => _isTracking;
        public double LiveQuality       => _overallQualityScore;
        public double LiveVelocity      => _currentVelocity;
        public int    LiveFlicks        => _flickCount;
        public double LiveSmoothness    => _smoothnessScore;
        public TimeSpan SessionElapsed  => _isTracking ? DateTime.Now - _sessionStart : TimeSpan.Zero;

        // ─────────────────────────────────────────────────────────────
        // CONSTRUCTOR — FIXED WITH SourceInitialized
        // ─────────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();

            // NEW RAW INPUT SYSTEM
            _rawInput = new RawInputService();
            _rawInput.MouseMoved += OnRawMouseMove;

            // FIX: Initialize raw input AFTER window handle exists
            this.SourceInitialized += (_, _) =>
            {
                _rawInput.Initialize(this);
            };

            LogService.Info("MainWindow initialized");

            var settings = SettingsService.Load();
            _dpi = settings.DPI;
            _sensitivity = settings.Sensitivity;

            DpiInput.Text = _dpi.ToString("F0");
            SensitivityInput.Text = _sensitivity.ToString("F4");

            var profiles = ProfileStorage.LoadProfiles();
            _gameProfiles = GameProfile.GetAllProfiles(profiles);

            GameProfileCombo.Items.Clear();
            foreach (var p in _gameProfiles)
                GameProfileCombo.Items.Add(p.DisplayName);

            int savedIndex = _gameProfiles.FindIndex(p => p.Name == settings.SelectedProfile);
            GameProfileCombo.SelectedIndex = savedIndex >= 0 ? savedIndex : 0;

            if (_gameProfiles.Count > 0)
                _selectedProfile = _gameProfiles[GameProfileCombo.SelectedIndex];

            ResetDisplaysToIdle();

            UpdateTrialBanner();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            LoadTodayStats();

            // TASK-09: If user clicked "Hit Start" in onboarding, auto-start a 90s baseline session
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.Load();
            if (settings.OnboardingAutoStart)
            {
                settings.OnboardingAutoStart = false;
                SettingsService.Save(settings);

                // Small delay so the window finishes rendering first
                var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                delayTimer.Tick += (s, args) =>
                {
                    delayTimer.Stop();
                    StartButton_Click(this, new RoutedEventArgs());

                    // Auto-stop after 90 seconds
                    _onboardingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(90) };
                    _onboardingTimer.Tick += (s2, args2) =>
                    {
                        _onboardingTimer?.Stop();
                        _onboardingTimer = null;
                        if (_isTracking)
                        {
                            StopButton_Click(this, new RoutedEventArgs());
                            var msg = MessageBox.Show(
                                "Your baseline session is done! Click 'Summary' to see your aim analysis.",
                                "Baseline Captured ✓",
                                MessageBoxButton.OKCancel,
                                MessageBoxImage.None);
                            if (msg == MessageBoxResult.OK)
                                OpenSummary_Click(this, new RoutedEventArgs());
                        }
                    };
                    _onboardingTimer.Start();
                };
                delayTimer.Start();
            }
        }

        // TRIAL BANNER
        private void UpdateTrialBanner()
        {
            string banner = TrialService.GetBannerText();
            TrialBannerText.Text = banner;
            TrialBannerText.Visibility = string.IsNullOrEmpty(banner)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────
        // RAW INPUT MOVEMENT HANDLER
        // ─────────────────────────────────────────────────────────────
        private const int MAX_DELTA = 50;
        private const int MIN_DELTA = -50;
        private const int JITTER_THRESHOLD = 2;

        private void OnRawMouseMove(int dx, int dy)
        {
            if (Math.Abs(dx) <= JITTER_THRESHOLD && Math.Abs(dy) <= JITTER_THRESHOLD)
                return;

            dx = Math.Clamp(dx, MIN_DELTA, MAX_DELTA);
            dy = Math.Clamp(dy, MIN_DELTA, MAX_DELTA);

            Dispatcher.Invoke(() =>
            {
                LastDeltaText.Text = $"Last Delta: {dx}, {dy}";
                if (!_isTracking) return;

                _movementEvents++;
                DxDyText.Text = $"dX: {dx}  dY: {dy}";

                double eventDistance = Math.Sqrt(dx * dx + dy * dy);
                _distancePerEventTotal += eventDistance;

                double deviation = Math.Abs(eventDistance - _averageDistancePerEvent);
                _movementConsistency = Math.Clamp(100 - deviation * 10, 0, 100);

                _overallQualityScore = Math.Clamp(
                    (_smoothnessScore * 0.50) +
                    (_movementConsistency * 0.35) +
                    ((100 - _correctionSharpness) * 0.15),
                    0, 100);
                OverallQualityText.Text = $"{_overallQualityScore:F0}";

                if (eventDistance > _peakDistancePerEvent)
                    _peakDistancePerEvent = eventDistance;

                _lastAngle = Math.Atan2(dy, dx) * (180 / Math.PI);
                double angleDiff = Math.Abs(_lastAngle - _previousAngle);

                _smoothnessScore = Math.Clamp(100 - Math.Abs(angleDiff) * 2, 0, 100);
                SmoothnessText.Text = $"{_smoothnessScore:F0}";

                _angleChangeTotal += angleDiff;

                _angleStability = angleDiff;
                StabilityText.Text = $"{_angleStability:F2}";

                if (Math.Abs(dx) + Math.Abs(dy) < 3)
                {
                    _jitterAmount++;
                    JitterText.Text = $"{_jitterAmount:F0}";
                }

                _previousAngle = _lastAngle;

                double counts = Math.Abs(dx) + Math.Abs(dy);
                double cmMoved = counts / _dpi * 2.54;
                _totalDistance += cmMoved;
                TotalDistanceText.Text = $"{_totalDistance:F2} cm";

                DateTime now = DateTime.Now;
                double deltaTime = (now - _lastMoveTime).TotalSeconds;
                _lastMoveTime = now;

                if (deltaTime > 0)
                {
                    _currentVelocity = cmMoved / deltaTime;
                    SpeedText.Text = $"{_currentVelocity:F2} cm/s";
                    CurrentSpeedText.Text = $"{_currentVelocity:F1} cm/s";

                    if (_wasIdle && _currentVelocity > 20)
                    {
                        _idleBurstCount++;
                        IdleBurstsText.Text = $"{_idleBurstCount}";
                    }
                    _wasIdle = (_currentVelocity < 1);

                    if (_currentVelocity > _peakVelocity)
                    {
                        _peakVelocity = _currentVelocity;
                        PeakSpeedText.Text = $"{_peakVelocity:F2}";
                    }

                    if (_currentVelocity > 50)
                    {
                        if ((now - _lastFlickTime).TotalMilliseconds > 150)
                        {
                            _flickCount++;
                            FlicksCountText.Text = $"{_flickCount}";

                            if (_currentVelocity < 100)
                            {
                                _smallFlicks++;
                                SmallFlicksText.Text = $"{_smallFlicks}";
                            }
                            else
                            {
                                _largeFlicks++;
                                LargeFlicksText.Text = $"{_largeFlicks}";
                            }

                            _lastFlickTime = now;
                        }
                    }

                    double velocityChange = Math.Abs(_currentVelocity - _previousVelocity);
                    _correctionSharpness = Math.Min(velocityChange * 2, 100);
                    CorrectionSharpnessText.Text = $"{_correctionSharpness:F0}";

                    _previousVelocity = _currentVelocity;
                }

                StatsUpdated?.Invoke();
            });
        }

        // TIMER TICK
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_isTracking) return;

            TimeSpan elapsed = DateTime.Now - _sessionStart;
            SessionTimeText.Text = $"{elapsed:mm\\:ss}";
            _sessionSeconds = elapsed.TotalSeconds;

            if (_wasIdle) _idleTimeSeconds += 1.0;
            else _idleTimeSeconds = 0;

            int eventsThisTick = _movementEvents - _lastMovementCount;
            _movementCountThisSecond = eventsThisTick;
            MpsText.Text = $"{_movementCountThisSecond}";
            _lastMovementCount = _movementEvents;

            _rollingDensity = _movementCountThisSecond;
            RollingDensityText.Text = $"{_rollingDensity:F2}";

            if (_movementEvents > 0)
                _averageDistancePerEvent = _distancePerEventTotal / _movementEvents;

            if (_movementEvents == _lastMovementEvents) _idleTime++;
            else _idleTime = 0;
            _lastMovementEvents = _movementEvents;

            if (_sessionSeconds > 0)
            {
                _movementDensity = _movementEvents / _sessionSeconds;
                DensityText.Text = $"{_movementDensity:F2}";
            }

            if (_sessionSeconds > 0)
            {
                _averageVelocity = _totalDistance / _sessionSeconds;
                AverageSpeedText.Text = $"{_averageVelocity:F2}";
            }

            if (_sessionSeconds > 0)
            {
                double angleRate = _angleChangeTotal / _sessionSeconds;
                AngleChangeText.Text = $"{angleRate:F1} deg/s";
            }
        }

        // START / STOP
        public void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(DpiInput.Text, out double dpi)) _dpi = dpi;
            if (double.TryParse(SensitivityInput.Text, out double sens)) _sensitivity = sens;

            _isTracking = true;
            _sessionStart = DateTime.Now;
            _totalDistance = 0;

            TotalDistanceText.Text = "0";
            DxDyText.Text = "dX: 0  dY: 0";

            double cm360 = CalculateCmPer360();
            CmPer360Text.Text = $"{cm360:F2}";

            _rawInput.Start();
            _timer.Start();
            RecordingPanel.Visibility = Visibility.Visible;

            LogService.Info($"Tracking started — DPI:{_dpi} Sens:{_sensitivity} Profile:{_selectedProfile?.Name}");
        }

        public void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _isTracking = false;
            _timer.Stop();
            _rawInput.Stop();
            RecordingPanel.Visibility = Visibility.Collapsed;
            LogService.Info("Tracking stopped");
        }

        public void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _isTracking = false;
            _timer.Stop();
            RecordingPanel.Visibility = Visibility.Collapsed;

            _totalDistance = 0;
            _peakVelocity = 0;
            _averageVelocity = 0;
            _currentVelocity = 0;
            _movementEvents = 0;
            _sessionSeconds = 0;
            _flickCount = 0;
            _smallFlicks = 0;
            _largeFlicks = 0;
            _jitterAmount = 0;
            _movementDensity = 0;
            _idleTime = 0;
            _idleBurstCount = 0;
            _lastAngle = 0;
            _angleStability = 0;
            _angleChangeTotal = 0;
            _movementConsistency = 100;
            _smoothnessScore = 100;
            _correctionSharpness = 0;
            _overallQualityScore = 100;

            LastDeltaText.Text = "Last Delta: 0, 0";
            DxDyText.Text = "dX: 0  dY: 0";
            ResetDisplaysToIdle();
        }

        // OVERLAY
        private OverlayWindow? _overlay;

        private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (!TrialService.RequestProAccess("In-Game Overlay")) return;

            if (_overlay == null)
            {
                _overlay = new OverlayWindow();
                _overlay.Closed += (s, args) =>
                {
                    _overlay = null;
                    ToggleOverlayButton.Content = "Show Overlay";
                };
                _overlay.Show();
                ToggleOverlayButton.Content = "Hide Overlay";
            }
            else
            {
                _overlay.Close();
                _overlay = null;
                ToggleOverlayButton.Content = "Show Overlay";
            }
        }

        // NAVIGATION
        private void NavSettings_Click(object sender, RoutedEventArgs e)
            => new SettingsWindow { Owner = this }.Show();

        private void NavHistory_Click(object sender, RoutedEventArgs e)
        {
            // TASK-14: Full history is Pro; basic tracking is always free
            if (!TrialService.RequestProAccess("Full Session History")) return;
            new SessionHistoryWindow { Owner = this }.Show();
        }

        private void NavAbout_Click(object sender, RoutedEventArgs e)
            => new AboutWindow { Owner = this }.Show();

        private void NavHome_Click(object sender, RoutedEventArgs e)
        {
            this.Activate();
            this.Focus();
        }

        private void OpenGlossary_Click(object sender, RoutedEventArgs e)
            => new GlossaryWindow { Owner = this }.Show();

        private void OpenHelp_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Help documentation coming soon.", "Help");

        private void OpenAbout_Click(object sender, RoutedEventArgs e)
            => new AboutWindow().ShowDialog();

        private void OpenExport_Click(object sender, RoutedEventArgs e)
        {
            if (!TrialService.RequestProAccess("Export")) return;

            var sessions = SessionStorage.LoadAll();
            if (sessions.Count == 0)
            {
                MessageBox.Show("No sessions to export yet. Complete a session first.", "Nothing to Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Session History",
                Filter = "CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json",
                FileName = $"CleanAimTracker_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                if (dialog.FilterIndex == 1)
                    ExportService.ExportAllToCsv(sessions, dialog.FileName);
                else
                    System.IO.File.WriteAllText(dialog.FileName,
                        System.Text.Json.JsonSerializer.Serialize(sessions,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                MessageBox.Show($"Exported {sessions.Count} sessions successfully!", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenConverter_Click(object sender, RoutedEventArgs e)
        {
            // TASK-14: Converter is free — utility that keeps users in the app
            new ConverterWindow { Owner = this }.Show();
        }

        private void OpenWeeklyReport_Click(object sender, RoutedEventArgs e)
        {
            // TASK-14: Weekly report is a Pro trend feature
            if (!TrialService.RequestProAccess("Weekly Report & Trends")) return;
            new WeeklyReportWindow { Owner = this }.Show();
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.Load();
            bool isDark = (settings.ThemeMode ?? "Dark") == "Dark";
            string newMode = isDark ? "Light" : "Dark";
            settings.ThemeMode = newMode;
            settings.Theme = newMode;
            SettingsService.Save(settings);
            ThemeService.ApplyTheme(newMode);
            ThemeToggleBtn.Content = newMode == "Dark" ? "🌙  Dark Mode" : "☀️  Light Mode";
        }

        private void OpenSessionHistory_Click(object sender, RoutedEventArgs e)
        {
            if (!TrialService.RequestProAccess("Full Session History")) return;
            new SessionHistoryWindow { Owner = this }.Show();
        }

        private void GameProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameProfileCombo.SelectedIndex < 0 || GameProfileCombo.SelectedIndex >= _gameProfiles.Count)
                return;
            _selectedProfile = _gameProfiles[GameProfileCombo.SelectedIndex];
            LogService.Info($"Profile changed to {_selectedProfile.Name}");
        }

        private void OpenSummary_Click(object sender, RoutedEventArgs e)
        {
            // TASK-14: Basic summary is always free
            if (_sessionSeconds < 60)
            {
                MessageBox.Show(
                    $"Session too short for analysis. Keep going — {(int)(60 - _sessionSeconds)} more seconds needed.",
                    "Session Too Short",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _isTracking = false;
            _timer.Stop();

            var summary = BuildSessionSummary();
            SessionStorage.SaveSession(summary);

            // TASK-16: Update streak after saving
            var streakResult = StreakService.UpdateStreak();

            LoadTodayStats();
            UpdateTrialBanner();   // refresh count after session saves

            var rec = RecommendationEngine.Analyze(summary, _selectedProfile);
            new SummaryWindow(summary, rec, streakResult) { Owner = this }.Show();

            // TASK-11: Value moment — show upgrade nudge at sessions 3, 10, 25
            if (!TrialService.IsFullVersion())
            {
                int count = TrialService.SessionsCompleted();
                if (TrialService.IsValueMoment(count))
                {
                    var result = MessageBox.Show(
                        TrialService.GetValueMomentMessage(count) +
                        "\n\nUpgrade now?",
                        "🎯 Nice Work!",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.None);

                    if (result == MessageBoxResult.Yes)
                        UpgradeDialog.Show("Pro Features");
                }
            }
        }

        public void OpenRecommendation_Click(object sender, RoutedEventArgs e)
        {
            // TASK-14: Basic recommendations always free

            var summary = SessionStorage.LoadLast();
            if (summary == null)
            {
                MessageBox.Show("No session data available. Start a session first.");
                return;
            }

            if (_selectedProfile == null)
            {
                MessageBox.Show("Please select a game profile.");
                return;
            }

            var rec = RecommendationEngine.Analyze(summary, _selectedProfile);
            new RecommendationWindow(rec) { Owner = this }.ShowDialog();
        }

        private void OpenAddProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!TrialService.RequestProAccess("Custom Profiles")) return;

            var win = new AddProfileWindow();
            win.ShowDialog();

            if (win.ProfileSaved)
                RefreshProfiles();
        }

        private void TrialBanner_Click(object sender, RoutedEventArgs e)
        {
            UpgradeDialog.Show("Pro Features");
        }

        private void OpenAimTrainer_Click(object sender, RoutedEventArgs e)
        {
            // TASK-14: Aim Trainer is free — it's a core value driver
            new AimTrainerWindow { Owner = this }.Show();
        }

        private void ViewLastCoachReport_Click(object sender, RoutedEventArgs e)
        {
            AimTrainerResultWindow.OpenLastReport(this);
        }

        // HELPERS
        private double CalculateCmPer360()
            => (360.0 / (_dpi * _sensitivity)) * 2.54;

        private SessionSummary BuildSessionSummary()
        {
            return new SessionSummary
            {
                DPI = (int)Math.Round(_dpi),
                Sensitivity = _sensitivity,
                ProfileName = _selectedProfile?.Name ?? "Unknown",
                TotalDistanceCm = _totalDistance,
                PeakVelocity = _peakVelocity,
                AverageVelocity = _averageVelocity,
                FlickCount = _flickCount,
                SmallFlickCount = _smallFlicks,
                LargeFlickCount = _largeFlicks,
                JitterAmount = _jitterAmount,
                IdleBurstCount = _idleBurstCount,
                SmoothnessScore = _smoothnessScore,
                CorrectionSharpness = _correctionSharpness,
                MovementConsistency = _movementConsistency,
                OverallQualityScore = _overallQualityScore,
                SessionSeconds = _sessionSeconds,
                Timestamp = DateTime.Now,
                CmPer360 = CalculateCmPer360(),
                IdlePercentage = _sessionSeconds > 0
                    ? (_idleTimeSeconds / _sessionSeconds) * 100.0
                    : 0,
                TotalSamples = _movementEvents
            };
        }

        private void LoadTodayStats()
        {
            try
            {
                var all = SessionStorage.LoadAll();
                var today = all.Where(s => s.Timestamp.Date == DateTime.Today).ToList();
                TodaySessionsText.Text = today.Count.ToString();

                // Streak: count consecutive days going backwards from today
                int streak = 0;
                var day = DateTime.Today;
                while (true)
                {
                    bool hasSession = all.Any(s => s.Timestamp.Date == day);
                    if (!hasSession) break;
                    streak++;
                    day = day.AddDays(-1);
                }
                StreakText.Text = streak.ToString();

                // Best quality today
                if (today.Count > 0)
                    BestQualityTodayText.Text = $"{today.Max(s => s.OverallQualityScore):F0}";
                else
                    BestQualityTodayText.Text = "—";

                // Weekly average
                var weekAgo = DateTime.Today.AddDays(-7);
                var weekSessions = all.Where(s => s.Timestamp.Date >= weekAgo).ToList();
                if (weekSessions.Count > 0)
                    WeeklyAvgText.Text = $"{weekSessions.Average(s => s.OverallQualityScore):F0}";
                else
                    WeeklyAvgText.Text = "—";

                // TASK-15: Tier badge
                var tier = ProgressionService.GetTier(all);
                TierText.Text = $"{tier.Emoji} {tier.Name}";
                TierText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tier.Color));
            }
            catch { /* ignore if no data */ }
        }

        private void ResetDisplaysToIdle()
        {
            DxDyText.Text                = "--";
            TotalDistanceText.Text       = "--";
            CmPer360Text.Text            = "--";
            SpeedText.Text               = "--";
            CurrentSpeedText.Text        = "--";
            PeakSpeedText.Text           = "--";
            AverageSpeedText.Text        = "--";
            MpsText.Text                 = "--";
            RollingDensityText.Text      = "--";
            PeakVelocityChangeText.Text  = "--";
            FlicksCountText.Text         = "--";
            SmallFlicksText.Text         = "--";
            LargeFlicksText.Text         = "--";
            IdleBurstsText.Text          = "--";
            OverallQualityText.Text      = "--";
            SmoothnessText.Text          = "--";
            CorrectionSharpnessText.Text = "--";
            StabilityText.Text           = "--";
            AngleChangeText.Text         = "--";
            JitterText.Text              = "--";
            DensityText.Text             = "--";
            SessionTimeText.Text         = "--";
        }

        private void RefreshProfiles()
        {
            var profiles = ProfileStorage.LoadProfiles();
            _gameProfiles = GameProfile.GetAllProfiles(profiles);

            GameProfileCombo.Items.Clear();
            foreach (var p in _gameProfiles)
                GameProfileCombo.Items.Add(p.DisplayName);

            if (_gameProfiles.Count > 0)
            {
                GameProfileCombo.SelectedIndex = 0;
                _selectedProfile = _gameProfiles[0];
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _rawInput.Stop();
            base.OnClosed(e);
        }
    }
}
