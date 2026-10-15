# Careers service

My Jobs, Hire Fest, the Career Launchpad and My Matches. Schema `careers` holds per-learner state only; the catalogue
(jobs, employers, fests, slots, sectors, match cards) is seed data copied from the frontend mocks, served through
`IJobCatalogue`, `IFestCatalogue` and `ICareerCatalogue` so an employer portal or CMS can replace it.

## Endpoints (`/api/v1`, learner session)
| Method | Path | Notes |
|---|---|---|
| GET | `jobs/summary` | API-075; tab counts, profile %, quests with gaps; `unlockAt` 0 (A-261). Also clears the Launchpad's "new matches" (BR-62) |
| GET | `jobs/list/{tab}` | API-076; tab `available` · `matching` (match ≥ 60 %) · `bookmarked`; 404 `tab-not-found` |
| GET | `jobs/invitations` | API-077 |
| GET | `jobs/applications` | API-078 |
| GET | `jobs/job/{id}` | API-079; 404 `job-not-found` |
| GET | `jobs/invitations/{id}` | API-080; 404 `invitation-not-found` |
| GET | `jobs/applications/{id}` | API-081; 404 `application-not-found` |
| PUT | `jobs/job/{id}/saved` | API-082; `{ saved }` |
| GET | `jobs/job/{id}/apply` | API-083; name, masked contact, the learner's uploaded CVs, the employer's questions |
| POST | `jobs/cv` | API-084; multipart `file` (PDF, .docx, .doc by content, ≤ 5 MB) → Blob Storage → `{ id, name, meta }` (200); 422 `file-missing` · `unsupported-type` · `too-large`. [Idempotent] |
| POST | `jobs/job/{id}/apply` | API-085; `{ cvId, answers, invitationId? }` → 201 + `Location`; 409 `already-applied` · `reapply-cool-off` (30 days, BR-66) · `job-closed`; 422 `cv-not-found`. [Idempotent] |
| POST | `jobs/invitations/{id}/{accept\|decline\|suggest}` | API-086; `{ note }`; 410 `invitation-expired`. [Idempotent] |
| POST | `jobs/applications/{id}/withdraw` | API-087; 409 `cannot-withdraw` for an offer (BR-66) |
| GET/PUT | `jobs/alerts` | API-088/089; `{ on, frequency: daily\|weekly, searches }` |
| GET | `fests/summary` | API-041; `unlockAt` 60 and the quests still to fill (A-230) |
| GET | `fests?filter=` | API-042; `all` · `free` · `in-person` · `online` · `city` (learner's city); upcoming fests in the catalogue's recommended order |
| GET | `fests/bookings` · `fests/bookings/{id}` | API-043/044; upcoming tickets |
| POST | `fests/bookings/{id}/cancel` | API-045; `{ employerId \| null, reason }` → the booking left, or 204 when none is left; 409 `too-late-to-cancel` inside 24 h |
| GET | `fests/past` | API-046; tickets for fests that have ended |
| PUT | `fests/past/{id}/outcome` | API-047; `{ employerId, outcome: offer\|waiting\|no\|null }`; an offer adds an Offer to My Jobs → Applied once (BR-70) |
| PUT | `fests/past/{id}/rating` | API-048; `{ stars: 1–5 }`; no XP (BR-139) |
| GET/PUT | `fests/alerts` | API-049/050; `{ city, online }` |
| GET | `fests/{id}` | API-051 |
| PUT | `fests/{id}/saved` | API-052 |
| GET | `fests/{id}/slots?employers=a,b` | API-053; employer → date → slots, seats left after every learner's bookings |
| POST | `fests/{id}/book` | API-054; 201 new ticket / 200 changed. 422 `profile-incomplete` (< 60 %, BR-68), `too-many-employers` (2), `slot-overlap` (30-min buffer, BR-69), `slot-not-found`, `employer-not-found`; 409 `slot-full`, `fest-full`; 410 `booking-closed` (from the first day); 503 `learner-unavailable`. [Idempotent] |
| GET/POST | `fests/{id}/waitlist` | API-055/056; position = order of joining. [Idempotent] |
| GET | `launchpad/readiness` | API-090; level = missions done, % = XP of done missions ÷ 350 (BR-61) |
| GET | `launchpad/profile` | API-091; 503 `learner-unavailable` when Learner doesn't answer |
| GET | `launchpad/assessment` | API-092; `not-started` or `done` with archetype and top drivers |
| GET | `launchpad/jobs` | API-093 |
| GET | `launchpad/fest` | API-094; the learner's next booked fest, else the next open one |
| GET | `launchpad/sectors` | API-095; fit blank until the assessment is done (BR-63) |
| PUT | `launchpad/fest/alerts` | API-096; `{ on }` sets both Hire Fest alerts |
| GET | `me/matches/preview` | API-104 |
| POST | `me/profile/publish` | API-105; `{ discoverable, invites }` → 204. [Idempotent] |
| GET | `me/matches` | API-106; cards once published or after a confirmed claim (BR-72) |
| POST | `me/matches/{id}/apply` · `save` · `accept` | API-107–109 → 204; 404 `match-not-found` |

**List shape (deviation from the generic paging rule).** My Jobs lists take jobs.api.ts `ListQuery`
(`?q&place&sort&limit&filters`, `filters` = JSON of key → option ids) and return its `ListResult`
(`{ items, total, facets, suggestions, note }`): the first `limit` hits (default 20, max 200), facet counts with the
other filters applied, and J6 suggestions when nothing matches. The frontend reads this shape, so `page/pageSize` is
not used here.

## Rules
- **Matching:** match % = skills 0.6 + location 0.25 + assessment 0.15 (`Careers:Matching`). Each job skill the learner
  has counts 1.0 with proof (assessed, verified, language level) and 0.8 declared. Matching lists ≥ 60 %. Skills,
  city and relocation come from Learner (`internal/learners/{id}/careers-profile`, proposed); while Learner doesn't
  serve them, matches are unknown (no match box, Matching empty) rather than invented.
- **Missions:** assessment = `assessment.attempt.submitted`; profile = `learner.job-profile.completed` or 100 % now;
  apply = an application sent from My Jobs and not withdrawn; fest = a booking. XP is display only (Gamification pays
  from the events, once).
- **Hire Fest:** one ticket per fest, up to 2 employers, 30-min interviews with a 30-min buffer; seats per slot are
  the seed's minus every learner's bookings; booking closes when the fest starts; cancel up to 24 h before.
- **Dates** are shown in the market's time zone (IST / SAST) as "2 Oct".

## Events
Publishes `careers.application.sent`, `careers.application.withdrawn`, `careers.fest.booked` (new ticket only),
`careers.fest.cancelled` (whole ticket). Consumes (subscription `careers`): `assessment.attempt.submitted`,
`learner.job-profile.completed`, `identity.claim.confirmed`, `identity.learner.erased`.

## PII
`cv_files.name` (the learner's file name) and the CV files themselves (Blob container `careers-files`);
`job_applications.cv_name` and `answers`; `invitations.note`. All erased on `identity.learner.erased`.

## Config
| Key | Where |
|---|---|
| `ConnectionStrings:Careers` | Key Vault `careers-db-connection` |
| `Careers:LearnerBaseAddress` | `http://ca-learner/` (Bicep), `http://learner:8080/` (compose) |
| `Careers:Storage:ServiceUri` (Azure, managed identity) or `Careers:Storage:ConnectionString` (Azurite) | CV files; memory in Development/Testing if neither |
| `Redis:Host` or `ConnectionStrings:Redis` | Idempotency-Key store |
| `Careers:FestBookingGate`, `MatchingThreshold`, `ReapplyCoolOffDays`, `CancelCutoffHours`, `MaxCvBytes`, `Missions`, `Matching`, `Fest`, `TimeZones` | optional overrides |
| `ServiceBus:Subscription` | `careers` |

## Erasure tombstone (MISS-08)

On `identity.learner.erased` the learner's rows are deleted and a tombstone stays in `erased_learners`
(`learner_id`, `erased_at` — nothing else, no tenant, no personal data; shared building block
`AddErasureTombstones<TContext>()`). An access token stays valid for up to 10 minutes after erasure, so:

- every request from an erased account gets **410 `learner-erased`** (`ErasedAccountMiddleware`, SkillMuni.Web) instead
  of recreating an empty row;
- an event about an erased learner (any event implementing `ILearnerEvent`) delivered late is recorded in the inbox and
  skipped.
