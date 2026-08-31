# Task 22 private-birthday review

Task 22 implements the local and deployable-code foundation for private
birthdays and annual gifts. It does not claim a Firebase deployment, a finished
Clubhouse celebration scene or final cosmetic art.

## Shipped contract

- Day, month and year remain private save/account data. UI copy never reveals
  age thresholds or suggests which date unlocks a less restricted flow.
- Privacy age observes a leap-day birthday on March 1 in non-leap years; the
  player-friendly gift window starts February 28.
- The claim window is exactly 30 trusted UTC dates. Guest claims are serialized
  and checkpointed locally; authenticated claims use one Firestore transaction.
- The callable ignores caller-provided birthday and time fields. It reads the
  authenticated UID document, uses server process time, preserves a
  server-owned annual-claim ledger and requires App Check.
- Firestore clients may neither forge nor change that annual-claim ledger. It is
  stored in the root UID document so normal account deletion removes it with
  the private profile rather than orphaning a subcollection. Client deletion is
  denied: the App-Check-protected account-deletion callable removes Firebase
  Auth first, deletes the UID root directly and has an Auth deletion trigger as
  retryable cleanup, so an authenticated UID cannot erase and recreate its gift
  ledger—even with a still-live pre-deletion ID token. Client profile creation
  is denied for the same reason; JSS-021 must route first-profile creation
  through a validated App-Check-protected server bootstrap. The typed
  Firestore write contract requires a concrete Task 21 activation gateway to
  preserve this server-owned field on every client save update.
- One birthday correction is allowed. Later corrections use the grown-up
  confirmation controller. Unknown and child states fail to the strictest
  grown-up rule.
- The 2026 content contract grants `birthday.ori-starlight.2026`, names the Ori
  delivery and homemade-decoration cues, and cannot expose a purchase prompt.

## Ownership boundary

This task publishes scene-independent presentation identifiers. Task 26 stages
the real Clubhouse celebration and crew dialogue, Task 27 supplies the finished
yearly cosmetic/catalogue ownership, and Task 28 renders and binds the birthday
setup and grown-up flows. Keeping those owners separate avoids placeholder art
or a duplicate UI authority.

## Verification

- `Builds/TestResults/task22-birthday-corrective-green.xml`: `11/11` passed.
- `Builds/TestResults/task22-save-schema-corrective-green.xml`: affected save,
  migration and cloud projection fixtures `32/32` passed.
- `Builds/TestResults/task22-server-delete-green.xml`: affected account
  lifecycle fixtures `13/13` passed with direct cloud deletion unused and the
  typed server deletion gateway invoked exactly once.
- `pnpm --dir firebase/functions test`: TypeScript build and `6/6` focused
  function tests passed.
- `pnpm --dir firebase test:rules` with the installed Java 21 runtime: Firestore
  Emulator rules `5/5` passed, including rejection of a client-forged annual
  claim ledger and a stale post-grant cloud-save revision.

The checked-in birthday/deletion callables and rules are locally verified
source, but the account system is deliberately not activation-complete: no
Firebase project, credential, server-authoritative profile-bootstrap callable,
remote function or cloud document was created. JSS-021 remains the explicit
credentialed bootstrap/activation and two-device integration seam.

The bounded independent Task 22 critic re-read the final sources, tests,
rules, evidence and staging contract and returned `PROCEED` with no remaining
Task 22 blocker.
