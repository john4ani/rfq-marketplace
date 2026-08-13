# Task 3D-D verification checkpoint

Date: 2026-08-13

## Scope verified

- Company membership remains the authorization boundary for provider messaging, opportunities, offers, Trust facts and claim actions.
- Commercial company identifiers remain distinct from human User identifiers.
- The shared email transport uses the configured Google Workspace SMTP Relay settings only when `Email:Enabled=true`.

## Automated evidence

- The pre-existing compiled integration-test regression selection passed: 7 passed, 0 failed. It covers production-readiness, supplier discovery, offer, open-request and company-membership foundations/authorization. It is retained as a smoke signal only: the source changed after that binary was produced, and a fresh compile was blocked as noted below.
- The frontend production build passed. It reported eight existing stylesheet size-budget warnings; no build failure.
- `terraform fmt -check` for staging passed.
- Production readiness now rejects an enabled-email configuration whose verification link is not HTTPS on the configured frontend origin. The deployment compose file supplies `/verify-email` from `FRONTEND_ORIGIN`.

## Not verified automatically in this checkpoint

- A retry of `dotnet build Marketplace.slnx --no-restore` compiled Domain, Application, Infrastructure and API successfully, then failed only while test projects attempted to write protected `obj` cache/manifest files (`Marketplace.Domain.Tests`, `Marketplace.Application.Tests`, and `Marketplace.IntegrationTests`). No running process was stopped.
- Migration-script generation could not run because the API build output (`Marketplace.Api.deps.json`) is absent. Task 3D-D contains no schema migration; it is compatible with the Task 3D-A `CompanyMembership` migration.
- Local browser smoke check passed for the authenticated shell: `/dashboard`, `/requests` and `/account` rendered without visible error states or browser console errors.
- No real SMTP email was sent. The deployed runtime configuration has not yet been updated with the new `Email__*` parameters, and relay delivery/DNS records cannot be established from source code alone.

## Delivery configuration required before real-email verification

Set the documented `Email__*` values in the deployment environment, including `Email__Enabled=true`, and deploy the compose/render/Terraform configuration. Google Workspace must permit the deployment server IP to use the relay. Then send a password-reset and an email-confirmation request to a controlled inbox and verify the sender, Reply-To and public links.

In-app notifications and supplier invitations deliberately remain in-app/admin flows. They do not send email until a separate notification-policy slice authorizes that behaviour.
