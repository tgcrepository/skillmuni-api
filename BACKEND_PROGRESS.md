# Backend progress · SkillMuni India Phase 1

Updated 13 Oct 2026. Repo: `skillmuni-api` (zip from the 13 Oct chat; 5 commits).

## Status
| Step | State | Notes |
|---|---|---|
| 1 · Foundation | ✅ Written · ⚠️ not yet compiled | Solution, `CLAUDE.md`, BuildingBlocks (Common, Web, Messaging), YARP gateway, docker compose, Bicep, CI |
| 2 · Identity | ✅ Written · ⚠️ not yet compiled | 33 endpoints: login (mobile / email code / password), sign-up, sessions, Google, contact verify, claims, consents, privacy |
| 3 · Learner | ✅ Written · ⚠️ not yet compiled | 27 endpoints + 1 internal: quests, progress, drafts, profile + photo, DigiLocker, programme codes, IFSC, feedback, trophies, ESCO, preferences; 6 event handlers |
| 4 · CV | ✅ Written · ⚠️ not yet compiled | 4 endpoints: upload (file or photographed pages), status, summary, delete; background parse worker; Document Intelligence reader + rule-based interpreter; quest pre-fill event (Learner merges) |
| 5 · Gamification | ✅ Written · ⚠️ not yet compiled | 9 endpoints: Arena (board, cheer, challenge), Rewards, badges + showcase, XP ledger, streak, daily quests; 15 event handlers; the only XP ledger |
| Tests | ✅ Written · ⚠️ not yet run | Identity 60 + 16; Learner ~108 + 14; CV ~30 + 9; Gamification ~22 unit + 6 integration (Testcontainers MySQL) |
| 6 · Assessment (Career Drivers) | ⏭ Next | |

## Not verified yet — do this first
The build workspace can't reach NuGet or download the .NET SDK and has no Docker daemon, so nothing has been
compiled or run. Checked instead: all C# files parse (tree-sitter), every project reference and `using` resolves,
JSON / YAML / MSBuild XML parse, `docker compose config` passes.

```bash
dotnet build SkillMuni.sln && dotnet test SkillMuni.sln   # needs the .NET 10 SDK and Docker
```
CI (`.github/workflows/ci.yml`) runs the same plus format, vulnerable packages, migration drift (all four services),
images and Bicep.

Most likely to need a fix on first build:
1. Oracle MySQL provider: Guid → `char(36)`, bool → `tinyint(1)`, `json` columns, `ExecuteUpdate`/`ExecuteDelete`.
2. Hand-written migrations and snapshots. If `has-pending-model-changes` fails, delete that service's
   `Persistence/Migrations` and run `dotnet ef migrations add InitialCreate` (README has the command).
3. Package API drift: Asp.Versioning 10.2, OpenTelemetry 1.15, Testcontainers 4.15, Http.Resilience 10,
   Azure.AI.DocumentIntelligence 1.0 (`AnalyzeDocumentAsync` overload).
4. `list.Contains(x)` in LINQ (Arena batch / friends boards, player lookups): if Oracle's provider can't translate
   parameterised collections, switch those queries to constants or a join.

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
| B-36 | Claim confirmation pays 100 XP | Amount not in the BRD — to confirm |

## For the frontend
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
- `Idempotency-Key` not handled yet (Identity `register`, `admin/claims`; Learner `me/programme`, `me/feedback`).
- Learner doesn't answer `identity.data-export.requested` yet (MISS-08 export).
- `gamification.xp.awarded` and `assessment.attempt.submitted` payloads are proposed in Learner; confirm when built.
- Redis in Azure is Entra-only; Learner uses per-replica memory cache there until the Entra Redis client is added.
- ESCO group → category mapping and quest step labels are flagged to confirm (A-245, quest-defs).
- CV: skill vocabulary is a ~50-skill starter set (replace with the frontend's 956-skill deck); the interpreter is
  rule-based; Document Intelligence F0 (dev) reads only the first 2 pages; CV doesn't answer data-export yet.
- Careers' CV uploads (`jobs/cv`, `job-profile/cv`) will reuse the CV service when Careers is built.
- Gamification: League, promotion, weekly reset, challenge results (MISS-11) and coins / vouchers (MISS-10, A-302) are
  placeholders; `topSkill` on Arena rows is empty (needs Assessment); badges for Knooks, CPD, skill cards, friends and
  night / morning learning wait on their source events. Learning's event payloads are proposed, not agreed.
- Claim V4 numbers come from the institute upload; they should come from Learner's internal `profile-progress`.
- Retention jobs wait on the retention policy (BRD §9).
- ~~Docs said .NET 8 + Pomelo~~ Fixed 15 Oct: `BACKEND_RULES.md` and `BACKEND_HANDOFF.md` now say .NET 10 + Oracle provider.
- **Reconcile with the final BRD (14 Oct) before Assessment:** see the table in `PROJECT_CONTEXT.md` (streak, claim XP, coins, code tries, XP table, `/me/progress`).

## Next
| Step | Estimate |
|---|---|
| First build + test run, fix what the compiler finds | 1.5–2.5 h |
| Reconcile Identity / Learner / Gamification with the final BRD (PROJECT_CONTEXT table) | 1.5–2 h |
| Assessment (Career Drivers) | ~3 h |
| Learning, Careers (jobs, fests, launchpad, matches), Muni, Platform (notifications, tenant theme) | ~16–20 h |
