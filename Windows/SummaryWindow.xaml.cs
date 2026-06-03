using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Linq;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class SummaryWindow : Window
    {
        private readonly SessionSummary _s;
        private readonly SensitivityRecommendation _rec;
        private readonly StreakService.StreakResult? _streak;

        public SummaryWindow(SessionSummary s, SensitivityRecommendation rec,
                              StreakService.StreakResult? streak = null)
        {
            InitializeComponent();
            _s      = s;
            _rec    = rec;
            _streak = streak;

            LoadRecommendedSettings();
            LoadSummary();
            LoadComparison();
            LoadStreakMessage();   // TASK-16
            LoadTrackerCoach();
        }

        // ─────────────────────────────────────────────────────────────
        // Recommended Settings Section
        // ─────────────────────────────────────────────────────────────
        private void LoadRecommendedSettings()
        {
            // Sensitivity
            RecommendedSensitivityText.Text =
                $"Recommended Sensitivity: {_rec.RecommendedSensitivity:F4}";

            RecommendedSensitivityRangeText.Text =
                $"Range: {_rec.RecommendedSensitivityMin:F4} – {_rec.RecommendedSensitivityMax:F4}";

            // DPI
            RecommendedDpiText.Text =
                $"Recommended DPI: {_rec.RecommendedDPI} DPI";

            // cm/360
            RecommendedCm360Text.Text =
                $"Recommended cm/360: {_rec.RecommendedCm360:F1} cm";

            RecommendedCm360RangeText.Text =
                $"Range: {_rec.Cm360RangeMin:F1} – {_rec.Cm360RangeMax:F1} cm";

            // TASK-03: guard against uninitialized/invalid cm/360 values (< 1.0 or > 500)
            // A value below 1.0 means sensitivity has not been entered or calculated yet.
            // Showing a red "below recommended range" warning in that case is misleading.
            bool cmIsValid = _s.CmPer360 >= 1.0 && _s.CmPer360 <= 500.0;

            if (!cmIsValid)
            {
                // No real sensitivity data — show a muted prompt, not a warning
                RecommendedCm360WarningText.Text = "Enter your DPI and sensitivity in the Sensitivity screen to get a personalized recommendation.";
                RecommendedCm360WarningText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88));
                RecommendedCm360WarningText.FontWeight = System.Windows.FontWeights.Normal;
            }
            else if (_s.CmPer360 < _rec.Cm360RangeMin)
            {
                RecommendedCm360WarningText.Text =
                    $"Your current cm/360 ({_s.CmPer360:F1}) is below the recommended range.";
            }
            else if (_s.CmPer360 > _rec.Cm360RangeMax)
            {
                RecommendedCm360WarningText.Text =
                    $"Your current cm/360 ({_s.CmPer360:F1}) is above the recommended range.";
            }
            else
            {
                RecommendedCm360WarningText.Text = "";
            }

            // Confidence
            ConfidenceText.Text =
                $"Confidence: {_rec.Confidence}%";

            // Explanation
            ExplanationText.Text = _rec.Explanation;

            // Only replace explanation when there's a real out-of-range warning (not the data-missing prompt)
            bool hasWarning = cmIsValid && !string.IsNullOrEmpty(RecommendedCm360WarningText.Text);
            if (hasWarning && (_rec.Explanation.Contains("well-balanced") || _rec.Explanation.Contains("fine-tune")))
            {
                ExplanationText.Text = _rec.Explanation.Replace(
                    "Your aim profile is well‑balanced. The recommended settings fine‑tune your control without disrupting muscle memory.",
                    "Your movement fundamentals are solid — but your cm/360 is outside the recommended range. See the warning above.");
                // Also handle the non-breaking hyphen variant
                ExplanationText.Text = ExplanationText.Text.Replace(
                    "Your aim profile is well-balanced. The recommended settings fine-tune your control without disrupting muscle memory.",
                    "Your movement fundamentals are solid — but your cm/360 is outside the recommended range. See the warning above.");
            }

            // Tips
            if (_rec.Tips != null && _rec.Tips.Count > 0)
            {
                TipsText.Text = string.Join("\n• ", _rec.Tips)
                    .Insert(0, "• ");
            }
            else
            {
                TipsText.Text = "No tips available.";
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Core Stats Section
        // ─────────────────────────────────────────────────────────────
        private void LoadSummary()
        {
            // Core stats
            TimeText.Text = $"Time: {_s.SessionSeconds:F1} s";
            DistanceText.Text = $"Distance: {_s.TotalDistanceCm:F1} cm";
            AvgSpeedText.Text = $"Avg Speed: {_s.AverageVelocity:F1} cm/s";
            PeakSpeedText.Text = $"Peak Speed: {_s.PeakVelocity:F1} cm/s";

            // Flicks
            FlicksText.Text = $"Flicks: {_s.FlickCount}";
            SmallFlicksText.Text = $"Small Flicks: {_s.SmallFlickCount}";
            LargeFlicksText.Text = $"Large Flicks: {_s.LargeFlickCount}";

            // Diagnostics
            SmoothnessText.Text = $"Smoothness: {_s.SmoothnessScore:F0}";
            SharpnessText.Text = $"Correction Sharpness: {_s.CorrectionSharpness:F0}";
            ConsistencyText.Text = $"Consistency: {_s.MovementConsistency:F0}";
            QualityText.Text = $"Overall Quality: {_s.OverallQualityScore:F0}";

            // Advanced diagnostics (from engine)
            CorrectionSharpnessText.Text = $"Sharpness Score: {_rec.CorrectionSharpnessScore:F0}";
            VelocityStabilityText.Text = $"Velocity Stability: {_rec.VelocityStabilityScore:F0}";
            IdlePenaltyText.Text = $"Idle Penalty: {_rec.IdlePenaltyScore:F0}";
            OverallDiagnosticText.Text = $"Weighted Diagnostic: {_rec.OverallDiagnostic:F0}";

            // Trend
            TrendText.Text = _rec.TrendSummary;
        }

        private void LoadComparison()
        {
            try
            {
                var history = SessionStorage.LoadAll();
                // Exclude current session (it was just saved before this window opened)
                var previous = history
                    .Where(h => h.Timestamp < _s.Timestamp)
                    .OrderByDescending(h => h.Timestamp)
                    .ToList();

                if (previous.Count == 0)
                {
                    QualityCompareText.Text  = "This is your first session — this is your baseline.";
                    VelocityCompareText.Text = "";
                    BaselineText.Text        = "Complete more sessions to see your progress over time.";
                    return;
                }

                // Personal best quality
                double pbQuality   = previous.Max(h => h.OverallQualityScore);
                double lastQuality = previous[0].OverallQualityScore;
                double qualityDelta = _s.OverallQualityScore - lastQuality;

                string qualityDeltaStr = qualityDelta >= 0
                    ? $"+{qualityDelta:F0} vs last"
                    : $"{qualityDelta:F0} vs last";

                QualityCompareText.Text = $"Quality:  {_s.OverallQualityScore:F0}  ({qualityDeltaStr})  •  PB: {pbQuality:F0}";

                // TASK-27: Surface the personal best moment
                if (_s.OverallQualityScore > pbQuality)
                {
                    PersonalBestBanner.Visibility = Visibility.Visible;
                    double margin = _s.OverallQualityScore - pbQuality;
                    PersonalBestScoreText.Text =
                        $"Quality {_s.OverallQualityScore:F0}/100 — beats your previous best " +
                        $"({pbQuality:F0}) by {margin:F0} pts";
                }

                // Velocity delta
                double lastVel  = previous[0].AverageVelocity;
                double velDelta = _s.AverageVelocity - lastVel;
                string velDeltaStr = velDelta >= 0 ? $"+{velDelta:F1}" : $"{velDelta:F1}";
                VelocityCompareText.Text = $"Avg Speed:  {_s.AverageVelocity:F1} cm/s  ({velDeltaStr} vs last)";

                // Since first session
                var first = previous.LastOrDefault();
                if (first != null && previous.Count >= 3)
                {
                    double sinceFirst = _s.OverallQualityScore - first.OverallQualityScore;
                    string direction  = sinceFirst >= 0 ? "up" : "down";
                    BaselineText.Text = $"{Math.Abs(sinceFirst):F0} pts {direction} since your first session  •  {previous.Count + 1} sessions total";
                }
                else
                {
                    BaselineText.Text = $"{previous.Count + 1} sessions completed";
                }
            }
            catch
            {
                QualityCompareText.Text  = "Could not load session history.";
                VelocityCompareText.Text = "";
                BaselineText.Text        = "";
            }
        }

        // TASK-16: Show streak retention message
        private void LoadStreakMessage()
        {
            if (_streak == null) return;

            StreakMessageText.Text            = _streak.Message;
            StreakMessageText.Visibility      = Visibility.Visible;
            StreakMessageBorder.Visibility    = Visibility.Visible;

            // Color: red for broken streak, gold for new best, green for active
            if (_streak.JustBrokeStreak)
                StreakMessageText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x35));
            else if (_streak.JustHitNewBest)
                StreakMessageText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00));
            else
                StreakMessageText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x00, 0xE5, 0xA0));
        }

        // TASK-27: Copy personal best to clipboard
        private void SharePb_Click(object sender, RoutedEventArgs e)
        {
            string text =
                $"🏆 New personal best in Clean Aim Tracker! " +
                $"Aim quality: {_s.OverallQualityScore:F0}/100 " +
                $"(smoothness {_s.SmoothnessScore:F0}, " +
                $"cm/360: {_s.CmPer360:F1}) " +
                $"— keep grinding! 🎯 #CleanAimTracker #AimTraining";

            System.Windows.Clipboard.SetText(text);
            SharePbBtn.Content = "✅  Copied!";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadTrackerCoach()
        {
            try
            {
                var history = SessionStorage.LoadAll()
                    .Where(h => h.Timestamp < _s.Timestamp)
                    .OrderByDescending(h => h.Timestamp)
                    .Take(10)
                    .ToList();

                // TASK-18: Build CoachMemory for cross-coach insights
                // Pass null for current AimTrainerResult — this is the tracker coach context
                var settings      = SettingsService.Load();
                var memory        = CoachMemoryBuilder.Build(null, settings);
                int trackerCount  = SessionStorage.LoadAll()?.Count ?? 0;

                // Free tracker session check
                if (FreeCoachSessionService.ShouldTriggerFreeTrackerSession(settings, trackerCount))
                {
                    FreeCoachSessionService.MarkFreeTrackerSessionUsed(settings);
                    if (FreeTrackerSessionBanner != null)
                        FreeTrackerSessionBanner.Visibility = Visibility.Visible;
                }

                var report = TrackerCoachService.Analyze(_s, history, memory);

                CoachHeadlineText.Text             = report.Headline;
                CoachObservationsList.ItemsSource  = report.Observations;
                CoachSuggestionsList.ItemsSource   = report.Suggestions;
                CoachNextDrillText.Text            = report.NextDrillSuggestion;
            }
            catch
            {
                CoachHeadlineText.Text = "Coach unavailable for this session.";
            }
        }

        // TASK-18: Start Another Session
        private void StartAnother_Click(object sender, RoutedEventArgs e)
        {
            Close();
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.ResetButton_Click(this, new RoutedEventArgs());
                main.StartButton_Click(this, new RoutedEventArgs());
            }
        }
    }
}
