using System.Collections.Generic;
using System.Linq;
using AgentClicker.Core;
using AgentClicker.Office;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>
    /// CorpOS: the 2D game, rendered on a world-space canvas that sits on the main monitor's screen.
    /// The camera dollies into the monitor so it fills the view, but it stays live (and clickable) in the office view.
    /// </summary>
    public class ComputerUI : MonoBehaviour
    {
        public const float Width = 1600, Height = 900;

        GameManager _gm;
        Canvas _canvas;
        RectTransform _root, _login, _desktop, _locked, _modals, _toasts, _fx;
        TopBar _top;
        ShipPanel _ship;
        FleetPanel _fleet;
        StorePanel _store;
        float _refresh;

        // login
        TextMeshProUGUI _loginDay, _loginPassword, _loginEmail;
        float _loginTyping = -1;

        // events
        RectTransform _dropCard;
        TextMeshProUGUI _dropText;
        Image _dropTimer;
        RectTransform _outage;
        TextMeshProUGUI _outageText;
        readonly List<(RectTransform rt, float until, CanvasGroup group)> _activeToasts = new List<(RectTransform, float, CanvasGroup)>();

        GameObject _modal;
        readonly Stack<FloatingText> _floatPool = new Stack<FloatingText>();
        int _unreadCache = -1, _mailCountSeen = -1, _readCountSeen = -1;

        public void Init(GameManager gm)
        {
            _gm = gm;
            BuildCanvas();
            BuildLogin();
            BuildDesktop();
            BuildLocked();
            _fx = UIKit.Rect("Fx", _root);
            _fx.Fill();
            _toasts = UIKit.Rect("Toasts", _root);
            _toasts.Fill();
            _modals = UIKit.Rect("Modals", _root);
            _modals.Fill();
            // Nested canvases: a changing number only rebuilds its own column, not the whole screen.
            foreach (var rt in new[] { _fx, _toasts, _modals }) UIKit.SubCanvas(rt);
            _gm.Model.MailReceived += OnMailReceived;
        }

        void BuildCanvas()
        {
            var screen = _gm.Refs.MainScreen;
            SceneRefs.ScreenFrame(screen, out var center, out var frame, out var size);

            var go = new GameObject("CorpOS Canvas", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(screen.transform.parent, true);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _gm.Refs.MainCamera;
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<RectMask2D>(); // nothing may spill outside the monitor
            _root = (RectTransform)go.transform;
            _root.sizeDelta = new Vector2(Width, Height);
            _root.SetPositionAndRotation(center + frame * Vector3.back * 0.0016f, frame);
            float scale = size.x / Width;
            _root.localScale = Vector3.one * scale / Mathf.Max(1e-6f, go.transform.parent.lossyScale.x);

            var bg = UIKit.Image(_root, "Wallpaper", Theme.Bg);
            bg.rectTransform.Fill();
            var glow = UIKit.Image(_root, "WallpaperGlow", Theme.Accent2.WithAlpha(0.07f));
            glow.sprite = UIKit.Circle;
            glow.rectTransform.Center(1700, 1300, 520, -380);
        }

        // ------------------------------------------------------------------ screens
        void BuildLogin()
        {
            _login = UIKit.Rect("Login", _root);
            _login.Fill();
            // the whole screen is clickable, so you can log in straight from the office view
            var hit = UIKit.Image(_login, "Hit", Color.clear, true);
            hit.rectTransform.Fill();
            PointerRelay.On(hit).Click = _ => StartLoginAnimation();
            var glow = UIKit.Image(_login, "Glow", Theme.Accent.WithAlpha(0.08f));
            glow.sprite = UIKit.Circle;
            glow.rectTransform.Center(1200, 1200, -500, 300);

            UIKit.Text(_login, "Brand", $"<color={UIKit.Hex(Theme.Accent)}>◆</color> CorpOS <size=50%><color=#56627A>11 Enterprise</color></size>",
                       34, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold).rectTransform.TopLeft(48, 40, 700, 50);

            var card = UIKit.Panel(_login, "Card", Theme.Panel, 22);
            card.rectTransform.Center(560, 470, 0, -10);
            var avatar = UIKit.Image(card.transform, "Avatar", Theme.Accent2);
            avatar.sprite = UIKit.Circle;
            avatar.rectTransform.Center(118, 118, 0, 140);
            UIKit.Text(avatar.transform, "Initials", "SR", 46, Color.white, TextAlignmentOptions.Center, UIFonts.Bold).rectTransform.Fill();
            UIKit.Text(card.transform, "Name", FlavorText.EmployeeName, 34, Theme.Text, TextAlignmentOptions.Center, UIFonts.Bold)
                 .rectTransform.Center(500, 44, 0, 48);
            _loginDay = UIKit.Text(card.transform, "Day", "", 17, Theme.TextDim, TextAlignmentOptions.Center);
            _loginDay.rectTransform.Center(500, 26, 0, 12);

            var field = UIKit.Panel(card.transform, "Password", Theme.PanelLight, 10);
            field.rectTransform.Center(400, 54, 0, -50);
            _loginPassword = UIKit.Text(field.transform, "Dots", "", 30, Theme.Text, TextAlignmentOptions.Center, UIFonts.Mono);
            _loginPassword.rectTransform.Fill();

            var btn = UIKit.Button(card.transform, "LogIn", Theme.Accent, StartLoginAnimation, 12);
            btn.GetComponent<RectTransform>().Center(400, 64, 0, -130);
            btn.Label("LOG IN", 26, Theme.Bg);
            btn.gameObject.AddComponent<Pulse>().ScaleAmount = 0.02f;

            _loginEmail = UIKit.Text(_login, "Email", "", 16, Theme.TextDim, TextAlignmentOptions.Bottom);
            _loginEmail.rectTransform.Anchor(0, 0, 1, 0).Insets(40, 34, 40, -70);
        }

        void StartLoginAnimation()
        {
            if (_loginTyping >= 0) return;
            _loginTyping = 0;
        }

        void BuildDesktop()
        {
            _desktop = UIKit.Rect("Desktop", _root);
            _desktop.Fill();
            _top = new TopBar(_desktop, _gm, this);
            _ship = new ShipPanel(_desktop, _gm, this);
            _fleet = new FleetPanel(_desktop, _gm);
            _store = new StorePanel(_desktop, _gm);
            BuildDropCard();
            BuildOutageBanner();
            foreach (Transform child in _desktop) UIKit.SubCanvas((RectTransform)child);
            // and the busiest columns per card, so a ticking number doesn't rebuild its neighbours
            foreach (var column in new[] { "ShipColumn", "FleetColumn" })
            {
                var col = _desktop.Find(column);
                if (col) foreach (Transform card in col) UIKit.SubCanvas((RectTransform)card);
            }
        }

        void BuildLocked()
        {
            _locked = UIKit.Rect("Locked", _root);
            _locked.Fill();
            UIKit.Image(_locked, "Dark", Color.black).rectTransform.Fill();
            UIKit.Text(_locked, "Text", $"<color={UIKit.Hex(Theme.Accent)}>◆</color> Locked\n<size=45%><color=#56627A>See you tomorrow, Sam.</color></size>",
                       60, Theme.TextDim, TextAlignmentOptions.Center, UIFonts.Bold).rectTransform.Fill();
        }

        public void ShowLogin()
        {
            CloseModal();
            _login.gameObject.SetActive(true);
            _desktop.gameObject.SetActive(false);
            _locked.gameObject.SetActive(false);
            _loginTyping = -1;
            _loginPassword.text = "";
            var m = _gm.Model;
            _loginDay.text = $"{(m.State.reorgs > 0 ? m.DivisionName : FlavorText.Company)} · Day {m.State.day} · {FlavorText.Weekday(m.State.day)} · {NumberFormat.Clock(m.ClockHours)}";
            int unread = UnreadMail;
            string latest = null;
            for (int i = m.State.mail.Count - 1; i >= 0 && latest == null; i--)
                if (!m.State.mailRead.Contains(m.State.mail[i])) latest = StoryDatabase.MailById(m.State.mail[i])?.Subject;
            _loginEmail.text = unread > 0
                ? $"<color=#FFD166>✉ {unread} unread email{(unread == 1 ? "" : "s")}</color>  ·  {latest}"
                : $"<color=#4DD0E1>1 new email</color>  ·  {_gm.RandomMorningEmail(m.State.day)}";
        }

        public void ShowDesktop()
        {
            CloseModal();
            _login.gameObject.SetActive(false);
            _desktop.gameObject.SetActive(true);
            _locked.gameObject.SetActive(false);
            RefreshAll(0);
        }

        public void ShowLocked()
        {
            CloseModal();
            HideDrop();
            HideOutage();
            _login.gameObject.SetActive(false);
            _desktop.gameObject.SetActive(false);
            _locked.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            if (_gm == null) return;
            float dt = Time.unscaledDeltaTime;

            if (_loginTyping >= 0)
            {
                _loginTyping += dt;
                int dots = Mathf.Min(10, (int)(_loginTyping * 22));
                _loginPassword.text = new string('•', dots);
                if (_loginTyping > 0.75f)
                {
                    _loginTyping = -1;
                    _gm.Login();
                }
            }

            if (_desktop.gameObject.activeSelf)
            {
                _fleet.Tick(dt);
                _refresh -= dt;
                if (_refresh <= 0) RefreshAll(dt);
                UpdateDrop();
                UpdateOutage();
            }
            UpdateToasts();
        }

        int _refreshTick;

        /// <summary>Credits refresh at 10 Hz, fleet and store at ~3 Hz, the top bar at 2 Hz (each refresh rebuilds a canvas).</summary>
        void RefreshAll(float dt)
        {
            _refresh = 0.1f;
            _refreshTick++;
            _ship.Refresh();
            if (dt <= 0 || _refreshTick % 5 == 0) _top.Refresh(0.5f);
            if (dt <= 0 || _refreshTick % 3 == 0)
            {
                _fleet.Refresh();
                _store.Refresh();
            }
        }

        public void SelectStoreTab(int index) => _store.SelectTab(index);
        public void ShowCareer(bool boardRoom) => _store.ShowCareer(boardRoom);

        /// <summary>Clears per-session UI (toasts, modals, events) for a new game.</summary>
        public void ResetSession()
        {
            CloseModal();
            HideDrop();
            HideOutage();
            foreach (var (rt, _, _) in _activeToasts) if (rt) Destroy(rt.gameObject);
            _activeToasts.Clear();
            _mailCountSeen = -1;
            _store.SelectTab(0);
        }
        public RectTransform ShipButton => _ship.Button;

        public void ShipFromKeyboard()
        {
            if (_desktop.gameObject.activeSelf && _modal == null) _ship.ShipFromKeyboard();
        }

        public void OnAutoClick(ClickResult r)
        {
            if (_desktop.gameObject.activeSelf) _ship.OnAutoClick(r);
        }

        // ------------------------------------------------------------------ fx
        public void SpawnFloat(PointerEventData e, double amount, bool crit)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_fx, e.position, e.pressEventCamera ?? _gm.Refs.MainCamera, out var local))
                SpawnFloatLocal(local, amount, crit);
        }

        public void SpawnFloatAt(RectTransform target, double amount, bool crit)
        {
            Vector3 world = target.TransformPoint(target.rect.center);
            Vector2 local = _fx.InverseTransformPoint(world);
            SpawnFloatLocal(local + new Vector2(Random.Range(-60f, 60f), 20f), amount, crit);
        }

        void SpawnFloatLocal(Vector2 local, double amount, bool crit)
        {
            FloatingText f;
            TextMeshProUGUI t;
            if (_floatPool.Count > 0)
            {
                f = _floatPool.Pop();
                t = f.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                t = UIKit.Text(_fx, "Float", "", 30, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                t.rectTransform.sizeDelta = new Vector2(360, 60);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.outlineWidth = 0.15f;
                t.outlineColor = new Color32(0, 0, 0, 160);
                f = t.gameObject.AddComponent<FloatingText>();
                f.Finished = x => { x.gameObject.SetActive(false); _floatPool.Push(x); };
            }
            t.text = (crit ? "CRIT! +" : "+") + NumberFormat.Short(amount);
            t.fontSize = crit ? 40 : 30;
            t.color = crit ? Theme.Gold : Color.white;
            t.rectTransform.anchoredPosition = local;
            f.gameObject.SetActive(true);
            f.Restart(local);
        }

        public void Toast(string message, Color accent, float seconds = 4.5f)
        {
            if (_toasts == null) return;
            var card = UIKit.Panel(_toasts, "Toast", Theme.Hex("#0B111B").WithAlpha(0.96f), 12);
            var rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(760, 48);
            var bar = UIKit.Panel(card.transform, "Accent", accent, 4);
            bar.rectTransform.Anchor(0, 0, 0, 1).Insets(10, 12, 0, 12);
            bar.rectTransform.sizeDelta = new Vector2(6, bar.rectTransform.sizeDelta.y);
            var t = UIKit.Text(card.transform, "Text", message, 18, Theme.Text, TextAlignmentOptions.MidlineLeft, UIFonts.Medium);
            t.rectTransform.Fill().Insets(28, 4, 16, 4);
            var cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            rt.anchoredPosition = new Vector2(0, -40);
            _activeToasts.Add((rt, Time.unscaledTime + seconds, cg));
            while (_activeToasts.Count > 3)
            {
                Destroy(_activeToasts[0].rt.gameObject);
                _activeToasts.RemoveAt(0);
            }
        }

        void UpdateToasts()
        {
            float y = 26;
            for (int i = _activeToasts.Count - 1; i >= 0; i--)
            {
                var (rt, until, cg) = _activeToasts[i];
                float left = until - Time.unscaledTime;
                if (left <= 0)
                {
                    Destroy(rt.gameObject);
                    _activeToasts.RemoveAt(i);
                    continue;
                }
                // ease into place, then stop touching it (every change rebuilds the toast canvas)
                var target = new Vector2(0, y);
                var p = rt.anchoredPosition;
                if ((p - target).sqrMagnitude > 0.25f)
                    rt.anchoredPosition = Vector2.Lerp(p, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12));
                else if (p != target) rt.anchoredPosition = target;
                float a = Mathf.Clamp01(left / 0.4f);
                if (!Mathf.Approximately(cg.alpha, a)) cg.alpha = a;
                y += 56;
            }
        }

        // ------------------------------------------------------------------ model drops & outages
        void BuildDropCard()
        {
            _dropCard = UIKit.Rect("ModelDrop", _desktop).TopLeft(600, 300, 400, 128);
            var glow = UIKit.Panel(_dropCard, "Glow", Theme.Gold.WithAlpha(0.5f), 20);
            glow.rectTransform.Fill(-5);
            var card = UIKit.Panel(_dropCard, "Card", Theme.Hex("#1B1430"), 16, true);
            card.rectTransform.Fill();
            var pulse = _dropCard.gameObject.AddComponent<Pulse>();
            pulse.Glow = glow;
            pulse.ScaleAmount = 0.04f;
            pulse.Speed = 5f;
            UIKit.Text(card.transform, "Kicker", "★ NEW MODEL DROP", 15, Theme.Gold, TextAlignmentOptions.TopLeft, UIFonts.Bold)
                 .rectTransform.TopLeft(20, 14, 360, 20);
            _dropText = UIKit.Text(card.transform, "Text", "", 19, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _dropText.rectTransform.TopLeft(20, 38, 364, 56);
            UIKit.Bar(card.transform, "Timer", Theme.PanelLight, Theme.Gold, out _dropTimer, 3).rectTransform.TopLeft(20, 104, 360, 6);
            PointerRelay.On(card).Click = _ => _gm.Model.ClaimDrop();
            _dropCard.gameObject.SetActive(false);
        }

        public void ShowDrop(ModelDrop d)
        {
            _dropText.text = $"{d.LabName} just released <color=#FFD166>{d.ModelName}</color>! <size=85%><color=#8A97AD>Click to try it.</color></size>";
            _dropCard.anchoredPosition = new Vector2(Random.Range(460f, 1150f), -Random.Range(110f, 560f));
            _dropCard.gameObject.SetActive(true);
        }

        public void HideDrop()
        {
            if (_dropCard) _dropCard.gameObject.SetActive(false);
        }

        void UpdateDrop()
        {
            var d = _gm.Model.ActiveDrop;
            if (d == null) { if (_dropCard.gameObject.activeSelf) HideDrop(); return; }
            UIKit.SetFill(_dropTimer, d.Remaining / GameDatabase.DropLifetime);
        }

        void BuildOutageBanner()
        {
            var b = UIKit.Panel(_desktop, "Outage", Theme.Bad, 12, true);
            _outage = b.rectTransform;
            _outage.TopLeft(452, 54, 560, 56);
            _outageText = UIKit.Text(b.transform, "Text", "", 17, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
            _outageText.rectTransform.Fill(6);
            b.gameObject.AddComponent<Pulse>().ScaleAmount = 0.015f;
            var punch = b.gameObject.AddComponent<Punch>();
            PointerRelay.On(b).Down = _ =>
            {
                punch.Play(0.06f);
                _gm.Sfx.Play(Util.Sound.Key, 0.6f, 0.7f);
                _gm.Model.ClickOutage();
            };
            _outage.gameObject.SetActive(false);
        }

        public void ShowOutage(Outage o) => _outage.gameObject.SetActive(true);
        public void HideOutage() { if (_outage) _outage.gameObject.SetActive(false); }

        void UpdateOutage()
        {
            var o = _gm.Model.ActiveOutage;
            if (o == null) { if (_outage.gameObject.activeSelf) HideOutage(); return; }
            _outageText.text = $"⚠ {o.LabName} API OUTAGE: production halved ({o.Remaining:0}s)\n" +
                               $"<size=80%>Click to fail over to a backup provider ({o.ClicksLeft} more)</size>";
        }

        // ------------------------------------------------------------------ inbox
        public int UnreadMail
        {
            get
            {
                var st = _gm.Model.State;
                if (st.mail.Count != _mailCountSeen || st.mailRead.Count != _readCountSeen)
                {
                    _mailCountSeen = st.mail.Count;
                    _readCountSeen = st.mailRead.Count;
                    _unreadCache = _gm.Model.UnreadMail;
                }
                return _unreadCache;
            }
        }

        void OnMailReceived(MailDef mail)
        {
            var who = StoryDatabase.Person(mail.From);
            _gm.Sfx.Play(Util.Sound.Mail, 0.7f);
            Toast($"✉ New email from {who.Name}: <b>{mail.Subject}</b>", Theme.Hex(who.ColorHex), 6f);
            if (mail.Important && _gm.Settings.autoOpenStoryMail && _desktop.gameObject.activeSelf && _modal == null &&
                _gm.Cam.Mode == Office.CamMode.Monitor)
                StartCoroutine(OpenInboxSoon(mail.Id));
        }

        System.Collections.IEnumerator OpenInboxSoon(string id)
        {
            yield return new WaitForSecondsRealtime(0.9f);
            if (_modal == null && _desktop.gameObject.activeSelf) ShowInbox(id);
        }

        public bool InboxOpen { get; private set; }

        public void ShowInbox(string openId)
        {
            var m = _gm.Model;
            var card = OpenModal(1280, 780, out _);
            InboxOpen = true;
            UIKit.Text(card, "Title", "✉  INBOX", 26, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold).rectTransform.TopLeft(28, 22, 400, 34);
            UIKit.Text(card, "Chapter", StoryDatabase.ChapterLine(m), 15, Theme.Gold,
                       TextAlignmentOptions.TopRight, UIFonts.Bold).rectTransform.TopLeft(560, 30, 600, 24);
            var close = UIKit.Button(card, "Close", Theme.PanelLight, CloseModal, 8);
            close.GetComponent<RectTransform>().TopLeft(1186, 18, 72, 36);
            close.Label("✕", 18, Theme.TextDim, UIFonts.Mono);

            var list = UIKit.ScrollList(card, "List", 6, out _);
            list.parent.GetComponent<RectTransform>().TopLeft(20, 74, 440, 686);
            var reader = UIKit.Panel(card, "Reader", Theme.PanelLight, 14);
            reader.rectTransform.TopLeft(476, 74, 784, 686);

            var avatar = UIKit.Image(reader.transform, "Avatar", Theme.Accent);
            avatar.sprite = UIKit.Circle;
            avatar.rectTransform.TopLeft(28, 26, 64, 64);
            var initials = UIKit.Text(avatar.transform, "Initials", "", 22, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
            initials.rectTransform.Fill();
            var from = UIKit.Text(reader.transform, "From", "", 20, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            from.rectTransform.TopLeft(108, 28, 640, 28);
            var role = UIKit.Text(reader.transform, "Role", "", 15, Theme.TextDim, TextAlignmentOptions.TopLeft);
            role.rectTransform.TopLeft(108, 58, 640, 22);
            var subject = UIKit.Text(reader.transform, "Subject", "", 28, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            subject.rectTransform.TopLeft(28, 110, 728, 72);
            var bodyList = UIKit.ScrollList(reader.transform, "Body", 0, out _);
            bodyList.parent.GetComponent<RectTransform>().TopLeft(28, 186, 740, 480);
            var body = UIKit.Text(bodyList, "Text", "", 20, Theme.Text, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 8;

            var ids = new List<string>(m.State.mail);
            ids.Reverse();
            if (ids.Count == 0)
            {
                UIKit.Set(subject, "No mail yet.");
                UIKit.Set(body, "<color=#8A97AD>Your inbox is empty. Enjoy it while it lasts.</color>");
                return;
            }
            var rows = new Dictionary<string, (Image bg, Image dot)>();

            void Open(string id)
            {
                var mail = StoryDatabase.MailById(id);
                if (mail == null) return;
                var who = StoryDatabase.Person(mail.From);
                avatar.color = Theme.Hex(who.ColorHex);
                initials.text = who.Initials;
                from.text = who.Name;
                role.text = $"{who.Role} · to {FlavorText.EmployeeName}";
                subject.text = mail.Subject;
                body.text = mail.Body;
                m.MarkRead(id);
                foreach (var kv in rows)
                {
                    kv.Value.bg.color = kv.Key == id ? Theme.PanelHover : Theme.Panel;
                    kv.Value.dot.enabled = !m.State.mailRead.Contains(kv.Key);
                }
                _gm.Sfx.Play(Util.Sound.Page, 0.6f);
            }

            foreach (var id in ids)
            {
                var mail = StoryDatabase.MailById(id);
                if (mail == null) continue;
                var who = StoryDatabase.Person(mail.From);
                string mailId = id;
                var b = UIKit.Button(list, id, Theme.Panel, () => Open(mailId), 10);
                b.GetComponent<RectTransform>().Height(78);
                var av = UIKit.Image(b.transform, "Avatar", Theme.Hex(who.ColorHex));
                av.sprite = UIKit.Circle;
                av.rectTransform.TopLeft(14, 15, 48, 48);
                UIKit.Text(av.transform, "I", who.Initials, 17, Color.white, TextAlignmentOptions.Center, UIFonts.Bold).rectTransform.Fill();
                UIKit.Text(b.transform, "From", who.Name, 17, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Bold).rectTransform.TopLeft(76, 14, 320, 22);
                UIKit.Text(b.transform, "Subject", mail.Subject, 15, Theme.TextDim, TextAlignmentOptions.TopLeft).rectTransform.TopLeft(76, 40, 330, 22);
                var dot = UIKit.Image(b.transform, "Unread", Theme.Gold);
                dot.sprite = UIKit.Circle;
                dot.rectTransform.TopLeft(406, 32, 12, 12);
                rows[id] = (b.GetComponent<Image>(), dot);
            }

            string first = openId;
            if (first == null) first = ids.FirstOrDefault(x => !m.State.mailRead.Contains(x)) ?? ids[0];
            Open(first);
        }

        // ------------------------------------------------------------------ modals
        bool _modalDismissable;

        RectTransform OpenModal(float w, float h, out Image card, bool dismissable = true)
        {
            CloseModal();
            var dim = UIKit.Image(_modals, "Modal", new Color(0.02f, 0.03f, 0.06f, 0.82f), true);
            dim.rectTransform.Fill();
            _modal = dim.gameObject;
            _modalDismissable = dismissable;
            if (dismissable) PointerRelay.On(dim).Click = _ => CloseModal();
            card = UIKit.Panel(dim.transform, "Card", Theme.Panel, 22, true); // raycast target: clicks on the card don't close it
            card.rectTransform.Center(w, h);
            return card.rectTransform;
        }

        public bool ModalOpen => _modal != null;

        /// <summary>Closes the current modal unless it must be answered (the performance review).</summary>
        public bool CloseDismissableModal()
        {
            if (_modal == null || !_modalDismissable) return false;
            CloseModal();
            return true;
        }

        public void CloseModal()
        {
            if (_modal) Destroy(_modal);
            _modal = null;
            InboxOpen = false;
        }

        public void ShowDayEndPrompt()
        {
            var card = OpenModal(640, 330, out _);
            UIKit.Text(card, "Title", "It's 5:00 PM.", 40, Theme.Warn, TextAlignmentOptions.Top, UIFonts.Bold).rectTransform.TopLeft(0, 36, 640, 50);
            var m = _gm.Model;
            bool met = m.State.earnedToday >= m.State.quotaToday;
            UIKit.Text(card, "Body",
                (met ? "<color=#3DDC97>Quota met.</color> " : "<color=#FF5D5D>Quota not met yet.</color> ") +
                "Clock out for your performance review, or stay late. Overtime counts toward today's quota.",
                19, Theme.TextDim, TextAlignmentOptions.Top).rectTransform.TopLeft(50, 100, 540, 90);
            var stay = UIKit.Button(card, "Stay", Theme.PanelLight, CloseModal, 12);
            stay.GetComponent<RectTransform>().TopLeft(50, 220, 260, 64);
            stay.Label("WORK LATE", 22, Theme.Text);
            var go = UIKit.Button(card, "ClockOut", Theme.Warn, () => { CloseModal(); _gm.ClockOut(); }, 12);
            go.GetComponent<RectTransform>().TopLeft(330, 220, 260, 64);
            go.Label("CLOCK OUT", 22, Theme.Bg);
        }

        public void ShowReview(DayReview r)
        {
            HideDrop();
            HideOutage();
            var card = OpenModal(760, 560, out _, dismissable: false);
            UIKit.Text(card, "Kicker", $"PERFORMANCE REVIEW · DAY {r.Day} · {FlavorText.Weekday(r.Day).ToUpperInvariant()}", 16, Theme.TextDim,
                       TextAlignmentOptions.Top, UIFonts.Medium).rectTransform.TopLeft(0, 34, 760, 24);
            UIKit.Text(card, "Verdict", r.Met ? "QUOTA MET ★" : "QUOTA MISSED", 52, r.Met ? Theme.Gold : Theme.Bad,
                       TextAlignmentOptions.Top, UIFonts.Bold).rectTransform.TopLeft(0, 64, 760, 64);

            UIKit.Text(card, "Numbers",
                $"Shipped today: <b>{NumberFormat.Short(r.Earned)}</b>  <color=#56627A>/</color>  quota {NumberFormat.Short(r.Quota)}" +
                (r.Met ? $"\nPerformance bonus: <color=#3DDC97>+{NumberFormat.Credits(r.Bonus)}</color>   ·   ★ {r.Stars} total (+{r.Stars}% credits/sec)" : ""),
                21, Theme.Text, TextAlignmentOptions.Top, UIFonts.Medium).rectTransform.TopLeft(40, 150, 680, 70);

            var bubble = UIKit.Panel(card, "Manager", Theme.PanelLight, 14);
            bubble.rectTransform.TopLeft(60, 248, 640, 130);
            var boss = StoryDatabase.Person(r.ManagerName == "Heirloom PM" ? "heirloom" : "dana");
            var face = UIKit.Image(bubble.transform, "Face", Theme.Hex(boss.ColorHex));
            face.sprite = UIKit.Circle;
            face.rectTransform.TopLeft(20, 25, 80, 80);
            UIKit.Text(face.transform, "Initials", boss.Initials, 26, Theme.Bg, TextAlignmentOptions.Center, UIFonts.Bold).rectTransform.Fill();
            UIKit.Text(bubble.transform, "Quote", $"<color=#8A97AD>{r.ManagerName} says:</color>\n\"{r.ManagerSays}\"", 20, Theme.Text,
                       TextAlignmentOptions.MidlineLeft).rectTransform.Fill().Insets(120, 10, 20, 10);

            var m = _gm.Model;
            double nightPreview = System.Math.Floor(m.NightShiftEstimate);
            UIKit.Text(card, "Night", $"Your agents will work the night shift: about <color=#4DD0E1>+{NumberFormat.Credits(nightPreview)}</color>",
                       17, Theme.TextDim, TextAlignmentOptions.Top).rectTransform.TopLeft(40, 396, 680, 26);
            var go = UIKit.Button(card, "GoHome", Theme.Accent, () => { CloseModal(); _gm.GoHome(); }, 14);
            go.GetComponent<RectTransform>().TopLeft(230, 450, 300, 72);
            go.Label("GO HOME  →", 26, Theme.Bg);
        }

        public void ShowFactoryBoot()
        {
            var card = OpenModal(1100, 600, out var img);
            img.color = Theme.Hex("#0A0E14");
            UIKit.Text(card, "Title", "SOFTWARE FACTORY", 64, Theme.Gold, TextAlignmentOptions.Top, UIFonts.Bold).rectTransform.TopLeft(0, 50, 1100, 80);
            UIKit.Text(card, "Sub", "ONLINE", 30, Theme.Good, TextAlignmentOptions.Top, UIFonts.Bold).rectTransform.TopLeft(0, 132, 1100, 40);
            var stages = new[] { "TICKETS", "SPECS", "CODE", "TESTS", "REVIEW", "DEPLOY" };
            for (int i = 0; i < stages.Length; i++)
            {
                var box = UIKit.Panel(card, stages[i], Theme.PanelLight, 12);
                box.rectTransform.TopLeft(60 + i * 168, 240, 140, 110);
                UIKit.Text(box.transform, "L", stages[i], 20, Theme.Text, TextAlignmentOptions.Center, UIFonts.Bold).rectTransform.Fill();
                var p = box.gameObject.AddComponent<Pulse>();
                p.Speed = 4f + i * 0.6f;
                p.ScaleAmount = 0.05f;
                if (i < stages.Length - 1)
                    UIKit.Text(card, "Arrow", "→", 34, Theme.Accent, TextAlignmentOptions.Center, UIFonts.Bold).rectTransform.TopLeft(200 + i * 168, 268, 28, 50);
            }
            var m = _gm.Model;
            UIKit.Text(card, "Body", m.State.factoriesBuilt <= 1
                ? "All agents connected. Every ticket is now written, coded, tested, reviewed and deployed without you.\n" +
                  "<color=#4DD0E1>Automation: 100%.</color>  You can finally lean back."
                : $"Factory #{m.State.factoriesBuilt} is online in {m.DivisionName}. All production x2.\n" +
                  "<color=#4DD0E1>Automation: 100%.</color>  Reorg from the FACTORY tab whenever you're ready.",
                22, Theme.TextDim, TextAlignmentOptions.Top).rectTransform.TopLeft(80, 400, 940, 110);
            var close = UIKit.Button(card, "Close", Theme.PanelLight, CloseModal, 10);
            close.GetComponent<RectTransform>().TopLeft(450, 520, 200, 50);
            close.Label("CLOSE", 18, Theme.Text);
        }
    }
}
