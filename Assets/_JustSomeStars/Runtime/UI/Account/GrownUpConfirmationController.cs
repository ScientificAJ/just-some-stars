using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;

namespace JustSomeStars.Runtime.UI.Account
{
    public readonly struct GrownUpPrompt
    {
        public GrownUpPrompt(GrownUpAction action, string copy)
        {
            Action = action;
            Copy = copy ?? string.Empty;
        }

        public GrownUpAction Action { get; }

        public string Copy { get; }
    }

    public sealed class GrownUpConfirmationController
    {
        public const string AskCopy =
            "Please ask a grown-up to confirm this change.";

        private readonly Func<GrownUpPrompt, CancellationToken, ValueTask<bool>>
            m_Confirm;

        public GrownUpConfirmationController(
            Func<GrownUpPrompt, CancellationToken, ValueTask<bool>> confirm)
        {
            m_Confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
        }

        public ValueTask<bool> ConfirmAsync(
            GrownUpAction action,
            CancellationToken cancellationToken) =>
            m_Confirm(new GrownUpPrompt(action, AskCopy), cancellationToken);
    }
}
