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

        // See You Tomorrow prompt — shown at most once per calendar day
        public DateTime LastTomorrowPromptDate { get; set; } = DateTime.MinValue;

        // XP + Level system
        public int TotalXP      { get; set; } = 0;
        public int CurrentLevel { get; set; } = 1;

        // Weekly summary notification
        public DateTime LastWeeklySummaryDate { get; set; } = DateTime.MinValue;

        // Upgrade reminder — set when user clicks "Remind me after my next session"
        public bool PendingUpgradeReminder { get; set; } = false;

        // Free full coaching session — aim trainer (unlocks at session 5, one time)
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
