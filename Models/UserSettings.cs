namespace CleanAimTracker.Models
{
    public class UserSettings
    {
        // General settings
        public int DPI { get; set; } = 800;
        public double Sensitivity { get; set; } = 0.5;

        // Theme system
        public string Theme { get; set; } = "Dark";
        public string ThemeMode { get; set; } = "Dark";

        // First launch system
        public bool FirstLaunchComplete { get; set; } = false;
        public DateTime FirstLaunchDate { get; set; } = DateTime.MinValue;
        public bool OnboardingAutoStart { get; set; } = false;

        // Profile system
        public string SelectedProfile { get; set; } = "";

        // Overlay position
        public double OverlayLeft { get; set; } = -1;
        public double OverlayTop { get; set; } = -1;

        // AI Coach — API key is NOT stored here; retrieve it from Windows.Security.Credentials.PasswordVault
        // if/when AI coaching is re-enabled.

        // Goal + Streak tracking
        public int DailyGoalQuality { get; set; } = 70;
        public int CurrentStreak { get; set; } = 0;
        public int BestStreakDays { get; set; } = 0;
        public DateTime LastSessionDate { get; set; } = DateTime.MinValue;

        // Daily challenge tracking
        public DateTime LastChallengeDate   { get; set; } = DateTime.MinValue;
        public int      ChallengesCompleted { get; set; } = 0;

        // Update banner — tracks last version the user has seen the What's New banner for
        public string LastVersionSeen { get; set; } = "";

        // Re-engagement notifications — reset to false after each session
        public bool ReEngagementNotificationSent { get; set; } = false;

        // CAT_RETENTION_NOTIFICATIONS: win-back channel state.
        // NotificationsEnabled = user's in-app toggle (default on; set false if they decline/disable).
        // NotificationPermissionAsked = we've surfaced the "why" prompt once.
        // NotificationVariantSeed = rotates message phrasing so consecutive nudges differ.
        public bool NotificationsEnabled        { get; set; } = true;
        public bool NotificationPermissionAsked { get; set; } = false;
        public int  NotificationVariantSeed     { get; set; } = 0;
        public int  PreferredNudgeHour          { get; set; } = 19;   // learned from last-trained hour

        // CAT_FIRST_FUN_AND_FUNNEL: hit/miss sound effects (game feel). Default on.
        public bool SoundEnabled { get; set; } = true;

        // CAT_CROSSHAIR: the trainer cursor. Older settings files just get the default.
        public CrosshairSettings Crosshair { get; set; } = new();

        // ── CAT_SESSION_REPORT (2026-08-14) ──────────────────────────────────
        // The coach report used to open after EVERY drill. Telemetry showed a real
        // player run 8 drills in 7 minutes and close all 8 reports in 0-14 seconds —
        // it was a speed bump between reps, not a moment anyone read. The report now
        // comes once, when they stop training.
        //
        // ReportAfterEveryDrill restores the old behaviour for anyone who preferred it.
        // Default false = the new, quieter flow.
        public bool ReportAfterEveryDrill { get; set; } = false;

        // Start of a manual training session whose summary hasn't been shown yet.
        // MinValue = nothing pending. If the app is killed before the summary renders,
        // this survives and the summary is shown at the next launch instead of being
        // silently lost — which is also what stops the upgrade ask depending on
        // catching someone mid-exit.
        public DateTime PendingSummarySessionStartUtc { get; set; } = DateTime.MinValue;

        // ── CAT_TELEMETRY ────────────────────────────────────────────────────
        // Anonymous product analytics. OPT-OUT by default: opt-in would sample only
        // the most engaged users, which is precisely the population that does NOT need
        // measuring — the whole question is why the others leave.
        //
        // TelemetryInstallId is a random GUID minted on first use. It is scoped to this
        // install and nothing else: not a hardware ID, not an account, not derivable
        // back to a person. Clearing it (see TelemetryService.Reset) makes the install
        // indistinguishable from a new one.
        //
        // TelemetryNoticeShown gates the one-time first-run disclosure. Nothing is sent
        // before the user has seen it — see TelemetryService.IsEnabled.
        public bool   TelemetryEnabled     { get; set; } = true;
        public bool   TelemetryNoticeShown { get; set; } = false;
        public string TelemetryInstallId   { get; set; } = "";

        // Toast id → the moment it was scheduled to fire. Written at reschedule time and
        // read once on the next launch to infer whether a nudge brought the user back
        // (see ToastService.ReportNudgeAttribution). Cleared after each read.
        public Dictionary<string, DateTime> ScheduledNudgeTimes { get; set; } = new();


        // CAT_CALIBRATION_AS_GAME: the headline "aim score" from calibration — the number to beat.
        public int BestSkillScore { get; set; } = 0;

        // CAT_STREAK_GOAL: a streak the user CHOSE (7/14/30). Research: a self-selected
        // goal builds far stronger commitment than an assigned one, and loss aversion
        // around day 7 is the strongest week-2 return driver. 0 = never picked one.
        // StreakGoalPromptedAt gates the ask so it's offered once, never nagged.
        public int      StreakGoalDays      { get; set; } = 0;
        public DateTime StreakGoalPromptedAt { get; set; } = DateTime.MinValue;
        public int      StreakGoalsHit      { get; set; } = 0;

        // CAT_REVIEW_PROMPT: PB-triggered Store-rating ask. Defaults mean "allowed to
        // fire" only once the counters genuinely permit (MinValue = never prompted).
        public DateTime LastReviewPromptUtc           { get; set; } = DateTime.MinValue;
        public int      ReviewPromptCount             { get; set; } = 0;
        public bool     HasRatedOrDismissedPermanently { get; set; } = false;

        // See You Tomorrow prompt — shown at most once per calendar day
        public DateTime LastTomorrowPromptDate { get; set; } = DateTime.MinValue;

        // XP + Level system
        public int TotalXP      { get; set; } = 0;
        public int CurrentLevel { get; set; } = 1;

        // Weekly summary notification
        public DateTime LastWeeklySummaryDate { get; set; } = DateTime.MinValue;

        // Upgrade reminder — set when user clicks "Remind me after my next session"
        public bool PendingUpgradeReminder { get; set; } = false;

        // Free full coaching session — aim trainer.
        // CAT_COACH_FREEMIUM_FIX: TWO free full tastes (session 1 onboarding payoff +
        // a re-hook before session 15). FreeFullTastesUsed counts them (0→1→2, then locked).
        // HasUsedFreeFullSession kept coherent (= 2 tastes used) for legacy callers.
        public int    FreeFullTastesUsed           { get; set; } = 0;
        public bool   HasUsedFreeFullSession       { get; set; } = false;
        public int    FreeFullSessionTrigger       { get; set; } = 5;
        public string LastPrescribedScenario       { get; set; } = "";
        public string LastPrescribedDifficulty     { get; set; } = "";
        public int    LastPrescribedSessionIndex   { get; set; } = 0;

        // Free full coaching session — tracker (unlocks at tracker session 3, one time)
        public bool HasUsedFreeFullTrackerSession { get; set; } = false;

        // Per-scenario difficulty tracking
        public Dictionary<string, ScenarioDifficultyState>
            ScenarioDifficulties { get; set; } = new();

        // Diagnostic assessment history
        public List<DiagnosticProfile>
            DiagnosticHistory { get; set; } = new();

        // Whether the free assessment coaching report has been used
        public bool HasUsedFreeAssessmentReport { get; set; } = false;

        // Active sensitivity transition plan
        public SensitivityTransitionPlan?
            ActiveTransitionPlan { get; set; } = null;

        // Sessions logged at current sensitivity (used for transition tracking)
        public int SessionsAtCurrentSensitivity { get; set; } = 0;

        // Dismissed assessment prompt card
        public bool DismissedAssessmentPrompt { get; set; } = false;

        // Onboarding calibration state — TASK-12
        public bool CalibrationComplete { get; set; } = false;
        public bool OnboardingSkipped   { get; set; } = false;

        // Tip rotation — keys of recently shown tips (newest first, max 20 entries)
        public List<string> RecentTipKeys { get; set; } = new();

        // TASK-0.1: achievement toasts queued past the 2-per-session display cap
        public List<string> PendingAchievementToasts { get; set; } = new();

        // TASK-2.1/2.2: technique prescription loop state
        public PrescriptionState? ActiveTechniquePrescription { get; set; } = null;
        // PrescriptionKey → TotalDrillCount when last prescribed (cooldown tracking)
        public Dictionary<string, int> PrescriptionCooldowns { get; set; } = new();

        public ScenarioDifficultyState GetScenarioState(
            string scenario, string variant)
        {
            string key = $"{scenario}_{variant}";
            if (!ScenarioDifficulties.ContainsKey(key))
                ScenarioDifficulties[key] = new ScenarioDifficultyState();
            return ScenarioDifficulties[key];
        }
    }
}
