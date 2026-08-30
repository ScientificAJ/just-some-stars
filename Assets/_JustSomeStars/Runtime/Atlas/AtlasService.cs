using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Missions;

namespace JustSomeStars.Runtime.Atlas
{
    public sealed class AtlasService : IDisposable
    {
        private readonly IProgressionStore m_Progression;
        private readonly Dictionary<ContentId, AtlasEntry> m_ByPhenomenon;
        private readonly Dictionary<ContentId, AtlasEntry> m_ById;
        private readonly Dictionary<ContentId, ScienceSourceDefinition> m_Sources;
        private readonly LocalizedEnglishCatalog m_English;
        private readonly IDisposable m_Subscription;
        private bool m_Disposed;

        public AtlasService(
            GameEventBus gameEvents,
            IProgressionStore progression,
            IEnumerable<AtlasEntry> entries,
            IEnumerable<ScienceSourceDefinition> sources,
            LocalizedEnglishCatalog english)
        {
            m_Progression = progression ?? throw new ArgumentNullException(nameof(progression));
            m_English = english ?? throw new ArgumentNullException(nameof(english));
            m_English.ValidateOrThrow();
            var entryArray = (entries ?? throw new ArgumentNullException(nameof(entries)))
                .ToArray();
            var sourceArray = (sources ?? throw new ArgumentNullException(nameof(sources)))
                .ToArray();
            if (entryArray.Any(entry => entry == null) ||
                sourceArray.Any(source => source == null))
            {
                throw new ArgumentException("Atlas content cannot contain null assets.");
            }

            foreach (var entry in entryArray)
            {
                entry.ValidateOrThrow();
            }

            foreach (var source in sourceArray)
            {
                source.ValidateOrThrow();
            }

            m_ById = RequireUnique(entryArray, entry => entry.StableId, "Atlas entry");
            m_ByPhenomenon = RequireUnique(
                entryArray,
                entry => entry.PhenomenonId,
                "Atlas phenomenon");
            m_Sources = RequireUnique(
                sourceArray,
                source => source.StableId,
                "science source");
            foreach (var entry in entryArray)
            {
                if (!m_Sources.ContainsKey(entry.ScienceSourceId))
                {
                    throw new InvalidOperationException(
                        $"Atlas entry '{entry.StableId}' references missing source '{entry.ScienceSourceId}'.");
                }

                _ = m_English.Resolve(entry.ShortTextKey);
                _ = m_English.Resolve(entry.BalancedTextKey);
                _ = m_English.Resolve(entry.DeepTextKey);
            }

            m_Subscription = (gameEvents ?? throw new ArgumentNullException(nameof(gameEvents)))
                .Subscribe<PhenomenonObserved>(OnObserved);
        }

        public string ResolveEnglish(ContentId entryId, ScienceDepth depth)
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(typeof(ScienceDepth), depth))
            {
                throw new ArgumentOutOfRangeException(nameof(depth));
            }

            if (!m_ById.TryGetValue(entryId, out var entry))
            {
                throw new KeyNotFoundException($"Atlas entry '{entryId}' is not authored.");
            }

            var key = depth switch
            {
                ScienceDepth.Guided => entry.ShortTextKey,
                ScienceDepth.Balanced => entry.BalancedTextKey,
                ScienceDepth.Deep => entry.DeepTextKey,
                _ => throw new ArgumentOutOfRangeException(nameof(depth)),
            };
            return m_English.Resolve(key);
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Subscription.Dispose();
            m_Disposed = true;
        }

        private void OnObserved(PhenomenonObserved observation)
        {
            if (!m_Disposed && m_ByPhenomenon.TryGetValue(observation.PhenomenonId, out var entry))
            {
                m_Progression.TryUnlock(entry.PhenomenonId, entry.StableId);
            }
        }

        private static Dictionary<ContentId, T> RequireUnique<T>(
            IEnumerable<T> values,
            Func<T, ContentId> keySelector,
            string role)
        {
            var result = new Dictionary<ContentId, T>();
            foreach (var value in values)
            {
                var key = keySelector(value);
                if (!result.TryAdd(key, value))
                {
                    throw new ArgumentException($"{role} ID '{key}' is duplicated.");
                }
            }

            return result;
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(AtlasService));
            }
        }
    }
}
