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
    }
}
