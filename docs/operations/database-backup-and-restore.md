# Database backup and restore runbook

## Backup policy

- Every deployment creates a PostgreSQL custom-format backup before migration when an active database exists.
- The `Database backup` GitHub workflow runs daily at 02:17 UTC for production and can be run manually for either environment. The current single live environment is production.
- Each dump has a SHA-256 checksum that is verified before upload.
- Backups are stored in the environment's private S3 backup bucket with server-side AES-256 encryption and bucket versioning.
- Host copies older than seven days are removed. S3 lifecycle policy is the longer-term retention authority.
- A successful job proves creation and off-instance upload; it does not prove restorability. Rehearse restoration before production and after material PostgreSQL changes.

## Create an on-demand backup

Run the `Database backup` workflow for the intended environment. Confirm:

- the workflow succeeded;
- both `.dump` and `.sha256` objects exist under `database/`;
- S3 encryption and object size are present;
- the checksum file names the corresponding dump.

Never download production data to an unmanaged machine.

## Restore rehearsal

Use a non-production recovery host (Staging once it exists, or an isolated recovery host). Restoring replaces the target database.

1. Record the target environment, current release SHA, backup key, approver, and start time.
2. Download the dump and checksum from the correct environment bucket.
3. Verify the checksum with `sha256sum --check`.
4. Copy the verified dump to `/opt/rfq-marketplace/backups/` on the recovery host.
5. Confirm the active release and that the target is not production.
6. Run:

   ```bash
   sudo CONFIRM_DATABASE_RESTORE=RESTORE \
     /opt/rfq-marketplace/current/scripts/database-restore.sh \
     /opt/rfq-marketplace/backups/<backup>.dump
   ```

7. Verify `/health/ready`, then run the Milestone 7 functional smoke journey.
8. Record duration, result, data timestamp, and any recovery gap.

The restore script stops Caddy and the API, recreates the configured database, restores with `--exit-on-error`, and starts the application. The confirmation environment variable is intentionally required.

## Production recovery

Do not improvise an in-place production restore.

- Confirm the incident and preserve the failed host/disk for diagnosis when possible.
- Select the newest known-good checksum-verified backup.
- Prefer restoring into newly provisioned infrastructure and validating it before directing traffic.
- Notify affected users if the recovery point loses accepted business transactions.
- Rotate credentials if compromise is suspected.
- After recovery, run the full acceptance smoke test and document the actual recovery point and recovery time.

## Known MVP limitation

The API and database share one Lightsail host, so an instance failure interrupts transactional work. Off-instance backups reduce data-loss risk but do not provide high availability or continuous point-in-time recovery.
