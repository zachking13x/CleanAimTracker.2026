using CleanAimTracker.Helpers;
using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Windows;
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
        // -----------------------------
        // RAW INPUT + PROFILE DATA
        // -----------------------------
        private readonly RawInputService _rawInput;
        private List<GameProfile> _gameProfiles = new();
        private GameProfile _selectedProfile;

        // -----------------------------
        // MOVEMENT + VELOCITY
        // -----------------------------
        private double _totalDistance = 0;
        private double _currentVelocity = 0;
        private double _peakVelocity = 0;
        private double _averageVelocity = 0;
        private double _previousVelocity = 0;
        private DateTime _lastMoveTime = DateTime.Now;


        // -----------------------------
        // ANGLES + QUALITY
        // -----------------------------
        private double _lastAngle = 0;
        private double _previousAngle = 0;
        private double _angleChangeTotal = 0;
        private double _angleStability = 0;
        private double _smoothnessScore = 100;
        private double _correctionSharpness = 0;
        private double _movementConsistency = 100;
        private double _overallQualityScore = 100;

        // -----------------------------
        // FLICKS + JITTER + DENSITY
        // -----------------------------
        private int _flickCount = 0;
        private int _smallFlicks = 0;
        private int _largeFlicks = 0;
        private DateTime _lastFlickTime = DateTime.MinValue;
        private double _jitterAmount = 0;
        private double _movementDensity = 0;
        private double _rollingDensity = 0;

        // -----------------------------
        // IDLE + BURSTS
        // -----------------------------
        private bool _wasIdle = false;
        private double _idleTimeSeconds = 0;
        private double _idlePercentage = 0;
        private int _idleBurstCount = 0;
        private double _idleTime = 0;

        // -----------------------------
        // EVENT COUNTS
        // -----------------------------
        private int _movementEvents = 0;
        private int _movementCountThisSecond = 0;
        private int _lastMovementCount = 0;
        private int _lastMovementEvents = 0;

        // -----------------------------
        // DISTANCE PER EVENT
        // -----------------------------
        private double _distancePerEventTotal = 0;
        private double _averageDistancePerEvent = 0;
        private double _peakDistancePerEvent = 0;

        // -----------------------------
        // SESSION + TIMER
        // -----------------------------
        private bool _isTracking = false;
        private DateTime _sessionStart;
        private readonly DispatcherTimer _timer = new();
        public double _sessionSeconds = 0;

        // -----------------------------
        // DPI + SENSITIVITY
        // -----------------------------
        private double _dpi = 800;
        private double _sensitivity = 1.0;

        public event Action? StatsUpdated;

        public MainWindow()
        {
            InitializeComponent();
            LogService.Info("MainWindow initialized");

            // Load settings
            var settings = SettingsService.Load();
            _dpi = settings.DPI;
            _sensitivity = settings.Sensitivity;
            DpiInput.Text = _dpi.ToString("F0");
            SensitivityInput.Text = _sensitivity.ToString("F4");

            // Load profiles
            var profiles = ProfileStorage.LoadProfiles();
            _gameProfiles = GameProfile.GetAllProfiles(profiles);

            foreach (var p in _gameProfiles)
                GameProfileCombo.Items.Add(p.DisplayName);

            int savedIndex = _gameProfiles.FindIndex(p => p.Name == settings.SelectedProfile);
            GameProfileCombo.SelectedIndex = savedIndex >= 0 ? savedIndex : 0;
            _selectedProfile = _gameProfiles[GameProfileCombo.SelectedIndex];

            UpdateTrialBanner();

            // Raw input
            _rawInput = new RawInputService();
            _rawInput.MouseMoved += OnMouseMoved;

            // Timer
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            // Window handle
            this.SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                _rawInput.Register(hwnd);

                HwndSource source = HwndSource.FromHwnd(hwnd);
                source.AddHook(WndProc);
            };
        }

        // -----------------------------
        // TRIAL BANNER
        // -----------------------------
        private void UpdateTrialBanner()
        {
            string status = TrialService.GetStatusText();
            TrialBannerText.Text = status;
            TrialBannerText.Visibility = status == "Full Version"
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // -----------------------------
        // RAW INPUT HOOK
        // -----------------------------
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;

            if (msg == WM_INPUT)
            {
                var (dx, dy) = _rawInput.ProcessRawInput(lParam);
                if (dx != 0 || dy != 0)
                    OnMouseMoved(dx, dy);
            }

            return IntPtr.Zero;
        }

        // -----------------------------
        // MOUSE MOVEMENT HANDLER
        // -----------------------------
        private void OnMouseMoved(int dx, int dy)
        {
            Dispatcher.Invoke(() =>
            {
                LastDeltaText.Text = $"Last Delta: {dx}, {dy}";
                if (!_isTracking) return;

                _movementEvents++;
                DxDyText.Text = $"dX: {dx}  dY: {dy}";

                // Distance per event
                double eventDistance = Math.Sqrt(dx * dx + dy * dy);
                _distancePerEventTotal += eventDistance;

                // Movement consistency
                double deviation = Math.Abs(eventDistance - _averageDistancePerEvent);
                _movementConsistency = Math.Clamp(100 - deviation * 10, 0, 100);
                MovementConsistencyText.Text = $"{_movementConsistency:F0}";

                // Overall quality
                _overallQualityScore = Math.Clamp(
                    (_smoothnessScore + _correctionSharpness + _movementConsistency) / 3,
                    0, 100);
                OverallQualityText.Text = $"{_overallQualityScore:F0}";

                // Peak distance per event
                if (eventDistance > _peakDistancePerEvent)
                    _peakDistancePerEvent = eventDistance;

                // Angle
                _lastAngle = Math.Atan2(dy, dx) * (180 / Math.PI);
                AngleText.Text = $"{_lastAngle:F0}°";

                double angleDiff = Math.Abs(_lastAngle - _previousAngle);

                // Smoothness
                _smoothnessScore = Math.Clamp(100 - Math.Abs(angleDiff) * 2, 0, 100);
                SmoothnessText.Text = $"{_smoothnessScore:F0}";

                // Angle totals
                _angleChangeTotal += angleDiff;
                AngleChangeText.Text = $"{_angleChangeTotal:F2}";

                _angleStability = angleDiff;
                StabilityText.Text = $"{_angleStability:F2}";

                // Jitter
                if (Math.Abs(dx) + Math.Abs(dy) < 3)
                {
                    _jitterAmount++;
                    JitterText.Text = $"{_jitterAmount:F0}";
                }

                _previousAngle = _lastAngle;
                PreviousAngleText.Text = $"{_previousAngle:F0}°";

                // Distance
                double counts = Math.Abs(dx) + Math.Abs(dy);
                double cmMoved = counts / _dpi * 2.54;
                _totalDistance += cmMoved;
                TotalDistanceText.Text = $"{_totalDistance:F2} cm";

                // Velocity
                DateTime now = DateTime.Now;
                double deltaTime = (now - _lastMoveTime).TotalSeconds;
                _lastMoveTime = now;

                if (deltaTime > 0)
                {
                    _currentVelocity = cmMoved / deltaTime;
                    SpeedText.Text = $"{_currentVelocity:F2} cm/s";
                    CurrentSpeedText.Text = $"{_currentVelocity:F1} cm/s";

                    // Idle bursts
                    if (_wasIdle && _currentVelocity > 20)
                    {
                        _idleBurstCount++;
                        IdleBurstsText.Text = $"{_idleBurstCount}";
                    }
                    _wasIdle = (_currentVelocity < 1);

                    // Peak velocity
                    if (_currentVelocity > _peakVelocity)
                    {
                        _peakVelocity = _currentVelocity;
                        PeakSpeedText.Text = $"{_peakVelocity:F2}";
                    }

                    // Flick detection
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

                    // Correction sharpness
                    double velocityChange = Math.Abs(_currentVelocity - _previousVelocity);
                    _correctionSharpness = Math.Min(velocityChange * 2, 100);
                    CorrectionSharpnessText.Text = $"{_correctionSharpness:F0}";

                    _previousVelocity = _currentVelocity;
                }

                StatsUpdated?.Invoke();
            });
        }

        // -----------------------------
        // TIMER TICK (1s)
        // -----------------------------
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_isTracking) return;

            TimeSpan elapsed = DateTime.Now - _sessionStart;
            SessionTimeText.Text = $"{elapsed:mm\\:ss}";
            _sessionSeconds = elapsed.TotalSeconds;

            // Idle %
            if (_sessionSeconds > 0)
            {
                _idlePercentage = (_idleTimeSeconds / _sessionSeconds) * 100.0;
                IdlePercentText.Text = $"{_idlePercentage:F1}%";
            }

            // Movements per second
            int eventsThisTick = _movementEvents - _lastMovementCount;
            _movementCountThisSecond = eventsThisTick;
            MpsText.Text = $"{_movementCountThisSecond}";
            _lastMovementCount = _movementEvents;

            if (_wasIdle) _idleTimeSeconds += 1.0;
            else _idleTimeSeconds = 0;

            // Rolling density
            _rollingDensity = _movementCountThisSecond;
            RollingDensityText.Text = $"{_rollingDensity:F2}";

            // Average distance per event
            if (_movementEvents > 0)
                _averageDistancePerEvent = _distancePerEventTotal / _movementEvents;

            // Idle time
            if (_movementEvents == _lastMovementEvents)
            {
                _idleTime++;
                IdleTimeText.Text = $"{_idleTime:F0} s";
            }
            else
            {
                _idleTime = 0;
                IdleTimeText.Text = "0 s";
            }
            _lastMovementEvents = _movementEvents;

            // Density
            if (_sessionSeconds > 0)
            {
                _movementDensity = _movementEvents / _sessionSeconds;
                DensityText.Text = $"{_movementDensity:F2}";
            }

            // Average velocity
            if (_sessionSeconds > 0)
            {
                _averageVelocity = _totalDistance / _sessionSeconds;
                AverageSpeedText.Text = $"{_averageVelocity:F2}";
            }
        }

        // -----------------------------
        // BUTTONS
        // -----------------------------
        private void StartButton_Click(object sender, RoutedEventArgs e)
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

            _timer.Start();
            LogService.Info($"Tracking started — DPI:{_dpi} Sens:{_sensitivity} Profile:{_selectedProfile.Name}");
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _isTracking = false;
            _timer.Stop();
            LogService.Info("Tracking stopped");
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _isTracking = false;
            _timer.Stop();

            _totalDistance = 0;
            TotalDistanceText.Text = "0";
            DxDyText.Text = "dX: 0  dY: 0";
            LastDeltaText.Text = "Last Delta: 0, 0";
            SessionTimeText.Text = "00:00";
            CmPer360Text.Text = "--";

            _peakVelocity = 0; PeakSpeedText.Text = "0";
            _averageVelocity = 0; AverageSpeedText.Text = "0";
            _currentVelocity = 0; CurrentSpeedText.Text = "0";

            _movementEvents = 0;
            _sessionSeconds = 0;

            _flickCount = 0; FlicksCountText.Text = "0";
            _smallFlicks = 0; SmallFlicksText.Text = "0";
            _largeFlicks = 0; LargeFlicksText.Text = "0";

            _jitterAmount = 0; JitterText.Text = "0";
            _movementDensity = 0; DensityText.Text = "0";

            _idleTime = 0; IdleTimeText.Text = "0 s";
            _idleBurstCount = 0; IdleBurstsText.Text = "0";

            _lastAngle = 0; AngleText.Text = "0°";
            _angleStability = 0; StabilityText.Text = "0";
            _angleChangeTotal = 0; AngleChangeText.Text = "0";

            _movementConsistency = 100; MovementConsistencyText.Text = "100";
            _smoothnessScore = 100; SmoothnessText.Text = "100";
            _correctionSharpness = 0; CorrectionSharpnessText.Text = "0";
            _overallQualityScore = 100; OverallQualityText.Text = "100";
        }

        // -----------------------------
        // NAVIGATION
        // -----------------------------
        private void NavSettings_Click(object sender, RoutedEventArgs e)
            => new SettingsWindow { Owner = this }.Show();

        private void NavHistory_Click(object sender, RoutedEventArgs e)
            => new SessionHistoryWindow { Owner = this }.Show();

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
            var summary = BuildSessionSummary();
            ExportService.ExportSummary(summary);
            MessageBox.Show("Session exported successfully.", "Export");
        }

        private void OpenConverter_Click(object sender, RoutedEventArgs e)
            => new ConverterWindow(_selectedProfile.Name, _dpi, _sensitivity).Show();

        private void OpenSessionHistory_Click(object sender, RoutedEventArgs e)
            => new SessionHistoryWindow().Show();

        private void GameProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameProfileCombo.SelectedIndex < 0) return;

            _selectedProfile = _gameProfiles[GameProfileCombo.SelectedIndex];
            LogService.Info($"Profile changed to {_selectedProfile.Name}");
        }

        private void OpenSummary_Click(object sender, RoutedEventArgs e)
        {
            _isTracking = false;
            _timer.Stop();

            var summary = BuildSessionSummary();
            SessionStorage.SaveSession(summary);

            var rec = RecommendationEngine.Analyze(summary, _selectedProfile);
            new SummaryWindow(summary, rec).Show();

        }

        private void OpenRecommendation_Click(object sender, RoutedEventArgs e)
        {
            _isTracking = false;
            _timer.Stop();

            var summary = BuildSessionSummary();
            var rec = RecommendationEngine.Analyze(summary, _selectedProfile);

            new RecommendationWindow(rec).Show();
        }

        private void OpenAddProfile_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddProfileWindow();
            win.ShowDialog();

            if (win.ProfileSaved)
                RefreshProfiles();
        }

        // -----------------------------
        // HELPERS
        // -----------------------------
        private double CalculateCmPer360()
            => (360.0 / (_dpi * _sensitivity)) * 2.54;

        private SessionSummary BuildSessionSummary()
        {
            return new SessionSummary
            {
                DPI = (int)Math.Round(_dpi),
                Sensitivity = _sensitivity,
                ProfileName = _selectedProfile.Name,
                TotalDistanceCm = _totalDistance,
                PeakVelocity = _peakVelocity,
                AverageVelocity = _averageVelocity,
                FlickCount = _flickCount,
                SmoothnessScore = _smoothnessScore,
                CorrectionSharpness = _correctionSharpness,
                MovementConsistency = _movementConsistency,
                OverallQualityScore = _overallQualityScore,
                SessionSeconds = _sessionSeconds
            };
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

    }

}


