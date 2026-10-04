using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentClicker.Core;
using AgentClicker.Office;
using Unity.Profiling;
using UnityEngine;

namespace AgentClicker
{
    /// <summary>
    /// Performance benchmark: AgentClicker.x86_64 -benchmark
    /// Loads a late-game office, then measures uncapped frame times, GC allocations and render stats
    /// in a few typical situations. Results go to the player log as "[Bench]" lines, then it quits.
    /// </summary>
    public class Benchmark : MonoBehaviour
    {
        GameManager _gm;
        GameModel M => _gm.Model;

        IEnumerator Start()
        {
            _gm = GetComponent<GameManager>();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            _gm.SuppressShowcase = true;
            yield return new WaitForSeconds(1.5f);

            // late-game office with everything installed
            var s = M.State;
            s.day = 22;
            s.lifetimeEarned = 2e11;
            int[] late = { 150, 120, 100, 100, 80, 60, 40, 25, 8, 2 };
            for (int i = 0; i < late.Length; i++) s.agentCounts[i] = late[i];
            M.MarkDirty();
            foreach (var o in GameDatabase.OfficeItems)
                if (o.Id != "recliner") { s.credits = 1e12; M.BuyOffice(o.Id); }
            s.titleIndex = 5;
            s.dayMinutes = 6.5f * 60;
            _gm.Office.Refresh(false);
            _gm.Settings.autoOpenStoryMail = false;
            _gm.Login();
            _gm.Computer.CloseModal();
            yield return new WaitForSeconds(2f);

            _gm.Cam.SetMode(CamMode.Office, 0.01f);
            yield return new WaitForSeconds(1f);
            yield return Measure("office view, idle", 5f, false);

            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            yield return new WaitForSeconds(1f);
            yield return Measure("monitor view, idle", 5f, false);
            yield return Measure("monitor view, clicking 15/s", 5f, true);

            _gm.Cam.SetMode(CamMode.Office, 0.01f);
            yield return new WaitForSeconds(1f);
            yield return Measure("office view, clicking 15/s", 5f, true);

            Application.targetFrameRate = 60;
            yield return Measure("monitor view, 60 fps cap, clicking 15/s (10 s)", 10f, true);
            Application.targetFrameRate = -1;
            yield return Markers("late game");

            // ---- endless: hundreds of hours in. Full 20-type fleet, hundreds of upgrades and trophies, huge numbers.
            s.factoryBuilt = true;
            s.reorgs = 12;
            s.optionsEarned = 4e9;
            s.lifetimeEarned = s.allTimeEarned = 3e40;
            s.credits = 2e39;
            for (int i = 0; i < s.agentCounts.Length; i++) s.agentCounts[i] = 500 - i * 20;
            foreach (var u in GameDatabase.Upgrades) if (u.Kind != UpgradeKind.AgentTier || u.Tier <= 9) s.upgrades.Add(u.Id);
            M.Load(s);
            M.State.Phase = GamePhase.Working;
            M.CheckAchievements();
            _gm.Office.Refresh(false);
            _gm.Computer.ShowDesktop();
            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            yield return new WaitForSeconds(2f);
            Debug.Log($"[Bench] endless state: {M.TotalAgents} agents, {s.upgrades.Count} upgrades, {M.AvailableUpgradesCached.Count} available, " +
                      $"{M.AchievementCount} trophies, {NumberFormat.Rate(M.Cps)}");
            yield return Measure("endless: monitor, agents tab, clicking 15/s", 5f, true);
            _gm.Computer.SelectStoreTab(1);
            yield return new WaitForSeconds(0.5f);
            yield return Measure("endless: upgrades tab, clicking 15/s", 5f, true);
            _gm.Computer.SelectStoreTab(UI.StorePanel.TrophiesTabIndex);
            yield return new WaitForSeconds(0.5f);
            yield return Measure("endless: trophies tab, clicking 15/s", 5f, true);
            _gm.Computer.SelectStoreTab(UI.StorePanel.StatsTabIndex);
            yield return new WaitForSeconds(0.5f);
            yield return Measure("endless: stats tab, clicking 15/s", 5f, true);
            _gm.Computer.SelectStoreTab(0);
            Application.targetFrameRate = 60;
            yield return Measure("endless: 60 fps cap, clicking 15/s (10 s)", 10f, true);
            Application.targetFrameRate = -1;
            yield return Markers("endless");
            yield return AllocBisect();
            Debug.Log("[Bench] done");
            Application.Quit();
        }

        /// <summary>Per-subsystem main-thread cost via profiler markers (development builds only).</summary>
        IEnumerator Markers(string phase)
        {
            if (!Debug.isDebugBuild) yield break;
            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            Application.targetFrameRate = 60;
            string[] names =
            {
                "BehaviourUpdate", "LateBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
                "EventSystem.Update", "Canvas.SendWillRenderCanvases", "PostLateUpdate.PlayerUpdateCanvases",
                "UGUI.Rendering.UpdateBatches", "Canvas.BuildBatch", "Layout", "Animators.Update", "Director.ProcessFrame",
                "PlayerLoop", "RenderPipelineManager.DoRenderLoop_Internal()", "Inl_UniversalRenderPipeline.RenderSingleCameraInternal",
                "TextMeshPro.UpdateMesh", "TMP.GenerateText", "Physics.Processing", "Update.ScriptRunBehaviourUpdate",
            };
            var recs = names.Select(n => (n, ProfilerRecorder.StartNew(new ProfilerCategory("Any"), n, 1))).ToList();
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            var sums = new double[names.Length];
            double gcSum = 0;
            int frames = 0;
            float t = 0, clickAcc = 0;
            while (t < 6f)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                frames++;
                for (int i = 0; i < recs.Count; i++) if (recs[i].Item2.Valid) sums[i] += recs[i].Item2.LastValue;
                if (gc.Valid) gcSum += gc.LastValue;
                clickAcc += Time.unscaledDeltaTime * 8f;
                while (clickAcc >= 1) { clickAcc -= 1; _gm.Computer.ShipFromKeyboard(); }
            }
            for (int i = 0; i < recs.Count; i++)
            {
                var (n, r) = recs[i];
                Debug.Log(r.Valid ? $"[Bench] {phase} marker {n}: {sums[i] / frames / 1e6:0.000} ms/frame" : $"[Bench] {phase} marker {n}: n/a");
                r.Dispose();
            }
            Debug.Log($"[Bench] {phase} GC allocated: {(gc.Valid ? $"{gcSum / frames:0} B/frame" : "n/a")}");
            gc.Dispose();
            Application.targetFrameRate = -1;
        }

        /// <summary>Disables one system at a time to see who allocates managed memory every frame.</summary>
        IEnumerator AllocBisect()
        {
            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            yield return new WaitForSeconds(0.5f);
            var candidates = new List<(string, Behaviour[])>
            {
                ("(baseline)", new Behaviour[0]),
                ("EventSystem", new Behaviour[] { UnityEngine.EventSystems.EventSystem.current }),
                ("ComputerUI", new Behaviour[] { _gm.Computer }),
                ("Overlay", new Behaviour[] { _gm.Overlay }),
                ("SideScreens", FindObjectsByType<UI.SideScreen>().Cast<Behaviour>().ToArray()),
                ("CameraRig", new Behaviour[] { _gm.Cam }),
                ("TimeOfDay+GameManager", new Behaviour[] { _gm }),
                ("Employee", new Behaviour[] { _gm.Employee }),
                ("CallUI", new Behaviour[] { _gm.Calls }),
                ("MenuUI", new Behaviour[] { _gm.Menu }),
                ("Sfx", new Behaviour[] { _gm.Sfx }),
                ("All canvases", FindObjectsByType<Canvas>().Cast<Behaviour>().ToArray()),
            };
            foreach (var (name, list) in candidates)
            {
                foreach (var b in list) if (b) b.enabled = false;
                yield return null;
                var gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                long sum = 0, last = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                int frames = 0;
                float t = 0;
                while (t < 2f)
                {
                    yield return null;
                    t += Time.unscaledDeltaTime;
                    frames++;
                    if (gcRec.Valid) sum += gcRec.LastValue;
                    else
                    {
                        long h = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                        if (h > last) sum += h - last;
                        last = h;
                    }
                }
                gcRec.Dispose();
                Debug.Log($"[Bench] alloc without {name}: {sum / (float)frames:0} B/frame");
                foreach (var b in list) if (b) b.enabled = true;
            }
        }

        IEnumerator Measure(string label, float seconds, bool clicking)
        {
            var frames = new List<float>();
            var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            long allocSum = 0, batchSum = 0, setPassSum = 0, triSum = 0, cpuSum = 0;
            long lastHeap = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            int gc0 = System.GC.CollectionCount(0);
            float t = 0, clickAcc = 0;
            while (t < seconds)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                t += dt;
                frames.Add(dt * 1000f);
                long heap = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                if (heap > lastHeap) allocSum += heap - lastHeap;
                lastHeap = heap;
                if (cpu.Valid) cpuSum += cpu.LastValue;
                if (batches.Valid) batchSum += batches.LastValue;
                if (setPass.Valid) setPassSum += setPass.LastValue;
                if (tris.Valid) triSum += tris.LastValue;
                if (clicking)
                {
                    clickAcc += dt * 15f;
                    while (clickAcc >= 1) { clickAcc -= 1; _gm.Computer.ShipFromKeyboard(); }
                }
            }
            int gcs = System.GC.CollectionCount(0) - gc0;
            frames.Sort();
            int n = frames.Count;
            float avg = frames.Average();
            float p95 = frames[Mathf.Min(n - 1, (int)(n * 0.95f))];
            float p99 = frames[Mathf.Min(n - 1, (int)(n * 0.99f))];
            Debug.Log($"[Bench] {label}: {n / seconds:0} fps, avg {avg:0.00} ms, p95 {p95:0.00} ms, p99 {p99:0.00} ms, max {frames[n - 1]:0.0} ms | " +
                      $"cpu {(cpu.Valid ? $"{cpuSum / (double)n / 1e6:0.00} ms" : "n/a")} | " +
                      $"GC {allocSum / seconds / 1024f:0} KB/s, {gcs} collections | " +
                      $"batches {(batches.Valid ? batchSum / n : -1)}, setpass {(setPass.Valid ? setPassSum / n : -1)}, tris {(tris.Valid ? triSum / n / 1000 : -1)}K");
            cpu.Dispose(); batches.Dispose(); setPass.Dispose(); tris.Dispose();
        }
    }
}
