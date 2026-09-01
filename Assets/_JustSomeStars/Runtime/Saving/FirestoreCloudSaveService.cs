using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Cosmetics;
using UnityEngine;

namespace JustSomeStars.Runtime.Saving
{
    public sealed class FirestoreDocumentWrite
    {
        public FirestoreDocumentWrite(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new ArgumentException(
                    "A canonical cloud payload is required.",
                    nameof(payloadJson));
            }

            PayloadJson = payloadJson;
        }

        public string PayloadJson { get; }

        public bool SetCreatedAtOnCreate => true;

        public bool RequiresServerAuthoritativeCreate => true;

        public bool PreserveCreatedAtOnUpdate => true;

        public bool PreserveServerOwnedBirthdayGiftYearsOnUpdate => true;

        public bool SetUpdatedAtToServerTime => true;
    }

    public interface IFirestoreDocumentGateway : IGameService
    {
        bool IsConfigured { get; }

        ValueTask<string> ReadAsync(
            string documentPath,
            CancellationToken cancellationToken);

        ValueTask WriteAsync(
            string documentPath,
            FirestoreDocumentWrite document,
            CancellationToken cancellationToken);

        ValueTask<CloudCommitResult> WriteIfVersionAsync(
            string documentPath,
            FirestoreDocumentWrite document,
            CloudSaveVersion expectedRemote,
            CancellationToken cancellationToken);

        ValueTask DeleteAsync(
            string documentPath,
            CancellationToken cancellationToken);
    }

    public sealed class UnavailableFirestoreDocumentGateway :
        IFirestoreDocumentGateway
    {
        public bool IsConfigured => false;

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<StartupResult>(StartupResult.Unavailable(
                "Google backup is not configured for this build."));
        }

        public ValueTask ShutdownAsync() => default;

        public ValueTask<string> ReadAsync(
            string documentPath,
            CancellationToken cancellationToken) => throw Unavailable();

        public ValueTask WriteAsync(
            string documentPath,
            FirestoreDocumentWrite document,
            CancellationToken cancellationToken) => throw Unavailable();

        public ValueTask<CloudCommitResult> WriteIfVersionAsync(
            string documentPath,
            FirestoreDocumentWrite document,
            CloudSaveVersion expectedRemote,
            CancellationToken cancellationToken) => throw Unavailable();

        public ValueTask DeleteAsync(
            string documentPath,
            CancellationToken cancellationToken) => throw Unavailable();

        private static InvalidOperationException Unavailable() =>
            new InvalidOperationException(
                "Google backup is not configured for this build.");
    }

    public sealed class FirestoreCloudSaveService : ICloudSaveService
    {
        public const int EnvelopeSchemaVersion = 1;
        private static readonly Regex ValidUid = new Regex(
            "^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$",
            RegexOptions.CultureInvariant);
        private static readonly Regex ValidCloudId = new Regex(
            "^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$",
            RegexOptions.CultureInvariant);

        private readonly IFirestoreDocumentGateway m_Gateway;

        public FirestoreCloudSaveService(IFirestoreDocumentGateway gateway)
        {
            m_Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await m_Gateway.InitializeAsync(cancellationToken);
        }

        public ValueTask ShutdownAsync() => m_Gateway.ShutdownAsync();

        public async ValueTask<CloudSaveSnapshot?> DownloadAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            var path = BuildPath(userId);
            var document = await m_Gateway.ReadAsync(path, cancellationToken);
            if (string.IsNullOrEmpty(document))
            {
                return null;
            }

            return Deserialize(document);
        }

        public async ValueTask UploadAsync(
            string userId,
            GameSave save,
            CancellationToken cancellationToken)
        {
            var document = CreateWrite(save);
            await m_Gateway.WriteAsync(
                BuildPath(userId),
                document,
                cancellationToken);
        }

        public ValueTask<CloudCommitResult> UploadIfUnchangedAsync(
            string userId,
            GameSave save,
            CloudSaveVersion expectedRemote,
            CancellationToken cancellationToken)
        {
            return m_Gateway.WriteIfVersionAsync(
                BuildPath(userId),
                CreateWrite(save),
                expectedRemote,
                cancellationToken);
        }

        public ValueTask DeleteAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = BuildPath(userId);
            throw new InvalidOperationException(
                "Cloud profile deletion requires the server-authoritative " +
                "account lifecycle gateway.");
        }

        public async ValueTask<string> ExportAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            var document = await m_Gateway.ReadAsync(
                BuildPath(userId),
                cancellationToken);
            if (string.IsNullOrEmpty(document))
            {
                throw new FileNotFoundException("No cloud backup exists for this account.");
            }

            _ = Deserialize(document);
            return document;
        }

        private static string BuildPath(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || !ValidUid.IsMatch(userId))
            {
                throw new ArgumentException(
                    "Firebase user ID is not valid for a direct document path.",
                    nameof(userId));
            }

            return "users/" + userId;
        }

        private static FirestoreDocumentWrite CreateWrite(GameSave save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var projected = save.Copy();
            projected.Photographs = Array.Empty<PhotoMetadata>();
            projected.ThrowIfInvalid(nameof(save));
            RequireCloudProjection(projected, nameof(save));
            var envelope = new CloudEnvelope
            {
                documentSchemaVersion = EnvelopeSchemaVersion,
                revision = projected.Metadata.Revision,
                clientWriteId = Guid.NewGuid().ToString("N"),
                save = CloudSaveProjection.From(projected),
            };
            return new FirestoreDocumentWrite(
                JsonUtility.ToJson(envelope, prettyPrint: true));
        }

        private static CloudSaveSnapshot Deserialize(string document)
        {
            try
            {
                var envelope = JsonUtility.FromJson<CloudEnvelope>(document);
                if (envelope == null ||
                    envelope.documentSchemaVersion != EnvelopeSchemaVersion ||
                    envelope.revision < 0 ||
                    string.IsNullOrWhiteSpace(envelope.clientWriteId) ||
                    envelope.save == null)
                {
                    throw new InvalidDataException("Cloud backup envelope is invalid.");
                }

                var save = envelope.save.ToGameSave();
                save.ThrowIfInvalid(nameof(document));
                RequireCloudProjection(save, nameof(document));
                if (save.SchemaVersion != GameSave.CurrentSchemaVersion ||
                    save.Photographs.Length != 0 ||
                    save.Metadata.Revision != envelope.revision)
                {
                    throw new InvalidDataException(
                        "Cloud backup payload violates the current projection contract.");
                }

                return new CloudSaveSnapshot(
                    save,
                    envelope.documentSchemaVersion,
                    new CloudSaveVersion(envelope.revision, envelope.clientWriteId),
                    CloudSaveSource.Server);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NullReferenceException)
            {
                throw new InvalidDataException(
                    "Cloud backup document could not be validated.",
                    exception);
            }
        }

        private static void RequireCloudProjection(
            GameSave save,
            string parameterName)
        {
            RequireCloudId(save.Story.CheckpointId, parameterName);
            RequireCloudId(save.Captain.BodyFamilyId, parameterName);
            RequireCloudId(save.Captain.AppearancePresetId, parameterName);
            RequireCloudId(save.Captain.SuitCosmeticId, parameterName);
            RequireCloudId(save.CosmeticLoadout.Captain, parameterName);
            RequireCloudId(save.CosmeticLoadout.Ori, parameterName);
            RequireCloudId(save.CosmeticLoadout.Ship, parameterName);
            RequireCloudId(save.CosmeticLoadout.Lens, parameterName);
            RequireCloudId(save.CosmeticLoadout.Clubhouse, parameterName);
            RequireCloudId(save.CosmeticLoadout.Photo, parameterName);
            RequireCloudId(save.CosmeticLoadout.Crew, parameterName);
            RequireCloudId(save.Metadata.SaveId, parameterName);
            RequireCloudIds(save.DiscoveryIds, 2048, parameterName);
            RequireCloudIds(save.EarnedCosmeticIds, 1024, parameterName);
            RequireCloudIds(save.AtlasEntryIds, 4096, parameterName);
            RequireCloudIds(save.Mission.CompletedNodeIds, 4096, parameterName);
            RequireCloudIds(save.Mission.ActiveNodeIds, 4096, parameterName);
            if (save.Mission.HasMission)
            {
                RequireCloudId(save.Mission.MissionId, parameterName);
                RequireCloudId(save.Mission.CheckpointNodeId, parameterName);
            }
        }

        private static void RequireCloudIds(
            string[] values,
            int maximumCount,
            string parameterName)
        {
            if (values.Length > maximumCount)
            {
                throw new ArgumentException(
                    "Cloud backup identifier collection exceeds its safe limit.",
                    parameterName);
            }

            foreach (var value in values)
            {
                RequireCloudId(value, parameterName);
            }
        }

        private static void RequireCloudId(string value, string parameterName)
        {
            if (!ValidCloudId.IsMatch(value))
            {
                throw new ArgumentException(
                    "Cloud backup identifiers must use the canonical 128-character format.",
                    parameterName);
            }
        }

        [Serializable]
        private sealed class CloudEnvelope
        {
            public int documentSchemaVersion;
            public long revision;
            public string clientWriteId;
            public CloudSaveProjection save;
        }

        [Serializable]
        private sealed class CloudSaveProjection
        {
            public int schemaVersion;
            public StoryProgress story;
            public ChapterOneProgress chapterOne;
            public MissionProgress mission;
            public CaptainState captain;
            public CosmeticLoadoutState cosmeticLoadout;
            public string[] discoveryIds;
            public string[] earnedCosmeticIds;
            public string[] atlasEntryIds;
            public BirthdayState birthday;
            public SaveMetadata metadata;

            public static CloudSaveProjection From(GameSave save)
            {
                return new CloudSaveProjection
                {
                    schemaVersion = save.SchemaVersion,
                    story = save.Story.Copy(),
                    chapterOne = save.ChapterOne.Copy(),
                    mission = save.Mission.Copy(),
                    captain = save.Captain.Copy(),
                    cosmeticLoadout = save.CosmeticLoadout.Copy(),
                    discoveryIds = (string[])save.DiscoveryIds.Clone(),
                    earnedCosmeticIds = (string[])save.EarnedCosmeticIds.Clone(),
                    atlasEntryIds = (string[])save.AtlasEntryIds.Clone(),
                    birthday = save.Birthday.Copy(),
                    metadata = save.Metadata.Copy(),
                };
            }

            public GameSave ToGameSave()
            {
                if (metadata == null)
                {
                    throw new InvalidDataException(
                        "Cloud backup save projection is invalid.");
                }

                var projected = GameSave.CreateNew(
                    metadata.SaveId,
                    metadata.CreatedUtcTicks);
                projected.Story = story;
                projected.ChapterOne = chapterOne;
                projected.Mission = mission;
                projected.Captain = captain;
                projected.CosmeticLoadout = cosmeticLoadout;
                projected.DiscoveryIds = discoveryIds;
                projected.EarnedCosmeticIds = earnedCosmeticIds;
                projected.AtlasEntryIds = atlasEntryIds;
                projected.Photographs = Array.Empty<PhotoMetadata>();
                projected.Birthday = birthday;
                projected.Metadata = metadata;
                projected.SetSchemaVersionForMigration(schemaVersion);

                var serializer = new JsonSaveSerializer(SaveMigrator.CreateCurrent());
                if (!serializer.TryDeserialize(
                        JsonUtility.ToJson(projected, prettyPrint: true),
                        out var migrated))
                {
                    throw new InvalidDataException(
                        "Cloud backup save projection is invalid.");
                }

                return migrated;
            }
        }
    }
}
