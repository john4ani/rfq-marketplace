# Deployment Diagram

Status: Implemented in code; AWS staging not yet provisioned

```mermaid
flowchart TB
    user["User browser"]
    github["GitHub Actions<br/>OIDC deployment identity"]

    subgraph AWS["AWS account / selected region"]
        route53["Route 53 hosted zone"]
        cert["ACM certificate<br/>Frontend domain"]

        subgraph Edge["AWS edge"]
            cloudfront["CloudFront distribution"]
        end

        frontend["Private S3 bucket<br/>Angular artifacts"]
        apiOriginDns["API origin DNS<br/>Route 53 or sslip.io"]
        attachments["Private S3 bucket<br/>User attachments"]
        parameters["Parameter Store<br/>Runtime configuration"]
        ses["SES identity and email transport"]

        subgraph Lightsail["Lightsail instance"]
            firewall["Lightsail firewall<br/>80/443 public; 22 admin CIDR"]
            caddy["Caddy container<br/>Public 80/443"]
            api["API container<br/>Internal port only; non-root"]
            postgres["PostgreSQL container<br/>Internal network only"]
            volume[("Named persistent<br/>PostgreSQL volume")]
            keys[("Persistent Data Protection<br/>key directory")]
        end

        backups["Encrypted off-host<br/>database backups"]
    end

    user --> route53
    route53 --> cloudfront
    cloudfront -->|"Default behaviour"| frontend
    cloudfront -->|"/api/* and /health/*"| apiOriginDns
    apiOriginDns --> firewall
    firewall --> caddy
    caddy --> api
    api --> keys
    api --> postgres
    postgres --> volume
    api --> attachments
    api --> parameters
    api --> ses
    volume -.->|"Scheduled encrypted backup"| backups
    cert --> cloudfront
    github -->|"Upload frontend + invalidate"| frontend
    github -->|"Deploy API package/image + migrate + health check"| Lightsail
```

## Deployment properties

- With a custom domain, `app.example.com` resolves to CloudFront. Domainless staging uses the generated CloudFront hostname.
- Angular uses relative `/api/*` URLs. CloudFront's default behaviour serves S3, while ordered `/api/*` and `/health/*` behaviours use Caddy as the API origin.
- An origin-only DNS name resolves to the Lightsail static IP. Domainless staging uses `<static-ip>.sslip.io` so Caddy can obtain origin TLS without buying a domain.
- The API and PostgreSQL ports are not exposed publicly.
- Caddy manages origin TLS; CloudFront uses ACM for the public frontend TLS.
- PostgreSQL data survives container restarts through a named volume.
- ASP.NET Core Data Protection keys survive API replacement in a host directory restricted to the container user. The MVP stores those keys on the encrypted host disk rather than adding a separate KMS integration.
- Backups must be stored off-instance and restoration must be tested before production.
- Terraform provisions infrastructure. Deployment automation delivers application artifacts and runs migrations.
- Production deployment requires a protected environment/manual approval.
- GitHub Actions receives temporary AWS credentials through OIDC; there are no long-lived AWS keys in GitHub.

## Availability constraint

API and database share one low-cost instance. Instance or disk failure can interrupt the entire transactional system. This is an explicitly accepted MVP constraint, mitigated by health checks, monitoring, backups, and a documented restore procedure—not by premature high-availability infrastructure.
