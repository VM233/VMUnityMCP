using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    public sealed class MCPPersistenceFileTests
    {
        [Test]
        public void WriteAllText_WaitsForExclusiveTargetLeaseAndPublishesCompleteSnapshot()
        {
            string directory = CreateTestDirectory();
            string path = Path.Combine(directory, "state.json");
            MCPPersistenceFile.WriteAllText(path, "{\"generation\":1}");

            try
            {
                Task writer;
                var started = new ManualResetEventSlim();
                using (var lease = new FileStream(path, FileMode.Open, FileAccess.ReadWrite,
                           FileShare.None))
                {
                    writer = Task.Run(() =>
                    {
                        started.Set();
                        MCPPersistenceFile.WriteAllText(path, "{\"generation\":2}");
                    });
                    Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);
                    Thread.Sleep(80);
                    Assert.That(writer.IsCompleted, Is.False,
                        "The exclusive lease must prevent target publication until it is released.");
                }

                Assert.That(writer.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(MCPPersistenceFile.ReadAllText(path),
                    Is.EqualTo("{\"generation\":2}"));
                AssertNoPrivateSnapshots(directory);
            }
            finally
            {
                DeleteTestDirectory(directory);
            }
        }

        [Test]
        public void ConcurrentReadersAndWriters_OnlyAdoptCompletePublishedSnapshots()
        {
            string directory = CreateTestDirectory();
            string path = Path.Combine(directory, "state.json");
            var expected = new HashSet<string>(StringComparer.Ordinal);
            for (int writer = 0; writer < 6; writer++)
            {
                for (int iteration = 0; iteration < 16; iteration++)
                    expected.Add(BuildPayload(writer, iteration));
            }
            string initial = BuildPayload(-1, -1);
            expected.Add(initial);
            MCPPersistenceFile.WriteAllText(path, initial);

            var failures = new ConcurrentQueue<Exception>();
            var gate = new ManualResetEventSlim();
            var tasks = new List<Task>();
            try
            {
                for (int writer = 0; writer < 6; writer++)
                {
                    int writerId = writer;
                    tasks.Add(Task.Run(() =>
                    {
                        gate.Wait();
                        try
                        {
                            for (int iteration = 0; iteration < 16; iteration++)
                                MCPPersistenceFile.WriteAllText(path,
                                    BuildPayload(writerId, iteration));
                        }
                        catch (Exception exception)
                        {
                            failures.Enqueue(exception);
                        }
                    }));
                }

                for (int reader = 0; reader < 6; reader++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        gate.Wait();
                        try
                        {
                            for (int iteration = 0; iteration < 96; iteration++)
                            {
                                string adopted = MCPPersistenceFile.ReadAllText(path);
                                if (!expected.Contains(adopted))
                                {
                                    throw new InvalidDataException(
                                        "A reader adopted a partial or foreign snapshot: " + adopted);
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            failures.Enqueue(exception);
                        }
                    }));
                }

                gate.Set();
                Assert.That(Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(15)), Is.True);
                Assert.That(failures, Is.Empty);
                Assert.That(expected, Does.Contain(MCPPersistenceFile.ReadAllText(path)));
                AssertNoPrivateSnapshots(directory);
            }
            finally
            {
                gate.Dispose();
                DeleteTestDirectory(directory);
            }
        }

        [Test]
        public void WriteAllText_WithBackupPublishesCurrentAndPriorCompleteSnapshots()
        {
            string directory = CreateTestDirectory();
            string path = Path.Combine(directory, "state.json");
            string backupPath = path + ".bak";
            try
            {
                MCPPersistenceFile.WriteAllText(path, "{\"generation\":1}",
                    backupPath: backupPath);
                MCPPersistenceFile.WriteAllText(path, "{\"generation\":2}",
                    backupPath: backupPath);

                Assert.That(MCPPersistenceFile.ReadAllText(path),
                    Is.EqualTo("{\"generation\":2}"));
                Assert.That(MCPPersistenceFile.ReadAllText(backupPath),
                    Is.EqualTo("{\"generation\":1}"));
                AssertNoPrivateSnapshots(directory);
            }
            finally
            {
                DeleteTestDirectory(directory);
            }
        }

        [Test]
        public void WriteAllText_WithBackupSerializesBackupAdoptionWithPublication()
        {
            string directory = CreateTestDirectory();
            string path = Path.Combine(directory, "state.json");
            string backupPath = path + ".bak";
            MCPPersistenceFile.WriteAllText(path, "{\"generation\":0}");
            MCPPersistenceFile.WriteAllText(path, "{\"generation\":1}",
                backupPath: backupPath);

            try
            {
                Task writer;
                Task<string> backupReader;
                var started = new ManualResetEventSlim();
                using (var lease = new FileStream(path, FileMode.Open, FileAccess.ReadWrite,
                           FileShare.None))
                {
                    writer = Task.Run(() =>
                    {
                        started.Set();
                        MCPPersistenceFile.WriteAllText(path, "{\"generation\":2}",
                            backupPath: backupPath);
                    });
                    Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);
                    Assert.That(SpinWait.SpinUntil(
                            () => Directory.EnumerateFiles(directory, "*.unity-mcp-*.tmp").Any(),
                            TimeSpan.FromSeconds(2)),
                        Is.True, "The writer must reach publication while holding both path locks.");
                    backupReader = Task.Run(() => MCPPersistenceFile.ReadAllText(backupPath));
                    Thread.Sleep(80);
                    Assert.That(writer.IsCompleted, Is.False);
                    Assert.That(backupReader.IsCompleted, Is.False,
                        "Backup readers must wait for the target/backup publication pair.");
                }

                Assert.That(writer.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(backupReader.Result, Is.EqualTo("{\"generation\":1}"));
                Assert.That(MCPPersistenceFile.ReadAllText(path),
                    Is.EqualTo("{\"generation\":2}"));
                AssertNoPrivateSnapshots(directory);
            }
            finally
            {
                DeleteTestDirectory(directory);
            }
        }

        [Test]
        public void WriteAllBytes_PublishesExactSnapshotWithoutPrivateFiles()
        {
            string directory = CreateTestDirectory();
            string path = Path.Combine(directory, "prefab-snapshot.bin");
            byte[] snapshot = { 0xEF, 0xBB, 0xBF, 0x00, 0x0A, 0x7F, 0xFF };
            try
            {
                MCPPersistenceFile.WriteAllBytes(path, snapshot);
                snapshot[3] = 0x42;

                CollectionAssert.AreEqual(
                    new byte[] { 0xEF, 0xBB, 0xBF, 0x00, 0x0A, 0x7F, 0xFF },
                    MCPPersistenceFile.ReadAllBytes(path));
                AssertNoPrivateSnapshots(directory);
            }
            finally
            {
                DeleteTestDirectory(directory);
            }
        }

        private static string BuildPayload(int writer, int iteration)
        {
            return "{\"writer\":" + writer + ",\"iteration\":" + iteration +
                   ",\"padding\":\"" + new string((char)('a' + Math.Max(0, writer)), 512) + "\"}";
        }

        private static string CreateTestDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "unity-mcp-persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void AssertNoPrivateSnapshots(string directory)
        {
            Assert.That(Directory.EnumerateFiles(directory, "*.unity-mcp-*.tmp").ToArray(),
                Is.Empty);
        }

        private static void DeleteTestDirectory(string directory)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
