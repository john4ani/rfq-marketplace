# ADR-012: SEAP published documents — import resolution, notice-id persistence and on-demand resolution

- Status: accepted
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

- **At import**: the ingestion resolves and attaches the documents (as before).
- **On demand**: `POST /api/v1/seap/tenders/{id}/documents` resolves the documents for a tender that has a
  `noticeId` but no attachments yet (idempotent — returns the current list when attachments exist). The
  SPA fires it automatically once per page view when the „Documente Originale" tab is opened with an empty
  list, shows a real skeleton while it runs, and surfaces a recoverable 503 + „Reîncearcă" state on failure.
- **Self-heal backfill**: the on-demand „Caută în SEAP" search re-finds an already-imported tender and
  backfills the notice id/type (`BackfillNoticeSource`, idempotent) and refreshes the documents.

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
