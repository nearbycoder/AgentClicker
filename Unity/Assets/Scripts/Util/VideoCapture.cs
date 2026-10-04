using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AgentClicker.Util
{
    /// <summary>
    /// Offline gameplay recorder. Locks the game to a fixed timestep (Time.captureFramerate), pipes every frame
    /// into ffmpeg (x264) and captures the game's own audio with AudioRenderer, then muxes both into an mp4.
    /// Because time only advances one frame per capture, the video is perfectly smooth however slow capture is.
    /// </summary>
    public class VideoCapture : MonoBehaviour
    {
        public string OutputPath;
        public int Fps = 30;
        public bool IsRecording { get; private set; }

        Process _ffmpeg;
        Stream _stdin;
        Texture2D _frame;
        string _videoTmp, _audioTmp;
        readonly List<float> _audio = new List<float>();
        int _channels, _sampleRate, _frames;

        public void Begin()
        {
            if (IsRecording) return;
            int w = Screen.width & ~1, h = Screen.height & ~1;
            _frame = new Texture2D(w, h, TextureFormat.RGB24, false);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputPath)));
            _videoTmp = Path.ChangeExtension(OutputPath, ".video.mp4");
            _audioTmp = Path.ChangeExtension(OutputPath, ".audio.wav");

            var psi = new ProcessStartInfo("ffmpeg",
                $"-y -loglevel error -f rawvideo -pix_fmt rgb24 -s {w}x{h} -r {Fps} -i - -vf vflip " +
                $"-c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -movflags +faststart \"{_videoTmp}\"")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            _ffmpeg = Process.Start(psi);
            _stdin = _ffmpeg.StandardInput.BaseStream;

            _channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            _sampleRate = AudioSettings.outputSampleRate;
            Time.captureFramerate = Fps;
            bool audioOk = AudioRenderer.Start();
            Debug.Log($"[Video] recording {w}x{h}@{Fps} → {OutputPath} (audio capture: {audioOk}, {_sampleRate} Hz x{_channels})");
            IsRecording = true;
            StartCoroutine(CaptureLoop());
        }

        IEnumerator CaptureLoop()
        {
            var wait = new WaitForEndOfFrame();
            while (IsRecording)
            {
                yield return wait;
                if (!IsRecording) break;
                _frame.ReadPixels(new Rect(0, 0, _frame.width, _frame.height), 0, 0, false);
                var data = _frame.GetRawTextureData<byte>();
                _stdin.Write(data.ToArray(), 0, data.Length);
                _frames++;

                int samples = AudioRenderer.GetSampleCountForCaptureFrame();
                if (samples > 0)
                {
                    using var buf = new NativeArray<float>(samples * _channels, Allocator.Temp);
                    if (AudioRenderer.Render(buf)) _audio.AddRange(buf);
                }
                if (_frames % 300 == 0) Debug.Log($"[Video] {_frames} frames ({_frames / (float)Fps:0}s)");
            }
        }

        /// <summary>Stops recording and writes the final mp4. Blocks while ffmpeg finishes.</summary>
        public void End()
        {
            if (!IsRecording) return;
            IsRecording = false;
            AudioRenderer.Stop();
            Time.captureFramerate = 0;
            _stdin.Flush();
            _stdin.Close();
            _ffmpeg.WaitForExit();
            WriteWav(_audioTmp, _audio, _channels, _sampleRate);
            var mux = Process.Start(new ProcessStartInfo("ffmpeg",
                $"-y -loglevel error -i \"{_videoTmp}\" -i \"{_audioTmp}\" -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart \"{OutputPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            mux.WaitForExit();
            if (mux.ExitCode == 0)
            {
                File.Delete(_videoTmp);
                File.Delete(_audioTmp);
            }
            Debug.Log($"[Video] done: {OutputPath} ({_frames} frames, {_frames / (float)Fps:0.0}s, audio {_audio.Count / (float)Math.Max(1, _channels * _sampleRate):0.0}s, mux exit {mux.ExitCode})");
        }

        static void WriteWav(string path, List<float> samples, int channels, int rate)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = samples.Count * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(rate);
            w.Write(rate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (var s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767));
        }
    }
}
