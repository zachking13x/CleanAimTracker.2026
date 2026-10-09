using CleanAimTracker.Helpers;
using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Windows;
using System.Windows.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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
        // TASK-1.1: session-aggregate smoothness — running sum/count of per-event
        // instantaneous smoothness, so the stored score is a session mean rather
        // than whatever the final two mouse events happened to be.
        private double _smoothnessSum = 0;
        private int    _smoothnessSamples = 0;
        // T0.2: _correctionSharpness was MISNAMED and last-write-wins. It measures
        // |Δspeed| between consecutive events (speed variability), NOT overshoot —
        // the tracker has no targets, so it cannot see overcorrection. Now a session
        // aggregate (mean), and the display is renamed honestly to "Speed Variability".
        private double _correctionSharpness = 0;   // session-mean speed variability (0=steady, 100=jerky)
        private double _speedVarSum = 0;
        private int    _speedVarSamples = 0;
        private double _movementConsistency = 0;
        private double _overallQualityScore = 0;

        // GATE 0: session-scoped raw buffer so the SAVED/coached smoothness +
        // consistency come from the shared MovementQualityCalculator — the same
        // implementation the drill path uses (no divergent inline copy feeding
        // coached values). The live on-screen readout stays incremental (display
        // only). BOUNDED: tracker sessions can run for hours; cap the buffer and
        // stop adding past the ceiling — ~100k ACTIVE-movement events (idle/jitter
        // never reach here) is a representative smoothness sample.
        private readonly List<RawInputSample> _trackerBuffer = new();
        private const int TrackerBufferCap = 100_000;

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

        // T0.1: consistency session aggregate (running mean of per-event consistency).
        // Was last-write-wins against a once-per-second-stale average — see fix below.
        private double _consistencySum = 0;
        private int    _consistencySamples = 0;

        // SESSION + TIMER
        private bool _isTracking = false;
        private DateTime _sessionStart;
        private readonly DispatcherTimer _timer = new();
        private double _sessionSeconds = 0;
        private DispatcherTimer? _challengeCountdownTimer;

        // DPI + SENSITIVITY
        private double _dpi = 800;
        private double _sensitivity = 1.0;

        // COACH REPORT BANNER — pending session saved on Stop, consumed by banner or Summary button
        private SessionSummary? _pendingSessionSummary;
        private SensitivityRecommendation? _pendingRec;
        private StreakService.StreakResult? _pendingStreak;

        // LAST REPORT — kept alive so the nav button can re-open any time
        private SessionSummary? _lastSummary;
        private SensitivityRecommendation? _lastRec;
        private StreakService.StreakResult? _lastStreak;

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
            CheckWhatsNew();

            // TASK-09: If user clicked "Hit Start" in onboarding, auto-start a 90s baseline session
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 5A: Start subtle background atmosphere drift
            StartAtmosphereAnimation();

            CheckWeeklyAimRecap();

            var settings = SettingsService.Load();
            if (settings.OnboardingAutoStart)
            {
                settings.OnboardingAutoStart = false;
                SettingsService.Save(settings);

                LaunchOnboardingSession();
                return;   // don't stack a privacy dialog on top of the first drill
            }

            // CAT_TELEMETRY: the one-time disclosure. Deliberately AFTER onboarding —
            // a privacy modal in front of a brand-new user's first session is the worst
            // possible first impression, and nothing is sent until this has been seen.
            TelemetryNoticeWindow.ShowIfNeeded(this);

            // CAT_SESSION_REPORT: a session whose summary never rendered — they killed
            // the app, or it crashed — is shown here instead of being lost. This is what
            // closes the "they close the whole app and never see it" hole, and it lands
            // at a strictly better moment: arriving rather than leaving.
            ShowDeferredSessionSummary();
        }

        /// <summary>
        /// CAT_SESSION_REPORT: replay a session summary that never got shown.
        /// The pending marker is a timestamp, not a copy of the results, so the drills
        /// are re-read from storage — there's no second source of truth to drift.
        /// </summary>
        private void ShowDeferredSessionSummary()
        {
            try
            {
                var settings = SettingsService.Load();
                var pending  = settings.PendingSummarySessionStartUtc;
                if (pending == DateTime.MinValue) return;

                // Clear first: a summary that somehow fails to render must not become a
                // popup that greets them on every launch forever.
                settings.PendingSummarySessionStartUtc = DateTime.MinValue;
                SettingsService.Save(settings);

                var drills = AimTrainerStorage.LoadAll()
                    .Where(r => !r.IsAssessmentSession && r.Timestamp.ToUniversalTime() >= pending)
                    .OrderBy(r => r.Timestamp)
                    .ToList();

                if (drills.Count == 0) return;

                new RoutineSummaryWindow(drills, "Last session") { Owner = this }.ShowDialog();
            }
            catch (Exception ex) { LogService.Error("Deferred session summary failed", ex); }
        }

        private void LaunchOnboardingSession()
        {
            var trainer = new AimTrainerWindow { Owner = this };
            trainer.OnboardingSessionCompleted += () =>
            {
                Dispatcher.BeginInvoke(() => ShowPostOnboardingCard());
            };
            trainer.Show();
            trainer.BeginOnboardingSession();
        }

        private void ShowPostOnboardingCard()
        {
            var s = SettingsService.Load();
            s.FirstLaunchComplete = true;
            SettingsService.Save(s);

            PostOnboardingCard.Visibility = Visibility.Visible;
            PostOnboardingCard.BringIntoView();
        }

        private void PostOnboardingDrill_Click(object sender, RoutedEventArgs e)
        {
            PostOnboardingCard.Visibility = Visibility.Collapsed;
            new AimTrainerWindow { Owner = this }.Show();
        }

        // TRIAL BANNER
        public void UpdateTrialBanner()
        {
            // CAT_PAID_APP: no "License check failed" warning any more. Nothing is gated on
            // the add-on refresh now, so warning offline buyers about it would just scare them.
            TrialBannerText.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
            string banner = TrialService.GetBannerText();
            TrialBannerText.Text = banner;

            // Hide the entire container (including the hardcoded "Tap to upgrade →" line)
            // when the user is Pro or has no sessions yet — not just the text element.
            bool showBanner = !string.IsNullOrEmpty(banner);
            TrialBannerContainer.Visibility = showBanner ? Visibility.Visible : Visibility.Collapsed;
            TrialBannerText.Visibility      = showBanner ? Visibility.Visible : Visibility.Collapsed;

            UpdateProNavVisibility();
        }

        /// <summary>
        /// Called after a successful purchase or restore to collapse all free-tier
        /// upsell cards and fully reload the player panel with the unlocked Pro state.
        /// </summary>
        public void RefreshAfterPurchase()
        {
            ValueMomentCard.Visibility = Visibility.Collapsed;
            FreeLimitCard.Visibility   = Visibility.Collapsed;
            LoadTodayStats(animatePanel: false);
        }

        // ─────────────────────────────────────────────────────────────
        // RAW INPUT MOVEMENT HANDLER
        // ─────────────────────────────────────────────────────────────
        private const int MAX_DELTA = 50;
        private const int MIN_DELTA = -50;
        private const int JITTER_THRESHOLD = 2;

        private void OnRawMouseMove(int dx, int dy, long timestamp)
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
                // GATE 0: buffer the (clamped, jitter-filtered) event for the
                // batch movement-quality computation at session end. Bounded.
                if (_trackerBuffer.Count < TrackerBufferCap)
                    _trackerBuffer.Add(new RawInputSample(dx, dy, timestamp));
                DxDyText.Text = $"dX: {dx}  dY: {dy}";

                double eventDistance = Math.Sqrt(dx * dx + dy * dy);
                _distancePerEventTotal += eventDistance;

                // T0.1 FIX — two stacked bugs, same class as the smoothness fix above:
                //  (1) STALE AVERAGE: deviation was measured against _averageDistancePerEvent,
                //      which only updated once per second in Timer_Tick. For the whole first
                //      second the average was 0, so deviation == eventDistance and consistency
                //      floored to 0 on every event. After that a per-event value raced an
                //      average lagging hundreds of events behind.
                //  (2) LAST-WRITE-WINS: _movementConsistency was overwritten each event, so the
                //      SAVED value was just the final event's deviation, not a session statistic.
                // Fix: _distancePerEventTotal already includes this event (line above) and
                // _movementEvents already counts it, so total/count IS the current running mean —
                // no stale lag. Then aggregate per-event consistency into a session mean.
                // Worked example, event distances [5, 15, 10]:
                //   n=1: mean=5,  dev=0,  inst=100
                //   n=2: mean=10, dev=5,  inst=clamp(100-50)=50
                //   n=3: mean=10, dev=0,  inst=100   → session mean (100+50+100)/3 = 83.3
                // Constant-velocity buffer [10,10,10] → every dev=0 → session mean = 100.
                _averageDistancePerEvent = _distancePerEventTotal / _movementEvents; // running mean, current
                double deviation = Math.Abs(eventDistance - _averageDistancePerEvent);
                double instantConsistency = Math.Clamp(100 - deviation * 10, 0, 100);
                _consistencySum += instantConsistency;
                _consistencySamples++;
                _movementConsistency = _consistencySum / _consistencySamples; // _consistencySamples >= 1 here

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

                // TASK-1.1 FIX 1 — angular wrap-around normalization.
                // Atan2 returns -180..+180. Without normalization, smooth LEFTWARD motion
                // with ±1 count of y-noise alternates ~+179° / ~-179°:
                //   raw diff = |174.29 - (-174.29)| = 348.57 → 100 - 697 → clamped to 0
                // even though the true angular change is 360 - 348.57 = 11.43°.
                // Worked example after fix: events (-10,+1) then (-10,-1):
                //   angles 174.29°, -174.29° → raw 348.57 → normalized 11.43
                //   instant = clamp(100 - 11.43*2) = 77.1  (was 0 before fix)
                if (angleDiff > 180)
                    angleDiff = 360 - angleDiff;

                // TASK-1.1 FIX 2 — aggregate over the session instead of last-write-wins.
                // Previously _smoothnessScore was overwritten per event, so the SAVED value
                // was just the final two deltas of the session (≈ random, often exactly 0).
                // Worked example: events (10,0),(10,1),(10,0) → angles 0°, 5.71°, 0°
                //   diffs 5.71, 5.71 → instants 88.6, 88.6 → session mean 88.6
                double instantSmoothness = Math.Clamp(100 - angleDiff * 2, 0, 100);
                _smoothnessSum += instantSmoothness;
                _smoothnessSamples++;
                _smoothnessScore = _smoothnessSum / _smoothnessSamples; // _smoothnessSamples >= 1 here
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

                    // T0.2: speed variability = |Δspeed| between events, ×2, capped.
                    // Re-scoped from last-write-wins to a session mean so the value is
                    // a real statistic. (Was: the final event's spike, ≈ noise.)
                    double velocityChange = Math.Abs(_currentVelocity - _previousVelocity);
                    double instantSpeedVar = Math.Min(velocityChange * 2, 100);
                    _speedVarSum += instantSpeedVar;
                    _speedVarSamples++;
                    _correctionSharpness = _speedVarSum / _speedVarSamples; // session mean; _speedVarSamples >= 1
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

            int eventsThisTick = _movementEvents - _lastMovementCount;
            _movementCountThisSecond = eventsThisTick;
            MpsText.Text = $"{_movementCountThisSecond}";
            _lastMovementCount = _movementEvents;

            // Idle accounting (fixed): _idleTimeSeconds is the session TOTAL.
            // The old code reset it to 0 on any movement (it measured the current
            // idle streak, not the total), and _wasIdle only updates when raw-input
            // events arrive — an untouched mouse generates no events, so true idle
            // never counted. A tick with zero raw-input events IS an idle second.
            // Hand-check: 600s session, mouse untouched for 300s → 300 zero-event
            // ticks → IdlePercentage = 300/600 = 50%.
            if (eventsThisTick == 0 || _wasIdle)
                _idleTimeSeconds += 1.0;

            _rollingDensity = _movementCountThisSecond;
            RollingDensityText.Text = $"{_rollingDensity:F2}";

            // T0.1: _averageDistancePerEvent is now maintained per-event as a running mean
            // in the raw-input handler — the once-per-second update here was the stale source
            // that floored consistency, and has been removed.

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
            // Dismiss any pending coach report banner from the previous session
            CoachReportBanner.Visibility = Visibility.Collapsed;
            _pendingSessionSummary = null;
            _pendingRec            = null;
            _pendingStreak         = null;

            // Force a display sync first so any pending LostFocus state is flushed,
            // then read the text boxes as the authoritative source for this session.
            UpdateSensitivityDisplay();
            if (double.TryParse(DpiInput.Text, out double dpi)) _dpi = dpi;
            if (double.TryParse(SensitivityInput.Text, out double sens)) _sensitivity = sens;
            LogService.Info($"Session start — DPI:{_dpi} Sens:{_sensitivity}");

            // TASK-0.1: EVERY per-session buffer resets at session start. Only
            // _totalDistance was reset here before — smoothness sums, flick
            // counts, peak velocity, idle accounting all accumulated across
            // sessions within one app run, so a 22s near-zero-movement session
            // rendered the PREVIOUS session's diagnostics and scored a PB on them.
            ResetSessionMetrics();

            _isTracking = true;
            _sessionStart = DateTime.Now;

            TotalDistanceText.Text = "0";
            DxDyText.Text = "dX: 0  dY: 0";

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

            // Build ONCE per stop — the double build logged SMOOTHNESS-DIAG twice
            // and did duplicate work for the same numbers.
            if (_sessionSeconds >= 10)
            {
                var summary = BuildSessionSummary();

                // TASK-11: Populate plain-English verdicts if session was long enough
                PopulateTrackerInterpretations(summary);

                // Always save sessions >= 10s; show banner when >= 45s
                TrySaveAndShowCoachBanner(summary);
            }
        }

        private void TrySaveAndShowCoachBanner(SessionSummary summary)
        {
            // ── Step 1: Save the session — always, for any session >= 10s ──────
            try
            {
                _pendingSessionSummary = summary;
                SessionStorage.Save(_pendingSessionSummary);
                _pendingStreak = StreakService.UpdateStreak();
                LoadTodayStats();
                UpdateTrialBanner();
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to save session on Stop", ex);
                _pendingSessionSummary = null;
                _pendingStreak         = null;
                return;   // nothing saved — nothing else to do
            }

            // ── Step 2: Compute recommendation and show banner ─────────────────
            // A failure here must NOT lose the already-saved session data.
            try
            {
                _pendingRec = RecommendationEngine.Analyze(_pendingSessionSummary, _selectedProfile);
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to compute sensitivity recommendation", ex);
                // _pendingSessionSummary is intentionally kept alive so the
                // Summary button can still open SummaryWindow without a rec.
                _pendingRec = null;
            }

            // Show the banner regardless of whether rec succeeded.
            // If rec is null the click handler will try again on demand.
            CoachReportBanner.Visibility = Visibility.Visible;
        }

        private void CoachReportBannerBtn_Click(object sender, RoutedEventArgs e)
        {
            CoachReportBanner.Visibility = Visibility.Collapsed;
            if (_pendingSessionSummary == null) return;

            var summary = _pendingSessionSummary;
            var streak  = _pendingStreak;
            var rec     = _pendingRec;
            _pendingSessionSummary = null;
            _pendingRec            = null;
            _pendingStreak         = null;

            // If rec wasn't computed on Stop (e.g. profile issue), try once more now.
            if (rec == null && _selectedProfile != null)
            {
                try { rec = RecommendationEngine.Analyze(summary, _selectedProfile); }
                catch (Exception ex) { LogService.Error("On-demand rec failed", ex); }
            }

            if (rec != null)
            {
                _lastSummary = summary;
                _lastRec     = rec;
                _lastStreak  = streak;
                UpdateLastReportButton();
                new SummaryWindow(summary, rec, streak) { Owner = this }.Show();
            }
            else
                MessageBox.Show(
                    "Session saved! Select a game profile and click Recommend to see your sensitivity analysis.",
                    "Session Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
        }

        private void PopulateTrackerInterpretations(SessionSummary s)
        {
            // Movement / cm/360 verdict
            string movementVerdict;
            if (s.CmPer360 < 20)
                movementVerdict = $"Your sensitivity is very high — small movements cause large cursor jumps. " +
                                  $"Consider lowering it to bring cm/360 above 20 (currently {s.CmPer360:F1} cm/360).";
            else if (s.CmPer360 > 60)
                movementVerdict = $"Your sensitivity is very low — large mouse movements needed to turn. " +
                                  $"This can limit reaction speed (currently {s.CmPer360:F1} cm/360).";
            else
                movementVerdict = $"Your sensitivity is in a good range — {s.CmPer360:F1} cm/360. " +
                                  $"Consistent with smooth aim control.";

            // Velocity consistency verdict
            string velocityVerdict = s.AverageVelocity > 0 && s.PeakVelocity > 0
                ? (s.AverageVelocity / s.PeakVelocity > 0.6
                    ? "Consistent speed — good movement control."
                    : "High speed variance — movement is inconsistent.")
                : "Not enough movement data.";

            // Overall quality verdict
            // TASK-0.1: "Check grip and surface" causal suffix removed pending smoothness validation.
            string qualityVerdict = s.OverallQualityScore >= 75
                ? $"Strong session — quality {s.OverallQualityScore:F0}/100."
                : s.OverallQualityScore >= 50
                ? $"Average session — quality {s.OverallQualityScore:F0}/100. Room to improve."
                : $"Tough session — quality {s.OverallQualityScore:F0}/100.";

            // Smoothness verdict
            // DISABLED pending TASK-1.1 — do not re-enable without validity gate
            // Smoothness-derived narration with grip/sensitivity causal claims on an unvalidated metric.
            // string smoothnessVerdict = s.SmoothnessScore >= 80
            //     ? "Smooth movement — good mouse control."
            //     : s.SmoothnessScore >= 60
            //     ? "Some jitter detected — check sensitivity and grip pressure."
            //     : "High jitter — sensitivity may be too high or grip too tense.";
            string smoothnessVerdict = "—";

            MovementVerdictText.Text   = movementVerdict;
            VelocityVerdictText.Text   = velocityVerdict;
            QualityVerdictText.Text    = qualityVerdict;
            SmoothnessVerdictText.Text = smoothnessVerdict;
        }

        // ── Assessment Prompt Card ──────────────────────────────────────────

        private void AssessmentPromptCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Allow clicking anywhere on the card to open the assessment
            OpenDiagnosticAssessment();
        }

        private void AssessmentPromptBegin_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // prevent card MouseDown from also firing
            OpenDiagnosticAssessment();
        }

        private void AssessmentPromptDismiss_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            AssessmentPromptCard.Visibility = Visibility.Collapsed;

            var settings = SettingsService.Load();
            settings.DismissedAssessmentPrompt = true;
            SettingsService.Save(settings);
        }

        private void OpenDiagnosticAssessment()
        {
            var win = new DiagnosticAssessmentWindow { Owner = this };
            win.ShowDialog();
            // Refresh the dashboard after assessment completes
            LoadTodayStats();
        }

        public void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // Dismiss any pending coach report banner
            CoachReportBanner.Visibility = Visibility.Collapsed;
            _pendingSessionSummary = null;
            _pendingRec            = null;
            _pendingStreak         = null;

            _isTracking = false;
            _timer.Stop();
            RecordingPanel.Visibility = Visibility.Collapsed;

            ResetSessionMetrics();

            LastDeltaText.Text = "Last Delta: 0, 0";
            DxDyText.Text = "dX: 0  dY: 0";
            ResetDisplaysToIdle();

            // Clear verdict lines so they don't carry over to the next session
            MovementVerdictText.Text   = "";
            VelocityVerdictText.Text   = "";
            QualityVerdictText.Text    = "";
            SmoothnessVerdictText.Text = "";
        }

        /// <summary>
        /// TASK-0.1: the single complete per-session reset — called at session
        /// START (StartButton_Click) and on manual reset. Every field that feeds
        /// BuildSessionSummary lives here; a field missing from this list is a
        /// cross-session data leak.
        /// </summary>
        private void ResetSessionMetrics()
        {
            _totalDistance   = 0;
            _peakVelocity    = 0;
            _averageVelocity = 0;
            _currentVelocity = 0;
            _previousVelocity = 0;

            _movementEvents          = 0;
            _lastMovementCount       = 0;
            _movementCountThisSecond = 0;
            _sessionSeconds          = 0;

            _flickCount  = 0;
            _smallFlicks = 0;
            _largeFlicks = 0;

            _jitterAmount    = 0;
            _movementDensity = 0;

            _idleTime        = 0;
            _idleTimeSeconds = 0;   // the accumulator BuildSessionSummary reads — was never reset anywhere
            _idleBurstCount  = 0;
            _wasIdle         = false;

            _lastAngle        = 0;
            _previousAngle    = 0;  // first event of a new session must not compare against the previous session's last angle
            _angleStability   = 0;
            _angleChangeTotal = 0;

            _movementConsistency = 0;
            _consistencySum      = 0;   // T0.1
            _consistencySamples  = 0;   // T0.1
            _trackerBuffer.Clear();     // GATE 0
            _distancePerEventTotal   = 0;
            _averageDistancePerEvent = 0;
            _peakDistancePerEvent    = 0;
            _smoothnessScore     = 0;
            _smoothnessSum       = 0;
            _smoothnessSamples   = 0;
            _correctionSharpness = 0;
            _speedVarSum         = 0;   // T0.2
            _speedVarSamples     = 0;   // T0.2
            _overallQualityScore = 0;
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

            // Persist so the same profile is selected on next launch
            var settings = SettingsService.Load();
            settings.SelectedProfile = _selectedProfile.Name;
            SettingsService.Save(settings);
        }

        private void OpenSummary_Click(object sender, RoutedEventArgs e)
        {
            // If the session was already saved via the Stop → banner path, use it directly
            // to avoid double-saving the same session.
            if (_pendingSessionSummary != null && _pendingRec != null)
            {
                CoachReportBanner.Visibility = Visibility.Collapsed;
                var pendingSummary = _pendingSessionSummary;
                var pendingRec     = _pendingRec;
                var pendingStreak  = _pendingStreak;
                _pendingSessionSummary = null;
                _pendingRec            = null;
                _pendingStreak         = null;
                _lastSummary = pendingSummary;
                _lastRec     = pendingRec;
                _lastStreak  = pendingStreak;
                UpdateLastReportButton();
                new SummaryWindow(pendingSummary, pendingRec, pendingStreak) { Owner = this }.Show();
                MaybeShowValueMoment();
                return;
            }

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
                SessionStorage.Save(summary);
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
            _lastSummary = summary;
            _lastRec     = rec;
            _lastStreak  = streakResult;
            UpdateLastReportButton();
            new SummaryWindow(summary, rec, streakResult) { Owner = this }.Show();

            MaybeShowValueMoment();
        }

        private void MaybeShowValueMoment()
        {
            if (TrialService.IsFullVersion()) return;

            // CAT_SESSION_REPORT: the reverse trial running out is what locks the coach,
            // so that — not the dead 30-session counter — is what puts the card up. Once
            // it appears it stays: it's the permanent home of the upgrade ask now that
            // the locked report no longer carries it.
            try
            {
                var memory = CoachMemoryBuilder.Build(null, SettingsService.Load());
                if (memory.RealDrillCount > FreeCoachSessionService.FreeCoachedDrills)
                {
                    ShowFreeLimitCard();
                    return;
                }
            }
            catch { /* fall through to the legacy gate */ }

            if (TrialService.IsAtFreeLimit()) { ShowFreeLimitCard(); return; }

            int count = TrialService.SessionsCompleted();
            if (!TrialService.IsValueMoment(count)) return;

            ValueMomentBodyText.Text = TrialService.GetValueMomentMessage(count);
            ValueMomentCard.Visibility = Visibility.Visible;
            ValueMomentCard.BringIntoView();
        }

        private void ShowFreeLimitCard()
        {
            try
            {
                // Personal conversion moment: show the REAL improvement the coach helped
                // produce (first sessions → recent sessions), not a generic stat. This is
                // the "here's how much you improved — keep the coach" moment.
                var drills = AimTrainerStorage.LoadAll()
                                              .Where(r => !r.IsAssessmentSession)
                                              .OrderBy(r => r.Timestamp)
                                              .ToList();
                int count = drills.Count;

                string line = "";
                if (count >= 6)
                {
                    int take = Math.Max(3, count / 5);   // first-fifth vs last-fifth, min 3
                    double early = drills.Take(take).Average(r => r.Accuracy);
                    double late  = drills.Skip(count - take).Average(r => r.Accuracy);
                    double delta = late - early;
                    if (delta >= 3)
                        line = $"Your accuracy climbed {early:F0}% → {late:F0}% across these sessions.";
                    else
                        line = $"Best accuracy: {drills.Max(r => r.Accuracy):F0}%  ·  {SettingsService.Load().CurrentStreak} day streak";
                }
                else if (count > 0)
                {
                    line = $"Best accuracy: {drills.Max(r => r.Accuracy):F0}%  ·  {SettingsService.Load().CurrentStreak} day streak";
                }
                FreeLimitStatsText.Text = line;

                FreeLimitBodyText.Text =
                    $"Your first {FreeCoachSessionService.FreeCoachedDrills} drills came fully coached. " +
                    "Unlock it and the coach reads every session from here — what to fix, and whether the fix worked.";
                FreeLimitPriceText.Text = $"{Pricing.Lifetime} once";
            }
            catch
            {
                FreeLimitStatsText.Text = "";
            }

            FreeLimitCard.Visibility = Visibility.Visible;

            // CAT_SESSION_REPORT: deliberately NO BringIntoView. Scrolling the dashboard
            // to the paywall is the shouty behaviour we're moving away from — the card
            // works by being permanently present, not by grabbing the viewport.
            TrackPaywallImpressionOnce();
        }

        // One impression per app launch, not per dashboard refresh — otherwise a card
        // that sits there forever would out-count every other surface and make the
        // Sep 9 comparison meaningless.
        private static bool _paywallImpressionSent;
        private static void TrackPaywallImpressionOnce()
        {
            if (_paywallImpressionSent) return;
            _paywallImpressionSent = true;
            try { TelemetryService.TrackPaywallShown("dashboard_locked_card"); } catch { }
        }

        // CAT_REVERSE_TRIAL: permanent "what do I get?" entry point. Only free users see
        // it — a paying customer being sold to is a bug, not a nudge.
        private void UpdateProNavVisibility()
        {
            try
            {
                NavGoProBtn.Visibility = TrialService.IsFullVersion()
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            catch { /* never block the shell */ }
        }

        private void NavGoPro_Click(object sender, RoutedEventArgs e)
        {
            new UpgradeWindow("nav_go_pro") { Owner = this }.ShowDialog();
            UpdateTrialBanner();
            UpdateProNavVisibility();
        }

        private void ValueMomentUpgrade_Click(object sender, RoutedEventArgs e)
        {
            ValueMomentCard.Visibility = Visibility.Collapsed;
            new UpgradeWindow("value_moment_card") { Owner = this }.ShowDialog();
            UpdateTrialBanner();
        }

        private void ValueMomentDismiss_Click(object sender, RoutedEventArgs e)
            => ValueMomentCard.Visibility = Visibility.Collapsed;

        private void FreeLimitUpgrade_Click(object sender, RoutedEventArgs e)
        {
            // CAT_SESSION_REPORT: no dismiss. This used to collapse the card on click,
            // so opening the upgrade window and NOT buying hid the ask until the next
            // dashboard refresh — a dismiss button by accident. If they do buy,
            // MaybeShowValueMoment's IsFullVersion check retires it on the next load.
            new UpgradeWindow("free_limit_card") { Owner = this }.ShowDialog();
            UpdateTrialBanner();
            LoadTodayStats();
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

            // Always override with current live values — the saved session may be from
            // a different DPI or profile. _sensitivity IS the in-game sensitivity because
            // that is what the user types into the input box.
            summary.DPI             = (int)Math.Round(_dpi);
            summary.Sensitivity     = _sensitivity;
            summary.GameSensitivity = _sensitivity; // user input IS the game sensitivity
            summary.CmPer360        = CalculateCmPer360(); // recalculate with current profile

            var rec = RecommendationEngine.Analyze(summary, _selectedProfile);
            new RecommendationWindow(rec) { Owner = this }.ShowDialog();
        }

        private void OpenAddProfile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Custom game profiles are coming soon.",
                "Coming Soon",
                MessageBoxButton.OK,
                MessageBoxImage.None);
        }

        private void TrialBanner_Click(object sender, RoutedEventArgs e)
        {
            if (TrialService.IsAtFreeLimit())
            {
                ShowFreeLimitCard();
                FreeLimitCard.BringIntoView();
            }
            else
            {
                UpgradeDialog.Show("Pro Features");
            }
        }

        private void OpenAimTrainer_Click(object sender, RoutedEventArgs e)
        {
            // TASK-14: Aim Trainer is free — it's a core value driver
            var trainer = new AimTrainerWindow { Owner = this };
            trainer.Closed += (s, args) => Dispatcher.Invoke(() =>
            {
                LoadTodayStats(animatePanel: true);
                NavAimTrainerReportBtn.Content = "🤖  Aim Coach Report · just now";
            });
            trainer.Show();
        }

        // ── CAT_ROUTINE ──────────────────────────────────────────────────────
        // Deliberately a PEER of "pick your own", not a replacement for it. Players who
        // already know what they want to train are not served by being funnelled into a
        // plan, and taking that choice away would be a regression for existing users.
        // The dashboard presents both at equal weight; this just launches one of them.
        private void StartRoutine_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var routine = RoutineService.Build(AimTrainerStorage.LoadAll(), SettingsService.Load());
                if (routine.Steps.Count == 0) { OpenAimTrainer_Click(sender, e); return; }

                var trainer = new AimTrainerWindow { Owner = this };
                trainer.Closed += (s, args) => Dispatcher.Invoke(() =>
                {
                    LoadTodayStats(animatePanel: true);
                    NavAimTrainerReportBtn.Content = "🤖  Aim Coach Report · just now";
                });
                trainer.BeginRoutine(routine);
                trainer.Show();
            }
            catch (Exception ex)
            {
                LogService.Error("Start routine failed", ex);
                OpenAimTrainer_Click(sender, e);   // never strand the user
            }
        }

        /// <summary>Refresh the routine card — recomputed on every dashboard load.</summary>
        private void LoadRoutineCard()
        {
            try
            {
                var routine = RoutineService.Build(AimTrainerStorage.LoadAll(), SettingsService.Load());

                RoutineTitleText.Text = routine.Headline;
                RoutineMetaText.Text  = $"{routine.Steps.Count} drills · about {Math.Max(1, routine.EstimatedSeconds / 60)} min";
                RoutineWhyText.Text   = routine.Subtitle;

                RoutineStepList.Children.Clear();
                foreach (var step in routine.Steps)
                {
                    RoutineStepList.Children.Add(new TextBlock
                    {
                        Text = $"·  {step.Scenario} — {step.Difficulty}",
                        FontSize = 11,
                        Foreground = (System.Windows.Media.Brush)FindResource("SecondaryText"),
                        Margin = new Thickness(0, 2, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    });
                }

                RoutineCard.Visibility = Visibility.Visible;
            }
            catch
            {
                RoutineCard.Visibility = Visibility.Collapsed;
            }
        }

        private void ViewLastAimCoachReport_Click(object sender, RoutedEventArgs e)
        {
            AimTrainerResultWindow.OpenLastReport(this);
        }

        private void ViewLastCoachReport_Click(object sender, RoutedEventArgs e)
        {
            // If we already have the last report in memory, just re-open it.
            if (_lastSummary != null && _lastRec != null)
            {
                new SummaryWindow(_lastSummary, _lastRec, _lastStreak) { Owner = this }.Show();
                return;
            }

            // Fall back: load the most recent saved session from disk.
            try
            {
                var sessions = SessionStorage.LoadAll();
                if (sessions == null || sessions.Count == 0)
                {
                    MessageBox.Show(
                        "No sessions recorded yet. Complete a tracking session first.",
                        "No Sessions",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var last = sessions.OrderByDescending(s => s.Timestamp).First();

                SensitivityRecommendation? rec = null;
                if (_selectedProfile != null)
                {
                    try { rec = RecommendationEngine.Analyze(last, _selectedProfile); }
                    catch (Exception ex) { LogService.Error("Rec failed in ViewLastReport", ex); }
                }

                if (rec == null)
                {
                    MessageBox.Show(
                        "Select a game profile from the dropdown first, then try again.",
                        "Profile Needed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                _lastSummary = last;
                _lastRec     = rec;
                _lastStreak  = null;
                new SummaryWindow(last, rec, null) { Owner = this }.Show();
            }
            catch (Exception ex)
            {
                LogService.Error("ViewLastCoachReport_Click failed", ex);
            }
        }

        /// <summary>
        /// Updates the "Last Report" nav button label to show a timestamp hint
        /// once a report is available, so users know they can re-open it.
        /// </summary>
        private void UpdateLastReportButton()
        {
            if (_lastSummary == null) return;
            var elapsed = DateTime.Now - _lastSummary.Timestamp;
            string hint = elapsed.TotalMinutes < 2  ? "just now"
                        : elapsed.TotalMinutes < 60 ? $"{(int)elapsed.TotalMinutes}m ago"
                        : elapsed.TotalHours   < 24 ? $"{(int)elapsed.TotalHours}h ago"
                        : _lastSummary.Timestamp.ToString("MMM d");
            NavLastReportBtn.Content = $"📋  Last Report · {hint}";
        }

        // ── DPI / SENSITIVITY INPUT ───────────────────────────────────

        private void DpiInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(DpiInput.Text, out double dpi) || dpi <= 0)
                return;

            // Warn for unusual values — but still save, some hardware is outside typical range
            if (dpi < 100 || dpi > 32000)
            {
                MessageBox.Show(
                    "DPI is typically between 400 and 3200. Check your mouse settings.",
                    "Unusual DPI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            _dpi = dpi;
            var settings = SettingsService.Load();
            settings.DPI = (int)dpi;
            SettingsService.Save(settings);
            LogService.Info($"DPI saved: {settings.DPI}");
            UpdateSensitivityDisplay();
        }

        private void SensitivityInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(SensitivityInput.Text, out double sens))
                return;

            if (sens > 200.0)
            {
                // Almost certainly a data-entry error — DPI entered in the sensitivity field
                MessageBox.Show(
                    "That sensitivity looks unusually high — did you mean to enter your DPI here instead?\n\n" +
                    "In-game sensitivities are typically between 0.1 and 20. " +
                    "Check your game settings and try again.",
                    "Check your sensitivity",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return; // do not save
            }

            if (sens <= 0)
            {
                MessageBox.Show(
                    "Sensitivity must be greater than 0.",
                    "Invalid sensitivity",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _sensitivity = sens;
            var settings = SettingsService.Load();
            settings.Sensitivity = sens;
            SettingsService.Save(settings);
            LogService.Info($"Sensitivity saved: {settings.Sensitivity}");
            UpdateSensitivityDisplay();
        }

        // HELPERS
        private double CalculateCmPer360()
        {
            if (_selectedProfile != null && _selectedProfile.YawPerCount > 0 && _dpi > 0 && _sensitivity > 0)
            {
                // Game-specific formula — uses yaw to convert game sensitivity to cm/360
                return (360.0 / (_sensitivity * _dpi * _selectedProfile.YawPerCount)) * 2.54;
            }
            else
            {
                // Fallback when no profile is selected — treat sensitivity as raw multiplier
                return _dpi > 0 && _sensitivity > 0
                    ? (360.0 / (_dpi * _sensitivity)) * 2.54
                    : 30.0; // safe default
            }
        }

        /// <summary>
        /// Refreshes the cm/360 and game-sensitivity display from the current
        /// _dpi / _sensitivity / _selectedProfile values.
        /// Called on LostFocus for both input fields and at the start of a session.
        /// </summary>
        private void UpdateSensitivityDisplay()
        {
            double cm360 = CalculateCmPer360();

            // Show game sensitivity as primary display when a profile is loaded
            if (_selectedProfile != null && _selectedProfile.YawPerCount > 0 && _dpi > 0)
            {
                double gameSens = 914.4 / (cm360 * _dpi * _selectedProfile.YawPerCount);
                if (!double.IsNaN(gameSens) && !double.IsInfinity(gameSens) && gameSens > 0)
                {
                    SensDisplayText.Text  = $"{gameSens:F4}";
                    SensDisplayLabel.Text = $"{_selectedProfile.Name} Sensitivity";
                }
                else
                {
                    SensDisplayText.Text  = "--";
                    SensDisplayLabel.Text = "Sensitivity";
                }
            }
            else
            {
                // No profile / yaw — fall back to cm/360 as primary
                SensDisplayText.Text  = $"{cm360:F2}";
                SensDisplayLabel.Text = "Sensitivity";
            }

            // Always show cm/360 as secondary context
            CmPer360Text.Text = cm360 > 0 ? $"{cm360:F1} cm/360" : "--";
        }

        private SessionSummary BuildSessionSummary()
        {
            double cm360 = CalculateCmPer360(); // now uses yaw when a profile is loaded

            // _sensitivity is always the user's in-game sensitivity value
            // (e.g. 11.1 for Fortnite) — store it directly, no conversion needed.
            double gameSens = _sensitivity;

            // GATE 0: SAVED smoothness + consistency come from the shared
            // MovementQualityCalculator (one implementation across both coaches).
            // The inline incremental values still drive the live on-screen readout.
            var mq = MovementQualityCalculator.FromBuffer(_trackerBuffer);
            double savedSmoothness  = mq.Smoothness;
            double savedConsistency = mq.Consistency;
            // Regression record: batch (saved) vs inline (display). They differ only
            // by the inline's one spurious first-event-vs-0 sample — see calculator
            // comment — so they agree within ~100/N. Logged for the acceptance check.
            LogService.Info(
                $"MQ-REGRESSION smoothness inline={(_smoothnessSamples > 0 ? _smoothnessSum / _smoothnessSamples : 0):F4} " +
                $"batch={savedSmoothness:F4} | consistency inline={_movementConsistency:F4} batch={savedConsistency:F4} " +
                $"| bufferSamples={_trackerBuffer.Count}");

            // TASK-1.2: per-metric validity. Sample count is the same source the
            // batch calculator saw (every active event is buffered).
            var smoothValidity     = MetricValidation.ForSmoothness(_smoothnessSamples, _smoothnessSum);
            var consistencyValidity = MetricValidation.ForMovementMetric(_movementEvents, savedConsistency);
            var correctionValidity  = MetricValidation.ForMovementMetric(_movementEvents, _correctionSharpness);
            var qualityValidity     = MetricValidation.ForOverallQuality(
                smoothValidity, consistencyValidity, correctionValidity);

            var summary = new SessionSummary
            {
                MetricValidities = new Dictionary<string, MetricValidity>
                {
                    ["SmoothnessScore"]     = smoothValidity,
                    ["MovementConsistency"] = consistencyValidity,
                    ["CorrectionSharpness"] = correctionValidity,
                    ["OverallQualityScore"] = qualityValidity
                },
                DPI             = (int)Math.Round(_dpi),
                Sensitivity     = _sensitivity,
                GameSensitivity = gameSens,
                ProfileName     = _selectedProfile?.Name ?? "Unknown",
                TotalDistanceCm = _totalDistance,
                PeakVelocity    = _peakVelocity,
                AverageVelocity = _averageVelocity,
                FlickCount      = _flickCount,
                SmallFlickCount = _smallFlicks,
                LargeFlickCount = _largeFlicks,
                JitterAmount    = _jitterAmount,
                IdleBurstCount  = _idleBurstCount,
                // GATE 0: saved values from the shared calculator (not the inline copy).
                SmoothnessScore = savedSmoothness,
                CorrectionSharpness = _correctionSharpness,
                MovementConsistency = savedConsistency,
                MovementMetricVersion = MovementConsistencyCalculator.FormulaVersion,
                VelocityStability   = mq.VelocityStability,   // GATE 2 (-1 if too few active samples)
                OverallQualityScore = _overallQualityScore,
                SessionSeconds  = _sessionSeconds,
                Timestamp       = DateTime.Now,
                CmPer360        = cm360,
                IdlePercentage  = _sessionSeconds > 0
                    ? (_idleTimeSeconds / _sessionSeconds) * 100.0
                    : 0,
                TotalSamples    = _movementEvents
            };

            // TASK-0.2: low-activity sessions score nothing — invalidate the
            // movement metrics so Quality/PB/Progress/trends all skip them.
            MetricValidation.ApplyLowActivityGate(summary);
            return summary;
        }

        // TASK-1.1: logs smoothness internals per session and returns the final score.
        // Logged: sample count entering smoothness, intermediate sum, pre-rounding mean.
        private double LogSmoothnessDiagnostics()
        {
            double preRounding = _smoothnessSamples > 0
                ? _smoothnessSum / _smoothnessSamples
                : 0;
            LogService.Info(
                $"SMOOTHNESS-DIAG samples={_smoothnessSamples} " +
                $"sum={_smoothnessSum:F2} preRounding={preRounding:F4} " +
                $"movementEvents={_movementEvents} sessionSeconds={_sessionSeconds:F0}");
            return preRounding;
        }

        // ── What's New banner ─────────────────────────────────────────────
        private void CheckWhatsNew()
        {
            try
            {
                string current  = System.Reflection.Assembly.GetExecutingAssembly()
                                      .GetName().Version?.ToString(3) ?? "";   // "1.0.32"
                var    settings = SettingsService.Load();

                if (settings.LastVersionSeen == current) return;

                WhatsNewVersionText.Text = $"What's new in v{current}";
                WhatsNewBodyText.Text    = GetWhatsNewText(current);
                WhatsNewBanner.Visibility = Visibility.Visible;

                settings.LastVersionSeen = current;
                SettingsService.Save(settings);
            }
            catch { /* non-critical */ }
        }

        private static string GetWhatsNewText(string version) => version switch
        {
            "1.0.102" => "Everything is unlocked · Clean Aim Tracker is now a one-time purchase, with no add-ons and no subscription · " +
                        "The full coach, every report, history, trends, export and the overlay are all included · " +
                        "If you installed before the switch, all of it is yours free, for good. Thanks for being here early",

            "1.0.101" => "Fixed the coach reporting a time on drills that have no per-shot timing · " +
                        "On hold-to-spray drills like Track the Head it could read \"0ms avg time per target\", " +
                        "which was the app's way of saying \"no reading\" leaking out as if it were a real number · " +
                        "Those drills now report only what was actually measured",

            "1.0.100" => "An honesty pass on everything the coach says · " +
                        "Where it can't actually see a cause, it now says what it measured and what to test, instead of telling you what your hand was doing · " +
                        "Removed claims the app has no data for — nothing about how rare your numbers are or what other players do · " +
                        "The Pro preview is clearly labelled as an example, so sample figures can't be mistaken for your own results · " +
                        "Hold-to-spray drills no longer report in-flight overshoot, which was being measured off the fire rate rather than your aim · " +
                        "Fixed the consistency score — it was giving a free perfect mark to the first movement of every session, so yours may read slightly lower and more honestly now",

            "1.0.99" => "Your coach now tells you what a finding looks like in the game you actually play — the same habit reads differently in CS2 than it does in Apex · " +
                        "Custom game profiles now save properly, and the sensitivity maths behind them was wrong — if you made one before, remake it · " +
                        "Fixed a locked report claiming you had free sessions left when your coaching had already finished · " +
                        "Skipping the intro now actually sticks · " +
                        "\"Restore purchases\" no longer says you own nothing when it simply could not reach the Store · " +
                        "Dynamic Clicking (Bounce) now speeds up off every wall, not just the sides",

            "1.0.93" => "The coach report no longer interrupts you after every single drill · " +
                        "Between drills you now get one line — what the coach saw — and the next drill starts whenever you're ready · " +
                        "When you finish training you get one report covering the whole session, and if you close the app first it's waiting next time you open it · " +
                        "Prefer the old way? Settings → Coaching → \"Coach report after every drill\"",


            "1.0.92" => "NEW — Today's session: a short routine built from your own profile — a warm-up on your strongest area, real work on your weakest, and the drill your coach is verifying · " +
                        "It runs the drills back to back and reports ONCE at the end instead of interrupting you after every single one · " +
                        "Picking your own drill works exactly as before — both options sit side by side, neither is the default · " +
                        "NEW — Your standing: a named rank on the dashboard, anchored to the same benchmark thresholds the coach uses. It tells you which standard you clear, never what percentile you're in",


            "1.0.91" => "Your Aim Profile is now clickable — open it for a full breakdown of all six axes · " +
                        "Each axis shows what it's scored against, how many sessions back it, and which drills feed it, " +
                        "so a low spoke comes with a route to fix it · " +
                        "Axes you haven't played tell you what to run to unlock them · " +
                        "Copy your profile to the clipboard to share it",


            // CAT_COACH_AUDIT: an honesty release. Nothing new was added — several things
            // the coach used to claim were removed because the data never supported them.
            "1.0.90" => "Coach honesty pass — the coach no longer comments on shot timing in hold-to-fire drills, where there is no per-shot timing to measure · " +
                        "It no longer reads a horizontal/vertical tracking split on drills whose targets only move sideways · " +
                        "Streak feedback no longer claims when in the session your streak happened — that was never recorded · " +
                        "A strong session is no longer given a \"weakest area\" just because something had to rank last · " +
                        "Likely causes are now offered as things to check rather than stated as findings · " +
                        "Your standing now shows on every drill: benchmark thresholds where a real standard exists, and your own personal best where none does",


            // CAT_TELEMETRY: the only user-visible change in this build is the privacy
            // disclosure itself, so that is what the note says. Announcing analytics
            // plainly is the point — a user who finds out later feels tracked, a user
            // who is told feels informed.
            "1.0.89" => "Clean Aim Tracker now collects anonymous usage data — which drills get played and whether features get opened — so I can tell what's actually working and fix what isn't · " +
                        "Your results never leave your device: no scores, no accuracy, no reaction times, no sensitivity, nothing that identifies you · " +
                        "You'll see a one-time note explaining it, and you can turn it off any time in Settings → Privacy",

            "1.0.88" => "NEW — Your Aim Profile: a six-axis shape on the dashboard showing where you actually stand — Flick, Tracking, Switching, Precision, Speed and Consistency, scored against benchmark thresholds. Axes you haven't played show as empty, not as a bad score · " +
                        "Every result now tells you where you rank and exactly what it takes to reach the next rung — no more \"good session\" with nothing to aim at · " +
                        "Your active fix now has a visible plan: what the coach is fixing, which session you're on, and whether the number is actually moving",

            "1.0.87" => "NEW — Bot drills: strafing targets with real heads. Land headshots for bonus points in Headshot Strafes, Peek & Click, and Track the Head · " +
                        "Hold to fire — Tracking, Air Tracking and SMG/AR now work like your actual weapon: hold the button and stay on target instead of clicking · " +
                        "Fullscreen mode (F11) with a redesigned HUD — live score, streak, accuracy, headshot %, and your exact sensitivity · " +
                        "Your first 5 drills now come fully coached, so you can see the coach find a habit, prescribe a fix, and confirm it worked · " +
                        "Set a streak goal — pick 7, 14 or 30 days and hold it · " +
                        "Smarter, more honest coach: it now reads the true direction of your misses (landing short vs long) instead of guessing · " +
                        "Founder's pricing — unlock the coach forever for a one-time price, no subscription required",

            "1.0.86" => "Retention and reliability pass — smarter reminders, streak goals, and a clearer picture of what Pro includes",

            "1.0.32" => "Player Panel with tier, streak, and daily challenges · " +
                        "29 achievements with unlock popups · " +
                        "Personal Bests tab in history · " +
                        "Scenario-aware coaching benchmarks · " +
                        "Clean game profile names in dropdown",

            "1.0.33" => "Sensitivity recommendation rebuilt — now uses your actual game sensitivity · " +
                        "Fortnite sensitivity fix — recommendations are now accurate · " +
                        "XP and level system — earn XP every drill · " +
                        "Hot streak mode — 5 consecutive hits triggers 2× score · " +
                        "Share Result button on personal best sessions · " +
                        "Smarter notifications reference your streak and last accuracy · " +
                        "Daily challenge card shows countdown timer · " +
                        "Sensitivity window no longer auto-opens after drills",

            "1.0.66" => "NEW — Your Aim Profile: a cross-session read of who you are as a player. " +
                        "The coach now connects your last 20 sessions into one story — names what's working, " +
                        "finds the ONE thing to fix, and tells it straight (no fluff, no guesswork). " +
                        "Open any result and check the \"YOUR AIM\" card · " +
                        "Smarter overshoot/undershoot coaching — tells \"clicks land long\" from \"flies past mid-motion\" " +
                        "and gives the right fix for each · " +
                        "Reaction-timing analysis is far more accurate · " +
                        "Coming soon: per-game analysis that factors in your real matches.",

            _        => "Bug fixes and performance improvements.",
        };

        private void WhatsNewDismiss_Click(object sender, RoutedEventArgs e)
            => WhatsNewBanner.Visibility = Visibility.Collapsed;

        private void WeeklyRecapDismiss_Click(object sender, RoutedEventArgs e)
            => WeeklyRecapBanner.Visibility = Visibility.Collapsed;

        // ── Weekly Aim Recap (Feature 3) ─────────────────────────────────
        private void CheckWeeklyAimRecap()
        {
            try
            {
                var settings = SettingsService.Load();

                // Show on Mondays (first launch of the week), or if 7+ days since last shown
                bool isDueMonday  = DateTime.Today.DayOfWeek == DayOfWeek.Monday
                                    && settings.LastWeeklySummaryDate.Date < DateTime.Today.AddDays(-1);
                bool isOverdue    = (DateTime.Today - settings.LastWeeklySummaryDate.Date).TotalDays >= 7;
                if (!isDueMonday && !isOverdue) return;

                var allDrills = AimTrainerStorage.LoadAll();
                var lastWeek  = allDrills
                    .Where(r => r.Timestamp.Date >= DateTime.Today.AddDays(-7)
                             && r.Timestamp.Date <  DateTime.Today)
                    .ToList();

                if (lastWeek.Count < 2) return; // not enough data to be meaningful

                settings.LastWeeklySummaryDate = DateTime.Today;
                SettingsService.Save(settings);

                double avgAcc  = lastWeek.Average(r => r.Accuracy);
                double bestAcc = lastWeek.Max(r => r.Accuracy);
                int    xpList  = lastWeek.Sum(r => XPService.CalculateSessionXP(r));

                WeeklyRecapTitleText.Text = $"Last week: {lastWeek.Count} session{(lastWeek.Count == 1 ? "" : "s")} · +{xpList} XP";
                WeeklyRecapBodyText.Text  = $"Avg accuracy {avgAcc:F0}%  ·  Peak {bestAcc:F0}%  — keep it going 💪";
                WeeklyRecapBanner.Visibility = Visibility.Visible;
            }
            catch { /* non-critical */ }
        }

        // ── Today's stats ─────────────────────────────────────────────────
        private void LoadTodayStats(bool animatePanel = false)
        {
            try
            {
                var all = SessionStorage.LoadAll();
                var today = all.Where(s => s.Timestamp.Date == DateTime.Today).ToList();

                // 5F: animate session count after a session, static on load
                if (animatePanel)
                    AnimateSessionsCount(today.Count);
                else
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

                // TASK-15 / TASK-16: Tier + FreeCoachTeaser — both use AimTrainer data
                var aimDrillsForTier = AimTrainerStorage.LoadAll();
                double aimAvgForTier = aimDrillsForTier.Count > 0 ? aimDrillsForTier.Average(r => r.Accuracy) : 0;
                var tier = ProgressionService.GetTierForAvg(aimAvgForTier, aimDrillsForTier.Count);

                // Assessment Prompt Card — shown to new users who haven't taken the assessment yet
                var freeSettings = SettingsService.Load();
                int drillCount   = aimDrillsForTier.Count;

                bool showAssessmentPrompt =
                    drillCount < 5
                    && freeSettings.DiagnosticHistory.Count == 0
                    && !freeSettings.DismissedAssessmentPrompt
                    && (freeSettings.CalibrationComplete || freeSettings.OnboardingSkipped); // TASK-14: suppress during active onboarding
                AssessmentPromptCard.Visibility = showAssessmentPrompt
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                // TASK-16: Show/update FreeCoachTeaser
                if (!freeSettings.HasUsedFreeFullSession && !TrialService.IsFullVersion())
                {
                    FreeCoachTeaser.Visibility = Visibility.Visible;
                    FreeCoachProgress.Value    = Math.Min(drillCount, 5);

                    if (drillCount >= 5)
                    {
                        FreeCoachTeaserTitle.Text = "🎯 Your full coaching report is ready.";
                        FreeCoachTeaserBody.Text  = "Run any drill to unlock your complete analysis — free, one time.";
                        FreeCoachProgressLabel.Text = "Ready — run a drill to unlock";
                    }
                    else
                    {
                        FreeCoachTeaserTitle.Text   = "🎯 Your full coaching report is almost ready.";
                        FreeCoachTeaserBody.Text    = "After session 5 your coach unlocks a complete analysis — every strength, every weakness, exactly what to work on. No blur. No guessing.";
                        FreeCoachProgressLabel.Text = $"{drillCount} of 5 sessions complete";
                    }
                }
                else
                {
                    FreeCoachTeaser.Visibility = Visibility.Collapsed;
                }
                TierText.Text = $"{tier.Emoji} {tier.Name}";
                TierText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tier.Color));

                // TASK-12: Session context line
                string dayName = DateTime.Today.DayOfWeek.ToString();
                int todayCount = today.Count;
                SessionContextText.Text = todayCount > 0
                    ? $"{dayName} · {todayCount} session{(todayCount == 1 ? "" : "s")} today"
                    : $"{dayName} · No sessions yet today";

                LoadPlayerPanel(all, animatePanel);
            }
            catch { /* ignore if no data */ }
        }

        // ── TASK-08–11: Player Panel ──────────────────────────────────────
        private void LoadPlayerPanel(System.Collections.Generic.IEnumerable<SessionSummary> sessions,
                                     bool animate = false)
        {
            try
            {
                var list = sessions.ToList();
                double avgQuality = list.Count > 0
                    ? list.Average(s => s.OverallQualityScore)
                    : 0;

                // ── TASK-08: Tier Hero Card — based on AimTrainer accuracy ──
                var aimDrills  = AimTrainerStorage.LoadAll();
                double aimAvg  = aimDrills.Count > 0 ? aimDrills.Average(r => r.Accuracy) : 0;
                var tier       = ProgressionService.GetTierForAvg(aimAvg, aimDrills.Count);
                TierEmojiText.Text = tier.Emoji;
                TierNameText.Text  = tier.Name;
                TierAvgText.Text   = aimDrills.Count > 0
                    ? $"Avg accuracy: {aimAvg:F0}%"
                    : "No sessions yet";
                TierNextGoalText.Text = tier.NextGoal;
                double progressPct = CalculateTierProgress(aimAvg, tier.Name) * 100;
                if (!animate) TierProgressBar.Value = progressPct;  // animation handles it when animate=true

                var tierColor = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tier.Color));
                TierNameText.Foreground = tierColor;
                TierProgressBar.Foreground = tierColor;

                // ── TASK-09: Streak Card ──────────────────────────────────
                var (currentStreak, bestStreak) = StreakService.GetStreakInfo();
                if (!animate) StreakValueText.Text = currentStreak.ToString();  // animation handles it
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

                UpdateStreakGoalUi(currentStreak);

                // ── CAT_AIM_RADAR ─────────────────────────────────────────
                LoadAimRadar(aimDrills);

                // ── CAT_ROUTINE ───────────────────────────────────────────
                LoadRoutineCard();

                // ── CAT_SESSION_REPORT ────────────────────────────────────
                // The locked-coach card has to be evaluated on every dashboard load.
                // It used to be reachable ONLY from the tracker session-end path, so a
                // card meant to be permanently present was in practice almost never
                // shown — which defeats the entire point of moving the ask here.
                MaybeShowValueMoment();

                // ── TASK-10: Daily Challenge Card ─────────────────────────
                var settings  = SettingsService.Load();
                var challenge = DailyChallengeService.GetToday();
                bool done     = DailyChallengeService.IsTodayComplete(settings);
                ChallengeDescText.Text = challenge.ShortDesc;
                if (done)
                {
                    ChallengeStatusBadge.Text       = " · ✓ Done";
                    ChallengeStatusBadge.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(76, 175, 80));
                    ChallengeAcceptBtn.Visibility         = Visibility.Collapsed;
                    ChallengeDoneText.Visibility          = Visibility.Visible;
                    ChallengeCountdownText.Visibility     = Visibility.Collapsed;
                    _challengeCountdownTimer?.Stop();
                    _challengeCountdownTimer = null;
                }
                else
                {
                    ChallengeStatusBadge.Text       = " · ⚡ Active";
                    ChallengeStatusBadge.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryText");
                    ChallengeAcceptBtn.Visibility   = Visibility.Visible;
                    ChallengeDoneText.Visibility    = Visibility.Collapsed;
                    StartChallengeCountdown();
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

                // ── TASK-03: Next action hint (first 5 sessions) ──────────
                int sessionCount = aimDrills.Count;
                if (sessionCount == 0)
                {
                    NextActionHint.Visibility = Visibility.Visible;
                    NextActionHintText.Text   = "Hit Start Training below to begin your first drill!";
                }
                else if (sessionCount < 5 && !DailyChallengeService.HasCompletedToday())
                {
                    NextActionHint.Visibility = Visibility.Visible;
                    NextActionHintText.Text   = "Try today's challenge to earn your first achievement!";
                }
                else if (sessionCount < 5)
                {
                    NextActionHint.Visibility = Visibility.Visible;
                    NextActionHintText.Text   = $"Session {sessionCount} done — start another drill to build your streak!";
                }
                else
                {
                    NextActionHint.Visibility = Visibility.Collapsed;
                }

                // ── TASK-01: Trigger celebration animations post-session ───
                if (animate)
                    AnimatePlayerPanelAfterSession(currentStreak, progressPct);
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

        // Card MouseDown and Accept button both call the same logic
        private void DailyChallenge_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => LaunchDailyChallenge();

        private void DailyChallenge_ButtonClick(object sender, RoutedEventArgs e)
            => LaunchDailyChallenge();

        private void LaunchDailyChallenge()
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
                Dispatcher.Invoke(() =>
                {
                    var latestResults = AimTrainerStorage.LoadAll();
                    bool completed    = false;
                    if (latestResults.Count > 0)
                    {
                        var latest        = latestResults.OrderByDescending(r => r.Timestamp).First();
                        var freshSettings = SettingsService.Load();
                        completed = DailyChallengeService.TryComplete(challenge, latest, freshSettings);
                    }
                    // Refresh with animations
                    LoadTodayStats(animatePanel: true);
                    if (completed)
                    {
                        MessageBox.Show(
                            "Challenge complete! Well done.",
                            "Daily Challenge",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                });
            };

            trainer.PreSelectScenario(challenge.Scenario, challenge.Difficulty);
            trainer.Show();
        }

        private void AchievementPanel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            NavAchievements_Click(sender, e);
        }

        private void ResetDisplaysToIdle()
        {
            DxDyText.Text                = "--";
            TotalDistanceText.Text       = "--";
            CmPer360Text.Text             = "--";
            SensDisplayText.Text          = "--";
            SensDisplayLabel.Text         = "Sensitivity";
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
            _challengeCountdownTimer?.Stop();
            _challengeCountdownTimer = null;
            base.OnClosed(e);
        }

        // ─────────────────────────────────────────────────────────────
        // TASK-4A: Challenge countdown timer
        // ─────────────────────────────────────────────────────────────
        private void StartChallengeCountdown()
        {
            _challengeCountdownTimer?.Stop();
            _challengeCountdownTimer = new DispatcherTimer
                { Interval = TimeSpan.FromMinutes(1) };
            _challengeCountdownTimer.Tick += (_, _) => UpdateChallengeCountdown();
            _challengeCountdownTimer.Start();
            UpdateChallengeCountdown(); // update immediately so label doesn't wait 1 min
        }

        private void UpdateChallengeCountdown()
        {
            var remaining = DateTime.Today.AddDays(1) - DateTime.Now;

            // Only show when under 12 hours remain
            if (remaining.TotalHours >= 12)
            {
                ChallengeCountdownText.Visibility = Visibility.Collapsed;
                return;
            }

            if (remaining.TotalMinutes <= 0)
            {
                _challengeCountdownTimer?.Stop();
                _challengeCountdownTimer = null;
                ChallengeCountdownText.Visibility = Visibility.Collapsed;
                return;
            }

            int h = (int)remaining.TotalHours;
            int m = remaining.Minutes;

            if (remaining.TotalHours < 3)
            {
                ChallengeCountdownText.Text       = $"⚠  {h}h {m:D2}m left";
                ChallengeCountdownText.Foreground =
                    (System.Windows.Media.Brush)FindResource("AccentOrange");
            }
            else
            {
                ChallengeCountdownText.Text       = $"⏱  {h}h {m:D2}m left";
                ChallengeCountdownText.Foreground =
                    (System.Windows.Media.Brush)FindResource("TextMuted");
            }

            ChallengeCountdownText.Visibility = Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────
        // CAT_STREAK_GOAL: a self-chosen streak target + visible progress.
        // A passive streak readout has nothing at stake; a goal the user PICKED
        // creates the loss aversion that drives week-2 return (strongest ~day 7).
        // ─────────────────────────────────────────────────────────────
        // ── CAT_AIM_RADAR: six-axis skill shape ───────────────────────────────
        // The identity artifact. One glance answers "what kind of aimer am I?", which
        // the coach's per-session diagnosis never could. Deliberately free: it's the
        // thing worth screenshotting, and a visibly dented spoke is the most honest
        // reason to care about the fix the coach is selling.
        private AimRadarService.AimRadar? _radar;

        private void LoadAimRadar(System.Collections.Generic.List<AimTrainerResult> aimDrills)
        {
            try
            {
                _radar = AimRadarService.Build(aimDrills);

                if (!_radar.HasEnoughData)
                {
                    AimRadarCard.Visibility = Visibility.Collapsed;
                    return;
                }

                AimRadarCard.Visibility    = Visibility.Visible;
                AimRadarOverallText.Text   = $"{_radar.AverageScore:F0} / 100";
                AimRadarSummaryText.Text   = AimRadarService.Summarize(_radar);

                // CAT_RANK: the named standing, anchored to the same benchmark thresholds
                // the radar uses — so the rank and the spokes can never disagree.
                var standing = RankService.FromRadar(_radar);
                if (standing.IsRanked)
                {
                    RankNameText.Text = standing.TierName.ToUpperInvariant();
                    RankNameText.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                            .ConvertFromString(RankService.ColorFor(standing.TierIndex)));

                    RankNextText.Text = standing.NextTierName == null
                        ? standing.Explanation
                        : $"{standing.PointsToNext:F0} points to {standing.NextTierName}";

                    RankPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    RankPanel.Visibility = Visibility.Collapsed;
                }

                var missing = _radar.Axes.Where(a => !a.HasData).Select(a => a.Name).ToList();
                var thin    = _radar.Axes.Where(a => a.HasData && a.IsProvisional).Select(a => a.Name).ToList();

                var notes = new System.Collections.Generic.List<string>();
                if (missing.Count > 0)
                    notes.Add($"No data yet: {string.Join(", ", missing)} — play one to fill {(missing.Count == 1 ? "it" : "them")} in.");
                if (thin.Count > 0)
                    notes.Add($"Still settling: {string.Join(", ", thin)}.");
                notes.Add("Scored against Voltaic benchmark thresholds at each session's own difficulty. Consistency is measured against your own sessions.");
                AimRadarFootnote.Text = string.Join(" ", notes);

                DrawAimRadar();

                // CAT_TELEMETRY: does the identity artifact do anything? Sending the
                // COUNT of populated axes (not the scores) tells us whether people are
                // filling the shape in — which is the behaviour the radar is meant to
                // provoke — without shipping anyone's skill numbers off the machine.
                TelemetryService.TrackAimRadarSeen(_radar.Axes.Count(a => a.HasData));
            }
            catch
            {
                // A cosmetic card must never take the dashboard down with it.
                AimRadarCard.Visibility = Visibility.Collapsed;
            }
        }

        private void AimRadarCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawAimRadar();

        // The card is a doorway, not just a readout — the shape provokes the question
        // ("why is Speed low?") and this is where it gets answered.
        private void AimRadarCard_Click(object sender, MouseButtonEventArgs e)
            => AimProfileWindow.Open(this);

        // Delegates to the shared renderer so this card and the full breakdown window
        // can never draw different shapes from the same data.
        private void DrawAimRadar()
        {
            if (_radar == null || AimRadarCanvas == null) return;
            AimRadarRenderer.Draw(AimRadarCanvas, _radar, this);
        }

        private void UpdateStreakGoalUi(int currentStreak)
        {
            try
            {
                var s = SettingsService.Load();

                // No goal yet — offer the ask, but only once they've actually trained
                // (a goal means nothing before the first session) and only once.
                if (s.StreakGoalDays <= 0)
                {
                    StreakGoalPanel.Visibility = Visibility.Collapsed;
                    SetStreakGoalBtn.Visibility = AimTrainerStorage.LoadAll().Count > 0
                        ? Visibility.Visible : Visibility.Collapsed;
                    return;
                }

                SetStreakGoalBtn.Visibility = Visibility.Collapsed;
                StreakGoalPanel.Visibility  = Visibility.Visible;

                int goal = s.StreakGoalDays;
                int done = Math.Min(currentStreak, goal);
                double frac = goal > 0 ? (double)done / goal : 0;

                // Bar width is measured from the card, so it stays correct at any size.
                double maxW = StreakGoalPanel.ActualWidth > 20 ? StreakGoalPanel.ActualWidth : 200;
                StreakGoalFill.Width = Math.Max(0, maxW * Math.Clamp(frac, 0, 1));

                int left = Math.Max(0, goal - currentStreak);
                StreakGoalText.Text = currentStreak >= goal
                    ? $"🏆 {goal}-day goal complete — set a bigger one?"
                    : left == 1
                        ? $"1 day from your {goal}-day goal — don't drop it now."
                        : $"{done} of {goal} days · {left} to go";
            }
            catch { /* the streak card must never break the dashboard */ }
        }

        private void SetStreakGoal_Click(object sender, RoutedEventArgs e)
        {
            var choice = MessageBox.Show(
                "Pick a streak goal — a target you commit to.\n\n" +
                "YES  →  7 days   (build the habit)\n" +
                "NO   →  14 days  (lock it in)\n" +
                "CANCEL → 30 days (serious)\n\n" +
                "Training any day keeps the streak alive.",
                "Set your streak goal",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            int goal = choice switch
            {
                MessageBoxResult.Yes => 7,
                MessageBoxResult.No  => 14,
                _                    => 30,
            };

            var s = SettingsService.Load();
            s.StreakGoalDays       = goal;
            s.StreakGoalPromptedAt = DateTime.Now;
            SettingsService.Save(s);

            var (cur, _) = StreakService.GetStreakInfo();
            UpdateStreakGoalUi(cur);
        }

        // ─────────────────────────────────────────────────────────────
        // TASK-01: Post-session celebration animations
        // ─────────────────────────────────────────────────────────────
        private void AnimatePlayerPanelAfterSession(int newStreak, double tierProgressPct)
        {
            AnimateCountUp(StreakValueText, 0, newStreak, duration: 800);
            AnimateProgressBar(TierProgressBar, 0, tierProgressPct, duration: 1000);
            if (!DailyChallengeService.HasCompletedToday())
                FlashCard(DailyChallengeCardBorder, color: "#00D4FF", times: 2);
        }

        private void AnimateCountUp(System.Windows.Controls.TextBlock target, int from, int to, int duration)
        {
            int steps = Math.Abs(to - from);
            if (steps == 0) { target.Text = to.ToString(); return; }

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(duration / Math.Max(steps, 1))
            };
            int current = from;
            timer.Tick += (s, e) =>
            {
                current++;
                target.Text = current.ToString();
                if (current >= to) timer.Stop();
            };
            timer.Start();
        }

        private void AnimateProgressBar(System.Windows.Controls.ProgressBar bar,
                                        double from, double to, int duration)
        {
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(duration))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            bar.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, anim);
        }

        private async void FlashCard(Border card, string color, int times)
        {
            var glowColor = (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(color);

            for (int i = 0; i < times; i++)
            {
                card.Effect = new DropShadowEffect
                {
                    Color = glowColor, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.8
                };
                await Task.Delay(300);
                card.Effect = new DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    BlurRadius = 8, ShadowDepth = 0, Opacity = 0.35
                };
                await Task.Delay(200);
            }
            // Restore the cyan glow the card always has
            card.Effect = new DropShadowEffect
            {
                Color = glowColor, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2
            };
        }

        // ─────────────────────────────────────────────────────────────
        // TASK-05A: Atmosphere background drift animation
        // ─────────────────────────────────────────────────────────────
        private void StartAtmosphereAnimation()
        {
            var xAnim = new DoubleAnimation(0, 80, TimeSpan.FromSeconds(8))
            {
                AutoReverse      = true,
                RepeatBehavior   = RepeatBehavior.Forever,
                EasingFunction   = new SineEase()
            };
            var yAnim = new DoubleAnimation(0, 40, TimeSpan.FromSeconds(12))
            {
                AutoReverse      = true,
                RepeatBehavior   = RepeatBehavior.Forever,
                EasingFunction   = new SineEase()
            };
            AtmosphereTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, xAnim);
            AtmosphereTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, yAnim);
        }

        // ─────────────────────────────────────────────────────────────
        // TASK-05F: Animated sessions-today counter
        // ─────────────────────────────────────────────────────────────
        private void AnimateSessionsCount(int newCount)
        {
            TodaySessionsText.Text = newCount.ToString();

            TodaySessionsText.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            TodaySessionsText.RenderTransform = new System.Windows.Media.ScaleTransform();

            var scaleAnim = new DoubleAnimation(1.0, 1.3, TimeSpan.FromMilliseconds(150))
            {
                AutoReverse = true
            };
            ((System.Windows.Media.ScaleTransform)TodaySessionsText.RenderTransform)
                .BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
            ((System.Windows.Media.ScaleTransform)TodaySessionsText.RenderTransform)
                .BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);

            TodaySessionsText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00D4FF"));

            Task.Delay(350).ContinueWith(_ => Dispatcher.Invoke(() =>
                TodaySessionsText.Foreground =
                    (System.Windows.Media.Brush)FindResource("PrimaryText")));
        }
    }
}
