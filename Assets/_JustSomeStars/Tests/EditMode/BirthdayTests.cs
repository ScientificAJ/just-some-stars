using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.UI.Account;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class BirthdayTests
    {
        private static readonly DateTimeOffset GiftDay =
            new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void LeapDay_UsesFebruary28ForGiftButMarch1ForPrivacyAge()
        {
            var birthday = BirthdayDate.Create(
                29,
                2,
                2012,
                new DateTimeOffset(2025, 2, 28, 12, 0, 0, TimeSpan.Zero));

            Assert.That(
                BirthdayPolicy.AgeOn(
                    birthday,
                    new DateTimeOffset(2025, 2, 28, 23, 59, 59, TimeSpan.Zero)),
                Is.EqualTo(12),
                "Privacy age must not advance a leap-day birthday early.");
            Assert.That(
                BirthdayPolicy.AgeBandOn(
                    birthday,
                    new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero)),
                Is.EqualTo(BirthdayAgeBand.Teen));

            var window = BirthdayPolicy.GiftWindowOn(
                birthday,
                new DateTimeOffset(2025, 2, 28, 0, 0, 0, TimeSpan.Zero));
            Assert.That(window.IsActive, Is.True);
            Assert.That(window.GiftYear, Is.EqualTo(2025));
            Assert.That(window.StartUtc, Is.EqualTo(new DateTime(2025, 2, 28)));
            Assert.That(window.EndExclusiveUtc, Is.EqualTo(new DateTime(2025, 3, 30)));
        }

        [Test]
        public void GiftWindow_IsExactlyThirtyUtcDatesAndIgnoresLocalOffset()
        {
            var birthday = BirthdayDate.Create(4, 7, 2013, GiftDay);

            Assert.That(
                BirthdayPolicy.GiftWindowOn(
                    birthday,
                    new DateTimeOffset(2026, 7, 3, 23, 59, 59, TimeSpan.Zero))
                    .IsActive,
                Is.False);
            Assert.That(
                BirthdayPolicy.GiftWindowOn(
                    birthday,
                    new DateTimeOffset(2026, 7, 4, 0, 0, 0, TimeSpan.Zero))
                    .IsActive,
                Is.True);
            Assert.That(
                BirthdayPolicy.GiftWindowOn(
                    birthday,
                    new DateTimeOffset(2026, 8, 2, 23, 59, 59, TimeSpan.Zero))
                    .IsActive,
                Is.True);
            Assert.That(
                BirthdayPolicy.GiftWindowOn(
                    birthday,
                    new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero))
                    .IsActive,
                Is.False);

            var localJulyFourthButUtcJulyThird =
                new DateTimeOffset(2026, 7, 4, 1, 0, 0, TimeSpan.FromHours(14));
            Assert.That(
                BirthdayPolicy.GiftWindowOn(birthday, localJulyFourthButUtcJulyThird)
                    .IsActive,
                Is.False,
                "Eligibility must use the trusted UTC date, not the caller's offset.");
            var yearCrossing = BirthdayPolicy.GiftWindowOn(
                BirthdayDate.Create(20, 12, 2013, GiftDay),
                new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero));
            Assert.That(yearCrossing.StartUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(yearCrossing.EndExclusiveUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(yearCrossing.StartUtc, Is.EqualTo(new DateTime(
                2026, 12, 20, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(yearCrossing.EndExclusiveUtc, Is.EqualTo(new DateTime(
                2027, 1, 19, 0, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void BirthdayChange_AllowsOneCorrectionThenRequiresGrownUp()
        {
            var first = BirthdayPolicy.ApplyDate(
                new BirthdayState(),
                BirthdayDate.Create(4, 7, 2013, GiftDay),
                grownUpConfirmed: false);
            Assert.That(first.CorrectionCount, Is.Zero);

            first.LastBirthdayGiftYear = 2025;
            var corrected = BirthdayPolicy.ApplyDate(
                first,
                BirthdayDate.Create(5, 7, 2013, GiftDay),
                grownUpConfirmed: false);
            Assert.That(corrected.CorrectionCount, Is.EqualTo(1));
            Assert.That(corrected.LastBirthdayGiftYear, Is.EqualTo(2025));

            Assert.Throws<BirthdayCorrectionRequiresGrownUpException>(() =>
                BirthdayPolicy.ApplyDate(
                    corrected,
                    BirthdayDate.Create(6, 7, 2013, GiftDay),
                    grownUpConfirmed: false));

            var confirmed = BirthdayPolicy.ApplyDate(
                corrected,
                BirthdayDate.Create(6, 7, 2013, GiftDay),
                grownUpConfirmed: true);
            Assert.That(confirmed.CorrectionCount, Is.EqualTo(2));
            Assert.That(confirmed.LastBirthdayGiftYear, Is.EqualTo(2025));
        }

        [Test]
        public async Task GuestClaim_GrantsOnceLocallyWithoutPurchasePrompt()
        {
            var save = CreateSaveWithBirthday();
            var saves = new FakeSaveService(save);
            var accounts = FakeAccountService.Offline();
            var gateway = new FakeBirthdayGiftGateway();
            var service = CreateService(saves, accounts, gateway);

            var first = await service.ClaimAsync(CancellationToken.None);
            var second = await service.ClaimAsync(CancellationToken.None);

            Assert.That(first.Status, Is.EqualTo(BirthdayGiftClaimStatus.Granted));
            Assert.That(first.GiftYear, Is.EqualTo(2026));
            Assert.That(first.CosmeticId, Is.EqualTo("birthday.ori-starlight.2026"));
            Assert.That(first.Presentation.AllowsPurchasePrompt, Is.False);
            Assert.That(first.Presentation.OriDeliveryCue,
                Is.EqualTo("birthday.ori.delivery.2026"));
            Assert.That(second.Status,
                Is.EqualTo(BirthdayGiftClaimStatus.AlreadyClaimed));
            Assert.That(saves.Current.Birthday.LastBirthdayGiftYear,
                Is.EqualTo(2026));
            Assert.That(saves.Current.EarnedCosmeticIds,
                Is.EqualTo(new[] { "birthday.ori-starlight.2026" }));
            Assert.That(gateway.CallCount, Is.Zero);
        }

        [Test]
        public async Task ConcurrentGuestClaim_ReturnsOneGrantAndOneAlreadyClaimed()
        {
            var saves = new ObservedSaveService(CreateSaveWithBirthday());
            var service = CreateService(
                saves,
                FakeAccountService.Offline(),
                new FakeBirthdayGiftGateway());

            var claims = await Task.WhenAll(
                service.ClaimAsync(CancellationToken.None).AsTask(),
                service.ClaimAsync(CancellationToken.None).AsTask());

            Assert.That(
                claims.Select(result => result.Status),
                Is.EquivalentTo(new[]
                {
                    BirthdayGiftClaimStatus.Granted,
                    BirthdayGiftClaimStatus.AlreadyClaimed,
                }));
            Assert.That(saves.Current.EarnedCosmeticIds,
                Is.EqualTo(new[] { "birthday.ori-starlight.2026" }));
        }

        [Test]
        public async Task ClaimAndBirthdayCorrection_SerializeOneLocalSaveMutationAtATime()
        {
            var saves = new ObservedSaveService(CreateSaveWithBirthday());
            var service = CreateService(
                saves,
                FakeAccountService.Offline(),
                new FakeBirthdayGiftGateway());

            await Task.WhenAll(
                service.ClaimAsync(CancellationToken.None).AsTask(),
                service.UpdateBirthdayAsync(
                    5,
                    7,
                    2013,
                    grownUpConfirmed: false,
                    CancellationToken.None).AsTask());

            Assert.That(saves.MaximumConcurrentLoads, Is.EqualTo(1));
            Assert.That(saves.Current.Birthday.CorrectionCount, Is.EqualTo(1));
            Assert.That(saves.Current.Birthday.LastBirthdayGiftYear, Is.EqualTo(2026));
            Assert.That(saves.Current.EarnedCosmeticIds,
                Does.Contain("birthday.ori-starlight.2026"));
        }

        [Test]
        public async Task LinkedClaim_UsesAuthenticatedGatewayWithoutSendingBirthday()
        {
            var saves = new FakeSaveService(CreateSaveWithBirthday());
            var accounts = FakeAccountService.Linked("firebase.uid.22");
            var gateway = new FakeBirthdayGiftGateway
            {
                Next = BirthdayGiftGatewayResult.Granted(
                    2026,
                    "birthday.ori-starlight.2026"),
            };
            var service = CreateService(saves, accounts, gateway);

            var result = await service.ClaimAsync(CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(BirthdayGiftClaimStatus.Granted));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
            Assert.That(saves.Current.Birthday.LastBirthdayGiftYear,
                Is.EqualTo(2026));
            Assert.That(saves.Current.EarnedCosmeticIds,
                Does.Contain("birthday.ori-starlight.2026"));
        }

        [Test]
        public async Task SetupController_UsesNeutralCopyAndGatesOnlyLaterCorrections()
        {
            var state = CreateSaveWithBirthday();
            state.Birthday.CorrectionCount = 1;
            var saves = new FakeSaveService(state);
            var service = CreateService(
                saves,
                FakeAccountService.Offline(),
                new FakeBirthdayGiftGateway());
            var prompts = new List<GrownUpPrompt>();
            var grownUp = new GrownUpConfirmationController(
                (prompt, _) =>
                {
                    prompts.Add(prompt);
                    return new ValueTask<bool>(true);
                });
            var controller = new BirthdaySetupController(service, grownUp);

            var result = await controller.SubmitAsync(
                6,
                7,
                2013,
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(BirthdayUpdateStatus.Saved));
            Assert.That(prompts, Has.Count.EqualTo(1));
            Assert.That(prompts[0].Action,
                Is.EqualTo(GrownUpAction.BirthdayCorrection));
            Assert.That(BirthdaySetupController.Explanation,
                Does.Not.Contain("13").And.Not.Contain("18"));
            Assert.That(BirthdaySetupController.Explanation.ToLowerInvariant(),
                Does.Not.Contain("unlock").And.Not.Contain("less restricted"));
        }

        [Test]
        public void GrownUpPolicy_IsStrictForUnknownAndChildWithoutAgeHints()
        {
            foreach (GrownUpAction action in Enum.GetValues(typeof(GrownUpAction)))
            {
                Assert.That(
                    BirthdayPolicy.RequirementFor(BirthdayAgeBand.Unknown, action),
                    Is.EqualTo(GrownUpRequirement.AskAGrownUp));
                Assert.That(
                    BirthdayPolicy.RequirementFor(BirthdayAgeBand.Child, action),
                    Is.EqualTo(GrownUpRequirement.AskAGrownUp));
            }

            Assert.That(
                BirthdayPolicy.RequirementFor(
                    BirthdayAgeBand.Adult,
                    GrownUpAction.Purchase),
                Is.EqualTo(GrownUpRequirement.ConfirmAction));
            Assert.That(
                BirthdayPolicy.RequirementFor(
                    BirthdayAgeBand.Adult,
                    GrownUpAction.CloudLink),
                Is.EqualTo(GrownUpRequirement.None));
            Assert.That(GrownUpConfirmationController.AskCopy,
                Does.Not.Contain("child").And.Not.Contain("teen").And.Not.Contain("adult"));
        }

        [Test]
        public void SchemaV2_MigratesWithUnusedCorrectionAllowance()
        {
            var serializer = new JsonSaveSerializer(SaveMigrator.CreateCurrent());
            var legacy = JsonUtility.ToJson(CreateSaveWithBirthday(), prettyPrint: true)
                .Replace("\"schemaVersion\": 5", "\"schemaVersion\": 2")
                .Replace("    \"correctionCount\": 0,\n", string.Empty);

            Assert.That(serializer.TryDeserialize(legacy, out var migrated), Is.True);
            Assert.That(migrated.SchemaVersion, Is.EqualTo(5));
            Assert.That(migrated.Birthday.CorrectionCount, Is.Zero);
        }

        [Test]
        public void BirthdayGiftCatalog_IsPrivateCelebrationContentWithoutPurchasePrompt()
        {
            var path = Path.Combine(
                Application.dataPath,
                "_JustSomeStars/Content/Cosmetics/birthday/birthday-gifts.json");
            var offers = BirthdayGiftCatalog.Parse(File.ReadAllText(path));

            Assert.That(offers.Count, Is.EqualTo(1));
            Assert.That(offers[0].GiftYear, Is.EqualTo(2026));
            Assert.That(offers[0].CosmeticId,
                Is.EqualTo("birthday.ori-starlight.2026"));
            Assert.That(offers[0].OriDeliveryCue,
                Is.EqualTo("birthday.ori.delivery.2026"));
            Assert.That(offers[0].DecorationSetId,
                Is.EqualTo("birthday.decorations.homemade.2026"));
            Assert.That(
                new BirthdayGiftPresentation(offers[0]).AllowsPurchasePrompt,
                Is.False);
        }

        private static BirthdayGiftService CreateService(
            ISaveService saves,
            FakeAccountService accounts,
            FakeBirthdayGiftGateway gateway)
        {
            return new BirthdayGiftService(
                saves,
                accounts,
                gateway,
                () => GiftDay,
                new[]
                {
                    new BirthdayGiftOffer(
                        2026,
                        "birthday.ori-starlight.2026",
                        "birthday.celebration.title.2026",
                        "birthday.ori.delivery.2026",
                        "birthday.decorations.homemade.2026"),
                });
        }

        private static GameSave CreateSaveWithBirthday()
        {
            var save = GameSave.CreateNew("save.birthday.tests", 100);
            save.Birthday = new BirthdayState
            {
                HasValue = true,
                Day = 4,
                Month = 7,
                Year = 2013,
                CorrectionCount = 0,
                LastBirthdayGiftYear = 0,
            };
            return save;
        }

        private sealed class FakeBirthdayGiftGateway : IBirthdayGiftGateway
        {
            public int CallCount { get; private set; }

            public BirthdayGiftGatewayResult Next { get; set; } =
                BirthdayGiftGatewayResult.Unavailable();

            public ValueTask<BirthdayGiftGatewayResult> ClaimAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return new ValueTask<BirthdayGiftGatewayResult>(Next);
            }
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(GameSave initial)
            {
                Current = initial.Copy();
            }

            public GameSave Current { get; private set; }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;

            public ValueTask<LoadSaveResult> LoadAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<LoadSaveResult>(
                    new LoadSaveResult(
                        LoadSaveStatus.LoadedPrimary,
                        Current,
                        "Loaded."));
            }

            public ValueTask SaveCheckpointAsync(
                GameSave save,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                save.ThrowIfInvalid(nameof(save));
                Current = save.Copy();
                return default;
            }

            public ValueTask<LoadSaveResult> RecoverAsync(
                CancellationToken cancellationToken) => LoadAsync(cancellationToken);

            public GameSave Merge(GameSave local, GameSave cloud) =>
                SaveMerge.Combine(local, cloud);
        }

        private sealed class ObservedSaveService : ISaveService
        {
            private readonly object m_Sync = new object();
            private int m_ActiveLoads;

            public ObservedSaveService(GameSave initial)
            {
                Current = initial.Copy();
            }

            public GameSave Current { get; private set; }

            public int MaximumConcurrentLoads { get; private set; }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;

            public async ValueTask<LoadSaveResult> LoadAsync(
                CancellationToken cancellationToken)
            {
                var active = Interlocked.Increment(ref m_ActiveLoads);
                lock (m_Sync)
                {
                    MaximumConcurrentLoads = Math.Max(MaximumConcurrentLoads, active);
                }

                try
                {
                    await Task.Delay(25, cancellationToken);
                    lock (m_Sync)
                    {
                        return new LoadSaveResult(
                            LoadSaveStatus.LoadedPrimary,
                            Current.Copy(),
                            "Loaded.");
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref m_ActiveLoads);
                }
            }

            public ValueTask SaveCheckpointAsync(
                GameSave save,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                save.ThrowIfInvalid(nameof(save));
                lock (m_Sync)
                {
                    Current = save.Copy();
                }

                return default;
            }

            public ValueTask<LoadSaveResult> RecoverAsync(
                CancellationToken cancellationToken) => LoadAsync(cancellationToken);

            public GameSave Merge(GameSave local, GameSave cloud) =>
                SaveMerge.Combine(local, cloud);
        }

        private sealed class FakeAccountService : IAccountService
        {
            private FakeAccountService(AccountState current)
            {
                Current = current;
            }

            public AccountState Current { get; }

            public event Action<AccountState> StateChanged
            {
                add { }
                remove { }
            }

            public static FakeAccountService Offline() =>
                new FakeAccountService(new AccountState(
                    AccountConnection.OfflineGuest,
                    AccountCapability.Offline,
                    AccountSyncState.LocalOnly,
                    AccountOperation.None,
                    "guest.task22",
                    string.Empty,
                    "Offline."));

            public static FakeAccountService Linked(string uid) =>
                new FakeAccountService(new AccountState(
                    AccountConnection.Linked,
                    AccountCapability.Available,
                    AccountSyncState.Synced,
                    AccountOperation.None,
                    "guest.task22",
                    uid,
                    "Linked."));

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;

            public ValueTask<AccountLinkResult> LinkGoogleAsync(
                CancellationToken cancellationToken) => default;

            public ValueTask<AccountLinkResult> ResolveConflictAsync(
                AccountConflictChoice choice,
                CancellationToken cancellationToken) => default;

            public ValueTask<CloudSyncResult> SyncAsync(
                CancellationToken cancellationToken) => default;

            public ValueTask<AccountExportResult> ExportDataAsync(
                CancellationToken cancellationToken) => default;

            public ValueTask<AccountUnlinkResult> UnlinkGoogleAsync(
                CancellationToken cancellationToken) => default;

            public ValueTask SignOutAsync(CancellationToken cancellationToken) =>
                default;

            public ValueTask DeleteAccountAsync(
                CancellationToken cancellationToken) => default;
        }
    }
}
