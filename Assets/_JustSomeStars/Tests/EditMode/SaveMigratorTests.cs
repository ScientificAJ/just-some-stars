using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class SaveMigratorTests
    {
        [Test]
        public void CurrentV1Document_IsAnExactIdentityWithoutInventedLegacyFormat()
        {
            const string current = "{\"schemaVersion\":1,\"fixture\":\"unchanged\"}";
            var migrator = SaveMigrator.CreateCurrent();

            var migrated = migrator.TryMigrate(current, out var result);

            Assert.That(migrated, Is.True);
            Assert.That(result, Is.EqualTo(current));
            Assert.That(migrator.TargetVersion, Is.EqualTo(1));
            Assert.That(migrator.RegisteredStepCount, Is.EqualTo(0));
        }

        [TestCase("not-json")]
        [TestCase("{}")]
        [TestCase("{\"schemaVersion\":0}")]
        [TestCase("{\"schemaVersion\":2}")]
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
