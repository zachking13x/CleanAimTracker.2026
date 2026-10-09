using CleanAimTracker.Models;
using System;
using System.IO;
using System.Windows.Input;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_CROSSHAIR: turns a rendered crosshair into a real Windows cursor.
    ///
    /// The crosshair IS the cursor, not an overlay chasing it. Windows draws the cursor in
    /// hardware, so it moves with the mouse at zero added latency. A WPF-drawn crosshair
    /// following MouseMove lags the true aim point by a frame or two, which is the last
    /// thing an aim trainer should add.
    /// </summary>
    public static class CrosshairCursor
    {
        public static Cursor Create(CrosshairSettings settings, double dpiScale)
            => new(new MemoryStream(ToCurFile(CrosshairRenderer.Render(settings, dpiScale))), scaleWithDpi: false);

        /// <summary>Encodes a 32-bit alpha .cur (ICONDIR + DIB) with the hotspot on the aim pixel.</summary>
        public static byte[] ToCurFile(CrosshairImage img)
        {
            int n = img.Size;
            if (n < 1 || n > 255) throw new ArgumentOutOfRangeException(nameof(img), "Cursor size must be 1-255 px.");

            int maskStride = (n + 31) / 32 * 4;
            int pixelBytes = n * n * 4;
            int maskBytes  = maskStride * n;
            int dibBytes   = 40 + pixelBytes + maskBytes;

            using var ms = new MemoryStream(6 + 16 + dibBytes);
            using var w  = new BinaryWriter(ms);

            // ICONDIR: reserved, type 2 = cursor, one image
            w.Write((ushort)0); w.Write((ushort)2); w.Write((ushort)1);
            // ICONDIRENTRY: for cursors the planes/bitcount slots carry the hotspot
            w.Write((byte)n); w.Write((byte)n); w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)img.Hotspot); w.Write((ushort)img.Hotspot);
            w.Write(dibBytes); w.Write(6 + 16);

            // BITMAPINFOHEADER: height is doubled because the AND mask follows the colour data
            w.Write(40); w.Write(n); w.Write(n * 2); w.Write((ushort)1); w.Write((ushort)32);
            w.Write(0); w.Write(pixelBytes + maskBytes); w.Write(0); w.Write(0); w.Write(0); w.Write(0);

            // DIB rows run bottom-up
            for (int y = n - 1; y >= 0; y--)
                w.Write(img.Bgra, y * n * 4, n * 4);

            // All-zero AND mask: the alpha channel decides transparency.
            w.Write(new byte[maskBytes]);

            w.Flush();
            return ms.ToArray();
        }
    }
}
