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
using System.Windows.Input;
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
        private double _smoothnessScore = 0;
        private double _correctionSharpness = 0;
        private double _movementConsistency = 0;
        private double _overallQualityScore = 0;

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
        private DispatcherTimer? _countdownTimer;
        private int _onboardingCountdown = 3;

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

                OnboardingOverlay.Visibility = Visibility.Visible;
                _onboardingCountdown = 3;
                OnboardingCountdownText.Text = "3";

                _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _countdownTimer.Tick += (_, _) =>
                {
                    _onboardingCountdown--;
                    if (_onboardingCountdown <= 0)
                    {
                        _countdownTimer?.Stop();
                        _countdownTimer = null;
                        LaunchOnboardingSession();
                    }
                    else
                    {
                        OnboardingCountdownText.Text = _onboardingCountdown.ToString();
                    }
                };
                _countdownTimer.Start();
            }
        }

        private void OnboardingGotIt_Click(object sender, RoutedEventArgs e)
        {
            _countdownTimer?.Stop();
            _countdownTimer = null;
            LaunchOnboardingSession();
        }

        private void LaunchOnboardingSession()
        {
            OnboardingOverlay.Visibility = Visibility.Collapsed;

            var trainer = new AimTrainerWindow { Owner = this };
            trainer.OnboardingSessionCompleted += () =>
            {
                Dispatcher.BeginInvoke(() => ShowAimTrainerHighlight());
            };
            trainer.Show();
            trainer.BeginOnboardingSession();
        }

        private void ShowAimTrainerHighlight()
        {
            var pos = AimTrainerNavBtn.TranslatePoint(new Point(0, 0), this);
            AimTrainerCallout.Margin = new Thickness(pos.X + 185, pos.Y - 4, 0, 0);
            AimTrainerHighlight.Visibility = Visibility.Visible;
        }

        private void AimTrainerHighlight_Click(object sender, MouseButtonEventArgs e)
        {
            AimTrainerHighlight.Visibility = Visibility.Collapsed;
            var s = SettingsService.Load();
            s.FirstLaunchComplete = true;
            SettingsService.Save(s);

            if (!s.OnboardingTourComplete)
                StartTour();
        }

        // ─────────────────────────────────────────────────────────────
        // GUIDED TOUR
        // ─────────────────────────────────────────────────────────────
        private int _tourStep = 0;

        private static readonly (string Name, string Text)[] TourStops =
        {
            ("TodayStatsCard",    "Your session stats live here. Quality score, streak, and personal best update after every drill."),
            ("AimTrainerNavBtn",  "Start a drill here anytime. Five scenarios, four difficulty levels."),
            ("StreakPanel",       "Your daily streak. Train every day to keep it alive."),
            ("TierBadge",        "Your progression tier. It improves as your average quality score rises."),
            ("NavHistoryBtn",    "Every session is logged here with a trend chart so you can see improvement over time."),
            ("NavLastReportBtn", "Tap here anytime to re-read your last AI coaching report."),
            ("NavSensitivityBtn","After a few sessions the app will recommend your optimal sensitivity based on your actual movement data."),
        };

        private void StartTour()
        {
            _tourStep = 0;
            TourFinalCard.Visibility  = Visibility.Collapsed;
            TourBubble.Visibility     = Visibility.Collapsed;
            TourHighlight.Visibility  = Visibility.Collapsed;
            TourOverlay.Visibility    = Visibility.Visible;
            ShowTourStep(0);
        }

        private void ShowTourStep(int step)
        {
            if (step >= TourStops.Length) { ShowTourFinalScreen(); return; }

            var (name, text) = TourStops[step];
            if (FindName(name) is not FrameworkElement el) { AdvanceTour(); return; }

            var pos = el.TranslatePoint(new Point(0, 0), this);
            double w = el.ActualWidth;
            double h = el.ActualHeight;

            // Highlight ring
            TourHighlight.Width   = w + 8;
            TourHighlight.Height  = h + 8;
            TourHighlight.Margin  = new Thickness(pos.X - 4, pos.Y - 4, 0, 0);
            TourHighlight.Visibility = Visibility.Visible;

            // Bubble text + last-step label
            TourBubbleText.Text  = text;
            TourNextBtn.Content  = step == TourStops.Length - 1 ? "Done →" : "Next →";

            // Bubble positioning — prefer right of element, fall back to below, then above.
            // All coordinates come from TranslatePoint so they're correct on any screen size.
            const double BubbleW = 252, BubbleH = 110, Gap = 12;

            double rightX = pos.X + w + Gap;
            double belowY = pos.Y + h + Gap;
            double aboveY = pos.Y - BubbleH - Gap;

            double bx, by;
            if (rightX + BubbleW <= ActualWidth)          // fits to the right
            {
                bx = rightX;
                by = Math.Max(8, Math.Min(pos.Y, ActualHeight - BubbleH - 8));
            }
            else if (belowY + BubbleH <= ActualHeight)    // fits below
            {
                bx = Math.Max(8, Math.Min(pos.X, ActualWidth - BubbleW - 8));
                by = belowY;
            }
            else                                           // place above
            {
                bx = Math.Max(8, Math.Min(pos.X, ActualWidth - BubbleW - 8));
                by = Math.Max(8, aboveY);
            }

            TourBubble.Margin     = new Thickness(bx, by, 0, 0);
            TourBubble.Visibility = Visibility.Visible;
        }

        private void ShowTourFinalScreen()
        {
            TourHighlight.Visibility = Visibility.Collapsed;
            TourBubble.Visibility    = Visibility.Collapsed;
            TourFinalCard.Visibility = Visibility.Visible;
        }

        private void AdvanceTour()
        {
            _tourStep++;
            ShowTourStep(_tourStep);
        }

        private void CompleteTour()
        {
            TourOverlay.Visibility = Visibility.Collapsed;
            var s = SettingsService.Load();
            s.OnboardingTourComplete = true;
            SettingsService.Save(s);
        }

        private void TourNext_Click(object sender, RoutedEventArgs e)    => AdvanceTour();
        private void TourLetsGo_Click(object sender, RoutedEventArgs e)  => CompleteTour();

        private void TourOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_tourStep >= TourStops.Length) CompleteTour();
            else AdvanceTour();
        }

        private void TourBubble_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // prevent click propagating to TourOverlay_MouseDown
        }

        // TRIAL BANNER
        public void UpdateTrialBanner()
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
                if (_sessionSeconds >= 3)
                    OverallQualityText.Text = $"{_overallQualityScore:F0}";

                if (eventDistance > _peakDistancePerEvent)
                    _peakDistancePerEvent = eventDistance;

                _lastAngle = Math.Atan2(dy, dx) * (180 / Math.PI);
                double angleDiff = Math.Abs(_lastAngle - _previousAngle);

                _smoothnessScore = Math.Clamp(100 - Math.Abs(angleDiff) * 2, 0, 100);
                if (_sessionSeconds >= 3)
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
                    if (_sessionSeconds >= 3)
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

            try
            {
                _rawInput.Start();
            }
            catch (Exception ex)
            {
                _isTracking = false;
                LogService.Error("Failed to start raw input", ex);
                MessageBox.Show(
                    "Could not start mouse tracking. Try running the app as Administrator if the problem persists.",
                    "Tracking Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

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

            // TASK-11: Populate plain-English verdicts if session was long enough
            if (_sessionSeconds >= 10)
                PopulateTrackerInterpretations(BuildSessionSummary());
        }

        private void PopulateTrackerInterpretations(SessionSummary s)
        {
            // Movement / cm/360 verdict
            string movementVerdict = s.CmPer360 < 20
                ? $"cm/360 is low ({s.CmPer360:F1}) — sensitivity may be limiting micro-adjustment."
                : s.CmPer360 > 60
                ? $"cm/360 is high ({s.CmPer360:F1}) — consider lowering sensitivity for more control."
                : $"cm/360 is in a good range ({s.CmPer360:F1}).";

            // Velocity consistency verdict
            string velocityVerdict = s.AverageVelocity > 0 && s.PeakVelocity > 0
                ? (s.AverageVelocity / s.PeakVelocity > 0.6
                    ? "Consistent speed — good movement control."
                    : "High speed variance — movement is inconsistent.")
                : "Not enough movement data.";

            // Overall quality verdict
            string qualityVerdict = s.OverallQualityScore >= 75
                ? $"Strong session — quality {s.OverallQualityScore:F0}/100."
                : s.OverallQualityScore >= 50
                ? $"Average session — quality {s.OverallQualityScore:F0}/100. Room to improve."
                : $"Tough session — quality {s.OverallQualityScore:F0}/100. Check grip and surface.";

            // Smoothness verdict
            string smoothnessVerdict = s.SmoothnessScore >= 80
                ? "Smooth movement — good mouse control."
                : s.SmoothnessScore >= 60
                ? "Some jitter detected — check sensitivity and grip pressure."
                : "High jitter — sensitivity may be too high or grip too tense.";

            MovementVerdictText.Text   = movementVerdict;
            VelocityVerdictText.Text   = velocityVerdict;
            QualityVerdictText.Text    = qualityVerdict;
            SmoothnessVerdictText.Text = smoothnessVerdict;
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
            _movementConsistency = 0;
            _smoothnessScore = 0;
            _correctionSharpness = 0;
            _overallQualityScore = 0;

            LastDeltaText.Text = "Last Delta: 0, 0";
            DxDyText.Text = "dX: 0  dY: 0";
            ResetDisplaysToIdle();

            // Clear verdict lines so they don't carry over to the next session
            MovementVerdictText.Text   = "";
            VelocityVerdictText.Text   = "";
            QualityVerdictText.Text    = "";
            SmoothnessVerdictText.Text = "";
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

        private void NavAchievements_Click(object sender, RoutedEventArgs e)
            => new AchievementsWindow { Owner = this }.ShowDialog();

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
            try
            {
                SessionStorage.SaveSession(summary);
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to save session", ex);
                MessageBox.Show(
                    "Your session couldn't be saved — storage may be full or unavailable. " +
                    "Your stats are still visible for this session.",
                    "Save Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

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

                // TASK-12: Session context line
                string dayName = DateTime.Today.DayOfWeek.ToString();
                int todayCount = today.Count;
                SessionContextText.Text = todayCount > 0
                    ? $"{dayName} · {todayCount} session{(todayCount == 1 ? "" : "s")} today"
                    : $"{dayName} · No sessions yet today";

                LoadPlayerPanel(all);
            }
            catch { /* ignore if no data */ }
        }

        // ── TASK-08–11: Player Panel ──────────────────────────────────────
        private void LoadPlayerPanel(System.Collections.Generic.IEnumerable<SessionSummary> sessions)
        {
            try
            {
                var list = sessions.ToList();
                double avgQuality = list.Count > 0
                    ? list.Average(s => s.OverallQualityScore)
                    : 0;

                // ── TASK-08: Tier Hero Card ──────────────────────────────
                var tier = ProgressionService.GetTier(list);
                TierEmojiText.Text = tier.Emoji;
                TierNameText.Text  = tier.Name;
                TierAvgText.Text   = list.Count > 0
                    ? $"Avg quality: {avgQuality:F0} pts"
                    : "No sessions yet";
                TierNextGoalText.Text = tier.NextGoal;
                TierProgressBar.Value = CalculateTierProgress(avgQuality, tier.Name) * 100;

                var tierColor = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tier.Color));
                TierNameText.Foreground = tierColor;
                TierProgressBar.Foreground = tierColor;

                // ── TASK-09: Streak Card ──────────────────────────────────
                var (currentStreak, bestStreak) = StreakService.GetStreakInfo();
                StreakValueText.Text = currentStreak.ToString();
                BestStreakText.Text  = $"Best: {bestStreak} days";
                StreakFlameText.Text = currentStreak switch
                {
                    >= 30 => "🔥🔥🔥",
                    >= 14 => "🔥🔥",
                    >= 3  => "🔥",
                    _     => "💤",
                };
                StreakValueText.Foreground = currentStreak >= 7
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange)
                    : (System.Windows.Media.Brush)FindResource("PrimaryText");

                // ── TASK-10: Daily Challenge Card ─────────────────────────
                var settings  = SettingsService.Load();
                var challenge = DailyChallengeService.GetToday();
                bool done     = DailyChallengeService.IsTodayComplete(settings);
                ChallengeDescText.Text = challenge.ShortDesc;
                if (done)
                {
                    ChallengeStatusBadge.Text     = " · ✓ Done";
                    ChallengeStatusBadge.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(76, 175, 80));
                    ChallengeAcceptBtn.Visibility  = Visibility.Collapsed;
                    ChallengeDoneText.Visibility   = Visibility.Visible;
                }
                else
                {
                    ChallengeStatusBadge.Text      = " · ⚡ Active";
                    ChallengeStatusBadge.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryText");
                    ChallengeAcceptBtn.Visibility  = Visibility.Visible;
                    ChallengeDoneText.Visibility   = Visibility.Collapsed;
                }

                // ── TASK-11: Achievement Badge Row ────────────────────────
                var unlocked = AchievementService.LoadUnlocked();
                AchievementCountText.Text = $"  {unlocked.Count} / {AchievementService.GetTotalCount()}";
                if (unlocked.Count > 0)
                {
                    AchievementBadges.ItemsSource   = unlocked
                        .OrderByDescending(a => a.UnlockedAt)
                        .Take(10)
                        .ToList();
                    AchievementBadges.Visibility    = Visibility.Visible;
                    AchievementEmptyText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AchievementBadges.Visibility    = Visibility.Collapsed;
                    AchievementEmptyText.Visibility = Visibility.Visible;
                }
            }
            catch { /* non-critical */ }
        }

        private static double CalculateTierProgress(double avg, string tierName) => tierName switch
        {
            "Rookie"  => avg >= 40 ? 1.0 : Math.Max(0, avg / 40.0),
            "Bronze"  => avg >= 55 ? 1.0 : Math.Max(0, (avg - 40) / 15.0),
            "Silver"  => avg >= 70 ? 1.0 : Math.Max(0, (avg - 55) / 15.0),
            "Gold"    => avg >= 82 ? 1.0 : Math.Max(0, (avg - 70) / 12.0),
            "Elite"   => 1.0,
            _         => 0,
        };

        private void DailyChallenge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var settings  = SettingsService.Load();
            var challenge = DailyChallengeService.GetToday();

            if (DailyChallengeService.IsTodayComplete(settings))
            {
                MessageBox.Show(
                    "You already completed today's challenge. Come back tomorrow!",
                    "Challenge Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var trainer = new AimTrainerWindow { Owner = this };

            trainer.Closed += (s, args) =>
            {
                var latestResults = AimTrainerStorage.LoadAll();
                if (latestResults.Count > 0)
                {
                    var latest        = latestResults.OrderByDescending(r => r.Timestamp).First();
                    var freshSettings = SettingsService.Load();
                    bool completed    = DailyChallengeService.TryComplete(challenge, latest, freshSettings);
                    if (completed)
                    {
                        LoadPlayerPanel(SessionStorage.LoadAll());
                        MessageBox.Show(
                            "Challenge complete! Well done.",
                            "Daily Challenge",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            };

            trainer.PreSelectScenario(challenge.Scenario, challenge.Difficulty);
            trainer.Show();
        }

        private void ChallengeAccept_Click(object sender, RoutedEventArgs e)
        {
            var challenge = DailyChallengeService.GetToday();
            MessageBox.Show(
                $"Challenge: {challenge.Description}\n\nSet Aim Trainer to {challenge.Scenario} · {challenge.Difficulty} and complete a drill to earn it.",
                "Accept Challenge",
                MessageBoxButton.OK,
                MessageBoxImage.None);
            // Open Aim Trainer
            OpenAimTrainer_Click(sender, e);
        }

        private void AchievementPanel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            NavAchievements_Click(sender, e);
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
