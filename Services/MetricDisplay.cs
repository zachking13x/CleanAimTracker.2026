using CleanAimTracker.Models;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-4.4: display formatting for session metrics. An invalid metric
    /// renders "—", never 0 — a 0 is a measurement, a dash is an honest "we
    /// don't know".
    /// </summary>
    public static class MetricDisplay
    {
        public const string InvalidPlaceholder = "—";
        public const string InvalidTooltip = "Not enough data this session";

        /// <summary>Format a 0–100 metric value, or the placeholder when invalid.</summary>
        public static string Format(double value, MetricValidity validity) =>
            validity.IsValid ? $"{value:F0}" : InvalidPlaceholder;

        public static string Format(SessionSummary s, string metricName, double value) =>
            Format(value, s.GetValidity(metricName));
    }
}
