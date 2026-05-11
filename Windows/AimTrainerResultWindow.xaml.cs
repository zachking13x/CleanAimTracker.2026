using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CleanAimTracker.Windows
{
    public partial class AimTrainerResultWindow : Window
    {
        private readonly AimTrainerResult _result;
        private readonly bool _isReplay;

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

        // ── Populate stats immediately ────────────────────────────────
        private void PopulateStats(AimTrainerResult r)
        {
            ScoreText.Text        = r.Score.ToString("N0");
            ScenarioBadgeText.Text = $"{r.Scenario}  •  {r.Difficulty}  •  {r.DurationSeconds}s";

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
                var history = await Task.Run(() => AimTrainerStorage.LoadAll());
                var report  = await Task.Run(() => AiCoachService.Analyze(result, history));
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
            => Close();
    }
}
