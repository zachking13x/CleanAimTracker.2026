namespace CleanAimTracker.Models
{
    /// <summary>
    /// A specific drill recommendation with a reason why it addresses the diagnosed problem.
    /// </summary>
    public class DrillPrescription
    {
        public string Scenario    { get; set; } = "";
        public string Difficulty  { get; set; } = "";
        public string SubVariant  { get; set; } = "Standard";
        public int    DurationSec { get; set; } = 60;
        public string Reason      { get; set; } = "";
        public string FocusCue    { get; set; } = "";
    }
}
