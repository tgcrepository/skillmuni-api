# Learning service

Learn modules, topics, briefs and quizzes; the Lobby feed with "Your first moves"; My Stats → Learning; learning
minutes. Schema `learning`. XP is shown here but credited only by Gamification, from this service's events.

## Endpoints (`/api/v1`, learner session)
| Method | Path | Notes |
|---|---|---|
| GET | `learn/modules/{module}` | API-097 `ModuleDetail` + optional `resume` (B-44, below); 404 `module-not-found` |
| GET | `learn/modules/{module}/topics/{topic}` | API-098 `TopicDetail`; 404 `topic-not-found` |
| PUT | `learn/briefs/{id}/bookmark` | API-099 `{ on }` → 204; 404 `brief-not-found`; 422 `validation` without `on` |
| PUT | `learn/briefs/{id}/feedback` | API-100 `{ helpful: true \| false \| null }` → 204; 404 `brief-not-found` |
| GET | `learn/modules/{module}/topics/{topic}/quiz` | API-101 `Quiz` of the current brief (the last brief, as practice, once all are done); 404 `topic-not-found` |
| PUT | `learn/quizzes/{id}/draft` | API-102 `{ answers }` → 204; 404 `quiz-not-found`; ≤ 50 answers |
| POST | `learn/quizzes/{id}/submit` | API-103 `QuizResult`; `[Idempotent]`; 404 `quiz-not-found`; 422 `quiz-locked` (a brief after the current one) |
| POST | `learn/activity` | MISS-04 `{ minutes }` 1–60 → 204; `[Idempotent]`; capped at 1440 min per local day. **Not called by the app yet** |
| GET | `home` | API-063 `HomeFeed` |
| POST | `home/first-steps/{id}` | API-061 → 204; `jobs` is recorded, `assessment` / `learn` complete by doing them; 404 `step-not-found` |
| POST | `home/first-moves/claim` | API-062 `{ xp }`: 50 once all three moves are done, 0 when already collected; 409 `first-moves-not-done`; `[Idempotent]` |
| GET | `stats/learning` | API-130 `LearningDashboard` |

Ids are the mock's: brief id = quiz id = `{module}~{topic}~{index}` (0-based index).

## Rules
- **Done brief:** a brief is done from the first submit of its quiz, whatever the score (the mock's rule). The first
  brief not done is `current`; later ones are `locked`. Quizzes are taken in order (422 `quiz-locked`).
- **Quiz XP (BR-106):** +1 per correct answer on the first submit only; retakes are practice (0). The response shows
  the burst; Gamification credits it from `learning.quiz.submitted` (key `quiz:{quizId}`).
- **Questions per brief:** a topic with a multi-question quiz uses it for every brief; otherwise earlier briefs get
  its first question and the last brief the full quiz (the mock's rule).
- **Resume point (B-44):** `ModuleDetail.resume = { topic: { slug, title }, brief: { id, index, title }, link }` is
  the current brief of the topic the learner last checked a quiz in, or of the first unfinished topic; null once the
  module is done. `link` is the topic route (`/learn/{module}/{topic}`), which opens at the current brief. The Lobby's
  "Pick up where you left off" card uses the same rule across modules (null for a learner who hasn't started).
- **First moves (BR-42, BR-107, A-266):** shown until the bonus is collected. Assessment = `assessment.attempt.submitted`
  received; learn = any brief done; jobs = `POST home/first-steps/jobs`. The claim returns 50 once and publishes
  `learning.first-moves.claimed`; Gamification credits +50 (key `first-moves:once`).
- **Daypart:** from the market time zone (IN Asia/Kolkata, ZA Africa/Johannesburg).
- **My Stats:** one course per catalogue module, sorted in progress (highest % first), not started, done. Percent has
  two decimals rounded down. `xpOnFinish` = quiz XP still available. `currentId` = module of the last quiz checked, if
  in progress. `nudge` for an in-progress module ≥ 90%.

## Content
Static catalogue in `Infrastructure/Content/SeedLearningContent.cs` behind `ILearningContent` (a CMS provider can
replace it): Brain Vita and Digital Basics with the design copy and quizzes, every other module a Lobby tile links to
with the mock's generic content (2 topics × 3 briefs), plus the Lobby pathways and Knooks.

## Events
Publishes (`courseSlug` = the app's module, e.g. `brain-vita`; `moduleSlug` = the app's topic inside it):
`learning.quiz.submitted` (every submit, `firstAttempt`), `learning.brief.completed` (first submit of a brief, once,
`firstCompletion` true), `learning.module.completed` (every brief of a topic done, once), `learning.course.completed`
(every brief of a module done, once), `learning.activity.recorded`, `learning.first-moves.claimed`.
Consumes (subscription `learning`): `identity.learner.erased` (deletes the learner's rows),
`assessment.attempt.submitted` (first move "Discover your Career Drivers").

## PII
None. Rows hold the learner id, content ids, scores, bookmarks, ratings, minutes and timestamps.

## Config
| Key | Where |
|---|---|
| `ConnectionStrings:Learning` | Key Vault `learning-db-connection` |
| `Redis:Host` (Azure, managed identity) or `ConnectionStrings:Redis` (compose) | Idempotency-Key store; memory if neither |
| `Learning:FirstMovesXp` (50), `CareerDriversXp` (100), `MaxDailyMinutes` (1440), `NudgeAtPercent` (90), `TimeZones` | optional overrides |
| `ServiceBus:Subscription` | `learning` |

## Gaps
- `rewardsReady` and `unreadNotifications` on the Lobby are 0 (vouchers are not in Phase 1; notifications belong to
  Platform).
- The assessment first move always shows "36 quick this-or-that picks · 6 min" (no "N of 36 picked" until Assessment
  publishes progress).
- The app doesn't call `POST learn/activity` yet (MISS-04).

## Erasure tombstone (MISS-08)

On `identity.learner.erased` the learner's rows are deleted and a tombstone stays in `erased_learners`
(`learner_id`, `erased_at` — nothing else, no tenant, no personal data; shared building block
`AddErasureTombstones<TContext>()`). An access token stays valid for up to 10 minutes after erasure, so:

- every request from an erased account gets **410 `learner-erased`** (`ErasedAccountMiddleware`, SkillMuni.Web) instead
  of recreating an empty row;
- an event about an erased learner (any event implementing `ILearnerEvent`) delivered late is recorded in the inbox and
  skipped.

## Lobby state row

`lobby_states` holds one row per learner (`ux_lobby_states_learner`), created on first use by the Home lobby request or
by `assessment.attempt.submitted`. The create is an insert-if-absent (`INSERT … ON DUPLICATE KEY UPDATE`) followed by a
read, so both arriving together never hit the unique key.
