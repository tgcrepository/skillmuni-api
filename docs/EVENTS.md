# Integration events · SkillMuni India Phase 1

The one list of cross-service events (CLAUDE.md §9). Each service declares its own copy of the records it publishes or
consumes (no shared contract assembly); field names here are the JSON (camelCase) contract. Every event also carries
`eventId`, `tenantId` and `occurredAt` (UTC, set by the outbox) from `IntegrationEvent`. Consumers are idempotent
(inbox on `eventId`) and ignore fields they don't know.

XP is credited only by Gamification, with ledger key `learnerId + reason + sourceId` (BRD §4.1). Keys below are
`reason:sourceId`; existing reasons in the Gamification code (e.g. `quest.step` + `{slug}:{part}`) stay as they are.

Learning events: `courseSlug` is the app's module (`/learn/:module`, e.g. `brain-vita`) and `moduleSlug` the app's topic
inside it (e.g. `non-verbal-reasoning`), as in BRD §4.3 ("Brain-Vita Boss: all Brain-Vita modules"). Topic slugs
repeat across courses, so `(courseSlug, moduleSlug)` is the unique key. `briefId` = `quizId` = `{course}~{module}~{index}`.

| Event | Publisher | Fields | Consumers → effect |
|---|---|---|---|
| `identity.learner.registered` | Identity | accountId, firstName, lastName, market | Learner (create profile), Gamification (player), Assessment, Careers (learner row, lazy otherwise); Learning creates its rows lazily |
| `identity.contact.verified` | Identity | accountId, kind, value | Learner |
| `identity.claim.consented` | Identity | claimId, accountId, shareProgress, verifyCertificate, applyForMe | Learner |
| `identity.claim.confirmed` | Identity | claimId, accountId | Learner → marks quests 1–5 done and publishes their step and quest events (BR-119). Gamification pays nothing for the claim itself |
| `identity.claim.declined` | Identity | claimId | — |
| `identity.consent.changed` | Identity | accountId, scope, policyVersion, granted | — |
| `identity.data-export.requested` | Identity | accountId | every service with learner data (export, when built) |
| `identity.learner.erased` | Identity | accountId | every service → erase the learner's rows |
| `learner.quest.step-completed` | Learner | learnerId, slug, quest, part, paid | Gamification → +20 when `paid` (BR-100/101), key `step:{slug}:{part}` |
| `learner.quest.completed` | Learner | learnerId, slug, quest, badge | Gamification → +50 (BR-102) + quest badge |
| `learner.profile.task-completed` | Learner | learnerId, task (`photo`, `skills`) | Gamification → +5 photo (BR-108), +10 skills (BR-109), once |
| `learner.job-profile.completed` | Learner | learnerId | Gamification → +50 mission (BR-110), key `mission:job-profile`; Careers → Launchpad mission tick |
| `learner.programme.enrolled` | Learner | learnerId, programmeCode, paysStipend | Gamification → +100 once per learner (BR-113), key `programme:once` |
| `learner.feedback.sent` | Learner | learnerId, mood | Gamification → +5 at most once per 30 days (BR-114), decided by Gamification |
| `learner.photo.changed` | Learner | learnerId, photoUrl | Identity → `AuthUser.photoUrl` (subscription `identity`); Gamification → Arena photo |
| `learner.identity.link-changed` | Learner | learnerId, document, linked | — |
| `cv.parse.completed` | CV | cvId, learnerId, outcome, prefill | Learner → pre-fill unanswered quest fields |
| `cv.document.deleted` | CV | cvId, learnerId | Learner |
| `assessment.island.cleared` | Assessment | learnerId, attempt, island (1–3), firstAttempt | Gamification → +25 when firstAttempt (BR-104), key `assessment:island:{n}` |
| `assessment.attempt.submitted` | Assessment | learnerId, attempt, firstAttempt, archetypeId, archetypeName, archetypeEmoji, archetypeSummary, topDrivers[] (top 3 driver names, highest first), topDriverIds[] (the same as A–I) | Gamification → +25 when firstAttempt (BR-105), key `assessment:submitted`, Career Clarity badge progress; Careers → Launchpad mission; Learning → first move "Discover your Career Drivers" |
| `learning.activity.recorded` | Learning | learnerId, minutes (1–60 per report, at most 1440 per local day) | Gamification → daily quest "Learn for 15 minutes" (BR-118) |
| `learning.brief.completed` | Learning | learnerId, briefId, moduleSlug, courseSlug, firstCompletion (sent once per brief, on its quiz's first submit, so always true) | Gamification → daily quest "Finish Brain-Vita" when courseSlug = `brain-vita` and firstCompletion (B-44); badge progress |
| `learning.quiz.submitted` | Learning | learnerId, quizId, briefId, moduleSlug, courseSlug, correct, total, firstAttempt (every submit) | Gamification → +1 per correct on firstAttempt (BR-106), key `quiz:{quizId}`; streak day (§4.4, first-attempt submits only, learner's local date, B-45); daily quest "Score 80%+" |
| `learning.module.completed` | Learning | learnerId, moduleSlug, courseSlug (once, when every brief of the topic is done) | Gamification → badge progress |
| `learning.course.completed` | Learning | learnerId, courseSlug (once, when every brief of every topic is done) | Gamification → badge progress |
| `learning.first-moves.claimed` | Learning | learnerId | Gamification → +50 (BR-107), key `first-moves:once` |
| `careers.application.sent` | Careers | learnerId, applicationId, jobId | Gamification → +100 on the first (BR-111), key `mission:first-application` |
| `careers.application.withdrawn` | Careers | learnerId, applicationId | — (XP never decreases, BR-115) |
| `careers.fest.booked` | Careers | learnerId, bookingId, festId | Gamification → +100 on the first (BR-112), key `mission:first-fest-booking` |
| `careers.fest.cancelled` | Careers | learnerId, bookingId | — |
| `gamification.xp.awarded` | Gamification | learnerId, totalXp, level, rank, levelStartXp, nextLevelXp, badges, streakDays | Learner (profile header), Careers (Launchpad) |
| `gamification.badge.earned` | Gamification | learnerId, badgeId, badgeName | — |

Queues (not topics): `notifications` (Identity → Platform): one message per SMS / email / push to send.
