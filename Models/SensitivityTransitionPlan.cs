using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    public class SensitivityTransitionStep
    {
        public int    StepNumber        { get; set; }
        public double TargetCmPer360    { get; set; }
        public double TargetSensitivity { get; set; }
        public int    RequiredSessions  { get; set; } = 3;
        public int    CompletedSessions { get; set; } = 0;
        public bool   IsComplete        { get; set; } = false;
    }

    public class SensitivityTransitionPlan
    {
        public double StartCmPer360   { get; set; }
        public double TargetCmPer360  { get; set; }
        public string TransitionType  { get; set; } = "Gradual"; // "Gradual" or "Direct"
        public List<SensitivityTransitionStep> Steps
            { get; set; } = new();
        public int      CurrentStepIndex { get; set; } = 0;
        public bool     IsComplete       { get; set; } = false;
        public DateTime CreatedAt        { get; set; }
    }
}
