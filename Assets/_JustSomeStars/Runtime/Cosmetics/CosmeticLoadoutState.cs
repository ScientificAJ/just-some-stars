using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Cosmetics
{
    [Serializable]
    public sealed class CosmeticLoadoutState : IEquatable<CosmeticLoadoutState>
    {
        [SerializeField] private string captain = "cosmetic.captain.clubhouse-canvas";
        [SerializeField] private string ori = "cosmetic.ori.clubhouse-brass";
        [SerializeField] private string ship = "cosmetic.ship.clubhouse-observatory";
        [SerializeField] private string lens = "cosmetic.lens.clubhouse-constellation";
        [SerializeField] private string clubhouse = "cosmetic.clubhouse.patchwork-chair";
        [SerializeField] private string photo = "cosmetic.photo.ori-camera";
        [SerializeField] private string crew = "cosmetic.crew.mira-light-study";
        [SerializeField] private long lastEquippedUtcTicks;

        public string Captain { get => captain; set => captain = value; }
        public string Ori { get => ori; set => ori = value; }
        public string Ship { get => ship; set => ship = value; }
        public string Lens { get => lens; set => lens = value; }
        public string Clubhouse { get => clubhouse; set => clubhouse = value; }
        public string Photo { get => photo; set => photo = value; }
        public string Crew { get => crew; set => crew = value; }

        public long LastEquippedUtcTicks
        {
            get => lastEquippedUtcTicks;
            set => lastEquippedUtcTicks = value;
        }

        public string Selected(CosmeticCategory category)
        {
            return category switch
            {
                CosmeticCategory.Captain => captain,
                CosmeticCategory.Ori => ori,
                CosmeticCategory.Ship => ship,
                CosmeticCategory.Lens => lens,
                CosmeticCategory.Clubhouse => clubhouse,
                CosmeticCategory.Photo => photo,
                CosmeticCategory.Crew => crew,
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };
        }

        public void Set(CosmeticCategory category, string itemId, long equippedUtcTicks)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException(
                    "An equipped cosmetic requires a stable ID.",
                    nameof(itemId));
            }
            if (equippedUtcTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(equippedUtcTicks));
            }

            switch (category)
            {
                case CosmeticCategory.Captain:
                    captain = itemId;
                    break;
                case CosmeticCategory.Ori:
                    ori = itemId;
                    break;
                case CosmeticCategory.Ship:
                    ship = itemId;
                    break;
                case CosmeticCategory.Lens:
                    lens = itemId;
                    break;
                case CosmeticCategory.Clubhouse:
                    clubhouse = itemId;
                    break;
                case CosmeticCategory.Photo:
                    photo = itemId;
                    break;
                case CosmeticCategory.Crew:
                    crew = itemId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }

            lastEquippedUtcTicks = equippedUtcTicks;
        }

        public CosmeticLoadoutState Copy()
        {
            return new CosmeticLoadoutState
            {
                captain = captain,
                ori = ori,
                ship = ship,
                lens = lens,
                clubhouse = clubhouse,
                photo = photo,
                crew = crew,
                lastEquippedUtcTicks = lastEquippedUtcTicks,
            };
        }

        public bool Equals(CosmeticLoadoutState other)
        {
            return other != null &&
                string.Equals(captain, other.captain, StringComparison.Ordinal) &&
                string.Equals(ori, other.ori, StringComparison.Ordinal) &&
                string.Equals(ship, other.ship, StringComparison.Ordinal) &&
                string.Equals(lens, other.lens, StringComparison.Ordinal) &&
                string.Equals(clubhouse, other.clubhouse, StringComparison.Ordinal) &&
                string.Equals(photo, other.photo, StringComparison.Ordinal) &&
                string.Equals(crew, other.crew, StringComparison.Ordinal) &&
                lastEquippedUtcTicks == other.lastEquippedUtcTicks;
        }

        public override bool Equals(object obj) => Equals(obj as CosmeticLoadoutState);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = captain?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (ori?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (ship?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (lens?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (clubhouse?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (photo?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (crew?.GetHashCode() ?? 0);
                return (hash * 397) ^ lastEquippedUtcTicks.GetHashCode();
            }
        }
    }
}
