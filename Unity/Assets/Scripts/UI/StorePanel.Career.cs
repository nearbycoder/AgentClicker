using System.Collections.Generic;
using System.Text;
using AgentClicker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>The FACTORY tab (factory, reorg and the Board Room), TROPHIES and STATS.</summary>
    public partial class StorePanel
    {
        class PerkRow
        {
            public PerkDef Def;
            public Button Button;
            public Image Background, Icon;
            public TextMeshProUGUI Name, Desc, Cost;
        }

        // factory / reorg
        RectTransform _factoryView, _boardView;
        Button _viewFactory, _viewBoard;
        TextMeshProUGUI _viewBoardLabel, _optionsLabel, _heroTitle, _heroPitch;
        TextMeshProUGUI _factoryReqs, _factoryStatus, _factoryButtonLabel, _reqTitle;
        Image _factoryFill;
        Button _factoryButton;
        bool _boardRoom;

        // board room
        TextMeshProUGUI _boardHeader;
        readonly List<PerkRow> _perkRows = new List<PerkRow>();

        // trophies
        RectTransform _trophyGrid;
        TextMeshProUGUI _trophyHeader;
        readonly List<Image> _trophyTiles = new List<Image>();
        int _trophiesShown = -1;

        // stats
        TextMeshProUGUI _stats;
        readonly StringBuilder _sb = new StringBuilder();

        // ------------------------------------------------------------------ factory
        void BuildFactory(RectTransform page)
        {
            _viewFactory = UIKit.Button(page, "ViewFactory", Theme.PanelLight, () => { _boardRoom = false; _hover = null; Refresh(); }, 6);
            _viewFactory.GetComponent<RectTransform>().TopLeft(0, 0, 150, 30);
            _viewFactory.Label("FACTORY", 13, Theme.Text, UIFonts.Bold);
            _viewBoard = UIKit.Button(page, "ViewBoard", Theme.PanelLight, () => { _boardRoom = true; _hover = null; Refresh(); }, 6);
            _viewBoard.GetComponent<RectTransform>().TopLeft(156, 0, 150, 30);
            _viewBoardLabel = _viewBoard.Label("BOARD ROOM", 13, Theme.Text, UIFonts.Bold);
            _optionsLabel = UIKit.Text(page, "Options", "", 15, Theme.Gold, TextAlignmentOptions.MidlineRight, UIFonts.Bold);
            _optionsLabel.rectTransform.TopLeft(310, 0, 210, 30);

            _factoryView = UIKit.Rect("FactoryView", page).TopLeft(0, 40, 524, 540);
            var hero = UIKit.Panel(_factoryView, "Hero", Theme.Hex("#101826"), 12);
            hero.rectTransform.TopLeft(0, 0, 524, 136);
            _heroTitle = UIKit.Text(hero.transform, "Title", "THE SOFTWARE FACTORY", 26, Theme.Gold, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _heroTitle.rectTransform.TopLeft(20, 14, 490, 34);
            _heroPitch = UIKit.Text(hero.transform, "Pitch", "", 15, Theme.TextDim, TextAlignmentOptions.TopLeft);
            _heroPitch.rectTransform.TopLeft(20, 52, 484, 80);

            _reqTitle = UIKit.Text(_factoryView, "ReqTitle", "REQUIREMENTS", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _reqTitle.rectTransform.TopLeft(4, 148, 500, 20);
            _factoryReqs = UIKit.Text(_factoryView, "Reqs", "", 17, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _factoryReqs.enableAutoSizing = true;
            _factoryReqs.fontSizeMin = 13;
            _factoryReqs.fontSizeMax = 17;
            _factoryReqs.rectTransform.TopLeft(4, 172, 516, 150);
            _factoryReqs.lineSpacing = 14;
            UIKit.Bar(_factoryView, "Progress", Theme.PanelLight, Theme.Gold, out _factoryFill, 8).rectTransform.TopLeft(4, 330, 516, 16);
            _factoryStatus = UIKit.Text(_factoryView, "Status", "", 15, Theme.TextDim, TextAlignmentOptions.TopLeft);
            _factoryStatus.rectTransform.TopLeft(4, 354, 516, 62);
            _factoryButton = UIKit.Button(_factoryView, "Build", Theme.Gold, OnFactoryButton, 14);
            _factoryButton.GetComponent<RectTransform>().TopLeft(4, 438, 516, 84);
            _factoryButtonLabel = _factoryButton.Label("BUILD THE FACTORY", 28, Theme.Bg);
            Hover(_factoryButton, FactoryButtonInfo);

            BuildBoardRoom(page);
        }

        void OnFactoryButton()
        {
            if (_gm.Model.State.factoryBuilt) _gm.RequestReorg();
            else _gm.BuildFactory();
        }

        (string, string, string) FactoryButtonInfo()
        {
            var m = _gm.Model;
            if (!m.State.factoryBuilt) return ("The Software Factory", "Tickets in, shipped software out. Doubles all production and unlocks frontier agents.", "");
            return ($"Reorg to {m.NextDivisionName}",
                    "Resets credits, agents, upgrades, the day and your title" + (m.HasBoardPerk("pack_your_desk") ? "" : ", and your office gadgets") +
                    ". Keeps options, perks, trophies, the story and your coworkers.",
                    $"Production bonus after: +{NumberFormat.Percent(m.OptionValue * (m.State.optionsEarned + m.PendingOptions))}");
        }

        void BuildBoardRoom(RectTransform page)
        {
            _boardView = UIKit.Rect("BoardView", page).TopLeft(0, 40, 524, 540);
            _boardHeader = UIKit.Text(_boardView, "Header", "", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _boardHeader.rectTransform.TopLeft(4, 0, 516, 40);
            var content = UIKit.ScrollList(_boardView, "List", 6, out _);
            content.parent.GetComponent<RectTransform>().TopLeft(0, 46, 524, 494);
            foreach (var p in GameDatabase.Perks)
            {
                var def = p;
                var row = new PerkRow { Def = p };
                row.Button = UIKit.Button(content, p.Id, Theme.PanelLight, () => BuyPerk(def), 10);
                row.Button.GetComponent<RectTransform>().Height(66);
                row.Background = row.Button.GetComponent<Image>();
                row.Icon = UIKit.Panel(row.Button.transform, "Icon", Theme.Gold, 10);
                row.Icon.rectTransform.TopLeft(10, 11, 44, 44);
                UIKit.Text(row.Icon.transform, "Glyph", p.Repeatable ? "◆" : "★", 22, Theme.Bg, TextAlignmentOptions.Center, UIFonts.Bold).rectTransform.Fill();
                row.Name = UIKit.Text(row.Button.transform, "Name", p.Name, 17, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
                row.Name.rectTransform.TopLeft(66, 7, 320, 22);
                row.Desc = UIKit.Text(row.Button.transform, "Desc", p.Description, 12, Theme.TextDim, TextAlignmentOptions.TopLeft);
                row.Desc.rectTransform.TopLeft(66, 29, 330, 34);
                row.Cost = UIKit.Text(row.Button.transform, "Cost", "", 16, Theme.Gold, TextAlignmentOptions.MidlineRight, UIFonts.Bold);
                row.Cost.rectTransform.TopLeft(396, 0, 110, 66);
                Hover(row.Button, () => PerkInfo(def));
                _perkRows.Add(row);
            }
        }

        void BuyPerk(PerkDef p)
        {
            if (!_gm.Model.BuyPerk(p.Id)) { _gm.Sfx.Play(Util.Sound.Error, 0.5f); return; }
            Refresh();
        }

        (string, string, string) PerkInfo(PerkDef p)
        {
            var m = _gm.Model;
            string foot = p.Repeatable ? $"Seats held: {m.State.boardSeats} (x{System.Math.Pow(1.1, m.State.boardSeats):0.00} production)"
                : m.HasBoardPerk(p.Id) ? "Owned. Applies in every division."
                : p.Requires != null && !m.HasBoardPerk(p.Requires) ? $"Requires {GameDatabase.Perk(p.Requires).Name}."
                : $"Costs ◆ {NumberFormat.Short(m.PerkCost(p))} Stock Options.";
            return (p.Name, p.Description, foot);
        }

        static bool CanAffordAnyPerk(GameModel m)
        {
            if (m.State.options < 1) return false;
            foreach (var p in GameDatabase.Perks)
                if (m.IsPerkAvailable(p) && m.State.options >= m.PerkCost(p)) return true;
            return false;
        }

        void RefreshFactory(GameModel m)
        {
            bool career = m.CareerUnlocked || m.State.options > 0;
            UIKit.SetActive(_viewFactory, career);
            UIKit.SetActive(_viewBoard, career);
            if (!career) _boardRoom = false;
            UIKit.Set(_optionsLabel, career ? $"◆ {NumberFormat.Short(m.State.options)} options" : "");
            _viewFactory.GetComponent<Image>().color = !_boardRoom ? Theme.Accent2 : Theme.PanelLight;
            _viewBoard.GetComponent<Image>().color = _boardRoom ? Theme.Accent2 : Theme.PanelLight;
            UIKit.Set(_viewBoardLabel, CanAffordAnyPerk(m) ? "BOARD ROOM <color=#FFD166>●</color>" : "BOARD ROOM");
            if (_factoryView.gameObject.activeSelf == _boardRoom) _factoryView.gameObject.SetActive(!_boardRoom);
            if (_boardView.gameObject.activeSelf != _boardRoom) _boardView.gameObject.SetActive(_boardRoom);
            if (_boardRoom) { RefreshBoardRoom(m); return; }

            if (m.State.factoryBuilt)
            {
                RefreshReorg(m);
                return;
            }
            UIKit.Set(_heroTitle, "THE SOFTWARE FACTORY");
            UIKit.Set(_heroPitch, m.State.reorgs == 0
                ? "A fully autonomous pipeline: tickets in, shipped software out. Build it and your day job runs itself. You will never have to click again."
                : $"{m.DivisionName} wants its own Factory, sized for your career so far. It doubles all production, and once it's online you can reorg again.");
            UIKit.Set(_reqTitle, "REQUIREMENTS");
            var reqs = m.FactoryRequirements();
            _sb.Clear();
            for (int i = 0; i < reqs.Count; i++)
            {
                var r = reqs[i];
                if (i > 0) _sb.Append('\n');
                _sb.Append(r.met ? "<color=#3DDC97>✓</color> " : "<color=#FF5D5D>✗</color> ");
                if (r.met) _sb.Append(r.label); else _sb.Append("<color=#A4AFC2>").Append(r.label).Append("</color>");
            }
            UIKit.Set(_factoryReqs, _sb.ToString());
            double cost = m.FactoryCost;
            float progress = (float)(m.State.credits / cost);
            UIKit.SetFill(_factoryFill, progress);
            UIKit.Set(_factoryStatus, $"Savings: {NumberFormat.Short(m.State.credits)} / {NumberFormat.Short(cost)} " +
                                      $"({NumberFormat.Percent(Mathf.Clamp01(progress))})" +
                                      (m.Cps > 0 && progress < 1 ? $" · about {NumberFormat.Duration((cost - m.State.credits) / m.Cps)} at current rate" : ""));
            _factoryButton.interactable = m.CanBuildFactory;
            UIKit.Set(_factoryButtonLabel, "BUILD THE FACTORY");
        }

        void RefreshReorg(GameModel m)
        {
            double pending = m.PendingOptions;
            UIKit.Set(_heroTitle, "FACTORY ONLINE · 100% AUTOMATED");
            UIKit.Set(_heroPitch,
                $"{m.DivisionName} runs itself and every number is doubled. Keep growing here, or " +
                $"<color=#FFD166>reorg</color>: take the Factory to <b>{m.NextDivisionName}</b> and start over, faster.");
            UIKit.Set(_reqTitle, $"REORG TO {m.NextDivisionName.ToUpperInvariant()}");
            bool desk = m.HasBoardPerk("pack_your_desk");
            UIKit.Set(_factoryReqs,
                "<color=#3DDC97>Keeps</color>   options, perks, trophies, story, coworkers" + (desk ? ", office" : "") + "\n" +
                "<color=#FF5D5D>Resets</color>   credits, agents, upgrades, " + (desk ? "" : "gadgets, ") + "day, title\n" +
                (m.State.optionsEarned > 0
                    ? $"<color=#A4AFC2>Options</color>   ◆ {NumberFormat.Short(m.State.optionsEarned)} earned so far (+{NumberFormat.Percent(m.OptionValue * m.State.optionsEarned)} production)"
                    : "<color=#A4AFC2>Options</color>   none yet: this would be your first reorg"));
            // progress toward the next vested option
            double owned = m.State.optionsEarned + pending;
            double from = GameModel.EarningsForOptions(owned), to = GameModel.EarningsForOptions(owned + 1);
            float frac = (float)((m.State.allTimeEarned - from) / System.Math.Max(1, to - from));
            UIKit.SetFill(_factoryFill, frac);
            UIKit.Set(_factoryStatus,
                $"Next option vests at {NumberFormat.Short(to)} all-time credits ({NumberFormat.Percent(Mathf.Clamp01(frac))})" +
                (m.Cps > 0 ? $" · about {NumberFormat.Duration((to - m.State.allTimeEarned) / m.Cps)}" : "") +
                (pending >= 1 ? $"\n<color=#FFD166>Reorg now for +{NumberFormat.Short(pending)} options: +{NumberFormat.Percent(m.OptionValue * pending)} production, forever.</color>" : ""));
            _factoryButton.interactable = m.CanReorg;
            UIKit.Set(_factoryButtonLabel, pending >= 1 ? $"REORG  ·  +{NumberFormat.Short(pending)} ◆" : "REORG  ·  NO OPTIONS VESTED YET");
        }

        void RefreshBoardRoom(GameModel m)
        {
            UIKit.Set(_boardHeader,
                $"<color=#FFD166>◆ {NumberFormat.Short(m.State.options)}</color> to spend  ·  {NumberFormat.Short(m.State.optionsEarned)} earned " +
                $"(+{NumberFormat.Percent(m.OptionValue * m.State.optionsEarned)} production)\n" +
                "<size=90%><color=#828EA5>Spending options never lowers your production bonus. Perks last forever.</color></size>");
            foreach (var r in _perkRows)
            {
                var p = r.Def;
                bool owned = !p.Repeatable && m.HasBoardPerk(p.Id);
                bool locked = p.Requires != null && !m.HasBoardPerk(p.Requires);
                double cost = m.PerkCost(p);
                bool afford = m.State.options >= cost;
                if (p.Repeatable) UIKit.Set(r.Name, $"{p.Name} <color=#A4AFC2>×{m.State.boardSeats}</color>");
                if (owned)
                {
                    UIKit.Set(r.Cost, "OWNED");
                    r.Cost.color = Theme.Good;
                    r.Icon.color = Theme.Good;
                    r.Background.color = Theme.PanelLight;
                }
                else if (locked)
                {
                    UIKit.Set(r.Cost, "LOCKED");
                    r.Cost.color = Theme.TextFaint;
                    r.Icon.color = Theme.Border;
                    r.Background.color = Theme.PanelLight;
                }
                else
                {
                    UIKit.Set(r.Cost, $"◆ {NumberFormat.Short(cost)}");
                    r.Cost.color = afford ? Theme.Gold : Theme.Bad;
                    r.Icon.color = afford ? Theme.Gold : Theme.Border;
                    r.Background.color = afford ? Theme.PanelHover : Theme.PanelLight;
                }
                r.Button.interactable = !owned && !locked;
            }
        }

        // ------------------------------------------------------------------ trophies
        const int TrophyColumns = 30, TrophyCell = 15, TrophyStep = 17;

        void BuildTrophies(RectTransform page)
        {
            _trophyHeader = UIKit.Text(page, "Header", "", 14, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _trophyHeader.rectTransform.TopLeft(4, 0, 516, 40);
            // A fixed wall rather than a scroll view: a masked scroll view re-culls every square every frame.
            _trophyGrid = UIKit.Rect("Wall", page).TopLeft(4, 44, 516, 536);
            UIKit.SubCanvas(_trophyGrid); // hundreds of squares: keep them out of the store's batch
        }

        void EnsureTrophyTiles()
        {
            if (_trophyTiles.Count > 0) return;
            // built on first view: ~500 squares sharing one material, grouped by category
            var all = AchievementDatabase.All;
            float y = 0;
            int i = 0;
            while (i < all.Count)
            {
                var cat = all[i].Category;
                var label = UIKit.Text(_trophyGrid, "Cat" + cat, "", 12, Theme.TextDim, TextAlignmentOptions.TopLeft, UIFonts.Bold);
                label.rectTransform.TopLeft(0, y, 516, 15);
                _trophyLabels.Add((cat, label));
                y += 16;
                int n = 0;
                for (; i < all.Count && all[i].Category == cat; i++, n++)
                {
                    var def = all[i];
                    var tile = UIKit.Panel(_trophyGrid, def.Id, Theme.PanelLight, 4, true);
                    tile.rectTransform.TopLeft(n % TrophyColumns * TrophyStep, y + n / TrophyColumns * TrophyStep, TrophyCell, TrophyCell);
                    Hover(tile, () => TrophyInfo(def));
                    _trophyTiles.Add(tile);
                }
                y += (n + TrophyColumns - 1) / TrophyColumns * TrophyStep + 4;
            }
        }

        readonly List<(TrophyCategory cat, TextMeshProUGUI label)> _trophyLabels = new List<(TrophyCategory, TextMeshProUGUI)>();

        (string, string, string) TrophyInfo(AchievementDef a)
        {
            bool got = _gm.Model.HasAchievement(a.Id);
            string cat = AchievementDatabase.CategoryNames[(int)a.Category];
            string col = AchievementDatabase.CategoryColors[(int)a.Category];
            return (got ? $"<color={col}>★</color> {a.Name}" : $"<color=#828EA5>○</color> {a.Name}",
                    a.Description, got ? $"{cat} · unlocked · +4% Clout" : $"{cat} · locked");
        }

        void RefreshTrophies(GameModel m)
        {
            EnsureTrophyTiles();
            int count = m.AchievementCount;
            if (count == _trophiesShown) return;
            _trophiesShown = count;
            var all = AchievementDatabase.All;
            int cats = AchievementDatabase.CategoryColors.Length;
            var colors = new Color[cats];
            var got = new int[cats];
            var total = new int[cats];
            for (int i = 0; i < cats; i++) colors[i] = Theme.Hex(AchievementDatabase.CategoryColors[i]);
            for (int i = 0; i < all.Count; i++)
            {
                int c = (int)all[i].Category;
                total[c]++;
                bool has = m.HasAchievement(all[i].Id);
                if (has) got[c]++;
                var color = has ? colors[c] : Theme.PanelLight;
                if (_trophyTiles[i].color != color) _trophyTiles[i].color = color;
            }
            foreach (var (cat, label) in _trophyLabels)
            {
                int c = (int)cat;
                label.text = $"<color={AchievementDatabase.CategoryColors[c]}>{AchievementDatabase.CategoryNames[c].ToUpperInvariant()}</color>  " +
                             $"<color=#828EA5>{got[c]} / {total[c]}</color>";
            }
            _sb.Clear();
            _sb.Append("<size=125%><color=#E6EDF7>").Append(count).Append("</color><color=#828EA5> / ").Append(all.Count).Append(" trophies</color></size>   ")
               .Append("<color=#FFD166>Clout +").Append(NumberFormat.Percent(m.Clout)).Append("</color>\n<color=#828EA5>")
               .Append("+4% Clout per trophy · influence upgrades turn it into ").Append(NumberFormat.Mult(m.CloutMultiplier)).Append(" production</color>");
            UIKit.Set(_trophyHeader, _sb.ToString());
        }

        // ------------------------------------------------------------------ stats
        void BuildStats(RectTransform page)
        {
            var content = UIKit.ScrollList(page, "List", 0, out _);
            content.parent.GetComponent<RectTransform>().TopLeft(0, 0, 524, 580);
            _stats = UIKit.Text(content, "Stats", "", 15, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _stats.lineSpacing = 4;
            _stats.overflowMode = TextOverflowModes.Overflow; // the list's layout sizes it to its preferred height
        }

        static string People(GameModel m)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                var p = (Person)i;
                int r = m.Rapport(p);
                string dots = new string('●', Mathf.Max(0, r)) + new string('○', Mathf.Max(0, GameModel.RapportMax - Mathf.Max(0, r)));
                string col = r >= GameModel.PerkThreshold ? "#3DDC97" : r < 0 ? "#FF5D5D" : "#A4AFC2";
                sb.Append($"{CallDatabase.PeopleNames[i]}  <color={col}>{dots} {(r > 0 ? "+" : "")}{r}</color>\n");
                if (m.HasPerk(p)) sb.Append($"<size=85%><color=#3DDC97>   ✓ {CallDatabase.PerkNames[i]}</color></size>\n");
            }
            return sb.ToString();
        }

        void RefreshStats(GameModel m)
        {
            var s = m.State;
            int types = 0;
            foreach (var c in s.agentCounts) if (c > 0) types++;
            string Row(string label, string value) => $"<color=#A4AFC2>{label}</color>   {value}\n";
            _sb.Clear();
            _sb.Append("<color=#4DD0E1>THIS DIVISION</color>  <size=85%><color=#828EA5>").Append(m.DivisionName).Append("</color></size>\n")
               .Append(Row("Credits in bank", NumberFormat.Grouped(s.credits)))
               .Append(Row("Earned here", NumberFormat.Short(s.lifetimeEarned)))
               .Append(Row("Credits/sec", $"{NumberFormat.Short(m.Cps)}  ({NumberFormat.Mult(m.GlobalMultiplier)} global)"))
               .Append(Row("Click power", NumberFormat.Short(m.ClickPower)))
               .Append(Row("Agents", $"{m.TotalAgents:N0} across {types} types"))
               .Append(Row("Upgrades", $"{s.upgrades.Count} / {GameDatabase.Upgrades.Count}"))
               .Append(Row("Office gadgets", $"{s.office.Count} / {GameDatabase.OfficeItems.Length}"))
               .Append(Row("Day", $"{s.day}  ·  <color=#FFD166>★ {s.stars}</color> quotas met (+{s.stars}% credits/sec)"))
               .Append(Row("Title", m.Title.Name))
               .Append(Row("Automation", NumberFormat.Percent(m.Automation)))
               .Append('\n')
               .Append("<color=#FFD166>CAREER</color>\n")
               .Append(Row("Division", $"{m.DivisionName} <color=#828EA5>(#{s.reorgs + 1})</color>  ·  {s.reorgs} reorg{(s.reorgs == 1 ? "" : "s")}"))
               .Append(Row("Stock Options", $"◆ {NumberFormat.Short(s.options)} to spend · {NumberFormat.Short(s.optionsEarned)} earned"))
               .Append(Row("Trophies", $"{m.AchievementCount} / {GameModel.AchievementTotal}  ·  Clout +{NumberFormat.Percent(m.Clout)}"))
               .Append(Row("All-time earned", NumberFormat.Short(s.allTimeEarned)))
               .Append(Row("Hand-shipped", $"{NumberFormat.Short(s.allTimeHandmade)} from {s.allTimeClicks:N0} clicks"))
               .Append(Row("Days worked", $"{s.totalDays:N0}  ·  ★ {s.totalStars:N0} quotas met"))
               .Append(Row("Factories built", s.factoriesBuilt + (s.bestFactoryDay > 0 ? $"  ·  fastest on day {s.bestFactoryDay}" : "")))
               .Append(Row("Time at work", NumberFormat.Duration(s.playSeconds)))
               .Append(Row("Calls", $"{s.callsAnswered} answered, {s.callsMissed} missed  ·  <color=#A4AFC2>Asks done</color> {s.asksCompleted}"))
               .Append('\n')
               .Append("<color=#4DD0E1>MULTIPLIERS</color>\n")
               .Append(Row("Stock options", NumberFormat.Mult(m.PrestigeMultiplier)))
               .Append(Row("Board seats", NumberFormat.Mult(System.Math.Pow(1.1, s.boardSeats))))
               .Append(Row("Research", NumberFormat.Mult(m.ResearchMultiplier)))
               .Append(Row("Clout", NumberFormat.Mult(m.CloutMultiplier)))
               .Append(Row("Factory", s.factoryBuilt ? "x2.00" : "<color=#828EA5>not built</color>"))
               .Append('\n')
               .Append("<color=#FFD166>PEOPLE</color>  <size=85%><color=#828EA5>(phone-call choices change how they feel; 3+ unlocks a perk)</color></size>\n")
               .Append(People(m));
            UIKit.Set(_stats, _sb.ToString());
        }
    }
}
