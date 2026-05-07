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

        // Profile system
        public string SelectedProfile { get; set; } = "";

        // Trial system
        public bool TrialActive { get; set; } = true;
        public int TrialDaysRemaining { get; set; } = 7;
        public double OverlayLeft { get; set; } = -1;
        public double OverlayTop { get; set; } = -1;

        // AI Coach (optional — enables personalized coaching in result window)
        public string AnthropicApiKey { get; set; } = "";
    }
}
