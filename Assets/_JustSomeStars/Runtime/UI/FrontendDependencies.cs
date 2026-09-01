using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;

namespace JustSomeStars.Runtime.UI
{
    public sealed class FrontendDependencies
    {
        public FrontendDependencies(
            SettingsService settings,
            InputRouter input,
            IAccountService account = null,
            Func<CancellationToken, ValueTask> beginChapterOne = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Account = account;
            BeginChapterOne = beginChapterOne;
        }

        public SettingsService Settings { get; }

        public InputRouter Input { get; }

        public IAccountService Account { get; }

        public Func<CancellationToken, ValueTask> BeginChapterOne { get; }
    }
}
