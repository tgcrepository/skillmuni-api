# Gamification service

The only place XP is credited (CLAUDE.md §9). Turns events from Identity, Learner, Assessment, Learning and Careers
into XP, levels, badges, streaks and daily quests, and serves the Arena, the Rewards / badge screens and the learner's
progress. Schema `gamification`. BRD §4 is the rulebook: an action not in §4.1 earns nothing.

## Endpoints (`/api/v1`, learner session)
| Method | Path | Notes |
|---|---|---|
| GET | `me/progress` | API-125 `LearnerProgress`: highest quest finished in order, parts done in the next one, the ledger XP total (BR-138), earned badges, level, rank, next level's XP. Moved here from Learner |
| GET | `arena?scope&period` | API-001; scope `everyone` · `batch` (programme cohort) · `friends` (players you cheer); period `week` · `month` · `all`; anything else 422 `validation` |
| POST | `arena/players/{id}/cheer` | API-002; `{ on }` → `{ cheers }`; 422 `not-yourself`, 404 `player-not-found` |
| POST | `arena/players/{id}/challenge` | API-003; 204; 409 `challenge-open` (one 7-day race per pair) |
| GET | `stats/rewards` | API-129 Player HQ; wallet coins / credits always 0 (A-317, D-10) |
| GET | `badges` | API-132; 8 quest badges + 48 catalogue badges |
| PUT | `badges/showcase` | API-133; `{ ids }` ≤ 3 earned; 422 `too-many-pinned` / `badge-not-earned`; missing or null `ids` 422 `validation` |
| GET | `me/xp/ledger?page&pageSize` | MISS-01 |
| GET | `me/streak` | MISS-04 `{ days, best, week }` (no bonus: streaks earn no XP) |
| GET | `me/daily-quests` | MISS-03 / BR-118; credited automatically when done |

`stats/learning` and `stats/assessments` (API-130/131) belong to the Learning and Assessment services.

## Rules
- **Ledger:** `(learner, reason, source)` is unique, so an action pays once (BR-116). Amounts in `Gamification:Xp`.

  | BR | Reason · source | XP | Event |
  |---|---|---|---|
  | 100 / 101 | `quest.step` · `{slug}:{part}` | 20, only when `paid` | `learner.quest.step-completed` |
  | 102 | `quest.complete` · `{slug}` | 50 | `learner.quest.completed` |
  | 103 | (sum of the above) | 760 max | — |
  | 104 | `assessment` · `island:{1-3}` | 25, first attempt | `assessment.island.cleared` |
  | 105 | `assessment` · `submitted` | 25, first attempt | `assessment.attempt.submitted` |
  | 106 | `quiz.correct` · `{quizId}` | +1 per correct, first submit | `learning.quiz.submitted` |
  | 107 | `first-moves` · `once` | 50 | `learning.first-moves.claimed` |
  | 108 / 109 | `profile.task` · `photo` / `skills` | 5 / 10 | `learner.profile.task-completed` |
  | 110 | `mission` · `job-profile` | 50 | `learner.job-profile.completed` |
  | 111 | `mission` · `first-application` | 100, first only | `careers.application.sent` |
  | 112 | `mission` · `first-fest-booking` | 100, first only | `careers.fest.booked` |
  | 113 | `programme.enrolled` · `once` | 100, once per learner | `learner.programme.enrolled` |
  | 114 | `feedback` · `{eventId}` | 5, at most once per 30 days (decided here from the last `feedback` credit) | `learner.feedback.sent` |
  | 118 | `daily.{id}` · `{local date}` | Finish Brain-Vita 10 · Learn 15 min 5 · Score 80%+ 5 | `learning.brief.completed` (first completion, course `brain-vita`) · `learning.activity.recorded` · `learning.quiz.submitted` (first submit) |
  | 119 | the quest rows above | 550 for quests 1–5 | Learner publishes the step and quest events for a claimed learner |

  Nothing else pays (BR-139): the institute claim itself, streak days, Arena cheers and challenges, retakes, replays.
- **Levels:** A-249 ladder (Rookie 0 · Pathfinder 400 · Trailblazer 800 · Legend 1200 · Icon 1600).
- **Streak (§4.4, B-45):** a day counts when the learner submits a brief's quiz for the first time that day, in their
  local time (market time zone, IST / SAST) of the event's `occurredAt`. Retakes, practice, learning time and other XP
  don't count. Consecutive days grow it; a gap restarts it; a late event for an older day changes nothing. No XP.
- **Daily quests (BR-118, B-44):** "Finish Brain-Vita" links to `/learn/brain-vita` with the label "Resume where you
  left off" (the frontend / Learning resolves the resume point) and is done by the first completion of a Brain-Vita
  brief that day. Reset at the learner's local midnight.
- **Badges (§4.3):** counters on the player. Phase 1 awards only the 8 quest badges (Main Character, Link Up, Big Brain,
  Player Card, Storyteller, Arc, Receipts, Vibe Check). Catalogue badges show progress / locked and are awarded only
  when `Gamification:Badges:AwardCatalogue` is true (default false). Quest badges carry `image`
  `assets/quests/quest-N.png`; the quest in progress shows "X / Y parts" and "Continue quest".
- **Arena:** XP summed over the window from the ledger; ties go to whoever got there first; movement compares with
  the board at local midnight; "rank up" compares with the rank the learner saw last. League and promotion are
  placeholders (MISS-11); `rewards` is always `{ coins: 0, voucher: null }` (A-317).

## Events
Publishes `gamification.xp.awarded` (totals; Learner copies them) and `gamification.badge.earned`.
Consumes (subscription `gamification`, filter in `deploy/bicep/main.bicep`): `identity.learner.registered`,
`identity.learner.erased`, `learner.quest.step-completed`, `learner.quest.completed`, `learner.profile.task-completed`,
`learner.job-profile.completed`, `learner.programme.enrolled`, `learner.feedback.sent`, `learner.photo.changed`,
`assessment.island.cleared`, `assessment.attempt.submitted`, `learning.activity.recorded`, `learning.brief.completed`,
`learning.quiz.submitted`, `learning.module.completed`, `learning.course.completed`, `learning.first-moves.claimed`,
`careers.application.sent`, `careers.fest.booked`. Field names per `docs/EVENTS.md`.

## PII
`players.name`, `players.photo_url` (shown to the tenant's other learners on the Arena).

## Config
| Key | Where |
|---|---|
| `ConnectionStrings:Gamification` | Key Vault `gamification-db-connection` |
| `Redis:Host` (Azure, managed identity) or `ConnectionStrings:Redis` (compose) | boards cache and Idempotency-Key store; memory if neither |
| `Gamification:Xp`, `Levels`, `Quests`, `DailyQuests`, `Arena`, `TimeZones`, `FoundingUntil`, `FeedbackXpEvery` | optional overrides |
| `Gamification:Badges:AwardCatalogue` | `false` for launch (§4.3) |
| `ServiceBus:Subscription` | `gamification` |
