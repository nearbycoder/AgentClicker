using System.IO;
using AgentClicker.Core;
using NUnit.Framework;

namespace AgentClicker.Tests
{
    public class SaveSystemTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "agentclicker-save-test-" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.Folder = _dir;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Folder = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static GameState State(double credits) => new GameState { credits = credits, introSeen = true };

        [Test]
        public void SavingTwiceKeepsTheSaveAndOneBackup()
        {
            SaveSystem.Save(State(1));
            Assert.IsTrue(File.Exists(SaveSystem.FilePath));
            Assert.IsFalse(File.Exists(SaveSystem.BackupPath), "nothing to back up on the first save");
            SaveSystem.Save(State(2));
            Assert.IsTrue(File.Exists(SaveSystem.BackupPath));
            Assert.IsFalse(File.Exists(SaveSystem.TempPath));
            Assert.AreEqual(2, SaveSystem.Load().credits);
            Assert.AreEqual(1, SaveSystem.FromJson(File.ReadAllText(SaveSystem.BackupPath)).credits);
        }

        [Test]
        public void ACorruptSaveFallsBackToTheBackup()
        {
            SaveSystem.Save(State(1));
            SaveSystem.Save(State(2));
            File.WriteAllText(SaveSystem.FilePath, "{\"credits\": 2, \"agentCo");   // power cut mid-write
            Assert.AreEqual(1, SaveSystem.Load().credits);
            File.WriteAllText(SaveSystem.FilePath, "");
            Assert.AreEqual(1, SaveSystem.Load().credits);
        }

        [Test]
        public void AMissingSaveRecoversTheNewestOfTempAndBackup()
        {
            SaveSystem.Save(State(1));
            SaveSystem.Save(State(2));
            File.Delete(SaveSystem.FilePath);
            Assert.AreEqual(1, SaveSystem.Load().credits, "only the backup is left");

            // crashed after writing the temp file but before swapping it in
            var newer = State(3);
            newer.lastSaveUnix = long.MaxValue / 2;
            File.WriteAllText(SaveSystem.TempPath, SaveSystem.ToJson(newer));
            Assert.AreEqual(3, SaveSystem.Load().credits, "the interrupted write is newer than the backup");

            File.WriteAllText(SaveSystem.TempPath, "{\"cred");
            Assert.AreEqual(1, SaveSystem.Load().credits, "a half-written temp file is ignored");
        }

        [Test]
        public void DeleteRemovesEveryCopy()
        {
            SaveSystem.Save(State(1));
            SaveSystem.Save(State(2));
            File.WriteAllText(SaveSystem.TempPath, "x");
            SaveSystem.Delete();
            Assert.IsNull(SaveSystem.Load());
            Assert.IsFalse(File.Exists(SaveSystem.BackupPath) || File.Exists(SaveSystem.TempPath));
        }

        // ------------------------------------------------------------------ save files brought in by the player
        [Test]
        public void ASaveFileRoundTrips()
        {
            var state = State(1234.5);
            state.day = 12;
            state.reorgs = 2;
            state.agentCounts[3] = 7;
            var loaded = SaveSystem.Validate(SaveSystem.ToJson(state), out string error);
            Assert.IsNotNull(loaded, error);
            Assert.IsNull(error);
            Assert.AreEqual(1234.5, loaded.credits);
            Assert.AreEqual(12, loaded.day);
            Assert.AreEqual(2, loaded.reorgs);
            Assert.AreEqual(7, loaded.agentCounts[3]);
        }

        [TestCase("")]
        [TestCase("   \n")]
        [TestCase("not json at all")]
        [TestCase("{")]
        [TestCase("[1, 2, 3]")]
        [TestCase("{}")]
        [TestCase("{\"name\": \"some other game\", \"level\": 4}")]
        public void JunkIsRejectedWithAReason(string text)
        {
            Assert.IsNull(SaveSystem.Validate(text, out string error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void ACareerThatNeverStartedIsRejected()
        {
            var fresh = new GameState(); // still on the intro cards
            Assert.IsNull(SaveSystem.Validate(SaveSystem.ToJson(fresh), out string error));
            StringAssert.Contains("career", error);
        }

        [Test]
        public void AHugeFileIsRejected()
        {
            string huge = "{\"agentCounts\":[" + new string('1', SaveSystem.MaxFileChars) + "]}";
            Assert.IsNull(SaveSystem.Validate(huge, out string error));
            StringAssert.Contains("big", error);
        }
    }
}
