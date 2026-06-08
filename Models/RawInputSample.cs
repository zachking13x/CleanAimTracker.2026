namespace CleanAimTracker.Models
{
    /// <summary>
    /// Lightweight value type that captures one hardware mouse-movement event
    /// during an aim trainer drill.  Using a struct (rather than a class)
    /// prevents GC pressure from the thousands of allocations that a high
    /// polling-rate mouse produces per session.
    /// </summary>
    public readonly struct RawInputSample
    {
        public readonly int  Dx;
        public readonly int  Dy;
        public readonly long Timestamp;   // Stopwatch.GetTimestamp() ticks

        public RawInputSample(int dx, int dy, long ts)
        {
            Dx        = dx;
            Dy        = dy;
            Timestamp = ts;
        }
    }
}
