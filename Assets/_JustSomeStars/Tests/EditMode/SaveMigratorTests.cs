using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class SaveMigratorTests
    {
        [Test]
        public void ProductionRegistry_MigratesRealV1SaveToExplicitEmptyMissionState()
        {
            var current = GameSave.CreateNew("save.legacy", 10);
            current.DiscoveryIds = new[] { "phenomenon.legacy" };
            var v5 = JsonUtility.ToJson(current);
            var v4 = v5.Replace("\"schemaVersion\":5", "\"schemaVersion\":4");
            var v3 = Regex.Replace(
                v4.Replace("\"schemaVersion\":4", "\"schemaVersion\":3"),
                ",\"chapterOne\":\\{[^}]*\\}",
                string.Empty);
            var v2 = v3.Replace("\"schemaVersion\":3", "\"schemaVersion\":2");
            var v1 = Regex.Replace(
                v2.Replace("\"schemaVersion\":2", "\"schemaVersion\":1"),
                ",\"mission\":\\{[^}]*\\}",
                string.Empty);
            var migrator = SaveMigrator.CreateCurrent();

            var migrated = migrator.TryMigrate(v1, out var result);

            Assert.That(migrated, Is.True);
            Assert.That(result, Does.Contain("\"schemaVersion\": 5"));
            Assert.That(result, Does.Contain("\"mission\""));
            Assert.That(result, Does.Contain("\"chapterOne\""));
            Assert.That(result, Does.Contain("phenomenon.legacy"));
            Assert.That(migrator.TargetVersion, Is.EqualTo(5));
            Assert.That(migrator.RegisteredStepCount, Is.EqualTo(4));
        }

        [TestCase("not-json")]
        [TestCase("{}")]
        [TestCase("{\"schemaVersion\":0}")]
        [TestCase("{\"schemaVersion\":6}")]
        public void ProductionRegistry_RejectsMalformedUnsupportedAndFutureDocuments(
            string document)
        {
            var migrator = SaveMigrator.CreateCurrent();

            Assert.That(migrator.TryMigrate(document, out var result), Is.False);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FutureRegistry_RunsContiguousStepsInOrderWithoutMutatingFixture()
        {
            const string original = "{\"schemaVersion\":1,\"history\":\"start\"}";
            var order = new List<string>();
            var migrator = new SaveMigrator(
                targetVersion: 3,
                new ISaveMigration[]
                {
                    new FixtureMigration(1, 2, order, "one"),
                    new FixtureMigration(2, 3, order, "two"),
                });

            var migrated = migrator.TryMigrate(original, out var result);

            Assert.That(migrated, Is.True);
            Assert.That(order, Is.EqualTo(new[] { "one", "two" }));
            Assert.That(original, Is.EqualTo("{\"schemaVersion\":1,\"history\":\"start\"}"));
            Assert.That(result, Does.Contain("\"schemaVersion\":3"));
            Assert.That(result, Does.Contain("start,one,two"));
        }

        [Test]
        public void Registry_RejectsGapDuplicateOrBackwardStepBeforeUse()
        {
            var order = new List<string>();

            Assert.Throws<ArgumentException>(() => new SaveMigrator(
                3,
                new ISaveMigration[]
                {
                    new FixtureMigration(1, 2, order, "one"),
                    new FixtureMigration(1, 3, order, "duplicate"),
                }));
            Assert.Throws<ArgumentException>(() => new SaveMigrator(
                3,
                new ISaveMigration[]
                {
                    new FixtureMigration(1, 2, order, "one"),
                }));
            Assert.Throws<ArgumentException>(() => new SaveMigrator(
                1,
                new ISaveMigration[]
                {
                    new FixtureMigration(2, 1, order, "backward"),
                }));
        }

        [Test]
        public void StepThatClaimsWrongOutputVersion_FailsClosed()
        {
            var migrator = new SaveMigrator(
                2,
                new[] { new WrongVersionMigration() });

            var migrated = migrator.TryMigrate(
                "{\"schemaVersion\":1}",
                out var result);

            Assert.That(migrated, Is.False);
            Assert.That(result, Is.Null);
        }

        private sealed class FixtureMigration : ISaveMigration
        {
            private readonly IList<string> m_Order;
            private readonly string m_Marker;

            public FixtureMigration(
                int fromVersion,
                int toVersion,
                IList<string> order,
                string marker)
            {
                FromVersion = fromVersion;
                ToVersion = toVersion;
                m_Order = order;
                m_Marker = marker;
            }

            public int FromVersion { get; }

            public int ToVersion { get; }

            public string Migrate(string document)
            {
                m_Order.Add(m_Marker);
                var history = document.Contains("start,one", StringComparison.Ordinal)
                    ? "start,one,two"
                    : "start,one";
                return $"{{\"schemaVersion\":{ToVersion},\"history\":\"{history}\"}}";
            }
        }

        private sealed class WrongVersionMigration : ISaveMigration
        {
            public int FromVersion => 1;

            public int ToVersion => 2;

            public string Migrate(string document)
            {
                return "{\"schemaVersion\":1}";
            }
        }
    }
}
