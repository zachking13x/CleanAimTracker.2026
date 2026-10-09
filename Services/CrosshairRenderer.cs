using CleanAimTracker.Models;
using System;
using System.Globalization;

namespace CleanAimTracker.Services
{
    /// <summary>A rendered crosshair: square BGRA pixels (top-down, straight alpha) plus the aim pixel.</summary>
    public sealed record CrosshairImage(int Size, byte[] Bgra, int Hotspot)
    {
        public byte Alpha(int x, int y) => Bgra[(y * Size + x) * 4 + 3];
    }

    /// <summary>
    /// CAT_CROSSHAIR: draws the crosshair straight into pixels. Pure (no WPF), so it is
    /// testable and pixel-exact the way in-game crosshairs are; the same image feeds the
    /// real cursor and the Settings preview, so the preview can't lie.
    /// </summary>
    public static class CrosshairRenderer
    {
        /// <summary>Keeps the cursor inside the sizes Windows handles reliably.</summary>
        public const int MaxSize = 127;

        private const byte FallbackR = 0x2F, FallbackG = 0xD5, FallbackB = 0xF2;

        public static bool TryParseColor(string? hex, out byte r, out byte g, out byte b)
        {
            r = g = b = 0;
            var h = (hex ?? "").Trim().TrimStart('#');
            if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v))
                return false;
            r = (byte)(v >> 16); g = (byte)(v >> 8); b = (byte)v;
            return true;
        }

        /// <param name="scale">Display scale (1.0 = 100%, 1.5 = 150%).</param>
        public static CrosshairImage Render(CrosshairSettings s, double scale)
        {
            scale = Math.Clamp(double.IsFinite(scale) ? scale : 1.0, 1.0, 4.0);
            int Px(int dip, int min) => Math.Max(min, (int)Math.Round(dip * scale));

            int th  = Px(Math.Clamp(s.Thickness, CrosshairSettings.MinThickness, CrosshairSettings.MaxThickness), 1);
            int len = Px(Math.Clamp(s.Length,    CrosshairSettings.MinLength,    CrosshairSettings.MaxLength), 1);
            int gap = Px(Math.Clamp(s.Gap,       CrosshairSettings.MinGap,       CrosshairSettings.MaxGap), 0);
            int ol  = s.Outline ? Math.Max(1, (int)Math.Round(scale)) : 0;
            double opacity = Math.Clamp(s.Opacity, CrosshairSettings.MinOpacity, CrosshairSettings.MaxOpacity) / 100.0;
            if (!TryParseColor(s.Color, out byte cr, out byte cg, out byte cb))
                (cr, cg, cb) = (FallbackR, FallbackG, FallbackB);

            // The core band around the aim pixel. Even thicknesses can't centre on one pixel,
            // so the extra row/column goes right/down, as most games do.
            int lo = (th - 1) / 2, hi = th / 2;

            // Shrink the arms rather than grow past MaxSize at extreme size × DPI.
            int maxReach = (MaxSize - 1) / 2;
            double ringOuter = gap + len + hi;
            int reach = s.Style switch
            {
                CrosshairStyle.Dot    => hi + ol,
                CrosshairStyle.Circle => (int)Math.Ceiling(ringOuter + ol) + 1,
                _                     => hi + gap + len + ol,
            };
            if (reach > maxReach)
            {
                int over = reach - maxReach;
                len = Math.Max(1, len - over);
                ringOuter = gap + len + hi;
                reach = maxReach;
            }

            int size = reach * 2 + 1, c = reach;
            var core  = new double[size * size];
            var outer = new double[size * size];

            void Rect(int x0, int y0, int x1, int y1)   // inclusive, aim-pixel relative
            {
                Fill(core,  x0, y0, x1, y1);
                Fill(outer, x0 - ol, y0 - ol, x1 + ol, y1 + ol);
            }
            void Fill(double[] m, int x0, int y0, int x1, int y1)
            {
                for (int y = Math.Max(0, c + y0); y <= Math.Min(size - 1, c + y1); y++)
                    for (int x = Math.Max(0, c + x0); x <= Math.Min(size - 1, c + x1); x++)
                        m[y * size + x] = 1;
            }

            bool arms = s.Style is CrosshairStyle.Cross or CrosshairStyle.CrossDot;
            bool dot  = s.Style is CrosshairStyle.Dot   or CrosshairStyle.CrossDot;

            if (arms)
            {
                Rect(hi + gap + 1,           -lo, hi + gap + len,        hi);   // right
                Rect(-lo - gap - len,        -lo, -lo - gap - 1,         hi);   // left
                Rect(-lo, hi + gap + 1,           hi, hi + gap + len);          // down
                Rect(-lo, -lo - gap - len,        hi, -lo - gap - 1);           // up
            }
            if (dot) Rect(-lo, -lo, hi, hi);

            if (s.Style == CrosshairStyle.Circle)
            {
                // Anti-aliased ring; coverage = how much of each pixel the band crosses.
                double cx = c + (hi - lo) / 2.0 + 0.5, cy = cx;
                double inner = ringOuter - th;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        double d = Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy));
                        core[y * size + x]  = Band(d, inner, ringOuter);
                        outer[y * size + x] = ol > 0 ? Band(d, inner - ol, ringOuter + ol) : core[y * size + x];
                    }
            }

            // Colour over a black outline, then the player's opacity. Straight alpha.
            var px = new byte[size * size * 4];
            for (int i = 0; i < core.Length; i++)
            {
                double k = core[i], o = Math.Max(outer[i], k);
                double a = k + o * (1 - k);
                if (a <= 0) continue;
                double colour = k / a;   // share of the pixel that's crosshair colour vs outline black
                px[i * 4 + 0] = (byte)Math.Round(cb * colour);
                px[i * 4 + 1] = (byte)Math.Round(cg * colour);
                px[i * 4 + 2] = (byte)Math.Round(cr * colour);
                px[i * 4 + 3] = (byte)Math.Round(255 * a * opacity);
            }
            return new CrosshairImage(size, px, c);
        }

        private static double Band(double d, double inner, double outerR)
            => Math.Clamp(Math.Min(d - inner, outerR - d) + 0.5, 0, 1);
    }
}
