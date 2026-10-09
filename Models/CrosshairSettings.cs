namespace CleanAimTracker.Models
{
    public enum CrosshairStyle { Cross, CrossDot, Dot, Circle }

    /// <summary>
    /// CAT_CROSSHAIR: the player's trainer crosshair. Sizes are in DIPs at 100% display
    /// scale; the renderer converts to device pixels so 2px stays crisp at any DPI.
    /// </summary>
    public class CrosshairSettings
    {
        public CrosshairStyle Style  { get; set; } = CrosshairStyle.Cross;
        public string Color          { get; set; } = "#2FD5F2";   // brand cyan, readable on the dark arena
        public int    Length         { get; set; } = 6;
        public int    Thickness      { get; set; } = 2;
        public int    Gap            { get; set; } = 3;
        public bool   Outline        { get; set; } = true;
        public int    Opacity        { get; set; } = 100;          // percent

        public const int MinLength = 1, MaxLength = 20;
        public const int MinThickness = 1, MaxThickness = 6;
        public const int MinGap = 0, MaxGap = 12;
        public const int MinOpacity = 30, MaxOpacity = 100;

        public CrosshairSettings Clone() => (CrosshairSettings)MemberwiseClone();
    }
}
