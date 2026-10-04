using System;
using System.Collections;
using System.Linq;
using AgentClicker.Core;
using AgentClicker.Office;
using AgentClicker.UI;
using AgentClicker.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AgentClicker
{
    /// <summary>
    /// Owns the <see cref="GameModel"/> and runs the flow:
    /// Title → (intro) → Morning (login screen) → Working → 17:00 → Review → Night → next Morning … → Factory → Epilogue.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public SceneRefs Refs;
        public float AutosaveSeconds = 15f;

        public GameModel Model { get; private set; }
        public GameSettings Settings { get; set; }
        public CameraRig Cam { get; private set; }
        public OfficeDirector Office { get; private set; }
        public EmployeeController Employee { get; private set; }
        public TimeOfDay TimeOfDay { get; private set; }
        public ComputerUI Computer { get; private set; }
        public Overlay Overlay { get; private set; }
        public MenuUI Menu { get; private set; }
        public CallUI Calls { get; private set; }
        public Sfx Sfx { get; private set; }

        public bool SavingEnabled { get; set; } = true;
        public bool SuppressShowcase { get; set; }
        /// <summary>Bulk purchases (Buy All) report once instead of toasting every item.</summary>
        public bool SuppressPurchaseToasts { get; set; }
        public bool InEnding { get; private set; }
        public bool OnTitle { get; private set; }

        float _saveTimer, _tutorialTimer;
        bool _boardRoomOnLogin;
        double _offlineGain;
        bool _automated;   // tour / benchmark: skip the title screen
        int _lastHour = -1;
        ProbeRefresher _probe;
        readonly System.Random _rng = new System.Random();

        void Awake()
        {
            Instance = this;
            Settings = GameSettings.Load();

            string[] args = Environment.GetCommandLineArgs();
            float? dayLengthOverride = null;
            string tourDir = null;
            bool benchmark = false, demo = false;
            string recordPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-reset") SaveSystem.Delete();
                if (args[i] == "-daylength" && i + 1 < args.Length && float.TryParse(args[i + 1], out var d)) dayLengthOverride = d;
                if (args[i] == "-tour" && i + 1 < args.Length) tourDir = args[i + 1];
                if (args[i] == "-benchmark") benchmark = true;
                if (args[i] == "-demo") demo = true;
                if (args[i] == "-record" && i + 1 < args.Length) { recordPath = args[i + 1]; demo = true; }
            }
            _automated = tourDir != null || benchmark;

            if (demo)
            {
                // scripted showcase: fresh career, default settings, never touches the real save
                Settings = new GameSettings();
                SavingEnabled = false;
            }
            GameState state = _automated || demo ? null : SaveSystem.Load();
            Model = new GameModel(state, 0, dayLengthOverride ?? Settings.DayLengthSeconds);
            if (state != null)
            {
                // Always resume at the start of a session: back at the login screen of the same day.
                if (Model.Phase == GamePhase.Working || Model.Phase == GamePhase.Review) Model.State.Phase = GamePhase.Login;
                double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Model.State.lastSaveUnix;
                _offlineGain = Model.ApplyOffline(away);
            }

            Sfx = gameObject.AddComponent<Sfx>();
            Cam = gameObject.AddComponent<CameraRig>();
            Office = gameObject.AddComponent<OfficeDirector>();
            Employee = gameObject.AddComponent<EmployeeController>();
            TimeOfDay = gameObject.AddComponent<TimeOfDay>();
            Computer = gameObject.AddComponent<ComputerUI>();
            Overlay = gameObject.AddComponent<Overlay>();
            Menu = gameObject.AddComponent<MenuUI>();
            Calls = gameObject.AddComponent<CallUI>();

            if (demo)
                gameObject.AddComponent<Demo>().RecordPath = recordPath;
            else if (benchmark)
            {
                SavingEnabled = false;
                gameObject.AddComponent<Benchmark>();
            }
            else if (tourDir != null)
            {
                SavingEnabled = false;
                gameObject.AddComponent<Tour>().OutputDir = tourDir;
            }
        }

        void Start()
        {
            UIFonts.Prewarm();
            Cam.Init(Refs);
            TimeOfDay.Init(Refs);
            Office.Init(Model, Refs);
            Employee.Init(Model, Refs);
            Computer.Init(this);
            Overlay.Init(this);
            Menu.Init(this);
            Calls.Init(this);
            foreach (var side in FindObjectsByType<SideScreen>(FindObjectsInactive.Include))
                side.Init(this);
            _probe = FindAnyObjectByType<ProbeRefresher>();
            HookEvents();
            ApplySettings(save: false);

            if (_automated)
            {
                Model.State.introSeen = true;
                BeginMorning(false);
            }
            else ShowTitle();
        }

        public void ApplySettings(bool save = true)
        {
            Settings.Clamp();
            SettingsApplier.ApplyAll(Settings, Refs, Sfx, Model);
            Cam.MouseSensitivity = Settings.mouseSensitivity;
            if (save) Settings.Save();
        }

        void OnApplicationFocus(bool focused)
        {
            if (Settings == null || Sfx == null) return;
            SettingsApplier.ApplyFramePacing(Settings, focused || _automated);
            SettingsApplier.ApplyAudio(Settings, Sfx, focused || _automated);
        }

        void HookEvents()
        {
            Model.Purchased += OnPurchased;
            Model.Promoted += t =>
            {
                Sfx.Play(Sound.Promotion);
                Office.Refresh(true);
                if (_probe) _probe.RequestRender(1f);
                Employee.PlayOneShot(EmployeeController.Celebrate, 1.6f);
                Computer.Toast(FlavorText.PromotionLine(GameDatabase.Titles[t]), Theme.Gold, 6f);
            };
            Model.DayEndReached += () =>
            {
                Sfx.Play(Sound.Bell);
                Computer.ShowDayEndPrompt();
            };
            Model.ClockedOut += review =>
            {
                Sfx.Play(Sound.Bell, 0.7f);
                Computer.ShowReview(review);
                Employee.PlayOneShot(review.Met ? EmployeeController.Celebrate : EmployeeController.Facepalm, 1.8f);
                Save();
            };
            Model.DropSpawned += d => { Sfx.Play(Sound.Drop); Computer.ShowDrop(d); };
            Model.DropExpired += () => Computer.HideDrop();
            Model.DropClaimed += r =>
            {
                Sfx.Play(Sound.DropClaim);
                Computer.HideDrop();
                Computer.Toast(r.Headline, Theme.Accent, 5f);
                if (r.Outcome != DropOutcome.Funding) Employee.PlayOneShot(EmployeeController.Celebrate, 1.4f);
            };
            Model.OutageStarted += o =>
            {
                Sfx.Play(Sound.Alert);
                Computer.ShowOutage(o);
                Employee.PlayOneShot(EmployeeController.Facepalm, 1.8f);
            };
            Model.OutageEnded += fixedIt =>
            {
                Computer.HideOutage();
                if (fixedIt) { Sfx.Play(Sound.Fixed); Computer.Toast("Failed over to a backup provider. Production restored!", Theme.Good); }
            };
            Model.Clicked += r =>
            {
                if (!r.Auto) return;
                Computer.OnAutoClick(r);
                Employee.NotifyClick();
            };
            // the first Factory gets the full ending; later divisions get a victory lap
            Model.FactoryBuilt += () => StartCoroutine(Model.State.endingSeen ? FactoryAgainSequence() : EndingSequence());
            Model.AchievementsUnlocked += OnTrophies;
            Model.PerkBought += p =>
            {
                Sfx.Play(Sound.BigBuy);
                Computer.Toast($"<color=#FFD166>◆ BOARD ROOM</color>  {p.Name}" + (p.Repeatable ? $" #{Model.State.boardSeats}" : "") + " acquired.", Theme.Gold, 5f);
            };

            // phone calls
            Model.CallIncoming += call =>
            {
                Sfx.StartRing();
                if (Refs.Phone) Refs.Phone.SetRinging(true);
                Calls.ShowIncoming(call);
            };
            Model.CallAnswered += call =>
            {
                Sfx.StopRing();
                Sfx.Play(Sound.PickUp);
                if (Refs.Phone) { Refs.Phone.SetRinging(false); Refs.Phone.SetOffHook(true); }
                Employee.SetOnPhone(true);
                Computer.HideDrop();
                Cam.BeginCall();
                Calls.ShowDialogue(call);
            };
            Model.CallEnded += (def, choice, summary) =>
            {
                Sfx.StopRing();
                if (Refs.Phone) Refs.Phone.SetRinging(false);
                if (choice != null) Calls.ShowResponse(choice, summary, FinishCall);
                else
                {
                    Calls.HideAll();
                    FinishCall();
                    if (!string.IsNullOrEmpty(summary)) Computer.Toast($"You ignored {def.Name}. <color=#FF5D5D>{summary}</color>", Theme.Bad, 5f);
                }
            };
            Model.CallMissed += def => Computer.Toast($"☎ Missed call from {def.Name}.", Theme.TextDim, 5f);
            Model.PerkUnlocked += p =>
            {
                Sfx.Play(Sound.Promotion, 0.7f);
                Computer.Toast($"★ <b>{CallDatabase.PeopleNames[(int)p]}</b> likes you. {CallDatabase.PerkNames[(int)p]}", Theme.Gold, 8f);
            };
            Model.AskCompleted += (ask, reward) =>
            {
                Sfx.Play(Sound.Fixed);
                var def = AskDatabase.ById(ask.id);
                Computer.Toast($"✓ Ask done: {def?.Text(ask.target)}  <color=#3DDC97>+{NumberFormat.Credits(reward)}</color>", Theme.Good, 5f);
            };
            Model.ChapterChanged += ch =>
            {
                if (InEnding) return; // the ending has its own fanfare
                Sfx.Play(Sound.Chapter);
                Overlay.ShowChapterBanner(ch);
            };
        }

        /// <summary>Hang up: handset back on the cradle, camera back where it was.</summary>
        void FinishCall()
        {
            if (Refs.Phone && Refs.Phone.isActiveAndEnabled) { Refs.Phone.SetOffHook(false); Sfx.Play(Sound.HangUp, 0.8f); }
            Employee.SetOnPhone(false);
            Cam.EndCall();
        }

        void OnPurchased(Purchase p)
        {
            switch (p.Kind)
            {
                case PurchaseKind.Agent:
                    Sfx.Play(Sound.Buy);
                    int idx = GameDatabase.AgentIndex(p.Id);
                    if (Model.AgentCount(idx) == p.Count)
                        Computer.Toast($"Hired your first {GameDatabase.Agents[idx].Name} from {GameDatabase.Lab(GameDatabase.Agents[idx].LabId).Name}!", Theme.Accent);
                    break;
                case PurchaseKind.Upgrade:
                    if (SuppressPurchaseToasts) break;
                    Sfx.Play(Sound.Buy, 0.9f, 1.15f);
                    Computer.Toast($"Upgrade unlocked: {GameDatabase.Upgrade(p.Id).Name}", Theme.Accent2);
                    break;
                case PurchaseKind.Office:
                    Sfx.Play(Sound.BigBuy);
                    Office.Refresh(true);
                    if (_probe) _probe.RequestRender(1.2f);
                    Computer.Toast(FlavorText.OfficeInstalled(GameDatabase.Office(p.Id)), Theme.Gold);
                    if (!SuppressShowcase && Settings.purchaseShowcase) Cam.Showcase(Office.ShowcaseTarget(p.Id));
                    Employee.PlayOneShot(EmployeeController.Celebrate, 1.4f);
                    break;
            }
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.25f);
            Model.Tick(dt);

            float hour = OnTitle ? 16.6f : Model.ClockHours;
            TimeOfDay.Apply(hour);
            int wholeHour = (int)hour;
            if (wholeHour != _lastHour)
            {
                // the sky changed noticeably: refresh reflections now and then, never every frame
                _lastHour = wholeHour;
                if (_probe) _probe.RequestRender();
            }

            if (SavingEnabled && !OnTitle)
            {
                _saveTimer += Time.unscaledDeltaTime;
                if (_saveTimer >= AutosaveSeconds) Save();
            }

            UpdateTutorial(dt);
            HandleKeys();
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null || InEnding || Menu.Blocking || Calls.Busy) return;
            if (kb.tabKey.wasPressedThisFrame) Cam.Toggle();
            if (Model.IsWorking && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                Computer.ShipFromKeyboard();
            if (kb.f12Key.wasPressedThisFrame)
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                ScreenCapture.CaptureScreenshot(path);
                Computer.Toast("Screenshot saved: " + path, Theme.TextDim);
            }
        }

        void OnApplicationQuit() => Save();

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        public void Save()
        {
            _saveTimer = 0;
            if (SavingEnabled && Model.State.introSeen) SaveSystem.Save(Model.State);
        }

        // ------------------------------------------------------------------ title & menus
        public void ShowTitle()
        {
            OnTitle = true;
            InEnding = false;
            Time.timeScale = 1f;
            Overlay.HideNight();
            Overlay.HideDayCard();
            Overlay.HideEnding();
            Overlay.FadeIn(0.7f);
            Computer.ShowLogin();
            Office.Refresh(false);
            Cam.SetMode(CamMode.Menu, 0.01f);
            Menu.ShowTitle();
        }

        public void ContinueGame()
        {
            OnTitle = false;
            Menu.HideAll();
            if (Model.Phase == GamePhase.Night) Overlay.ShowNight(0, instant: true);
            else BeginMorning(false);
            if (_offlineGain > 0)
            {
                Computer.Toast($"While you were away, your agents earned {NumberFormat.Credits(_offlineGain)} " +
                               $"<color=#8A97AD>({NumberFormat.Percent(Model.OfflineEfficiency)} rate, up to {NumberFormat.Duration(Model.OfflineCapSeconds)})</color>", Theme.Good, 6f);
                _offlineGain = 0;
            }
        }

        public void NewGame()
        {
            if (SavingEnabled) SaveSystem.Delete();
            Model.Load(new GameState());
            Model.DayLengthSeconds = Settings.DayLengthSeconds;
            _offlineGain = 0;
            Office.Refresh(false);
            Computer.ResetSession();
            Menu.HideAll();
            Menu.ShowStoryCards(StoryDatabase.Intro(), () =>
            {
                OnTitle = false;
                Model.State.introSeen = true;
                Save();
                BeginMorning(false);
            });
        }

        public void QuitToTitle()
        {
            if (Model.Phase == GamePhase.Working || Model.Phase == GamePhase.Review) Model.State.Phase = GamePhase.Login;
            Save();
            Computer.CloseModal();
            ShowTitle();
        }

        public void QuitGame()
        {
            Save();
            Application.Quit();
        }

        // ------------------------------------------------------------------ tutorial
        void UpdateTutorial(float dt)
        {
            if (!Settings.tutorialTips || !Model.IsWorking || OnTitle) return;
            _tutorialTimer -= dt;
            if (_tutorialTimer > 0) return;
            _tutorialTimer = 0.5f;
            var s = Model.State;
            string tip = null;
            switch (s.tutorialStep)
            {
                case 0:
                    tip = "Click SHIP CODE (or press Space) to ship code and earn compute credits.";
                    break;
                case 1 when s.credits >= 15 && Model.TotalAgents == 0:
                    tip = "You can afford an agent! Hire an <b>Autocomplete</b> in the ModelMart on the right.";
                    break;
                case 1 when Model.TotalAgents > 0:
                    s.tutorialStep++;
                    return;
                case 2 when Model.TotalAgents > 0:
                    tip = "Agents earn credits every second, even when your hands are off the keyboard.";
                    break;
                case 3 when s.credits >= 50 && !Model.HasOffice("mug"):
                    tip = "Check the <b>OFFICE</b> tab: desk gadgets appear in your office and boost your output.";
                    break;
                case 3 when Model.HasOffice("mug"):
                    s.tutorialStep++;
                    return;
                case 4 when Model.AvailableUpgrades().Any():
                    tip = "An <b>UPGRADE</b> is available. Upgrades multiply your agents' output forever.";
                    break;
                case 5 when s.mail.Count >= 2:
                    tip = "You've got mail. Click <b>✉ Inbox</b> in the top bar to keep up with the office drama.";
                    break;
                case 6 when s.dropsClaimed == 0 && Model.ActiveDrop != null:
                    tip = "A new model just dropped! Click the gold card before it disappears.";
                    break;
                case 6 when s.dropsClaimed > 0:
                    s.tutorialStep++;
                    return;
                case 7 when Model.CanReorg && s.endingSeen:
                    tip = "You can <b>REORG</b> in the FACTORY tab: start over in a new division with Stock Options (+1% production each, forever).";
                    break;
                case 8 when s.options >= 1:
                    tip = "Spend your Stock Options on permanent perks: <b>FACTORY → BOARD ROOM</b>. The Starter Kit is a great first buy.";
                    break;
                default:
                    return;
            }
            s.tutorialStep++;
            Computer.Toast("<color=#4DD0E1>TIP</color>  " + tip, Theme.Accent, 9f);
        }

        // ------------------------------------------------------------------ day flow
        public void BeginMorning(bool newDay)
        {
            OnTitle = false;
            InEnding = false;
            Time.timeScale = 1f;
            Office.Refresh(newDay);
            if (_probe) _probe.RequestRender(0.5f);
            Computer.ShowLogin();
            Cam.SetMode(CamMode.Office, 0.01f);
            Overlay.ShowDayCard(Model.State.day);
            Employee.PlayOneShot(EmployeeController.Stretch, 2.5f);
            if (newDay)
            {
                string change = FlavorText.DayChange(Model.State.day);
                if (change != null) Computer.Toast(change, Theme.TextDim, 7f);
            }
        }

        public void Login()
        {
            if (Model.Phase != GamePhase.Login) return;
            Model.ClockIn();
            Sfx.Play(Sound.Login);
            Computer.ShowDesktop();
            if (_boardRoomOnLogin)
            {
                // fresh division: spend the new options before the first click
                _boardRoomOnLogin = false;
                Computer.ShowCareer(true);
            }
            Cam.SetMode(CamMode.Monitor, 1.3f);
        }

        public ClickResult Ship()
        {
            var r = Model.Click();
            Employee.NotifyClick();
            Sfx.Play(r.Crit ? Sound.Crit : Sound.Key, r.Crit ? 0.8f : 0.45f);
            return r;
        }

        public void ClockOut() => Model.ClockOut();

        public void GoHome()
        {
            double night = Model.GoHome();
            Save();
            Sfx.Play(Sound.Whoosh);
            Cam.SetMode(CamMode.Office, 1.2f);
            Computer.ShowLocked();
            Overlay.ShowNight(night);
        }

        public void ClockIn()
        {
            Model.StartNextDay();
            Save();
            BeginMorning(true);
        }

        public void BuildFactory()
        {
            if (!Model.BuildFactory()) Sfx.Play(Sound.Error);
        }

        IEnumerator EndingSequence()
        {
            InEnding = true;
            Save();
            Sfx.Play(Sound.Promotion);
            Computer.ShowFactoryBoot();
            Office.Refresh(true);
            if (_probe) _probe.RequestRender(1f);
            yield return new WaitForSeconds(4.5f);
            Computer.CloseModal();
            Cam.SetMode(CamMode.Ending, 3.5f);
            Employee.PlayOneShot(EmployeeController.FeetUp, 0.1f);
            yield return new WaitForSeconds(3.0f);
            Overlay.ShowEnding();
        }

        IEnumerator FactoryAgainSequence()
        {
            Save();
            Sfx.Play(Sound.Promotion);
            Computer.ShowFactoryBoot();
            Office.Refresh(true);
            if (_probe) _probe.RequestRender(1f);
            Employee.PlayOneShot(EmployeeController.Celebrate, 1.6f);
            yield return new WaitForSeconds(1.5f);
            Computer.Toast($"Factory #{Model.State.factoriesBuilt} online in {Model.DivisionName}. Production x2. Reorg from the FACTORY tab whenever you like.", Theme.Gold, 8f);
        }

        void OnTrophies(System.Collections.Generic.List<AchievementDef> list)
        {
            if (OnTitle) return;
            Sfx.Play(Sound.Trophy, 0.8f);
            if (list.Count == 1)
                Computer.Toast($"<color=#FFD166>★ TROPHY</color>  <b>{list[0].Name}</b>  <color=#8A97AD>{list[0].Description}</color>", Theme.Gold, 5f);
            else
                Computer.Toast($"<color=#FFD166>★ {list.Count} TROPHIES</color>  {list[0].Name}, {list[1].Name}{(list.Count > 2 ? "…" : "")}  " +
                               $"<color=#8A97AD>Clout +{NumberFormat.Percent(Model.Clout)}</color>", Theme.Gold, 6f);
        }

        // ------------------------------------------------------------------ reorg (prestige)
        public void RequestReorg()
        {
            if (!Model.CanReorg || InEnding) { Sfx.Play(Sound.Error); return; }
            if (Model.ActiveCall != null) { Computer.Toast("Finish your phone call first.", Theme.TextDim); return; }
            double pending = Model.PendingOptions;
            Menu.Confirm($"Reorg to <color=#FFD166>{Model.NextDivisionName}</color>?\n" +
                         $"<size=70%><color=#8A97AD>Credits, agents, upgrades{(Model.HasBoardPerk("pack_your_desk") ? "" : ", gadgets")} and the day start over.\n" +
                         $"You gain</color> <color=#FFD166>+{NumberFormat.Short(pending)} Stock Options</color><color=#8A97AD> (+{NumberFormat.Percent(Model.OptionValue * pending)} production, forever).</color></size>",
                         "REORG", ReorgNow, Theme.Gold);
        }

        /// <summary>Reorgs without asking (the confirm dialog's YES).</summary>
        public void ReorgNow()
        {
            if (Model.CanReorg && !InEnding) StartCoroutine(ReorgSequence());
        }

        IEnumerator ReorgSequence()
        {
            InEnding = true;
            Computer.CloseModal();
            Sfx.Play(Sound.Whoosh);
            string from = Model.DivisionName, to = Model.NextDivisionName, blurb = GameDatabase.DivisionBlurb(Model.State.reorgs + 1);
            double gained = Model.Reorg();
            if (gained <= 0) { InEnding = false; yield break; }
            var s = Model.State;
            _boardRoomOnLogin = s.options >= 1;
            Computer.ResetSession();
            Office.Refresh(false);
            if (_probe) _probe.RequestRender(0.5f);
            Computer.ShowLogin();
            Cam.SetMode(CamMode.Office, 0.01f);
            Save();
            Sfx.Play(Sound.Chapter);
            Menu.ShowStoryCards(new System.Collections.Generic.List<StoryCard>
            {
                new StoryCard("MEMO · FROM REX HALVORSEN, CEO", $"The Factory is going to {to}.",
                    $"{from} runs itself now. Pack your duck, Sam. You're rolling the Factory out on the next floor.\n\n" +
                    "New desk, new quota, new title. Same Sam."),
                new StoryCard("STOCK OPTIONS VESTED", $"+{NumberFormat.Short(gained)} options",
                    $"You now hold <color=#FFD166>◆ {NumberFormat.Short(s.options)}</color> to spend and have earned {NumberFormat.Short(s.optionsEarned)} in total: " +
                    $"<color=#3DDC97>+{NumberFormat.Percent(Model.OptionValue * s.optionsEarned)} production</color> in every division, forever.\n\n" +
                    "Spend them on permanent perks in <b>FACTORY → BOARD ROOM</b>."),
                new StoryCard($"DIVISION {s.reorgs + 1}", to, blurb),
            }, () =>
            {
                InEnding = false;
                BeginMorning(true);
            });
            yield break;
        }

        public void PlayEpilogue()
        {
            Overlay.HideEnding();
            Sfx.DuckMusic(0.7f);
            Menu.ShowStoryCards(StoryDatabase.Epilogue(Model), () => Menu.ShowCredits(() =>
            {
                Sfx.DuckMusic(1f);
                KeepPlaying();
            }));
        }

        public void KeepPlaying()
        {
            InEnding = false;
            Model.State.endingSeen = true;
            Overlay.HideEnding();
            Cam.SetMode(CamMode.Office, 1.5f);
            if (Model.IsWorking) Computer.ShowDesktop();
            Save();
        }

        public string RandomTicker() => FlavorText.DayTicker[_rng.Next(FlavorText.DayTicker.Length)];
        public string RandomMorningEmail(int day) => FlavorText.MorningEmails[(day * 7 + 3) % FlavorText.MorningEmails.Length];
    }
}
