using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.UI.Shop
{
    public readonly struct GrownUpChallenge
    {
        public GrownUpChallenge(
            Guid id,
            GrownUpRequirement requirement,
            GrownUpAction action,
            string prompt,
            int leftOperand,
            int rightOperand,
            DateTime expiresAtUtc)
        {
            Id = id;
            Requirement = requirement;
            Action = action;
            Prompt = prompt ?? string.Empty;
            LeftOperand = leftOperand;
            RightOperand = rightOperand;
            ExpiresAtUtc = expiresAtUtc;
        }

        public Guid Id { get; }

        public GrownUpRequirement Requirement { get; }

        public GrownUpAction Action { get; }

        public string Prompt { get; }

        public int LeftOperand { get; }

        public int RightOperand { get; }

        public DateTime ExpiresAtUtc { get; }

        public bool RequiresArithmetic =>
            Requirement == GrownUpRequirement.AskAGrownUp;
    }

    public readonly struct GrownUpChallengeResponse
    {
        public GrownUpChallengeResponse(Guid challengeId, bool confirmed, int answer)
        {
            ChallengeId = challengeId;
            Confirmed = confirmed;
            Answer = answer;
        }

        public Guid ChallengeId { get; }

        public bool Confirmed { get; }

        public int Answer { get; }
    }

    public interface IGrownUpChallengePresenter
    {
        ValueTask<GrownUpChallengeResponse> PresentAsync(
            GrownUpChallenge challenge,
            CancellationToken cancellationToken);
    }

    public interface IGrownUpPurchaseGate
    {
        ValueTask<bool> AuthorizeAsync(
            BirthdayAgeBand ageBand,
            GrownUpAction action,
            CancellationToken cancellationToken);
    }

    public sealed class GrownUpPurchaseGate : IGrownUpPurchaseGate
    {
        private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);
        private readonly IGrownUpChallengePresenter m_Presenter;
        private readonly Func<DateTime> m_UtcNow;

        public GrownUpPurchaseGate(IGrownUpChallengePresenter presenter)
            : this(presenter, () => DateTime.UtcNow)
        {
        }

        internal GrownUpPurchaseGate(
            IGrownUpChallengePresenter presenter,
            Func<DateTime> utcNow)
        {
            m_Presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            m_UtcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public async ValueTask<bool> AuthorizeAsync(
            BirthdayAgeBand ageBand,
            GrownUpAction action,
            CancellationToken cancellationToken)
        {
            var requirement = BirthdayPolicy.RequirementFor(ageBand, action);
            if (requirement == GrownUpRequirement.None)
            {
                return true;
            }

            var left = requirement == GrownUpRequirement.AskAGrownUp
                ? RandomNumberGenerator.GetInt32(7, 20)
                : 0;
            var right = requirement == GrownUpRequirement.AskAGrownUp
                ? RandomNumberGenerator.GetInt32(7, 20)
                : 0;
            var createdAt = m_UtcNow();
            if (createdAt.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "The grown-up challenge clock must return UTC.");
            }

            var challenge = new GrownUpChallenge(
                Guid.NewGuid(),
                requirement,
                action,
                requirement == GrownUpRequirement.AskAGrownUp
                    ? $"Please ask a grown-up: what is {left} + {right}?"
                    : "Confirm this clearly priced optional purchase action.",
                left,
                right,
                createdAt + ChallengeLifetime);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(ChallengeLifetime);
            var response = await m_Presenter.PresentAsync(
                challenge,
                timeout.Token);
            var completedAt = m_UtcNow();
            return response.ChallengeId == challenge.Id &&
                response.Confirmed &&
                completedAt <= challenge.ExpiresAtUtc &&
                (!challenge.RequiresArithmetic ||
                    response.Answer == challenge.LeftOperand + challenge.RightOperand);
        }
    }

    public sealed class ShopController : IDisposable
    {
        public const string FreeStoryCopy =
            "Chapter One is complete without a purchase. Optional items are cosmetic only.";
        public const string RestoreCopy =
            "Restore checks the store for prior non-consumable purchases. " +
            "Restoring can move ownership to this game profile.";

        private readonly IStoreService m_Store;
        private readonly IGrownUpPurchaseGate m_GrownUpGate;
        private readonly SemaphoreSlim m_Operation = new SemaphoreSlim(1, 1);
        private CancellationTokenSource m_SurfaceLifetime =
            new CancellationTokenSource();
        private long m_Generation;
        private bool m_IsOpen;
        private string m_StatusMessage = string.Empty;

        public ShopController(
            IStoreService store,
            IGrownUpPurchaseGate grownUpGate)
        {
            m_Store = store ?? throw new ArgumentNullException(nameof(store));
            m_GrownUpGate = grownUpGate ??
                throw new ArgumentNullException(nameof(grownUpGate));
            m_Store.StateChanged += HandleStoreChanged;
        }

        public bool IsOpen => m_IsOpen;

        public IReadOnlyList<StoreProduct> Products => m_Store.Products;

        public EntitlementSnapshot Entitlements => m_Store.CurrentEntitlements;

        public StoreAvailability Availability => m_Store.Availability;

        public string StatusMessage => string.IsNullOrEmpty(m_StatusMessage)
            ? m_Store.StatusMessage
            : m_StatusMessage;

        public event Action StateChanged;

        public async ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            Close();
            m_SurfaceLifetime.Dispose();
            m_SurfaceLifetime = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            m_IsOpen = true;
            m_StatusMessage = FreeStoryCopy;
            NotifyChanged();
            await m_Store.GetProductsAsync(m_SurfaceLifetime.Token);
        }

        public async ValueTask<PurchaseResult> PurchaseAsync(
            ContentId productId,
            BirthdayAgeBand ageBand,
            CancellationToken cancellationToken)
        {
            if (!m_IsOpen || !productId.IsValid)
            {
                return new PurchaseResult(
                    PurchaseStatus.Unavailable,
                    m_Store.CurrentEntitlements,
                    "Open the optional shop and choose an available item first.");
            }

            var generation = Interlocked.Read(ref m_Generation);
            var storePurchaseStarted = false;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                m_SurfaceLifetime.Token);
            await m_Operation.WaitAsync(linked.Token);
            try
            {
                if (!IsCurrent(generation, productId))
                {
                    return Disarmed();
                }

                var authorized = await m_GrownUpGate.AuthorizeAsync(
                    ageBand,
                    GrownUpAction.Purchase,
                    linked.Token);
                if (!authorized || !IsCurrent(generation, productId))
                {
                    m_StatusMessage =
                        "Purchase confirmation closed before the store opened.";
                    NotifyChanged();
                    return new PurchaseResult(
                        PurchaseStatus.Cancelled,
                        m_Store.CurrentEntitlements,
                        m_StatusMessage);
                }

                storePurchaseStarted = true;
                var result = await m_Store.PurchaseAsync(productId, linked.Token);
                if (!IsCurrent(generation, productId))
                {
                    return Disarmed();
                }

                m_StatusMessage = result.Message;
                NotifyChanged();
                return result;
            }
            catch (OperationCanceledException)
            {
                m_StatusMessage = storePurchaseStarted
                    ? "Store confirmation closed. Ownership will be checked " +
                      "when the game resumes."
                    : "Purchase confirmation closed before the store opened.";
                NotifyChanged();
                throw;
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public async ValueTask<EntitlementSnapshot> RestoreAsync(
            BirthdayAgeBand ageBand,
            CancellationToken cancellationToken)
        {
            if (!m_IsOpen)
            {
                return m_Store.CurrentEntitlements;
            }

            var generation = Interlocked.Read(ref m_Generation);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                m_SurfaceLifetime.Token);
            await m_Operation.WaitAsync(linked.Token);
            try
            {
                if (!IsCurrent(generation))
                {
                    return m_Store.CurrentEntitlements;
                }

                var authorized = await m_GrownUpGate.AuthorizeAsync(
                    ageBand,
                    GrownUpAction.Purchase,
                    linked.Token);
                if (!authorized || !IsCurrent(generation))
                {
                    m_StatusMessage = "Restore confirmation closed.";
                    NotifyChanged();
                    return m_Store.CurrentEntitlements;
                }

                var result = await m_Store.RestoreAsync(linked.Token);
                if (IsCurrent(generation))
                {
                    m_StatusMessage = m_Store.StatusMessage;
                    NotifyChanged();
                }

                return result;
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public void NotifyBackgrounded() => Close();

        public ValueTask NotifyResumedAsync(CancellationToken cancellationToken) =>
            m_Store.ResumeAsync(cancellationToken);

        public void Close()
        {
            Interlocked.Increment(ref m_Generation);
            m_IsOpen = false;
            m_StatusMessage = string.Empty;
            if (!m_SurfaceLifetime.IsCancellationRequested)
            {
                m_SurfaceLifetime.Cancel();
            }

            NotifyChanged();
        }

        public void Dispose()
        {
            m_Store.StateChanged -= HandleStoreChanged;
            Close();
            m_SurfaceLifetime.Dispose();
            m_Operation.Dispose();
        }

        private bool IsCurrent(long generation) =>
            m_IsOpen && generation == Interlocked.Read(ref m_Generation);

        private bool IsCurrent(long generation, ContentId productId)
        {
            if (!IsCurrent(generation))
            {
                return false;
            }

            foreach (var product in m_Store.Products)
            {
                if (product.Id == productId)
                {
                    return true;
                }
            }

            return false;
        }

        private PurchaseResult Disarmed() => new PurchaseResult(
            PurchaseStatus.Cancelled,
            m_Store.CurrentEntitlements,
            "The shop closed. Store ownership will reconcile when the game resumes.");

        private void HandleStoreChanged() => NotifyChanged();

        private void NotifyChanged() => StateChanged?.Invoke();
    }
}
