using System;
using System.Collections.Generic;
using System.Linq;
using AgentClicker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>Right column: the ModelMart (agents, upgrades, office gadgets, the factory & board room, trophies and stats).</summary>
    public partial class StorePanel
    {
        enum Tab { Agents, Upgrades, Office, Factory, Trophies, Stats }

        class AgentRow
        {
            public Button Button;
            public Image Icon;
            public TextMeshProUGUI Mono, Name, Sub, Cost, Owned, Badge;
            public string SubText;
            // last values shown: the row's strings are only rebuilt when one of these changes
            public double ShownCost = -1;
            public int ShownOwned = -1, ShownN = -1, ShownState = -1;
        }

        class OfficeRow
        {
            public OfficeItemDef Def;
            public Button Button;
            public Image Icon;
            public TextMeshProUGUI Name, Desc, Cost;
        }

        class UpgradeTile
        {
            public UpgradeDef Def;
            public Button Button;
            public Image Background, Icon;
            public TextMeshProUGUI Mono, Badge, Cost;
        }

        const int BuyMax = -1;

        readonly GameManager _gm;
        readonly RectTransform _root;
        readonly Dictionary<Tab, RectTransform> _pages = new Dictionary<Tab, RectTransform>();
        readonly Dictionary<Tab, Button> _tabButtons = new Dictionary<Tab, Button>();
        readonly Dictionary<Tab, TextMeshProUGUI> _tabLabels = new Dictionary<Tab, TextMeshProUGUI>();
        int _lastUpgradeBadge = -1, _lastTrophyBadge = -1;
        Tab _tab = Tab.Agents;
        int _buyAmount = 1;
        static readonly int[] Amounts = { 1, 10, 100, BuyMax };
        readonly Button[] _amountButtons = new Button[Amounts.Length];

        readonly AgentRow[] _agentRows;
        readonly List<OfficeRow> _officeRows = new List<OfficeRow>();
        RectTransform _upgradeGrid;
        TextMeshProUGUI _upgradeHeader, _buyAllLabel;
        Button _buyAll;
        int _upgradeSignature = int.MinValue;
        readonly List<UpgradeTile> _upgradeTiles = new List<UpgradeTile>();
        int _upgradeTilesActive;

        readonly TextMeshProUGUI _infoTitle, _infoBody, _infoFoot;
        Func<(string, string, string)> _hover;

        public StorePanel(RectTransform parent, GameManager gm)
        {
            _gm = gm;
            _root = UIKit.Rect("StoreColumn", parent).TopLeft(1028, 62, 556, 822);
            var card = UIKit.Panel(_root, "Store", Theme.Panel, 14);
            card.rectTransform.Fill();

            UIKit.Text(card.transform, "Title", "MODELMART", 22, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold)
                 .rectTransform.TopLeft(20, 14, 260, 28);
            UIKit.Text(card.transform, "Sub", "Frontier agents, delivered to your desk", 14, Theme.TextDim, TextAlignmentOptions.TopRight)
                 .rectTransform.TopLeft(250, 20, 286, 20);

            string[] names = { "AGENTS", "UPGRADES", "OFFICE", "FACTORY", "TROPHIES", "STATS" };
            for (int i = 0; i < names.Length; i++)
            {
                var t = (Tab)i;
                var b = UIKit.Button(card.transform, "Tab" + names[i], Theme.PanelLight, () => SetTab(t), 8);
                b.GetComponent<RectTransform>().TopLeft(16 + i * 88, 52, 84, 34);
                var label = b.Label(names[i], 13, Theme.Text, UIFonts.Bold);
                label.enableAutoSizing = true;
                label.fontSizeMin = 9;
                label.fontSizeMax = 13;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                _tabLabels[t] = label;
                _tabButtons[t] = b;
            }

            // pages
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
                _pages[t] = UIKit.Rect("Page" + t, card.transform).TopLeft(16, 98, 524, 580);

            _agentRows = BuildAgents(_pages[Tab.Agents]);
            BuildUpgrades(_pages[Tab.Upgrades]);
            BuildOffice(_pages[Tab.Office]);
            BuildFactory(_pages[Tab.Factory]);
            BuildTrophies(_pages[Tab.Trophies]);
            BuildStats(_pages[Tab.Stats]);

            // info box
            var info = UIKit.Panel(card.transform, "Info", Theme.PanelLight, 12);
            info.rectTransform.TopLeft(16, 690, 524, 116);
            _infoTitle = UIKit.Text(info.transform, "Title", "", 18, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _infoTitle.rectTransform.TopLeft(16, 10, 492, 24);
            _infoBody = UIKit.Text(info.transform, "Body", "", 15, Theme.TextDim, TextAlignmentOptions.TopLeft);
            _infoBody.rectTransform.TopLeft(16, 36, 492, 48);
            _infoFoot = UIKit.Text(info.transform, "Foot", "", 14, Theme.Accent, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _infoFoot.rectTransform.TopLeft(16, 88, 492, 20);
            _infoFoot.enableAutoSizing = true; // long agent stats plus an estimate shrink a little rather than wrap
            _infoFoot.fontSizeMin = 11;
            _infoFoot.fontSizeMax = 14;

            SetTab(Tab.Agents);
        }

        public void SelectTab(int index) => SetTab((Tab)Mathf.Clamp(index, 0, (int)Tab.Stats));
        public static int FactoryTabIndex => (int)Tab.Factory;

        /// <summary>Opens the FACTORY tab on its Board Room (or factory) view.</summary>
        public void ShowCareer(bool boardRoom)
        {
            _boardRoom = boardRoom;
            SetTab(Tab.Factory);
        }
        public static int TrophiesTabIndex => (int)Tab.Trophies;
        public static int OfficeTabIndex => (int)Tab.Office;
        public int CurrentTab => (int)_tab;
        public RectTransform AgentRowRect(int i) => (RectTransform)_agentRows[i].Button.transform;
        public string InfoFoot => _infoFoot.text;
        public static int StatsTabIndex => (int)Tab.Stats;

        void SetTab(Tab t)
        {
            _tab = t;
            foreach (var kv in _pages) kv.Value.gameObject.SetActive(kv.Key == t);
            foreach (var kv in _tabButtons)
                kv.Value.GetComponent<Image>().color = kv.Key == t ? Theme.Accent2 : Theme.PanelLight;
            _hover = null;
            _gm.Sfx?.Play(Util.Sound.Key, 0.3f, 0.8f);
            Refresh();
        }

        void Hover(Component c, Func<(string, string, string)> info)
        {
            var relay = PointerRelay.On(c);
            relay.Enter = _ => _hover = info;
            relay.Exit = _ => { if (_hover == info) _hover = null; };
        }

        // ------------------------------------------------------------------ agents
        AgentRow[] BuildAgents(RectTransform page)
        {
            UIKit.Text(page, "Buy", "BUY", 13, Theme.TextDim, TextAlignmentOptions.MidlineLeft, UIFonts.Medium).rectTransform.TopLeft(4, 0, 40, 28);
            for (int i = 0; i < Amounts.Length; i++)
            {
                int amt = Amounts[i];
                var b = UIKit.Button(page, "Amt" + (amt == BuyMax ? "Max" : amt.ToString()), Theme.PanelLight, () => { _buyAmount = amt; Refresh(); }, 6);
                b.GetComponent<RectTransform>().TopLeft(44 + i * 62, 0, 56, 28);
                b.Label(amt == BuyMax ? "MAX" : "x" + amt, 14, Theme.Text, UIFonts.Bold);
                _amountButtons[i] = b;
            }

            var content = UIKit.ScrollList(page, "List", 6, out _);
            content.parent.GetComponent<RectTransform>().TopLeft(0, 38, 524, 542);
            var rows = new AgentRow[GameDatabase.Agents.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                int idx = i;
                var a = GameDatabase.Agents[i];
                var lab = GameDatabase.Lab(a.LabId);
                var b = UIKit.Button(content, "Agent" + i, Theme.PanelLight, () => BuyAgent(idx), 10);
                b.GetComponent<RectTransform>().Height(70);
                var row = new AgentRow { Button = b };
                row.Icon = UIKit.Panel(b.transform, "Icon", Theme.Hex(lab.ColorHex), 10);
                row.Icon.rectTransform.TopLeft(10, 10, 50, 50);
                row.Mono = UIKit.Text(row.Icon.transform, "Mono", a.Monogram, 20, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
                row.Mono.rectTransform.Fill();
                row.Name = UIKit.Text(b.transform, "Name", a.Name, 20, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
                row.Name.rectTransform.TopLeft(72, 9, 330, 26);
                row.SubText = $"{lab.Name} · {a.Model}";
                row.Sub = UIKit.Text(b.transform, "Sub", row.SubText, 14, Theme.TextDim, TextAlignmentOptions.TopLeft);
                row.Sub.rectTransform.TopLeft(72, 32, 330, 18);
                row.Cost = UIKit.Text(b.transform, "Cost", "", 15, Theme.Good, TextAlignmentOptions.TopLeft, UIFonts.Medium);
                row.Cost.rectTransform.TopLeft(72, 49, 330, 18);
                row.Owned = UIKit.Text(b.transform, "Owned", "", 34, Theme.TextFaint, TextAlignmentOptions.MidlineRight, UIFonts.Bold);
                row.Owned.rectTransform.TopLeft(380, 0, 124, 70);
                row.Owned.enableAutoSizing = true;
                row.Owned.fontSizeMin = 18;
                row.Owned.fontSizeMax = 34;
                row.Badge = UIKit.Text(b.transform, "Badge", "", 12, Theme.Gold, TextAlignmentOptions.TopRight, UIFonts.Bold);
                row.Badge.rectTransform.TopLeft(300, 6, 204, 16);
                Hover(b, () => AgentInfo(idx));
                rows[i] = row;
            }
            return rows;
        }

        void BuyAgent(int idx)
        {
            var m = _gm.Model;
            if (!m.IsAgentRevealed(idx)) return;
            int n = _buyAmount == BuyMax ? m.MaxAffordable(idx) : _buyAmount;
            if (n <= 0 || m.State.credits < m.AgentCost(idx, n))
            {
                int max = m.MaxAffordable(idx);
                if (max <= 0) { _gm.Sfx.Play(Util.Sound.Error, 0.5f); return; }
                n = Mathf.Min(Mathf.Max(1, n), max);
            }
            m.BuyAgent(idx, n);
            Refresh();
        }

        (string, string, string) AgentInfo(int i)
        {
            var m = _gm.Model;
            var a = GameDatabase.Agents[i];
            if (!m.IsAgentRevealed(i))
                return GameDatabase.IsFrontier(i) && !m.FrontierUnlocked
                    ? ("Frontier agent", "Classified. The labs only sell frontier agents to companies running a Software Factory.", "Build the Factory to unlock.")
                    : ("???", "Keep shipping. A new kind of agent is on the roadmap.", "");
            var lab = GameDatabase.Lab(a.LabId);
            string foot = $"Each produces {NumberFormat.Rate(m.UnitCps(i))}";
            if (m.AgentCount(i) > 0)
                foot += $"  ·  {m.AgentCount(i)} producing {NumberFormat.Rate(m.AgentTypeCps(i))} ({NumberFormat.Percent(m.RawCps > 0 ? m.AgentTypeCps(i) / m.RawCps : 0)})";
            int n = _buyAmount == BuyMax ? Mathf.Max(1, m.MaxAffordable(i)) : _buyAmount;
            foot += AffordIn(m.AgentCost(i, n));
            return ($"{a.Name}  <size=70%><color=#8A97AD>by {lab.Name}</color></size>",
                    $"{a.Description}\n<i><color=#56627A>\"{lab.Tagline}\"</color></i>", foot);
        }

        // ------------------------------------------------------------------ upgrades
        void BuildUpgrades(RectTransform page)
        {
            _upgradeHeader = UIKit.Text(page, "Header", "", 14, Theme.TextDim, TextAlignmentOptions.MidlineLeft, UIFonts.Medium);
            _upgradeHeader.rectTransform.TopLeft(4, 0, 360, 28);
            _buyAll = UIKit.Button(page, "BuyAll", Theme.Good, BuyAllUpgrades, 6);
            _buyAll.GetComponent<RectTransform>().TopLeft(364, 0, 154, 28);
            _buyAllLabel = _buyAll.Label("BUY ALL", 13, Theme.Bg, UIFonts.Bold);
            Hover(_buyAll, () => ("Buy all", "Buys every upgrade you can afford, cheapest first.", ""));
            var content = UIKit.ScrollList(page, "List", 6, out _);
            content.parent.GetComponent<RectTransform>().TopLeft(0, 38, 524, 542);
            var gridRt = UIKit.Rect("Grid", content);
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(96, 112);
            grid.spacing = new Vector2(6, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            var fit = gridRt.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _upgradeGrid = gridRt;
        }

        static readonly string[] Roman = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV" };
        static readonly Color ResearchColor = Theme.Hex("#1F9E89");

        UpgradeTile CreateTile()
        {
            var tile = new UpgradeTile();
            var b = UIKit.Button(_upgradeGrid, "Upgrade", Theme.PanelLight, () => { if (tile.Def != null) BuyUpgrade(tile.Def); }, 10);
            tile.Button = b;
            tile.Background = b.GetComponent<Image>();
            tile.Icon = UIKit.Panel(b.transform, "Icon", Theme.Accent, 10);
            tile.Icon.rectTransform.TopLeft(14, 10, 68, 62);
            tile.Mono = UIKit.Text(tile.Icon.transform, "Mono", "", 22, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
            tile.Mono.rectTransform.Fill().Insets(0, 12, 0, 0);
            tile.Mono.enableAutoSizing = true;
            tile.Mono.fontSizeMin = 12;
            tile.Mono.fontSizeMax = 22;
            tile.Badge = UIKit.Text(tile.Icon.transform, "Badge", "", 15, Color.white.WithAlpha(0.85f), TextAlignmentOptions.Bottom, UIFonts.Bold);
            tile.Badge.rectTransform.Fill().Insets(0, 4, 0, 0);
            tile.Badge.enableAutoSizing = true;
            tile.Badge.fontSizeMin = 9;
            tile.Badge.fontSizeMax = 15;
            tile.Cost = UIKit.Text(b.transform, "Cost", "", 15, Theme.Good, TextAlignmentOptions.Top, UIFonts.Medium);
            tile.Cost.rectTransform.TopLeft(0, 78, 96, 22);
            Hover(b, () => tile.Def == null ? ("", "", "") : (tile.Def.Name, tile.Def.Description,
                                                             $"Cost: {NumberFormat.Credits(_gm.Model.UpgradeCost(tile.Def))}" + AffordIn(_gm.Model.UpgradeCost(tile.Def))));
            _upgradeTiles.Add(tile);
            return tile;
        }

        void BindTile(UpgradeTile tile, UpgradeDef u)
        {
            tile.Def = u;
            tile.Button.name = u.Id;
            Color color;
            string mono, badge;
            switch (u.Kind)
            {
                case UpgradeKind.AgentTier:
                    var a = GameDatabase.Agents[u.AgentIndex];
                    color = Theme.Hex(GameDatabase.Lab(a.LabId).ColorHex);
                    mono = a.Monogram; badge = Roman[Mathf.Clamp(u.Tier, 0, Roman.Length - 1)];
                    break;
                case UpgradeKind.LabContract:
                    color = Theme.Hex(GameDatabase.Lab(u.LabId).ColorHex);
                    mono = GameDatabase.Lab(u.LabId).Monogram; badge = u.Value >= 2 ? "★★" : "★";
                    break;
                case UpgradeKind.ClickMult:
                    color = Theme.Accent; mono = "⏎"; badge = "x2";
                    break;
                case UpgradeKind.Research:
                    color = ResearchColor; mono = "R&D"; badge = $"+{(u.Value - 1) * 100:0.#}%";
                    break;
                case UpgradeKind.Clout:
                    color = Theme.Gold; mono = "★"; badge = "CLOUT";
                    break;
                default:
                    color = Theme.Accent2; mono = "⏎"; badge = "+%";
                    break;
            }
            tile.Icon.color = color;
            tile.Mono.text = mono;
            tile.Badge.text = badge;
            tile.Cost.text = NumberFormat.Short(_gm.Model.UpgradeCost(u));
        }

        void RebindUpgrades(List<UpgradeDef> list)
        {
            // pooled tiles: buying an upgrade rebinds a handful of tiles instead of rebuilding the grid
            while (_upgradeTiles.Count < list.Count) CreateTile();
            for (int i = 0; i < _upgradeTiles.Count; i++)
            {
                var tile = _upgradeTiles[i];
                bool on = i < list.Count;
                if (tile.Button.gameObject.activeSelf != on) tile.Button.gameObject.SetActive(on);
                if (on) BindTile(tile, list[i]);
                else tile.Def = null;
            }
            _upgradeTilesActive = list.Count;
        }

        void BuyUpgrade(UpgradeDef u)
        {
            if (!_gm.Model.BuyUpgrade(u.Id)) { _gm.Sfx.Play(Util.Sound.Error, 0.5f); return; }
            _hover = null;
            Refresh();
        }

        void BuyAllUpgrades()
        {
            var m = _gm.Model;
            var list = new List<UpgradeDef>(m.AvailableUpgradesCached);
            int bought = 0;
            _gm.SuppressPurchaseToasts = true;
            foreach (var u in list)
                if (m.State.credits >= m.UpgradeCost(u) && m.BuyUpgrade(u.Id)) bought++;
            _gm.SuppressPurchaseToasts = false;
            if (bought == 0) { _gm.Sfx.Play(Util.Sound.Error, 0.5f); return; }
            _gm.Computer.Toast($"Bought {bought} upgrade{(bought == 1 ? "" : "s")}.", Theme.Accent2);
            _hover = null;
            Refresh();
        }

        // ------------------------------------------------------------------ office
        void BuildOffice(RectTransform page)
        {
            UIKit.Text(page, "Header", "Gadgets are installed at your desk and show up in the office.", 14, Theme.TextDim,
                       TextAlignmentOptions.MidlineLeft).rectTransform.TopLeft(4, 0, 520, 28);
            var content = UIKit.ScrollList(page, "List", 6, out _);
            content.parent.GetComponent<RectTransform>().TopLeft(0, 38, 524, 542);
            foreach (var o in GameDatabase.OfficeItems.OrderBy(o => o.Cost))
            {
                var def = o;
                var b = UIKit.Button(content, o.Id, Theme.PanelLight, () => BuyOffice(def), 10);
                b.GetComponent<RectTransform>().Height(64);
                var row = new OfficeRow { Def = o, Button = b };
                row.Icon = UIKit.Panel(b.transform, "Icon", Theme.Gold, 10);
                row.Icon.rectTransform.TopLeft(10, 10, 44, 44);
                UIKit.Text(row.Icon.transform, "Glyph", o.Name.TrimStart('"').Substring(0, 1), 22, Theme.Bg, TextAlignmentOptions.Center, UIFonts.Bold)
                     .rectTransform.Fill();
                row.Name = UIKit.Text(b.transform, "Name", o.Name, 18, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
                row.Name.rectTransform.TopLeft(66, 9, 300, 24);
                row.Desc = UIKit.Text(b.transform, "Desc", o.Description, 13, Theme.TextDim, TextAlignmentOptions.TopLeft);
                row.Desc.rectTransform.TopLeft(66, 33, 330, 24);
                row.Desc.textWrappingMode = TextWrappingModes.NoWrap;
                row.Cost = UIKit.Text(b.transform, "Cost", "", 16, Theme.Good, TextAlignmentOptions.MidlineRight, UIFonts.Bold);
                row.Cost.rectTransform.TopLeft(380, 0, 124, 64);
                Hover(b, () => OfficeInfo(def));
                _officeRows.Add(row);
            }
        }

        void BuyOffice(OfficeItemDef o)
        {
            var m = _gm.Model;
            if (!m.BuyOffice(o.Id)) { _gm.Sfx.Play(Util.Sound.Error, 0.5f); return; }
            Refresh();
        }

        (string, string, string) OfficeInfo(OfficeItemDef o)
        {
            var m = _gm.Model;
            string foot;
            if (m.HasOffice(o.Id)) foot = "Installed at your desk.";
            else if (o.Requires != null && !m.HasOffice(o.Requires)) foot = $"Requires: {GameDatabase.Office(o.Requires).Name}";
            else foot = $"Cost: {NumberFormat.Credits(m.OfficeCost(o))}" + AffordIn(m.OfficeCost(o));
            return (o.Name, o.Description, foot);
        }

        /// <summary>"  ·  about 6m 12s" for something you can't afford yet (at the current rate), else nothing.</summary>
        string AffordIn(double cost)
        {
            var m = _gm.Model;
            double cps = m.Cps;
            if (m.State.credits >= cost || cps <= 0) return "";
            string eta = NumberFormat.Eta((cost - m.State.credits) / cps);
            return eta.Length == 0 ? "" : $"  <color=#8A97AD>·  {eta}</color>";
        }

        // ------------------------------------------------------------------ refresh
        public void Refresh()
        {
            var m = _gm.Model;
            switch (_tab)
            {
                case Tab.Agents: RefreshAgents(m); break;
                case Tab.Upgrades: RefreshUpgrades(m); break;
                case Tab.Office: RefreshOffice(m); break;
                case Tab.Factory: RefreshFactory(m); break;
                case Tab.Trophies: RefreshTrophies(m); break;
                case Tab.Stats: RefreshStats(m); break;
            }

            // badge counts on tabs
            int ups = 0;
            foreach (var u in m.AvailableUpgradesCached) if (m.UpgradeCost(u) <= m.State.credits) ups++;
            if (ups != _lastUpgradeBadge)
            {
                _lastUpgradeBadge = ups;
                _tabLabels[Tab.Upgrades].text = ups > 0 ? $"UPGRADES <color=#3DDC97>{ups}</color>" : "UPGRADES";
            }
            bool highlight = m.CanBuildFactory || m.CanReorg && _gm.Model.State.endingSeen || CanAffordAnyPerk(m);
            UIKit.Set(_tabLabels[Tab.Factory], highlight ? "<color=#FFD166>FACTORY</color>" : "FACTORY");
            int trophies = m.AchievementCount;
            if (trophies != _lastTrophyBadge)
            {
                _lastTrophyBadge = trophies;
                _tabLabels[Tab.Trophies].text = trophies > 0 ? $"TROPHIES <color=#FFD166>{trophies}</color>" : "TROPHIES";
            }

            var info = _hover?.Invoke() ?? DefaultInfo();
            UIKit.Set(_infoTitle, info.Item1);
            UIKit.Set(_infoBody, info.Item2);
            UIKit.Set(_infoFoot, info.Item3);
        }

        (string, string, string) DefaultInfo()
        {
            var m = _gm.Model;
            switch (_tab)
            {
                case Tab.Agents:
                    return ("Hire agents", "Agents earn credits every second. Each one costs 15% more than the last. Hover for details.",
                            m.FrontierUnlocked ? "Frontier agents are unlocked. There is no last agent." : "Tip: MAX buys as many as you can afford.");
                case Tab.Upgrades: return ("Upgrades", "Unlock by owning agents, shipping code, earning and collecting trophies. Each upgrade multiplies something forever.", "");
                case Tab.Office: return ("Office", "Better gear for your desk. Every gadget appears in the 3D office. Press Tab to admire it.", "");
                case Tab.Factory:
                    return _boardRoom
                        ? ("Board Room", "Spend Stock Options on permanent perks. Perks survive every reorg.", "Every option you earn also adds production, spent or not.")
                        : m.State.factoryBuilt
                            ? ("Reorg", "Roll the Factory out to the next division and start over, faster. Options vest the more you earn.", "")
                            : ("The goal", "Build the Software Factory and your character can finally put their feet up.", "");
                case Tab.Trophies: return ("Trophies", "Every trophy adds 4% Clout. Hover a square to see what it's for.", "Clout upgrades appear in UPGRADES.");
                default: return ("Stats", "Numbers about your numbers.", "");
            }
        }

        void RefreshAgents(GameModel m)
        {
            for (int i = 0; i < _amountButtons.Length; i++)
                _amountButtons[i].GetComponent<Image>().color = Amounts[i] == _buyAmount ? Theme.Accent2 : Theme.PanelLight;

            // the revealed agent with the best production per credit gets a hint
            int best = -1;
            double bestRatio = 0;
            for (int i = 0; i < _agentRows.Length; i++)
            {
                if (!m.IsAgentRevealed(i)) continue;
                double ratio = m.UnitCps(i) / m.AgentCost(i, 1);
                if (ratio > bestRatio) { bestRatio = ratio; best = i; }
            }

            bool shownLocked = false;
            for (int i = 0; i < _agentRows.Length; i++)
            {
                var r = _agentRows[i];
                bool revealed = m.IsAgentRevealed(i);
                // show one locked "???" row after the revealed ones
                bool visible = revealed || !shownLocked;
                if (!revealed) shownLocked = true;
                if (r.Button.gameObject.activeSelf != visible) r.Button.gameObject.SetActive(visible);
                if (!visible) continue;

                var a = GameDatabase.Agents[i];
                int n = _buyAmount == BuyMax ? Mathf.Max(1, m.MaxAffordable(i)) : _buyAmount;
                double cost = m.AgentCost(i, n);
                bool afford = m.State.credits >= cost;
                int owned = m.AgentCount(i);
                // 0 locked, then revealed: affordable / best-value / discount flags
                int state = revealed ? 1 + (afford ? 2 : 0) + (i == best ? 4 : 0) + (m.State.agentDiscount > 0 ? 8 : 0) + (_buyAmount == BuyMax ? 16 : 0) : 0;
                if (cost == r.ShownCost && owned == r.ShownOwned && n == r.ShownN && state == r.ShownState) continue;
                r.ShownCost = cost; r.ShownOwned = owned; r.ShownN = n; r.ShownState = state;
                if (revealed)
                {
                    var lab = GameDatabase.Lab(a.LabId);
                    UIKit.Set(r.Name, a.Name);
                    UIKit.Set(r.Sub, r.SubText);
                    r.Icon.color = Theme.Hex(lab.ColorHex);
                    UIKit.Set(r.Mono, a.Monogram);
                    UIKit.Set(r.Cost, $"{NumberFormat.Short(cost)} credits" + (n > 1 || _buyAmount == BuyMax ? $"  <color=#8A97AD>(x{n})</color>" : ""));
                    r.Cost.color = afford ? Theme.Good : Theme.Bad;
                    UIKit.Set(r.Owned, owned > 0 ? owned.ToString() : "");
                    r.Name.color = afford ? Theme.Text : Theme.TextDim;
                    UIKit.Set(r.Badge, i == best ? "★ BEST VALUE" : m.State.agentDiscount > 0 ? $"{m.State.agentDiscount * 100:0}% OFF" : "");
                }
                else
                {
                    bool frontierLock = GameDatabase.IsFrontier(i) && !m.FrontierUnlocked;
                    UIKit.Set(r.Name, frontierLock ? "Frontier agents" : "???");
                    UIKit.Set(r.Sub, frontierLock ? "Classified. Unlocks with the Software Factory." : "Unannounced frontier model");
                    r.Icon.color = Theme.Border;
                    UIKit.Set(r.Mono, frontierLock ? "◆" : "?");
                    UIKit.Set(r.Cost, frontierLock ? "" : NumberFormat.Short(cost) + " credits");
                    r.Cost.color = Theme.TextFaint;
                    UIKit.Set(r.Owned, "");
                    r.Name.color = Theme.TextDim;
                    UIKit.Set(r.Badge, "");
                }
                r.Button.interactable = revealed;
            }
        }

        void RefreshUpgrades(GameModel m)
        {
            var list = m.AvailableUpgradesCached;
            int sig = list.Count;
            for (int i = 0; i < list.Count; i++) sig = sig * 31 + list[i].Id.GetHashCode();
            sig = sig * 31 + (m.HasBoardPerk("expense_account") ? 1 : 0);
            if (sig != _upgradeSignature)
            {
                _upgradeSignature = sig;
                RebindUpgrades(list);
            }
            int affordable = 0;
            for (int i = 0; i < _upgradeTilesActive; i++)
            {
                var tile = _upgradeTiles[i];
                bool afford = m.State.credits >= m.UpgradeCost(tile.Def);
                if (afford) affordable++;
                tile.Cost.color = afford ? Theme.Good : Theme.Bad;
                tile.Background.color = afford ? Theme.PanelHover : Theme.PanelLight;
            }
            UIKit.Set(_upgradeHeader, list.Count == 0
                ? "No upgrades available yet. Hire more agents to unlock them."
                : $"{list.Count} available · {m.State.upgrades.Count} owned");
            UIKit.SetActive(_buyAll, affordable > 1);
            if (affordable > 1) UIKit.Set(_buyAllLabel, $"BUY ALL ({affordable})");
        }

        void RefreshOffice(GameModel m)
        {
            foreach (var r in _officeRows)
            {
                var o = r.Def;
                bool owned = m.HasOffice(o.Id);
                bool locked = !owned && o.Requires != null && !m.HasOffice(o.Requires);
                bool revealed = m.IsOfficeRevealed(o);
                double cost = m.OfficeCost(o);
                bool afford = m.State.credits >= cost;
                UIKit.Set(r.Name, revealed || owned ? o.Name : "???");
                UIKit.Set(r.Desc, revealed || owned ? o.Description : "Keep earning to reveal this upgrade.");
                if (owned)
                {
                    UIKit.Set(r.Cost, "INSTALLED");
                    r.Cost.color = Theme.Good;
                    r.Icon.color = Theme.Good;
                }
                else if (locked)
                {
                    UIKit.Set(r.Cost, "LOCKED");
                    r.Cost.color = Theme.TextFaint;
                    r.Icon.color = Theme.Border;
                }
                else
                {
                    UIKit.Set(r.Cost, NumberFormat.Short(cost));
                    r.Cost.color = afford ? Theme.Good : Theme.Bad;
                    r.Icon.color = revealed ? Theme.Gold : Theme.Border;
                }
                r.Button.interactable = !owned && !locked && revealed;
            }
        }
    }
}
