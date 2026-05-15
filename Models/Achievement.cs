namespace CleanAimTracker.Models
{
    public class Achievement
    {
        public string    Id          { get; set; } = "";
        public string    Name        { get; set; } = "";
        public string    Description { get; set; } = "";
        public string    Emoji       { get; set; } = "";
        public string    Category    { get; set; } = "";
        public bool      IsUnlocked  { get; set; } = false;
        public DateTime? UnlockedAt  { get; set; } = null;
    }
}
