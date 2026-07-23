# Terraform Infrastructure Diagram

Status: Proposed

```mermaid
flowchart LR
    operator["Operator / GitHub Actions"]

    subgraph Bootstrap["Bootstrap state (applied separately)"]
        stateBucket["Private versioned S3 state bucket<br/>Encryption + native lockfile"]
    end

    subgraph Environment["Environment root: dev or prod"]
        root["Environment composition<br/>providers, versions, variables"]
        lightsail["module.lightsail-host"]
        frontend["module.frontend"]
        attachment["module.attachments"]
        dns["module.dns"]
        email["module.email"]
        oidc["module.identity-federation"]
    end

    operator --> Bootstrap
    operator -->|"terraform init/plan/apply"| root
    root -->|"Separate environment state key"| stateBucket
    root --> lightsail
    root --> frontend
    root --> attachment
    root --> dns
    root --> email
    root --> oidc
    frontend -->|"Distribution domain and certificate validation"| dns
    lightsail -->|"Static IP"| dns
    email -->|"SES verification and DKIM records"| dns
    oidc -->|"Least-privilege deploy roles"| operator
```

## Terraform/application boundary

Terraform owns:

- Lightsail instance, static IP, firewall, and minimal cloud-init;
- private frontend and attachment buckets;
- CloudFront, origin access control, cache/error behavior, and headers;
- Route 53 records and certificate validation;
- SES identity and DNS verification records;
- CI OIDC provider and least-privilege roles;
- remote state controls.

Terraform does not:

- build application code;
- embed application secrets in user data;
- use routine `remote-exec`;
- permanently deploy the API;
- run normal database migrations;
- upload each frontend release.

## Environment isolation

- `dev` and `prod` are separate Terraform roots and state keys.
- Environment-specific values are passed through non-secret reviewed variables.
- Secret `.tfvars`, state, plans, and `.terraform/` are excluded from Git.
- Provider and Terraform versions are constrained.
- State uses S3 versioning, encryption, and `use_lockfile = true`.
