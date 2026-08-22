# Just Some Stars issue register

This register holds findings that are real but outside the frozen acceptance
contract of the task currently being executed.

## Operating rule

- An out-of-scope finding is recorded here; it does not expand, block, or
  restart the current task.
- Severity does not automatically promote a finding into the current task.
- A finding is worked only when the user explicitly promotes it or its named
  owner task begins.
- Critics report against the frozen task contract once. Newly observed
  out-of-scope findings are appended here instead of starting another audit
  loop.
- Focused verification belongs inside implementation. Full regression runs
  happen once at the declared package/release boundary.

## Open findings

| ID | Finding | Owner / revisit point | Current-task effect |
|---|---|---|---|
| JSS-001 | Codemagic app/repository/workflow setup is complete, with 500 free macOS minutes available. The user approved deferring remote Unity execution because the account has Unity Personal and no Plus/Pro serial for CI activation. | Revisit only if a valid Unity CI license becomes available | None; do not spend build minutes |
| JSS-002 | Produce the signed Google Play AAB, begin closed testing, and create the Galaxy Seller application record when a playable release candidate exists. | Growth and release package (Tasks 31–33) | None |
| JSS-003 | Replace the temporary Settings and disabled Continue behavior with the real settings, save-state, input, and navigation flows. | Tasks 6–8 and the later Frontend integration task | None |
| JSS-004 | Evolve the temporary offline/footer copy when the first real online service is introduced. | Task 21/28 integration | None |
| JSS-005 | Investigate the two un-attributed persistent native allocations reported only during development-player process shutdown if they recur under stack-enabled profiling. | Later device/performance QA | None |
| JSS-006 | Improve Unity-canvas device automation discovery so Argent does not require the documented logical-coordinate fallback; add local recording support only if the missing encoder becomes necessary. | Tooling/QA maintenance | None |
| JSS-007 | Replace Task-specific CI test counts and smoke filters as later tasks expand or rename the suites. | Task 31 CI hardening | None |

## Completed tasks

| Task | Completed | Evidence |
|---|---|---|
| Award-level Frontend redesign | 2026-08-22 — user approved | Approved landscape main screen and all local panels; EditMode `211/211`; PlayMode `70/70`; internal APK SHA-256 `9e85f4ec24d20c62a786997ac4a42dab132a79118e401f2c1d3efa61b3ff6b83` |
| Release Runway local foundation | 2026-08-22 — user approved revised exit | Installable tested Android skeleton, pushed repository, Codemagic app/workflow configured with remote Unity execution explicitly deferred, and store submission moved to the final Growth and release package |

## Resolved or promoted findings

Move an entry here only when its owner task starts or the user explicitly
promotes it. Record the resolving task or commit instead of deleting history.
