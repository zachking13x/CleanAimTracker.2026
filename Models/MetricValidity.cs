namespace CleanAimTracker.Models
{
    /// <summary>Why a metric is not valid for coaching/trend use.</summary>
    public enum MetricInvalidReason
    {
        None = 0,               // metric is valid
        InsufficientSamples,    // too few raw-input events to be statistically meaningful
        DegenerateInput,        // NaN/Infinity intermediate, or input that cannot produce the metric
        CaptureGap,             // capture stream lost events (e.g. focus loss in fullscreen-exclusive)
        NotComputed             // metric was never computed this session (e.g. legacy data)
    }

    /// <summary>
    /// TASK-1.2: per-metric validity attached to the session diagnostics result.
    /// A metric with IsValid=false must never be narrated, trended, or averaged —
    /// it renders as "—", not as 0.
    /// </summary>
    public class MetricValidity
    {
        public bool IsValid { get; set; } = false;
        public int SampleCount { get; set; } = 0;
        public MetricInvalidReason InvalidReason { get; set; } = MetricInvalidReason.NotComputed;

        public static MetricValidity Valid(int sampleCount) => new()
        {
            IsValid       = true,
            SampleCount   = sampleCount,
            InvalidReason = MetricInvalidReason.None
        };

        public static MetricValidity Invalid(MetricInvalidReason reason, int sampleCount) => new()
        {
            IsValid       = false,
            SampleCount   = sampleCount,
            InvalidReason = reason
        };
    }
}
