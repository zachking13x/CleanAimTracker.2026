namespace CleanAimTracker.Helpers
{
    public static class Helpers
    {
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static double Round(double value, int decimals = 2)
        {
            return Math.Round(value, decimals);
        }
    }
}
