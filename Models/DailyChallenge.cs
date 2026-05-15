namespace CleanAimTracker.Models
{
    public class DailyChallenge
    {
        public string Scenario    { get; set; } = "";
        public string Difficulty  { get; set; } = "";
        public string GoalType    { get; set; } = "";   // "Accuracy" | "Reaction" | "MaxStreak"
        public double GoalValue   { get; set; }
        public string Description { get; set; } = "";   // Full: "Tracking · Hard — Hit 85% accuracy"
        public string ShortDesc   { get; set; } = "";   // Short: "Hit 85% accuracy"
        public bool   IsCompleted { get; set; } = false;
    }
}
