# ADR-0007: Store Attachments in a Private S3 Bucket

Status: Proposed  
Date: 2026-07-23

## Context

RFQs and offers may include business-sensitive files. Uploading large files through the API would increase host bandwidth and memory pressure.

## Decision

Use a dedicated private S3 bucket with public access blocked and encryption enabled. The API authorizes and creates randomized metadata, issues short-lived presigned uploads, verifies the resulting object, and issues authorized short-lived download URLs.

## Alternatives considered

- Public bucket: rejected because files are private.
- Lightsail filesystem: rejected because it complicates durability, backup, and scaling.
- API-proxied uploads: not the default because it consumes instance resources.

## Consequences

Upload is a multi-step lifecycle and abandoned objects need cleanup. Extension, MIME type, size, and S3 metadata must all be checked.

## Security and operational implications

Authorization occurs before every URL issuance. Presigned URLs expire quickly. Virus scanning is a documented post-MVP gap.

## Revisit when

Add malware scanning before broader public adoption or when compliance/risk assessment requires it.
