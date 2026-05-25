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
        public bool OnboardingTourComplete { get; set; } = false;

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
    }
}
