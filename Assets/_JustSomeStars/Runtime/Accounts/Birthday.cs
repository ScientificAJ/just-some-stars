using System;
using JustSomeStars.Runtime.Saving;

namespace JustSomeStars.Runtime.Accounts
{
    public enum BirthdayAgeBand
    {
        Unknown = 0,
        Child = 1,
        Teen = 2,
        Adult = 3,
    }

    public enum GrownUpAction
    {
        CloudLink = 0,
        Purchase = 1,
        ExternalLink = 2,
        BirthdayCorrection = 3,
    }

    public enum GrownUpRequirement
    {
        None = 0,
        ConfirmAction = 1,
        AskAGrownUp = 2,
    }

    public readonly struct BirthdayDate : IEquatable<BirthdayDate>
    {
        private BirthdayDate(int day, int month, int year)
        {
            Day = day;
            Month = month;
            Year = year;
        }

        public int Day { get; }

        public int Month { get; }

        public int Year { get; }

        public static BirthdayDate Create(
            int day,
            int month,
            int year,
            DateTimeOffset trustedNow)
        {
            DateTime value;
            try
            {
                value = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    "Birthday must be a real calendar date.");
            }

            var today = trustedNow.UtcDateTime.Date;
            if (value > today || year < today.Year - 120)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(year),
                    "Birthday must describe a plausible date in the past.");
            }

            return new BirthdayDate(day, month, year);
        }

        internal static BirthdayDate FromState(BirthdayState state)
        {
            if (state == null || !state.HasValue)
            {
                throw new InvalidOperationException("A birthday has not been provided.");
            }

            return new BirthdayDate(state.Day, state.Month, state.Year);
        }

        public bool Equals(BirthdayDate other) =>
            Day == other.Day && Month == other.Month && Year == other.Year;

        public override bool Equals(object obj) =>
            obj is BirthdayDate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Day * 397) ^ Month) * 397 ^ Year;
            }
        }
    }

    public readonly struct BirthdayGiftWindow
    {
        internal BirthdayGiftWindow(
            bool isActive,
            int giftYear,
            DateTime startUtc,
            DateTime endExclusiveUtc)
        {
            IsActive = isActive;
            GiftYear = giftYear;
            StartUtc = startUtc;
            EndExclusiveUtc = endExclusiveUtc;
        }

        public bool IsActive { get; }

        public int GiftYear { get; }

        public DateTime StartUtc { get; }

        public DateTime EndExclusiveUtc { get; }
    }

    public sealed class BirthdayCorrectionRequiresGrownUpException :
        InvalidOperationException
    {
        public BirthdayCorrectionRequiresGrownUpException()
            : base("Another birthday correction requires grown-up confirmation.")
        {
        }
    }

    public static class BirthdayPolicy
    {
        private const int GiftWindowDays = 30;

        public static int AgeOn(BirthdayDate birthday, DateTimeOffset trustedNow)
        {
            var today = trustedNow.UtcDateTime.Date;
            var anniversary = PrivacyAnniversary(birthday, today.Year);
            return today.Year - birthday.Year - (today < anniversary ? 1 : 0);
        }

        public static BirthdayAgeBand AgeBandOn(
            BirthdayDate birthday,
            DateTimeOffset trustedNow)
        {
            var age = AgeOn(birthday, trustedNow);
            if (age < 0)
            {
                return BirthdayAgeBand.Unknown;
            }

            if (age <= 12)
            {
                return BirthdayAgeBand.Child;
            }

            return age <= 17 ? BirthdayAgeBand.Teen : BirthdayAgeBand.Adult;
        }

        public static BirthdayGiftWindow GiftWindowOn(
            BirthdayDate birthday,
            DateTimeOffset trustedNow)
        {
            var today = trustedNow.UtcDateTime.Date;
            var start = GiftAnniversary(birthday, today.Year);
            if (today < start)
            {
                start = GiftAnniversary(birthday, today.Year - 1);
            }

            var end = start.AddDays(GiftWindowDays);
            return new BirthdayGiftWindow(
                today >= start && today < end,
                start.Year,
                start,
                end);
        }

        public static BirthdayState ApplyDate(
            BirthdayState current,
            BirthdayDate birthday,
            bool grownUpConfirmed)
        {
            current ??= new BirthdayState();
            if (current.HasValue &&
                current.Day == birthday.Day &&
                current.Month == birthday.Month &&
                current.Year == birthday.Year)
            {
                return current.Copy();
            }

            var isCorrection = current.HasValue;
            if (isCorrection && current.CorrectionCount >= 1 && !grownUpConfirmed)
            {
                throw new BirthdayCorrectionRequiresGrownUpException();
            }

            return new BirthdayState
            {
                HasValue = true,
                Day = birthday.Day,
                Month = birthday.Month,
                Year = birthday.Year,
                CorrectionCount = isCorrection
                    ? checked(current.CorrectionCount + 1)
                    : 0,
                LastBirthdayGiftYear = current.LastBirthdayGiftYear,
            };
        }

        public static GrownUpRequirement RequirementFor(
            BirthdayAgeBand ageBand,
            GrownUpAction action)
        {
            if (ageBand == BirthdayAgeBand.Unknown ||
                ageBand == BirthdayAgeBand.Child)
            {
                return GrownUpRequirement.AskAGrownUp;
            }

            if (ageBand == BirthdayAgeBand.Teen)
            {
                return action == GrownUpAction.CloudLink
                    ? GrownUpRequirement.ConfirmAction
                    : GrownUpRequirement.AskAGrownUp;
            }

            return action == GrownUpAction.Purchase ||
                action == GrownUpAction.ExternalLink ||
                action == GrownUpAction.BirthdayCorrection
                ? GrownUpRequirement.ConfirmAction
                : GrownUpRequirement.None;
        }

        private static DateTime PrivacyAnniversary(BirthdayDate birthday, int year)
        {
            if (birthday.Month == 2 && birthday.Day == 29 &&
                !DateTime.IsLeapYear(year))
            {
                return new DateTime(year, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            return new DateTime(
                year,
                birthday.Month,
                birthday.Day,
                0,
                0,
                0,
                DateTimeKind.Utc);
        }

        private static DateTime GiftAnniversary(BirthdayDate birthday, int year)
        {
            if (birthday.Month == 2 && birthday.Day == 29 &&
                !DateTime.IsLeapYear(year))
            {
                return new DateTime(year, 2, 28, 0, 0, 0, DateTimeKind.Utc);
            }

            return new DateTime(
                year,
                birthday.Month,
                birthday.Day,
                0,
                0,
                0,
                DateTimeKind.Utc);
        }
    }
}
