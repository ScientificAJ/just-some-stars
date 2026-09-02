using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.UI.Shop;

namespace JustSomeStars.Runtime.UI
{
    public static class AccountLinkAuthorization
    {
        public static async ValueTask<bool> TryLinkAsync(
            IAccountService account,
            IGrownUpPurchaseGate grownUpGate,
            BirthdayAgeBand ageBand,
            CancellationToken cancellationToken)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }
            if (grownUpGate == null)
            {
                throw new ArgumentNullException(nameof(grownUpGate));
            }
            if (!await grownUpGate.AuthorizeAsync(
                    ageBand,
                    GrownUpAction.CloudLink,
                    cancellationToken))
            {
                return false;
            }

            await account.LinkGoogleAsync(cancellationToken);
            return true;
        }
    }

    public sealed class FrontendDependencies
    {
        public FrontendDependencies(
            SettingsService settings,
            InputRouter input,
            IAccountService account = null,
            Func<CancellationToken, ValueTask> beginChapterOne = null,
            ISaveService saves = null,
            Func<CancellationToken, ValueTask> startNewGame = null,
            Func<GameSave, CancellationToken, ValueTask> continueGame = null,
            Func<GameSave, bool> canContinue = null,
            Func<GameSave, string> describeCheckpoint = null,
            IGrownUpPurchaseGate grownUpGate = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Account = account;
            BeginChapterOne = beginChapterOne;
            Saves = saves;
            StartNewGame = startNewGame ?? beginChapterOne;
            ContinueGame = continueGame;
            CanContinue = canContinue;
            DescribeCheckpoint = describeCheckpoint;
            GrownUpGate = grownUpGate;
        }

        public SettingsService Settings { get; }

        public InputRouter Input { get; }

        public IAccountService Account { get; }

        public Func<CancellationToken, ValueTask> BeginChapterOne { get; }

        public ISaveService Saves { get; }

        public Func<CancellationToken, ValueTask> StartNewGame { get; }

        public Func<GameSave, CancellationToken, ValueTask> ContinueGame { get; }

        public Func<GameSave, bool> CanContinue { get; }

        public Func<GameSave, string> DescribeCheckpoint { get; }

        public IGrownUpPurchaseGate GrownUpGate { get; }
    }
}
