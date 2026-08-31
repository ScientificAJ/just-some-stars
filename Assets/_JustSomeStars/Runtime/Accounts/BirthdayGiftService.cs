using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Saving;
using UnityEngine;

namespace JustSomeStars.Runtime.Accounts
{
    public enum BirthdayGiftClaimStatus
    {
        Granted = 0,
        AlreadyClaimed = 1,
        OutsideWindow = 2,
        MissingBirthday = 3,
        Unavailable = 4,
    }

    public enum BirthdayUpdateStatus
    {
        Saved = 0,
        RequiresGrownUp = 1,
        Unavailable = 2,
    }

    public sealed class BirthdayGiftPresentation
    {
        internal BirthdayGiftPresentation(BirthdayGiftOffer offer)
        {
            TitleKey = offer?.TitleKey ?? string.Empty;
            OriDeliveryCue = offer?.OriDeliveryCue ?? string.Empty;
            DecorationSetId = offer?.DecorationSetId ?? string.Empty;
        }

        public string TitleKey { get; }

        public string OriDeliveryCue { get; }

        public string DecorationSetId { get; }

        public bool AllowsPurchasePrompt => false;
    }

    public sealed class BirthdayGiftOffer
    {
        public BirthdayGiftOffer(
            int giftYear,
            string cosmeticId,
            string titleKey,
            string oriDeliveryCue,
            string decorationSetId)
        {
            if (giftYear < 1 ||
                string.IsNullOrWhiteSpace(cosmeticId) ||
                string.IsNullOrWhiteSpace(titleKey) ||
                string.IsNullOrWhiteSpace(oriDeliveryCue) ||
                string.IsNullOrWhiteSpace(decorationSetId))
            {
                throw new ArgumentException("Birthday gift offer is incomplete.");
            }

            GiftYear = giftYear;
            CosmeticId = cosmeticId;
            TitleKey = titleKey;
            OriDeliveryCue = oriDeliveryCue;
            DecorationSetId = decorationSetId;
        }

        public int GiftYear { get; }

        public string CosmeticId { get; }

        public string TitleKey { get; }

        public string OriDeliveryCue { get; }

        public string DecorationSetId { get; }
    }

    public static class BirthdayGiftCatalog
    {
        public static IReadOnlyList<BirthdayGiftOffer> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Birthday gift catalog is required.", nameof(json));
            }

            var document = JsonUtility.FromJson<CatalogDocument>(json);
            if (document == null || document.schemaVersion != 1 ||
                document.gifts == null || document.gifts.Length == 0)
            {
                throw new FormatException("Birthday gift catalog is invalid.");
            }

            var offers = document.gifts.Select(entry =>
                new BirthdayGiftOffer(
                    entry.giftYear,
                    entry.cosmeticId,
                    entry.titleKey,
                    entry.oriDeliveryCue,
                    entry.decorationSetId)).ToArray();
            if (offers.Select(offer => offer.GiftYear).Distinct().Count() != offers.Length ||
                document.allowsPurchasePrompt)
            {
                throw new FormatException(
                    "Birthday gift years must be unique and cannot enable purchase prompts.");
            }

            return offers;
        }

        [Serializable]
        private sealed class CatalogDocument
        {
            public int schemaVersion;
            public bool allowsPurchasePrompt;
            public CatalogEntry[] gifts;
        }

        [Serializable]
        private sealed class CatalogEntry
        {
            public int giftYear;
            public string cosmeticId;
            public string titleKey;
            public string oriDeliveryCue;
            public string decorationSetId;
        }
    }

    public sealed class BirthdayGiftClaimResult
    {
        internal BirthdayGiftClaimResult(
            BirthdayGiftClaimStatus status,
            int giftYear,
            string cosmeticId,
            BirthdayGiftPresentation presentation)
        {
            Status = status;
            GiftYear = giftYear;
            CosmeticId = cosmeticId ?? string.Empty;
            Presentation = presentation ??
                new BirthdayGiftPresentation(null);
        }

        public BirthdayGiftClaimStatus Status { get; }

        public int GiftYear { get; }

        public string CosmeticId { get; }

        public BirthdayGiftPresentation Presentation { get; }
    }

    public sealed class BirthdayUpdateResult
    {
        internal BirthdayUpdateResult(BirthdayUpdateStatus status)
        {
            Status = status;
        }

        public BirthdayUpdateStatus Status { get; }
    }

    public sealed class BirthdayGiftGatewayResult
    {
        private BirthdayGiftGatewayResult(
            BirthdayGiftClaimStatus status,
            int giftYear,
            string cosmeticId)
        {
            Status = status;
            GiftYear = giftYear;
            CosmeticId = cosmeticId ?? string.Empty;
        }

        public BirthdayGiftClaimStatus Status { get; }

        public int GiftYear { get; }

        public string CosmeticId { get; }

        public static BirthdayGiftGatewayResult Granted(
            int giftYear,
            string cosmeticId) =>
            new BirthdayGiftGatewayResult(
                BirthdayGiftClaimStatus.Granted,
                giftYear,
                cosmeticId);

        public static BirthdayGiftGatewayResult AlreadyClaimed(int giftYear) =>
            new BirthdayGiftGatewayResult(
                BirthdayGiftClaimStatus.AlreadyClaimed,
                giftYear,
                string.Empty);

        public static BirthdayGiftGatewayResult OutsideWindow() =>
            new BirthdayGiftGatewayResult(
                BirthdayGiftClaimStatus.OutsideWindow,
                0,
                string.Empty);

        public static BirthdayGiftGatewayResult Unavailable() =>
            new BirthdayGiftGatewayResult(
                BirthdayGiftClaimStatus.Unavailable,
                0,
                string.Empty);
    }

    public interface IBirthdayGiftGateway
    {
        ValueTask<BirthdayGiftGatewayResult> ClaimAsync(
            CancellationToken cancellationToken);
    }

    public sealed class UnavailableBirthdayGiftGateway : IBirthdayGiftGateway
    {
        public ValueTask<BirthdayGiftGatewayResult> ClaimAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<BirthdayGiftGatewayResult>(
                BirthdayGiftGatewayResult.Unavailable());
        }
    }

    public sealed class BirthdayGiftService
    {
        private readonly ISaveService m_Saves;
        private readonly IAccountService m_Accounts;
        private readonly IBirthdayGiftGateway m_Gateway;
        private readonly Func<DateTimeOffset> m_TrustedNow;
        private readonly IReadOnlyDictionary<int, BirthdayGiftOffer> m_Offers;
        private readonly SemaphoreSlim m_MutationGate = new SemaphoreSlim(1, 1);

        public BirthdayGiftService(
            ISaveService saves,
            IAccountService accounts,
            IBirthdayGiftGateway gateway,
            Func<DateTimeOffset> trustedNow,
            IEnumerable<BirthdayGiftOffer> offers)
        {
            m_Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            m_TrustedNow = trustedNow ?? throw new ArgumentNullException(nameof(trustedNow));
            m_Offers = (offers ?? throw new ArgumentNullException(nameof(offers)))
                .ToDictionary(offer => offer.GiftYear);
        }

        public async ValueTask<BirthdayGiftClaimResult> ClaimAsync(
            CancellationToken cancellationToken)
        {
            await m_MutationGate.WaitAsync(cancellationToken);
            try
            {
                var save = await LoadAsync(cancellationToken);
                if (!save.Birthday.HasValue)
                {
                    return Result(
                        BirthdayGiftClaimStatus.MissingBirthday,
                        0,
                        string.Empty);
                }

                if (IsAuthenticated(m_Accounts.Current))
                {
                    var remote = await m_Gateway.ClaimAsync(cancellationToken);
                    if (remote.Status != BirthdayGiftClaimStatus.Granted)
                    {
                        return Result(
                            remote.Status,
                            remote.GiftYear,
                            remote.CosmeticId);
                    }

                    await PersistGrantAsync(
                        save,
                        remote.GiftYear,
                        remote.CosmeticId,
                        cancellationToken);
                    return Result(remote.Status, remote.GiftYear, remote.CosmeticId);
                }

                var window = BirthdayPolicy.GiftWindowOn(
                    BirthdayDate.FromState(save.Birthday),
                    m_TrustedNow());
                if (!window.IsActive)
                {
                    return Result(
                        BirthdayGiftClaimStatus.OutsideWindow,
                        0,
                        string.Empty);
                }

                if (save.Birthday.LastBirthdayGiftYear >= window.GiftYear)
                {
                    return Result(
                        BirthdayGiftClaimStatus.AlreadyClaimed,
                        window.GiftYear,
                        string.Empty);
                }

                if (!m_Offers.TryGetValue(window.GiftYear, out var offer))
                {
                    return Result(
                        BirthdayGiftClaimStatus.Unavailable,
                        window.GiftYear,
                        string.Empty);
                }

                await PersistGrantAsync(
                    save,
                    window.GiftYear,
                    offer.CosmeticId,
                    cancellationToken);
                return Result(
                    BirthdayGiftClaimStatus.Granted,
                    window.GiftYear,
                    offer.CosmeticId);
            }
            finally
            {
                m_MutationGate.Release();
            }
        }

        public async ValueTask<BirthdayUpdateResult> UpdateBirthdayAsync(
            int day,
            int month,
            int year,
            bool grownUpConfirmed,
            CancellationToken cancellationToken)
        {
            await m_MutationGate.WaitAsync(cancellationToken);
            try
            {
                var save = await LoadAsync(cancellationToken);
                var birthday = BirthdayDate.Create(day, month, year, m_TrustedNow());
                save.Birthday = BirthdayPolicy.ApplyDate(
                    save.Birthday,
                    birthday,
                    grownUpConfirmed);
                await m_Saves.SaveCheckpointAsync(save, cancellationToken);
                return new BirthdayUpdateResult(BirthdayUpdateStatus.Saved);
            }
            finally
            {
                m_MutationGate.Release();
            }
        }

        private async ValueTask<GameSave> LoadAsync(CancellationToken cancellationToken)
        {
            var loaded = await m_Saves.LoadAsync(cancellationToken);
            if (loaded.Save == null)
            {
                throw new InvalidOperationException("Birthday data needs an available local save.");
            }

            return loaded.Save.Copy();
        }

        private async ValueTask PersistGrantAsync(
            GameSave save,
            int giftYear,
            string cosmeticId,
            CancellationToken cancellationToken)
        {
            if (giftYear < 1 || string.IsNullOrWhiteSpace(cosmeticId))
            {
                throw new InvalidOperationException("Birthday gift response is incomplete.");
            }

            save.Birthday.LastBirthdayGiftYear = Math.Max(
                save.Birthday.LastBirthdayGiftYear,
                giftYear);
            save.EarnedCosmeticIds = save.EarnedCosmeticIds
                .Concat(new[] { cosmeticId })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            await m_Saves.SaveCheckpointAsync(save, cancellationToken);
        }

        private BirthdayGiftClaimResult Result(
            BirthdayGiftClaimStatus status,
            int giftYear,
            string cosmeticId)
        {
            m_Offers.TryGetValue(giftYear, out var offer);
            return new BirthdayGiftClaimResult(
                status,
                giftYear,
                cosmeticId,
                new BirthdayGiftPresentation(offer));
        }

        private static bool IsAuthenticated(AccountState state) =>
            state != null &&
            !string.IsNullOrWhiteSpace(state.FirebaseUserId) &&
            (state.Connection == AccountConnection.Linked ||
                state.Connection == AccountConnection.Pending ||
                state.Connection == AccountConnection.Conflict);
    }
}
