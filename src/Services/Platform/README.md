# Platform service

Shared platform features: address lookup, the tenant's theme and market config (MISS-12), and notifications (MISS-06):
the bell, channel preferences and sending the SMS / email Identity queues. Schema `platform`.

## Endpoints (`/api/v1`)
| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `geo/{market}/postal/{code}` | anonymous | API-032; `{ region, district, city, areas[{ name, sub }] }`; 404 `postal-not-found` (the app lets the learner pick the state) |
| GET | `geo/{market}/regions` | anonymous | API-033; string array; unknown market → `[]` (as the mock) |
| GET | `tenant/theme` | anonymous | The app's `TenantTheme` `{ id, name, brand?, accent?, logoUrl? }` for the tenant in `X-Tenant-Id` (gateway, by host). Only known ramp steps with hex colours are returned |
| GET | `tenant/market?code=` | anonymous | The app's `MarketConfig` (+ `timeZone`); patterns are JS regex sources, `check` names a client check (`luhn`). Default = the tenant's market; 404 `unknown-market` |
| GET | `me/notifications?page&pageSize` | learner | `{ items[{ id, kind, title, body, link, read, createdAt }], page, pageSize, total, unread }`, newest first |
| PUT | `me/notifications/{id}/read` | learner | 204; 404 `notification-not-found` |
| GET | `me/notification-preferences` | learner | `{ push, whatsApp, sms, email }`; defaults push and email on, WhatsApp and SMS off (opt-in) |
| PUT | `me/notification-preferences` | learner | same body, all four required (422 `validation`) → saved preferences |

The app doesn't call the notification endpoints yet (the bell is a placeholder) or the tenant endpoints (theme and market
are client config today); paths and payloads follow the app's types so it can switch over.

Anonymous GETs are output-cached for 10 minutes per tenant header and `?code` (signed-in requests skip the output
cache); themes and postal answers are also cached in Redis (`Platform:CacheFor`).

## Reference data
- Postal codes and regions: `Geo/postal-starter.json`, embedded, behind `IPostalDirectory` — the mock's codes (IN 560038,
  400001; ZA 2194, 2001) plus a few city-centre codes, and all states / UTs and provinces. Replace with the India Post
  and SAPO directories by implementing the interface.
- Tenant themes: table `tenant_themes`, seeded with `skillmuni-in` and `skillmuni-za` (Skillmuni's own look: no ramp
  overrides, no logo). A tenant with no row gets `{ id, name: "Skillmuni" }`.
- Market config: `Platform:Markets` (defaults copied from the app's `market.ts`).

## Sending notifications
`NotificationQueueConsumer` reads the Service Bus queue `notifications` (Identity's `{ tenantId, channel, to, template,
data: { secret, validForMinutes } }`), picks the `INotificationSender` for the channel and records a
`notification_deliveries` row (recipient masked, never the secret; the queue message id stops a redelivery being sent
twice). Unreadable bodies are dead-lettered; a sender failure abandons the message so Service Bus retries. Providers are
open (BRD §9), so every channel (`sms`, `email`, `push`, `whatsapp`) uses the sandbox sender, which sends nothing and
logs only the template, channel and masked recipient. Sign-in and verification codes ignore preferences (the learner
asked for them).

## Events
Publishes nothing. Consumes (subscription `platform`): `gamification.badge.earned` → a bell notification once per badge;
`identity.learner.erased` → deletes the learner's notifications and preferences (delivery records hold no learner id).

## PII
None in clear. `notification_deliveries.recipient` is masked (`***3210`, `y***@gmail.com`).

## Config
| Key | Default / where |
|---|---|
| `ConnectionStrings:Platform` | Key Vault `platform-db-connection` |
| `Platform:CacheFor` · `DefaultThemeName` · `Markets` | 00:10:00 · Skillmuni · code defaults |
| `Platform:Notifications:QueueEnabled` | true (the consumer starts only when Service Bus is configured) |
| `ServiceBus:Subscription` · `NotificationsQueue` | `platform` · `notifications` |
| `Redis:Host` or `ConnectionStrings:Redis` | theme and postal cache; memory if neither |

## Erasure tombstone (MISS-08)

On `identity.learner.erased` the learner's rows are deleted and a tombstone stays in `erased_learners`
(`learner_id`, `erased_at` — nothing else, no tenant, no personal data; shared building block
`AddErasureTombstones<TContext>()`). An access token stays valid for up to 10 minutes after erasure, so:

- every request from an erased account gets **410 `learner-erased`** (`ErasedAccountMiddleware`, SkillMuni.Web) instead
  of recreating an empty row;
- an event about an erased learner (any event implementing `ILearnerEvent`) delivered late is recorded in the inbox and
  skipped.
