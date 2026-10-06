using System;
using System.IO;
using UnityEngine;

namespace AgentClicker.Core
{
    /// <summary>
    /// The save game: one JSON file in <see cref="Folder"/>. Writes go to a temp file that then replaces the save
    /// (the previous save is kept as a .bak), so a crash mid-save never leaves you without a career.
    /// </summary>
    public static class SaveSystem
    {
        static string _folder;

        /// <summary>Defaults to Unity's persistentDataPath. Tests point it at a temp folder.</summary>
        public static string Folder
        {
            get => _folder ?? Application.persistentDataPath;
            set => _folder = value;
        }

        public static string FilePath => Path.Combine(Folder, "agentclicker_save.json");
        public static string BackupPath => FilePath + ".bak";
        public static string TempPath => FilePath + ".tmp";

        /// <summary>Called after every successful write (the browser build flushes it to IndexedDB).</summary>
        public static Action Written;

        public static string ToJson(GameState state) => JsonUtility.ToJson(state, true);

        public static GameState FromJson(string json)
        {
            var state = JsonUtility.FromJson<GameState>(json);
            state?.Normalize();
            return state;
        }

        public static void Save(GameState state)
        {
            try
            {
                state.lastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Directory.CreateDirectory(Folder);
                File.WriteAllText(TempPath, ToJson(state));
                if (!File.Exists(FilePath)) File.Move(TempPath, FilePath);
                else
                {
                    try
                    {
                        File.Replace(TempPath, FilePath, BackupPath); // atomic swap, previous save becomes the .bak
                    }
                    catch (Exception e) when (e is PlatformNotSupportedException || e is IOException || e is UnauthorizedAccessException)
                    {
                        // no Replace on this file system: keep the old save as .bak first, then overwrite
                        File.Copy(FilePath, BackupPath, true);
                        File.Copy(TempPath, FilePath, true);
                        File.Delete(TempPath);
                    }
                }
                Written?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] save failed: " + e.Message);
            }
        }

        /// <summary>
        /// The save, or null for a fresh start. If the main file is missing or unreadable, falls back to the newest
        /// readable of the interrupted write (.tmp) and the previous save (.bak).
        /// </summary>
        public static GameState Load()
        {
            var main = TryRead(FilePath);
            if (main != null) return main;
            var tmp = TryRead(TempPath);
            var bak = TryRead(BackupPath);
            var best = tmp == null ? bak : bak == null ? tmp : tmp.lastSaveUnix >= bak.lastSaveUnix ? tmp : bak;
            if (best != null)
                Debug.LogWarning($"[SaveSystem] main save {(File.Exists(FilePath) ? "unreadable" : "missing")}, recovered from {(best == tmp ? TempPath : BackupPath)}");
            return best;
        }

        static GameState TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return FromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] could not read {Path.GetFileName(path)}: {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            foreach (var p in new[] { FilePath, BackupPath, TempPath })
                if (File.Exists(p)) File.Delete(p);
            Written?.Invoke();
        }
    }
}
