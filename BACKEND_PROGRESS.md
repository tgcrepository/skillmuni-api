# Backend progress · SkillMuni India Phase 1

Updated 16 Oct 2026. Repo: GitHub `tgcrepository/skillmuni-api`, branch `main` (PRs #1–#8 merged). CI runs on every PR
and on `main`.

## Status: Phase 1 backend complete
| Service | State | Endpoints (`/api/v1`) |
|---|---|---|
| Foundation | ✅ | BuildingBlocks (Common, Web, Messaging, Storage), YARP gateway, compose, Bicep, CI, dev-tools workflow |
| Identity | ✅ reconciled with BRD | Auth, session, Google, claims, contact verify, consents, sessions, data export, erasure |
| Learner | ✅ reconciled with BRD | 8 quests (18 steps), profile + photo, drafts, DigiLocker, programme, trophies, ESCO, preferences, **Job Profile** |
| CV | ✅ | Upload, parse status, summary, delete |
| Gamification | ✅ reconciled with BRD §4 | XP ledger, `me/progress`, levels, badges + showcase, streak, daily quests, Arena, Rewards |
| Assessment | ✅ new | Career Drivers (state, answers, submit, result, restart, retake, remind), My Stats assessments |
| Learning | ✅ new | Learn modules/topics/briefs/quizzes, learning minutes, Lobby (feed, first steps, first-moves bonus), My Stats learning |
| Careers | ✅ new | My Jobs, Hire Fest, Career Launchpad, My Matches (44 endpoints) |
| Muni | ✅ new | Welcome, messages (keyword intents), reveal, ratings, history |
| Platform | ✅ new | Geo (postal, regions), tenant theme + market config, notifications (bell, preferences, sending) |

- All **133 endpoints** in `docs/API_CONTRACT.md` are routed by the gateway and implemented (checked by script, 15 Oct).
- Missing APIs done: MISS-01 (ledger), 02 (badge engine, catalogue awards behind a flag), 03 (daily quests, auto-credit),
  04 (streak, `learn/activity`), 05 (missions auto-credit), 06 (notifications), 07 (consents), 08 (export, erasure),
  09 (preferences), 12 (tenant theme/market), 13 (refresh, sessions, rate limits), 14 (intent catalogue behind
  `IMuniResponder`). Not done by decision: MISS-10 (rewards store, removed for launch A-317), MISS-11 (league rules to
  confirm), MISS-15 (Muni has no voice).
- Every event in `docs/EVENTS.md` has matching fields between publisher and consumers, and every consumer's Service Bus
  filter (Bicep and the local emulator) lists exactly the events it handles (checked by script, 15 Oct).
- **CI on `main`: all green** — build (0 warnings), format, vulnerable packages, migrations match the model,
  ~510 unit + integration tests (Testcontainers MySQL), 10 Docker images, Bicep.

## Full regression (16 Oct) — launch readiness
The whole system now runs on every PR and on `main` with **docker compose in GitHub Actions** (`.github/workflows/e2e.yml`):
MySQL, the Service Bus emulator, Redis, Azurite, all 9 services and the gateway, exactly as deployed. Journeys in
`tools/contract/journeys/` then drive the API the way the Angular app does (cookie session, real field names from the
app's source) and check every response against the app's TypeScript types (`tools/contract/contract.json`, extracted
from `src/app/core/**/*.api.ts` by `tools/contract/extract-contract.js`).

**Result on `main`: 130 / 130 frontend calls covered, 721 checks, 0 problems; 539 unit + integration tests passing.**

Defects found and fixed by the regression (all with tests):
- Local stack didn't start: MySQL init script stopped MySQL (`set -u`), Service Bus emulator TTL above its 1 h limit,
  SQL Edge retired (now SQL Server 2022), services crashed while MySQL warmed up (restart policy).
- Identity: a Service Bus outage while sending a code was a 500 → now 503 `notifications-unavailable`; re-sign-up after
  erasure blocked by a used code; claim confirmed before registration lost quests 1–5 and 550 XP.
- Gamification: parallel events for one learner overwrote each other (lost XP, wrong quest counts, double feedback XP)
  → `players.version` concurrency token + one event at a time per replica + dispatcher retry; Rewards read XP from the
  player row instead of the ledger; Arena dropped players with no XP and sent `photo: null`; Arena GET could 500.
- Learner: quest step keys and Job Profile / careers-profile field names didn't match the app's quests (guessed
  before the frontend source was available) → now read the real fields; no programme codes in Development; ESCO,
  CV and Careers responses sent `null` for optional fields the app types as absent; duplicate-row races on
  `job_profiles`, `quest_answers`, `lobby_states`.
- Careers: the Applied tab ignored the sort.
- Muni: two suggestion chips fell back to "I don't have that one yet"; late events resurrected erased learners.
- Erasure: every service now keeps a tombstone and answers 410 `learner-erased` instead of recreating an empty profile
  from a still-valid token.
- Gateway: security headers (nosniff, frame DENY, CSP, HSTS), problem+json 404 for unknown routes, rate limits that
  no longer block a shared college network (1200/min per IP, 120 on auth), plus a per-account limit (600/min).

Owner questions raised by the regression:
- **810 vs 760 XP after registration:** the 8 quests pay 760 (BR-103) and finishing them takes the Job Profile to 100%,
  which pays the "Finish job profile" mission +50 (BR-110) → **810 XP, level 3 Trailblazer** (the app's own Quest 8
  card already says "Level 3 · Trailblazer"). BRD §4.2 says a learner who finishes all quests is a level 2
  Pathfinder. Which one is right?
- **Career Clarity badge:** with catalogue badges off for Phase 1 (B-50) it shows "100% in progress" after the
  assessment instead of "earned". OK for launch?

## Chat log
**15 Oct · Chat 1: first build.** Repo moved to GitHub and restructured. First compile and test run fixed
(`application/problem+json` under `[Produces]`, ICU cultures, versioned OpenAPI, format). Added Idempotency-Key (B-37),
Redis via managed identity (B-40), CI results on PRs (B-41).

**15 Oct · Chats 2–9 (one session, owner asked to finish the whole backend).** Event catalogue and dev-tools workflow
(B-43); reconcile of Gamification / Learner / Identity with BRD §4 (PR #3); Assessment (#4); Learning (#5); Muni and
Platform (#6); Careers (#7); Learner `careers-profile` for matching (#8). Built in parallel on separate branches,
merged one by one with CI green after each merge.

## Decisions (B-xx)
| Id | Decision | Why |
|---|---|---|
| B-01 | ASP.NET Core 10 / C# 14 (Yash, 13 Oct), replacing A-309's .NET 8 | .NET 8 support ends 10 Nov 2026 |
| B-02 | EF Core 10 + Oracle `MySql.EntityFrameworkCore` 10.0.9 instead of Pomelo | Pomelo's latest is 9.0.0 |
| B-03 | Built-in OpenAPI (`/openapi/v1.json`); gateway shows Swagger UI over all services in Development | .NET 10 standard |
| B-04 | Sessions: RS256 access JWT 10 min (`sm_at`), rotating refresh (`sm_rt`), 30 min idle, 30 days max, 30 s grace | MISS-13 |
| B-05 | The gateway refreshes silently once the access token is 2 min old | No frontend change |
| B-06 | Codes stored as HMAC hashes; resend inside 30 s returns the current challenge; max 5 codes / hour / target | BR-136 |
| B-07 | Account is created only after the email code | An unverified address can't block its owner |
| B-08 | ~~Password: 10 wrong tries → locked 15 min (423)~~ Superseded by B-46. | Not in the BRD |
| B-09 | Codes and reset links go to Service Bus queue `notifications` for the Platform service | Provider open (BRD §9) |
| B-10 | Production migrations run from EF bundles in the release pipeline, not at start-up | Replicas must not race |
| B-11 | **Deviation:** services sign in to MySQL with a Key Vault password, not Managed Identity | Oracle's provider has no token callback |
| B-12 | Gateway maps host → tenant (`X-Tenant-Id`); a token from another tenant gets 403 `tenant-mismatch` | White-label isolation |
| B-13 | ~~Learner shows XP at once (its own award + Gamification's last totals) for 5 min, then Gamification's totals only~~ Superseded by B-51. | XP is credited only by Gamification (CLAUDE.md §9) yet screens need `xpEarned` and progress in the same response |
| B-14 | Quest catalogue, XP amounts and level ladder are config with code defaults copied from the frontend | Keeps Learner, Gamification and the app on one table; labels flagged to check against `quest-defs.ts` |
| B-15 | `POST quests/{slug}/complete` returns `xpEarned: 0` on a repeat and 422 `quest-locked` if earlier quests aren't done | Mock always returns 50 and allows skipping |
| B-16 | Learner row is created by `identity.learner.registered`, or lazily from the token if a request arrives first | Events are async |
| B-17 | Bank account numbers AES-256-GCM encrypted (Key Vault key); only last 4 stored in clear, last 2 returned | Stipend payouts need the number back |
| B-18 | Uploads typed from their bytes (JPG, PNG, WEBP, HEIC, PDF); photo ≤ 5 MB, certificate ≤ 10 MB; private blob container | Renamed files can't slip through |
| B-19 | Credential link check refuses private, loopback, link-local and metadata addresses on every connection | SSRF |
| B-20 | DigiLocker: sandbox in Development/Testing, otherwise 503 `digilocker-unavailable` | Needs MeitY partner credentials |
| B-21 | Quests 1–5 count as done on `identity.claim.confirmed` (V4), not on consent | The learner has only checked the data at V4 |
| B-22 | Added `ErrorKind.Timeout` → 504 to BuildingBlocks | Contract's "504 link check timeout" |
| B-23 | CV text read by Azure AI Document Intelligence `prebuilt-read` (managed identity, keys disabled) | OCR for photos + PDF/DOCX in one service, data stays in the Azure region |
| B-24 | CV fields extracted by a rule-based interpreter behind `ICvInterpreter`; an LLM interpreter can replace it | No LLM provider decided; deterministic and testable meanwhile |
| B-25 | Parse queue = the `cv_documents` table, claimed with a conditional UPDATE by a worker in each replica | No extra infrastructure; crash-safe (stale claims requeue, 3 attempts) |
| B-26 | Pre-fill travels as `cv.parse.completed`; Learner adds only fields the learner hasn't saved | Services own their data; the learner's own answers always win |
| B-27 | Raw CV text is never stored; ID numbers, marital status, DOB, religion listed as "skipped"; originals deleted after 30 days | Privacy copy "Skipping the private stuff" |
| B-28 | `.doc` and WEBP refused at upload (S1) | Document Intelligence can't read them |
| B-29 | Upload returns 201 `{ cvId }` (the contract table says `{ id }`; the app reads `cvId`) | Matches the code the app runs |
| B-30 | Shared `SkillMuni.Storage` (Blob) and `tests/SkillMuni.Testing` (tokens, assertions) | No duplicated plumbing across services |
| B-31 | ~~Gamification owns the XP table and level ladder; Learner keeps a display copy and shows Gamification's totals after 5 min~~ Learner keeps amounts only for award responses (B-51). | One ledger (CLAUDE.md §9) |
| B-32 | Arena boards are computed from the ledger per window (week from Monday, month from the 1st, in IST / SAST) and cached 60 s | No nightly job; always consistent with XP |
| B-33 | Arena "batch" = learners in the same programme; "friends" = players you cheer (until friends exist) | No batch or friends data elsewhere yet |
| B-34 | ~~Streak counts any day with XP or learning; daily quests credit automatically (no claim button)~~ Superseded by B-45 (streak) and BR-118 auto-credit. | Matches the screens; MISS-03 proposed a claim endpoint |
| B-35 | Founding Learner is earned by everyone who joins before 1 Oct 2027 | "Join in Skillmuni's first year"; launch date to confirm |
| B-36 | ~~Claim confirmation pays 100 XP~~ Superseded by the final BRD (BR-119): normal step and quest XP. Fix in chat 2/3 | — |
| B-37 | `Idempotency-Key`: `[Idempotent]` resource filter in `SkillMuni.Web`. Key scoped to tenant + caller + method + path; request body SHA-256 (size and type for uploads over 1 MB); 2xx/4xx kept 24 h in the distributed cache, 5xx/409/429 not kept; 422 `idempotency-key-reused`, 400 `invalid-idempotency-key`, 409 `request-in-progress`; replays send `Idempotent-Replayed: true`. Header optional | CLAUDE.md §4. Kept in Redis, not MySQL: short-lived, no schema change per service. The ledger's unique key stays the final guard |
| B-38 | Errors are written by `ProblemActionResult`, not `ObjectResult` | `[Produces]` filters overwrote `application/problem+json` |
| B-39 | Images keep `InvariantGlobalization`; no ICU culture lookups in code | Smaller images; dates and language names don't depend on OS data |
| B-40 | One cache registration in `AddSkillMuniServiceDefaults`: `Redis:Host` → Azure Cache for Redis with Entra (managed identity, Data Contributor access policy per service in Bicep); `ConnectionStrings:Redis` locally; memory otherwise. Keys prefixed `skillmuni:<service>:` | Access keys are disabled in Azure (no secret); services can't read each other's keys |
| B-41 | CI posts a results comment (test counts, errors, failing assertions) to the PR | Build logs can't be downloaded from the Claude workspace; the PR comment is how results are read |
| B-42 | OpenAPI stays on built-in `Microsoft.AspNetCore.OpenApi` (B-03), not Swashbuckle as `CLAUDE.md` §4 says | .NET 10 standard; Swashbuckle's UI is still used in the gateway. `CLAUDE.md` wording to update |
| B-43 | `docs/EVENTS.md` is the single event catalogue; `IntegrationEvent.OccurredAt` is stamped by the outbox; a `dev-tools` workflow generates EF migrations and runs `dotnet format` on a branch | Services built in parallel need one contract; streaks need event time; no .NET SDK in the Claude workspace |
| B-44 | **Owner:** daily quest "Finish Brain-Vita" (+10) = resume where the learner left off: label "Resume where you left off", Learning exposes the resume point (`resume` on the module response and the Lobby card); done on the first completion of a Brain-Vita brief that day | Yash, 15 Oct |
| B-45 | **Owner:** a streak day needs a brief quiz submitted for the first time that day (learner's local time); retakes and practice don't count; no XP | Yash, 15 Oct |
| B-46 | **Owner:** no password lock (B-08 removed); gateway per-IP / per-account rate limits remain | Yash, 15 Oct: "keep as BRD" |
| B-47 | Job Profile endpoints live in Learner, not Careers | Learner owns the quest answers it renders (A-262) |
| B-48 | Job Profile CV upload isn't parsed by Learner: file stored in Blob, changes proposed from the latest `cv.parse.completed` pre-fill; contact changes are never applied | One parser (CV service) |
| B-49 | An optional step pays +20 only when no `xp:false`, no `<key>Skipped` and at least one non-empty value (BR-101) | Server-side check, not the client flag |
| B-50 | Catalogue badges compute progress but are awarded only when `Gamification:Badges:AwardCatalogue` is on (default off) | BRD §4.3: Phase 1 awards the 8 quest badges |
| B-51 | `me/progress` served by Gamification from the ledger (BR-138); Learner keeps a display estimate only inside award responses | One total everywhere |
| B-52 | Hidden limit of 20 wrong codes applies to every code flow (login, claim, contact); `triesLeft` is still sent (counted from 20) because the frontend type has it, never shown | BR-11, A-312 |
| B-53 | Internal endpoints (`/internal/...`) are reachable only inside the Container Apps environment; the gateway proxies only `/api/v1/...` (test); PII goes in bodies, not URLs | Audit of 15 Oct |
| B-54 | Arena `rewards` stays the constant `{ coins: 0, voucher: null }`, wallet coins and credits 0; streak bonus removed | A-317, BR-139 |
| B-55 | Job Profile share link: 8-character random code, `{ShareBaseUrl}/cv/{firstname}-{code}`, verify code = first 5 characters; radar/strength labels/About-me template are backend choices to review with design | Not specified in the BRD |
| B-56 | Assessment: one state row per learner plus one row per submitted attempt (scores, archetype, raw answers kept for re-scoring); content is an embedded JSON generated from `assessment.data.ts` behind a provider | CMS can replace content later |
| B-57 | Assessment: XP-eligible = no attempt submitted yet; a retake before any submit acts as a restart; if islands were never cleared by a save, submit clears them so the first attempt always pays 100 | Mock would lose first-attempt XP |
| B-58 | Assessment `stats/assessments` returns the mock's empty state (skill assessments don't exist in Phase 1); mandatory courses fixed at 2 until Learning provides it | Phase 1 scope |
| B-59 | Learning: in events `courseSlug` = the app's module (e.g. `brain-vita`), `moduleSlug` = its topic; a brief is completed on the first submit of its quiz, whatever the score | BRD §4.3 vocabulary, mock rule |
| B-60 | Learning: quizzes in order (422 `quiz-locked` for a later brief); unknown module 404; claiming first moves early 409 `first-moves-not-done`; learning minutes capped at 1440 per local day | Mock doesn't enforce; data integrity |
| B-61 | Careers matching: match % = 0.6 × skills + 0.25 × location + 0.15 × assessment done; proof 1.0 verified / 0.8 declared; lists ≥ 60% | A-222; mock values were fixed per sample learner |
| B-62 | Careers reads Learner through `internal/learners/{id}/profile-progress` and `careers-profile` (typed HttpClient, retry/timeout/circuit breaker); Learner down → 503 `learner-unavailable` on booking and the profile card, 0% elsewhere | No cross-schema reads |
| B-63 | Careers status codes: apply 201 + Location; book 201 new / 200 changed; cancelling the last interview 204; booking gate 422 `profile-incomplete`; `slot-full`/`fest-full` 409; `booking-closed` 410; cancel < 24 h 409 `too-late-to-cancel`; re-apply 30 days after withdrawing (409 `reapply-cool-off`) | Not specified in the mock |
| B-64 | Careers: invitations and application status changes need an employer side (not built); alerts default off; a fest "Offer" outcome adds an offer to My Jobs without ticking "Apply to a job" | Phase 1 is learner-only |
| B-65 | Muni: keyword-intent catalogue as seed data behind `IMuniResponder` (an LLM can replace it); never shows invented data (mock's sample jobs/progress replaced by neutral text or real projections) | MISS-14 open |
| B-66 | Muni history: messages < 30 min apart form one conversation; history and ratings deleted after `Muni:HistoryDays` (default 90) | Retention policy open (BRD §9) |
| B-67 | Platform: postal data = mock codes plus a starter set behind `IPostalDirectory` (Redis cached); theme and market config served from seed/config | No geo provider decided |
| B-68 | Platform notifications: sandbox sender for SMS / email / push / WhatsApp (masked recipient logged, code never stored); defaults push + email on, WhatsApp + SMS opt-in; badges become bell notifications | Provider open (BRD §9) |
| B-69 | Full regression in CI (`e2e` workflow) against the whole stack, journeys checked against the app's types; every PR must stay at 0 problems | Owner asked for launch readiness and zero frontend conflicts |
| B-70 | Gamification: `players.version` optimistic concurrency, one event at a time per replica, and the inbox dispatcher retries a lost save race up to 4 times | Parallel events lost XP and counters |
| B-71 | Erasure tombstones (`erased_learners`: id + time only, no tenant) in every service that creates learner rows lazily; requests answer 410 `learner-erased`, late events are skipped | Tokens stay valid up to 10 min after erasure |
| B-72 | A Service Bus outage while sending a code answers 503 `notifications-unavailable` (DependencyUnavailableException → global handler) | Codes are never stored in plain text, so they can't go through the outbox |
| B-73 | Gateway limits: 1200/min per IP, 120/min per IP on auth routes, 600/min per signed-in account; security headers on every response | A college's shared IP must not lock out a class |

## For the frontend (pass to a developer)
- Set `environment.apiBaseUrl` to `/api/v1`. API and app must be same-site (cookies are SameSite=Lax).
- Optional `Idempotency-Key` header (UUID per user action, reused on retry) on every POST that creates something or
  pays XP (quest complete, feedback, programme, trophies, photo, CV, cheer, challenge, register, consents, claim
  confirm/finish, assessment submit/restart/retake, quiz submit, first-moves claim, apply, book, waitlist, invitations,
  match apply/accept, publish).
- New calls the app should start making: `POST learn/activity { minutes }` (learning minutes for "Learn 15 min" and
  streak screens); the module response's optional `resume` field (B-44 deep link); `GET tenant/theme` and
  `GET tenant/market` (white-label config from the server); `GET me/notifications` for the bell.
- Behaviour that differs from the mock (all intentional, see decisions):
  - Repeat quest completion returns `xpEarned: 0`; out-of-order completion 422 `quest-locked`.
  - Wrong codes: no count shown, 429 after 20 (B-52). Wrong passwords never lock (B-46).
  - Validation errors are 422 with code `validation` (e.g. arena scope/period).
  - New learners start empty: no sample applications, invitations, bookings or matches (mock persona data).
  - Careers matches stay empty until a learner has skills; distance and live queue are null.
  - Muni history is a bare array (as `muni.api.ts` expects); replies carry an extra `id` for ratings.
  - Contact masking is `•••••• 0697` everywhere.
- Error codes are listed in each service README (`src/Services/<Name>/README.md`).

## Gaps and open items
- **Product questions for Yash** (none block launch):
  - Archetype tie-break when two archetypes score the same (BR-52, A-211).
  - Founding Learner cut-off date (B-35) — only matters once catalogue badges are switched on (B-50).
  - Which catalogue badges launch first (D-02), then turn on `Gamification:Badges:AwardCatalogue`.
  - Arena league, weekly reset and challenge results (MISS-11).
  - Retention policy (BRD §9): Muni defaults to 90 days; nothing else is deleted automatically yet.
  - SMS / email / WhatsApp provider (BRD §9): all channels use a sandbox sender until chosen.
- **Needs other systems:** employer side (invitations, application status, fest check-in queue); content admin for
  modules, jobs, fests, assessment content (all seed data behind provider interfaces today); real postal / geo data;
  DigiLocker partner credentials (sandbox until then); Document Intelligence S0 for CVs longer than 2 pages.
- **Not wired yet:** the 6 PM assessment reminder (flag stored, no sender); data export (`identity.data-export.requested`)
  answered by Learner, CV and the new services; Assessment's "N of 36 picked" on the Lobby first move.
- **To check against real data:** Job Profile and `careers-profile` read some quest-answer field names leniently
  (`discoverable`, `inviteEmail`, `signature`, proof → skill links); confirm against the frontend's `quest-defs` when the
  first real learners sign up.
- `CLAUDE.md` §4 says Swashbuckle; the code uses built-in OpenAPI (B-42). Update the rules wording when convenient.

## Next
| Step | Who | Estimate |
|---|---|---|
| Create the Azure resources (`deploy/README.md`), Key Vault secrets, run the migration bundles, deploy images | Developer / DevOps | 0.5–1 day |
| Point the frontend at `/api/v1` and run it against the real gateway; fix any contract gaps it finds | Developer + Claude | 1 day |
| Choose SMS / email provider and swap the sandbox sender | Product + developer | 0.5 day after choice |
| Answer the product questions above | Yash | — |
