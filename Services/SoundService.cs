using System;
using System.IO;
using System.Media;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_FIRST_FUN_AND_FUNNEL T2.2: game-feel audio. Hit/miss sounds are SYNTHESIZED in
    /// code (16-bit PCM WAV) so no asset is needed and there's zero load latency — the
    /// players are pre-loaded so a hit pops instantly. Respect the mute setting. No
    /// dependency, no network — pure in-memory audio. Swap in a real .wav later if desired.
    /// </summary>
    public static class SoundService
    {
        private const int Rate = 44100;
        private static bool _enabled = true;

        private static readonly SoundPlayer _hit  = Build(GenerateHit());
        private static readonly SoundPlayer _miss = Build(GenerateMiss());

        public static void SetEnabled(bool on) => _enabled = on;

        /// <summary>Crisp satisfying pop on a hit. Instant — no lag (pre-loaded).</summary>
        public static void PlayHit()  { if (_enabled) Safe(_hit); }
        /// <summary>Softer, lower thunk on a miss.</summary>
        public static void PlayMiss() { if (_enabled) Safe(_miss); }

        private static void Safe(SoundPlayer p) { try { p.Play(); } catch { /* audio is non-critical */ } }

        private static SoundPlayer Build(byte[] wav)
        {
            var p = new SoundPlayer(new MemoryStream(wav));
            try { p.Load(); } catch { /* play will still attempt */ }
            return p;
        }

        // ── Synthesis ──────────────────────────────────────────────────────────
        // Hit: a percussive "thock" — a filtered-noise click transient over a fast
        // pitch-dropping low thump. Mostly transient + body, little sustained tone, so it
        // reads as a sharp mechanical tick (KovaaK's-style), NOT a squeaky cartoon chirp.
        private static byte[] GenerateHit()
        {
            int ms = 55;
            int n = Rate * ms / 1000;
            var pcm = new short[n];
            var rng = new Random(7);          // deterministic noise for the transient
            double phase = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                // Click transient: short burst of low-passed noise, gone in ~7 ms — the "snap".
                double nEnv  = Math.Exp(-t * 420.0);
                double noise = rng.NextDouble() * 2.0 - 1.0;
                lp += 0.35 * (noise - lp);    // simple one-pole low-pass to take the fizz off
                double click = lp * nEnv * 0.6;
                // Body: low thump with a fast downward pitch drop (220→90 Hz) and quick decay.
                double bEnv = Math.Exp(-t * 75.0);
                double freq = 220.0 - 130.0 * Math.Min(1.0, t / 0.018);
                phase += 2 * Math.PI * freq / Rate;
                double body = bEnv * 0.85 * Math.Sin(phase);
                double v = click + body;
                pcm[i] = (short)(Math.Clamp(v, -1, 1) * 0.6 * short.MaxValue);
            }
            return Encode(pcm);
        }

        // Miss: low, soft, short — clearly distinct from a hit, never harsh.
        private static byte[] GenerateMiss()
        {
            int ms = 55;
            int n = Rate * ms / 1000;
            var pcm = new short[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                double env = Math.Exp(-t * 30.0);
                double freq = 175.0;
                phase += 2 * Math.PI * freq / Rate;
                double v = env * Math.Sin(phase);
                pcm[i] = (short)(Math.Clamp(v, -1, 1) * 0.30 * short.MaxValue);
            }
            return Encode(pcm);
        }

        // 16-bit mono PCM WAV container.
        private static byte[] Encode(short[] pcm)
        {
            int dataBytes = pcm.Length * 2;
            using var ms = new MemoryStream(44 + dataBytes);
            using var w = new BinaryWriter(ms);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);                       // PCM fmt chunk size
            w.Write((short)1);                 // PCM
            w.Write((short)1);                 // mono
            w.Write(Rate);                     // sample rate
            w.Write(Rate * 2);                 // byte rate (rate * blockAlign)
            w.Write((short)2);                 // block align (mono * 16-bit)
            w.Write((short)16);                // bits per sample
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            foreach (var s in pcm) w.Write(s);
            w.Flush();
            return ms.ToArray();
        }
    }
}
