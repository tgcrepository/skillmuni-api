# Assessment service

Career Drivers ("Muni's Balance", BRD §3.7): 36 "this or that" pairs on a 4-notch see-saw, 4 islands of 9 pairs,
scoring into 9 drivers and 6 archetypes, attempts and retakes. Behaves like the app's `src/mocks/assessment.handlers.ts`.
Also serves My Stats → Assessments. Schema `assessment`.

## Endpoints (`/api/v1`, learner session)
| Method | Path | Notes |
|---|---|---|
| GET | `assessments/career` | API-004; `AssessmentState`: pairs, islands, answers (`0`–`3`, `"skip"`, `null`), current, xp, earnsXp, remind, attempts. `status` new · in-progress · done. A new learner gets the intro state without a row being written |
| PUT | `assessments/career/answers` | API-005 auto-save `{ answers[36], current }` (absolute values, BR-133) → `{ xp, xpEarned }`. Clearing island 1–3 shows +25 on the first attempt. 422 `validation` (not 36 answers, current outside 0–36), 400 `malformed-request` (an answer that isn't 0–3, "skip" or null) |
| POST | `assessments/career/submit` | API-006 `{ answers[36] }` → `AssessmentResult`. 422 `incomplete` unless all 36 are answered (skips count as unanswered, BR-54). `[Idempotent]` |
| GET | `assessments/career/result?attempt=n` | API-007; unknown or missing `attempt` = the latest (as the mock). 404 `result-not-found` before the first submit |
| POST | `assessments/career/restart` | API-008 → `AssessmentState`: answers cleared, islands already cleared stay cleared (never pay twice). `[Idempotent]` |
| POST | `assessments/career/retake` | API-009 → `AssessmentState`: new attempt, XP 0, islands reset; with no submitted attempt it is a restart. `[Idempotent]` |
| PUT | `assessments/career/remind` | API-010 `{ remind }` → `{ remind }` |
| GET | `stats/assessments` | API-131 `AssessmentsDashboard`: the Skill Stats empty state (`results: []`, 20 available, +20 first card, Muni's pick Digital Skills). Skill assessments don't exist in Phase 1 |

## Rules
- **Content** (`Content/career-drivers.json`, embedded): the 36 pairs, 4 islands, 9 drivers and 6 archetypes verbatim
  from the Career Drivers Survey 2025 (via `assessment.data.ts`), behind `ICareerDriversContentProvider` so a CMS can
  replace it. Checked at start-up (drivers A–I, pair order, island ranges).
- **Scoring** (`CareerDriversScoring`, BR-50–52): notch n → this 3 − n, that n; 0–24 per driver, 108 in all. Ranking:
  highest first, ties A → I. Archetype: highest sum of its 2 dominant drivers; ties → higher average of its secondary
  drivers (Strategic Leader adds the stronger of D or G), then table order (A-211, tie-break to confirm).
- **XP shown** (display only, Gamification credits from events): 25 per island 1–3 when its 9th pair is answered,
  25 on submit; first attempt only (100 in all, A-309); retakes and restarts after it show 0. Islands 1–3 still
  unpaid at submit (answers sent straight to submit) are cleared then. `Assessment:XpPerIsland` / `XpOnSubmit`.
- **Attempt date**: the learner's local date (IN Asia/Kolkata, ZA Africa/Johannesburg).
- **Reminder**: the flag is stored (`journeys.remind`, index for a reminder job). Sending the 6 PM nudge is the
  Platform notifications service's job; no event is published yet (gap).

## Events
Publishes `assessment.island.cleared` (learnerId, attempt, island 1–3, firstAttempt) and `assessment.attempt.submitted`
(learnerId, attempt, firstAttempt, archetypeId, archetypeName, archetypeEmoji, archetypeSummary, topDrivers = top 3
driver names, topDriverIds). Consumes `identity.learner.registered` (starts the journey row; requests also create it
lazily) and `identity.learner.erased` (deletes the learner's journey and attempts) on subscription `assessment`.

## Data and PII
`journeys` (one per learner, id = account id): answers in progress, current pair, islands cleared, XP shown, remind,
attempts. `attempts`: number, local date, scores A–I, archetype id, XP, the 36 answers. No names or contact data;
answers and results are personal data about motivation and are erased with the account.

## Config
| Key | Where |
|---|---|
| `ConnectionStrings:Assessment` | Key Vault `assessment-db-connection` |
| `Assessment:XpPerIsland`, `XpOnSubmit`, `MandatoryCourses`, `TimeZones`, `Stats:*` | optional (defaults from the mock) |
| `ServiceBus:Subscription` | `assessment` |
| `Redis:Host` (Azure, managed identity) or `ConnectionStrings:Redis` (compose) | Idempotency-Key store; memory if neither |
