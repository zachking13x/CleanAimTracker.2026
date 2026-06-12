using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Trainer;
using CleanAimTracker.Trainer.Scenarios;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CleanAimTracker.Windows
{
    public partial class AimTrainerWindow : Window
    {
        // ─────────────────────────────────────────────────────────────
        // STATE
        // ─────────────────────────────────────────────────────────────
        private IAimScenario? _scenarioInstance;
        private readonly Random _rng = new();

        // Raw input — collects hardware mouse deltas during active drills
        private readonly RawInputService _rawInput = new();
        private readonly List<RawInputSample> _rawInputBuffer = new();
        private bool _isDrillActive = false;

        // Telemetry buffers — cleared at drill start; read at drill end
        private readonly List<ClickOffsetSample> _clickOffsets   = new();
        private readonly List<TrackingFrame>     _trackingFrames = new();

        private bool _isRunning = false;
        private bool _uiReady = false;

        // Pre-selection set by PreSelectScenario() before Show()
        private string? _preSelectScenario;
        private string? _preSelectDifficulty;

        private readonly DispatcherTimer _gameTimer = new();
        private readonly DispatcherTimer _updateTimer = new();

        private int _secondsLeft;
        private int _durationSeconds = 30;

        // Score (kept at window level)
        private int _score;

        // Hot streak system
        private int    _consecutiveHits  = 0;
        private bool   _isHotStreak      = false;
        private double _scoreMultiplier  = 1.0;

        // Scenario selection
        private string _scenario = "Tracking";
        private string _difficulty = "Medium";
        private string _adaptiveWeakSpot = "Flicking";
        private string _variant = "Smooth";

        // Per-scenario accent colors — matches DESIGN_SPEC.md
        private static readonly Dictionary<string, (byte R, byte G, byte B)> ScenarioColors = new()
        {
            // ── Legacy / weapon scenarios ──────────────────────────────
            ["Tracking"]       = (0x00, 0xD4, 0xFF),   // AccentPrimary
            ["Flicking"]       = (0xFF, 0xB3, 0x47),   // AccentWarm
            ["Precision"]      = (0x00, 0xE5, 0xA0),   // AccentGreen
            ["Switching"]      = (0xFF, 0x6B, 0x35),   // AccentOrange
            ["Adaptive"]       = (0xA8, 0x55, 0xF7),   // AccentPurple
            ["WarmUp"]         = (0x00, 0xC8, 0x53),
            ["Sniper"]         = (0x00, 0xE5, 0xA0),   // patience / precision
            ["Shotgun"]        = (0xFF, 0x6B, 0x35),   // urgency / speed
            ["SmgAr"]          = (0x00, 0xD4, 0xFF),   // sustained movement
            // ── Clicking pillar ───────────────────────────────────────
            ["StaticClicking"] = (0xFF, 0xB3, 0x47),   // AccentWarm — same family as Flicking
            ["DynamicClicking"]= (0xFF, 0x80, 0x00),   // Deeper orange — movement + accuracy
            ["Reactive"]       = (0xFF, 0x45, 0x45),   // Red-orange — reflex / speed
            // ── Tracking pillar ──────────────────────────────────────
            ["AirTracking"]    = (0x00, 0xBF, 0xFF),   // Lighter cyan — air movement
            // ── Switching pillar ─────────────────────────────────────
            ["SpeedSwitching"] = (0xFF, 0x55, 0x20),   // Brighter orange — speed focus
            ["Evasive"]        = (0xFF, 0x6B, 0x35),   // AccentOrange — chase / switch
            ["PeekTraining"]   = (0xE0, 0x60, 0x50),   // Warm red — peek / timing
        };

        // Onboarding mode
        private bool _isOnboarding = false;
        public event Action? OnboardingSessionCompleted;

        // ── Drill Instruction Card (TASK-22) ──────────────────────────────
        // Tracks how many times user has played each scenario+variant combo.
        // Shows the coaching card for the first 5 plays, then auto-dismisses.
        private readonly Dictionary<string, int> _drillPlayCounts = new();
        private readonly DispatcherTimer _instructionTimer = new()
            { Interval = TimeSpan.FromMilliseconds(100) };
        private int    _instructionTicksLeft = 40;   // 40 × 100ms = 4 s
        private double _instructionBarMaxWidth = 364; // set on first show

        // Daily Warm-Up state
        private bool   _isWarmupMode       = false;
        private int    _warmupRound        = 0;
        private int    _warmupTotalHits    = 0;
        private int    _warmupTotalMisses  = 0;
        private double _warmupTotalReactionMs = 0;
        private double _warmupBestReaction = double.MaxValue;
        private int    _warmupMaxStreak    = 0;

        private static readonly string[] WarmupScenarioOrder =
            new[] { "Precision", "Tracking", "Flicking", "Switching" };

        // Difficulty config
        private record DifficultyConfig(double TargetSize, double MoveSpeed, double SpawnDelayMs);
        private static readonly Dictionary<string, DifficultyConfig> DiffConfigs = new()
        {
            ["Easy"] = new(48, 1.5, 1200),
            ["Medium"] = new(36, 2.5, 900),
            ["Hard"] = new(24, 4.0, 600),
            ["Nightmare"] = new(16, 6.0, 350),
        };

        private DifficultyConfig _config = DiffConfigs["Medium"];

        // ─────────────────────────────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────────────────────────────
        public AimTrainerWindow()
        {
            InitializeComponent();

            SourceInitialized += (s, e) =>
            {
                _rawInput.Initialize(this);
                _rawInput.MouseMoved += OnAimTrainerRawInput;
            };

            Loaded += (_, _) =>
            {
                _uiReady = true;
                RefreshNightmareLock();
                if (_preSelectScenario != null)
                    ApplyPreSelection(_preSelectScenario, _preSelectDifficulty ?? "Medium");
            };

            _gameTimer.Interval = TimeSpan.FromSeconds(1);
            _gameTimer.Tick += GameTimer_Tick;

            _updateTimer.Interval = TimeSpan.FromMilliseconds(16);
            _updateTimer.Tick += UpdateScenario_Tick;

            _instructionTimer.Tick += InstructionTimer_Tick;

            TargetCanvas.SizeChanged += (_, _) =>
            {
                PositionCrosshair();
                UpdateTimerBar();
            };

            LoadAdaptiveWeakSpot();
        }

        // ─────────────────────────────────────────────────────────────
        // ONBOARDING
        // ─────────────────────────────────────────────────────────────
        public void BeginOnboardingSession()
        {
            _isOnboarding    = true;
            _scenario        = "Tracking";
            _variant         = "Smooth";
            _difficulty      = "Medium";
            _durationSeconds = 30;
            _config          = DiffConfigs["Medium"];

            ScenarioLabel.Text   = "Tracking";
            DifficultyLabel.Text = "Medium";

            StartDrill();
            OnboardingHintText.Visibility = Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────
        // ADAPTIVE LOGIC
        // ─────────────────────────────────────────────────────────────
        private void LoadAdaptiveWeakSpot()
        {
            var all = AimTrainerStorage.LoadAll();
            if (all.Count == 0) return;

            // Determine weakest scenario by average accuracy across the last 10 results
            // for each of the four scenarios. -1 means the scenario has never been played.
            var scenarios = new[] { "Flicking", "Precision", "Tracking", "Switching" };
            var avgByScenario = new Dictionary<string, double>();

            foreach (var scenario in scenarios)
            {
                var recent = all
                    .Where(r => r.Scenario == scenario)
                    .OrderByDescending(r => r.Timestamp)
                    .Take(10)
                    .ToList();

                avgByScenario[scenario] = recent.Count > 0
                    ? recent.Average(r => r.Accuracy)
                    : -1;
            }

            // Pick the scenario with the lowest average; unplayed scenarios sort last
            // (never-played should not be chosen unless everything is unplayed).
            var played = avgByScenario.Where(kv => kv.Value >= 0).ToList();
            if (played.Count > 0)
                _adaptiveWeakSpot = played.OrderBy(kv => kv.Value).First().Key;
        }

        // ─────────────────────────────────────────────────────────────
        // UI EVENTS
        // ─────────────────────────────────────────────────────────────
        // PRE-SELECTION (daily challenge launch)
        // ─────────────────────────────────────────────────────────────

        /// <summary>Store scenario + difficulty to apply once the window finishes loading.</summary>
        public void PreSelectScenario(string scenario, string difficulty)
        {
            _preSelectScenario  = scenario;
            _preSelectDifficulty = difficulty;
        }

        private void ApplyPreSelection(string scenario, string difficulty)
        {
            // ── Scenario buttons ────────────────────────────────────────
            var parent = ScenarioBtn_Tracking.Parent as StackPanel;
            if (parent != null)
            {
                var match = parent.Children.OfType<Border>()
                                  .FirstOrDefault(b => b.Tag?.ToString() == scenario);
                if (match != null)
                    ApplyScenarioSelection(match);
            }

            _scenario          = scenario;
            ScenarioLabel.Text = scenario;
            UpdateVariantCombo(scenario);

            // ── Difficulty buttons (TASK-23) ────────────────────────────
            SelectDifficultyButton(difficulty);

            _difficulty      = difficulty;
            _config          = DiffConfigs.GetValueOrDefault(_difficulty, DiffConfigs["Medium"]);
            DifficultyLabel.Text = _difficulty;
        }

        // ─────────────────────────────────────────────────────────────
        private void ScenarioBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border btn) return;

            _scenario = btn.Tag?.ToString() ?? "Tracking";

            ScenarioLabel.Text = _scenario switch
            {
                "Adaptive"       => $"Adaptive → {_adaptiveWeakSpot}",
                "WarmUp"         => "☀️ Daily Warm-Up",
                "SmgAr"          => "SMG / AR",
                "StaticClicking" => "Static Clicking",
                "DynamicClicking"=> "Dynamic Clicking",
                "AirTracking"    => "Air Tracking",
                "SpeedSwitching" => "Speed Switching",
                "PeekTraining"   => "Peek Training",
                _                => _scenario,
            };

            ApplyScenarioSelection(btn);
            UpdateVariantCombo(_scenario);
        }

        // Highlight selected scenario card — restores gradient on deselect, adds glow on select.
        // Searches ALL pillar content panels so cross-pillar deselection works correctly.
        private void ApplyScenarioSelection(Border selected)
        {
            // Collect all scenario card Borders from every pillar panel + standalone slots
            var allPanels = new StackPanel?[]
            {
                PillarContent_Clicking,
                PillarContent_Tracking,
                PillarContent_Switching,
                // Adaptive and WarmUp live directly in the outer sidebar scroll StackPanel.
                // Walk up from selected's parent to find the outermost StackPanel if needed.
                selected.Parent as StackPanel,
            };

            foreach (var panel in allPanels)
            {
                if (panel == null) continue;
                foreach (var child in panel.Children.OfType<Border>())
                {
                    // Skip pillar-header Borders (no Tag that maps to ScenarioColors)
                    string tag = child.Tag?.ToString() ?? "";
                    if (string.IsNullOrEmpty(tag)) continue;

                    child.ClearValue(Border.BackgroundProperty);
                    child.ClearValue(UIElement.EffectProperty);
                    if (ScenarioColors.TryGetValue(tag, out var c))
                        child.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, c.R, c.G, c.B));
                }
            }

            // Selected: brighten left accent bar + glow shadow
            string selTag = selected.Tag?.ToString() ?? "";
            if (ScenarioColors.TryGetValue(selTag, out var sc))
            {
                selected.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, sc.R, sc.G, sc.B));
                selected.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = Color.FromRgb(sc.R, sc.G, sc.B),
                    BlurRadius  = 16,
                    ShadowDepth = 0,
                    Opacity     = 0.5
                };
            }
        }

        // ── Difficulty button selector (TASK-23) ─────────────────────
        private void DiffBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border btn) return;
            string tag = btn.Tag?.ToString() ?? "Medium";

            // Block locked tiers
            if (tag == "Nightmare" && !IsNightmareUnlocked()) return;

            SelectDifficultyButton(tag);
        }

        private void SelectDifficultyButton(string difficulty)
        {
            // Default colours for each tier
            static (string bg, string border, string fg) TierColors(string d) => d switch
            {
                "Easy"      => ("#1460C878", "#2260C878", "#60C878"),
                "Medium"    => ("#2200D4FF", "#4400D4FF", "#00D4FF"),
                "Hard"      => ("#22FFB347", "#44FFB347", "#FFB347"),
                "Nightmare" => ("#22FF4545", "#44FF4545", "#FF4545"),
                _           => ("#2200D4FF", "#4400D4FF", "#00D4FF"),
            };

            var buttons = new[]
            {
                (DiffBtn_Easy,      "Easy"),
                (DiffBtn_Medium,    "Medium"),
                (DiffBtn_Hard,      "Hard"),
                (DiffBtn_Nightmare, "Nightmare"),
            };

            foreach (var (btn, tier) in buttons)
            {
                bool selected = tier == difficulty;
                if (selected)
                {
                    var (bg, bd, _) = TierColors(tier);
                    btn.Background   = (SolidColorBrush)new BrushConverter().ConvertFrom(bg)!;
                    btn.BorderBrush  = (SolidColorBrush)new BrushConverter().ConvertFrom(bd)!;
                }
                else
                {
                    btn.Background  = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
                    btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
                }
            }

            _difficulty = difficulty;
            _config = DiffConfigs.GetValueOrDefault(_difficulty, DiffConfigs["Medium"]);
            DifficultyLabel.Text = _difficulty;
        }

        private void UpdateVariantCombo(string scenario)
        {
            string[] variants = scenario switch
            {
                // ── Legacy / weapon scenarios ──────────────────────────────────
                "Tracking"        => new[] { "Smooth", "Evasive", "Two-Track" },
                "Precision"       => new[] { "Standard", "Micro", "Double" },
                "Flicking"        => new[] { "Standard", "Peripheral", "Pairs" },
                "Switching"       => new[] { "4-Target", "6-Target", "Speed Rush" },
                "Sniper"          => new[] { "Standard", "Moving", "Wind" },
                "Shotgun"         => new[] { "Standard", "Duels", "Peek" },
                "SmgAr"           => new[] { "Standard", "Spray", "Strafe" },
                // ── Clicking pillar ───────────────────────────────────────────
                "StaticClicking"  => new[] { "Standard", "Micro", "Cluster", "Confirmation" },
                "DynamicClicking" => new[] { "Standard", "Bounce", "Arc", "Accelerating" },
                "Reactive"        => new[] { "Standard", "SpeedBurst", "Blink", "Chaotic" },
                // ── Tracking pillar ───────────────────────────────────────────
                "AirTracking"     => new[] { "Diagonal", "Parabolic", "Jetpack", "Falling" },
                // ── Switching pillar ─────────────────────────────────────────
                "SpeedSwitching"  => new[] { "Standard", "Burst", "TwoTarget", "Grid" },
                "Evasive"         => new[] { "Standard", "Aggressive", "Predictive", "Teleport" },
                "PeekTraining"    => new[] { "WideSwing", "Jiggle", "JumpPeek", "CounterStrafe" },
                _                 => new[] { "Standard" },
            };

            VariantCombo.SelectionChanged -= VariantCombo_Changed;
            VariantCombo.Items.Clear();
            foreach (var v in variants)
                VariantCombo.Items.Add(new ComboBoxItem { Content = v });
            VariantCombo.SelectedIndex = 0;
            VariantCombo.IsEnabled = scenario is not "Adaptive" and not "WarmUp";
            VariantCombo.SelectionChanged += VariantCombo_Changed;

            _variant = variants[0];
        }

        private void VariantCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            if (VariantCombo.SelectedItem is ComboBoxItem item)
                _variant = item.Content?.ToString() ?? "Standard";
        }

        private void DurationCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (DurationCombo.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int secs))
            {
                _durationSeconds = secs;
                if (_uiReady && !_isRunning)
                    UpdateTimerDisplay();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) StartStop_Click(this, new RoutedEventArgs());
            if (e.Key == Key.Escape && _isRunning) StopDrill(showResults: true);
        }

        // ─────────────────────────────────────────────────────────────
        // START / STOP
        // ─────────────────────────────────────────────────────────────
        public void StartNewDrill() => StartStop_Click(this, new RoutedEventArgs());

        private void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
                StopDrill(showResults: true);
            else
                StartDrill();
        }

        private void StartDrill()
        {
            _isRunning        = true;
            _score            = 0;
            _consecutiveHits  = 0;
            _isHotStreak      = false;
            _scoreMultiplier  = 1.0;
            HotStreakBanner.Visibility  = Visibility.Collapsed;
            CanvasGlowBorder.Visibility = Visibility.Collapsed;

            IdleMessage.Visibility = Visibility.Collapsed;
            StartStopBtn.Content = "■  Stop Drill";
            StartStopBtn.Background = new SolidColorBrush(Color.FromRgb(180, 40, 40));

            // TASK-3D+3F: Reset timer bar colour and show canvas overlay
            TimerBarFill.Background = new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF));
            LiveStatsOverlay.Visibility = Visibility.Visible;

            ClearTargets();
            UpdateLiveStats();

            if (_scenario == "WarmUp")
            {
                _isWarmupMode          = true;
                _warmupRound           = 1;
                _warmupTotalHits       = 0;
                _warmupTotalMisses     = 0;
                _warmupTotalReactionMs = 0;
                _warmupBestReaction    = double.MaxValue;
                _warmupMaxStreak       = 0;
                _secondsLeft           = 60;

                WarmUpRoundText.Text       = $"Round 1/4 — {WarmupScenarioOrder[0]}";
                WarmUpRoundText.Visibility = Visibility.Visible;

                _scenarioInstance = new StaticScenario("Precision", "Standard");
            }
            else
            {
                _isWarmupMode              = false;
                _secondsLeft               = _durationSeconds;
                WarmUpRoundText.Visibility = Visibility.Collapsed;

                // Show coaching card for first 5 plays (TASK-22)
                MaybeShowDrillInstruction();

                if (_scenario == "Adaptive")
                {
                    _scenarioInstance = new AdaptiveScenario(_adaptiveWeakSpot);
                }
                else
                {
                    _scenarioInstance = _scenario switch
                    {
                        // ── Legacy / weapon scenarios ──────────────────────────────
                        "Tracking"        => new TrackingScenario(_variant),
                        "Switching"       => new SwitchingScenario(_variant),
                        "Flicking"        => new StaticScenario("Flicking", _variant),
                        "Sniper"          => new SniperScenario(_variant),
                        "Shotgun"         => new ShotgunScenario(_variant),
                        "SmgAr"           => new SmgArScenario(_variant),
                        // ── Clicking pillar ───────────────────────────────────────
                        "StaticClicking"  => new StaticClickingScenario(_variant),
                        "DynamicClicking" => new DynamicClickingScenario(_variant),
                        "Reactive"        => new ReactiveScenario(_variant),
                        // ── Tracking pillar ───────────────────────────────────────
                        "AirTracking"     => new AirTrackingScenario(_variant),
                        // ── Switching pillar ─────────────────────────────────────
                        "SpeedSwitching"  => new SwitchingScenario(_variant),  // uses same class, different variants
                        "Evasive"         => new EvasiveScenario(_variant),
                        "PeekTraining"    => new PeekTrainingScenario(_variant),
                        // ── Default (Precision + any unregistered) ────────────────
                        _                 => new StaticScenario("Precision", _variant),
                    };
                }
            }

            UpdateTimerDisplay();
            _scenarioInstance.Start(TargetCanvas, _config.TargetSize, _config.MoveSpeed, _rng);
            _rawInputBuffer.Clear();
            _clickOffsets.Clear();
            _trackingFrames.Clear();
            _isDrillActive = true;
            _rawInput.Start();
            _gameTimer.Start();
            _updateTimer.Start();
        }

        private void StopDrill(bool showResults)
        {
            _isRunning = false;
            _isDrillActive = false;
            _rawInput.Stop();

            DismissDrillInstruction();   // dismiss card if still showing (TASK-22)

            _gameTimer.Stop();
            _updateTimer.Stop();

            // Capture stats before stopping
            var statsSource = _scenarioInstance;
            _scenarioInstance?.Stop(TargetCanvas);
            _scenarioInstance = null;

            ClearTargets();

            StartStopBtn.Content = "▶  Start Drill";
            StartStopBtn.Background = (Brush)Application.Current.Resources["AccentBrush"];

            IdleMessage.Visibility         = Visibility.Visible;
            WarmUpRoundText.Visibility     = Visibility.Collapsed;
            LiveStatsOverlay.Visibility    = Visibility.Collapsed;
            TimerBarFill.Width             = 0;
            UpdateTimerDisplay();

            if (_isWarmupMode)
            {
                // Accumulate final round stats
                if (statsSource != null)
                {
                    _warmupTotalHits       += statsSource.Hits;
                    _warmupTotalMisses     += statsSource.Misses;
                    _warmupTotalReactionMs += statsSource.AvgReactionMs * statsSource.Hits;
                    if (statsSource.BestReactionMs < _warmupBestReaction)
                        _warmupBestReaction = statsSource.BestReactionMs;
                    if (statsSource.MaxStreak > _warmupMaxStreak)
                        _warmupMaxStreak = statsSource.MaxStreak;
                }
                _isWarmupMode = false;

                if (showResults && (_warmupTotalHits + _warmupTotalMisses) > 0)
                {
                    var result = BuildWarmupResult();
                    SaveResult(result);
                    new AimTrainerResultWindow(result) { Owner = this }.ShowDialog();
                }
            }
            else
            {
                _isWarmupMode = false;

                if (_isOnboarding)
                {
                    _isOnboarding = false;
                    OnboardingHintText.Visibility = Visibility.Collapsed;

                    if (statsSource != null && (statsSource.Hits + statsSource.Misses) > 0)
                    {
                        var result = BuildResult(statsSource);
                        SaveResult(result);
                        // isOnboarding: true suppresses achievement popups during the onboarding flow
                        new AimTrainerResultWindow(result, isOnboarding: true)
                            { Owner = Application.Current.MainWindow }.ShowDialog();
                    }

                    Application.Current.MainWindow.Activate();
                    OnboardingSessionCompleted?.Invoke();
                    Close();
                    return;
                }

                if (showResults && statsSource != null && (statsSource.Hits + statsSource.Misses) > 0)
                {
                    var result = BuildResult(statsSource);

                    // ── TASK-28: Wire telemetry metrics ────────────────────────
                    // PathEfficiency — uses raw delta buffer; startPos/endPos unused by impl
                    if (_rawInputBuffer.Count >= 20)
                    {
                        result.PathEfficiency = TelemetryCalculator.CalculatePathEfficiency(
                            _rawInputBuffer,
                            new System.Windows.Point(0, 0),
                            new System.Windows.Point(0, 0));
                    }

                    // PeekTiming — only available for PeekTraining scenario
                    if (statsSource is PeekTrainingScenario peek && peek.PeekTimingOffsets.Count > 0)
                    {
                        var (earlyPct, latePct) = TelemetryCalculator.CalculatePeekTiming(peek.PeekTimingOffsets);
                        result.PeekEarlyClickPct = earlyPct;
                        result.PeekLateClickPct  = latePct;
                    }

                    // Click offset metrics — Clicking pillar scenarios
                    if (_clickOffsets.Count >= 3)
                    {
                        var (avgOffset, overshootPct, undershootPct) =
                            TelemetryCalculator.CalculateClickOffsets(_clickOffsets);
                        result.AvgClickOffset = avgOffset;
                        result.OvershootPct   = overshootPct;
                        result.UndershootPct  = undershootPct;
                    }

                    // Direction-change lag — Reactive scenario (target spawn = direction change)
                    if (statsSource is ReactiveScenario reactive
                        && reactive.DirectionChangeTimestamps.Count >= 3)
                    {
                        result.AvgDirectionChangeLagMs =
                            TelemetryCalculator.CalculateDirectionChangeLag(
                                reactive.DirectionChangeTimestamps, _rawInputBuffer);
                    }

                    // Axis split — AirTracking only
                    if (_trackingFrames.Count >= 100)
                    {
                        var frames = _trackingFrames
                            .Select(f => (f.CursorPos, f.TargetPos))
                            .ToList();
                        var (hAcc, vAcc) = TelemetryCalculator.CalculateAxisSplit(
                            frames, _config.TargetSize / 2);
                        result.HorizontalTrackingAcc = hAcc;
                        result.VerticalTrackingAcc   = vAcc;
                    }

                    // ── Difficulty unlock update ────────────────────────────────
                    try
                    {
                        var settings = SettingsService.Load();
                        ScenarioDifficultyService.UpdateAfterSession(result, settings);
                        SensitivityTransitionService.UpdateProgress(settings);
                    }
                    catch (Exception ex) { LogService.Error("Telemetry post-session update failed", ex); }

                    // ── Clear telemetry buffers ────────────────────────────────
                    _rawInputBuffer.Clear();
                    _clickOffsets.Clear();
                    _trackingFrames.Clear();

                    SaveResult(result);
                    CheckNightmareUnlock(result);
                    RefreshNightmareLock();
                    new AimTrainerResultWindow(result) { Owner = this }.ShowDialog();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // GAME LOOP
        // ─────────────────────────────────────────────────────────────
        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            _secondsLeft--;
            UpdateTimerDisplay();

            if (_secondsLeft <= 0)
            {
                if (_isWarmupMode && _warmupRound < 4)
                    AdvanceWarmupRound();
                else
                    StopDrill(showResults: true);
            }
        }

        private void UpdateScenario_Tick(object? sender, EventArgs e)
        {
            if (!_isRunning || _scenarioInstance == null)
                return;

            _scenarioInstance.Update(TargetCanvas);

            // TASK-05: collect per-frame axis-split data for AirTracking
            if (_isDrillActive && _scenario == "AirTracking")
            {
                var targetCenter = _scenarioInstance.CurrentTargetCenter;
                if (!double.IsNaN(targetCenter.X))
                {
                    var cursorPos = System.Windows.Input.Mouse.GetPosition(TargetCanvas);
                    _trackingFrames.Add(new TrackingFrame(cursorPos, targetCenter));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // CLICK HANDLING
        // ─────────────────────────────────────────────────────────────
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isRunning || _scenarioInstance == null)
                return;

            var pos = e.GetPosition(TargetCanvas);
            bool hit = _scenarioInstance.HandleClick(pos);

            if (hit)
            {
                // TASK-05: record click offset for post-session telemetry
                var center = _scenarioInstance.LastHitCenter;
                if (!double.IsNaN(center.X))
                    _clickOffsets.Add(new ClickOffsetSample(pos, center));

                _consecutiveHits++;
                if (!_isHotStreak && _consecutiveHits >= 5)
                    ActivateHotStreak();

                _score += (int)(_scenarioInstance.ScorePerHit * _scoreMultiplier);
                PlayHitEffect(pos);
            }
            else
            {
                _consecutiveHits = 0;
                if (_isHotStreak)
                    DeactivateHotStreak();
                PlayMissEffect(pos);
            }

            UpdateLiveStats();
        }

        // TASK-3C: Hit feedback — 3 expanding rings at the click point
        private void PlayHitEffect(Point pos)
        {
            if (!ScenarioColors.TryGetValue(_scenario, out var sc))
                sc = ((byte)0x00, (byte)0xD4, (byte)0xFF);
            var ringColor = Color.FromArgb(180, sc.R, sc.G, sc.B);

            for (int i = 0; i < 3; i++)
            {
                int    delay     = i * 30;
                double startSize = 20;
                var ring = new Ellipse
                {
                    Width            = startSize,
                    Height           = startSize,
                    Stroke           = new SolidColorBrush(ringColor),
                    StrokeThickness  = 1.5,
                    Fill             = Brushes.Transparent,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(ring, pos.X - startSize / 2);
                Canvas.SetTop (ring, pos.Y - startSize / 2);
                TargetCanvas.Children.Add(ring);

                var expandW = new DoubleAnimation(startSize, startSize * 2.2,
                    TimeSpan.FromMilliseconds(200))
                {
                    BeginTime      = TimeSpan.FromMilliseconds(delay),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var expandH  = expandW.Clone();
                var fadeRing = new DoubleAnimation(1.0, 0.0,
                    TimeSpan.FromMilliseconds(200))
                { BeginTime = TimeSpan.FromMilliseconds(delay) };

                ring.BeginAnimation(FrameworkElement.WidthProperty,   expandW);
                ring.BeginAnimation(FrameworkElement.HeightProperty,  expandH);
                ring.BeginAnimation(UIElement.OpacityProperty,        fadeRing);

                var cleanup = new DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(delay + 220) };
                cleanup.Tick += (s, _) =>
                {
                    ((DispatcherTimer)s!).Stop();
                    TargetCanvas.Children.Remove(ring);
                };
                cleanup.Start();
            }
        }

        // TASK-3C: Miss feedback — small red ring at click point
        private void PlayMissEffect(Point pos)
        {
            var ring = new Ellipse
            {
                Width            = 20,
                Height           = 20,
                Stroke           = new SolidColorBrush(Color.FromArgb(200, 220, 50, 50)),
                StrokeThickness  = 1.5,
                Fill             = Brushes.Transparent,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(ring, pos.X - 10);
            Canvas.SetTop (ring, pos.Y - 10);
            TargetCanvas.Children.Add(ring);

            var expand = new DoubleAnimation(20, 36,
                TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var expandH = expand.Clone();
            var fade    = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200));

            ring.BeginAnimation(FrameworkElement.WidthProperty,   expand);
            ring.BeginAnimation(FrameworkElement.HeightProperty,  expandH);
            ring.BeginAnimation(UIElement.OpacityProperty,        fade);

            var cleanup = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            cleanup.Tick += (s, _) =>
            {
                ((DispatcherTimer)s!).Stop();
                TargetCanvas.Children.Remove(ring);
            };
            cleanup.Start();
        }

        // ─────────────────────────────────────────────────────────────
        // HOT STREAK
        // ─────────────────────────────────────────────────────────────
        private void ActivateHotStreak()
        {
            _isHotStreak     = true;
            _scoreMultiplier = 2.0;
            HotStreakBanner.Visibility  = Visibility.Visible;
            CanvasGlowBorder.Visibility = Visibility.Visible;
        }

        private void DeactivateHotStreak()
        {
            _isHotStreak     = false;
            _scoreMultiplier = 1.0;
            HotStreakBanner.Visibility  = Visibility.Collapsed;
            CanvasGlowBorder.Visibility = Visibility.Collapsed;
        }

        // ─────────────────────────────────────────────────────────────
        // NIGHTMARE LOCK  (TASK-23)
        // ─────────────────────────────────────────────────────────────
        private void RefreshNightmareLock()
        {
            bool unlocked = IsNightmareUnlocked();
            NightmareBtnText.Text    = unlocked ? "NM"  : "🔒 NM";
            DiffBtn_Nightmare.Cursor = unlocked ? System.Windows.Input.Cursors.Hand
                                                : System.Windows.Input.Cursors.No;
            DiffBtn_Nightmare.ToolTip = unlocked ? null
                                                  : "Reach 80%+ accuracy on Hard to unlock";
            // If currently on Nightmare but just became locked, fall back to Hard
            if (!unlocked && _difficulty == "Nightmare")
                SelectDifficultyButton("Hard");
        }

        private static bool IsNightmareUnlocked()
        {
            var all = AimTrainerStorage.LoadAll();
            return all.Any(r => r.Difficulty == "Hard" && r.Accuracy >= 80.0);
        }

        /// <summary>
        /// Checks whether the just-completed result is the first time the user
        /// unlocked Nightmare difficulty, and fires a toast notification if so.
        /// </summary>
        private static void CheckNightmareUnlock(AimTrainerResult result)
        {
            if (result.Difficulty != "Hard" || result.Accuracy < 80.0) return;

            // Only fire once — check whether Nightmare was already unlocked before this session
            var all    = AimTrainerStorage.LoadAll();
            var before = all.Where(r => r.Timestamp < result.Timestamp)
                            .Any(r => r.Difficulty == "Hard" && r.Accuracy >= 80.0);
            if (before) return; // Already unlocked previously

            // First time — fire the unlock toast
            try
            {
                var xml = global::Windows.UI.Notifications.ToastNotificationManager
                    .GetTemplateContent(global::Windows.UI.Notifications.ToastTemplateType.ToastText02);
                var nodes = xml.GetElementsByTagName("text");
                nodes[0].AppendChild(xml.CreateTextNode("Nightmare unlocked! 💀"));
                nodes[1].AppendChild(xml.CreateTextNode(
                    $"You hit {result.Accuracy:F0}% on Hard. Nightmare difficulty is now available."));
                global::Windows.UI.Notifications.ToastNotificationManager
                    .CreateToastNotifier()
                    .Show(new global::Windows.UI.Notifications.ToastNotification(xml));
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────────────
        // LIVE STATS
        // ─────────────────────────────────────────────────────────────
        private void UpdateTimerDisplay()
        {
            if (!_uiReady || TimerText == null)
                return;

            int secs = _isRunning ? _secondsLeft : _durationSeconds;
            TimerText.Text = $"{secs / 60}:{secs % 60:D2}";

            if (_isRunning && _secondsLeft <= 5)
                TimerText.Foreground = Brushes.OrangeRed;
            else
                TimerText.Foreground = Brushes.White;

            UpdateTimerBar();
        }

        // TASK-3D: Update visual timer bar
        private void UpdateTimerBar()
        {
            if (!_uiReady || TimerBarFill == null) return;

            if (!_isRunning || _durationSeconds <= 0)
            {
                TimerBarFill.Width = 0;
                return;
            }

            double maxW = Math.Max(0, TargetCanvas.ActualWidth);
            double ratio = Math.Clamp((double)_secondsLeft / _durationSeconds, 0, 1);
            TimerBarFill.Width = maxW * ratio;

            // Turn orange in final 10 seconds
            TimerBarFill.Background = _secondsLeft <= 10
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x35))  // AccentOrange
                : new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF)); // AccentPrimary
        }

        private void UpdateLiveStats()
        {
            if (_scenarioInstance == null)
            {
                LiveHitsText.Text     = "0";
                LiveMissesText.Text   = "0";
                LiveScoreText.Text    = _score.ToString("N0");
                LiveAccuracyText.Text = "--";
                LiveReactionText.Text = "--";
                StreakText.Text       = "0";
                // Canvas overlay reset
                CanvasAccText.Text  = "—";
                CanvasHitsText.Text = "—";
                return;
            }

            int hits   = _scenarioInstance.Hits;
            int misses = _scenarioInstance.Misses;
            int total  = hits + misses;

            LiveHitsText.Text   = hits.ToString();
            LiveMissesText.Text = misses.ToString();
            LiveScoreText.Text  = _score.ToString("N0");

            string accStr = total > 0 ? $"{(hits * 100.0 / total):F0}%" : "--";
            LiveAccuracyText.Text = accStr;

            // TASK-0.3: honest label — "Avg Reaction" only for stimulus-anchored
            // scenarios; hit-anchored scenarios measure time per target.
            LiveReactionLabel.Text = ReactionMetric.IsTrueReaction(_scenario)
                ? "Avg Reaction"
                : "Avg Time/Target";
            LiveReactionText.Text = _scenarioInstance.AvgReactionMs > 0
                ? $"{_scenarioInstance.AvgReactionMs:F0}ms"
                : "--";

            StreakText.Text = _scenarioInstance.MaxStreak.ToString();

            // TASK-3F: Canvas overlay (ACC + HIT only — streak shown in top bar)
            CanvasAccText.Text  = total > 0 ? $"{(hits * 100.0 / total):F0}%" : "—";
            CanvasHitsText.Text = hits.ToString();
        }

        // ─────────────────────────────────────────────────────────────
        // RESULT + RECOMMENDATION
        // ─────────────────────────────────────────────────────────────
        private AimTrainerResult BuildResult(IAimScenario stats)
        {
            int hits = stats.Hits;
            int misses = stats.Misses;
            int total = hits + misses;
            double accuracy = total > 0 ? hits * 100.0 / total : 0;

            // Guard against double.MaxValue sentinel — only emit a real best-reaction
            // value when at least one hit was recorded and the scenario tracked it.
            double bestReaction = (hits > 0 && stats.BestReactionMs < double.MaxValue)
                ? stats.BestReactionMs
                : 0;

            // Resolve pillar from scenario name
            string pillar = _scenario switch
            {
                "StaticClicking" or "DynamicClicking" or "Reactive"
                    or "Flicking" or "Precision" or "Sniper"
                    or "Shotgun" or "SmgAr"               => "Clicking",
                "Tracking" or "AirTracking"               => "Tracking",
                "Switching" or "SpeedSwitching"
                    or "Evasive" or "PeekTraining"        => "Switching",
                _                                         => _scenario,  // Adaptive, WarmUp
            };

            return new AimTrainerResult
            {
                Timestamp       = DateTime.Now,
                Scenario        = _scenario,
                SubVariant      = _variant,
                Difficulty      = _difficulty,
                DurationSeconds = _durationSeconds,
                Hits            = hits,
                Misses          = misses,
                Accuracy        = accuracy,
                Score           = _score,
                AvgReactionMs   = stats.AvgReactionMs,
                BestReactionMs  = bestReaction,
                MaxStreak       = stats.MaxStreak,
                Pillar          = pillar,
            };
        }

        private void AdvanceWarmupRound()
        {
            // Accumulate completed round stats
            if (_scenarioInstance != null)
            {
                _warmupTotalHits       += _scenarioInstance.Hits;
                _warmupTotalMisses     += _scenarioInstance.Misses;
                _warmupTotalReactionMs += _scenarioInstance.AvgReactionMs * _scenarioInstance.Hits;
                if (_scenarioInstance.BestReactionMs < _warmupBestReaction)
                    _warmupBestReaction = _scenarioInstance.BestReactionMs;
                if (_scenarioInstance.MaxStreak > _warmupMaxStreak)
                    _warmupMaxStreak = _scenarioInstance.MaxStreak;
                _scenarioInstance.Stop(TargetCanvas);
                _scenarioInstance = null;
            }

            _warmupRound++;
            _secondsLeft = 60;
            ClearTargets();

            string nextScenario = WarmupScenarioOrder[_warmupRound - 1];
            WarmUpRoundText.Text = $"Round {_warmupRound}/4 — {nextScenario}";

            _scenarioInstance = nextScenario switch
            {
                "Tracking"  => new TrackingScenario("Smooth"),
                "Flicking"  => new StaticScenario("Flicking", "Standard"),
                "Switching" => new SwitchingScenario("4-Target"),
                _           => new StaticScenario("Precision", "Standard"),
            };
            _scenarioInstance.Start(TargetCanvas, _config.TargetSize, _config.MoveSpeed, _rng);
            UpdateTimerDisplay();
        }

        private AimTrainerResult BuildWarmupResult()
        {
            int    total      = _warmupTotalHits + _warmupTotalMisses;
            double accuracy   = total > 0 ? _warmupTotalHits * 100.0 / total : 0;
            double avgReact   = _warmupTotalHits > 0 ? _warmupTotalReactionMs / _warmupTotalHits : 0;
            double bestReact  = _warmupBestReaction == double.MaxValue ? 0 : _warmupBestReaction;

            return new AimTrainerResult
            {
                Timestamp       = DateTime.Now,
                Scenario        = "Daily Warm-Up",
                Difficulty      = _difficulty,
                DurationSeconds = 240,
                Hits            = _warmupTotalHits,
                Misses          = _warmupTotalMisses,
                Accuracy        = accuracy,
                Score           = _score,
                AvgReactionMs   = avgReact,
                BestReactionMs  = bestReact,
                MaxStreak       = _warmupMaxStreak,
            };
        }

        private SensitivityRecommendation BuildRecommendation(AimTrainerResult result)
        {
            var settings = SettingsService.Load() ?? new UserSettings();

            // Use the single canonical profile system (GameProfile.GetAllProfiles) so that
            // name lookups here always match what MainWindow uses — avoids the "CS2" vs
            // "Counter-Strike 2" mismatch that was silently falling back to Valorant.
            var allProfiles = GameProfile.GetAllProfiles(ProfileStorage.LoadProfiles());
            var profile     = allProfiles.FirstOrDefault(p => p.Name == settings.SelectedProfile)
                           ?? allProfiles.First();

            double yaw  = profile.YawPerCount <= 0 ? 0.022 : profile.YawPerCount;
            double dpi  = settings.DPI > 0 ? settings.DPI : 800;
            double sens = settings.Sensitivity > 0 ? settings.Sensitivity : 1.0;

            // Correct formula: sens IS the in-game sensitivity (e.g. 11.1 for Fortnite)
            // cm/360 = (360 / (gameSens * DPI * yaw)) * 2.54
            double cm360 = (360.0 / (sens * dpi * yaw)) * 2.54;

            // The aim trainer has no raw mouse-movement data, so tracker-derived fields
            // (smoothness, correction sharpness, jitter, etc.) cannot be computed from
            // click accuracy without fabricating numbers. Use neutral 50/0 values instead
            // so the recommendation engine applies its confidence cap rather than producing
            // misleading coaching text based on invented metrics.
            var summary = new SessionSummary
            {
                Timestamp           = result.Timestamp,
                DPI                 = (int)Math.Round(dpi),
                Sensitivity         = sens,
                GameSensitivity     = sens,
                CmPer360            = cm360,
                SmoothnessScore     = 50,
                MovementConsistency = 50,
                JitterAmount        = 50,
                TotalSamples        = result.Hits + result.Misses,
                FlickCount          = result.Hits,
                SmallFlickCount     = result.Hits,
                LargeFlickCount     = 0,
                CorrectionSharpness = 50,
                PeakVelocity        = 1,
                AverageVelocity     = 1,
                IdlePercentage      = 0,
                SessionSeconds      = 0,   // 0 forces the engine confidence cap low
            };

            return RecommendationEngine.Analyze(summary, profile);
        }

        private static void SaveResult(AimTrainerResult result)
        {
            try
            {
                AimTrainerStorage.Save(result);
                // Reset re-engagement flag so it can fire again after the next absence gap
                var settings = SettingsService.Load();
                if (settings.ReEngagementNotificationSent)
                {
                    settings.ReEngagementNotificationSent = false;
                    SettingsService.Save(settings);
                }
            }
            catch (Exception ex) { LogService.Error("Failed to save trainer result", ex); }
        }

        // ─────────────────────────────────────────────────────────────
        // DRILL INSTRUCTION CARD  (TASK-22)
        // ─────────────────────────────────────────────────────────────
        private void MaybeShowDrillInstruction()
        {
            // Show card for the first 5 plays of each scenario+variant combo
            string key = $"{_scenario}|{_variant}";
            _drillPlayCounts.TryGetValue(key, out int plays);
            if (plays >= 5) return;   // user has seen it enough times

            // Increment play count
            _drillPlayCounts[key] = plays + 1;

            // Fetch coaching text
            var info = ScenarioInfoRegistry.Get(_scenario, _variant);

            DrillFocusText.Text = info.TrainingFocus;
            DrillCueText.Text   = info.MentalCue;

            // Measure bar max width from the card's current layout width (fallback 364)
            DrillInstructionCard.UpdateLayout();
            double barMax = DrillInstructionCard.ActualWidth - 56;   // 28px padding × 2
            if (barMax < 100) barMax = 364;
            _instructionBarMaxWidth = barMax;
            DrillInstructionTimerBar.Width = _instructionBarMaxWidth;

            _instructionTicksLeft = 40;   // 40 × 100ms = 4 s
            DrillInstructionTimerText.Text = "Dismisses in 4s  ·  Click to dismiss";
            DrillInstructionCard.Visibility = Visibility.Visible;
            _instructionTimer.Start();
        }

        private void InstructionTimer_Tick(object? sender, EventArgs e)
        {
            _instructionTicksLeft--;

            int secsLeft = (_instructionTicksLeft + 9) / 10;  // ceiling
            DrillInstructionTimerText.Text =
                $"Dismisses in {secsLeft}s  ·  Click to dismiss";

            double frac = _instructionTicksLeft / 40.0;
            DrillInstructionTimerBar.Width = _instructionBarMaxWidth * frac;

            if (_instructionTicksLeft <= 0)
                DismissDrillInstruction();
        }

        private void DismissDrillInstruction()
        {
            _instructionTimer.Stop();
            DrillInstructionCard.Visibility = Visibility.Collapsed;
        }

        private void DrillInstructionCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DismissDrillInstruction();
            e.Handled = true;
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────
        private void ClearTargets()
        {
            TargetCanvas.Children.Clear();
        }

        // TASK-3E: Position crosshair elements (in the separate crosshair overlay Canvas)
        private void PositionCrosshair()
        {
            if (!_uiReady) return;
            double cx = TargetCanvas.ActualWidth  / 2;
            double cy = TargetCanvas.ActualHeight / 2;

            // Ring (14×14 centered)
            Canvas.SetLeft(CrosshairRing, cx - 7);
            Canvas.SetTop (CrosshairRing, cy - 7);

            // Top arm: 1.5 wide × 6 tall, 2px gap above ring
            Canvas.SetLeft(CrosshairTop, cx - 0.75);
            Canvas.SetTop (CrosshairTop, cy - 7 - 2 - 6);   // cy - 15

            // Bottom arm: starts 9px below center
            Canvas.SetLeft(CrosshairBottom, cx - 0.75);
            Canvas.SetTop (CrosshairBottom, cy + 7 + 2);     // cy + 9

            // Left arm: 6 wide × 1.5 tall, 2px gap left of ring
            Canvas.SetLeft(CrosshairLeft, cx - 7 - 2 - 6);  // cx - 15
            Canvas.SetTop (CrosshairLeft, cy - 0.75);

            // Right arm: starts 9px right of center
            Canvas.SetLeft(CrosshairRight, cx + 7 + 2);     // cx + 9
            Canvas.SetTop (CrosshairRight, cy - 0.75);
        }

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
            => new AimTrainerHistoryWindow { Owner = this }.ShowDialog();

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _gameTimer.Stop();
            _updateTimer.Stop();
            Close();
        }

        private void RecommendedDrillsBtn_Click(object sender, RoutedEventArgs e)
        {
            var last = AimTrainerStorage.LoadLast();
            if (last == null)
            {
                MessageBox.Show("No trainer results found. Run a drill first.",
                    "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var rec = BuildRecommendation(last);

            var win = new RecommendedDrillsWindow(rec);
            win.Owner = this;
            win.ShowDialog();
        }

        // ─────────────────────────────────────────────────────────────
        // RAW INPUT
        // ─────────────────────────────────────────────────────────────
        private void OnAimTrainerRawInput(int dx, int dy, long timestamp)
        {
            if (!_isDrillActive) return;
            _rawInputBuffer.Add(new RawInputSample(dx, dy, timestamp));
        }


        // ─────────────────────────────────────────────────────────────
        // PILLAR COLLAPSE / EXPAND  (TASK-21 will implement full logic)
        // ─────────────────────────────────────────────────────────────
        private void PillarHeader_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border header) return;

            // Resolve the content panel and arrow for this pillar
            StackPanel? content = null;
            System.Windows.Controls.TextBlock? arrow = null;

            if (header.Name == "PillarHeader_Clicking")
            {
                content = PillarContent_Clicking;
                arrow   = PillarArrow_Clicking;
            }
            else if (header.Name == "PillarHeader_Tracking")
            {
                content = PillarContent_Tracking;
                arrow   = PillarArrow_Tracking;
            }
            else if (header.Name == "PillarHeader_Switching")
            {
                content = PillarContent_Switching;
                arrow   = PillarArrow_Switching;
            }

            if (content == null) return;

            bool collapsed = content.Visibility == Visibility.Collapsed;
            content.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            if (arrow != null)
                arrow.Text = collapsed ? "▲" : "▼";
        }

        protected override void OnClosed(EventArgs e)
        {
            _gameTimer.Stop();
            _updateTimer.Stop();
            _rawInput.Stop();
            base.OnClosed(e);
        }
    }
}
