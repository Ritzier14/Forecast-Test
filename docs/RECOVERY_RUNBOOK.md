# Project Cost Forecast recovery runbook

## Backup policy

When the application overwrites an existing project, it creates a backup in a
`backups` folder beside that project. A backup is reported as usable only after
it has been parsed, migrated to the current project format, and passed the
same validation boundary used for opening a project.

The application retains the 10 newest backup names by default. At least two
backup copies are retained, including when several saves occur in the same
millisecond and names require collision suffixes. Older backup files are
pruned only after the new backup has passed verification. The current project
file is never pruned by this policy.

Backups in the same directory protect against a bad save or an accidental
edit, but they do not protect against disk failure, ransomware, or loss of the
whole directory. Copy important project files and their `backups` folders to a
separate drive or approved cloud/document-management location.

## Restore to a new project file (recommended)

1. If the current project opens, save it first if you need its latest changes.
2. Select **Restore backup** in the project actions.
3. Choose the `.bak.json` file to recover.
4. Keep the suggested `*.restored.json` destination, or choose another new
   filename. Do not select the original project unless replacing it is
   intentional.
5. The application verifies the backup, writes the restored project
   atomically, and opens the restored file. Check the project title, current
   period, forecast totals, transactions, and audit history before continuing.

## Restore over an existing file

Use this only when the original path must be replaced. The restore operation
first verifies the backup and then creates a verified pre-restore backup of
the existing destination before writing. If that protection step fails, the
destination is left untouched. After the restore, confirm the new file and
keep the pre-restore backup until the result is accepted.

Restore is whole-file recovery. It does not merge individual fields from the
backup with newer edits; use a new destination when both versions contain
changes that must be compared.

## If a backup is corrupt

The application rejects a backup that cannot be parsed, migrated, or
validated. It does so before changing the selected destination or the current
project. Choose another backup and repeat the recovery steps. If all local
backups fail, use the separately stored off-device copy and preserve the
original files for support review.
