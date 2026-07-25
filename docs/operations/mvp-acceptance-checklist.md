# MVP Acceptance Checklist

Use this checklist before an MVP release candidate is approved. Tests must use a preserved upgrade database and a separate disposable clean database. Never reset the development database to make a failing migration pass.

## Automated gates

```powershell
dotnet restore
dotnet build Marketplace.slnx --no-restore
dotnet test Marketplace.slnx --no-build

cd src/marketplace-web
pnpm install --frozen-lockfile
pnpm exec ng test --watch=false --reporters=verbose --progress=false
pnpm build
```

Expected result: every command exits successfully. Existing Angular component-style budget warnings must be recorded but do not fail the MVP gate.

## Database migration gates

1. Back up the preserved development database.
2. Create a separate empty database with a unique, explicit name.
3. Apply every migration to the empty database.
4. Confirm `/health/ready` reports `Healthy`.
5. Apply pending migrations to the preserved database without deleting or recreating it.
6. Confirm existing users, profiles, requests, offers, notifications, and conversations remain available.
7. Generate the idempotent migration script used by the deployment workflow and review it before release.

## Browser acceptance

### Anonymous and recovery

- Piața Live loads without authentication.
- The frontend shell and public navigation remain usable when the API is unavailable.
- Sign-in links to password recovery.
- Password-recovery requests show the same success response for known and unknown addresses.
- An incomplete, invalid, or expired reset link gives a clear way to request another link.
- Password mismatch is reported next to the form.
- A successful reset allows the new password and rejects the old password.

### Buyer

- Register and complete a Buyer profile.
- Create a request from both Requests and Account entry points; both open `/rfqs/new`.
- Complete multiple positions and confirm completed positions collapse into concise summaries.
- Publish, view, extend the deadline, and cancel an open request.
- Receive an in-app notification when a Provider submits or revises an offer.
- Compare offers and select one with a clear confirmation.
- Reopen an awarded request once with a reason; a second manual reopening is rejected.
- Open and send private offer messages.

### Provider

- Register and complete a Provider profile, categories, delivery coverage, and full-coverage capability.
- Discover only matching open requests.
- Save, submit, revise, and withdraw an offer.
- Review made offers and distinguish awaiting, accepted, not-selected, and withdrawn states.
- Retain access to the originating request after selection.
- Open and send private offer messages.

### Dual-role account

- Register one identity with Buyer and Provider roles.
- Piața Live reflects both roles without requiring profile activation.
- Buyer and Provider navigation/actions remain available where valid.
- Ownership and privacy checks still prevent access to another participant's private resources.

## Operational checks

- `/health/live` checks process liveness.
- `/health/ready` checks PostgreSQL connectivity.
- Production refuses placeholder secrets and incomplete proxy/email configuration.
- Forwarded headers are accepted only from the configured Caddy IP.
- Authentication and category-creation rate limits return `429` without exposing stack traces.
- Correlation IDs appear in responses and structured logs.
