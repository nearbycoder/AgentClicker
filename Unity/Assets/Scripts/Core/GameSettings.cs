using System;
using UnityEngine;

namespace AgentClicker.Core
{
    /// <summary>Player preferences. Stored as JSON in PlayerPrefs, separate from the save game.</summary>
    [Serializable]
    public class GameSettings
    {
        public const string PrefsKey = "agentclicker.settings";

        public static readonly string[] QualityNames = { "Low", "Medium", "High", "Ultra" };
        public static readonly string[] DisplayModeNames = { "Windowed", "Borderless", "Fullscreen" };
        public static readonly int[] FpsCaps = { 30, 60, 120, 144, 240, 0 };
        public static readonly string[] FpsCapNames = { "30", "60", "120", "144", "240", "Unlimited" };
        public static readonly float[] DayLengths = { 180, 300, 480, 720 };
        public static readonly string[] DayLengthNames = { "3 min (fast)", "5 min (default)", "8 min", "12 min (relaxed)" };
        public static readonly string[] NumberStyleNames = { "Short (1.23 Qa)", "Scientific (1.23e15)" };

        // graphics
        public int quality = 2;
        public int displayMode;
        public int resolutionIndex = -1;   // -1: keep the current window size
        public bool vsync = true;
        public int fpsCap = 1;             // index into FpsCaps
        public float renderScale = 1f;
        public float fieldOfView = 50f;
        public bool postProcessing = true;
        public bool showFps;
        public bool throttleInBackground = true;

        // audio
        public float masterVolume = 0.8f;
        public float sfxVolume = 0.8f;
        public float musicVolume = 0.45f;
        public float ambienceVolume = 0.5f;
        public bool muteInBackground;

        // gameplay
        public int dayLength = 1;          // index into DayLengths
        public bool purchaseShowcase = true;
        public bool tutorialTips = true;
        public bool autoOpenStoryMail = true;
        public bool autopilotDay = true;   // run the day (clock out, go home, clock in, log in) when nobody is at the keyboard
        public float mouseSensitivity = 1f;
        public int numberStyle;            // NumberStyle

        public float DayLengthSeconds => DayLengths[Mathf.Clamp(dayLength, 0, DayLengths.Length - 1)];
        public int TargetFps => FpsCaps[Mathf.Clamp(fpsCap, 0, FpsCaps.Length - 1)];

        public static GameSettings Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var s = JsonUtility.FromJson<GameSettings>(json);
                    if (s != null) return s.Clamp();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Settings] could not read settings: " + e.Message);
            }
            return new GameSettings();
        }

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Clamp()));
            PlayerPrefs.Save();
        }

        public GameSettings Clamp()
        {
            quality = Mathf.Clamp(quality, 0, QualityNames.Length - 1);
            displayMode = Mathf.Clamp(displayMode, 0, DisplayModeNames.Length - 1);
            fpsCap = Mathf.Clamp(fpsCap, 0, FpsCaps.Length - 1);
            renderScale = Mathf.Clamp(renderScale, 0.5f, 1f);
            fieldOfView = Mathf.Clamp(fieldOfView, 40f, 70f);
            masterVolume = Mathf.Clamp01(masterVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            ambienceVolume = Mathf.Clamp01(ambienceVolume);
            dayLength = Mathf.Clamp(dayLength, 0, DayLengths.Length - 1);
            mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.25f, 3f);
            numberStyle = Mathf.Clamp(numberStyle, 0, NumberStyleNames.Length - 1);
            return this;
        }

        public GameSettings Clone() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));
    }
}
