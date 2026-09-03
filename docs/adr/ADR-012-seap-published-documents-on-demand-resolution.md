# ADR-012: SEAP published documents — import resolution, notice-id persistence and on-demand resolution

- Status: accepted — amended 2026-09-03 (Direct Hit dossier exception, see below)
- Date: 2026-08-31
- Area: SEAP (e-licitatie.ro integration)

## Decision

Published documents (Fișa de date, Caiet de sarcini, formulare, model contract) are resolved from the
public e-licitatie.ro notice-document API — `GET /api-pub/NoticeCommon/GetDfNoticeSectionFiles/?initNoticeId=&sysNoticeTypeId=`
(the same call the SPA bundle uses) — and stored as `SeapTenderAttachment` rows holding the **remote**
e-licitatie.ro file URL; the download endpoint proxies them on demand with a same-origin `Referer`.

Because the notice-search payload does **not** carry the documents, the numeric SEAP `noticeId` and
`sysNoticeTypeId` are now **persisted on `SeapTender`** (additive migration `AddSeapNoticeIds`). This makes
the documents resolvable again later, without re-searching SEAP:

- **At import**: the background ingestion resolves and attaches the documents (as before). The on-demand
  „Caută în SEAP" search **defers** document resolution: a user-initiated search can import up to
  `OnDemandSearchMaxMatches` (default 500) tenders per request, and paying one notice-detail HTTP call per
  match inside the request thread is what kept the search both slow and narrow. Search-imported tenders
  therefore resolve their documents through the on-demand path below.
- **On demand**: `POST /api/v1/seap/tenders/{id}/documents` resolves the documents for a tender that has a
  `noticeId` but no attachments yet (idempotent — returns the current list when attachments exist). The
  SPA fires it automatically once per page view when the „Documente Originale" tab is opened with an empty
  list, shows a real skeleton while it runs, and surfaces a recoverable 503 + „Reîncearcă" state on failure.
- **Self-heal backfill**: the on-demand „Caută în SEAP" search re-finds an already-imported tender and
  backfills the notice id/type (`BackfillNoticeSource`, idempotent) so its documents stay resolvable on
  demand; it does not re-fetch the documents during the bulk search.

## Motivation

The notice-search payload does not include the published documents, so they must come from a second API
call that needs the numeric notice id. Tenders imported before this feature existed (or before the notice
id was persisted) have no documents and no way to resolve them without re-searching — and the platform's
filtered search can be temporarily unavailable or return partial slices, so the search is not a reliable
healer. Persisting the notice id decouples document resolution from the search.

## Alternatives considered

- **On-demand resolution through the filtered search only**: rejected — the filtered search currently
  errors server-side (`Eroare de sistem`, NRE) and its result slice is unstable; a stored notice id makes
  resolution independent of the search.
- **Downloading documents into local storage at import**: rejected earlier — remote files are proxied on
  demand (same-origin `Referer` requirement) instead of duplicating them locally.
- **Resolving documents inside the detail GET**: rejected — a state-changing side effect inside a GET is an
  anti-pattern; the SPA triggers an explicit, idempotent POST when the tab needs it.

## Consequences

- New imports carry `noticeId`/`sysNoticeTypeId`; the „Documente Originale" tab self-resolves the documents
  on first open — no manual action.
- Tenders imported **before** the migration have no notice id yet; they get one (and their documents) when
  the on-demand search re-finds them. Until then the tab shows the honest empty state.
- A failed document fetch returns 503 with a friendly message and a deliberate „Reîncearcă" action; the
  tender stays fully usable without documents.
- The DUAE documents (section „Documente DUAE") are still **not** returned by the discovered
  `GetDfNoticeSectionFiles` endpoint — a documented platform limitation.

---

## Amendment 2026-09-03 — Direct Hit dossier stores originals durably (Stage 1, DocumentImport)

### Context

The Direct Hit resolve flow builds a **stable dossier of the notice** that later stages (AI digest, offer
generation) must be able to reuse even when e-licitatie.ro becomes temporarily unavailable or replaces a
file upstream. That contradicts the on-demand proxy default above, so a **limited, deliberate exception**
is introduced for Direct Hit only.

### Decision

- **Ordinary SEAP browsing stays unchanged**: notices imported through the poll/„Caută în SEAP" paths keep
  remote references and stream the files from e-licitatie.ro on demand (the on-demand proxy above).
- **Direct Hit resolves run a Stage 1 `DocumentImport` ingestion run** (one run per tender,
  `SeapIngestionRun.Purpose = DocumentImport`): the original published documents are downloaded from SEAP
  and **durably stored** in the dedicated dossier storage (`SeapOriginalDocumentStore`, key namespace
  `seap-originals/{tenderId}` — a storage/namespace logically separate both from user uploads and from the
  browsing copies under `seap/{tenderId}`).
- Each attachment is stamped per-file with a functional `DocumentGroup` and a durable `DownloadStatus`
  (`Downloaded`/`Failed`, column defaults `None`), then the run reports `Ready` (all originals stored),
  `Partial` (some stored, some failed — remote references kept for on-demand fallback) or `Failed`
  (nothing could be stored).
- **The AI digest is deliberately NOT part of this stage**: a `DocumentImport` run never extracts text and
  never calls the LLM. Document digests, text extraction and the analysis run belong to a later stage.
- Out of scope today (later stages): the second automatic run, automatic AI analysis, a reimport button,
  P7S extraction and offer generation.

### Consequences

- Direct Hit tenders get a durable, content-hashed dossier that survives SEAP unavailability/upstream
  changes; repeat Direct Hit resolves of the same notice are idempotent for Stage 1 (a terminal
  `DocumentImport` dossier is reported as-is, no reimport loop).
- Storage cost grows only for Direct Hit tenders; the logical separation guarantees the browse path and the
  user-upload path are unaffected.

---

## Amendment 2026-09-03 — Stage 1 hardening: DUAE grouping, controlled download, idempotency, size limits

### Decisions

- **Functional groups** now include a dedicated **`Duae`** group (the platform's „Documente DUAE" section and
  the published `DUAE_CERERE_*.xml` forms) and a **`Corrigenda`** group. Classification prefers the
  SEAP-provided section/type label when the notice-detail payload carries one (parsed from the optional
  `noticeDocumentSection` property) and falls back to the published file name / upstream document id
  (`SeapDocumentClassifier`); diacritics-insensitive matching keeps „Fișa de date"/„FISA DE DATE" identical.
- **Honest storage model**: the legacy single `S3Url` column is split (migration
  `AddSeapDocumentImportStage1Hardening`, data-preserving) into **`SourceUrl`** (the original e-licitatie.ro
  URL, provenance, kept untouched when the dossier stores a copy) and **`StorageReference`** (a
  `local://`/`s3://` object reference — browsing copy or dossier original). No property is misnamed `S3Url`
  anymore and the platform source is never lost when a dossier object replaces a browsing copy.
- **Controlled dossier download**: `GET /api/v1/seap/tenders/{tenderId}/documents/{attachmentId}/download`
  streams the EXACT stored bytes of a `Downloaded` original (authorization + IDOR-guard + original file name
  from the attachment row + stored content type). A signed `.p7s` is served untouched — never unwrapped.
  `SeapTenderAttachmentRef.DownloadUrl` points at this route only for durably stored originals; everything
  else keeps the browsing route. The „Documente Originale" tab now groups files by `DocumentGroup`, shows the
  original file name, per-file `Downloaded`/`Failed` state (with the machine-readable failure reason), the
  stored size and a „Descarcă originalul" action, plus a „Dosar parțial" warning when some files failed.
- **DB-level idempotency**: a unique index on `(TenderId, SourceDocumentId)` guarantees one row per upstream
  document per tender even when two resolve/fetch executions race (multiple NULL source ids remain allowed for
  legacy browsing copies); the migration also de-duplicates any pre-existing duplicates.
- **Size limits**: `Seap:MaxOriginalDocumentBytes` (default 100 MB) and `Seap:MaxTenderOriginalDocumentsBytes`
  (default 1 GB) bound the dossier. The per-file cap is checked from the declared length AND while streaming —
  an over-limit upload is aborted with no partial object left (the local storage layer deletes the partial
  file; the R2 layer buffers before `PutObject`, so nothing is created). Crossing the tender cap deletes the
  over-quota object again and marks the file `Failed`. Failures carry explicit codes
  (`OriginalDocumentTooLarge`, `DossierQuotaExceeded`, `DownloadTimeout`, …) persisted on
  `SeapTenderAttachment.DownloadErrorCode`.

