# ADR-0023: Factual trust foundation

## Status

Accepted — 2026-08-07

## Context

CompaSpot needs useful assurance for early marketplace participants, without implying a reputation that the product cannot yet support. The MVP has no complete dispute, fulfilment, rating, or appeals workflow.

## Decision

The product records only factual account indicators: Identity email confirmation; Administrator-confirmed phone and company states; a private, explainable profile-completeness checklist; and factual participation counts. Each Administrator review records the reviewer and timestamp. Profile saves and verification reviews are append-only account audit events.

An Administrator must base confirmation on a real contact or document. The application does not infer verification from a filled-in field, a discovered company, or an offer.

## Consequences

Trust facts appear only where they reduce a commercial decision's uncertainty: Provider request discovery/detail, Buyer offer comparison, and the full Profile trust section. Compact cards show at most three relevant facts. A numeric score, public rating, badge, ranking, referral reward, and automated verification stay deferred under ADR-0012. Feedback facts are introduced only after feedback is tied to a confirmed completed collaboration.
