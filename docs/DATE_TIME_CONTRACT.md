# Date, time-zone, and locale contract

Project Cost Forecast uses three deliberately different date/time concepts.

| Concept | Representation | Meaning | Display/persistence rule |
| --- | --- | --- | --- |
| NZ business date | `DateOnly` | A calendar date used by fiscal periods, schedules, holidays, and report ranges | No time zone is attached. JSON uses the invariant ISO date form `yyyy-MM-dd`. |
| Local display time | `DateTimeOffset` converted to `Pacific/Auckland` | The time a New Zealand user should see for a durable instant | UI formatting uses the `en-NZ` culture and the NZ daylight-saving rules. |
| Durable instant | UTC `DateTimeOffset` | An audit, snapshot, comment, mapping, or preference event that must identify one instant | JSON and generated file names use invariant UTC forms. New workflow values come from `IClock.UtcNow`. |

## Clock seam

`IClock` is the application seam for current time. `SystemClock` is used at
the composition boundary; tests and replayable workflows can inject
`FixedClock`. Audit events, saved-month snapshots, schedule baselines, manual
comments, import-recovery records, cost-centre mappings, curve presets, and
diagnostic entries obtain their durable timestamps from that seam.

`IClock.TodayInNewZealand` is used when a workflow needs "today" as a business
date. It is calculated from the current UTC instant in `Pacific/Auckland`, so
the date remains correct around daylight-saving transitions and UTC midnight.

## Legacy migration

The project migration and preferences persistence boundaries accept both
offset-bearing timestamps and the older offset-free local timestamp form. An
offset-free legacy value is interpreted as a `Pacific/Auckland` local time,
then normalized to UTC. Explicit offsets and `Z` values are normalized to UTC
without changing their instant.

The NZ daylight-saving contract is deterministic: an ambiguous fall-back local
time uses the standard offset, while a local time in the spring-forward gap is
rejected as invalid. Fiscal-period labels and their canonical `DateOnly`
month starts are migrated independently, so timestamp normalization cannot
change fiscal-period semantics.

## Persistence and display examples

An instant such as `2026-08-29T10:00:00+12:00` is persisted as
`2026-08-28T22:00:00.0000000Z`. It is displayed after conversion to New
Zealand time, using `en-NZ` formatting. Backup, restore, and corrupt-preference
quarantine names use invariant UTC timestamps with deterministic collision
suffixes; machine culture does not affect their ordering or representation.

Missing timestamps on newly constructed durable model objects start at the
`DateTimeOffset.UnixEpoch` sentinel and are replaced by the owning workflow's
clock when the event is created. This prevents a model constructor from
silently reading machine-local time.
