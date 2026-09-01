using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Cosmetics
{
    public enum EditionFeature
    {
        ExpeditionReplay = 0,
        AdvancedPhotoMode = 1,
        CinematicModifiers = 2,
        DevelopmentArchive = 3,
        SoundtrackJukebox = 4,
    }

    public sealed class EditionFeatureService : IGameService
    {
        public const string ExplorerEntitlementId = "explorer_edition";

        private readonly IStoreService m_Store;
        private bool m_PreviouslyVerifiedExplorer;

        public EditionFeatureService(IStoreService store)
        {
            m_Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool BaseStoryAvailable => true;
        public bool AtlasScienceAvailable => true;
        public bool StandardPhotoModeAvailable => true;

        public bool ExplorerEditionOwned => m_PreviouslyVerifiedExplorer;

        public bool IsAvailable(EditionFeature feature)
        {
            if (!Enum.IsDefined(typeof(EditionFeature), feature))
            {
                throw new ArgumentOutOfRangeException(nameof(feature));
            }

            return m_PreviouslyVerifiedExplorer;
        }

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await m_Store.InitializeAsync(cancellationToken);
            Observe(m_Store.CurrentEntitlements);
            return result;
        }

        public ValueTask ShutdownAsync() => default;

        public void Observe(EntitlementSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsVerified)
            {
                return;
            }

            m_PreviouslyVerifiedExplorer = snapshot.Owns(
                new ContentId(ExplorerEntitlementId));
        }
    }
}
