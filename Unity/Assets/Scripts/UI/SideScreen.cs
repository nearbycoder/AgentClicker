using System.Collections.Generic;
using System.Linq;
using System.Text;
using AgentClicker.Core;
using AgentClicker.Office;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>Live dashboards on the extra monitors bought in the Office shop.</summary>
    public class SideScreen : MonoBehaviour
    {
        public Renderer Screen;
        public SideScreenKind Kind;

        const float W = 1600, H = 900;
        GameManager _gm;
        RectTransform _root;
        TextMeshProUGUI _main, _big;
        readonly List<Image> _bars = new List<Image>();
        readonly List<double> _samples = new List<double>();
        readonly List<string> _lines = new List<string>();
        readonly System.Random _rng = new System.Random();
        float _timer, _sampleTimer;
        readonly List<int> _owned = new List<int>();

        public void Init(GameManager gm)
        {
            _gm = gm;
            SceneRefs.ScreenFrame(Screen, out var center, out var frame, out var size);
            var go = new GameObject("SideCanvas " + Kind, typeof(RectTransform));
            go.transform.SetParent(Screen.transform.parent, true);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<RectMask2D>();
            _root = (RectTransform)go.transform;
            _root.sizeDelta = new Vector2(W, H);
            _root.SetPositionAndRotation(center + frame * Vector3.back * 0.0016f, frame);
            _root.localScale = Vector3.one * (size.x / W) / Mathf.Max(1e-6f, go.transform.parent.lossyScale.x);

            UIKit.Image(_root, "Bg", Theme.Hex("#0B1018")).rectTransform.Fill();
            string title = Kind switch
            {
                SideScreenKind.AgentLog => "agent-bus.log",
                SideScreenKind.Graph => "PRODUCTION",
                SideScreenKind.LabStatus => "LAB STATUS",
                _ => "SPRINT BOARD",
            };
            UIKit.Text(_root, "Title", title, 56, Theme.Accent, TextAlignmentOptions.TopLeft, UIFonts.Bold).rectTransform.TopLeft(50, 36, 1500, 70);
            _main = UIKit.Text(_root, "Main", "", 40, Theme.Text, TextAlignmentOptions.TopLeft,
                               Kind == SideScreenKind.AgentLog ? UIFonts.Mono : UIFonts.Medium);
            _main.rectTransform.TopLeft(50, 130, 1500, 740);
            _main.overflowMode = TextOverflowModes.Masking;
            _main.richText = true;

            if (Kind == SideScreenKind.Graph)
            {
                _big = UIKit.Text(_root, "Big", "", 120, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
                _big.rectTransform.TopLeft(50, 120, 1500, 150);
                _main.rectTransform.TopLeft(50, 270, 1500, 60);
                for (int i = 0; i < 40; i++)
                {
                    var bar = UIKit.Panel(_root, "Bar" + i, Theme.Accent, 4);
                    var rt = bar.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    rt.pivot = new Vector2(0, 0);
                    rt.anchoredPosition = new Vector2(50 + i * 37.5f, 50);
                    rt.sizeDelta = new Vector2(30, 10);
                    _bars.Add(bar);
                }
            }
        }

        void Update()
        {
            if (_gm == null || !Screen.gameObject.activeInHierarchy) return;
            var m = _gm.Model;
            _sampleTimer -= Time.deltaTime;
            if (_sampleTimer <= 0)
            {
                _sampleTimer = 3f;
                _samples.Add(m.Cps);
                if (_samples.Count > _bars.Count && _bars.Count > 0) _samples.RemoveAt(0);
            }
            _timer -= Time.deltaTime;
            if (_timer > 0) return;
            _timer = Kind == SideScreenKind.AgentLog ? 0.6f : 0.5f;

            switch (Kind)
            {
                case SideScreenKind.AgentLog: UpdateLog(m); break;
                case SideScreenKind.Graph: UpdateGraph(m); break;
                case SideScreenKind.LabStatus: UpdateLabs(m); break;
                case SideScreenKind.Kanban: UpdateKanban(m); break;
            }
        }

        void UpdateLog(GameModel m)
        {
            string line;
            _owned.Clear();
            for (int i = 0; i < GameDatabase.Agents.Length; i++) if (m.AgentCount(i) > 0) _owned.Add(i);
            var owned = _owned;
            if (owned.Count == 0 || !m.IsWorking) line = "<color=#828EA5>idle… waiting for agents</color>";
            else
            {
                int i = owned[_rng.Next(owned.Count)];
                var a = GameDatabase.Agents[i];
                line = $"<color={GameDatabase.Lab(a.LabId).ColorHex}>[{a.Monogram}]</color> {FlavorText.AgentLog(a, _rng.Next(1, m.AgentCount(i) + 1), _rng)}";
            }
            _lines.Add(line);
            while (_lines.Count > 15) _lines.RemoveAt(0);
            _main.text = string.Join("\n", _lines);
        }

        void UpdateGraph(GameModel m)
        {
            _big.text = NumberFormat.Rate(m.Cps);
            _main.text = $"<color=#A4AFC2>automation</color> {NumberFormat.Percent(m.Automation)}   <color=#A4AFC2>today</color> {NumberFormat.Short(m.State.earnedToday)}";
            double max = 1e-9;
            foreach (var v in _samples) if (v > max) max = v;
            for (int i = 0; i < _bars.Count; i++)
            {
                int s = i - (_bars.Count - _samples.Count);
                float h = s >= 0 && max > 0 ? (float)(_samples[s] / max) : 0;
                _bars[i].rectTransform.sizeDelta = new Vector2(30, Mathf.Max(6, h * 420));
                _bars[i].color = Color.Lerp(Theme.Accent2, Theme.Accent, h);
            }
        }

        void UpdateLabs(GameModel m)
        {
            var sb = new StringBuilder();
            foreach (var lab in GameDatabase.Labs)
            {
                bool down = m.ActiveOutage != null && m.ActiveOutage.LabName == lab.Name;
                int agents = m.LabAgentCount(lab.Id);
                sb.Append(down ? "<color=#FF5D5D>●  DOWN</color>   " : "<color=#3DDC97>●  OK</color>       ");
                sb.Append($"<color={lab.ColorHex}>{lab.Name}</color>  <color=#828EA5>{agents} agents · {99.9 - (lab.Name.Length % 5) * 0.01:0.00}% uptime</color>\n");
            }
            _main.text = sb.ToString();
        }

        void UpdateKanban(GameModel m)
        {
            int done = (int)(m.State.earnedToday / Mathf.Max(1, (float)m.State.quotaToday) * 100);
            int doing = Mathf.Clamp(m.TotalAgents, 0, 99);
            _main.text =
                $"<color=#A4AFC2>TODO</color>      {Mathf.Max(0, 100 - done)}\n" +
                $"<color=#FFB020>DOING</color>     {doing}\n" +
                $"<color=#3DDC97>DONE</color>      {done}\n\n" +
                $"<size=70%><color=#828EA5>Velocity: {NumberFormat.Rate(m.Cps)} · Sprint {1 + (m.State.day - 1) / 5} · Day {m.State.day}</color></size>";
        }
    }
}
