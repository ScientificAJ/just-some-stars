# Just Some Stars — Technical Build Plan

**Status:** Approved and design-locked  
**Approved:** 2026-08-21  
**Date:** 2026-08-21  
**Owner:** ScientificAJ  
**Primary release:** Android mobile  
**Stores:** Google Play and Samsung Galaxy Store  
**Future platform:** Steam; iOS only if deliberately added later  
**Engine:** Unity 6 LTS, Universal Render Pipeline  
**Repository:** [ScientificAJ/just-some-stars](https://github.com/ScientificAJ/just-some-stars)  
**Narrative blueprint:** [astronomy-adventure-game-blueprint.md](./astronomy-adventure-game-blueprint.md)  
**Approved visual target:** [just-some-stars-mirra-gameplay-target-v1.png](./just-some-stars-mirra-gameplay-target-v1.png)

## 1. Purpose and precedence

This document converts the approved game blueprint into the controlling technical architecture and production sequence for the Shipaton release. The narrative blueprint remains authoritative for story, tone, learning, crew, locations, and play feel. This document is authoritative for implementation, accounts, cloud saving, birthdays, monetization, store variants, production tooling, testing, and the ShipKit activation sequence.

The later decisions recorded here supersede four earlier blueprint assumptions:

- Accounts remain optional, but Google-backed cloud accounts are supported.
- The player's exact birthday may be privately stored for age handling and annual birthday gifts.
- Monetization includes a large cosmetics catalogue, premium bundles, Explorer Edition, and future paid chapters.
- Platform services are activated progressively as their project, store, or revenue prerequisites become available.

Chapter One remains a complete, free 45–60 minute story from the clubhouse opening through the return before dinner.

## Approved visual quality bar

![Approved Just Some Stars Mirra gameplay quality bar](./just-some-stars-mirra-gameplay-target-v1.png)

The image above is the canonical visual quality bar for the playable game, not merely concept art or promotional inspiration. Character production, environments, lighting, materials, VFX, animation, camera composition and mobile UI are judged against the level of cohesion and finish demonstrated by this frame.

The release must preserve these qualities:

- A cinematic stylized-3D presentation with believable materials and physically readable lighting.
- A powerful warm-sunset versus frozen-night color divide that communicates Mirra's tidal locking through the scene itself.
- Clear child, Ori and ship silhouettes at mobile screen size.
- Dense environmental detail organized around readable traversal rather than visual noise.
- A handmade, child-built ship contrasted with precise Signal technology.
- Atmospheric depth, grounded contact shadows, controlled reflections and restrained bloom.
- Expressive, original characters with appealing proportions and production-quality clothing, hair and equipment.
- A polished touch HUD whose controls remain legible without dominating the view.
- A strong objective focal point and composition that creates awe while clearly showing where the player should travel.

The final game does not need to reproduce this exact frame pixel-for-pixel. It must match its perceived quality, art-direction coherence, readability, emotional impact and density on supported mobile hardware. Mirra is the first measurable benchmark; Koro/Vesper and Aster Veil must reach the same quality in their own approved palettes.

Quality reviews compare representative device screenshots directly against this image at the Mirra benchmark, content-lock and release-candidate gates. A scene is not considered final merely because its mechanics work.

## 2. Product and release constraints

The first release must provide:

- A complete opening-to-dinner Chapter One.
- Third-person surface exploration and arcade-accessible spaceflight.
- The Discovery Lens, scientific prediction, evidence collection, and Cosmic Atlas.
- A customizable Captain with three properly fitted body families.
- Five authored kid crew members and Ori, with two active companions plus Ori at full intelligence.
- Guided, Balanced, and Ace gameplay assistance independent of science depth.
- Offline guest play, optional Google cloud backup, and versioned recoverable saves.
- A RevenueCat-powered real in-app purchase.
- Google Play and Galaxy Store build variants.
- Stable 30 FPS on the Realme Narzo performance profile, with higher settings on capable devices.
- Family-safe purchases and data handling.

The game will not launch with advertisements, subscriptions, premium currency, loot boxes, energy timers, paid power, public chat, multiplayer, or an always-online requirement.

## 3. Languages and primary tools

| Technology | Role |
|---|---|
| C# | Unity runtime, editor tooling, gameplay, crew intelligence, missions, saves, UI, services and tests |
| Shader Graph | Shared skin, fabric, metal, ice, rock, atmosphere, visor, vegetation, hologram and Signal shaders |
| HLSL | Only effects that Shader Graph cannot express efficiently |
| Blender | Original characters, Ori, ship, signature props, environment kits, rigging, skinning and LODs |
| Python | Blender automation, asset validation, batch export and content-processing tools; never runtime gameplay |
| JSON | Versioned cloud/local serialization, service configuration and external content interchange |
| Unity ScriptableObjects | Missions, crew personalities, dialogue, phenomena, cosmetics, instruments, Atlas and tuning data |
| Kotlin/Java | Android bridge code for Galaxy billing, Google identity and narrowly scoped native services |
| TypeScript | Firebase Cloud Functions for secure account maintenance and annual birthday-gift grants |
| Gradle | Android libraries, manifests, dependencies, packaging and store-specific builds |
| YAML and shell | Codemagic workflows and reproducible command-line automation |

No general-purpose web backend, live generative character dialogue, or unnecessary JavaScript application layer is introduced.

## 4. Command-line and agent-assisted production

### 4.1 Unity CLI is a first-class build interface

The Unity Editor is used interactively, but every test and release operation must also be callable through Unity's command line. A dedicated editor entry point, `JustSomeStars.Editor.Build.BuildCli`, will expose deterministic build methods.

Canonical command shapes:

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -buildTarget Android \
  -projectPath "$JSS_PROJECT_PATH" \
  -runTests -testPlatform editmode \
  -assemblyNames JustSomeStars.EditModeTests \
  -testResults "$JSS_ARTIFACTS/editmode-results.xml"

python3 tools/qa/playmode_suite.py \
  --unity-editor "$JSS_UNITY_EDITOR" \
  --project-path "$JSS_PROJECT_PATH" \
  --output-directory "$JSS_ARTIFACTS/playmode-suite" \
  --log-directory "$JSS_ARTIFACTS/playmode-suite-logs"

"$JSS_UNITY_EDITOR" -batchmode -nographics -quit \
  -projectPath "$JSS_PROJECT_PATH" \
  -executeMethod JustSomeStars.Editor.Build.BuildCli.BuildGooglePlayRelease

"$JSS_UNITY_EDITOR" -batchmode -nographics -quit \
  -projectPath "$JSS_PROJECT_PATH" \
  -executeMethod JustSomeStars.Editor.Build.BuildCli.BuildGalaxyRelease
```

The CLI owns build-number injection, scripting symbols, package selection, keystore selection, Addressables building, artifact naming and failure exit codes. Codemagic calls the same entry points used locally so CI cannot silently build a different product.

Unity Test Framework 1.6 full-assembly PlayMode aggregation is not a release
gate: it can report complete discovery while recording zero tests. The
repository runner validates its fixture manifest against source, launches one
Unity process per fixture, rejects missing/malformed/empty NUnit results and
publishes a consolidated summary. Focused single-fixture Unity invocations
remain valid. Test invocations omit `-quit`; build entry points retain it.

### 4.2 Blender MCP is part of the asset pipeline

Blender MCP will be used for repetitive and scriptable Blender work:

- Scene setup, naming and collection organization.
- Reference-image plane placement.
- Blockout assistance after a reference sheet is approved.
- Procedural materials, geometry helpers and environment-kit generation.
- Rig inspection, bone naming, weight diagnostics and animation cleanup.
- LOD generation assistance and mesh validation.
- Batch pivot, scale, orientation and FBX export preparation.
- Python execution inside Blender for project-specific tooling.

It does not replace artistic approval. No hero character mesh begins before its reference sheet is approved, and generated geometry is treated as editable source rather than final art.

Current Blender MCP configuration:

- Port: `9876`
- Poly Haven: enabled
- Hyper3D Rodin: disabled because no account is available
- Sketchfab: disabled
- Tencent Hunyuan3D: disabled

Codex MCP configuration:

```toml
[mcp_servers.blender]
command = "uvx"
args = ["--python", "3.11", "blender-mcp"]

[mcp_servers.blender.env]
BLENDER_HOST = "localhost"
BLENDER_PORT = "9876"
UV_PYTHON_PREFERENCE = "only-managed"
DISABLE_TELEMETRY = "true"
```

Poly Haven supplies licensed HDRIs, materials and generic environmental ingredients. It does not define the original planets, crew, ship, Ori or Signal visual identity.

Blender's own background CLI complements MCP for deterministic export:

```bash
blender -b captain_source.blend --python tools/blender/export_unity_fbx.py
```

### 4.3 Media and animation tools

- Google Flow produces Signal visions, distant cosmic events, travel transitions, establishing shots without the customized Captain, and marketing shots.
- Scenes visibly containing the customized Captain remain real-time Unity cinematics.
- Lyria/Flowmusic produces musical material that is edited into offline loops, stems and intensity variants; music is not generated at runtime.
- Mixamo may provide generic animation clips through a normal WebGL-capable browser. The project never depends on Mixamo because Blender Rigify and authored animation remain the fallback.
- Sound effects come from recording, synthesis and appropriately licensed libraries.
- Generated video is delivered as 1080p, 30 FPS, H.264 MP4 without baked subtitles, with a still-image fallback and separate audio when possible.

## 5. Unity project architecture

### 5.1 Runtime foundation

- Unity 6 LTS with its exact editor revision committed to project metadata.
- Universal Render Pipeline.
- Unity Input System.
- Addressables for destination and optional-content delivery.
- TextMeshPro and uGUI for runtime interfaces.
- UI Toolkit for custom editor windows and validators.
- Assembly definitions isolate gameplay, content, editor, tests and platform SDKs.

### 5.2 Scene topology

Core scenes:

- `Boot`
- `Frontend`
- `Clubhouse`
- `SpaceFlight`
- `Mirra`
- `KoroVesper`
- `AsterVeil`
- Small additive cinematic and lighting scenes where useful

`Boot` initializes local settings, save recovery, age/privacy state, optional cloud identity, content catalogue and the selected store adapter. Destinations load additively and are grouped in Addressables by act. Approach, orbit, landing and departure sequences mask asynchronous loading without repeated menu screens.

### 5.3 Explicit game modes

The game uses an explicit mode controller:

```text
Frontend -> Customization -> Clubhouse -> Flight -> Surface
Surface -> Discovery Lens / Dialogue / Cinematic -> Surface
Surface -> Flight -> Clubhouse -> Ending
```

Pause, Photo Mode, accessibility and parental/account screens are overlays. Each mode owns its input map, camera policy, HUD and allowed transitions.

### 5.4 Major modules

- Player motor and Captain customization
- Crew Director and individual crew brains
- Ori controller
- Flight and landing
- Camera and cinematic camera
- Discovery Lens and scientific instruments
- Interaction anchors
- Mission graph and typed gameplay events
- Dialogue, hints and cinematics
- Cosmic Atlas and science sources
- Discoveries, cosmetics and Photo Mode
- Audio and Signal motif controller
- Versioned local saves and cloud synchronization
- Account, birthday, commerce, notification, analytics and store adapters
- UI, accessibility and localization

Gameplay code never calls Firebase, RevenueCat, Samsung, Google, OneSignal, Tenjin or Layers directly. It communicates through narrow C# interfaces so a service failure cannot block the story.

## 6. Gameplay systems

### 6.1 Surface movement

The Captain uses a custom kinematic capsule motor rather than an uncontrolled rigid body. It supports:

- Ground movement, slopes and step handling.
- Jump, suit jet and low-gravity tuning.
- Wind, ice, moving platforms and environmental forces.
- Authored recovery volumes and safe respawn anchors.
- Separate collider, camera, IK and stride calibration for short/compact, medium/average and tall/broad body families.

All three body families share gameplay speed and reach fairness. Body choice changes presentation, not power.

### 6.2 Camera

The third-person camera provides orbit, collision avoidance, horizon management, manual recentering, contextual FOV and reduced-motion behavior. Camera profiles cover exploration, aiming, Discovery Lens, flight, dialogue, cinematics and Photo Mode.

### 6.3 Flight

Flight is arcade-accessible and scientifically inspired rather than a full n-body simulation. Handcrafted regions support:

- Boost, brake, drift, momentum and controlled turning.
- Gravity-assist routes and simplified orbital planning.
- Approach, orbit, landing and departure.
- Guided, Balanced and Ace assistance.
- Dynamic debris and relative-motion challenges.
- Soft failure, quick recovery and readable prediction arcs.

### 6.4 Discovery Lens

The Lens is one coherent instrument interface with modes for:

- Imaging
- Spectrum
- Temperature
- Atmosphere
- Motion
- Signal analysis

Players make or select predictions and test them in the world. Incorrect predictions teach, update evidence and continue play rather than creating punitive quiz failure.

### 6.5 Destination mechanics

- Mirra: wind, temperature gradients, tidal locking and climate circulation.
- Koro/Vesper: low gravity, geysers, tidal heating and spectra.
- Aster Veil: moving debris, relative motion, momentum and gravity assists.

Interactables declare player, crew and Ori anchors, required tool, animation, events, reservations and recovery behavior.

## 7. Crew intelligence

Crew behavior is authored intelligence, not expensive open-ended AI. It combines a Crew Director, individual personality brains, small utility scoring and reliable action states.

### 7.1 Crew Director

The Director:

- Selects the two active companions for a destination.
- Chooses formations and contextual positions.
- Prevents crowding and reserves interaction anchors.
- Grants one dialogue token at a time.
- Coordinates synchronized reactions and radio contributions.
- Suspends unnecessary decision-making during cinematics.

Only two companions plus Ori run full logic. Other crew members communicate through controlled ship-radio events.

### 7.2 Individual personality priorities

- Mira notices light, atmospheres, weather, living possibilities and careful scientific evidence.
- Juno notices machinery, repairs, material failures and tool opportunities.
- Kai notices danger, traversal, fast routes, momentum and piloting challenges.
- Bea notices photographs, emotional beats, memories and crew wellbeing.
- Ori scans hazards, enters small spaces, communicates with tones and reacts unusually to the Signal.

Mandatory story actions outrank safety/recovery, which outrank personality opportunities.

### 7.3 Action states

- Spawn or join
- Follow player
- Move to contextual or authored position
- Traverse
- Observe or investigate
- Perform scripted interaction
- React
- Speak
- Participate in conversation
- Enter cinematic control
- Wait naturally
- Recover after navigation failure

Tagged perception, camera visibility, reservations and authored recovery anchors keep behavior reliable. Decisions run only a few times per second; movement and animation continue each frame. Dialogue and story memory are authored and deterministic—there is no live generative dialogue.

## 8. Character and art pipeline

### 8.1 Reference sheets are mandatory

Character generation follows this order:

1. A master visual-style sheet for the entire cast. It includes representative
   Captain, Mira, Juno, Kai, Bea and Ori examples and locks the shared face,
   proportion, hair, homemade-suit, Signal, rendering, detail-density and
   mobile-silhouette rules; it is not an individual turnaround.
2. A full height/silhouette lineup containing the Captain, Mira, Juno, Kai,
   Bea and Ori together on one floor line with exact Blender-unit heights.
3. A Captain-only sheet with neutral front, side and back views for all three
   equal-capability body families and their shared rig/clothing landmarks.
4. A Captain-only customization sheet covering face, skin, eyes, hair,
   clothing, colors and accessories across all three body families.
5. Separate Mira, Juno, Kai and Bea sheets, each with one named character's
   neutral front/side/back turnaround, face, hair, clothing, palette,
   equipment, exact height and mobile silhouette.
6. An Ori-only front/side/back mechanical sheet, adding a top view when needed
   and identifying its eye, antenna, joints, scanner, panels, Signal parts,
   lights, materials and exact size.
7. One cast-wide expression grid with separate Captain, Mira, Juno, Kai, Bea
   and Ori rows. Human rows include core emotions, blink and compact speech
   shapes; Ori uses its eye, light, antenna, head and body pose.
8. One character-equipment sheet covering each cast member's owned gear plus
   shared wearable/handheld equipment, with scale and attachment callouts.
9. One cast-wide material/color sheet covering skin, hair, fabrics, rubber,
   plastic, metal, glass, patches, Ori's shell, screens, lights, Signal energy
   and character palettes, including surface-response labels.

Sheets use neutral orthographic front, side and back views at consistent scale in an animation-ready pose. A character cannot enter Blender production until the corresponding sheet is approved.

Written labels, role descriptions, numerical dimensions, landmark diagrams and
control notes are the production authority if an approved generated image has
an incidental visual mismatch. Downstream modelers, riggers and animators must
correct that mismatch in the real asset; they must not blindly trace it. This
especially applies to matching facial expressions to their named semantics.

The Task 10 package therefore contains exactly 12 approved images: the master
sheet, lineup, two Captain sheets, five individual-character sheets and three
cast-wide support sheets. The approval ledger records subject coverage,
required views, scale/style consistency, mobile readability, equipment
consistency, decision, notes and date for every image. A group-sheet approval
does not implicitly approve an individual character.

The shared Task 10 art direction approved on 2026-08-23 is **cinematic
storybook realism**: the representative Captain and four named crew—five human
children total—read as 12–14 years old at roughly 6–6.5 heads tall, with softly believable faces,
slightly enlarged expressive eyes and modestly enlarged hands and boots for
readability. Materials remain physically believable while silhouettes,
features and homemade repaired exploration gear stay warmly stylized. The
palette and lighting follow the warm-sunset/cool-starlight duality of
`outputs/just-some-stars-mirra-gameplay-target-v1.png`, with restrained
cyan/violet Signal accents. The approved direction excludes anime, chibi,
photoreal-adult, toy-plastic, tactical-military, superhero, generic mascot and
recognizable franchise design language. The binding decision and per-sheet
status live in `docs/art/character-reference-approval.md`.

### 8.2 Blender production sequence

```text
Reference sheet -> blockout -> sculpt -> retopology -> UV
-> normal/AO bake -> texture -> facial blendshapes
-> shared humanoid rig -> skin weights -> LODs -> FBX
-> Unity Humanoid prefab -> Android device test
```

The `.blend` source remains authoritative. Unity receives clean FBX files with controlled transforms, materials, skeletons and clips.

### 8.3 Captain modularity

The Captain supports:

- Three body families with fitted clothing.
- Face presets and skin tones.
- Hair shapes and colors.
- Eye colors.
- Suit components, colors and patterns.
- Gloves, boots, patches, backpacks and accessories.
- Pronouns and callsign.

Cosmetics are ScriptableObject definitions that reference compatible meshes, materials, icons, body-family fits, rarity presentation and entitlement requirements. Runtime assembly aims for approximately three character render groups rather than one renderer per tiny part.

### 8.4 Crew and Ori

Crew members share humanoid rig conventions and shader foundations but are bespoke characters, not randomized Captain presets. Each has unique face, proportions, hair, colors, equipment, silhouette, idles and expressions. Compact blendshapes cover blinks, brows, smiles, frowns, surprise, fear, mouth emotion and a small viseme set. Ori uses a dedicated non-humanoid rig.

### 8.5 Initial asset budgets

- Captain/crew LOD0: approximately 50,000–80,000 triangles.
- LOD1: approximately 25,000–40,000 triangles.
- LOD2: approximately 8,000–15,000 triangles.
- Ori LOD0: approximately 25,000–45,000 triangles.
- Shared character texture sets: primarily 2K.
- Accessories and hair: primarily 1K, packed where practical.
- Hero environment atlases and trims: 2K–4K.
- Android texture target: ASTC on supported devices with fallback compression profiles.

Budgets are measured on the Realme Narzo and may move between assets without exceeding the scene frame, memory and thermal budgets.

## 9. Data-driven missions, dialogue and saves

### 9.1 Content assets

ScriptableObjects define missions, phenomena, dialogue, crew personalities, instruments, Atlas entries, cosmetics, accessibility profiles, audio events and cinematics.

A mission is a graph of nodes containing stable IDs, completion rules, optional objectives, scene, companions, dialogue, checkpoints, next nodes, discoveries and recovery behavior. Typed events include `LandingCompleted`, `PhenomenonObserved`, `PredictionRecorded`, `InstrumentUsed`, `SignalFragmentRecovered` and `ConversationCompleted`.

### 9.2 Dialogue and learning

Dialogue entries contain localization key, speaker, voice reference, emotion, expression, gesture, conditions, priority, interruptibility, cooldown and follow-ups. Hints respond to player behavior and crew personality. Science facts have explicit source records and separate gameplay, short Atlas and deep Atlas explanations.

English ships first, but all player-facing text uses localization keys from the beginning.

### 9.3 Local save

The offline save contains story progress, Captain configuration, earned cosmetics, Atlas discoveries, photographs metadata, birthday, accessibility and settings. It uses:

- A versioned schema.
- Atomic temporary-write and replace.
- Last-known-good backup.
- Safe mobile suspend handling.
- Migration tests for each version.
- Separate device graphics/control settings where practical.

Editor validators reject broken mission links, duplicate IDs, missing references, missing science sources and cosmetics without all declared body-family fits.

## 10. Accounts, cloud saving and birthdays

### 10.1 Guest-first account model

No account is required. Guest players receive complete local saving, gameplay and store restoration. The settings and post-checkpoint UI offer **Back up with Google** without blocking progress.

Google linking uses Firebase Authentication. Cloud data uses Cloud Firestore and remains accessible offline through its mobile cache. A guest save migrates into the authenticated profile without restarting the story.

### 10.2 Cloud merge policy

- Story keeps the furthest valid checkpoint.
- Discoveries, earned cosmetics and Atlas entries merge as a union.
- Captain appearance uses the newest explicit customization edit.
- Device graphics and control settings remain local.
- Photos remain local for Chapter One to control storage use.
- Genuine incompatible states present a clear player choice rather than silently overwriting.

Firestore rules restrict each profile to its Firebase UID. The game supports sign-out, Google unlinking, cloud-data export and complete account deletion.

### 10.3 Birthday data and gifts

The account privately stores day, month and year of birth. The birthday is never public or sent to advertising attribution. Guests store it locally; account linking migrates it to the private cloud profile.

The birthday may be corrected once. Further changes require the grown-up confirmation flow. A TypeScript Firebase Cloud Function uses server time and `lastBirthdayGiftYear` to grant one annual gift. The gift remains claimable for 30 days and never presents a purchase prompt.

The birthday event includes a Clubhouse celebration, crew dialogue, Ori delivery animation, homemade decorations and a yearly cosmetic set. Guest mode uses local eligibility; preventing abuse of a free offline gift is less important than preserving legitimate offline play.

For child players, cloud linking is optional and placed behind grown-up confirmation. Authentication data is not reused for advertising.

## 11. Monetization and cosmetics

### 11.1 Principles

- Chapter One remains fully playable for free.
- Cosmetics and creative/replay features are monetized.
- Gameplay power, learning, story completion and accessibility are never sold.
- No advertising, subscription, premium currency, randomized loot, energy or manipulative countdowns at launch.
- Real prices are displayed directly.
- Purchases use a grown-up confirmation followed by native store authentication.
- Restore Purchases is visible in the shop and settings.

### 11.2 Catalogue target

The launch catalogue targets more than 100 polished entries created from excellent modular geometry, materials, effects and coordinated combinations—not a wall of low-effort recolors.

Categories include:

- Captain suits, helmets, visors, gloves, boots, backpacks, patches, hair accessories and emotes.
- Ori shells, faces, antennae, scan beams, sounds and trails.
- Ship hulls, decals, cockpit themes, dashboard toys, engine trails and landing effects.
- Discovery Lens bodies, displays, holograms and scan effects.
- Clubhouse furniture, posters, telescopes and miniature planetariums.
- Photo poses, crew poses, frames, stickers and filters.
- Authored alternate crew expedition outfits.
- Coordinated Mirra, Koro/Vesper, Aster Veil and Signal collections.

The free game also awards attractive cosmetics through discovery and mastery.

### 11.3 Pricing ladder

US reference prices are localized by each store:

- Individual premium cosmetics: $1.99–$3.99.
- Small themed sets: $4.99–$7.99.
- Complete planet collections: $9.99.
- Explorer Edition: $14.99.
- Founder's Constellation Pack: $19.99.
- Complete Launch Collection: approximately $29.99 with a clearly stated bundle saving.

Explorer Edition contains Expedition Replay Mode, Advanced Photo Mode, cinematic camera tools, soundtrack jukebox, development/science archive, special modifiers and approximately 25–35 coordinated premium cosmetics.

The Founder's Constellation Pack contains an authored Founder Captain set, ship transformation, Signal materials, Ori shells, cockpit and clubhouse collection, trails, landing effects, Lens theme, badge and approximately 40–50 coordinated pieces. Any availability window must be honest and stated in absolute dates.

Future paid chapters are sold only when the relevant content is complete or its delivery terms are precise. There is no vague preorder promise.

### 11.4 RevenueCat model

Stable entitlement identifiers:

- `explorer_edition`
- `founders_constellation`
- `complete_launch_collection`
- `mirra_collection`
- `koro_vesper_collection`
- `aster_veil_collection`

Individual products use stable `jss.cosmetic.<category>.<item>` identifiers. RevenueCat Offerings group products and drive a dynamic shop presentation. The local catalogue maps entitlement state to visible inventory without embedding store prices.

## 12. Android stores and commerce adapters

### 12.1 Build variants

- Development: logs, cheats, debug panels and RevenueCat Test Store.
- Android Internal: signed physical-device testing.
- Google Play Release: `com.scientificaj.justsomestars` with RevenueCat Unity and Google Play Billing.
- Galaxy Release: `com.scientificaj.justsomestars.galaxy` with the isolated Galaxy adapter.

Separate assembly definitions and scripting symbols prevent the wrong billing libraries from entering a store build.

### 12.2 Store interface

`IStoreService` exposes initialization, offerings, purchase, restore, entitlement refresh and error state. Gameplay sees only owned cosmetic/edition entitlements.

The Google path uses the official RevenueCat Unity SDK. RevenueCat's documented Galaxy support is currently native Android, so the Galaxy build uses a small Kotlin/Java Unity bridge to RevenueCat's Galaxy Android module; Samsung's Unity IAP plugin remains the tested fallback. Galaxy commerce must never delay or block the free story.

Purchased entitlements are cached after verification. Previously verified content remains usable offline; buying and restoring require connectivity. Interrupted purchases are rechecked on resume and never guessed.

Google or Samsung processes Android payments and pays the publisher. RevenueCat manages offerings, receipts, customer identity and entitlements. Stripe is not required for Android in-app checkout.

## 13. Graphics, performance, UI and accessibility

### 13.1 Rendering

AAA-inspired mobile quality comes primarily from coherent art direction, strong lighting, materials, animation and composition—not unrestricted GPU cost.

- Mirra uses hot orange and frozen blue lighting across the twilight divide.
- Koro/Vesper uses ice cyan under an immense violet-blue sky.
- Aster Veil uses near-black space, bright debris reflections and Signal-purple accents.
- Shared Shader Graph foundations keep materials coherent and batchable.
- One major realtime shadow-casting light is the normal scene target.

### 13.2 Quality profiles

- Performance: stable 30 FPS with reduced resolution, shadows, particles and post-processing.
- Balanced: stable 30 FPS with improved lighting and effects.
- Cinematic: maximum approved 30 FPS presentation on capable devices.
- High Frame Rate: 60 FPS with dynamic resolution and scaled effects.

Optimization uses GPU instancing, batching, frustum/occlusion/distance culling, LODs, pooled VFX, additive loading, Addressables, streamed music, preloaded video and dynamic resolution. Device testing includes full-session thermal soak, low-battery behavior, suspend/resume and interruptions such as calls.

### 13.3 Runtime UI

Runtime UI uses uGUI and TextMeshPro. It combines large mobile touch targets with the homemade ship and Signal visual language. The Input System exposes semantic actions to touch, gamepad and later keyboard bindings.

### 13.4 Accessibility

- Independent piloting, exploration and science-depth settings.
- Scalable text, dyslexia-friendly font and adjustable dialogue speed.
- Captions with speaker names.
- Colorblind-safe symbols and contrast modes.
- Reduced shake, flashing, motion blur and particle density.
- Left-handed controls, sensitivity and camera recentering.
- Navigation line, contextual hints and recovery assistance.
- Separate music, dialogue, effects and haptic controls.
- Accessibility available before the opening cinematic.

## 14. Privacy and analytics

The game collects only what supports accounts, cloud saves, purchases, birthday gifts, stability and deliberately enabled growth measurement.

- No precise location, contacts, microphone or real-world camera permission.
- No advertising ID for child or unknown-age players.
- No public profiles, chat, freeform sharing or social feed.
- Google account details remain authentication data, not advertising data.
- Date of birth is private and excluded from analytics.
- Photo Mode captures only the rendered game world.
- Account deletion removes cloud profile data and birthday data.
- Privacy and purchase explanations use plain language accessible to families.

Tenjin and Layers analytics are initialized only under their approved mixed-audience configuration. Child and unknown-age flows receive the strictest defaults. OneSignal, when activated, requires consent before initialization and provides an in-game opt-out.

## 15. ShipKit and external-service operating plan

ShipKit perks unlock progressively after registration, project setup, store connection and first revenue. The project will check the Shipaton email after each verified milestone and update this ledger. Newly unlocked free/no-card tools are claimed promptly. A service is integrated only through an adapter or workflow that has a defined role and does not compromise the family design.

### 15.1 Complete status ledger

| Platform | Current status | Completed | Activation role and remaining work |
|---|---|---|---|
| RevenueCat | Ready | Account verified; **Just Some Stars** project created; Test Store active | Install Unity SDK; register both Android apps; create products, entitlements and Offerings; implement paywall; complete Test Store, Google and Galaxy purchases |
| Layers | Claimed | Two months of Pro active | Finish onboarding with the repository/site/store context; define one growth hypothesis; install and verify its SDK using the family-safe analytics configuration; run and document a focused growth loop |
| Junie | Claimed | 30 AI credits, valid through September 30 | Configure in the JetBrains development environment for focused C#, editor-tool and test assistance; validate all changes through normal review and tests |
| Codemagic | Connected; remote Unity deferred | Account, GitHub repository and `codemagic.yaml` workflow connected; 500 free macOS minutes available | Revisit Unity CLI execution only if a valid Plus/Pro CI license becomes available; never invent credentials or spend minutes on a guaranteed activation failure |
| Lance | Claimed | **Just Some Stars** organization; Pro and 2,000 monthly credits; top-ups disabled | Preserve the account. Lance currently targets App Store/iOS submission, so it activates only if iOS becomes a deliberate platform; it is not forced into Android |
| Limrun | Claimed | 2,000 credits with no expiry | Use agent-accessible Android emulators/device infrastructure for automated install, smoke, screenshot and demo validation when local hardware coverage is insufficient |
| Tenjin | Account ready | Account and **Just Some Stars** company profile created | Register the shipped app; confirm/apply the Shipaton plan; use consent- and age-gated attribution to measure adult/eligible acquisition campaigns without sending child DOB or restricted identifiers |
| Argent | Registered | Beta registration completed under **Just Some Stars** | When access arrives, install its agentic test toolkit so coding agents can launch the Android build, observe failures and verify fixes |
| OneSignal | Not activated | Existing account and promotion opened | After billing/company prerequisites are acceptable, activate it for optional birthday-gift and major-update notifications; require consent before SDK initialization; gameplay never depends on it |
| Noise | Not claimed | Existing account located | After the app is live and a creator campaign budget is deliberately approved, complete the $5 validation/spend requirement and use matched credits for repeatable launch creatives; no Noise SDK is required in child gameplay |
| Stripe | Milestone locked | No action currently possible | After RevenueCat and a real transaction unlock the $250 processing credit, claim it; use only for a later compliant web-to-app Explorer Edition funnel, never as a replacement for required Android in-app billing |
| Replit | Intentionally skipped | None | Paid-plan discount and Replit-specific build path do not fit the approved Unity pipeline |
| Bitrig | Intentionally skipped | None | Paid-plan discount and Swift/iOS focus do not fit the Android Unity release |

### 15.2 Non-ShipKit production services

| Service | Role |
|---|---|
| Firebase Authentication | Optional Google-backed Captain account |
| Cloud Firestore | Offline-capable cloud save and private profile |
| Firebase Cloud Functions | Secure birthday grants and account maintenance |
| Blender MCP | Assisted Blender scene, asset, rig and export operations |
| Poly Haven | Licensed HDRIs, materials and environment ingredients |
| Google Flow | Selected non-custom-Captain cinematics and marketing visuals |
| Lyria/Flowmusic | Music generation followed by human editing and offline integration |
| Mixamo | Optional generic animation source; never a required dependency |
| GitHub | Source, issues and public project history |

### 15.3 Milestone-triggered perk loop

1. Unity project created: register exact package identifiers and update RevenueCat, Firebase, Layers, Tenjin and CI records.
2. First Android build: connect Codemagic, Limrun and Argent when available; validate the internal build locally and defer store upload until a playable release candidate.
3. RevenueCat Test Store purchase: validate the entire entitlement and restore path.
4. Store accounts connected: import real products, configure signing, start licensed billing tests and refresh ShipKit email.
5. Public store release: finish Layers onboarding, activate the growth loop, register Tenjin app and prepare Noise launch work.
6. First real sale: confirm RevenueCat revenue, refresh milestone email, claim the Stripe perk and any newly unlocked tools.
7. Each later milestone: update this ledger before starting the next external integration.

## 16. CI, security and release verification

### 16.1 Codemagic pipeline

When a valid Plus/Pro CI license is available, Codemagic runs:

1. Repository checkout and Unity dependency/cache restore.
2. EditMode tests.
3. PlayMode smoke tests.
4. Addressables validation and build.
5. Google or Galaxy Unity CLI build.
6. Artifact signing through encrypted variables.
7. Build manifest, logs, test results and signed artifact retention.

Keystores, passwords, RevenueCat keys, Firebase service credentials and store credentials never enter Git. Parents may own or complete publisher, commercial seller, tax and payout details when legally required.

### 16.2 Required automated and device tests

- Mission graph, dialogue link, science-source and cosmetic-fit validation.
- Save serialization, corruption recovery and migrations.
- Cloud guest-to-Google migration and merge conflicts.
- Birthday eligibility and one-gift-per-year enforcement.
- Purchase success, cancellation, interruption, restore and offline cache.
- Surface movement and navigation recovery.
- Flight assists and checkpoints.
- Crew interaction reservations and off-camera recovery.
- Accessibility combinations.
- Fresh install, upgrade, airplane mode, suspend/resume and interrupted app lifecycle.
- Realme Narzo frame-time, memory and thermal soak.
- Google Play internal/closed test purchases.
- Galaxy licensed-device purchases; Galaxy commerce is not considered verified in an emulator.
- Build inspection proving Google and Galaxy adapters cannot coexist incorrectly.

## 17. Production calendar

### August 21–24: foundations and release runway

- Create the Unity project and command-line build entry points.
- Finalize package identifiers and Firebase Android clients.
- Build Boot, Frontend and graybox Clubhouse.
- Establish input, save, Addressables and scene loading.
- Produce the first Realme Narzo build.
- Prepare store runbooks and identifiers; defer listings and the Google closed-test clock until a playable release candidate exists.
- Configure RevenueCat Test Store.
- Begin the master style sheet and crew lineup.

### August 25–31: characters and core gameplay

- Complete mandatory character sheets.
- Produce Captain body bases, shared skeleton and first animation set.
- Establish modular cosmetic sockets.
- Produce first-pass Mira, Juno and Ori.
- Implement surface motor, camera, interactions, Lens and Crew Director.
- Establish Firebase accounts/cloud schema.
- Complete RevenueCat purchase and restore through the service abstraction.

### September 1–7: Mirra final-quality benchmark

- Complete Mirra's flight, landing, exploration, science and Signal path.
- Add final-direction lighting, VFX, sound and music.
- Reach the approved visual target on the declared quality profile.
- Prove companion intelligence, real-time cinematics and store abstraction.
- Begin Galaxy closed IAP testing.

### September 8–14: complete Chapter One

- Complete Koro/Vesper and Aster Veil.
- Complete the opening, ending-before-dinner and mission graph.
- Complete Atlas, science content, dialogue, Captain customization and birthday flow.
- Complete cloud synchronization and selected Flow/Lyria assets.
- Achieve a playable 45–60 minute start-to-finish build by September 14.

### September 15–19: catalogue and award polish

- Expand toward 100+ catalogue entries.
- Complete Explorer Edition, Founder's Constellation and planet collections.
- Complete Expedition Replay and Advanced Photo Mode.
- Polish animation, expressions, shaders, VFX, cinematography, accessibility and audio.
- Produce icon, screenshots, trailer and store materials.

### September 20: content lock

No new foundational systems or story restructuring begin after content lock. Work is limited to fixes, optimization, presentation polish and already validated cosmetic additions. Product and entitlement identifiers are frozen.

### September 21–23: store release candidates

- Sign and verify Google and Galaxy builds.
- Run purchase, restore, offline, account, birthday and thermal tests.
- Submit Galaxy Store release.
- Submit Google Play production release as soon as production access is granted.

### September 24–27: review and release

- Respond immediately to store feedback.
- Validate live products and RevenueCat entitlements.
- Release through the first approved qualifying store.
- Begin Layers, Tenjin and eligible growth workflows.

### September 28–30: protected buffer

- Confirm the public listing is downloadable.
- Complete and verify a real production purchase.
- Capture the final gameplay and monetization demonstration.
- Submit required Shipaton materials and publish only stability-critical updates.

If a new Google personal developer account is subject to the 12-tester/14-day gate, Galaxy Store is the parallel public-release safety route. Both builds remain supported products rather than disposable submission binaries.

## 18. Acceptance criteria

The technical release is complete when:

- A family can finish the full story and reach dinner in approximately one hour.
- The first public store version is live before the Shipaton deadline.
- RevenueCat records a real purchase that grants and restores the correct entitlement.
- Guest play works offline from clean install through the complete story.
- Google account linking backs up and restores progress without losing the guest save.
- The birthday is private and the annual gift cannot be claimed repeatedly by a cloud account.
- Captain cosmetics fit all three body families without clipping that breaks presentation.
- Crew members show distinct personality while recovering reliably from navigation failure.
- Guided, Balanced and Ace modes all complete every mission.
- Required accessibility combinations remain usable on the opening screen and throughout play.
- The Realme Narzo maintains the declared stable 30 FPS profile through representative gameplay and thermal testing.
- Google and Galaxy builds call only their intended store adapter.
- Service outages never block the free Chapter One experience.
- The ShipKit ledger reflects every unlocked milestone, claim and integration truthfully.

## 19. Reference links

- [RevenueCat Unity SDK](https://www.revenuecat.com/docs/getting-started/installation/unity)
- [RevenueCat Android and Galaxy modules](https://www.revenuecat.com/docs/getting-started/installation/android)
- [RevenueCat Offerings](https://www.revenuecat.com/docs/offerings/overview)
- [Shipaton 2026 preparation guide](https://revenuecat.github.io/codelabs/shipaton-2026-prep.html)
- [Shipaton 2026 resources and progressive ShipKit](https://revenuecat-shipaton-2026.devpost.com/resources)
- [Google Play testing requirements](https://support.google.com/googleplay/android-developer/answer/14151465)
- [Google Play Families Policy](https://support.google.com/googleplay/android-developer/answer/9893335)
- [Samsung Galaxy Store registration](https://developer.samsung.com/galaxy-store/launch.html)
- [Samsung IAP Unity plugin](https://developer.samsung.com/iap/samsung-iap-unity-plugin.html)
- [Firebase Authentication for Unity](https://firebase.google.com/docs/auth/unity/start)
- [Cloud Firestore offline data](https://firebase.google.com/docs/firestore/manage-data/enable-offline)
- [Blender MCP](https://github.com/ahujasid/blender-mcp)
