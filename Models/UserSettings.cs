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
        public DateTime FirstLaunchDate { get; set; } = DateTime.UtcNow;

        // Profile system
        public string SelectedProfile { get; set; } = "";

        // Trial system
        public bool TrialActive { get; set; } = true;
        public int TrialDaysRemaining { get; set; } = 7;
    }
}
