using System;
using System.Linq;
using AgentClicker.Util;
using NUnit.Framework;
using UnityEngine;

namespace AgentClicker.Tests
{
    /// <summary>The long lo-fi piece: long enough not to wear thin, its sections different, and seamless to switch to.</summary>
    public class MusicTests
    {
        const int Rate = 32000, Seed = 7;
        static float[] _loop, _song;

        [OneTimeSetUp]
        public void Render()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            _loop = LofiMusic.Render(Rate, Seed);
            double loopMs = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            _song = LofiMusic.Render(Rate, Seed, song: true);
            Debug.Log($"[Music] loop {_loop.Length / (double)Rate:0.0} s rendered in {loopMs:0} ms, " +
                      $"long piece {_song.Length / (double)Rate:0.0} s in {clock.Elapsed.TotalMilliseconds:0} ms");
        }

        static double Rms(float[] x, int from, int count)
        {
            double sum = 0;
            for (int i = from; i < from + count; i++) sum += x[i] * (double)x[i];
            return Math.Sqrt(sum / count);
        }

        static double Db(double ratio) => 20 * Math.Log10(ratio);

        [Test]
        public void TheLongPieceIsFourSectionsOfTheLoopsLength()
        {
            Assert.AreEqual(LofiMusic.Length(Rate, false), _loop.Length);
            Assert.AreEqual(4 * _loop.Length, _song.Length);
            Assert.GreaterOrEqual(_song.Length / (double)Rate, 90.0, "seconds before the music repeats");
        }

        [Test]
        public void TheLongPieceStartsWithTheLoopSampleForSample()
        {
            double worst = 0;
            for (int i = 0; i < _loop.Length; i++) worst = Math.Max(worst, Math.Abs(_song[i] - _loop[i]));
            Assert.AreEqual(0.0, worst, 1e-7, "the end of the loop must lead into the start of the long piece as into its own start");
        }

        [Test]
        public void NoTwoSectionsAreTheSame()
        {
            int n = _loop.Length;
            string[] names = { "A", "B", "C (breakdown)", "A'" };
            for (int a = 0; a < 4; a++)
            for (int b = a + 1; b < 4; b++)
            {
                double diff = 0;
                for (int i = 0; i < n; i++) { double d = _song[a * n + i] - _song[b * n + i]; diff += d * d; }
                double rel = Math.Sqrt(diff / n) / Math.Max(Rms(_song, a * n, n), Rms(_song, b * n, n));
                Debug.Log($"[Music] {names[a]} vs {names[b]}: difference {rel:P0} of the louder section's level");
                Assert.Greater(rel, 0.3, $"{names[a]} and {names[b]} sound too alike");
            }
        }

        /// <summary>The jump from the last sample back to the first is no bigger than the steps inside the piece.</summary>
        [Test]
        public void BothLoopPointsAreSeamless()
        {
            foreach (var (name, x) in new[] { ("loop", _loop), ("long piece", _song) })
            {
                var steps = new float[x.Length - 1];
                for (int i = 0; i < steps.Length; i++) steps[i] = Math.Abs(x[i + 1] - x[i]);
                Array.Sort(steps);
                float p999 = steps[(int)(steps.Length * 0.999)], seam = Math.Abs(x[0] - x[x.Length - 1]);
                Debug.Log($"[Music] {name} seam {seam:0.0000} (steps inside: 99.9th percentile {p999:0.0000}, largest {steps[steps.Length - 1]:0.0000})");
                Assert.LessOrEqual(seam, p999, $"{name}: a click where it loops");
            }
        }

        [Test]
        public void TheLongPieceIsAsLoudAsTheLoopAndNeverClips()
        {
            double loopRms = Rms(_loop, 0, _loop.Length), songRms = Rms(_song, 0, _song.Length);
            float loopPeak = _loop.Max(Math.Abs), songPeak = _song.Max(Math.Abs);
            int n = _loop.Length;
            string sections = string.Join(", ", Enumerable.Range(0, 4).Select(s => $"{Db(Rms(_song, s * n, n) / loopRms):+0.0;-0.0} dB"));
            Debug.Log($"[Music] loop RMS {loopRms:0.0000} peak {loopPeak:0.000}; long piece RMS {songRms:0.0000} ({Db(songRms / loopRms):+0.0;-0.0} dB) " +
                      $"peak {songPeak:0.000} ({Db(songPeak / loopPeak):+0.0;-0.0} dB); sections vs the loop: {sections}");
            Assert.Less(songPeak, 0.81f, "the limiter's ceiling is 0.8");
            Assert.LessOrEqual(Math.Abs(Db(songRms / loopRms)), 2.0, "overall level");
            Assert.LessOrEqual(Math.Abs(Db(songPeak / loopPeak)), 2.0, "peak level");
        }
    }
}
