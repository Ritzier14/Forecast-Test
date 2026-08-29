# LUNA-24 finding-to-packet matrix

Recorded: 2026-08-29

This matrix closes the audit trail from the original finding register to the
packet that fixed, disproved, or bounded each item. `F-13` is intentionally
not marked complete: it is a P1 repository-cleanup decision that requires
explicit user approval before any tracked file is removed from Git.

| Finding | Severity | Disposition | Packet / evidence |
|---|---|---|---|
| F-00 | P0 process gate | Fixed | LUNA-00 checkpoint and pushed branch recorded in `docs/audit/BASELINE.md` |
| F-01 | P0 | Fixed | LUNA-03 close policy, `MainWindow.OnClosing`, and safe-close tests |
| F-02 | P0 | Fixed | LUNA-04 staged New Month operation and failure/re-entry tests |
| F-03 | P1 | Fixed | LUNA-01/02/11 discovered xUnit gate, 428-assertion map, and LUNA-22 Windows CI |
| F-04 | P1 | Fixed | LUNA-05 versioned migration pipeline and LUNA-23 format truth |
| F-05 | P1 | Fixed | LUNA-06A validation boundary plus LUNA-15 workflow tests |
| F-06 | P1 | Fixed | LUNA-06B revision token and stale-write conflict tests |
| F-07 | P1 | Fixed | LUNA-07 verified backup policy, restore tests, and recovery runbook |
| F-08 | P1 | Fixed | LUNA-08 sanitized diagnostics, runtime exception policy, and preference quarantine |
| F-09 | P1 | Fixed | LUNA-09 bounded/formula-safe import and LUNA-15B staged workflow tests |
| F-10 | P1 | Fixed | LUNA-13/14 presentation-boundary moves and architecture negative controls |
| F-11 | P1 | Fixed | LUNA-12 and LUNA-16A/16B state ownership, identity, and dirty-boundary evidence |
| F-12 | P1 | Fixed | LUNA-15A/15B headless file and import interaction boundaries |
| F-13 | P1 | Split — explicit approval pending | LUNA-22 filters normal package data and ignores future outputs, but 16 tracked `release/ProjectCostForecast/` files plus one tracked `Temp` file remain for an approved cleanup decision |
| F-14 | P2 | Fixed | LUNA-18A lifecycle owner and detach/dispose tests |
| F-15 | P2 | Fixed | LUNA-18B observed async boundary and LUNA-18C binding-error gate |
| F-16 | P2 | Fixed | LUNA-19A/19B/19C shared interaction and curve-math boundaries |
| F-17 | P2 | Fixed | LUNA-17 refresh diagnostics and LUNA-20 deterministic workload evidence |
| F-18 | P2 | Fixed | LUNA-21 locked restore, 22-package audit, and license review |
| F-19 | P2 | Fixed | LUNA-10 date/time contract, clock seam, and migration tests |
| F-20 | P2 | Fixed as documentation boundary; product work deferred | LUNA-22/23 release checklist, recovery runbook, release truth, and explicit installer/signing/UAT limitations |

## Closure decision

There are no unresolved P0 findings. F-13 is the only unresolved P1 gate and
is visible, scoped, and approval-gated rather than silently deferred. The
remaining installer/signing, direct original-workbook importer, release-data
distribution, and broader user-acceptance items are explicit release
limitations with role owners in
[`LUNA-23-RELEASE-TRUTH.md`](LUNA-23-RELEASE-TRUTH.md).

Until F-13 is either approved and completed in its own cleanup commit or
explicitly accepted by the user with a mitigation and review date, Sol Ultra
must treat the candidate as not finally releasable.
