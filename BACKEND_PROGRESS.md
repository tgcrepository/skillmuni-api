# Backend progress · SkillMuni India Phase 1

Updated 15 Oct 2026. Repo: GitHub `tgcrepository/skillmuni-api`, PR #1 (`chore/first-build`). CI runs on every push to a PR.

## Status
| Step | State | Notes |
|---|---|---|
| 1 · Foundation | ✅ Built and verified in CI | Solution, `CLAUDE.md`, BuildingBlocks (Common, Web, Messaging, Storage), YARP gateway, docker compose, Bicep, CI |
| 2 · Identity | ✅ Built, tests pass · ⚠️ BRD reconcile pending (chat 3) | 33 endpoints |
| 3 · Learner | ✅ Built, tests pass · ⚠️ BRD reconcile pending (chat 3) | 27 endpoints + 1 internal; 6 event handlers |
| 4 · CV | ✅ Built, tests pass | 4 endpoints, background parse worker |
| 5 · Gamification | ✅ Built, tests pass · ⚠️ BRD reconcile pending (chat 2) | 9 endpoints; 15 event handlers; the only XP ledger |
| Tests | ✅ 265 passing, 0 failing | Unit + integration (Testcontainers MySQL), run by CI |
| 6 · Assessment (Career Drivers) | ⏭ After chats 2 and 3 | |

## Chat log
**15 Oct · Chat 1: first build.** Repo moved to GitHub and restructured (backend at the root, planning docs in `docs/`).
First compile and test run, fixed:
- Errors went out as `application/json`: `[Produces("application/json")]` on controllers rewrote the ObjectResult's content
  type. Problems are now written directly (`ProblemActionResult`), so every error is `application/problem+json` (B-38).
- `CultureInfo.GetCultureInfo("en-GB")` fails in globalization-invariant images; CV and Learner now use the invariant
  culture and a language-name table (B-39).
- Versioned OpenAPI documents (`Asp.Versioning.OpenApi`, analyzer AV0029/AV0030); `dotnet format` fixes.
Added: `Idempotency-Key` on XP and create POSTs (B-37), Redis via managed identity (B-40), CI posts results to the PR (B-41).
Every CI check is green: build (0 warnings), format, vulnerable packages, migrations match the model, tests, images, Bicep.

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
| B-08 | Password: 10 wrong tries → locked 15 min (423) | Not in the BRD |
| B-09 | Codes and reset links go to Service Bus queue `notifications` for the Platform service | Provider open (BRD §9) |
| B-10 | Production migrations run from EF bundles in the release pipeline, not at start-up | Replicas must not race |
| B-11 | **Deviation:** services sign in to MySQL with a Key Vault password, not Managed Identity | Oracle's provider has no token callback |
| B-12 | Gateway maps host → tenant (`X-Tenant-Id`); a token from another tenant gets 403 `tenant-mismatch` | White-label isolation |
| B-13 | Learner shows XP at once (its own award + Gamification's last totals) for 5 min, then Gamification's totals only | XP is credited only by Gamification (CLAUDE.md §9) yet screens need `xpEarned` and progress in the same response |
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
| B-31 | Gamification owns the XP table and level ladder; Learner keeps a display copy and shows Gamification's totals after 5 min | One ledger (CLAUDE.md §9) |
| B-32 | Arena boards are computed from the ledger per window (week from Monday, month from the 1st, in IST / SAST) and cached 60 s | No nightly job; always consistent with XP |
| B-33 | Arena "batch" = learners in the same programme; "friends" = players you cheer (until friends exist) | No batch or friends data elsewhere yet |
| B-34 | Streak counts any day with XP or learning; daily quests credit automatically (no claim button) | Matches the screens; MISS-03 proposed a claim endpoint |
| B-35 | Founding Learner is earned by everyone who joins before 1 Oct 2027 | "Join in Skillmuni's first year"; launch date to confirm |
| B-36 | ~~Claim confirmation pays 100 XP~~ Superseded by the final BRD (BR-119): normal step and quest XP. Fix in chat 2/3 | — |
| B-37 | `Idempotency-Key`: `[Idempotent]` resource filter in `SkillMuni.Web`. Key scoped to tenant + caller + method + path; request body SHA-256 (size and type for uploads over 1 MB); 2xx/4xx kept 24 h in the distributed cache, 5xx/409/429 not kept; 422 `idempotency-key-reused`, 400 `invalid-idempotency-key`, 409 `request-in-progress`; replays send `Idempotent-Replayed: true`. Header optional | CLAUDE.md §4. Kept in Redis, not MySQL: short-lived, no schema change per service. The ledger's unique key stays the final guard |
| B-38 | Errors are written by `ProblemActionResult`, not `ObjectResult` | `[Produces]` filters overwrote `application/problem+json` |
| B-39 | Images keep `InvariantGlobalization`; no ICU culture lookups in code | Smaller images; dates and language names don't depend on OS data |
| B-40 | One cache registration in `AddSkillMuniServiceDefaults`: `Redis:Host` → Azure Cache for Redis with Entra (managed identity, Data Contributor access policy per service in Bicep); `ConnectionStrings:Redis` locally; memory otherwise. Keys prefixed `skillmuni:<service>:` | Access keys are disabled in Azure (no secret); services can't read each other's keys |
| B-41 | CI posts a results comment (test counts, errors, failing assertions) to the PR | Build logs can't be downloaded from the Claude workspace; the PR comment is how results are read |
| B-42 | OpenAPI stays on built-in `Microsoft.AspNetCore.OpenApi` (B-03), not Swashbuckle as `CLAUDE.md` §4 says | .NET 10 standard; Swashbuckle's UI is still used in the gateway. `CLAUDE.md` wording to update |

## For the frontend
- Optional: send an `Idempotency-Key` header (a UUID per user action, reused on retry) on quest complete, feedback,
  programme, trophy upload, profile photo, CV upload, cheer, challenge, register, consents and claim confirm/finish.
  Without it the API behaves as before.
- Only change: `environment.apiBaseUrl` → `/api/v1`. API and app must be same-site (cookies are SameSite=Lax).
- `AuthUser.photoUrl` is `null` until Identity consumes `learner.photo.changed` (next Identity change).
- Learner differences from the mock: repeat quest completion returns `xpEarned: 0`; completing a quest out of order is
  422 `quest-locked`; `GET me/identity` returns JSON `null`; photo URL is `/api/v1/profile/photo?v=N` (session cookie).
  Profile `showcase`, `batchRank`, `bestSkill` and `learningHours` are empty until Gamification / Learning exist.
- New Learner error codes: `quest-not-found`, `part-not-found`, `answers-too-large`, `invalid-key`, `draft-too-large`,
  `programme-not-found`, `already-enrolled`, `marks-too-low`, `bank-required`, `invalid-ifsc`, `ifsc-not-found`,
  `bank-lookup-unavailable` (503), `digilocker-rejected`, `market-not-supported`, `esco-unavailable` (503),
  `link-timeout` (504), `file-missing`, `too-large`, `unsupported-type`, `too-many-files`, `dob-in-future`.
- New endpoints, not yet called by the app: `me/preferences` (MISS-09), `admin/programmes/{code}`.
- CV: `POST /cv` returns 201; new codes `file-missing`, `too-many-pages`, `too-many-uploads` (429), `cv-not-found`,
  `cv-not-ready` (409), `cv-parse-failed` (410). Remove `.doc` and `.webp` from `CV_ACCEPT`.

## Gaps and open items
- Learner doesn't answer `identity.data-export.requested` yet (MISS-08 export).
- `gamification.xp.awarded` and `assessment.attempt.submitted` payloads are proposed in Learner; confirm when built.
- ESCO group → category mapping and quest step labels are flagged to confirm (A-245, quest-defs).
- CV: skill vocabulary is a ~50-skill starter set (replace with the frontend's 956-skill deck); the interpreter is
  rule-based; Document Intelligence F0 (dev) reads only the first 2 pages; CV doesn't answer data-export yet.
- Careers' CV uploads (`jobs/cv`, `job-profile/cv`) will reuse the CV service when Careers is built.
- Gamification: League, promotion, weekly reset, challenge results (MISS-11) and coins / vouchers (MISS-10, A-302) are
  placeholders; `topSkill` on Arena rows is empty (needs Assessment); badges for Knooks, CPD, skill cards, friends and
  night / morning learning wait on their source events. Learning's event payloads are proposed, not agreed.
- Claim V4 numbers come from the institute upload; they should come from Learner's internal `profile-progress`.
- Retention jobs wait on the retention policy (BRD §9).
- **Reconcile with the final BRD (14 Oct), chats 2 and 3.** Audit of 15 Oct:
  - Gamification: claim pays a flat 100 (BR-119: step + quest XP); streak counts any activity (§4.4: a finished brief
    quiz only) plus a +5 streak bonus (none); coin / voucher fields (A-317, keep `coins: 0` where the app reads it);
    `/me/progress` from Learner's estimate (BR-138: the ledger total, served by Gamification); catalogue badges awarded
    now (§4.3: only the 8 quest badges in Phase 1); programme bonus once per code (BR-113: once per learner); feedback
    30-day cap decided in Learner (move to Gamification); daily quest 3 is "Finish a brief" (BR-118: Finish Brain-Vita);
    no validators (null showcase ids → 500).
  - Learner: Quest 5 has 2 parts (BRD: 3, 18 steps, 760 XP); optional flags on Identity/Receipts wrong (BR-101); quest
    badge names old (§4.3); claimed quests 1–5 send no step / quest events; programme code logged.
  - Identity: 5 code tries (BR-11: hidden limit 20, no count shown); internal account lookup anonymous with email in the
    query string; no `learner.photo.changed` consumer.
  - Waiting on unbuilt services: island XP 25 × 4 (Assessment), first-moves +50 (Learning), missions +50/+100/+100 (Careers).
- Product questions open (Yash): what "Finish Brain-Vita" means once the course is done; whether a retaken quiz keeps the
  streak; keep the 10-wrong-passwords lock (B-08)?
- Validators missing in CV and Gamification (FluentValidation).

## Next
| Chat | Work | Estimate |
|---|---|---|
| 2 | Gamification: reconcile with BRD §4 (list above), validators, `/me/progress` from the ledger | 3–4 h |
| 3 | Learner + Identity: quest catalogue, claim events, code tries, internal endpoint auth, photo event, logs | 2–3 h |
| 4 | Assessment (Career Drivers, island XP) | 3–4 h |
| 5 | Learning (briefs, quizzes, Lobby, first moves, streak input) | 4–5 h |
| 6 | Careers (Job Profile, matches, My Jobs, Hire Fest, Launchpad missions) | 6–8 h |
| 7 | Muni | 2–3 h |
| 8 | Platform (theme, market config, notifications) | 3–4 h |
| 9 | End-to-end: frontend against the gateway | 2 h |
