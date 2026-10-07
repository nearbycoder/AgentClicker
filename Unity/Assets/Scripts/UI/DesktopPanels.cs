using System.Collections.Generic;
using System.Linq;
using System.Text;
using AgentClicker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>CorpOS top bar: logo, ticker, clock, inbox, view toggle, menu and the clock-out button.</summary>
    public class TopBar
    {
        readonly GameManager _gm;
        readonly ComputerUI _ui;
        readonly TextMeshProUGUI _clock, _ticker, _viewLabel, _inboxLabel, _corp, _soundLabel;
        readonly Button _clockOut, _inbox;
        readonly Image _soundSlash;
        readonly Pulse _inboxPulse;
        float _tickerTimer;

        public TopBar(RectTransform parent, GameManager gm, ComputerUI ui)
        {
            _gm = gm;
            _ui = ui;
            var bar = UIKit.Image(parent, "TopBar", Theme.Hex("#0A0F18"));
            bar.rectTransform.TopLeft(0, 0, 1600, 46);
            UIKit.Image(bar.transform, "Line", Theme.Border).rectTransform.Anchor(0, 0, 1, 0).Insets(0, 0, 0, -1);

            var logo = UIKit.Text(bar.transform, "Logo", $"<color={UIKit.Hex(Theme.Accent)}>◆</color> CorpOS", 22, Theme.Text,
                                  TextAlignmentOptions.MidlineLeft, UIFonts.Bold);
            logo.rectTransform.TopLeft(18, 0, 160, 46);
            _corp = UIKit.Text(bar.transform, "Corp", FlavorText.Company + " Intranet", 15, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            _corp.rectTransform.TopLeft(150, 0, 200, 46);
            _corp.textWrappingMode = TextWrappingModes.NoWrap;

            _ticker = UIKit.Text(bar.transform, "Ticker", "", 15, Theme.TextDim, TextAlignmentOptions.Midline);
            _ticker.rectTransform.TopLeft(350, 0, 460, 46);
            // italic by tag, not fontStyle: TMP only finds the "…" for a long ticker in a component without a style

            _clock = UIKit.Text(bar.transform, "Clock", "", 18, Theme.Text, TextAlignmentOptions.MidlineRight, UIFonts.Medium);
            _clock.rectTransform.TopLeft(810, 0, 282, 46);

            _inbox = UIKit.Button(bar.transform, "Inbox", Theme.PanelLight, () => _ui.ShowInbox(null), 8);
            _inbox.GetComponent<RectTransform>().TopLeft(1104, 8, 120, 30);
            _inboxLabel = _inbox.Label("✉ Inbox", 14, Theme.Text, UIFonts.Medium);
            _inboxPulse = _inbox.gameObject.AddComponent<Pulse>();
            _inboxPulse.ScaleAmount = 0.04f;

            var view = UIKit.Button(bar.transform, "View", Theme.PanelLight, () => _gm.Cam.Toggle(), 8);
            view.GetComponent<RectTransform>().TopLeft(1232, 8, 140, 30);
            _viewLabel = view.Label("Look around  [Tab]", 14, Theme.TextDim, UIFonts.Medium);

            // all sound on or off (M); struck through while muted
            var sound = UIKit.Button(bar.transform, "Sound", Theme.PanelLight, () => _gm.ToggleMute(), 8);
            SoundButton = sound.GetComponent<RectTransform>();
            SoundButton.TopLeft(1380, 8, 40, 30);
            _soundLabel = sound.Label("♪", 18, Theme.TextDim, UIFonts.Mono);
            _soundSlash = UIKit.Image(sound.transform, "Slash", Theme.Bad);
            var slash = _soundSlash.rectTransform;
            slash.anchorMin = slash.anchorMax = slash.pivot = new Vector2(0.5f, 0.5f);
            slash.sizeDelta = new Vector2(26, 3);
            slash.localRotation = Quaternion.Euler(0, 0, -45);

            var menu = UIKit.Button(bar.transform, "Menu", Theme.PanelLight, () => _gm.Menu.OpenPause(), 8);
            menu.GetComponent<RectTransform>().TopLeft(1428, 8, 40, 30);
            menu.Label("⚙", 18, Theme.TextDim, UIFonts.Mono);

            _clockOut = UIKit.Button(bar.transform, "ClockOut", Theme.Warn, () => _gm.ClockOut(), 8);
            _clockOut.GetComponent<RectTransform>().TopLeft(1476, 8, 108, 30);
            _clockOut.Label("CLOCK OUT", 14, Theme.Bg);
            _clockOut.gameObject.AddComponent<Pulse>().ScaleAmount = 0.03f;
            _tickerTimer = 0;
        }

        /// <summary>The ♪ button (the tour clicks it).</summary>
        public RectTransform SoundButton { get; }

        public void Refresh(float dt)
        {
            var m = _gm.Model;
            UIKit.Set(_clock, $"Day {m.State.day} · {FlavorText.Weekday(m.State.day)} · <b>{NumberFormat.Clock(m.ClockHours)}</b>");
            string division = m.DivisionName;
            UIKit.Set(_corp, m.State.reorgs == 0 ? FlavorText.Company + " Intranet"
                : division.StartsWith("Synergex") ? division : "Synergex · " + division);
            _clock.color = m.PastFiveOClock ? Theme.Warn : Theme.Text;
            UIKit.SetActive(_clockOut, m.IsWorking && m.PastFiveOClock);
            bool monitor = _gm.Cam.Mode == Office.CamMode.Monitor;
            UIKit.Set(_viewLabel, _gm.Touch.Active ? (monitor ? "Look around" : "Sit down")
                                                   : (monitor ? "Look around  [Tab]" : "Sit down  [Tab]"));

            bool muted = _gm.Settings.muted;
            UIKit.SetActive(_soundSlash, muted);
            _soundLabel.color = muted ? Theme.TextFaint : Theme.TextDim;

            int unread = _ui.UnreadMail;
            UIKit.Set(_inboxLabel, unread > 0 ? $"✉ Inbox <color=#FFD166>{unread}</color>" : "✉ Inbox");
            _inboxPulse.enabled = unread > 0;

            _tickerTimer -= dt;
            if (_tickerTimer <= 0)
            {
                _tickerTimer = 14f;
                UIKit.Set(_ticker, "<i>" + (m.PastFiveOClock ? "It's after 5. Your agents don't mind overtime. Do you?" : _gm.RandomTicker()) + "</i>");
            }
        }
    }

    /// <summary>Left column: credits, the SHIP CODE terminal (the "cookie"), automation and today's quota.</summary>
    public class ShipPanel
    {
        readonly GameManager _gm;
        readonly ComputerUI _ui;
        readonly TextMeshProUGUI _credits, _rate, _buffs, _clickInfo, _autoPct, _today, _title, _next, _stars, _terminal;
        readonly Image _autoFill, _quotaFill;
        readonly RectTransform _button;
        readonly Punch _punch;
        readonly List<string> _lines = new List<string>();
        readonly System.Random _rng = new System.Random();
        readonly StringBuilder _sb = new StringBuilder();
        readonly TextMeshProUGUI[] _asks = new TextMeshProUGUI[2];
        TextMeshProUGUI _asksLabel, _focusLabel, _creditsLabel;
        string _shownCredits;
        int _asksKey = -1;
        Image _focusFill;

        public ShipPanel(RectTransform parent, GameManager gm, ComputerUI ui)
        {
            _gm = gm;
            _ui = ui;
            var col = UIKit.Rect("ShipColumn", parent).TopLeft(16, 62, 420, 822);

            // credits
            var card = UIKit.Panel(col, "Credits", Theme.Panel, 14);
            card.rectTransform.TopLeft(0, 0, 420, 132);
            // names the number once the suffixes stop being familiar: "COMPUTE CREDITS · 22.0 quadrillion"
            _creditsLabel = UIKit.Text(card.transform, "Label", "COMPUTE CREDITS", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _creditsLabel.rectTransform.TopLeft(20, 14, 380, 20);
            _creditsLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _credits = UIKit.Text(card.transform, "Amount", "0", 60, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _credits.rectTransform.TopLeft(18, 28, 390, 70);
            _credits.textWrappingMode = TextWrappingModes.NoWrap;
            _credits.overflowMode = TextOverflowModes.Overflow;
            _rate = UIKit.Text(card.transform, "Rate", "", 20, Theme.Accent, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _rate.rectTransform.TopLeft(20, 96, 240, 26);
            _buffs = UIKit.Text(card.transform, "Buffs", "", 15, Theme.Gold, TextAlignmentOptions.TopRight, UIFonts.Medium);
            _buffs.rectTransform.TopLeft(200, 98, 202, 24);

            // terminal + focus + ship button
            var term = UIKit.Panel(col, "Terminal", Theme.Terminal, 14, true);
            term.rectTransform.TopLeft(0, 144, 420, 360);
            UIKit.Text(term.transform, "Path", "~/synergex/monorepo <color=#828EA5>(main)</color>", 14, Theme.TextDim,
                       TextAlignmentOptions.TopLeft, UIFonts.Mono).rectTransform.TopLeft(18, 12, 384, 20);
            _terminal = UIKit.Text(term.transform, "Lines", "", 15, Theme.TerminalText, TextAlignmentOptions.BottomLeft, UIFonts.Mono);
            _terminal.rectTransform.TopLeft(18, 34, 384, 160);
            _terminal.overflowMode = TextOverflowModes.Masking;
            _terminal.textWrappingMode = TextWrappingModes.NoWrap;
            PointerRelay.On(term).Down = OnShip;

            _focusLabel = UIKit.Text(term.transform, "FocusLabel", "FOCUS", 14, Theme.TextDim, TextAlignmentOptions.MidlineLeft, UIFonts.Bold);
            _focusLabel.rectTransform.TopLeft(18, 200, 150, 20);
            UIKit.Bar(term.transform, "Focus", Theme.PanelLight, Theme.Gold, out _focusFill, 4).rectTransform.TopLeft(150, 206, 252, 8);

            var btn = UIKit.Panel(term.transform, "ShipButton", Theme.Accent, 18, true);
            _button = btn.rectTransform;
            _button.TopLeft(18, 230, 384, 112);
            var glow = UIKit.Panel(btn.transform, "Glow", Color.white.WithAlpha(0.18f), 18);
            glow.rectTransform.Fill(0);
            glow.rectTransform.Anchor(0, 0.5f, 1, 1).Insets(0, 0, 0, 0);
            var inner = UIKit.Text(btn.transform, "Label", "SHIP CODE", 44, Theme.Bg, TextAlignmentOptions.Center, UIFonts.Bold);
            inner.rectTransform.Fill().Insets(0, 24, 0, 0);
            _clickInfo = UIKit.Text(btn.transform, "Sub", "", 16, Theme.Bg.WithAlpha(0.75f), TextAlignmentOptions.Bottom, UIFonts.Medium);
            _clickInfo.rectTransform.Fill().Insets(0, 12, 0, 0);
            _punch = btn.gameObject.AddComponent<Punch>();
            var pulse = btn.gameObject.AddComponent<Pulse>();
            pulse.ScaleAmount = 0.012f;
            pulse.Speed = 2.2f;
            PointerRelay.On(btn).Down = OnShip;

            // automation
            var auto = UIKit.Panel(col, "Automation", Theme.Panel, 14);
            auto.rectTransform.TopLeft(0, 516, 420, 62);
            UIKit.Text(auto.transform, "Label", "AUTOMATION", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium)
                 .rectTransform.TopLeft(20, 12, 200, 20);
            _autoPct = UIKit.Text(auto.transform, "Pct", "0%", 22, Theme.Good, TextAlignmentOptions.TopRight, UIFonts.Bold);
            _autoPct.rectTransform.TopLeft(220, 6, 180, 28);
            UIKit.Bar(auto.transform, "Bar", Theme.PanelLight, Theme.Good, out _autoFill).rectTransform.TopLeft(20, 38, 380, 10);

            // today: quota, the manager's asks, career
            var today = UIKit.Panel(col, "Today", Theme.Panel, 14);
            today.rectTransform.TopLeft(0, 590, 420, 232);
            UIKit.Text(today.transform, "Label", "TODAY'S QUOTA", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium)
                 .rectTransform.TopLeft(20, 12, 200, 20);
            _stars = UIKit.Text(today.transform, "Stars", "", 18, Theme.Gold, TextAlignmentOptions.TopRight, UIFonts.Bold);
            _stars.rectTransform.TopLeft(220, 8, 180, 24);
            _today = UIKit.Text(today.transform, "Earned", "", 18, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _today.rectTransform.TopLeft(20, 34, 380, 24);
            UIKit.Bar(today.transform, "Bar", Theme.PanelLight, Theme.Accent, out _quotaFill).rectTransform.TopLeft(20, 62, 380, 10);
            _asksLabel = UIKit.Text(today.transform, "AsksLabel", "", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _asksLabel.rectTransform.TopLeft(20, 84, 380, 18);
            for (int i = 0; i < _asks.Length; i++)
            {
                _asks[i] = UIKit.Text(today.transform, "Ask" + i, "", 15, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Medium);
                _asks[i].rectTransform.TopLeft(20, 104 + i * 24, 380, 22);
                _asks[i].textWrappingMode = TextWrappingModes.NoWrap;
            }
            _title = UIKit.Text(today.transform, "Title", "", 17, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _title.rectTransform.TopLeft(20, 160, 380, 22);
            _next = UIKit.Text(today.transform, "Next", "", 14, Theme.TextDim, TextAlignmentOptions.TopLeft);
            _next.rectTransform.TopLeft(20, 184, 380, 40);

            for (int i = 0; i < 3; i++) AddLine(FlavorText.CodeLine(_rng), false);
        }

        void OnShip(PointerEventData e)
        {
            if (!_gm.Model.IsWorking) return;
            var r = _gm.Ship();
            _ui.SpawnFloat(e, r.Amount, r.Crit);
            _punch.Play(0.05f);
            AddLine(FlavorText.CodeLine(_rng), r.Crit);
        }

        public RectTransform Button => _button;

        public void ShipFromKeyboard()
        {
            var r = _gm.Ship();
            _ui.SpawnFloatAt(_button, r.Amount, r.Crit);
            _punch.Play(0.05f);
            AddLine(FlavorText.CodeLine(_rng), r.Crit);
        }

        public void OnAutoClick(ClickResult r)
        {
            AddLine("<color=#7C4DFF>[macro]</color> " + FlavorText.CodeLine(_rng), r.Crit);
        }

        void AddLine(string line, bool crit)
        {
            string prefix = crit ? "<color=#FFD166>★ debugged!</color> " : "<color=#4DD0E1>$</color> ";
            _lines.Add(prefix + line);
            while (_lines.Count > 9) _lines.RemoveAt(0); // nine lines fill the terminal without spilling over the path
            _sb.Clear();
            for (int i = 0; i < _lines.Count; i++)
            {
                if (i > 0) _sb.Append('\n');
                _sb.Append(_lines[i]);
            }
            _terminal.SetText(_sb);
        }

        /// <summary>The credits card's label (the tour checks it names the number and fits).</summary>
        public TextMeshProUGUI CreditsLabel => _creditsLabel;

        public void Refresh()
        {
            var m = _gm.Model;
            string shown = NumberFormat.Short(m.State.credits);
            if (shown != _shownCredits)
            {
                // the words only change when the digits do
                _shownCredits = shown;
                _credits.text = shown;
                string words = NumberFormat.Words(m.State.credits);
                UIKit.Set(_creditsLabel, words.Length == 0 ? "COMPUTE CREDITS" : "COMPUTE CREDITS  <color=#828EA5>·</color>  " + words);
            }
            _rate.text = $"+{NumberFormat.Rate(m.Cps)}";
            _sb.Clear();
            foreach (var b in m.Buffs) _sb.Append(b.Name).Append(" x").Append((int)b.Mult).Append(" · ").Append((int)b.Remaining).Append("s  ");
            if (m.ActiveOutage != null) _sb.Append("<color=#FF5D5D>OUTAGE x0.5</color>");
            if (_sb.Length > 0 || _buffs.text.Length > 0) UIKit.Set(_buffs, _sb.ToString());
            UIKit.Set(_clickInfo, $"+{NumberFormat.Short(m.ClickPower)} per click" + (m.CritChance > 0 ? $"  ·  {m.CritChance:P0} crit" : ""));
            UIKit.SetFill(_focusFill, m.Focus);
            UIKit.Set(_focusLabel, m.Focus > 0.01f ? $"FOCUS <color=#FFD166>x{m.FocusMult:0.0}</color>" : "FOCUS <color=#828EA5>keep clicking</color>");

            double a = m.Automation;
            _autoPct.text = NumberFormat.Percent(a);
            UIKit.SetFill(_autoFill, (float)a);

            double quota = m.State.quotaToday;
            bool met = m.State.earnedToday >= quota;
            _today.text = $"{NumberFormat.Short(m.State.earnedToday)} <color=#A4AFC2>/ {NumberFormat.Short(quota)}</color>" +
                          (met ? "  <color=#3DDC97>✓ met</color>" : "");
            UIKit.SetFill(_quotaFill, (float)(m.State.earnedToday / quota));
            _quotaFill.color = met ? Theme.Good : Theme.Accent;
            UIKit.Set(_stars, $"★ {m.State.stars}");
            UIKit.Set(_title, $"{FlavorText.EmployeeName} · {m.Title.Name}");
            var next = m.NextTitle;
            UIKit.Set(_next, next == null
                ? "You are at the top of the org chart."
                : $"Next promotion: {next.Name} at {NumberFormat.Short(next.Threshold)} lifetime credits " +
                  $"({NumberFormat.Percent(m.State.lifetimeEarned / next.Threshold)})");
            RefreshAsks(m);
        }

        void RefreshAsks(GameModel m)
        {
            // asks: rebuild strings only when something visible changed
            int asksKey = m.State.asks.Count * 1000 + (int)(m.State.agentDiscount * 100) + (FlavorText.ManagerName(m).Length << 12);
            for (int i = 0; i < m.State.asks.Count; i++)
                asksKey = asksKey * 31 + (m.State.asks[i].done ? 101 : (int)(m.AskProgress(m.State.asks[i]) * 100));
            if (asksKey == _asksKey) return;
            _asksKey = asksKey;
            UIKit.Set(_asksLabel, $"ASKS FROM {FlavorText.ManagerName(m).Split(' ')[0].ToUpperInvariant()}" +
                                  (m.State.agentDiscount > 0 ? $"   <color=#4DD0E1>{m.State.agentDiscount * 100:0}% OFF NEXT HIRE</color>" : ""));
            for (int i = 0; i < _asks.Length; i++)
            {
                if (i >= m.State.asks.Count) { UIKit.Set(_asks[i], ""); continue; }
                var ask = m.State.asks[i];
                var def = AskDatabase.ById(ask.id);
                string text = def != null ? def.Text(ask.target) : ask.id;
                UIKit.Set(_asks[i], ask.done
                    ? $"<color=#3DDC97>✓</color> <color=#828EA5><s>{text}</s></color>"
                    : $"<color=#A4AFC2>○</color> {text} <color=#A4AFC2>· {NumberFormat.Percent(m.AskProgress(ask))}</color>");
            }

        }
    }

    /// <summary>Middle column: the agent fleet and a live activity feed.</summary>
    public class FleetPanel
    {
        class Row
        {
            public GameObject Root;
            public RectTransform Icon, Bar;
            public TextMeshProUGUI Mono, Name, Count, Cps;
            public Image Fill;
            public int ShownCount = -1;
            public double ShownCps = -1;

            public void SetCompact(bool compact)
            {
                ((RectTransform)Root.transform).sizeDelta = new Vector2(528, compact ? 20 : 40);
                Icon.TopLeft(0, compact ? 1 : 2, compact ? 18 : 36, compact ? 18 : 36);
                Mono.fontSize = compact ? 9 : 15;
                Name.rectTransform.TopLeft(compact ? 26 : 48, 0, 230, compact ? 20 : 24);
                Name.fontSize = compact ? 14 : 17;
                Count.rectTransform.TopLeft(270, 0, 70, compact ? 20 : 24);
                Count.fontSize = compact ? 14 : 17;
                Cps.rectTransform.TopLeft(350, 0, 178, compact ? 20 : 40);
                Cps.fontSize = compact ? 14 : 16;
                Bar.gameObject.SetActive(!compact);
            }
        }

        bool _compact;
        const int MaxLogLines = 9; // what fits under the next goal card

        readonly GameManager _gm;
        readonly Row[] _rows;
        TextMeshProUGUI _goalTitle, _goalStatus, _goalEta;
        Image _goalFill;
        Goal _goal;
        readonly TextMeshProUGUI _header, _empty, _activity;
        readonly List<string> _log = new List<string>();
        readonly StringBuilder _logSb = new StringBuilder();
        // older lines fade out: precomputed <alpha> tags for each age
        static readonly string[] AlphaTags = BuildAlphaTags();

        static string[] BuildAlphaTags()
        {
            var tags = new string[MaxLogLines];
            for (int age = 0; age < tags.Length; age++)
                tags[age] = $"<alpha=#{(int)Mathf.Lerp(255, 90, age / (float)MaxLogLines):X2}>";
            return tags;
        }
        readonly System.Random _rng = new System.Random();
        float _logTimer;

        public FleetPanel(RectTransform parent, GameManager gm)
        {
            _gm = gm;
            var col = UIKit.Rect("FleetColumn", parent).TopLeft(452, 62, 560, 822);

            var card = UIKit.Panel(col, "Fleet", Theme.Panel, 14);
            card.rectTransform.TopLeft(0, 0, 560, 492);
            UIKit.Text(card.transform, "Title", "AGENT FLEET", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium)
                 .rectTransform.TopLeft(20, 14, 300, 20);
            _header = UIKit.Text(card.transform, "Count", "", 14, Theme.TextDim, TextAlignmentOptions.TopRight, UIFonts.Medium);
            _header.rectTransform.TopLeft(240, 14, 300, 20);
            _empty = UIKit.Text(card.transform, "Empty",
                "No agents yet.\n\nShip some code, then hire your first agent from the <color=#4DD0E1>ModelMart</color> →\n\n" +
                "<size=80%><color=#828EA5>Agents earn credits every second, even when you're not clicking.</color></size>",
                20, Theme.TextDim, TextAlignmentOptions.Center);
            _empty.rectTransform.TopLeft(40, 120, 480, 260);

            _rows = new Row[GameDatabase.Agents.Length];
            for (int i = 0; i < _rows.Length; i++)
            {
                var a = GameDatabase.Agents[i];
                var lab = GameDatabase.Lab(a.LabId);
                var root = UIKit.Rect("Row" + i, card.transform).TopLeft(16, 44 + i * 44, 528, 40);
                var icon = UIKit.Panel(root, "Icon", Theme.Hex(lab.ColorHex), 8);
                icon.rectTransform.TopLeft(0, 2, 36, 36);
                var mono = UIKit.Text(icon.transform, "Mono", a.Monogram, 15, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
                mono.rectTransform.Fill();
                var row = new Row { Root = root.gameObject, Icon = icon.rectTransform, Mono = mono };
                row.Name = UIKit.Text(root, "Name", a.Name, 17, Theme.Text, TextAlignmentOptions.MidlineLeft, UIFonts.Medium);
                row.Name.rectTransform.TopLeft(48, 0, 230, 24);
                row.Count = UIKit.Text(root, "Count", "", 17, Theme.TextDim, TextAlignmentOptions.MidlineRight, UIFonts.Bold);
                row.Count.rectTransform.TopLeft(270, 0, 70, 24);
                row.Bar = UIKit.Bar(root, "Share", Theme.PanelLight, Theme.Hex(lab.ColorHex), out row.Fill, 3).rectTransform.TopLeft(48, 28, 292, 6);
                row.Cps = UIKit.Text(root, "Cps", "", 16, Theme.Accent, TextAlignmentOptions.MidlineRight, UIFonts.Medium);
                row.Cps.rectTransform.TopLeft(350, 0, 178, 40);
                _rows[i] = row;
            }

            var act = UIKit.Panel(col, "Activity", Theme.Panel, 14);
            act.rectTransform.TopLeft(0, 504, 560, 318);
            BuildGoal(act.transform);
            UIKit.Image(act.transform, "Divider", Theme.Border).rectTransform.TopLeft(20, 104, 520, 1);
            UIKit.Text(act.transform, "Title", "ACTIVITY", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium)
                 .rectTransform.TopLeft(20, 114, 300, 20);
            _activity = UIKit.Text(act.transform, "Feed", "", 14, Theme.TextDim, TextAlignmentOptions.BottomLeft, UIFonts.Mono);
            _activity.rectTransform.TopLeft(20, 138, 520, 168);
            _activity.overflowMode = TextOverflowModes.Masking;
            _activity.textWrappingMode = TextWrappingModes.NoWrap;
            _activity.lineSpacing = 6;
            Log("<color=#828EA5>CorpOS agent bus connected.</color>");
        }

        /// <summary>The next goal (see <see cref="NextGoal"/>): what to save up for, how far along, and roughly when. Click to shop.</summary>
        void BuildGoal(Transform parent)
        {
            var card = UIKit.Button(parent, "NextGoal", Theme.Panel, OpenGoal, 10);
            GoalCard = card.GetComponent<RectTransform>();
            GoalCard.TopLeft(8, 6, 544, 92);
            UIKit.SubCanvas(GoalCard); // the estimate ticks; don't rebuild the activity feed with it
            UIKit.Text(card.transform, "Label", "NEXT GOAL", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium)
                 .rectTransform.TopLeft(12, 8, 200, 20);
            _goalEta = UIKit.Text(card.transform, "Eta", "", 14, Theme.Accent, TextAlignmentOptions.TopRight, UIFonts.Medium);
            _goalEta.rectTransform.TopLeft(212, 8, 320, 20);
            _goalTitle = UIKit.Text(card.transform, "Title", "", 18, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _goalTitle.rectTransform.TopLeft(12, 30, 520, 24);
            _goalTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _goalTitle.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Bar(card.transform, "Bar", Theme.PanelLight, Theme.Accent2, out _goalFill, 3).rectTransform.TopLeft(12, 60, 520, 6);
            _goalStatus = UIKit.Text(card.transform, "Status", "", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _goalStatus.rectTransform.TopLeft(12, 70, 520, 18);
            _goalStatus.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void OpenGoal()
        {
            if (_goal == null) return;
            _gm.Sfx.Play(Util.Sound.UiClick);
            switch (_goal.Kind)
            {
                case GoalKind.Office: _gm.Computer.SelectStoreTab(StorePanel.OfficeTabIndex); break;
                case GoalKind.Factory:
                case GoalKind.Option: _gm.Computer.ShowCareer(false); break;
                default: _gm.Computer.SelectStoreTab(0); break;
            }
        }

        /// <summary>The goal currently on the card, and the card itself (the tour checks both).</summary>
        public Goal Goal => _goal;
        public RectTransform GoalCard { get; private set; }

        void RefreshGoal(GameModel m)
        {
            _goal = NextGoal.Pick(m);
            UIKit.Set(_goalTitle, _goal.Title);
            UIKit.SetFill(_goalFill, (float)_goal.Progress);
            UIKit.Set(_goalStatus, _goal.Kind == GoalKind.Option
                ? $"{NumberFormat.Short(m.State.allTimeEarned)} / {NumberFormat.Short(_goal.Need)} all-time credits ({NumberFormat.Percent(_goal.Progress)})"
                : $"{NumberFormat.Short(_goal.Have)} / {NumberFormat.Short(_goal.Need)} credits ({NumberFormat.Percent(_goal.Progress)})");
            UIKit.Set(_goalEta, !_goal.Reached ? NumberFormat.Eta(_goal.SecondsAt(m.Cps))
                : _goal.Kind == GoalKind.Option ? "<color=#3DDC97>ready, click to reorg</color>" : "<color=#3DDC97>ready, click to buy</color>");
        }

        public void Log(string line)
        {
            _log.Add(line);
            while (_log.Count > MaxLogLines) _log.RemoveAt(0);
            _logSb.Clear();
            for (int i = 0; i < _log.Count; i++)
            {
                if (i > 0) _logSb.Append('\n');
                _logSb.Append(AlphaTags[_log.Count - 1 - i]).Append(_log[i]);
            }
            _activity.SetText(_logSb);
        }

        public void Tick(float dt)
        {
            var m = _gm.Model;
            if (!m.IsWorking || m.TotalAgents == 0) return;
            _logTimer -= dt;
            if (_logTimer > 0) return;
            _logTimer = Mathf.Clamp(3.2f / Mathf.Log10(10 + m.TotalAgents), 0.35f, 3f);

            // pick an agent type weighted by count
            int total = m.TotalAgents, pick = _rng.Next(total), idx = 0;
            for (; idx < GameDatabase.Agents.Length; idx++)
            {
                pick -= m.AgentCount(idx);
                if (pick < 0) break;
            }
            idx = Mathf.Min(idx, GameDatabase.Agents.Length - 1);
            var agent = GameDatabase.Agents[idx];
            var lab = GameDatabase.Lab(agent.LabId);
            string line = FlavorText.AgentLog(agent, _rng.Next(1, m.AgentCount(idx) + 1), _rng);
            Log($"<color={lab.ColorHex}>●</color> {line}");
        }

        public void Refresh()
        {
            var m = _gm.Model;
            double total = m.RawCps;
            int types = 0;
            for (int i = 0; i < _rows.Length; i++) if (m.AgentCount(i) > 0) types++;
            // up to ten types get roomy rows; a full twenty-type fleet switches to a compact single-line layout
            bool compact = types > 10;
            if (compact != _compact)
            {
                _compact = compact;
                foreach (var r in _rows) r.SetCompact(compact);
            }
            float step = compact ? 22 : 44;
            int y = 0;
            for (int i = 0; i < _rows.Length; i++)
            {
                int n = m.AgentCount(i);
                var r = _rows[i];
                if (r.Root.activeSelf != n > 0) r.Root.SetActive(n > 0);
                if (n == 0) continue;
                var pos = new Vector2(16, -(44 + y * step));
                var rt = (RectTransform)r.Root.transform;
                if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
                y++;
                double cps = m.AgentTypeCps(i);
                if (n == r.ShownCount && cps == r.ShownCps) continue; // nothing changed: no new strings
                r.ShownCount = n;
                r.ShownCps = cps;
                UIKit.Set(r.Count, "x" + n);
                UIKit.Set(r.Cps, NumberFormat.Rate(cps));
                UIKit.SetFill(r.Fill, (float)(total > 0 ? cps / total : 0));
            }
            UIKit.SetActive(_empty, types == 0);
            UIKit.Set(_header, m.TotalAgents > 0 ? $"{m.TotalAgents:N0} agents · {types} types" : "");
            RefreshGoal(m);
        }
    }
}
