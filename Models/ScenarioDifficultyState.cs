namespace CleanAimTracker.Models
{
    public class ScenarioDifficultyState
    {
        public string CurrentDifficulty    { get; set; } = "Easy";
        public int    SessionsAtCurrent    { get; set; } = 0;
        public double AvgAccuracyAtCurrent { get; set; } = 0;
        public bool   MediumUnlocked       { get; set; } = false;
        public bool   HardUnlocked         { get; set; } = false;
        public bool   NightmareUnlocked    { get; set; } = false;
    }
}
