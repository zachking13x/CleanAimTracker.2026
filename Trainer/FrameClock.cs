using System;

namespace CleanAimTracker.Trainer
{
    /// <summary>
    /// CAT_FRAME_LOCK: converts real frame time into "ticks" so scenario motion keeps the
    /// per-tick tuning it was authored with while running smoothly on any display.
    ///
    /// The trainer used to step scenarios from a 16ms DispatcherTimer. Measured on Windows'
    /// default 15.6ms timer resolution, that timer really fires ~40x/s, alternating ~15ms and
    /// ~30ms gaps, so targets moved in uneven jumps. Worse, if another app raised the system
    /// timer resolution it ticked ~62x/s and every target ran ~56% faster. Every velocity in
    /// the scenarios is "pixels per tick" at that measured 25ms average, so scaling by
    /// realDt / 25ms keeps the speed players trained on, now independent of the timer.
    /// </summary>
    public static class FrameClock
    {
        /// <summary>The average tick interval every existing per-tick velocity was felt at.</summary>
        public const double NominalTickMs = 25.0;

        /// <summary>
        /// A hitch (window drag, GC, alt-tab) must not teleport targets across the screen.
        /// Beyond this one frame just runs slow.
        /// </summary>
        public const double MaxFrameMs = 100.0;

        /// <summary>This frame's length in nominal ticks. 1.0 = one old 25ms tick.</summary>
        public static double Scale { get; private set; } = 1.0;

        public static void Advance(double frameMs)
            => Scale = Math.Clamp(frameMs, 0, MaxFrameMs) / NominalTickMs;

        public static void Reset() => Scale = 1.0;

        /// <summary>
        /// A per-tick blend factor (x += (target - x) * rate) converted to this frame, so the
        /// blend per SECOND is unchanged. Multiplying the rate by Scale would overshoot past 1.
        /// </summary>
        public static double PerTick(double rate)
            => 1.0 - Math.Pow(1.0 - Math.Clamp(rate, 0, 1), Scale);
    }
}
