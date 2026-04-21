using System;

namespace CleanAimTracker.Models
{
    public class AimProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public int DPI { get; set; }

        public double Sensitivity { get; set; }

        public double EDPI => DPI * Sensitivity;

        public double CmPer360 { get; set; }

        public string Game { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime LastModified { get; set; } = DateTime.Now;
    }
}
