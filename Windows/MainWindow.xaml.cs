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
        // ─────────────────────────────────────────────────────────
        //  Services
        // ─────────────────────────────────────────────────────────
        private readonly RawInputService _rawInput;

        // ─────────────────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────────────────
        public event Action? StatsUpdated;

        // ─────────────────────────────────────────────────────────
        //  Game profiles
        // ─────────────────────────────────────────────────────────
        private List<GameProfile> _gameProfiles = new();
        private GameProfile _selectedProfile;

        // ─────────────────────────────────────────────────────────
        //  Aim analytics fields
        // ─────────────────────────────────────────────────────────
        private double _totalDistance = 0;
        private double _currentVelocity = 0;
        public double _peakVelocity = 0;
        private int _lastDx;
        private int _lastDy;
        public double _averageVelocity = 0;
        private int _movementEvents = 0;
        public double _sessionSeconds = 0;
        public double _totalDistanceInches = 0;
        public int _flickCount = 0;
        private DateTime _lastFlickTime = DateTime.MinValue;
        private int _smallFlicks = 0;
        private int _largeFlicks = 0;
        private double _jitterAmount = 0;
        private double _movementDensity = 0;
        private double _idleTime = 0;
        private int _lastMovementEvents = 0;
        private double _lastAngle = 0;
        private double _angleStability = 0;
        private double _angleChangeTotal = 0;
        private double _rawAngleDelta = 0;
        private double _trueAngleDelta = 0;
        private double _previousAngle = 0;
        private double _distancePerEventTotal = 0;
        private double _averageDistancePerEvent = 0;
        private double _peakDistancePerEvent = 0;
        private double _peakVelocityChange = 0;
        private double _previousVelocity = 0;
        private double _currentAcceleration = 0;
        private double _peakAcceleration = 0;
        private double _totalAcceleration = 0;
        private double _averageAcceleration = 0;
        public double _smoothnessScore = 100;
        public double _correctionSharpness = 0;
        public double _movementConsistency = 100;
        public double _overallQualityScore = 100;
        private int _movementCountThisSecond = 0;
        private int _lastMovementCount = 0;
        private double _rollingDensity = 0;
        private int _idleBurstCount = 0;
        private bool _wasIdle = false;
        private double _idleTimeSeconds = 0;
        private double _idlePercentage = 0;
        private DateTime _lastMoveTime = DateTime.Now;

        // ─────────────────────────────────────────────────────────
        //  Session + timer
        // ─────────────────────────────────────────────────────────
        private bool _isTracking = false;
        private DateTime _sessionStart;
        private readonly DispatcherTimer _timer = new();

        // ─────────────────────────────────────────────────────────
        //  DPI + sensitivity
        // ─────────────────────────────────────────────────────────
        private double _dpi = 800;
        private double _sensitivity = 1.0;

        // ═════════════════════════════════════════════════════════
        //  Constructor
        // ═════════════════════════════════════════════════════════
        public MainWindow()
        {
            InitializeComponent();
            LogService.Info("MainWindow initialized");

            // Load saved settings
            var settings = SettingsService.Load();
            _dpi = settings.DPI;
            _sensitivity = settings.Sensitivity;
            DpiInput.Text = _dpi.ToString("F0");
            SensitivityInput.Text = _sensitivity.ToString("F4");

            // Load game profiles
            var profiles = ProfileStorage.LoadProfiles();
            _gameProfiles = GameProfile.GetAllProfiles(profiles);

            foreach (var p in _gameProfiles)
                GameProfileCombo.Items.Add(p.DisplayName);

            int savedIndex = _gameProfiles.FindIndex(p => p.Name == settings.SelectedProfile);
            GameProfileCombo.SelectedIndex = savedIndex >= 0 ? savedIndex : 0;
            _selectedProfile = _gameProfiles[GameProfileCombo.SelectedIndex];

            // Trial banner
            UpdateTrialBanner();

            // Raw input service
            _rawInput = new RawInputService();
            _rawInput.MouseMoved += OnMouseMoved;

            // Timer
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            // WPF message loop hook
            this.SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                _rawInput.Register(hwnd);

                HwndSource source = HwndSource.FromHwnd(hwnd);
                source.AddHook(WndProc);
            };
        }

        // ═════════════════════════════════════════════════════════
        //  Trial banner
        // ═════════════════════════════════════════════════════════
        private void UpdateTrialBanner()
        {
            string status = TrialService.GetStatusText();
            if (status != "Full Version")
            {
                TrialBanner.Visibility = Visibility.Visible;
                TrialBannerText.Text = status;
            }
            else
            {
                TrialBanner.Visibility = Visibility.Collapsed;
            }
        }

        // ═════════════════════════════════════════════════════════
        //  Window procedure for WM_INPUT
        // ═════════════════════════════════════════════════════════
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


        // ═════════════════════════════════════════════════════════
        //  Raw mouse movement handler
        // ═════════════════════════════════════════════════════════
        private void OnMouseMoved(int dx, int dy)
        {
            Dispatcher.Invoke(() =>
            {
                _lastDx = dx;
                _lastDy = dy;
                LastDeltaText.Text = $"Last Delta: {dx}, {dy}";

                if (!_isTracking) return;

                _movementEvents++;
                MovementText.Text = $"dX: {dx}  dY: {dy}";

                // Distance per event
                double eventDistance = Math.Sqrt(dx * dx + dy * dy);
                _distancePerEventTotal += eventDistance;
                DistancePerEventText.Text = $"Dist/Event: {_distancePerEventTotal:F0}";

                // Movement consistency
                double distanceDeviation = Math.Abs(eventDistance - _averageDistancePerEvent);
                _movementConsistency = 100 - (distanceDeviation * 10);
                _movementConsistency = Math.Clamp(_movementConsistency, 0, 100);
                MovementConsistencyText.Text = $"Movement Consistency: {_movementConsistency:F0}";

                // Overall aim quality
                _overallQualityScore = (_smoothnessScore + _correctionSharpness + _movementConsistency) / 3;
                _overallQualityScore = Math.Clamp(_overallQualityScore, 0, 100);
                OverallQualityText.Text = $"Overall Quality: {_overallQualityScore:F0}";

                // Peak distance per event
                if (eventDistance > _peakDistancePerEvent)
                {
                    _peakDistancePerEvent = eventDistance;
                    PeakDistancePerEventText.Text = $"Peak Dist/Event: {_peakDistancePerEvent:F0}";
                }

                // Movement angle
                _lastAngle = Math.Atan2(dy, dx) * (180 / Math.PI);
                AngleText.Text = $"Angle: {_lastAngle:F0} deg";

                // True angle delta
                double angleDifference = Math.Abs(_lastAngle - _previousAngle);
                _trueAngleDelta = angleDifference;
                TrueAngleDeltaText.Text = $"True Delta: {_trueAngleDelta:F2} deg";

                // Smoothness
                _smoothnessScore = 100 - (Math.Abs(_trueAngleDelta) * 2);
                _smoothnessScore = Math.Clamp(_smoothnessScore, 0, 100);
                SmoothnessText.Text = $"Smoothness: {_smoothnessScore:F0}";

                // Raw angle delta
                _rawAngleDelta = angleDifference;
                RawAngleDeltaText.Text = $"Raw Delta: {_rawAngleDelta:F2} deg";

                // Angle change total
                _angleChangeTotal += angleDifference;
                AngleChangeText.Text = $"Angle Change: {_angleChangeTotal:F2} deg";

                // Angle stability
                _angleStability = angleDifference;
                AngleStabilityText.Text = $"Stability: {_angleStability:F2} deg";

                // Jitter detection
                if (Math.Abs(dx) + Math.Abs(dy) < 3)
                {
                    _jitterAmount++;
                    JitterText.Text = $"Jitter: {_jitterAmount:F0}";
                }

                _previousAngle = _lastAngle;
                PreviousAngleText.Text = $"Prev Angle: {_previousAngle:F0} deg";

                // Convert counts to cm
                double counts = Math.Abs(dx) + Math.Abs(dy);
                double cmMoved = counts / _dpi * 2.54;
                _totalDistance += cmMoved;
                _totalDistanceInches += cmMoved / 2.54;
                DistanceText.Text = $"Total Distance: {_totalDistance:F2} cm";

                // Velocity
                DateTime now = DateTime.Now;
                double deltaTime = (now - _lastMoveTime).TotalSeconds;
                _lastMoveTime = now;

                if (deltaTime > 0)
                {
                    _currentVelocity = cmMoved / deltaTime;
                    VelocityText.Text = $"Speed: {_currentVelocity:F2} cm/s";
                    CurrentVelocityText.Text = $"Current: {_currentVelocity:F1} cm/s";

                    // Acceleration
                    _currentAcceleration = (_currentVelocity - _previousVelocity) / deltaTime;
                    AccelerationText.Text = $"Accel: {_currentAcceleration:F2} cm/s2";
                    _totalAcceleration += _currentAcceleration;

                    if (_movementEvents > 0)
                    {
                        _averageAcceleration = _totalAcceleration / _movementEvents;
                        AverageAccelerationText.Text = $"Avg Accel: {_averageAcceleration:F2} cm/s2";
                    }

                    if (_currentAcceleration > _peakAcceleration)
                    {
                        _peakAcceleration = _currentAcceleration;
                        PeakAccelerationText.Text = $"Peak Accel: {_peakAcceleration:F2} cm/s2";
                    }

                    // Idle burst detection
                    if (_wasIdle && _currentVelocity > 20)
                    {
                        _idleBurstCount++;
                        IdleBurstText.Text = $"Idle Bursts: {_idleBurstCount}";
                    }
                    _wasIdle = (_currentVelocity < 1);

                    // Peak velocity + flicks
                    if (_currentVelocity > _peakVelocity)
                    {
                        _peakVelocity = _currentVelocity;
                        PeakVelocityText.Text = $"Peak: {_peakVelocity:F2} cm/s";

                        if (_currentVelocity > 50)
                        {
                            DateTime nowInner = DateTime.Now;
                            if ((nowInner - _lastFlickTime).TotalMilliseconds > 150)
                            {
                                _flickCount++;
                                FlickCountText.Text = $"Flicks: {_flickCount}";

                                if (_currentVelocity < 100)
                                {
                                    _smallFlicks++;
                                    SmallFlicksText.Text = $"Small Flicks: {_smallFlicks}";
                                }
                                else
                                {
                                    _largeFlicks++;
                                    LargeFlicksText.Text = $"Large Flicks: {_largeFlicks}";
                                }
                                _lastFlickTime = nowInner;
                            }
                        }
                    }

                    // Velocity change
                    double velocityChange = Math.Abs(_currentVelocity - _previousVelocity);
                    _correctionSharpness = Math.Min(velocityChange * 2, 100);
                    CorrectionSharpnessText.Text = $"Correction Sharpness: {_correctionSharpness:F0}";

                    if (velocityChange > _peakVelocityChange)
                    {
                        _peakVelocityChange = velocityChange;
                        PeakVelocityChangeText.Text = $"Peak Vel Change: {_peakVelocityChange:F2}";
                    }

                    _previousVelocity = _currentVelocity;
                }

                StatsUpdated?.Invoke();
            });
        }

        // ═════════════════════════════════════════════════════════
        //  Timer tick
        // ═════════════════════════════════════════════════════════
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_isTracking) return;

            TimeSpan elapsed = DateTime.Now - _sessionStart;
            TimeText.Text = $"Session Time: {elapsed:mm\\:ss}";
            _sessionSeconds = elapsed.TotalSeconds;

            // Idle percentage
            if (_sessionSeconds > 0)
            {
                _idlePercentage = (_idleTimeSeconds / _sessionSeconds) * 100.0;
                IdlePercentageText.Text = $"Idle %: {_idlePercentage:F1}%";
            }

            // Movement count per second
            int eventsThisTick = _movementEvents - _lastMovementCount;
            _movementCountThisSecond = eventsThisTick;
            MovementCountPerSecondText.Text = $"MPS: {_movementCountThisSecond}";
            _lastMovementCount = _movementEvents;

            // Idle time since last movement
            if (_wasIdle) _idleTimeSeconds += 1.0;
            else _idleTimeSeconds = 0;
            IdleTimeSinceMoveText.Text = $"Idle Time: {_idleTimeSeconds:F1} s";

            // Rolling density
            _rollingDensity = _movementCountThisSecond;
            RollingDensityText.Text = $"Rolling Density: {_rollingDensity:F2} eps";

            // Average distance per event
            if (_movementEvents > 0)
            {
                _averageDistancePerEvent = _distancePerEventTotal / _movementEvents;
                AverageDistancePerEventText.Text = $"Avg Dist/Event: {_averageDistancePerEvent:F2}";
            }

            // Idle time
            if (_movementEvents == _lastMovementEvents)
            {
                _idleTime++;
                IdleTimeText.Text = $"Idle: {_idleTime:F0} s";
            }
            else
            {
                _idleTime = 0;
                IdleTimeText.Text = "Idle: 0 s";
            }
            _lastMovementEvents = _movementEvents;

            // Movement density
            if (_sessionSeconds > 0)
            {
                _movementDensity = _movementEvents / _sessionSeconds;
                MovementDensityText.Text = $"Density: {_movementDensity:F2} eps";
            }

            // Average velocity
            double seconds = elapsed.TotalSeconds;
            if (seconds > 0)
            {
                _averageVelocity = _totalDistance / seconds;
                AverageVelocityText.Text = $"Average: {_averageVelocity:F2} cm/s";
            }
        }

        // ═════════════════════════════════════════════════════════
        //  Start / Stop / Reset
        // ═════════════════════════════════════════════════════════
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(DpiInput.Text, out double dpi)) _dpi = dpi;
            if (double.TryParse(SensitivityInput.Text, out double sens)) _sensitivity = sens;

            _isTracking = true;
            _sessionStart = DateTime.Now;
            _totalDistance = 0;
            DistanceText.Text = "Total Distance: 0";
            MovementText.Text = "dX: 0  dY: 0";

            double cm360 = CalculateCmPer360();
            Cm360Text.Text = $"cm/360: {cm360:F2}";

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
            DistanceText.Text = "Total Distance: 0";
            MovementText.Text = "dX: 0  dY: 0";
            _lastDx = 0; _lastDy = 0;
            LastDeltaText.Text = "Last Delta: 0, 0";
            TimeText.Text = "Session Time: 00:00";
            Cm360Text.Text = "cm/360: --";
            _peakVelocity = 0; PeakVelocityText.Text = "Peak: 0 cm/s";
            _averageVelocity = 0; AverageVelocityText.Text = "Average: 0 cm/s";
            _currentVelocity = 0; CurrentVelocityText.Text = "Current: 0 cm/s";
            _movementEvents = 0; _sessionSeconds = 0; _totalDistanceInches = 0;
            _flickCount = 0; FlickCountText.Text = "Flicks: 0";
            _lastFlickTime = DateTime.MinValue;
            _smallFlicks = 0; SmallFlicksText.Text = "Small Flicks: 0";
            _largeFlicks = 0; LargeFlicksText.Text = "Large Flicks: 0";
            _jitterAmount = 0; JitterText.Text = "Jitter: 0";
            _movementDensity = 0; MovementDensityText.Text = "Density: 0 eps";
            _idleTime = 0; IdleTimeText.Text = "Idle: 0 s";
            _lastMovementEvents = 0;
            _lastAngle = 0; AngleText.Text = "Angle: 0 deg";
            _angleStability = 0; AngleStabilityText.Text = "Stability: 0";
            _angleChangeTotal = 0; AngleChangeText.Text = "Angle Change: 0";
            _rawAngleDelta = 0; RawAngleDeltaText.Text = "Raw Delta: 0";
            _trueAngleDelta = 0; TrueAngleDeltaText.Text = "True Delta: 0";
        }

        // ═════════════════════════════════════════════════════════
        //  Toolbar: Settings
        // ═════════════════════════════════════════════════════════
        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            win.Owner = this;
            win.Show();
        }

        private void NavHistory_Click(object sender, RoutedEventArgs e)
        {
            var win = new SessionHistoryWindow();
            win.Owner = this;
            win.Show();
        }



        // ═════════════════════════════════════════════════════════
        //  Toolbar: Help
        // ═════════════════════════════════════════════════════════
        private void OpenHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Help documentation coming soon.", "Help");
        }

        // ═════════════════════════════════════════════════════════
        //  Toolbar: About
        // ═════════════════════════════════════════════════════════
        private void OpenAbout_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow();
            win.ShowDialog();
        }

        // ═════════════════════════════════════════════════════════
        //  Toolbar: Export
        // ═════════════════════════════════════════════════════════
        private void OpenExport_Click(object sender, RoutedEventArgs e)
        {
            var summary = BuildSessionSummary();
            ExportService.ExportSummary(summary);
            MessageBox.Show("Session exported successfully.", "Export");
        }

        // ═════════════════════════════════════════════════════════
        //  Converter
        // ═════════════════════════════════════════════════════════
        private void OpenConverter_Click(object sender, RoutedEventArgs e)
        {
            var win = new ConverterWindow(_selectedProfile.Name, _dpi, _sensitivity);
            win.Show();
        }

        // ═════════════════════════════════════════════════════════
        //  Session History
        // ═════════════════════════════════════════════════════════
        private void OpenSessionHistory_Click(object sender, RoutedEventArgs e)
        {
            var win = new SessionHistoryWindow();
            win.Show();
        }

        // ═════════════════════════════════════════════════════════
        //  Game Profile Selection
        // ═════════════════════════════════════════════════════════
        private void GameProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameProfileCombo.SelectedIndex < 0)
                return;

            _selectedProfile = _gameProfiles[GameProfileCombo.SelectedIndex];
            LogService.Info($"Profile changed to {_selectedProfile.Name}");
        }

        // ═════════════════════════════════════════════════════════
        //  Summary
        // ═════════════════════════════════════════════════════════
        private void OpenSummary_Click(object sender, RoutedEventArgs e)
        {
            _isTracking = false;
            _timer.Stop();

            var summary = BuildSessionSummary();
            SessionStorage.SaveSession(summary);

            var win = new SummaryWindow(summary);
            win.Show();
        }

        // ═════════════════════════════════════════════════════════
        //  Recommendation
        // ═════════════════════════════════════════════════════════
        private void OpenRecommendation_Click(object sender, RoutedEventArgs e)
        {
            var summary = BuildSessionSummary();
            var rec = RecommendationEngine.Analyze(summary, _selectedProfile);

            var win = new RecommendationWindow(rec);
            win.Show();
        }

        // ═════════════════════════════════════════════════════════
        //  Add Profile
        // ═════════════════════════════════════════════════════════
        private void OpenAddProfile_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddProfileWindow();
            win.ShowDialog();

            if (win.ProfileSaved)
                RefreshProfiles();
        }

        private double CalculateCmPer360()
        {
            return (360.0 / (_dpi * _sensitivity)) * 2.54;
        }

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


