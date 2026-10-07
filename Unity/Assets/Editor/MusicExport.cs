using System.IO;
using AgentClicker.Util;
using UnityEngine;

namespace AgentClicker.EditorTools
{
    /// <summary>
    /// Writes the synthesised music as 16-bit WAV files, for listening to it and for spectrograms:
    ///   Tools/unity.sh exec AgentClicker.EditorTools.MusicExport.Run   → Logs/music-loop.wav, Logs/music-long.wav
    /// </summary>
    public static class MusicExport
    {
        const int Rate = 32000;

        public static void Run()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Logs"));
            Directory.CreateDirectory(dir);
            Write(Path.Combine(dir, "music-loop.wav"), LofiMusic.Render(Rate, 7));
            Write(Path.Combine(dir, "music-long.wav"), LofiMusic.Render(Rate, 7, song: true));
            Debug.Log("[Music] wrote " + dir + "/music-loop.wav and music-long.wav");
        }

        static void Write(string path, float[] x)
        {
            using var w = new BinaryWriter(File.Create(path));
            w.Write("RIFF".ToCharArray()); w.Write(36 + x.Length * 2); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(x.Length * 2);
            foreach (float s in x) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }
}
