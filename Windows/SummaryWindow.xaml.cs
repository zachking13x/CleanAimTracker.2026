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

            // Warning if user is outside recommended range
            if (_s.CmPer360 < _rec.Cm360RangeMin)
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

            // If there's an active warning, ensure explanation acknowledges it
            bool hasWarning = !string.IsNullOrEmpty(RecommendedCm360WarningText.Text);
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
                StreakMessageText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            else if (_streak.JustHitNewBest)
                StreakMessageText.Foreground = System.Windows.Media.Brushes.Gold;
            else
                StreakMessageText.Foreground = System.Windows.Media.Brushes.LightGreen;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
