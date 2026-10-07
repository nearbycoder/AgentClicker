using System.Linq;
using AgentClicker.Core;
using NUnit.Framework;
using UnityEngine;

namespace AgentClicker.Tests
{
    public class StoryTests
    {
        static GameModel Working()
        {
            var m = new GameModel(new GameState(), seed: 9);
            m.ClockIn();
            return m;
        }

        [Test]
        public void FirstEmailIsTheCeoMandate()
        {
            var m = Working();
            Assert.AreEqual("ceo_mandate", m.DeliverNextMail().Id);
            Assert.AreEqual(1, m.UnreadMail);
        }

        [Test]
        public void ImportantMailWaitsForTheFirstLinesOfCode()
        {
            var m = Working();
            var mandate = m.DeliverNextMail();
            Assert.IsTrue(mandate.Important);
            Assert.IsFalse(StoryDatabase.ShouldAutoOpen(m, mandate), "day 1 starts with SHIP CODE, not the inbox");
            for (int i = 0; i < StoryDatabase.AutoOpenAfterClicks - 1; i++) m.Click();
            Assert.IsFalse(StoryDatabase.ShouldAutoOpen(m, mandate));
            m.Click();
            Assert.IsTrue(StoryDatabase.ShouldAutoOpen(m, mandate));
            Assert.IsFalse(StoryDatabase.ShouldAutoOpen(m, StoryDatabase.MailById("dana_welcome")), "only important mail opens itself");
        }

        [Test]
        public void MailIsDeliveredOnceAndOnlyWhenTriggered()
        {
            var m = Working();
            m.DeliverNextMail();
            Assert.IsNull(m.DeliverNextMail(), "nothing else is due on a fresh day 1");
            for (int i = 0; i < 20; i++) m.Click();
            Assert.AreEqual("dana_welcome", m.DeliverNextMail().Id);
            Assert.IsNull(m.DeliverNextMail());
            Assert.AreEqual(2, m.State.mail.Distinct().Count());
        }

        [Test]
        public void TickDeliversMailWithACooldown()
        {
            var m = Working();
            int received = 0;
            m.MailReceived += _ => received++;
            m.State.agentCounts[0] = 10; // unlocks several emails at once
            m.State.clicks = 100;
            m.MarkDirty();
            for (int i = 0; i < 5; i++) m.Tick(1f);
            Assert.AreEqual(1, received, "only one email in the first few seconds");
            for (int i = 0; i < 30; i++) m.Tick(1f);
            Assert.Greater(received, 2);
        }

        [Test]
        public void MarkReadClearsUnread()
        {
            var m = Working();
            var mail = m.DeliverNextMail();
            m.MarkRead(mail.Id);
            Assert.AreEqual(0, m.UnreadMail);
            m.MarkRead(mail.Id);
            Assert.AreEqual(1, m.State.mailRead.Count);
        }

        [Test]
        public void ChaptersFollowProgress()
        {
            var m = Working();
            Assert.AreEqual(1, m.Chapter);
            m.State.agentCounts[0] = 1;
            Assert.AreEqual(2, m.Chapter);
            m.State.titleIndex = 2;
            Assert.AreEqual(3, m.Chapter);
            m.State.titleIndex = 4;
            Assert.AreEqual(4, m.Chapter);
            m.State.agentCounts[GameDatabase.OrchestratorIndex] = 1;
            Assert.AreEqual(5, m.Chapter);
            m.State.factoryBuilt = true;
            Assert.AreEqual(6, m.Chapter);
        }

        [Test]
        public void HiringAProductManagerReplacesDana()
        {
            var m = Working();
            Assert.AreEqual("Dana Whitfield", FlavorText.ManagerName(m));
            m.State.agentCounts[GameDatabase.AgentIndex("pm")] = 1;
            Assert.AreEqual("Heirloom PM", FlavorText.ManagerName(m));
            var review = m.ClockOut();
            Assert.AreEqual("Heirloom PM", review.ManagerName);
        }

        [Test]
        public void EveryMailHasAKnownSenderAndUniqueId()
        {
            Assert.AreEqual(StoryDatabase.Mail.Length, StoryDatabase.Mail.Select(x => x.Id).Distinct().Count());
            foreach (var mail in StoryDatabase.Mail)
            {
                Assert.IsTrue(StoryDatabase.Cast.Any(c => c.Id == mail.From), mail.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(mail.Body), mail.Id);
            }
        }

        [Test]
        public void FactoryDayIsRecordedForEpilogueMail()
        {
            var m = Working();
            m.State.credits = GameDatabase.FactoryCost * 2;
            for (int i = 0; i < GameDatabase.Agents.Length; i++) m.State.agentCounts[i] = GameDatabase.FactoryOrchestrators;
            m.State.office.Add("recliner");
            m.Load(m.State);
            m.ClockIn();
            m.State.day = 7;
            Assert.IsTrue(m.BuildFactory());
            Assert.AreEqual(7, m.State.factoryDay);
            Assert.IsFalse(StoryDatabase.MailById("priya_lunch").Trigger(m));
            m.State.day = 8;
            Assert.IsTrue(StoryDatabase.MailById("priya_lunch").Trigger(m));
        }

        [Test]
        public void StoryProgressSurvivesSaving()
        {
            var m = Working();
            m.DeliverNextMail();
            m.State.dropsClaimed = 2;
            var copy = SaveSystem.FromJson(SaveSystem.ToJson(m.State));
            Assert.AreEqual(1, copy.mail.Count);
            Assert.AreEqual(2, copy.dropsClaimed);
        }
    }

    public class SettingsTests
    {
        [Test]
        public void ClampKeepsValuesInRange()
        {
            var s = new GameSettings { quality = 99, fpsCap = -3, renderScale = 4, masterVolume = -1, fieldOfView = 5, dayLength = 9 }.Clamp();
            Assert.AreEqual(GameSettings.QualityNames.Length - 1, s.quality);
            Assert.AreEqual(0, s.fpsCap);
            Assert.AreEqual(1f, s.renderScale);
            Assert.AreEqual(0f, s.masterVolume);
            Assert.AreEqual(40f, s.fieldOfView);
            Assert.AreEqual(GameSettings.DayLengths.Length - 1, s.dayLength);
        }

        [Test]
        public void SettingsRoundTripThroughJson()
        {
            var s = new GameSettings { quality = 0, musicVolume = 0.2f, dayLength = 3, showFps = true };
            var copy = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(s));
            Assert.AreEqual(0, copy.quality);
            Assert.AreEqual(0.2f, copy.musicVolume, 1e-6);
            Assert.AreEqual(720f, copy.DayLengthSeconds);
            Assert.IsTrue(copy.showFps);
        }

        [Test]
        public void MuteSilencesEverythingAndKeepsTheVolume()
        {
            var s = new GameSettings { masterVolume = 0.6f };
            Assert.AreEqual(0.6f, s.ListenerVolume(true), 1e-6);
            Assert.AreEqual(0.6f, s.ListenerVolume(false), 1e-6, "a background window keeps playing by default");
            s.muted = true;
            Assert.AreEqual(0f, s.ListenerVolume(true));
            Assert.AreEqual(0f, s.ListenerVolume(false));
            Assert.AreEqual(0.6f, s.masterVolume, 1e-6, "muting doesn't move the slider");
            s.muted = false;
            s.muteInBackground = true;
            Assert.AreEqual(0.6f, s.ListenerVolume(true), 1e-6);
            Assert.AreEqual(0f, s.ListenerVolume(false), "unmuted but in the background with mute-in-background on");
            var copy = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(new GameSettings { muted = true }));
            Assert.IsTrue(copy.muted, "mute survives a restart");
            Assert.IsFalse(new GameSettings().muted);
        }

        [Test]
        public void UnlimitedFrameRateIsZero()
        {
            var s = new GameSettings { fpsCap = GameSettings.FpsCaps.Length - 1 };
            Assert.AreEqual(0, s.TargetFps);
        }

        [Test]
        public void MusicLoopRendersSeamlessAudio()
        {
            var data = Util.LofiMusic.Render(8000, 3);
            Assert.Greater(data.Length, 8000 * 20);
            Assert.IsTrue(data.All(v => v >= -1f && v <= 1f && !float.IsNaN(v)));
            Assert.Greater(data.Max(v => Mathf.Abs(v)), 0.05f, "not silent");
        }
    }
}
