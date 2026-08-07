# Pasul 8 — Trust foundation

Pasul 8 adds a small, honest trust layer: private account verification state, profile completeness, participation facts, Administrator verification review, and append-only audit events.

Verification is not inferred from profile data or supplier discovery. Phone and company state can only be changed by an Administrator; every change identifies the reviewer and time. Email confirmation remains the Identity state and is displayed as such.

Trust facts are shown contextually in Provider request discovery/detail, Buyer offer comparison, and the full Profile. No public score, rating, tier, badge, ranking, or gamification is part of this step. Feedback requires a confirmed completed collaboration and remains deferred under ADR-0012.

The database migration `20260807135925_AddTrustFoundation` creates only trust and audit tables. It does not alter existing accounts, requests, offers, categories, or discovery data.
