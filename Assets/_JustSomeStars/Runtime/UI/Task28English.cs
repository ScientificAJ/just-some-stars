using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Atlas;

namespace JustSomeStars.Runtime.UI
{
    public static class Task28English
    {
        public const string FrontendTitle = "frontend.title";
        public const string FrontendStatus = "frontend.status";
        public const string FrontendVersion = "frontend.version";
        public const string FrontendNewGame = "frontend.newGame";
        public const string FrontendNewGameReady = "frontend.newGame.ready";
        public const string FrontendNewGameLoading = "frontend.newGame.loading";
        public const string FrontendContinue = "frontend.continue";
        public const string FrontendContinueNoSave = "frontend.continue.noSave";
        public const string FrontendContinueReady = "frontend.continue.ready";
        public const string FrontendContinueRecovered = "frontend.continue.recovered";
        public const string FrontendContinueUnreadable = "frontend.continue.unreadable";
        public const string FrontendContinueStorageUnavailable =
            "frontend.continue.storageUnavailable";
        public const string FrontendContinueContentUnavailable =
            "frontend.continue.contentUnavailable";
        public const string FrontendContinueLoading = "frontend.continue.loading";
        public const string FrontendCheckpointFallback =
            "frontend.checkpointFallback";
        public const string SettingsTitle = "settings.title";
        public const string SettingsBody = "settings.body";
        public const string CreditsTitle = "credits.title";
        public const string CreditsWrapper = "credits.wrapper";
        public const string CreditsApacheWrapper = "credits.apacheWrapper";
        public const string PrivacyTitle = "privacy.title";
        public const string PrivacyBody = "privacy.body";
        public const string Close = "common.close";
        public const string LocalPanelNote = "common.localPanelNote";
        public const string On = "common.on";
        public const string Off = "common.off";
        public const string Reduced = "common.reduced";
        public const string Full = "common.full";
        public const string Left = "common.left";
        public const string Right = "common.right";
        public const string NotAvailable = "common.notAvailable";
        public const string PhotoTitle = "photo.title";
        public const string PhotoExplorer = "photo.explorer";
        public const string CreditsButton = "credits.button";

        private static readonly LocalizedEnglishText[] Entries =
        {
            Entry(FrontendTitle, "Just Some Stars"),
            Entry(FrontendStatus, "CHAPTER ONE"),
            Entry(FrontendVersion, "Version {0}"),
            Entry(FrontendNewGame, "New Game"),
            Entry(FrontendNewGameReady, "Begin at the observatory"),
            Entry(FrontendNewGameLoading, "Opening the observatory…"),
            Entry(FrontendContinue, "Continue"),
            Entry(FrontendContinueNoSave, "No journey saved yet"),
            Entry(FrontendContinueReady, "Return to {0}"),
            Entry(FrontendContinueRecovered, "Recovered backup · Return to {0}"),
            Entry(FrontendContinueUnreadable, "Save needs recovery before continuing"),
            Entry(FrontendContinueStorageUnavailable, "Local saves are temporarily unavailable"),
            Entry(FrontendContinueContentUnavailable, "That checkpoint is not installed"),
            Entry(FrontendContinueLoading, "Returning to your crew…"),
            Entry(FrontendCheckpointFallback, "your last checkpoint"),
            Entry("frontend.state.ready", "READY"),
            Entry("frontend.state.recovered", "RECOVERED"),
            Entry("frontend.state.recovery", "CHECK SAVE"),
            Entry("frontend.state.offline", "LOCAL OFFLINE"),
            Entry("frontend.state.unavailable", "NOT INSTALLED"),
            Entry("frontend.state.new", "NEW"),
            Entry(SettingsTitle, "Settings"),
            Entry(SettingsBody,
                "Device settings are saved locally and are never included in cloud backup."),
            Entry(CreditsTitle, "Credits & Licenses"),
            Entry(CreditsButton, "Credits"),
            Entry(CreditsWrapper,
                "Just Some Stars is created by ScientificAJ.\n\n" +
                "Liberation Sans · Copyright 2010 Google Corporation and " +
                "2012 Red Hat, Inc.\n" +
                "Noto Sans · Copyright 2010, 2012–2020 Google Inc. and " +
                "2015–2020 Google LLC.\n" +
                "Both fonts are distributed under the SIL Open Font License " +
                "1.1. The complete license follows.\n\n"),
            Entry(CreditsApacheWrapper,
                "\n\nAndroid open-source components\n\n" +
                "This Android build includes AndroidX, Kotlin, Kotlin coroutines, " +
                "JetBrains annotations, and Guava components distributed under " +
                "the Apache License 2.0. The complete license follows.\n\n" +
                "Apache License 2.0\n\n"),
            Entry(PrivacyTitle, "Privacy"),
            Entry(PrivacyBody,
                "An account is optional. Progress stays on this device unless a " +
                "grown-up chooses private Google cloud backup. Photos and device " +
                "settings always stay local. Cloud data can be exported, signed " +
                "out, or deleted from Settings. Google sign-in data is never used " +
                "for advertising. Optional store purchases never sell story, " +
                "science, or accessibility."),
            Entry(Close, "Close"),
            Entry(LocalPanelNote, "LOCAL NOTE // NOTHING LEAVES THIS SCREEN"),
            Entry(On, "On"),
            Entry(Off, "Off"),
            Entry(Reduced, "Reduced"),
            Entry(Full, "Full"),
            Entry(Left, "Left"),
            Entry(Right, "Right"),
            Entry(NotAvailable, "Not available"),
            Entry("common.confirm", "Confirm"),
            Entry("common.percent", "{0}%"),
            Entry("common.multiplier", "{0:0.00}x"),
            Entry("settings.pilotingAssist", "Piloting assist"),
            Entry("settings.explorationAssist", "Exploration assist"),
            Entry("settings.scienceDepth", "Science detail"),
            Entry("settings.captions", "Captions and speaker labels"),
            Entry("settings.textScale", "Text size"),
            Entry("settings.readableType", "Readable type"),
            Entry("settings.dialogueSpeed", "Dialogue speed"),
            Entry("settings.colorVision", "Color-safe symbols"),
            Entry("settings.cameraShake", "Camera shake"),
            Entry("settings.flashing", "Flashing"),
            Entry("settings.motion", "Interface motion"),
            Entry("settings.motionBlur", "Motion blur"),
            Entry("settings.particles", "Particle density"),
            Entry("settings.quality", "Presentation quality"),
            Entry("settings.music", "Music"),
            Entry("settings.dialogue", "Dialogue"),
            Entry("settings.effects", "Effects"),
            Entry("settings.haptics", "Haptics"),
            Entry("settings.controlSide", "Control side"),
            Entry("settings.touchSensitivity", "Touch sensitivity"),
            Entry("value.guided", "Guided"),
            Entry("value.balanced", "Balanced"),
            Entry("value.ace", "Ace"),
            Entry("value.deep", "Deep"),
            Entry("value.standard", "Standard"),
            Entry("value.protanopia", "Protanopia"),
            Entry("value.deuteranopia", "Deuteranopia"),
            Entry("value.tritanopia", "Tritanopia"),
            Entry("value.performance", "Performance"),
            Entry("value.cinematic", "Cinematic"),
            Entry("value.highFrameRate", "High frame rate"),
            Entry("account.unavailable",
                "Google backup isn’t available in this build. Offline progress still works."),
            Entry("account.available", "Private Google backup is available."),
            Entry("account.linked", "Private backup is linked and ready."),
            Entry("account.pending", "Backup will finish when the connection returns."),
            Entry("account.conflict", "Choose which complete checkpoint to keep."),
            Entry("account.offline", "Playing offline. Progress stays on this device."),
            Entry("account.link", "Back up with Google"),
            Entry("account.linkCancelled",
                "Cloud linking stayed closed. No account was changed."),
            Entry("account.grownUpArithmetic",
                "Grown-up check for private backup: what is {0} + {1}?"),
            Entry("account.grownUpConfirm",
                "A grown-up must confirm private Google cloud backup."),
            Entry("account.sync", "Sync backup"),
            Entry("account.export", "Export my data"),
            Entry("account.signOut", "Sign out"),
            Entry("account.unlink", "Unlink Google"),
            Entry("account.delete", "Delete cloud account"),
            Entry("account.confirmDelete", "Confirm delete"),
            Entry("account.useDevice", "Use this device"),
            Entry("account.useBackup", "Use cloud backup"),
            Entry("account.busy", "Private backup is working: {0}"),
            Entry("account.operation.linking", "linking"),
            Entry("account.operation.syncing", "syncing"),
            Entry("account.operation.resolvingConflict", "resolving a conflict"),
            Entry("account.operation.exporting", "exporting"),
            Entry("account.operation.signingOut", "signing out"),
            Entry("account.operation.unlinking", "unlinking"),
            Entry("account.operation.deleting", "deleting"),
            Entry("hud.pause", "Pause"),
            Entry("hud.accessibility", "Accessibility"),
            Entry("hud.photoMode", "Photo Mode"),
            Entry("hud.interact", "Interact"),
            Entry("hud.signalLinked", "Signal linked"),
            Entry("hud.lens", "Discovery Lens"),
            Entry("subtitle.speaker", "{0}: {1}"),
            Entry("menu.resume", "Resume"),
            Entry("menu.journey", "Journey"),
            Entry("menu.atlas", "Atlas"),
            Entry("menu.customization", "Captain"),
            Entry("menu.shop", "Optional extras"),
            Entry("menu.account", "Private backup"),
            Entry("menu.grownUp", "Grown-up check"),
            Entry("menu.context", "PAUSED // THE JOURNEY WAITS FOR YOU"),
            Entry("menu.journey.noSave", "No local journey is available."),
            Entry("menu.journey.detail", "Next: {0}\nCheckpoint {1}"),
            Entry("menu.accessibility.detail",
                "Accessibility changes apply immediately to every player surface."),
            Entry("menu.accessibility.applied", "Accessibility updated."),
            Entry("menu.accessibility.previous", "Previous setting"),
            Entry("menu.accessibility.next", "Next setting"),
            Entry("menu.accessibility.decrease", "Decrease"),
            Entry("menu.accessibility.increase", "Increase"),
            Entry("menu.atlas.none",
                "No Atlas entries have been discovered yet. Use the Discovery Lens in the field."),
            Entry("menu.atlas.entry", "{0}\n{1}\n{2} detail · {3}/{4}"),
            Entry("menu.atlas.previous", "Previous entry"),
            Entry("menu.atlas.next", "Next entry"),
            Entry("menu.atlas.depth", "Science detail"),
            Entry("atlas.mirra.title", "Mirra · Twilight climate"),
            Entry("atlas.koro.title", "Koro · Geyser spectra"),
            Entry("atlas.mirra.temperature.short",
                "Mirra has a permanent hot day side and a cold night side."),
            Entry("atlas.mirra.temperature.balanced",
                "Mirra keeps one face toward its star, while its atmosphere moves heat from permanent day toward permanent night."),
            Entry("atlas.mirra.temperature.deep",
                "On a tidally locked world, atmospheric circulation can reduce the temperature contrast between the permanent day and night hemispheres. Mirra's thermal gradient is evidence of that continuing heat transport."),
            Entry("koro.atlas.guided",
                "Both plumes may carry water-related ultraviolet signatures."),
            Entry("koro.atlas.balanced",
                "Both plumes may contain water-related material, but the violet plume repeats one stronger line. That difference is evidence of the Signal pattern, not proof of life or a direct ocean source."),
            Entry("koro.atlas.deep",
                "The spectrometer compares authored false-color ultraviolet samples at 121.6, 130.4 and 135.6 nm. Their shared pattern may be consistent with water-related material; the enhanced 135.6 nm intensity in the violet plume marks a repeating fictional Signal component. Tidal flexing can provide energy and Europa-like worlds may hide oceans beneath ice, but these observations alone do not prove life, habitability, or direct exchange with a global ocean."),
            Entry("menu.customization.noSave", "Start a journey before changing your Captain."),
            Entry("menu.customization.detail",
                "Body: {0}\nLook: {1}\nSuit: {2}\nOwned item: {3}"),
            Entry("menu.customization.clubhouseOnly",
                "Captain changes are saved safely at the Clubhouse."),
            Entry("menu.customization.saved", "Saved {0} Captain."),
            Entry("menu.customization.noOwned", "No Captain items are owned yet."),
            Entry("menu.customization.body", "Body family"),
            Entry("menu.customization.appearance", "Face and colors"),
            Entry("menu.customization.suit", "Field suit"),
            Entry("menu.customization.cosmetic", "Owned item"),
            Entry("menu.operationFailed", "That action did not finish. Your progress is safe."),
            Entry("location.clubhouse", "the Clubhouse"),
            Entry("location.opening", "the observatory"),
            Entry("location.mirra", "Mirra"),
            Entry("location.koro", "Koro"),
            Entry("location.vesper", "Vesper flight"),
            Entry("location.aster", "Aster Veil"),
            Entry("captain.family.compact", "Compact"),
            Entry("captain.family.average", "Average"),
            Entry("captain.family.tallBroad", "Tall Broad"),
            Entry("captain.family.custom", "Custom"),
            Entry("captain.appearance", "Crew look {0}"),
            Entry("captain.suit.clubhouse", "Clubhouse canvas"),
            Entry("captain.suit.signal", "Signal field suit"),
            Entry("captain.suit.flight", "Flight suit"),
            Entry("shop.previous", "Previous item"),
            Entry("shop.next", "Next item"),
            Entry("shop.purchase", "Choose item"),
            Entry("shop.restore", "Restore purchases"),
            Entry("shop.available", "{0} optional cosmetic items\n{1} store products available"),
            Entry("shop.unavailable",
                "Optional purchases are unavailable. The complete story remains playable."),
            Entry("shop.unavailable.detail",
                "{0} cosmetic items are included in the catalogue. Purchases are unavailable in this build."),
            Entry("shop.grownUpRequired",
                "A grown-up must confirm before the store opens. Nothing was charged."),
            Entry("shop.grownUpArithmetic", "Grown-up check: what is {0} + {1}?"),
            Entry("shop.grownUpConfirm", "A grown-up must confirm this optional purchase action."),
            Entry("shop.answerDown", "Answer down"),
            Entry("shop.answerUp", "Answer up"),
            Entry("shop.confirm", "Confirm"),
            Entry("shop.cancel", "Cancel"),
            Entry("shop.loading", "Checking optional cosmetic items…\nThe complete story remains free."),
            Entry("shop.product", "{0}\n{1}\n{2} · Item {3}/{4}\nThe complete story remains free."),
            Entry("shop.purchaseFinished", "The store finished checking this item."),
            Entry("shop.restoreComplete",
                "Restore finished. Verified ownership is now shown on this profile."),
            Entry("birthday.title", "Birthday gift"),
            Entry("birthday.private", "Your birthday stays private."),
            Entry("birthday.notSet",
                "No birthday is saved. It is optional and always private."),
            Entry("birthday.set",
                "Private birthday: {0}/{1}/{2}\nCorrections used: {3}"),
            Entry("birthday.day", "Day"),
            Entry("birthday.month", "Month"),
            Entry("birthday.year", "Year"),
            Entry("birthday.save", "Save privately"),
            Entry("birthday.confirmCorrection", "Grown-up confirm"),
            Entry("birthday.grownUpRequired",
                "A grown-up must confirm a birthday correction. Nothing is shared."),
            Entry("birthday.saved",
                "Birthday saved privately on this device."),
            Entry("birthday.unavailable",
                "Start a local journey before saving a private birthday."),
            Entry("chapter.opening.title", "THE CLUBHOUSE · BEFORE DINNER"),
            Entry("chapter.opening.copy",
                "Mira · Juno · Kai · Bea · Ori\nWe’re going exploring! We’ll be back before dinner!\nPermission granted. Antenna awake. Signal received."),
            Entry("chapter.opening.beat",
                "Permission granted. Helmets sealed.\nOri wakes the antenna—the first Signal pulse answers."),
            Entry("chapter.reassembly.title", "THREE FRAGMENTS · ONE SIGNAL"),
            Entry("chapter.reassembly.copy",
                "The crew trusts your route. Rebuild the Signal.\nSTAR MAP: BEYOND AURELIA · RECENT PULSE CONFIRMED"),
            Entry("chapter.reassembly.beat",
                "MIRRA · KORO · ASTER\nThree fragments align. A route beyond Aurelia appears."),
            Entry("chapter.clubhouse.safe.title", "THE CLUBHOUSE · SAFE HARBOR"),
            Entry("chapter.clubhouse.safe.copy",
                "Rest, review the star map, or continue when the crew is ready."),
            Entry("chapter.clubhouse.return.title", "CLUBHOUSE · CRASH RETURN"),
            Entry("chapter.clubhouse.return.copy",
                "THE SCOUT SKIDS INTO THE CLUBHOUSE.\nEveryone is safe. Grab the fragment—race home."),
            Entry("chapter.clubhouse.birthday",
                "\nOri left a birthday delivery and handmade stars."),
            Entry("chapter.clubhouse.beat1",
                "IMPACT ABSORBED · HULL SAFE\nThe hatch opens. Everyone runs the fragment home."),
            Entry("chapter.clubhouse.beat2",
                "Across the ridge before the last light—home before dinner."),
            Entry("chapter.dinner.title", "HOME BEFORE DINNER"),
            Entry("chapter.dinner.question", "So, did you discover anything?"),
            Entry("chapter.dinner.answer", "Just some stars."),
            Entry("chapter.dinner.copy",
                "So, did you discover anything?\nJust some stars.\nORI EYE FLICKER · POCKET FRAGMENT PULSE\nCHAPTER TWO · SIGNAL BEYOND AURELIA\nCREDITS"),
            Entry("chapter.dinner.beat2",
                "Just some stars.\nOri’s eye flickers. The pocket fragment answers once."),
            Entry("aster.trust.pick",
                "MIRA · JUNO · KAI · BEA\nYOUR CALL, CAPTAIN. PICK THE LINE."),
            Entry("aster.trust.withYou",
                "MIRA · JUNO · KAI · BEA\nYOUR CALL, CAPTAIN. WE’RE WITH YOU."),
            Entry("aster.fragment.safeLane",
                "FLY TO THE THIRD FRAGMENT · HOLD THE SAFE LANE"),
            Entry("aster.escape.openLine",
                "ESCAPE LEFT ON THE OPEN MOMENTUM LINE"),
            Entry("aster.objective.0", "ASTER VEIL · READ THE SHIFTING LANES"),
            Entry("aster.objective.1",
                "THE CREW TRUSTS YOU · CHOOSE THE GRAVITY LINE"),
            Entry("aster.objective.2",
                "TRACK RELATIVE MOTION · NOT ABSOLUTE SPEED"),
            Entry("aster.objective.3", "THREAD THE SHATTERED-MOON DEBRIS"),
            Entry("aster.objective.4", "RECOVER THE THIRD SIGNAL FRAGMENT"),
            Entry("aster.objective.5",
                "REASSEMBLE ALL THREE SIGNAL FRAGMENTS"),
            Entry("aster.objective.6", "ESCAPE ON THE MOMENTUM LINE"),
            Entry("aster.objective.complete", "ASTER VEIL COMPLETE"),
            Entry("koro.objective.complete",
                "CHAPTER COMPLETE · SECOND SIGNAL RECOVERED"),
            Entry("koro.objective.landed", "LAND ON KORO"),
            Entry("koro.objective.traversal",
                "CROSS THE LOW-GRAVITY SHELVES"),
            Entry("koro.objective.spectra", "COMPARE BOTH GEYSER SPECTRA"),
            Entry("koro.objective.rhythm", "FOLLOW THE REPEATING RHYTHM"),
            Entry("koro.objective.fragment", "RECOVER THE SECOND FRAGMENT"),
            Entry("koro.objective.default", "SECOND SIGNAL · KORO"),
            Entry(PhotoTitle, "Photo Mode"),
            Entry("photo.pan", "Pan"),
            Entry("photo.zoom", "Zoom"),
            Entry("photo.depth", "Depth focus"),
            Entry("photo.exposure", "Exposure"),
            Entry("photo.cleanHud", "Clean view"),
            Entry("photo.frame", "Earned frame"),
            Entry("photo.capture", "Capture"),
            Entry(PhotoExplorer, "Explorer tools"),
            Entry("photo.lens", "Cinematic lens"),
            Entry("photo.pose", "Expanded pose"),
            Entry("photo.preset", "Save preset"),
            Entry("photo.loadPreset", "Load preset"),
        };

        public static IReadOnlyList<string> RequiredKeys
        {
            get
            {
                var keys = new string[Entries.Length];
                for (var index = 0; index < Entries.Length; index++)
                {
                    keys[index] = Entries[index].Key;
                }
                return keys;
            }
        }

        public static LocalizedEnglishText[] CreateEntries()
        {
            var copy = new LocalizedEnglishText[Entries.Length];
            for (var index = 0; index < Entries.Length; index++)
            {
                copy[index] = new LocalizedEnglishText(
                    Entries[index].Key,
                    Entries[index].Value);
            }
            return copy;
        }

        public static string Format(
            LocalizedEnglishCatalog catalog,
            string key,
            params object[] arguments)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                catalog.Resolve(key),
                arguments ?? Array.Empty<object>());
        }

        public static string ResolveDefault(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "A localization key is required.",
                    nameof(key));
            }
            for (var index = 0; index < Entries.Length; index++)
            {
                if (string.Equals(
                        Entries[index].Key,
                        key,
                        StringComparison.Ordinal))
                {
                    return Entries[index].Value;
                }
            }
            throw new KeyNotFoundException(
                $"Task 28 English key '{key}' is not authored.");
        }

        private static LocalizedEnglishText Entry(string key, string value) =>
            new LocalizedEnglishText(key, value);
    }
}
