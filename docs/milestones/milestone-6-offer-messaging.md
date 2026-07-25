# Milestone 6 — Private Offer Messaging

Status: Complete

## Outcome

The Buyer and the Provider attached to a submitted offer can hold a persistent private conversation without leaving the offer workflow. Both participants can open the conversation from their offer view, send messages, see new messages within a few seconds, and return to the relevant request.

## Access and privacy

- A conversation belongs to one submitted offer.
- Only the RFQ Buyer and that offer's Provider can read or send messages.
- Unauthorized or unknown conversation URLs return `404` so offer participation is not disclosed.
- Draft offers do not create a messaging channel.
- Messages are limited to 2,000 characters and empty messages are rejected.

## API

```text
GET  /api/v1/offers/{offerId}/conversation
POST /api/v1/offers/{offerId}/conversation/messages
POST /api/v1/offers/{offerId}/conversation/read
```

`GET` accepts an optional `after` timestamp. The active conversation polls every five seconds using the last received timestamp, while navigation away from the page stops polling automatically.

## Notifications and unread state

- Sending a message creates an in-app notification for the other participant.
- The notification links directly to `/offers/{offerId}/conversation`.
- The conversation persists separate Buyer and Provider read timestamps.
- Opening a conversation marks incoming messages as read.
- Notification deduplication uses the unique message identifier.

## UX

- Buyer offer cards and submitted Provider offer rows expose a clear **Mesaje** action.
- The conversation header keeps request context and provides a role-aware back link.
- Empty, loading, inaccessible, and send-error states provide an explicit next action.
- Messages distinguish the current participant visually and retain whitespace safely.
- The composer has a visible label, character counter, disabled empty-submit state, and responsive mobile layout.

## Verification

- Domain tests cover message validation and participant-only read state.
- Integration coverage exercises message sending, conversation retrieval, read state, notification creation, and non-disclosing missing-resource behavior.
- Angular service tests verify polling cursors, sending, and marking read.
- The production API and Angular builds complete successfully.

## Deferred

Attachments remain private and belong to a later milestone. SignalR/WebSockets are deferred until measured polling traffic or delivery latency justifies them, as recorded in ADR-0006.
