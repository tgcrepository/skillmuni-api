# Gamification service

The only place XP is credited (CLAUDE.md §9). Turns events from Identity, Learner, Assessment and Learning into XP,
levels, badges, streaks and daily quests, and serves the Arena and the Rewards / badge screens. Schema `gamification`.

## Endpoints (`/api/v1`, learner session)
| Method | Path | Notes |
|---|---|---|
| GET | `arena?scope&period` | API-001; scope `everyone` · `batch` (programme cohort) · `friends` (players you cheer); period `week` · `month` · `all`; 422 `invalid-scope` / `invalid-period` |
| POST | `arena/players/{id}/cheer` | API-002; `{ on }` → `{ cheers }`; 422 `not-yourself`, 404 `player-not-found` |
| POST | `arena/players/{id}/challenge` | API-003; 204; 409 `challenge-open` (one 7-day race per pair) |
| GET | `stats/rewards` | API-129 Player HQ |
| GET | `badges` | API-132; 8 quest badges + 48 catalogue badges |
| PUT | `badges/showcase` | API-133; `{ ids }` ≤ 3 earned; 422 `too-many-pinned` / `badge-not-earned` |
| GET | `me/xp/ledger?page&pageSize` | MISS-01 |
| GET | `me/streak` | MISS-04 |
| GET | `me/daily-quests` | MISS-03; credited automatically when done |

`stats/learning` and `stats/assessments` (API-130/131) belong to the Learning and Assessment services.

## Rules
- **Ledger:** `(learner, reason, source)` is unique, so an action pays once. Reasons: `quest.step` (20),
  `quest.complete` (50), `profile.task` (photo 5, skills 10), `programme.enrolled` (100), `feedback` (5, Learner
  decides the monthly rule), `assessment.career-drivers` (20, first attempt), `claim.confirmed` (100, flagged),
  `quiz.correct` (+1 per correct answer, first check only), `daily.<id>`. Amounts in `Gamification:Xp`.
- **Levels:** A-249 ladder (Rookie 0 · Pathfinder 400 · Trailblazer 800 · Legend 1200 · Icon 1600).
- **Streak:** a day (market time zone: IST / SAST) with learning or any XP. Consecutive days grow it; a gap restarts it.
- **Badges:** counters on the player; a badge is earned when its counter reaches the goal. Badges whose source
  isn't built yet (Knooks, CPD hours, skill cards, friends…) show as locked.
- **Arena:** XP summed over the window from the ledger; ties go to whoever got there first; movement compares with
  the board at local midnight; "rank up" compares with the rank the learner saw last. League, promotion and coins are
  placeholders (MISS-11, A-302).

## Events
Publishes `gamification.xp.awarded` (totals; Learner copies them) and `gamification.badge.earned`.
Consumes (subscription `gamification`): `identity.learner.registered`, `identity.claim.confirmed`,
`identity.learner.erased`, `learner.quest.step-completed`, `learner.quest.completed`, `learner.profile.task-completed`,
`learner.programme.enrolled`, `learner.feedback.sent`, `learner.photo.changed`, `assessment.attempt.submitted`*,
`learning.activity.recorded`*, `learning.brief.completed`*, `learning.quiz.submitted`*, `learning.module.completed`*,
`learning.course.completed`*. \* Payload proposed in `Application/Events.cs`; confirm when those services are built.

## PII
`players.name`, `players.photo_url` (shown to the tenant's other learners on the Arena).

## Config
| Key | Where |
|---|---|
| `ConnectionStrings:Gamification` | Key Vault `gamification-db-connection` |
| `Redis:Host` (Azure, managed identity) or `ConnectionStrings:Redis` (compose) | boards cache and Idempotency-Key store; memory if neither |
| `Gamification:Xp`, `Levels`, `Quests`, `DailyQuests`, `Arena`, `TimeZones`, `FoundingUntil` | optional overrides |
| `ServiceBus:Subscription` | `gamification` |
