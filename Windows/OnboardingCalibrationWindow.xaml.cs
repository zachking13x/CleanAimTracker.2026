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
        // CAT_CALIBRATION_AS_GAME: the calibration IS the fun — four scored rounds you chase,
        // not a clinical test. Brief = the "score your aim" intro; Calibration = the rounds;
        // Insight = the skill-score reveal. Same data captured, reframed as play.
        private enum FlowState { Brief, Calibration, Insight, FirstDrill }
        private FlowState _state = FlowState.Brief;

        private int  _currentTestIndex;
        private int  _secondsLeft;
        private bool _isTestRunning;
        private int  _roundStreak;          // live combo within the current round
        private bool _betweenRounds;        // showing a round's score before the next starts

        // CAT_GAME_FEEL: 3-2-1 count-in before each round. Also guarantees the
        // canvas has a layout pass before Start() reads ActualWidth — without it,
        // round 1 spawned all six targets stacked at (0,0) on the same tick the
        // calibration page became visible (T13 spawn-stack bug).
        private bool _isCountingDown;
        private int  _countdownValue;
        private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
        private int  _cumulativeScore;      // T15: skill-bar payout between rounds

        private IAimScenario? _scenario;
        private readonly Random _rng = new();
        private readonly List<AimTrainerResult> _results = new();
        private DiagnosticProfile? _profile;

        private readonly DispatcherTimer _gameTimer   = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };

        public OnboardingCalibrationWindow()
        {
            InitializeComponent();
            _gameTimer.Tick      += GameTimer_Tick;
            _updateTimer.Tick    += UpdateTimer_Tick;
            _countdownTimer.Tick += CountdownTimer_Tick;
            MuteBtn.Content = SettingsService.Load().SoundEnabled ? "🔊" : "🔇";
            BuildBriefList();
            ApplyState();
        }

        // ── State machine ────────────────────────────────────────────────────

        private void ApplyState()
        {
            PageWelcome.Visibility     = Visibility.Collapsed;   // reframed into PageBrief
            PageOffer.Visibility       = Visibility.Collapsed;   // warm-up patch removed
            PageBrief.Visibility       = _state == FlowState.Brief       ? Visibility.Visible : Visibility.Collapsed;
            PageCalibration.Visibility = _state == FlowState.Calibration ? Visibility.Visible : Visibility.Collapsed;
            PageInsight.Visibility     = _state == FlowState.Insight     ? Visibility.Visible : Visibility.Collapsed;
            PageFirstDrill.Visibility  = _state == FlowState.FirstDrill  ? Visibility.Visible : Visibility.Collapsed;

            int rounds = DiagnosticAssessmentService.CalibrationTests.Count;
            (PrimaryBtn.Content, StepIndicator.Text) = _state switch
            {
                FlowState.Brief       => ((object)"Start Round 1", $"{rounds} quick rounds — beat your scores"),
                FlowState.Calibration => ("Skip this round", $"Round {_currentTestIndex + 1} of {rounds}"),
                FlowState.Insight     => ("See my first drill", "Your aim, scored"),
                FlowState.FirstDrill  => ("Start this drill", "Your first drill"),
                _                     => ("Continue", "")
            };

            // T16: during a live round the accent button disappears entirely —
            // "Skip this round" is DEMOTED to the muted footer link. The accent
            // button only returns between rounds as "Next round →".
            PrimaryBtn.Visibility = _state == FlowState.Calibration
                ? Visibility.Collapsed : Visibility.Visible;

            // Once calibration data exists, leaving is "finish later", not "skip".
            SkipBtn.Content    = _state switch
            {
                FlowState.Calibration                     => "Skip this round",
                FlowState.Insight or FlowState.FirstDrill => "Take me to the app",
                _                                         => "Skip",
            };
            SkipBtn.Visibility = Visibility.Visible;
        }

        private void Primary_Click(object sender, RoutedEventArgs e)
        {
            switch (_state)
            {
                case FlowState.Brief:
                    _state = FlowState.Calibration;
                    _currentTestIndex = 0;
                    _results.Clear();
                    OnboardingFunnelService.Record(OnboardingFunnelService.CalibrationStarted);
                    ApplyState();
                    BeginTest(0);
                    break;

                case FlowState.Calibration:
                    // T16: PrimaryBtn is only visible between rounds ("Next round →").
                    if (_betweenRounds) { _betweenRounds = false; BeginTest(_currentTestIndex); }
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

        /// <summary>
        /// T16: the muted footer link. During a live round it skips the ROUND
        /// (demoted from the old accent-button placement); everywhere else it is
        /// the original whole-flow skip / "take me to the app".
        /// </summary>
        private void SkipSecondary_Click(object sender, RoutedEventArgs e)
        {
            if (_state == FlowState.Calibration && (_isTestRunning || _isCountingDown))
            {
                if (_isCountingDown)
                {
                    _countdownTimer.Stop();
                    _isCountingDown = false;
                    RoundCountdownText.Visibility = Visibility.Collapsed;
                }
                FinishCurrentTest();
                return;
            }
            Skip_Click(sender, e);
        }

        private void MuteBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.Load();
            settings.SoundEnabled = !settings.SoundEnabled;
            SettingsService.Save(settings);
            SoundService.SetEnabled(settings.SoundEnabled);
            MuteBtn.Content = settings.SoundEnabled ? "🔊" : "🔇";
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            StopTimers();
            var settings = SettingsService.Load();
            if (_profile != null)
                settings.CalibrationComplete = true;   // they finished — leaving from insight/drill is not a skip
            else
            {
                settings.OnboardingSkipped = true;
                // T1.1: a genuine skip — record WHICH step they bailed from.
                OnboardingFunnelService.Record(OnboardingFunnelService.OnboardingSkipped,
                    detail: _state.ToString());
            }
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

            // CAT_CALIBRATION_AS_GAME: round framing, no "test" language.
            TestLabel.Text = $"Round {index + 1}: {RoundName(index)}";
            TestDesc.Text  = test.Description;
            LiveReactLabel.Text = ReactionMetric.IsTrueReaction(test.Scenario)
                ? "REACTION " : "TIME/TARGET ";
            StepIndicator.Text = $"Round {index + 1} of {DiagnosticAssessmentService.CalibrationTests.Count}";

            _roundStreak   = 0;
            _betweenRounds = false;
            ComboText.Visibility     = Visibility.Collapsed;
            SkillBarPanel.Visibility = Visibility.Collapsed;

            // T16: accent button stays hidden while the round runs — the muted
            // footer link is the only skip. "Next round →" brings it back.
            PrimaryBtn.Visibility = Visibility.Collapsed;
            SkipBtn.Content       = "Skip this round";
            SkipBtn.Visibility    = Visibility.Visible;

            _scenario = CreateScenario(test);
            TestCanvas.Children.Clear();
            _secondsLeft = test.DurationSeconds;
            UpdateTimerText();

            // CAT_GAME_FEEL: 3-2-1 count-in — play-first pacing, and the canvas is
            // guaranteed laid out before Start() reads ActualWidth (T13 fix).
            _isCountingDown = true;
            _countdownValue = 3;
            RoundCountdownText.Text = "3";
            RoundCountdownText.Visibility = Visibility.Visible;
            PulseRoundCountdown();
            _countdownTimer.Start();
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            _countdownValue--;
            if (_countdownValue >= 1)
            {
                RoundCountdownText.Text = _countdownValue.ToString();
                PulseRoundCountdown();
                return;
            }

            _countdownTimer.Stop();
            RoundCountdownText.Visibility = Visibility.Collapsed;
            _isCountingDown = false;

            if (_scenario == null) return;   // round was skipped mid-countdown

            TestCanvas.UpdateLayout();       // belt + suspenders for the T13 spawn-stack bug
            _scenario.Start(TestCanvas, targetSize: 36, moveSpeed: 2.5, _rng);

            _isTestRunning = true;
            UpdateLiveStats();
            _gameTimer.Start();
            _updateTimer.Start();
        }

        private void PulseRoundCountdown()
        {
            var pop = new System.Windows.Media.Animation.DoubleAnimation(1.5, 1.0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
            RoundCountdownScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, pop);
            RoundCountdownScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, pop);
        }

        private static readonly string[] RoundNames = { "Clicking", "Tracking", "Switching", "Reaction" };
        private static string RoundName(int index) => index >= 0 && index < RoundNames.Length ? RoundNames[index] : "Aim";

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

            int roundScore = _results.Count > 0 ? _results[^1].Score : 0;

            // T1.1 + T2.2: record this round's completion (both the legacy and round names).
            OnboardingFunnelService.Record(_currentTestIndex switch
            {
                0 => OnboardingFunnelService.CalibrationTest1Done,
                1 => OnboardingFunnelService.CalibrationTest2Done,
                2 => OnboardingFunnelService.CalibrationTest3Done,
                _ => OnboardingFunnelService.CalibrationTest4Done,
            });
            OnboardingFunnelService.Record(_currentTestIndex switch
            {
                0 => OnboardingFunnelService.Round1Done,
                1 => OnboardingFunnelService.Round2Done,
                2 => OnboardingFunnelService.Round3Done,
                _ => OnboardingFunnelService.Round4Done,
            });

            _currentTestIndex++;
            _cumulativeScore += roundScore;

            if (_currentTestIndex < DiagnosticAssessmentService.CalibrationTests.Count)
            {
                // T1.1: score the instant the round ends, and invite the next as momentum.
                _betweenRounds = true;
                TestCanvas.Children.Clear();
                TestLabel.Text   = $"Round {_currentTestIndex}: {roundScore:N0}";
                ScoreText.Text   = roundScore.ToString("N0");
                ComboText.Visibility = Visibility.Collapsed;

                // T16: coach teaser — real numbers from the round just played,
                // hinting at the report that's coming.
                var justPlayed = _results[^1];
                TestDesc.Text = justPlayed.Hits + justPlayed.Misses > 0
                    ? $"Coach logged it: {justPlayed.Accuracy:F0}% accuracy, best streak ×{justPlayed.MaxStreak}. Next up: {RoundName(_currentTestIndex)}."
                    : $"Next up: {RoundName(_currentTestIndex)}.";

                // T15: the skill bar fills a quarter per round — visible progress
                // toward the aim score from minute one.
                int totalRounds = DiagnosticAssessmentService.CalibrationTests.Count;
                SkillBarPanel.Visibility = Visibility.Visible;
                SkillBarScoreText.Text   = _cumulativeScore.ToString("N0");
                SkillBarRoundsText.Text  = $"{_currentTestIndex} of {totalRounds} rounds banked";
                var fill = new System.Windows.Media.Animation.DoubleAnimation(
                    SkillBarFill.Width is double w && !double.IsNaN(w) ? w : 0,
                    380.0 * _currentTestIndex / totalRounds,
                    TimeSpan.FromMilliseconds(500))
                { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
                SkillBarFill.BeginAnimation(WidthProperty, fill);

                // T16: the accent button returns as forward momentum, skip hides.
                PrimaryBtn.Content    = $"Next round: {RoundName(_currentTestIndex)}  →";
                PrimaryBtn.Visibility = Visibility.Visible;
                SkipBtn.Visibility    = Visibility.Collapsed;
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
            var pos = e.GetPosition(TestCanvas);
            bool hit = _scenario.HandleClick(pos);

            // CAT_VISUAL_JUICE: full sound-off-friendly feedback (ring + hitmarker + floating
            // score) on hit; clear muted-red ✕ on miss; live combo.
            if (hit)
            {
                SoundService.PlayHit();
                Brush accent = TryFindResource("AccentBrush") as Brush
                               ?? new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF));
                HitFeedback.Hit(TestCanvas, pos, accent, points: 100);   // each calibration hit = 100

                _roundStreak++;
                if (_roundStreak >= 2)
                {
                    ComboText.Text = $"×{_roundStreak}";
                    ComboText.Visibility = Visibility.Visible;
                }
            }
            else
            {
                SoundService.PlayMiss();
                HitFeedback.Miss(TestCanvas, pos);
                _roundStreak = 0;
                ComboText.Visibility = Visibility.Collapsed;
            }
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
            _betweenRounds = false;
            OnboardingFunnelService.Record(OnboardingFunnelService.BaselineRevealed);
            OnboardingFunnelService.Record(OnboardingFunnelService.SkillScoreRevealed);

            // T2.1: the headline "aim score" — the climax of the run + the number to beat.
            int skillScore = _results.Sum(r => r.Score);

            // Build + persist the calibration baseline.
            var settings = SettingsService.Load();
            _profile = DiagnosticAssessmentService.BuildCalibrationProfile(
                _results, settings.DiagnosticHistory.Count + 1);
            settings.DiagnosticHistory.Add(_profile);
            settings.CalibrationComplete = true;
            settings.FirstLaunchComplete = true;
            settings.BestSkillScore = Math.Max(settings.BestSkillScore, skillScore);  // stored as the thing to beat
            SettingsService.Save(settings);

            SkillScoreText.Text = skillScore.ToString("N0");

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
            OnboardingFunnelService.Record(OnboardingFunnelService.FirstPostCalibrationDrill);
            OnboardingFunnelService.Record(OnboardingFunnelService.FirstRealDrillStarted);

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
            ScoreText.Text     = (hits * 100).ToString("N0");   // live score (matches the round Score)
        }

        private void StopTimers()
        {
            _gameTimer.Stop();
            _updateTimer.Stop();
            _countdownTimer.Stop();
            _isCountingDown = false;
            RoundCountdownText.Visibility = Visibility.Collapsed;
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
