# Email transport

CompaSpot uses a single `IEmailSender` transport. Local development and automated tests keep `Email:Enabled=false`, so no SMTP connection is attempted. To make a deliberate local real-send test, set `Email__Enabled=true` and the relay values below before starting the API; password-reset and email-confirmation requests then send normally, while development still returns its safe development token response for debugging.

For the current MVP production transport, configure Google Workspace SMTP Relay through environment or deployment configuration:

```text
Email__Enabled=true
Email__Host=smtp-relay.gmail.com
Email__Port=587
Email__UseTls=true
Email__FromAddress=notifications@compaspot.com
Email__FromName=CompaSpot
Email__ReplyToAddress=support@compaspot.com
```

The expected Google Workspace relay authentication method is the deployment server public IP. Leave `Email__Username` and `Email__Password` empty for that configuration. If a relay later requires SMTP credentials, set both values only through secret deployment configuration; never commit credentials, passwords, tokens, or production IP addresses.

For a local real-send test, Google Workspace must separately authorize the local machine's current public IP, or the local process must use an approved SMTP-authenticated relay configuration. A relay configured only for the deployment server will reject a home/office IP; this is expected and should not be solved by committing credentials.

The existing password-reset and email-verification deliveries use this shared transport. Task 8 supplier invitation email uses this same sender after an Administrator explicitly selects **Trimite invitație email** for an approved opportunity; it never derives a recipient from a Company ID or User ID.

`SupplierOutreach__EmailCooldownHours` defaults to 72 and prevents duplicate successful outreach. `SupplierOutreach__PublicApplicationUrl` supplies the public invitation and opaque opt-out links. Each attempt is persisted before SMTP, then marked sent or failed after SMTP returns; failure is visible to the Administrator and can be retried. Contact suppression is checked for every future attempt, including a later rediscovery of the candidate. No token or email body is logged.

When email is enabled, `EmailVerification__ConfirmationPageUrl` must be an HTTPS URL on the configured frontend origin. Production compose derives it from `FRONTEND_ORIGIN` as `/verify-email`.
