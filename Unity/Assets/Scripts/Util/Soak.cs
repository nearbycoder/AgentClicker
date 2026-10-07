using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using AgentClicker.Core;
using UnityEngine;
using UnityEngine.Profiling;

namespace AgentClicker
{
    /// <summary>
    /// Long-session check: AgentClicker.x86_64 -soak /path/to/dir 60 -daylength 30
    /// A late-game office left alone (the day autopilot runs day after day) with a purchase burst every few minutes.
    /// Every 30 s it logs memory, object counts and frame times as "[Soak]" lines and to soak.csv, then quits.
    /// Never touches the real save. In the browser build the page passes the arguments (?arg=-soak&amp;arg=&amp;arg=30).
    /// </summary>
    public class Soak : MonoBehaviour
    {
        public string OutputDir;
        public float Minutes = 60f;
        public const float SampleSeconds = 30f, BurstSeconds = 180f;

        GameManager _gm;
        GameModel M => _gm.Model;
        float _frames, _frameSum, _frameMax;
        int _bursts;
        readonly StringBuilder _csv = new StringBuilder();

        IEnumerator Start()
        {
            _gm = GetComponent<GameManager>();
            yield return new WaitForSeconds(1.5f);

            // the benchmark's late-game office: every gadget but the recliner, 700 agents, story mail off
            var s = M.State;
            s.day = 22;
            s.lifetimeEarned = 2e11;
            int[] late = { 150, 120, 100, 100, 80, 60, 40, 25, 8, 2 };
            for (int i = 0; i < late.Length; i++) s.agentCounts[i] = late[i];
            M.MarkDirty();
            foreach (var o in GameDatabase.OfficeItems)
                if (o.Id != "recliner") { s.credits = 1e12; M.BuyOffice(o.Id); }
            s.credits = 0;
            s.titleIndex = 5;
            _gm.Office.Refresh(false);
            _gm.Settings.autoOpenStoryMail = false;
            _gm.Login();
            _gm.Computer.CloseModal();
            Debug.Log($"[Soak] start: {Minutes} min, day length {M.DayLengthSeconds} s, {SystemInfo.graphicsDeviceName}");

            _csv.AppendLine("seconds,day,phase,cps,frames,avg_ms,max_ms,gc_mb,mono_used_mb,mono_heap_mb,unity_alloc_mb,unity_reserved_mb,rss_mb,gameobjects,textures,materials,meshes,toasts");
            float start = Time.realtimeSinceStartup, next = start + SampleSeconds, nextBurst = start + BurstSeconds;
            int startDay = s.day;
            while (Time.realtimeSinceStartup - start < Minutes * 60f)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                _frames++;
                _frameSum += dt;
                _frameMax = Mathf.Max(_frameMax, dt);
                if (Time.realtimeSinceStartup >= nextBurst)
                {
                    nextBurst += BurstSeconds;
                    Burst();
                }
                if (Time.realtimeSinceStartup >= next)
                {
                    next += SampleSeconds;
                    Sample(Time.realtimeSinceStartup - start);
                }
            }
            Debug.Log($"[Soak] done: {M.State.day - startDay} days, {_bursts} purchase bursts");
            Flush();
            Application.Quit();
        }

        /// <summary>What a player dropping in now and then would do: spend everything on agents and upgrades.</summary>
        void Burst()
        {
            if (!M.IsWorking) return;
            _bursts++;
            int bought = 0;
            foreach (var u in M.AvailableUpgradesCached.ToArray())
                if (M.BuyUpgrade(u.Id)) bought++;
            for (int i = GameDatabase.CoreAgentCount - 1; i >= 0; i--)
            {
                int n = M.MaxAffordable(i);
                if (n > 0 && M.BuyAgent(i, n)) bought++;
            }
            Debug.Log($"[Soak] purchase burst {_bursts}: {bought} purchases, {M.TotalAgents} agents");
        }

        void Sample(float seconds)
        {
            const double MB = 1024.0 * 1024.0;
            var inv = CultureInfo.InvariantCulture;
            string line = string.Join(",",
                seconds.ToString("F0", inv), M.State.day, M.Phase, M.Cps.ToString("G4", inv), _frames,
                (_frames > 0 ? _frameSum / _frames * 1000f : 0).ToString("F2", inv), (_frameMax * 1000f).ToString("F1", inv),
                (GC.GetTotalMemory(false) / MB).ToString("F1", inv),
                (Profiler.GetMonoUsedSizeLong() / MB).ToString("F1", inv), (Profiler.GetMonoHeapSizeLong() / MB).ToString("F1", inv),
                (Profiler.GetTotalAllocatedMemoryLong() / MB).ToString("F1", inv), (Profiler.GetTotalReservedMemoryLong() / MB).ToString("F1", inv),
                Rss().ToString("F1", inv),
                Resources.FindObjectsOfTypeAll<GameObject>().Length, Resources.FindObjectsOfTypeAll<Texture>().Length,
                Resources.FindObjectsOfTypeAll<Material>().Length, Resources.FindObjectsOfTypeAll<Mesh>().Length,
                _gm.Computer.ToastCount);
            _csv.AppendLine(line);
            Debug.Log("[Soak] " + line);
            _frames = _frameSum = _frameMax = 0;
            Flush();
        }

        /// <summary>Resident memory of the whole process in MB (Linux), or -1.</summary>
        static double Rss()
        {
            try
            {
                foreach (var l in File.ReadLines("/proc/self/status"))
                    if (l.StartsWith("VmRSS:"))
                        return double.Parse(l.Substring(6).Trim().Split(' ')[0], CultureInfo.InvariantCulture) / 1024.0;
            }
            catch (Exception) { }
            return -1;
        }

        void Flush()
        {
            if (string.IsNullOrEmpty(OutputDir)) return;
            try
            {
                Directory.CreateDirectory(OutputDir);
                File.WriteAllText(Path.Combine(OutputDir, "soak.csv"), _csv.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[Soak] " + e.Message); }
        }
    }
}
