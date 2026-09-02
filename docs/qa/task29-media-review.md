# Task 29 animation, audio and cinematic checkpoint

Task 29 is implementation-complete, source-frozen and accepted by its single
bounded extreme critic with `PROCEED`. In accordance with the user-approved Tasks 26–30 cadence, this
checkpoint records authored contracts, deterministic media validation and
compilation only. Unity Test Runner execution, full regression, Android
packaging, device playback and performance evidence remain queued for the
single Task 30 final matrix.

## Delivered runtime route

- The existing deterministic frame-atlas locomotion and interaction clips stay
  authoritative. Six new performance sets add reaction and conversation clips,
  one frame-driven performance for every authored `DialogueEntry`, and the
  four Chapter One cinematic performances. Each dialogue performance derives
  its body motion from the entry's authored gesture instead of ignoring it.
- The same ordered `SpriteFrameEvent` stream now drives body frames, contacts,
  tools, audio, VFX, captions, facial expressions, speech shapes and interaction
  release. Cinematic definitions contain body performances only; they no longer
  duplicate those events on a wall-clock beat list. Interaction participants
  also release on their authored animation event instead of waiting a guessed
  total duration.
- Captain, Mira, Juno, Kai, Bea and Ori each have ten approved expressions and
  six speech shapes. The portrait remains visible while a separate transparent
  mouth layer displays speech, so a viseme cannot replace a whole face.
- Opening, Signal Reassembly, Clubhouse and Dinner Ending start only after the
  saved Captain state and scene state have been applied. Each sequence uses the
  real layered Captain, in-scene crew/Ori bodies, localized readable captions
  and an immediate existing-art still fallback. Optional video preparation or
  decode failure keeps or restores that fallback; no black frame is exposed.
- Mirra, Koro/Vesper and Aster Veil now publish their actual music states.
  `AudioDirector` crossfades sample-aligned foundation/Signal pairs using each
  state's authored duration, while the Explorer jukebox resolves the same cue
  library rather than unresolved `Resources.Load` strings.
- Mirra and Koro/Vesper mission dialogue presenters now bind the localized
  speaker, layered face/speech targets and their in-scene actors. Caption time
  scales with localized copy length and the saved dialogue-speed setting, while
  body, expression, speech and release remain on the one frame-event clock.

## Original media package

The repository contains 18 original stereo 44.1 kHz WAV files: five looping
foundation tracks, five exactly aligned Signal stems and eight effects. The
music follows the approved playful homemade / warm orchestral / restrained
electronic direction. The same five-note Signal motif increases from the
Clubhouse through Aster Veil and resolves at dinner.

Google Flow Music/Lyria was investigated, but its authenticated generation
surface was unavailable to this agent session. No provenance was fabricated.
The checked-in media is deterministic project-authored synthesis with no
external samples.

- Audio manifest: `Assets/_JustSomeStars/Audio/task29-audio-manifest.json`
- Audio-manifest SHA-256:
  `39dd5408b7053ff864d68bbfc93223058aaed758ad98b3b31f013b08d785c49e`
- Audio files: `18`; authored package bytes: `45,689,316`
- Rights ledger: `docs/media/media-rights-ledger.csv`
- Rights-ledger SHA-256:
  `677a48d9671d9bf69d10911abdfbf33e509b1ef45c9077dae6e1c79853e82603`
- Face sets: `6`; frame-event performance sets: `6`

## Source-fresh checkpoint evidence

- `generate_task29_captain_face.py` published and then independently validated
  the Captain expression/speech atlas; `generate_task29_audio.py --rights-only`
  refreshed the rights ledger and its `--validate-only` pass succeeded.
- Unity materialization imported the audio, created all six face sets and all
  six performance sets, rebuilt the four body-only cinematic definitions,
  patched seven owned scenes, reopened them, and validated every authored
  binding before exiting `0`.
- Materialization/compile log:
  `Builds/Logs/task29-corrective-media-materialize.log`, SHA-256
  `076111d9d51f10f2a49136e060bb72f2e71be73fc77ea5faf82452597d058220`.
- That pass freshly emitted Runtime, Editor, EditModeTests and PlayModeTests
  assemblies and contains the Task 29 success marker with no C# compiler error,
  unhandled exception, missing-script warning or leaked weak pointer.

Unity Test Runner tests are authored in `Task29MediaAssetTests`,
`AudioDirectorTests`, `FacialAtlasControllerTests`, `CinematicDirectorTests`
and the amended `SpriteAtlasAnimatorTests`. Their execution is intentionally
pending until Task 30; this document does not relabel compilation as test
execution or runtime acceptance.

The final bounded Task 29 critic inspected the corrected serialized assets,
runtime bindings, generator provenance and source-fresh compilation evidence
and returned `PROCEED`. Task 30 owns the deliberately deferred execution and
release-device proof.
