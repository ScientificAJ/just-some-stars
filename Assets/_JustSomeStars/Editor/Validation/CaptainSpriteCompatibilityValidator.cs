using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Cosmetics;

namespace JustSomeStars.Editor.Validation
{
    public static class CaptainSpriteCompatibilityValidator
    {
        public static IReadOnlyList<string> Validate(
            CaptainSpriteSet spriteSet,
            CaptainSpriteLoadout loadout)
        {
            var issues = new List<string>();
            try
            {
                if (spriteSet == null)
                {
                    throw new ArgumentNullException(nameof(spriteSet));
                }
                if (loadout == null)
                {
                    throw new ArgumentNullException(nameof(loadout));
                }
                loadout.ValidateOrThrow();
                spriteSet.ValidateOrThrow();
            }
            catch (Exception exception)
            {
                issues.Add(exception.Message);
            }
            return issues;
        }
    }
}
