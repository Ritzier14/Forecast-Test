# Project Cost Forecast diagnostics runbook

## Local diagnostics

The application keeps a best-effort rolling log under:

```text
%LOCALAPPDATA%\ProjectCostForecast\diagnostics.log
```

The active log is limited to 64 KB and one rotated file is retained as
`diagnostics.1.log`. Entries include the UTC timestamp, severity, operation,
exception type, and a sanitized reason. Exception messages are not written.
Project values, imported row contents, personal names, and full paths are not
part of the default diagnostic payload. A permissions or disk-space failure
while writing diagnostics is ignored so it cannot replace the original error.

For support, close the application before copying the two diagnostics files.
Review the files for sensitive information before sending them outside the
approved support channel.

## Corrupt preferences

Preferences are normally stored at:

```text
%LOCALAPPDATA%\ProjectCostForecast\user-preferences.json
```

If the JSON cannot be read, the application moves it beside the original as a
timestamped file such as:

```text
user-preferences.corrupt-20260829-101112-000.json
```

The application then loads default preferences and shows a startup status
notice. The quarantined file preserves the previous preference content for
support review; it is not used as active configuration. If quarantine cannot
be completed, defaults are still loaded and the diagnostic records the
failure.

## Runtime failures

- A UI dispatcher failure is recorded, followed by a generic error message,
  and the application closes to avoid continuing in an unsafe state.
- An application-domain failure is recorded and remains fail-fast because the
  process is no longer considered safe to continue.
- An unobserved background-task failure is recorded and marked observed; the
  shell remains available because that boundary is isolated from UI state.
