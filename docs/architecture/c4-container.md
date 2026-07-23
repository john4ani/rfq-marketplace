# C4 Level 2 — Container Diagram

Status: Proposed

```mermaid
flowchart TB
    subgraph Users
        browser["Web Browser<br/>Buyer, Supplier, Administrator"]
    end

    subgraph Edge["AWS edge and DNS"]
        dns["Route 53"]
        cdn["CloudFront<br/>HTTPS and SPA delivery"]
        frontendBucket["Private S3 frontend bucket<br/>Compiled Angular assets"]
    end

    subgraph Host["Amazon Lightsail instance"]
        proxy["Caddy reverse proxy<br/>TLS termination and secure headers"]
        api["ASP.NET Core API<br/>Modular monolith REST API"]
        worker["Hosted notification worker<br/>Runs inside API process"]
        database[("PostgreSQL<br/>Application data, Identity,<br/>outbox and audit")]
    end

    subgraph External["AWS managed services"]
        attachments["Private S3 attachment bucket"]
        ses["Amazon SES"]
        parameters["Systems Manager Parameter Store"]
    end

    browser -->|"Resolve domains"| dns
    browser -->|"Load Angular SPA"| cdn
    cdn --> frontendBucket
    browser -->|"REST JSON over HTTPS"| proxy
    proxy --> api
    api --> database
    worker --> database
    worker --> ses
    api --> attachments
    api --> parameters
```

## Container responsibilities

| Container | Responsibility |
|---|---|
| Angular SPA | Responsive UI, reactive forms, client routing, presentation state |
| Caddy | Public ports 80/443, automatic TLS, reverse proxy, headers |
| ASP.NET Core API | Authentication, authorization, use cases, domain rules, REST endpoints |
| Hosted worker | Claims notification rows, renders templates, sends email, retries safely |
| PostgreSQL | Transactional source of truth, Identity, outbox, audit |
| Frontend S3 + CloudFront | Private static origin and global HTTPS SPA delivery |
| Attachment S3 | Private binary object storage |
| SES | Transactional email transport |
| Parameter Store | Runtime secret/configuration source |

## Modular monolith boundary

The API contains the modules Identity, Buyers, Suppliers, Categories, RequestsForQuotation, Offers, Messaging, Attachments, Notifications, and Administration. These are code and ownership boundaries, not separately deployed processes.

The notification worker is shown separately to expose its responsibility, but it is hosted in the same API process for the MVP.
