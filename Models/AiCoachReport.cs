using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    public class AiCoachReport
    {
        public string OverallRating { get; set; } = "";
        public string Headline { get; set; } = "";
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public List<string> Advice { get; set; } = new();
        public string NextDrillSuggestion { get; set; } = "";
        public string MotivationalClose { get; set; } = "";
    }
}
