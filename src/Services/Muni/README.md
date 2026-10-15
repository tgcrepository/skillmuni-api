# Muni service

Muni, the mascot guide, in the chat (Figma Muni Chat v2). Text only: Muni has no voice (BRD §3.20, A-314; MISS-15
closed for Phase 1). Schema `muni`.

## Endpoints (`/api/v1`, learner session)
| Method | Path | Notes |
|---|---|---|
| GET | `muni/welcome` | API-110; `{ firstName, intro, suggestions, topics, unread, nudge }`. New learners (no Career Drivers yet) get discovery suggestions; others Brain-Vita resume, jobs and their streak. `unread` 0 and `nudge` null until a real nudge source exists |
| POST | `muni/messages` | API-111; `{ text, intent? }` → `{ id, blocks, chips }` (≤ 3 chips; `id` is additive). 422 `validation` when both are empty. `Idempotency-Key` supported |
| POST | `muni/reveal` | API-112; `{ field: email \| mobile }` → `{ value }`, `Cache-Control: no-store`. 503 `reveal-unavailable` when Learner can't be reached; 422 `validation` for another field |
| PUT | `muni/messages/{id}/rating` | API-113; `{ helpful: true \| false \| null }` → 204. `id` is the reply id or any id the app made up (≤ 64 chars, else 422 `invalid-message-id`); `null` clears |
| GET | `muni/history` | API-114; array of `{ id, title, when, messages[{ from, text }] }`, newest first (muni.api.ts expects an array, not a page). Lines < `ConversationGap` apart form one conversation; `when` = Today · Yesterday · Mon · 12 Oct in the market's time zone |

## How replies are made (MISS-14)
`IMuniResponder` turns a question into blocks (`text`, `list`, `stats`, `profile`) and chips. Phase 1 ships
`KeywordMuniResponder`: the mock's keyword-intent catalogue as seed data (`Responders/MuniKnowledge.cs`), same order as
`muni.handlers.ts`: topic tiles → greetings → keyword-scored platform topics (or the fallback) → the fixed chips. An LLM
responder can replace it by registering another `IMuniResponder`; the API doesn't change. Differences from the mock:
- Personal values are real: name, XP, level, rank, streak, quests done and archetype from events; profile completeness
  and gaps from Learner.
- Sample data the mock invents is replaced by neutral copy (no named jobs, no "98.86% done", no "2 jobs match").
- Three keys added so chips reach their topic: `the arena`, `streaks`, `daily quests`.

## Data from other services
- Learner `GET internal/learners/{id}/profile-progress` (internal ingress, `X-Tenant-Id`): completeness and gaps.
  Unavailable → replies leave gaps out.
- Learner `POST api/v1/profile/reveal` with the caller's own access token forwarded: email and mobile for `reveal` and the
  profile card (masked as the mock does: `y••••@domain`, `••••••0697`). Never logged or cached.
- Both are typed HttpClients with the standard resilience handler (retry, timeout, circuit breaker). In Development /
  Testing with no `Muni:LearnerBaseAddress`, sandbox clients return the mock's values.

## Events
Publishes nothing. Consumes (subscription `muni`): `identity.learner.registered` (name, joined), `gamification.xp.awarded`
(totals and streak; lower totals arriving late are ignored), `learner.quest.completed` (quests-done bitmask),
`assessment.attempt.submitted` (archetype), `identity.learner.erased` (deletes the learner's copy, history and ratings).

## Retention
Chat lines and ratings older than `Muni:HistoryDays` (default 90; BRD §9 leaves it open) are deleted by a background
worker every `Muni:Cleanup:Every` (default 6 h), in batches, across tenants.

## PII
`learners.first_name`, `learners.last_name`; `messages.text` (whatever the learner typed). Private details are never stored.

## Config
| Key | Default / where |
|---|---|
| `ConnectionStrings:Muni` | Key Vault `muni-db-connection` |
| `Muni:LearnerBaseAddress` | `http://learner:8080/` (compose), `http://ca-learner/` (Azure); empty = sandbox (Development / Testing only) |
| `Muni:HistoryDays` · `ConversationGap` · `HistoryConversations` · `HistoryLines` | 90 · 00:30:00 · 20 · 400 |
| `Muni:Cleanup:Enabled` · `Every` · `Batch` | true · 06:00:00 · 1000 |
| `Muni:Levels`, `QuestStepXp`, `QuestCompleteXp`, `TimeZones` | display copies (Gamification is the source of truth) |
| `Redis:Host` or `ConnectionStrings:Redis` | Idempotency-Key store; memory if neither |
| `ServiceBus:Subscription` | `muni` |
