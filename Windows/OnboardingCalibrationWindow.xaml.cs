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
using System.Windows.Threading;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// TASK-4.1: the ONE first-run flow.
    /// Welcome → Brief → 4 fixed calibration tests → First Insight → First Drill.
    /// Fixed scenarios (one per capability dimension) produce comparable baselines.
    /// Calibration results are stored as baseline data (IsAssessmentSession=true)
    /// but never touch XP, streaks, or achievements — those are granted only on
    /// the normal drill-completion path, which this window does not call.
    /// </summary>
    public partial class OnboardingCalibrationWindow : Window
    {
        private enum FlowState { Welcome, Brief, Calibration, Insight, FirstDrill }
        private FlowState _state = FlowState.Welcome;

        private int  _currentTestIndex;
        private int  _secondsLeft;
        private bool _isTestRunning;

        private IAimScenario? _scenario;
        private readonly Random _rng = new();
        private readonly List<AimTrainerResult> _results = new();
        private DiagnosticProfile? _profile;

        private readonly DispatcherTimer _gameTimer   = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };

        public OnboardingCalibrationWindow()
        {
            InitializeComponent();
            _gameTimer.Tick   += GameTimer_Tick;
            _updateTimer.Tick += UpdateTimer_Tick;
            BuildBriefList();
            ApplyState();
        }

        // ── State machine ────────────────────────────────────────────────────

        private void ApplyState()
        {
            PageWelcome.Visibility     = _state == FlowState.Welcome     ? Visibility.Visible : Visibility.Collapsed;
            PageBrief.Visibility       = _state == FlowState.Brief       ? Visibility.Visible : Visibility.Collapsed;
            PageCalibration.Visibility = _state == FlowState.Calibration ? Visibility.Visible : Visibility.Collapsed;
            PageInsight.Visibility     = _state == FlowState.Insight     ? Visibility.Visible : Visibility.Collapsed;
            PageFirstDrill.Visibility  = _state == FlowState.FirstDrill  ? Visibility.Visible : Visibility.Collapsed;

            (PrimaryBtn.Content, StepIndicator.Text) = _state switch
            {
                FlowState.Welcome     => ((object)"Start calibration", "Step 1 of 4 — about 5 minutes total"),
                FlowState.Brief       => ("Begin test 1", "Step 2 of 4 — four 30-second tests"),
                FlowState.Calibration => ("Skip this test", $"Test {_currentTestIndex + 1} of {DiagnosticAssessmentService.CalibrationTests.Count}"),
                FlowState.Insight     => ("See my first drill", "Step 3 of 4 — your baseline"),
                FlowState.FirstDrill  => ("Start this drill", "Step 4 of 4 — your first prescription"),
                _                     => ("Continue", "")
            };

            // Once calibration data exists, leaving is "finish later", not "skip".
            SkipBtn.Content    = _state >= FlowState.Insight ? "Take me to the app" : "Skip setup";
            SkipBtn.Visibility = _state == FlowState.Calibration ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Primary_Click(object sender, RoutedEventArgs e)
        {
            switch (_state)
            {
                case FlowState.Welcome:
                    _state = FlowState.Brief;
                    ApplyState();
                    break;

                case FlowState.Brief:
                    _state = FlowState.Calibration;
                    _currentTestIndex = 0;
                    _results.Clear();
                    ApplyState();
                    BeginTest(0);
                    break;

                case FlowState.Calibration:
                    if (_isTestRunning) FinishCurrentTest(); // "Skip this test"
                    break;

                case FlowState.Insight:
                    _state = FlowState.FirstDrill;
                    ApplyState();
                    break;

                case FlowState.FirstDrill:
                    StartFirstDrill();
                    break;
            }
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            StopTimers();
            var settings = SettingsService.Load();
            if (_profile != null)
                settings.CalibrationComplete = true;   // they finished — leaving from insight/drill is not a skip
            else
                settings.OnboardingSkipped = true;
            settings.FirstLaunchComplete = true;       // ONE flow — the legacy wizard never runs after this
            SettingsService.Save(settings);
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        // ── Brief list ───────────────────────────────────────────────────────

        private void BuildBriefList()
        {
            BriefTestList.Children.Clear();
            string[] dims = { "Clicking", "Tracking", "Switching", "Reaction" };
            for (int i = 0; i < DiagnosticAssessmentService.CalibrationTests.Count; i++)
            {
                var test = DiagnosticAssessmentService.CalibrationTests[i];
                var row = new Border
                {
                    Background      = (Brush)FindResource("CardBackground"),
                    BorderBrush     = (Brush)FindResource("BorderSubtle"),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(8),
                    Padding         = new Thickness(14, 10, 14, 10),
                    Margin          = new Thickness(0, 0, 0, 8)
                };
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text       = $"{i + 1}.  {dims[i]} — {test.DurationSeconds}s",
                    FontSize   = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("PrimaryText")
                });
                stack.Children.Add(new TextBlock
                {
                    Text       = test.Description,
                    FontSize   = 11,
                    Foreground = (Brush)FindResource("SecondaryText"),
                    Margin     = new Thickness(0, 2, 0, 0)
                });
                row.Child = stack;
                BriefTestList.Children.Add(row);
            }
        }

        // ── Test runner (mirrors DiagnosticAssessmentWindow) ─────────────────

        private void BeginTest(int index)
        {
            if (index >= DiagnosticAssessmentService.CalibrationTests.Count)
            {
                ShowInsight();
                return;
            }

            var test = DiagnosticAssessmentService.CalibrationTests[index];

            TestLabel.Text = $"Test {index + 1}/{DiagnosticAssessmentService.CalibrationTests.Count} — {DiagnosticAssessmentService.GetDimensionLabel(test.Dimension)}";
            TestDesc.Text  = test.Description;
            // TASK-0.3: honest live-stat label per scenario.
            LiveReactLabel.Text = ReactionMetric.IsTrueReaction(test.Scenario)
                ? "REACTION " : "TIME/TARGET ";
            StepIndicator.Text = $"Test {index + 1} of {DiagnosticAssessmentService.CalibrationTests.Count}";

            _scenario = CreateScenario(test);
            TestCanvas.Children.Clear();
            _scenario.Start(TestCanvas, targetSize: 36, moveSpeed: 2.5, _rng);

            _secondsLeft   = test.DurationSeconds;
            _isTestRunning = true;
            UpdateTimerText();
            UpdateLiveStats();

            _gameTimer.Start();
            _updateTimer.Start();
        }

        private void FinishCurrentTest()
        {
            StopTimers();
            _isTestRunning = false;

            if (_scenario != null)
            {
                var test   = DiagnosticAssessmentService.CalibrationTests[_currentTestIndex];
                int hits   = _scenario.Hits;
                int misses = _scenario.Misses;
                int total  = hits + misses;

                _results.Add(new AimTrainerResult
                {
                    Timestamp           = DateTime.Now,
                    Scenario            = test.Scenario,
                    SubVariant          = test.Variant,
                    Difficulty          = "Medium",
                    DurationSeconds     = test.DurationSeconds,
                    Hits                = hits,
                    Misses              = misses,
                    Accuracy            = total > 0 ? hits * 100.0 / total : 0,
                    Score               = hits * 100,
                    AvgReactionMs       = _scenario.AvgReactionMs,
                    BestReactionMs      = _scenario.BestReactionMs < double.MaxValue ? _scenario.BestReactionMs : 0,
                    MaxStreak           = _scenario.MaxStreak,
                    IsAssessmentSession = true,
                    AssessmentDimension = test.Dimension,
                });

                _scenario.Stop(TestCanvas);
                _scenario = null;
            }

            _currentTestIndex++;
            if (_currentTestIndex < DiagnosticAssessmentService.CalibrationTests.Count)
            {
                var pause = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                pause.Tick += (s, _) =>
                {
                    ((DispatcherTimer)s!).Stop();
                    BeginTest(_currentTestIndex);
                };
                pause.Start();
            }
            else
            {
                ShowInsight();
            }
        }

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            _secondsLeft--;
            UpdateTimerText();
            if (_secondsLeft <= 0) FinishCurrentTest();
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_isTestRunning && _scenario != null)
            {
                _scenario.Update(TestCanvas);
                UpdateLiveStats();
            }
        }

        private void TestCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isTestRunning || _scenario == null) return;
            _scenario.HandleClick(e.GetPosition(TestCanvas));
            UpdateLiveStats();
        }

        private IAimScenario CreateScenario(DiagnosticAssessmentService.AssessmentTest test) =>
            (test.Scenario, test.Variant) switch
            {
                ("StaticClicking", var v) => new StaticClickingScenario(v),
                ("Tracking",       var v) => new TrackingScenario(v),
                ("Switching",      var v) => new SwitchingScenario(v),
                ("PeekTraining",   var v) => new PeekTrainingScenario(v),
                _                         => new StaticScenario("Precision", "Standard"),
            };

        // ── First Insight ────────────────────────────────────────────────────

        private void ShowInsight()
        {
            StopTimers();
            _isTestRunning = false;

            // Build + persist the calibration baseline.
            var settings = SettingsService.Load();
            _profile = DiagnosticAssessmentService.BuildCalibrationProfile(
                _results, settings.DiagnosticHistory.Count + 1);
            settings.DiagnosticHistory.Add(_profile);
            settings.CalibrationComplete = true;
            settings.FirstLaunchComplete = true;
            SettingsService.Save(settings);

            // Store the raw results as baseline drills (assessment-flagged —
            // excluded from XP/streak/achievement paths by never firing them).
            foreach (var r in _results)
                AimTrainerStorage.Save(r);

            BuildInsightUI(_profile);

            FreeReportBtn.Visibility = !settings.HasUsedFreeAssessmentReport && _results.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            _state = FlowState.Insight;
            ApplyState();
        }

        private void BuildInsightUI(DiagnosticProfile profile)
        {
            InsightScoreList.Children.Clear();

            var scores = new List<(string Dim, double Score)>();
            for (int i = 0; i < DiagnosticAssessmentService.CalibrationTests.Count && i < _results.Count; i++)
            {
                var test = DiagnosticAssessmentService.CalibrationTests[i];
                scores.Add((test.Dimension, DiagnosticAssessmentService.ScoreTest(_results[i], test)));
            }

            foreach (var (dim, score) in scores)
            {
                bool isWeakest   = dim == profile.WeakestDimension;
                bool isStrongest = dim == profile.StrongestDimension;

                var rowStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

                var labelRow = new Grid();
                labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                string label = DiagnosticAssessmentService.GetDimensionLabel(dim);
                if (isWeakest)   label += "  🔻 weakest";
                if (isStrongest) label += "  ⭐ strongest";

                var nameText = new TextBlock
                {
                    Text       = label,
                    FontSize   = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isWeakest
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x80))
                        : (Brush)FindResource("PrimaryText")
                };
                Grid.SetColumn(nameText, 0);

                var scoreText = new TextBlock
                {
                    Text       = $"{score:F0}",
                    FontSize   = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = ScoreColor(score)
                };
                Grid.SetColumn(scoreText, 1);

                labelRow.Children.Add(nameText);
                labelRow.Children.Add(scoreText);
                rowStack.Children.Add(labelRow);

                var barContainer = new Grid { Margin = new Thickness(0, 5, 0, 0) };
                barContainer.Children.Add(new Border
                {
                    Height       = 7,
                    CornerRadius = new CornerRadius(3.5),
                    Background   = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF))
                });
                barContainer.Children.Add(new Border
                {
                    Height              = 7,
                    CornerRadius        = new CornerRadius(3.5),
                    Background          = ScoreColor(score),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width               = Math.Clamp(score / 100.0, 0, 1) * 540
                });
                rowStack.Children.Add(barContainer);

                InsightScoreList.Children.Add(rowStack);
            }

            // ONE insight sentence — weakest vs strongest, with numbers.
            double weakScore   = scores.FirstOrDefault(s => s.Dim == profile.WeakestDimension).Score;
            double strongScore = scores.FirstOrDefault(s => s.Dim == profile.StrongestDimension).Score;
            string weakLabel   = DiagnosticAssessmentService.GetDimensionLabel(profile.WeakestDimension);
            string strongLabel = DiagnosticAssessmentService.GetDimensionLabel(profile.StrongestDimension);

            InsightSentence.Text =
                $"{weakLabel} is what's holding your aim back right now — {weakScore:F0}/100 against " +
                $"{strongScore:F0}/100 on your strongest skill, {strongLabel}. That's where training starts.";

            // First Drill page content, prepared now.
            var (recScenario, recVariant) = DiagnosticAssessmentService.GetRecommendedStartingScenario(profile);
            FirstDrillName.Text   = $"{recScenario} · {recVariant} — Medium";
            FirstDrillCue.Text    = "30–60 seconds at full focus beats 10 minutes on autopilot.";
            FirstDrillReason.Text =
                $"It targets your weakest dimension, {weakLabel} ({weakScore:F0}/100). " +
                "Run it a few times this week and the coach will measure the change against today's baseline.";
        }

        private void FreeReport_Click(object sender, RoutedEventArgs e)
        {
            if (_results.Count == 0) return;

            var settings = SettingsService.Load();
            settings.HasUsedFreeAssessmentReport = true;
            SettingsService.Save(settings);
            FreeReportBtn.Visibility = Visibility.Collapsed;

            var best = _results.Where(r => r.Accuracy > 0)
                               .OrderByDescending(r => r.Accuracy)
                               .FirstOrDefault() ?? _results[0];
            new AimTrainerResultWindow(best, isFullSession: true) { Owner = this }.ShowDialog();
        }

        // ── First Drill launch ───────────────────────────────────────────────

        private void StartFirstDrill()
        {
            if (_profile == null) { Close(); return; }

            var (scenario, _) = DiagnosticAssessmentService.GetRecommendedStartingScenario(_profile);
            var win = new AimTrainerWindow();
            win.PreSelectScenario(scenario, "Medium");
            win.Show();
            Close();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void UpdateTimerText()
        {
            TimerText.Text = $"0:{Math.Max(0, _secondsLeft):D2}";
            TimerText.Foreground = _secondsLeft <= 5
                ? new SolidColorBrush(Colors.OrangeRed)
                : (Brush)FindResource("AccentBrush");
        }

        private void UpdateLiveStats()
        {
            if (_scenario == null) return;
            int hits = _scenario.Hits, misses = _scenario.Misses, total = hits + misses;
            LiveHitsText.Text  = hits.ToString();
            LiveAccText.Text   = total > 0 ? $"{hits * 100.0 / total:F0}%" : "--";
            LiveReactText.Text = _scenario.AvgReactionMs > 0 ? $"{_scenario.AvgReactionMs:F0}ms" : "--";
        }

        private void StopTimers()
        {
            _gameTimer.Stop();
            _updateTimer.Stop();
        }

        private static Brush ScoreColor(double score)
        {
            if (score >= 75) return new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xA0));
            if (score >= 50) return new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF));
            if (score >= 30) return new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x47));
            return new SolidColorBrush(Color.FromRgb(0xFF, 0x60, 0x60));
        }

        protected override void OnClosed(EventArgs e)
        {
            StopTimers();
            _scenario?.Stop(TestCanvas);
            base.OnClosed(e);
        }
    }
}
