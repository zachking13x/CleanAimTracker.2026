namespace CleanAimTracker.Models
{
    public class AimTrainerResult
    {
        public DateTime Timestamp { get; set; }

        public string Scenario { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public int DurationSeconds { get; set; }

        public int Hits { get; set; }
        public int Misses { get; set; }
        public double Accuracy { get; set; }

        public int Score { get; set; }

        public double AvgReactionMs { get; set; }
        public double BestReactionMs { get; set; }

        public int MaxStreak { get; set; }
    }
}
