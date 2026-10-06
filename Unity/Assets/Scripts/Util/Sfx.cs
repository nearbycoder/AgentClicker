using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AgentClicker.Util
{
    public enum Sound { Key, Crit, Buy, BigBuy, Drop, DropClaim, Alert, Fixed, Promotion, Bell, Login, Error, Whoosh, UiClick, Mail, Page, PickUp, HangUp, Chapter, Trophy }

    /// <summary>
    /// Procedurally synthesised audio: sound effects, an office ambience loop and a lo-fi music loop.
    /// The game ships with zero audio files. Music is rendered on a worker thread at startup (in slices on the
    /// main thread in the browser build, which has no threads).
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        const int MusicRate = 32000;

        readonly Dictionary<Sound, AudioClip[]> _clips = new Dictionary<Sound, AudioClip[]>();
        AudioSource[] _voices;
        AudioSource _ambience, _music, _ring;
        int _next;
        float _lastKey;
        float _sfxVolume = 0.8f, _musicVolume = 0.45f, _ambienceVolume = 0.5f;
        float _musicDuck = 1f, _musicFade;
        Task<float[]> _musicJob;
        static readonly System.Random Rng = new System.Random(5);

        public static Sfx Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            _voices = new AudioSource[12];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0f;
            }
            Build();

            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.clip = Make("ambience", 8f, OfficeHum, loopCrossfade: true);
            _ambience.loop = true;
            _ambience.Play();

            _ring = gameObject.AddComponent<AudioSource>();
            _ring.clip = Make("ring", 3f, PhoneRing);
            _ring.loop = true;
            _ring.playOnAwake = false;

            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.priority = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            StartCoroutine(LofiMusic.RenderInSteps(MusicRate, 7, 6.0, SetMusic));
#else
            _musicJob = Task.Run(() => LofiMusic.Render(MusicRate, seed: 7));
#endif
            ApplyVolumes();
        }

        void SetMusic(float[] data)
        {
            var clip = AudioClip.Create("lofi", data.Length, 1, MusicRate, false);
            clip.SetData(data, 0);
            _music.clip = clip;
            _music.Play();
            _musicFade = 0f;
            Debug.Log($"[Sfx] music ready ({data.Length / (float)MusicRate:0.0} s loop) after {Time.realtimeSinceStartup:0.0} s");
        }

        void Update()
        {
            if (_musicJob != null && _musicJob.IsCompleted)
            {
                if (_musicJob.Status == TaskStatus.RanToCompletion) SetMusic(_musicJob.Result);
                else Debug.LogWarning("[Sfx] music render failed: " + _musicJob.Exception?.GetBaseException().Message);
                _musicJob = null;
            }
            if (_musicFade < 1f)
            {
                _musicFade = Mathf.Min(1f, _musicFade + Time.unscaledDeltaTime / 3f);
                ApplyVolumes();
            }
        }

        public void StartRing()
        {
            _ring.volume = 0.5f * _sfxVolume;
            if (!_ring.isPlaying) _ring.Play();
            DuckMusic(0.5f);
        }

        public void StopRing()
        {
            if (_ring.isPlaying) _ring.Stop();
            DuckMusic(1f);
        }

        public void SetVolumes(float sfx, float music, float ambience)
        {
            _sfxVolume = sfx;
            _musicVolume = music;
            _ambienceVolume = ambience;
            ApplyVolumes();
        }

        /// <summary>Lower the music a little (e.g. during the night card or the ending speech).</summary>
        public void DuckMusic(float amount)
        {
            _musicDuck = Mathf.Clamp01(amount);
            ApplyVolumes();
        }

        void ApplyVolumes()
        {
            if (_ambience) _ambience.volume = 0.22f * _ambienceVolume;
            if (_music) _music.volume = 0.55f * _musicVolume * _musicDuck * _musicFade;
        }

        /// <summary>The synthesised clip for a sound (its first variant), e.g. for exporting trailer stingers.</summary>
        public AudioClip Clip(Sound s) => _clips.TryGetValue(s, out var v) ? v[0] : null;

        public void Play(Sound s, float volume = 1f, float pitch = 1f)
        {
            if (!_clips.TryGetValue(s, out var variants)) return;
            if (s == Sound.Key)
            {
                if (Time.unscaledTime - _lastKey < 0.025f) return;
                _lastKey = Time.unscaledTime;
            }
            var v = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            v.clip = variants[UnityEngine.Random.Range(0, variants.Length)];
            v.volume = volume * _sfxVolume;
            v.pitch = pitch * (s == Sound.Key ? UnityEngine.Random.Range(0.92f, 1.1f) : 1f);
            v.Play();
        }

        // ------------------------------------------------------------------ synthesis
        void Build()
        {
            _clips[Sound.Key] = new[] { Make("key1", 0.06f, t => KeyClick(t, 1800)), Make("key2", 0.06f, t => KeyClick(t, 2300)),
                                        Make("key3", 0.06f, t => KeyClick(t, 1500)) };
            _clips[Sound.Crit] = new[] { Make("crit", 0.35f, t => Chime(t, new[] { 1046.5, 1568.0 }, 0.06, 0.25)) };
            _clips[Sound.Buy] = new[] { Make("buy", 0.3f, t => Chime(t, new[] { 659.3, 987.8 }, 0.07, 0.2)) };
            _clips[Sound.BigBuy] = new[] { Make("bigbuy", 0.8f, t => Chime(t, new[] { 523.3, 659.3, 784.0, 1046.5 }, 0.09, 0.4)) };
            _clips[Sound.Drop] = new[] { Make("drop", 0.6f, Sparkle) };
            _clips[Sound.DropClaim] = new[] { Make("claim", 0.7f, t => Chime(t, new[] { 784.0, 987.8, 1174.7, 1568.0 }, 0.06, 0.35)) };
            _clips[Sound.Alert] = new[] { Make("alert", 0.7f, Buzz) };
            _clips[Sound.Fixed] = new[] { Make("fixed", 0.4f, t => Chime(t, new[] { 440.0, 659.3 }, 0.08, 0.25)) };
            _clips[Sound.Promotion] = new[] { Make("promo", 1.4f, t => Chime(t, new[] { 523.3, 659.3, 784.0, 1046.5, 784.0, 1046.5 }, 0.12, 0.6)) };
            _clips[Sound.Bell] = new[] { Make("bell", 1.6f, Bell) };
            _clips[Sound.Login] = new[] { Make("login", 0.9f, t => Chime(t, new[] { 392.0, 523.3, 659.3 }, 0.12, 0.5)) };
            _clips[Sound.Error] = new[] { Make("error", 0.25f, t => Sine(t, 140) * Env(t, 0.005, 0.2) * 0.6) };
            _clips[Sound.Whoosh] = new[] { Make("whoosh", 0.6f, t => Noise() * Math.Sin(Math.PI * t / 0.6) * 0.25) };
            _clips[Sound.UiClick] = new[] { Make("ui", 0.05f, t => (Sine(t, 2600) * 0.6 + Noise() * 0.2) * Env(t, 0.0005, 0.008) * 0.5) };
            _clips[Sound.Mail] = new[] { Make("mail", 0.5f, t => Chime(t, new[] { 880.0, 1318.5 }, 0.09, 0.22)) };
            _clips[Sound.Page] = new[] { Make("page", 0.18f, t => Noise() * Env(t, 0.01, 0.05) * 0.25) };
            _clips[Sound.PickUp] = new[] { Make("pickup", 0.25f, t => (Noise() * 0.5 + Sine(t, 300) * 0.5) * Env(t, 0.002, 0.04) * 0.6 + Sine(t, 350) * Sine(t, 440) * Env(t, 0.05, 0.08) * 0.1) };
            _clips[Sound.HangUp] = new[] { Make("hangup", 0.3f, t => (Noise() * 0.6 + Sine(t, 160) * 0.6) * Env(t, 0.001, 0.05) * 0.7) };
            _clips[Sound.Chapter] = new[] { Make("chapter", 1.8f, t => Chime(t, new[] { 392.0, 493.9, 587.3, 784.0 }, 0.16, 0.9) * 0.9) };
            _clips[Sound.Trophy] = new[] { Make("trophy", 0.9f, t => Chime(t, new[] { 1046.5, 1318.5, 1568.0, 2093.0 }, 0.055, 0.4) * 0.7 + Sparkle(t) * 0.25) };
        }

        static AudioClip Make(string name, float seconds, Func<double, double> f, bool loopCrossfade = false)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = (float)Math.Max(-1, Math.Min(1, f(i / (double)Rate)));
            if (loopCrossfade)
            {
                // blend the last second into the first so the loop has no seam
                int x = Math.Min(Rate, n / 3);
                for (int i = 0; i < x; i++)
                {
                    float k = i / (float)x;
                    data[i] = data[i] * k + data[n - x + i] * (1 - k);
                }
                Array.Resize(ref data, n - x);
                n -= x;
            }
            else
            {
                int fade = Math.Min(n, Rate / 200);
                for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static double Noise() => Rng.NextDouble() * 2 - 1;
        static double Sine(double t, double hz) => Math.Sin(2 * Math.PI * hz * t);
        static double Env(double t, double attack, double decay) => t < attack ? t / attack : Math.Exp(-(t - attack) / decay);

        static double KeyClick(double t, double hz) =>
            (Noise() * 0.5 + Sine(t, hz) * 0.5) * Env(t, 0.0008, 0.012) * 0.55 + Sine(t, 180) * Env(t, 0.001, 0.02) * 0.2;

        static double Chime(double t, double[] notes, double step, double decay)
        {
            double s = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                double tt = t - i * step;
                if (tt < 0) continue;
                s += (Sine(tt, notes[i]) + 0.3 * Sine(tt, notes[i] * 2) + 0.1 * Sine(tt, notes[i] * 3)) * Env(tt, 0.004, decay);
            }
            return s * 0.22;
        }

        static double Sparkle(double t)
        {
            double s = 0;
            for (int i = 0; i < 6; i++)
            {
                double tt = t - i * 0.07;
                if (tt < 0) continue;
                s += Sine(tt, 1200 + i * 260) * Env(tt, 0.002, 0.08);
            }
            return s * 0.18;
        }

        static double Buzz(double t)
        {
            double gate = (t % 0.25) < 0.16 ? 1 : 0;
            double saw = 2 * ((t * 220) % 1.0) - 1;
            return saw * gate * Env(t, 0.01, 0.6) * 0.18;
        }

        static double Bell(double t) =>
            (Sine(t, 880) * Env(t, 0.002, 0.7) + 0.6 * Sine(t, 880 * 2.76) * Env(t, 0.002, 0.25) +
             0.4 * Sine(t, 880 * 5.4) * Env(t, 0.002, 0.12)) * 0.25;

        /// <summary>Classic dual-tone ring: two 0.4 s bursts, then a pause. 3 s loop.</summary>
        static double PhoneRing(double t)
        {
            bool on = t < 0.4 || (t > 0.6 && t < 1.0);
            if (!on) return 0;
            double warble = 0.5 + 0.5 * Math.Sign(Math.Sin(2 * Math.PI * 20 * t));
            return (Sine(t, 440) + Sine(t, 480)) * 0.18 * warble * Math.Min(1, (t % 0.6) / 0.01);
        }

        static double _hum;
        static double OfficeHum(double t)
        {
            _hum = (_hum + Noise() * 0.02) * 0.995;
            return _hum * 0.9 + Sine(t, 60) * 0.02 + Sine(t, 120) * 0.01;
        }
    }

    /// <summary>A seamless lo-fi loop: Rhodes-ish chords, round bass, swung drums, vinyl crackle.</summary>
    public static class LofiMusic
    {
        public const double Bpm = 76;

        public static float[] Render(int rate, int seed)
        {
            float[] result = null;
            var steps = RenderInSteps(rate, seed, double.PositiveInfinity, b => result = b);
            while (steps.MoveNext()) { }
            return result;
        }

        /// <summary>
        /// The same loop, rendered a few notes at a time: yields whenever a slice has taken `budgetMs`, so a
        /// coroutine can spread the work over frames.
        /// </summary>
        public static System.Collections.IEnumerator RenderInSteps(int rate, int seed, double budgetMs, Action<float[]> done)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            bool Spent() { if (clock.Elapsed.TotalMilliseconds < budgetMs) return false; clock.Restart(); return true; }
            var rng = new System.Random(seed);
            double beat = 60.0 / Bpm;
            int bars = 8;
            int length = (int)(bars * 4 * beat * rate);
            var buf = new float[length];

            // Fmaj7 - Em7 - Dm7 - Cmaj7, two bars each
            int[][] chords = { new[] { 53, 57, 60, 64 }, new[] { 52, 55, 59, 62 }, new[] { 50, 53, 57, 60 }, new[] { 48, 52, 55, 59 } };
            int[] scale = { 60, 62, 64, 67, 69, 72, 74, 76 };

            for (int bar = 0; bar < bars; bar++)
            {
                var chord = chords[bar / 2 % chords.Length];
                double barStart = bar * 4 * beat;
                // chords: beat 1 and the "and" of 3
                foreach (double at in new[] { 0.0, 2.5 })
                    foreach (int note in chord)
                    {
                        AddNote(buf, rate, barStart + at * beat + rng.NextDouble() * 0.012, Midi(note), 2.4, 0.075, Rhodes);
                        if (Spent()) yield return null;
                    }
                // bass: root on 1 and 3 (plus a pickup on the "and" of 4 every other bar)
                double root = Midi(chord[0] - 12);
                AddNote(buf, rate, barStart, root, 1.4, 0.2, Bass);
                AddNote(buf, rate, barStart + 2 * beat, root, 1.2, 0.17, Bass);
                if (bar % 2 == 1) AddNote(buf, rate, barStart + 3.5 * beat, Midi(chord[2] - 12), 0.4, 0.12, Bass);
                // sparse melody
                for (int e = 0; e < 8; e++)
                {
                    if (rng.NextDouble() > 0.28) continue;
                    double swing = e % 2 == 1 ? 0.12 : 0;
                    AddNote(buf, rate, barStart + (e * 0.5 + swing) * beat, Midi(scale[rng.Next(scale.Length)]), 0.9, 0.045, Pluck);
                }
                // drums
                for (int e = 0; e < 8; e++)
                {
                    double swing = e % 2 == 1 ? 0.12 : 0;
                    double at = barStart + (e * 0.5 + swing) * beat;
                    if (e == 0 || e == 4 || (e == 5 && bar % 2 == 0)) AddNote(buf, rate, at, 0, 0.35, 0.5, Kick);
                    if (e == 2 || e == 6) AddNote(buf, rate, at, 0, 0.3, 0.16, Snare);
                    AddNote(buf, rate, at, 0, 0.08, e % 2 == 0 ? 0.035 : 0.022, Hat);
                }
                if (Spent()) yield return null;
            }

            // vinyl crackle + warm low-pass + gentle limiter
            double lp = 0, k = 1 - Math.Exp(-2 * Math.PI * 4200 / rate);
            for (int i = 0; i < length; i++)
            {
                double x = buf[i];
                if (rng.NextDouble() < 0.0006) x += (rng.NextDouble() - 0.5) * 0.08;
                x += (rng.NextDouble() - 0.5) * 0.004;
                lp += (x - lp) * k;
                buf[i] = (float)Math.Tanh(lp * 1.4) * 0.8f;
                if ((i & 0x3FFF) == 0 && Spent()) yield return null;
            }
            done(buf);
        }

        static double Midi(int n) => 440.0 * Math.Pow(2, (n - 69) / 12.0);

        delegate double Voice(double t, double hz, ref double state);

        /// <summary>Renders a note into the buffer, wrapping around the end so the loop is seamless.</summary>
        static void AddNote(float[] buf, int rate, double start, double hz, double duration, double gain, Voice voice)
        {
            int s0 = (int)(start * rate);
            int n = (int)(duration * rate);
            double state = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate;
                buf[(s0 + i) % buf.Length] += (float)(voice(t, hz, ref state) * gain);
            }
        }

        static double S(double t, double hz) => Math.Sin(2 * Math.PI * hz * t);

        static double Rhodes(double t, double hz, ref double st)
        {
            double env = Math.Min(1, t / 0.008) * Math.Exp(-t / 1.1);
            double trem = 1 + 0.12 * Math.Sin(2 * Math.PI * 4.5 * t);
            return (S(t, hz) + 0.35 * S(t, hz * 2.001) * Math.Exp(-t / 0.3) + 0.12 * S(t, hz * 3) * Math.Exp(-t / 0.12)) * env * trem;
        }

        static double Bass(double t, double hz, ref double st)
        {
            double env = Math.Min(1, t / 0.01) * Math.Exp(-t / 0.7);
            return Math.Tanh(1.6 * (S(t, hz) + 0.25 * S(t, hz * 2))) * env;
        }

        static double Pluck(double t, double hz, ref double st) =>
            (S(t, hz) + 0.2 * S(t, hz * 2)) * Math.Min(1, t / 0.004) * Math.Exp(-t / 0.25);

        static double Kick(double t, double hz, ref double st)
        {
            double f = 45 + 80 * Math.Exp(-t / 0.035);
            st += 2 * Math.PI * f / 32000.0;
            return Math.Sin(st) * Math.Exp(-t / 0.16);
        }

        [ThreadStatic] static System.Random _noise;
        static double N() => ((_noise ??= new System.Random(11)).NextDouble() * 2 - 1);

        static double Snare(double t, double hz, ref double st)
        {
            double n = N();
            st += (n - st) * 0.35;                 // soften the noise a little
            return (st * 0.8 + 0.3 * S(t, 185)) * Math.Exp(-t / 0.09);
        }

        static double Hat(double t, double hz, ref double st)
        {
            double n = N();
            double hp = n - st;                    // crude high-pass
            st = n;
            return hp * 0.5 * Math.Exp(-t / 0.02);
        }
    }
}
