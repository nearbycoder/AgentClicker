using System;
using System.IO;
using UnityEngine;

namespace AgentClicker.Core
{
    public static class SaveSystem
    {
        public static string Folder => Application.persistentDataPath;
        public static string FilePath => Path.Combine(Folder, "agentclicker_save.json");

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
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, ToJson(state));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] save failed: " + e.Message);
            }
        }

        public static GameState Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return FromJson(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] load failed, starting fresh: " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
