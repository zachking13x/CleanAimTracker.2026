using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CleanAimTracker.Services
{
    /// <summary>One onboarding step's local record: how many runs reached it, and when.</summary>
    public class FunnelEntry
    {
        public int      Count    { get; set; }
        public DateTime FirstUtc { get; set; }
        public DateTime LastUtc  { get; set; }
        public string?  Detail   { get; set; }   // e.g. which step a skip happened from
    }

    /// <summary>
    /// CAT_FIRST_FUN_AND_FUNNEL Gate 1: LOCAL-ONLY onboarding funnel. Records how far each
    /// run gets through first-open so drop-off can be WATCHED on machines we control —
    /// never transmitted. Pure file IO to the same local folder as the rest of the user's
    /// data; there is deliberately no network code of any kind in this file (privacy gate).
    /// </summary>
    public static class OnboardingFunnelService
    {
        // ── Canonical step names (also fixes the report ordering) ──────────────
        public const string AppFirstOpen          = "app_first_open";
        public const string OnboardingShown       = "onboarding_shown";
        public const string WarmupStarted         = "warmup_started";
        public const string WarmupCompleted       = "warmup_completed";
        public const string CalibrationOfferedAfterWarmup   = "calibration_offered_after_warmup";
        public const string CalibrationAcceptedAfterWarmup  = "calibration_accepted_after_warmup";
        public const string CalibrationDeclined   = "calibration_declined";
        public const string CalibrationStarted    = "calibration_started";
        public const string CalibrationTest1Done  = "calibration_test_1_done";
        public const string CalibrationTest2Done  = "calibration_test_2_done";
        public const string CalibrationTest3Done  = "calibration_test_3_done";
        public const string CalibrationTest4Done  = "calibration_test_4_done";
        public const string BaselineRevealed      = "baseline_revealed";
        public const string FirstRealDrillStarted = "first_real_drill_started";
        public const string FirstSessionCompleted = "first_session_completed";
        public const string OnboardingSkipped     = "onboarding_skipped";
        // CAT_CALIBRATION_AS_GAME: scored-rounds reframe.
        public const string Round1Done            = "round_1_done";
        public const string Round2Done            = "round_2_done";
        public const string Round3Done            = "round_3_done";
        public const string Round4Done            = "round_4_done";
        public const string SkillScoreRevealed    = "skill_score_revealed";
        public const string FirstPostCalibrationDrill = "first_post_calibration_drill";

        /// <summary>The order steps are listed in the dev report (the intended funnel path).</summary>
        public static readonly string[] StepOrder =
        {
            AppFirstOpen, OnboardingShown, WarmupStarted, WarmupCompleted,
            CalibrationOfferedAfterWarmup, CalibrationAcceptedAfterWarmup, CalibrationDeclined,
            CalibrationStarted,
            Round1Done, Round2Done, Round3Done, Round4Done, SkillScoreRevealed,
            CalibrationTest1Done, CalibrationTest2Done, CalibrationTest3Done,
            CalibrationTest4Done, BaselineRevealed, FirstPostCalibrationDrill,
            FirstRealDrillStarted, FirstSessionCompleted, OnboardingSkipped
        };

        private static readonly string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CleanAimTracker", "onboarding_funnel.json");

        /// <summary>
        /// Record that a run reached <paramref name="step"/>. Fully guarded — logging must
        /// never block the UI or throw. <paramref name="detail"/> carries context such as
        /// the step a skip happened from.
        /// </summary>
        public static void Record(string step, string? detail = null)
        {
            try
            {
                var data = Load();
                Apply(data, step, detail, DateTime.UtcNow);
                Save(data);
            }
            catch { /* never let instrumentation break onboarding */ }
        }

        /// <summary>Pure update — increments the count, stamps first/last seen. Unit-tested.</summary>
        public static void Apply(Dictionary<string, FunnelEntry> data, string step, string? detail, DateTime nowUtc)
        {
            if (data == null || string.IsNullOrWhiteSpace(step)) return;
            if (!data.TryGetValue(step, out var e))
            {
                e = new FunnelEntry { FirstUtc = nowUtc };
                data[step] = e;
            }
            e.Count++;
            e.LastUtc = nowUtc;
            if (!string.IsNullOrEmpty(detail)) e.Detail = detail;
        }

        public static Dictionary<string, FunnelEntry> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                return JsonSerializer.Deserialize<Dictionary<string, FunnelEntry>>(File.ReadAllText(FilePath))
                       ?? new();
            }
            catch { return new(); }
        }

        private static void Save(Dictionary<string, FunnelEntry> data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, FilePath, overwrite: true);   // atomic, same pattern as SettingsService
        }

        /// <summary>Human-readable funnel for the dev/tester view (T1.2). Local read only.</summary>
        public static string FormatReport(Dictionary<string, FunnelEntry>? data = null)
        {
            data ??= Load();
            var sb = new StringBuilder();
            sb.AppendLine("ONBOARDING FUNNEL (this install — local only, never sent)");
            sb.AppendLine("------------------------------------------------------------");
            foreach (var step in StepOrder)
            {
                if (data.TryGetValue(step, out var e))
                {
                    sb.Append($"✓ {step,-34} ×{e.Count}  {e.FirstUtc.ToLocalTime():MM-dd HH:mm}");
                    if (!string.IsNullOrEmpty(e.Detail)) sb.Append($"  [{e.Detail}]");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"·   {step,-34} —");   // never reached
                }
            }
            // Any non-canonical steps recorded (forward-compat)
            foreach (var kv in data.Where(k => !StepOrder.Contains(k.Key)))
                sb.AppendLine($"?   {kv.Key,-34} ×{kv.Value.Count}");
            return sb.ToString();
        }
    }
}
