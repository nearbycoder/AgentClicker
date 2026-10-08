using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentClicker.Core;
using AgentClicker.Office;
using Unity.Profiling;
using UnityEngine;

namespace AgentClicker
{
    /// <summary>
    /// Graphics fidelity, step by step: AgentClicker.x86_64 -fidelity &lt;dir&gt; (Tools/fidelity.sh).
    /// In the late-game office it holds one frame still in each of three views (the office, the title screen, the monitor)
    /// and screenshots it at every step, so the steps can be compared pixel for pixel; then it measures uncapped frame times
    /// for every step in every view. Results go to the player log as "[Fidelity]" lines; the player's settings are put back.
    /// </summary>
    public class FidelityCheck : MonoBehaviour
    {
        public string OutputDir;
        GameManager _gm;
        int _original;

        static readonly (string id, string label, CamMode mode)[] Views =
        {
            ("office", "office view", CamMode.Office),
            ("title", "title screen", CamMode.Menu),
            ("monitor", "monitor view", CamMode.Monitor),
        };

        IEnumerator Start()
        {
            _gm = GetComponent<GameManager>();
            Directory.CreateDirectory(OutputDir);
            _original = _gm.Settings.quality;
            _gm.SuppressShowcase = true;
            yield return new WaitForSeconds(1.5f);
            Benchmark.LateGameOffice(_gm);
            yield return new WaitForSeconds(2f);
            Debug.Log($"[Fidelity] {Screen.width}x{Screen.height}, {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), " +
                      $"frame timing stats {(FrameTimingManager.IsFeatureEnabled() ? "on" : "off")}");

            foreach (var v in Views)
            {
                Enter(v.mode);
                yield return new WaitForSeconds(1.6f);
                yield return Stills(v.id);
            }
            // two passes through the steps, interleaved, so a burst of load elsewhere on the machine hits one sample of a
            // step rather than the whole step; the summary line takes the better of the two
            int n = Fidelity.Steps.Length;
            foreach (var v in Views)
            {
                Enter(v.mode);
                yield return new WaitForSeconds(1.6f);
                var avg = new float[2, n];
                var gpu = new float[2, n];
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < n; i++)
                    {
                        int step = i, at = pass;
                        yield return Measure($"{v.label} (pass {pass + 1})", step, 4f, (ms, g) => { avg[at, step] = ms; gpu[at, step] = g; });
                    }
                for (int i = 0; i < n; i++)
                {
                    float best = Mathf.Min(avg[0, i], avg[1, i]), g = Mathf.Min(gpu[0, i], gpu[1, i]);
                    // GPU time comes from FrameTimingManager (Vulkan here; OpenGL doesn't report it); it barely moves with CPU load
                    string gpuText = g > 0 ? $"gpu {g:0.00} ms (passes {gpu[0, i]:0.00} and {gpu[1, i]:0.00})" : "gpu n/a";
                    Debug.Log($"[Fidelity] summary {v.label} {Fidelity.Steps[i].Name}: frame {best:0.00} ms ({1000f / best:0} fps; passes " +
                              $"{avg[0, i]:0.00} and {avg[1, i]:0.00} ms) | {gpuText}");
                }
            }

            yield return TimesOfDay();

            _gm.Settings.quality = _original;
            _gm.ApplySettings(save: false);
            Debug.Log("[Fidelity] done");
            Application.Quit();
        }

        void Enter(CamMode mode)
        {
            if (mode == CamMode.Menu) _gm.Menu.ShowTitle();
            else _gm.Menu.HideAll();
            _gm.Cam.SetMode(mode, 0.01f);
        }

        void SetStep(int step)
        {
            _gm.Settings.quality = step;
            _gm.ApplySettings(save: false);
            if (_gm.FidelityFx) _gm.FidelityFx.SnapFocus();
        }

        /// <summary>The light through the window over a day, at the default step, from a view that takes in the window.</summary>
        IEnumerator TimesOfDay()
        {
            _gm.Menu.HideAll();
            SetStep(Fidelity.Default);
            QualitySettings.vSyncCount = 1;
            _gm.Computer.CloseModal();
            (int minutes, string name)[] times = { (30, "0930"), (390, "1530"), (500, "1720"), (640, "1940") };
            foreach (var (minutes, name) in times)
            {
                _gm.Model.State.dayMinutes = minutes;
                _gm.Cam.SetFixed(new Vector3(1.55f, 1.75f, -2.75f), new Vector3(-1.6f, 1.05f, -0.35f));
                yield return new WaitForSeconds(1.2f);
                _gm.Computer.CloseModal();
                yield return new WaitForEndOfFrame();
                string path = Path.Combine(OutputDir, $"time_{name}.png");
                ScreenCapture.CaptureScreenshot(path);
                yield return null;
                var atmo = _gm.Atmosphere;
                Debug.Log($"[Fidelity] time {name}: sunbeam {(atmo && atmo.BeamVisible ? $"on ({atmo.BeamIntensity:0.000})" : "off")}, dust {(atmo ? atmo.DustCount : 0)} ({path})");
            }
        }

        /// <summary>The same frozen frame at every step.</summary>
        IEnumerator Stills(string view)
        {
            float scale = Time.timeScale;
            Time.timeScale = 0f;
            for (int i = 0; i < Fidelity.Steps.Length; i++)
            {
                SetStep(i);
                // the reflection probe re-renders at its new size a moment after the change
                yield return new WaitForSecondsRealtime(0.6f);
                string path = Path.Combine(OutputDir, $"{view}_{i}_{Fidelity.Steps[i].Name.ToLowerInvariant()}.png");
                ScreenCapture.CaptureScreenshot(path);
                yield return null;
                yield return null;
                Debug.Log($"[Fidelity] still {path}");
            }
            Time.timeScale = scale;
        }

        IEnumerator Measure(string view, int step, float seconds, System.Action<float, float> result)
        {
            SetStep(step);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return new WaitForSeconds(1f);
            var frames = new List<float>();
            var gpu = new List<double>();
            var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
            long cpuSum = 0;
            var timings = new FrameTiming[1];
            float t = 0;
            while (t < seconds)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                t += dt;
                frames.Add(dt * 1000f);
                if (cpu.Valid) cpuSum += cpu.LastValue;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpu.Add(timings[0].gpuFrameTime);
            }
            frames.Sort();
            int n = frames.Count;
            float p95 = frames[Mathf.Min(n - 1, (int)(n * 0.95f))];
            string gpuText = gpu.Count > 0 ? $"{gpu.Average():0.00} ms" : "n/a";
            result(frames.Average(), gpu.Count > 0 ? (float)gpu.Average() : 0f);
            Debug.Log($"[Fidelity] {view} {Fidelity.Steps[step].Name}: {n / seconds:0} fps, avg {frames.Average():0.00} ms, p95 {p95:0.00} ms | " +
                      $"cpu {(cpu.Valid ? $"{cpuSum / (double)n / 1e6:0.00} ms" : "n/a")} | gpu {gpuText}");
            cpu.Dispose();
        }
    }
}
