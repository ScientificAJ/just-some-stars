using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;

namespace JustSomeStars.Runtime.UI.Account
{
    public sealed class BirthdaySetupController
    {
        public const string Explanation =
            "Your birthday stays private. It helps us choose safe account options and remember your yearly gift.";

        private readonly BirthdayGiftService m_Birthdays;
        private readonly GrownUpConfirmationController m_GrownUp;

        public BirthdaySetupController(
            BirthdayGiftService birthdays,
            GrownUpConfirmationController grownUp)
        {
            m_Birthdays = birthdays ?? throw new ArgumentNullException(nameof(birthdays));
            m_GrownUp = grownUp ?? throw new ArgumentNullException(nameof(grownUp));
        }

        public async ValueTask<BirthdayUpdateResult> SubmitAsync(
            int day,
            int month,
            int year,
            CancellationToken cancellationToken)
        {
            try
            {
                return await m_Birthdays.UpdateBirthdayAsync(
                    day,
                    month,
                    year,
                    grownUpConfirmed: false,
                    cancellationToken);
            }
            catch (BirthdayCorrectionRequiresGrownUpException)
            {
                if (!await m_GrownUp.ConfirmAsync(
                        GrownUpAction.BirthdayCorrection,
                        cancellationToken))
                {
                    return new BirthdayUpdateResult(
                        BirthdayUpdateStatus.RequiresGrownUp);
                }

                return await m_Birthdays.UpdateBirthdayAsync(
                    day,
                    month,
                    year,
                    grownUpConfirmed: true,
                    cancellationToken);
            }
        }
    }
}
