using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CleanAimTracker.Windows
{
    public partial class AimTrainerResultWindow : Window
    {
        private readonly AimTrainerResult _result;
        private readonly bool _isReplay;
        private List<Achievement>? _newlyUnlocked;

        /// <param name="result">The drill result to display.</param>
        /// <param name="isReplay">True when opened via "Last Report" — hides Play Again, changes title.</param>
        public AimTrainerResultWindow(AimTrainerResult result, bool isReplay = false)
        {
            InitializeComponent();
            _result   = result;
            _isReplay = isReplay;

            if (isReplay)
            {
                Title             = "Last Coaching Report";
                PlayAgainBtn.Visibility = Visibility.Collapsed;
            }

            PopulateStats(result);
            _ = LoadCoachingAsync(result);

            if (!isReplay)
            {
                _ = EvaluateAchievementsAsync(result);
                _ = LoadPersonalBestsAsync(result);
            }
        }

        /// <summary>Opens the most recent coaching report from storage, or shows a message if none exists.</summary>
        public static void OpenLastReport(Window owner)
        {
            var last = AimTrainerStorage.LoadLast();
            if (last == null)
            {
                MessageBox.Show(
                    "No coaching report yet. Complete an Aim Trainer drill to generate your first report.",
                    "No Report Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var win = new AimTrainerResultWindow(last, isReplay: true) { Owner = owner };
            win.Show();
        }

        // ── Achievement + daily challenge evaluation ──────────────────
        private async Task EvaluateAchievementsAsync(AimTrainerResult result)
        {
            try
            {
                var settings   = SettingsService.Load();
                var allResults = await Task.Run(() => AimTrainerStorage.LoadAll());

                // Daily challenge — TryComplete increments settings.ChallengesCompleted on success
                var challenge = DailyChallengeService.GetToday();
                DailyChallengeService.TryComplete(challenge, result, settings);

                _newlyUnlocked = await Task.Run(() =>
                    AchievementService.EvaluateAfterSession(
                        result,
                        allResults,
                        settings.CurrentStreak,
                        settings.ChallengesCompleted));

                // Show achievement unlock popup (never on replay)
                if (_newlyUnlocked != null && _newlyUnlocked.Count > 0 && !_isReplay)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var popup = new AchievementUnlockWindow(_newlyUnlocked)
                        {
                            Owner = Window.GetWindow(this) ?? Application.Current.MainWindow
                        };
                        popup.ShowDialog();
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Achievement evaluation failed", ex);
            }
        }

        // ── TASK-16 + TASK-17: Personal Bests + Top 5 ────────────────
        private async Task LoadPersonalBestsAsync(AimTrainerResult result)
        {
            try
            {
                var all = await Task.Run(() => AimTrainerStorage.LoadAll());

                // ── TASK-16: PB badges ────────────────────────────────────
                var same = all.Where(r => r.Scenario == result.Scenario).ToList();

                bool isBestScore    = same.Count == 0 || result.Score        >= same.Max(r => r.Score);
                bool isBestAccuracy = same.Count == 0 || result.Accuracy     >= same.Max(r => r.Accuracy);
                bool isBestStreak   = same.Count == 0 || result.MaxStreak    >= same.Max(r => r.MaxStreak);
                bool isBestReaction = result.BestReactionMs > 0 &&
                                      (same.Count == 0 ||
                                       result.BestReactionMs <= same.Where(r => r.BestReactionMs > 0)
                                                                    .Select(r => r.BestReactionMs)
                                                                    .DefaultIfEmpty(double.MaxValue)
                                                                    .Min());

                Dispatcher.Invoke(() =>
                {
                    PBBadgesPanel.Children.Clear();
                    void AddBadge(string label, string bg)
                    {
                        var b = new Border
                        {
                            Background  = new System.Windows.Media.SolidColorBrush(
                                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bg)),
                            CornerRadius = new CornerRadius(6),
                            Padding      = new Thickness(10, 4, 10, 4),
                            Margin       = new Thickness(0, 0, 8, 0),
                        };
                        b.Child = new TextBlock
                        {
                            Text       = label,
                            FontSize   = 11,
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                        };
                        PBBadgesPanel.Children.Add(b);
                    }

                    if (isBestScore)    AddBadge("🏆 New Best Score!",    "#1B5E20");
                    if (isBestAccuracy) AddBadge("🎯 Best Accuracy!",     "#0D47A1");
                    if (isBestReaction) AddBadge("⚡ Best Reaction!",     "#E65100");
                    if (isBestStreak)   AddBadge("🔥 Best Streak!",       "#4A148C");

                    PBBadgesPanel.Visibility = PBBadgesPanel.Children.Count > 0
                        ? Visibility.Visible : Visibility.Collapsed;
                });

                // ── TASK-17: Top 5 by score for this scenario ─────────────
                var top5 = all
                    .Where(r => r.Scenario == result.Scenario)
                    .OrderByDescending(r => r.Score)
                    .Take(5)
                    .Select((r, i) => new Top5Row
                    {
                        RankDisplay     = $"#{i + 1}",
                        DateDisplay     = r.Timestamp.ToString("MMM d"),
                        AccuracyDisplay = $"{r.Accuracy:F0}%",
                        ReactionDisplay = r.AvgReactionMs > 0 ? $"{r.AvgReactionMs:F0}ms" : "—",
                        ScoreDisplay    = r.Score.ToString("N0"),
                    })
                    .ToList();

                Dispatcher.Invoke(() =>
                {
                    if (top5.Count > 1)  // only show if there's more than 1 session to compare
                    {
                        Top5HeaderText.Text    = $"TOP {top5.Count} — {result.Scenario.ToUpperInvariant()}";
                        Top5List.ItemsSource   = top5;
                        Top5Panel.Visibility   = Visibility.Visible;
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.Error("Personal bests load failed", ex);
            }
        }

        private class Top5Row
        {
            public string RankDisplay     { get; set; } = "";
            public string DateDisplay     { get; set; } = "";
            public string AccuracyDisplay { get; set; } = "";
            public string ReactionDisplay { get; set; } = "";
            public string ScoreDisplay    { get; set; } = "";
        }

        // ── Populate stats immediately ────────────────────────────────
        private void PopulateStats(AimTrainerResult r)
        {
            ScoreText.Text        = r.Score.ToString("N0");
            string variantPart = string.IsNullOrEmpty(r.SubVariant) ? "" : $"  •  {r.SubVariant}";
            ScenarioBadgeText.Text = $"{r.Scenario}{variantPart}  •  {r.Difficulty}  •  {r.DurationSeconds}s";

            AccuracyText.Text    = $"{r.Accuracy:F0}%";
            AvgReactionText.Text = $"{r.AvgReactionMs:F0}ms";
            BestReactionText.Text = $"{r.BestReactionMs:F0}ms";
            StreakText.Text      = r.MaxStreak.ToString();

            HitsText.Text   = r.Hits.ToString();
            MissesText.Text = r.Misses.ToString();

            Loaded += (_, _) =>
            {
                double barWidth = (AccuracyBar.Parent as Border)?.ActualWidth ?? 300;
                AccuracyBar.Width = Math.Max(0, barWidth * (r.Accuracy / 100.0));
            };

            AccuracyText.Foreground = r.Accuracy >= 80
                ? System.Windows.Media.Brushes.LightGreen
                : r.Accuracy >= 60
                    ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                    : System.Windows.Media.Brushes.OrangeRed;
        }

        // ── Load AI coaching asynchronously ───────────────────────────
        private async Task LoadCoachingAsync(AimTrainerResult result)
        {
            CoachingLoading.Visibility = Visibility.Visible;
            CoachingContent.Visibility = Visibility.Collapsed;
            CoachingError.Visibility   = Visibility.Collapsed;

            try
            {
                var history       = await Task.Run(() => AimTrainerStorage.LoadAll());
                var recentTracker = await Task.Run(() => SessionStorage.LoadAll()
                                        ?.OrderByDescending(s => s.Timestamp)
                                        .FirstOrDefault(s => s.SessionSeconds >= 45));
                var report        = await Task.Run(() => AiCoachService.Analyze(result, history, recentTracker));
                PopulateCoaching(report);
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to load AI coaching", ex);
                CoachingLoading.Visibility = Visibility.Collapsed;
                CoachingError.Visibility   = Visibility.Visible;
            }
        }

        // ── Populate coaching panel ───────────────────────────────────
        private void PopulateCoaching(AiCoachReport report)
        {
            CoachingLoading.Visibility = Visibility.Collapsed;
            CoachingError.Visibility   = Visibility.Collapsed;
            CoachingContent.Visibility = Visibility.Visible;

            RatingText.Text   = report.OverallRating;
            HeadlineText.Text = report.Headline;

            RatingBadge.Background = report.OverallRating switch
            {
                "Excellent" => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 76, 175, 80)),
                "Great"     => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 0, 212, 255)),
                "Good"      => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 0, 212, 255)),
                "Developing"=> new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 255, 170, 68)),
                _           => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 211, 47, 47)),
            };

            RatingText.Foreground = report.OverallRating switch
            {
                "Excellent" => System.Windows.Media.Brushes.LightGreen,
                "Great"     => (System.Windows.Media.Brush)FindResource("AccentBrush"),
                "Good"      => (System.Windows.Media.Brush)FindResource("AccentBrush"),
                "Developing"=> System.Windows.Media.Brushes.Orange,
                _           => System.Windows.Media.Brushes.OrangeRed,
            };

            StrengthsList.ItemsSource  = report.Strengths;
            WeaknessesList.ItemsSource = report.Weaknesses;
            AdviceList.ItemsSource     = report.Advice;

            NextDrillText.Text    = report.NextDrillSuggestion;
            MotivationalText.Text = report.MotivationalClose;
        }

        // ── Buttons ───────────────────────────────────────────────────
        private void PlayAgain_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is AimTrainerWindow trainer)
            {
                Close();
                trainer.StartNewDrill();
            }
            else
            {
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Skip prompt when opened as a replay from Last Report
            if (_isReplay) { Close(); return; }

            var response = MessageBox.Show(
                "Come back tomorrow — 3 sessions builds a reliable trend.\n\nSchedule a reminder?",
                "See You Tomorrow",
                MessageBoxButton.YesNo,
                MessageBoxImage.None);

            if (response == MessageBoxResult.Yes)
                ToastService.ScheduleTomorrowReminder();

            Close();
        }
    }
}
