# SEAP Golden Payloads

Five golden payloads for the `Anunțuri SEAP` parsing suite. They are **real e-licitatie.ro responses**
captured from the public notice-search API on 2026-08-23:

- Endpoint: `POST https://e-licitatie.ro/api-pub/NoticeCommon/GetCNoticeList/`
- Body: `{ "sysNoticeTypeIds": [2], "sortProperties": [], "pageSize": N, "pageIndex": 0 }`
- Envelope: `{ "total", "items", "searchTooLong" }`

> **Provenance:** the anonymous public API serves a stale, capped dataset (2019 records, `total: 3000`) —
> a real limitation of the public endpoint, not of these files. The **structure is the real wire contract**.
>
> - `seap-notices-full.json` — verbatim capture of three real items.
> - `seap-notices-optional-missing.json` — real item with `estimatedValueRon`, `cpvCodeAndName`,
>   `minTenderReceiptDeadline`, `maxTenderReceiptDeadline` removed (derived from a real capture).
> - `seap-notices-no-cpv.json` — real item with `cpvCodeAndName` removed (derived; the NullReferenceException regression case).
> - `seap-notices-null-collections.json` — real item with those optional fields explicitly `null` (derived).
> - `seap-notices-html-error.html` — the real e-licitatie.ro SPA page (verbatim).

The parser must tolerate every optional-field variant without NullReferenceException and degrade non-JSON
payloads (HTML, error arrays) to an empty result.
