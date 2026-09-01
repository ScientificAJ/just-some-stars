using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JustSomeStars.Runtime.Cosmetics;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Editor
{
    public static class Task27CosmeticCatalogBuilder
    {
        private const string CatalogPath =
            "Assets/_JustSomeStars/Content/Cosmetics/CosmeticCatalog.asset";
        private const string IconRoot =
            "Assets/_JustSomeStars/Art/2D/Cosmetics/IconAtlases";
        private const string PresentationRoot =
            "Assets/_JustSomeStars/Art/2D/Cosmetics/PresentationAtlases";
        private const string ArtManifestPath =
            "Assets/_JustSomeStars/Art/2D/Cosmetics/cosmetic-presentation-manifest.json";
        private const string CollectionRoot =
            "Assets/_JustSomeStars/Content/Cosmetics";
        private const string CsvPath = "docs/product/cosmetic-catalog.csv";
        private const int GridSize = 4;
        private const int CellSize = 312;

        private static readonly CaptainBodyFamily[] AllCaptainFits =
        {
            CaptainBodyFamily.Compact,
            CaptainBodyFamily.Average,
            CaptainBodyFamily.TallBroad,
        };

        private static readonly string[] ActorClips =
        {
            "idle", "run", "turn", "jump", "land", "interact", "scan", "climb",
        };

        private static readonly string[] ActorEvents =
        {
            "FootContact", "ToolAttach", "ToolDetach", "Interaction", "Audio", "Vfx",
        };

        private static readonly string[] PrimaryColors =
        {
            "#D68A3A", "#C7B18B", "#4E9CB5", "#7EC7E8",
            "#734C95", "#4A73A5", "#D5633D", "#7953B8",
            "#1E3156", "#2A8A83", "#E0AA45", "#B899D7",
            "#F2C569", "#4E718D", "#9D5A38", "#34396B",
        };

        private static readonly string[] AccentColors =
        {
            "#55D9FF", "#F0A33A", "#A9ECFF", "#835CFF",
            "#63DCFF", "#BA72FF", "#FF9B54", "#E29BFF",
            "#BDE9FF", "#7BFFE0", "#FFD27B", "#A89CFF",
            "#FFF0A1", "#72CDEB", "#FFB573", "#B573FF",
        };

        private static readonly AtlasSpec[] Specs =
        {
            new AtlasSpec(
                "CaptainSuits.png",
                "CaptainSuitsPresentation.png",
                CosmeticCategory.Captain,
                new[]
                {
                    "clubhouse-canvas", "surveyor-blue", "mirra-sunrise", "mirra-frostline",
                    "koro-glacier", "vesper-violet", "aster-drift", "signal-resonance",
                    "founder-starcape", "founder-nightwatch", "explorer-fieldnotes",
                    "explorer-celestial", "birthday-banner", "launch-navigator",
                    "launch-planetary", "launch-starlight",
                }),
            new AtlasSpec(
                "CaptainGear.png",
                "CaptainGearPresentation.png",
                CosmeticCategory.Captain,
                new[]
                {
                    "explorer-bubble-helmet", "workshop-goggles", "signal-visor",
                    "founder-field-cap", "stitched-gloves", "lens-gauntlets",
                    "laceup-boots", "aurora-boots", "observatory-pack", "solar-field-pack",
                    "founder-patch", "mirra-patch", "constellation-watch", "star-charm",
                    "birthday-charm", "ori-wristlink",
                }),
            new AtlasSpec(
                "Ori.png",
                "OriPresentation.png",
                CosmeticCategory.Ori,
                new[]
                {
                    "clubhouse-brass", "mirra-frost", "signal-prism", "builder-gripper",
                    "aster-beacon", "founder-dome", "explorer-archive", "birthday-starlight",
                    "garden-shell", "vesper-diver", "navigator-sail", "shadow-shell",
                    "repair-rig", "festival-canopy", "moon-chimes", "comet-trail",
                }),
            new AtlasSpec(
                "Ship.png",
                "ShipPresentation.png",
                CosmeticCategory.Ship,
                new[]
                {
                    "clubhouse-observatory", "garden-habitat", "surveyor-array",
                    "founder-constellation", "signal-crystal", "living-canopy",
                    "midnight-cockpit", "warm-cabin", "mirrorball-landing",
                    "birthday-flight", "aster-patched", "vesper-snowcap", "orrery-hull",
                    "builder-rig", "signal-tower", "comet-launch",
                }),
            new AtlasSpec(
                "Lens.png",
                "LensPresentation.png",
                CosmeticCategory.Lens,
                new[]
                {
                    "clubhouse-constellation", "galaxy-field", "planet-grid", "comet-glass",
                    "home-memory", "botany-display", "orbit-display", "signal-prism",
                    "aster-beacon", "founder-map", "explorer-archive", "field-wristlens",
                    "birthday-cake", "observatory-scope", "rocket-window", "starlight-compass",
                }),
            new AtlasSpec(
                "Clubhouse.png",
                "ClubhousePresentation.png",
                CosmeticCategory.Clubhouse,
                new[]
                {
                    "patchwork-chair", "painted-telescope", "planet-mobile", "star-projector",
                    "pallet-sofa", "brass-orrery", "constellation-window", "atlas-table",
                    "signal-stringlights", "miniature-observatory", "founder-rug",
                    "canvas-hideaway", "specimen-shelf", "solar-scroll", "moon-chair",
                    "ori-radio",
                }),
            new AtlasSpec(
                "Photo.png",
                "PhotoPresentation.png",
                CosmeticCategory.Photo,
                new[]
                {
                    "ori-camera", "field-satchel", "brass-tripod", "aster-frame",
                    "moon-compass", "vesper-filter", "koro-filter", "mirra-filter",
                    "crew-keepsake", "signal-charm", "film-roll", "founder-album",
                    "hologram-frame", "chronicler-pose", "captain-pose", "stargazer-pose",
                }),
            new AtlasSpec(
                "Crew.png",
                "CrewPresentation.png",
                CosmeticCategory.Crew,
                new[]
                {
                    "mira-light-study", "juno-builder-rig", "kai-navigator-layers",
                    "bea-field-kit", "clubhouse-flight", "founder-stargazer",
                    "founder-navigator", "signal-surveyor", "mirra-expedition",
                    "koro-observer", "vesper-fieldcoat", "aster-driftcoat",
                    "explorer-constellation", "explorer-archive", "launch-homecoming",
                    "birthday-expedition",
                }),
        };

        [MenuItem("Just Some Stars/Task 27/Build Cosmetic Catalogue")]
        public static void Build()
        {
            ValidateInputs();
            EnsureFolder("Assets/_JustSomeStars/Art/2D/Cosmetics");
            EnsureFolder(IconRoot);
            EnsureFolder(PresentationRoot);
            EnsureFolder("Assets/_JustSomeStars/Content/Cosmetics");
            EnsureFolder("docs/product");

            for (var specIndex = 0; specIndex < Specs.Length; specIndex++)
            {
                ConfigureAtlas(Specs[specIndex]);
            }

            var definitions = new List<CosmeticDefinition>(128);
            var csv = new StringBuilder();
            csv.AppendLine(
                "id,display_name,category,source,pack,product,entitlements,can_be_earned," +
                "body_fits,icon_atlas,icon_cell,presentation_sprite,attachment_asset," +
                "palette_mask,layer,effect");

            for (var specIndex = 0; specIndex < Specs.Length; specIndex++)
            {
                var spec = Specs[specIndex];
                var iconPath = $"{IconRoot}/{spec.FileName}";
                var presentationPath = $"{PresentationRoot}/{spec.PresentationFileName}";
                var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(iconPath)
                    .OfType<Sprite>()
                    .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
                var presentationSprites =
                    AssetDatabase.LoadAllAssetRepresentationsAtPath(presentationPath)
                        .OfType<Sprite>()
                        .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
                for (var itemIndex = 0; itemIndex < spec.Slugs.Length; itemIndex++)
                {
                    var id = ItemId(spec.Category, spec.Slugs[itemIndex], specIndex, itemIndex);
                    if (!sprites.TryGetValue(id, out var icon))
                    {
                        throw new InvalidOperationException(
                            $"Icon atlas '{iconPath}' did not publish sprite '{id}'.");
                    }
                    if (!presentationSprites.TryGetValue(id, out var presentationSprite))
                    {
                        throw new InvalidOperationException(
                            $"Presentation atlas '{presentationPath}' did not publish sprite '{id}'.");
                    }

                    var ownership = Ownership(specIndex, itemIndex);
                    var packId = PackId(specIndex, itemIndex, ownership);
                    var productId = ownership == CosmeticOwnershipSource.IndividualPurchase
                        ? $"jss.cosmetic.{CategorySlug(spec.Category)}.{spec.Slugs[itemIndex]}"
                        : string.Empty;
                    var entitlements = Entitlements(ownership, packId, productId);
                    var layerId = LayerId(specIndex, itemIndex);
                    var presentation = $"{presentationPath}#{id}";
                    var attachment = AttachmentPath(specIndex, itemIndex, layerId);
                    var paletteMask = PaletteMaskPath(spec.Category, layerId);
                    var effectId = $"cosmetic.effect.{CategorySlug(spec.Category)}.{spec.Slugs[itemIndex]}";
                    var silhouette = spec.Category == CosmeticCategory.Captain;
                    var bodyFits = silhouette ? AllCaptainFits : Array.Empty<CaptainBodyFamily>();
                    var clipIds = Clips(spec.Category);
                    var frameEvents = FrameEvents(spec.Category);
                    var displayName = DisplayName(spec.Slugs[itemIndex]);
                    var canBeEarned = itemIndex <= 9 && ownership != CosmeticOwnershipSource.Birthday;

                    definitions.Add(CosmeticDefinition.CreateForEditor(
                        id,
                        displayName,
                        spec.Category,
                        ownership,
                        Rarity(ownership),
                        packId,
                        productId,
                        entitlements,
                        bodyFits,
                        icon,
                        presentationSprite,
                        presentation,
                        attachment,
                        paletteMask,
                        layerId,
                        effectId,
                        PrimaryColors[itemIndex],
                        AccentColors[itemIndex],
                        canBeEarned,
                        silhouette,
                        clipIds,
                        frameEvents));

                    AppendCsv(
                        csv,
                        id,
                        displayName,
                        spec,
                        ownership,
                        packId,
                        productId,
                        entitlements,
                        canBeEarned,
                        bodyFits,
                        itemIndex,
                        presentation,
                        attachment,
                        paletteMask,
                        layerId,
                        effectId);
                }
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.ConfigureForEditor(definitions.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            catalog.ValidateOrThrow();

            foreach (var category in Enum.GetValues(typeof(CosmeticCategory))
                         .Cast<CosmeticCategory>())
            {
                CreateCategoryCollection(catalog, category);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CsvPath) ?? "docs/product");
            File.WriteAllText(CsvPath, csv.ToString(), new UTF8Encoding(false));
            WriteArtManifest();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ValidatePublished(catalog, definitions);
            Debug.Log(
                $"[JSS Task 27] Published {catalog.Items.Count} cosmetics, " +
                $"{Specs.Length} icon atlases and seven category collections.");
        }

        private static void ValidateInputs()
        {
            if (Specs.Length != 8 || Specs.Any(spec => spec.Slugs.Length != 16))
            {
                throw new InvalidOperationException(
                    "Task 27 requires eight exact sixteen-cell icon atlases.");
            }

            foreach (var spec in Specs)
            {
                var path = $"{IconRoot}/{spec.FileName}";
                var presentationPath =
                    $"{PresentationRoot}/{spec.PresentationFileName}";
                if (!File.Exists(path) || !File.Exists(presentationPath))
                {
                    throw new FileNotFoundException(
                        "Cosmetic icon or presentation atlas is missing.",
                        !File.Exists(path) ? path : presentationPath);
                }
                var size = GetPngSize(path);
                var presentationSize = GetPngSize(presentationPath);
                if (size.x != GridSize * CellSize || size.y != GridSize * CellSize)
                {
                    throw new InvalidOperationException(
                        $"Cosmetic icon atlas '{path}' must be exactly 1248x1248.");
                }
                if (presentationSize.x != GridSize * CellSize ||
                    presentationSize.y != GridSize * CellSize ||
                    !HasPngAlpha(presentationPath))
                {
                    throw new InvalidOperationException(
                        $"Cosmetic presentation atlas '{presentationPath}' must be " +
                        "exactly 1248x1248 RGBA.");
                }
            }
        }

        private static bool HasPngAlpha(string path)
        {
            var bytes = File.ReadAllBytes(path);
            return bytes.Length >= 26 && bytes[25] == 6;
        }

        private static Vector2Int GetPngSize(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 ||
                bytes[12] != 73 || bytes[13] != 72 || bytes[14] != 68 || bytes[15] != 82)
            {
                throw new InvalidOperationException($"'{path}' is not a canonical PNG.");
            }

            return new Vector2Int(
                ReadBigEndianInt(bytes, 16),
                ReadBigEndianInt(bytes, 20));
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) |
                (bytes[offset + 1] << 16) |
                (bytes[offset + 2] << 8) |
                bytes[offset + 3];
        }

        private static void ConfigureAtlas(AtlasSpec spec)
        {
            ConfigureAtlas(
                $"{IconRoot}/{spec.FileName}",
                spec,
                transparencyFromInput: false);
            ConfigureAtlas(
                $"{PresentationRoot}/{spec.PresentationFileName}",
                spec,
                transparencyFromInput: true);
        }

        private static void ConfigureAtlas(
            string path,
            AtlasSpec spec,
            bool transparencyFromInput)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter ??
                throw new InvalidOperationException($"'{path}' has no TextureImporter.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaSource = transparencyFromInput
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = transparencyFromInput;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;

#pragma warning disable 618
            var metadata = new SpriteMetaData[16];
            for (var index = 0; index < 16; index++)
            {
                var rowFromTop = index / GridSize;
                var column = index % GridSize;
                metadata[index] = new SpriteMetaData
                {
                    name = ItemId(spec.Category, spec.Slugs[index],
                        Array.IndexOf(Specs, spec), index),
                    rect = new Rect(
                        column * CellSize,
                        (GridSize - rowFromTop - 1) * CellSize,
                        CellSize,
                        CellSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                };
            }
            importer.spritesheet = metadata;
#pragma warning restore 618
            importer.SaveAndReimport();
        }

        private static void CreateCategoryCollection(
            CosmeticCatalog catalog,
            CosmeticCategory category)
        {
            var folder = $"{CollectionRoot}/{category}";
            EnsureFolder(folder);
            var path = $"{folder}/{category}Collection.asset";
            var collection =
                AssetDatabase.LoadAssetAtPath<CosmeticCategoryCollection>(path);
            if (collection == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
                collection = ScriptableObject.CreateInstance<CosmeticCategoryCollection>();
                AssetDatabase.CreateAsset(collection, path);
            }
            collection.ConfigureForEditor(
                category,
                catalog,
                catalog.Items
                    .Where(item => item.Category == category)
                    .Select(item => item.Id)
                    .ToArray());
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            collection.ValidateOrThrow();
        }

        private static void ValidatePublished(
            CosmeticCatalog catalog,
            IReadOnlyCollection<CosmeticDefinition> definitions)
        {
            catalog.ValidateOrThrow();
            if (catalog.Items.Count != 128 || definitions.Count != 128 ||
                catalog.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() !=
                128 ||
                catalog.Items.Count(item => item.CanBeEarned) < 64 ||
                catalog.Items.Count(item =>
                    item.OwnershipSource == CosmeticOwnershipSource.Edition &&
                    item.PackId == "explorer_edition") is < 25 or > 35 ||
                catalog.Items.Count(item =>
                    item.OwnershipSource == CosmeticOwnershipSource.Edition &&
                    item.PackId == "founders_constellation") is < 40 or > 50)
            {
                throw new InvalidOperationException(
                    "Published catalogue does not meet the launch count/edition contract.");
            }

            var birthday = catalog.Find("birthday.ori-starlight.2026");
            if (birthday == null ||
                birthday.OwnershipSource != CosmeticOwnershipSource.Birthday)
            {
                throw new InvalidOperationException(
                    "The Task 22 annual gift is missing from the finished catalogue.");
            }

            foreach (var item in catalog.Items.Where(item =>
                         item.Category == CosmeticCategory.Captain &&
                         item.SilhouetteChanging))
            {
                if (item.BodyFits.Distinct().Count() != 3 ||
                    !item.AttachmentAssetPath.Contains("{body-family-title}",
                        StringComparison.Ordinal) ||
                    !item.AttachmentAssetPath.Contains("{body-family-slug}",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Captain cosmetic '{item.Id}' does not publish all family fits.");
                }
            }
        }

        private static CosmeticOwnershipSource Ownership(int specIndex, int itemIndex)
        {
            if (specIndex == 2 && itemIndex == 7)
            {
                return CosmeticOwnershipSource.Birthday;
            }
            if (itemIndex == 0)
            {
                return CosmeticOwnershipSource.Free;
            }
            if (itemIndex <= 4)
            {
                return CosmeticOwnershipSource.Earned;
            }

            var explorerEnd = specIndex < 4 ? 8 : 7;
            if (itemIndex <= explorerEnd)
            {
                return CosmeticOwnershipSource.Edition;
            }

            var founderEnd = specIndex < 4 ? 12 : 13;
            if (itemIndex <= founderEnd)
            {
                return CosmeticOwnershipSource.Edition;
            }

            return CosmeticOwnershipSource.IndividualPurchase;
        }

        private static string PackId(
            int specIndex,
            int itemIndex,
            CosmeticOwnershipSource ownership)
        {
            if (ownership == CosmeticOwnershipSource.Free ||
                ownership == CosmeticOwnershipSource.Earned)
            {
                return "launch.earned";
            }
            if (ownership == CosmeticOwnershipSource.Birthday)
            {
                return "birthday.2026";
            }

            var explorerEnd = specIndex < 4 ? 8 : 7;
            if (ownership == CosmeticOwnershipSource.Edition && itemIndex <= explorerEnd)
            {
                return "explorer_edition";
            }
            if (ownership == CosmeticOwnershipSource.Edition)
            {
                return "founders_constellation";
            }

            return (specIndex % 3) switch
            {
                0 => "mirra_collection",
                1 => "koro_vesper_collection",
                _ => "aster_veil_collection",
            };
        }

        private static string[] Entitlements(
            CosmeticOwnershipSource ownership,
            string packId,
            string productId)
        {
            if (ownership == CosmeticOwnershipSource.Edition)
            {
                return new[] { packId, "complete_launch_collection" };
            }
            if (ownership == CosmeticOwnershipSource.IndividualPurchase)
            {
                return new[] { packId, productId, "complete_launch_collection" };
            }
            return Array.Empty<string>();
        }

        private static CosmeticRarity Rarity(CosmeticOwnershipSource ownership)
        {
            return ownership switch
            {
                CosmeticOwnershipSource.Free => CosmeticRarity.Field,
                CosmeticOwnershipSource.Earned => CosmeticRarity.Discovery,
                CosmeticOwnershipSource.Birthday => CosmeticRarity.Signal,
                CosmeticOwnershipSource.Edition => CosmeticRarity.Constellation,
                CosmeticOwnershipSource.IndividualPurchase => CosmeticRarity.Signal,
                _ => throw new ArgumentOutOfRangeException(nameof(ownership)),
            };
        }

        private static string ItemId(
            CosmeticCategory category,
            string slug,
            int specIndex,
            int itemIndex)
        {
            if (specIndex == 2 && itemIndex == 7)
            {
                return "birthday.ori-starlight.2026";
            }
            return $"cosmetic.{CategorySlug(category)}.{slug}";
        }

        private static string CategorySlug(CosmeticCategory category) =>
            category.ToString().ToLowerInvariant();

        private static string DisplayName(string slug)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                slug.Replace('-', ' '));
        }

        private static string LayerId(int specIndex, int itemIndex)
        {
            if (specIndex == 0)
            {
                return "silhouette-costume";
            }
            if (specIndex == 1)
            {
                if (itemIndex <= 3 || itemIndex >= 12)
                {
                    return "head-hair";
                }
                if (itemIndex <= 7)
                {
                    return itemIndex <= 5 ? "foreground-hand-tool" : "silhouette-costume";
                }
                return itemIndex <= 9
                    ? "backpack-equipment"
                    : itemIndex <= 11
                        ? "silhouette-costume"
                        : "head-hair";
            }
            return Specs[specIndex].Category switch
            {
                CosmeticCategory.Ori => "ori-shell-effect",
                CosmeticCategory.Ship => "ship-hull-effect",
                CosmeticCategory.Lens => "lens-body-hologram",
                CosmeticCategory.Clubhouse => "clubhouse-prop",
                CosmeticCategory.Photo => "photo-frame-filter-pose",
                CosmeticCategory.Crew => "crew-fullbody-outfit",
                _ => "presentation",
            };
        }

        private static string AttachmentPath(
            int specIndex,
            int itemIndex,
            string layerId)
        {
            if (specIndex == 0)
            {
                return "Assets/_JustSomeStars/Art/2D/Characters/Captain/Customization/" +
                    "Modules/{body-family-title}/{facing}/captain-{body-family-slug}-" +
                    "{facing}-suitComponents.png";
            }
            if (specIndex == 1)
            {
                var module = itemIndex switch
                {
                    <= 3 => "helmets",
                    <= 5 => "gloves",
                    <= 7 => "boots",
                    <= 9 => "backpacks",
                    <= 11 => "patches",
                    _ => "accessories",
                };
                return "Assets/_JustSomeStars/Art/2D/Characters/Captain/Customization/" +
                    $"Modules/{{body-family-title}}/{{facing}}/captain-" +
                    $"{{body-family-slug}}-{{facing}}-{module}.png";
            }

            var category = Specs[specIndex].Category;
            return category switch
            {
                CosmeticCategory.Ori =>
                    "Assets/_JustSomeStars/Art/2D/Characters/Ori/Atlases/{facing}/ori-{facing}.png",
                CosmeticCategory.Ship =>
                    "Assets/_JustSomeStars/Art/2D/Ship/PlayerShip/PlayerShipMaster.png",
                CosmeticCategory.Lens =>
                    "Assets/_JustSomeStars/Art/2D/Environments/Mirra/Hud/MirraLensButton.png",
                CosmeticCategory.Clubhouse =>
                    "Assets/_JustSomeStars/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerTableForegroundV4.png",
                CosmeticCategory.Photo => $"{IconRoot}/Photo.png",
                CosmeticCategory.Crew =>
                    "Assets/_JustSomeStars/Art/2D/Characters/{crew}/Atlases/{facing}/{crew}-{facing}.png",
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };
        }

        private static string PaletteMaskPath(
            CosmeticCategory category,
            string layerId)
        {
            return category == CosmeticCategory.Captain
                ? "Assets/_JustSomeStars/Art/2D/Characters/Captain/Customization/" +
                  $"PaletteMasks/{{body-family-title}}/{{facing}}/captain-{{body-family-slug}}-" +
                  $"{{facing}}-{layerId}-palette-mask.png"
                : string.Empty;
        }

        private static string[] Clips(CosmeticCategory category)
        {
            return category switch
            {
                CosmeticCategory.Captain => ActorClips,
                CosmeticCategory.Ori => ActorClips,
                CosmeticCategory.Crew => ActorClips,
                CosmeticCategory.Ship => new[] { "flight", "landing", "idle" },
                CosmeticCategory.Lens => new[] { "idle", "scan", "success" },
                CosmeticCategory.Clubhouse => new[] { "idle", "celebration" },
                CosmeticCategory.Photo => new[] { "capture", "pose" },
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };
        }

        private static string[] FrameEvents(CosmeticCategory category)
        {
            return category switch
            {
                CosmeticCategory.Captain => ActorEvents,
                CosmeticCategory.Ori => ActorEvents,
                CosmeticCategory.Crew => ActorEvents,
                CosmeticCategory.Ship => new[] { "EnginePulse", "LandingContact", "Trail" },
                CosmeticCategory.Lens => new[] { "ScanPulse", "Hologram", "Audio" },
                CosmeticCategory.Clubhouse => new[] { "CelebrationCue" },
                CosmeticCategory.Photo => new[] { "CaptureFlash", "PoseReady" },
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };
        }

        private static void AppendCsv(
            StringBuilder csv,
            string id,
            string displayName,
            AtlasSpec spec,
            CosmeticOwnershipSource ownership,
            string packId,
            string productId,
            IReadOnlyList<string> entitlements,
            bool canBeEarned,
            IReadOnlyList<CaptainBodyFamily> bodyFits,
            int itemIndex,
            string presentation,
            string attachment,
            string mask,
            string layerId,
            string effectId)
        {
            var values = new[]
            {
                id,
                displayName,
                spec.Category.ToString(),
                ownership.ToString(),
                packId,
                productId,
                string.Join("|", entitlements),
                canBeEarned ? "true" : "false",
                string.Join("|", bodyFits),
                $"{IconRoot}/{spec.FileName}",
                itemIndex.ToString(CultureInfo.InvariantCulture),
                presentation,
                attachment,
                mask,
                layerId,
                effectId,
            };
            csv.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        private static void WriteArtManifest()
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"schemaVersion\": 1,");
            json.AppendLine("  \"grid\": { \"columns\": 4, \"rows\": 4, \"cellPixels\": 312 },");
            json.AppendLine("  \"backgroundExtraction\": \"ImageGen solid-key edit -> deterministic RGBA chroma extraction\",");
            json.AppendLine("  \"atlases\": [");
            for (var index = 0; index < Specs.Length; index++)
            {
                var spec = Specs[index];
                var path = $"{PresentationRoot}/{spec.PresentationFileName}";
                var hash = Sha256(path);
                json.Append("    { \"category\": \"")
                    .Append(spec.Category)
                    .Append("\", \"path\": \"")
                    .Append(path)
                    .Append("\", \"sha256\": \"")
                    .Append(hash)
                    .Append("\", \"sprites\": 16 }");
                json.AppendLine(index + 1 == Specs.Length ? string.Empty : ",");
            }
            json.AppendLine("  ]");
            json.AppendLine("}");
            File.WriteAllText(ArtManifestPath, json.ToString(), new UTF8Encoding(false));
        }

        private static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(File.ReadAllBytes(path))
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string EscapeCsv(string value)
        {
            var safe = value ?? string.Empty;
            return safe.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? $"\"{safe.Replace("\"", "\"\"")}\""
                : safe;
        }

        private static void EnsureFolder(string path)
        {
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                var current = "Assets";
                foreach (var segment in path.Substring("Assets/".Length).Split('/'))
                {
                    var next = $"{current}/{segment}";
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, segment);
                    }
                    current = next;
                }
                return;
            }

            Directory.CreateDirectory(path);
        }

        private sealed class AtlasSpec
        {
            public AtlasSpec(
                string fileName,
                string presentationFileName,
                CosmeticCategory category,
                string[] slugs)
            {
                FileName = fileName;
                PresentationFileName = presentationFileName;
                Category = category;
                Slugs = slugs;
            }

            public string FileName { get; }
            public string PresentationFileName { get; }
            public CosmeticCategory Category { get; }
            public string[] Slugs { get; }
        }
    }
}
