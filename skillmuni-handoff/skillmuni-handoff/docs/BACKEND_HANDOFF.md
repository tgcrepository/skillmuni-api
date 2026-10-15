# Backend handoff · SkillMuni India Phase 1

Read this first in the new backend chat, then `phase-1/BACKEND_RULES.md` (save it as the repo's `CLAUDE.md`). The frontend contract is in `phase-1/FRONTEND_TYPES.md` (types) and `phase-1/FRONTEND_MOCKS.md` (mock behaviour), so no attachment is needed. Last updated 13 Oct 2026.

## Decisions (A-309, A-310)
- **API:** ASP.NET Core 10 (LTS), C# 14, microservices built with Clean Architecture.
- **Hosting:** Azure Container Apps, one container per service.
- **Gateway:** YARP. It serves `/api/v1/*`. The frontend changes only `environment.apiBaseUrl` from `/api` to `/api/v1`.
- **Data:** MySQL on Azure Database for MySQL Flexible Server, one schema per service.
- **Events:** Azure Service Bus. **Files:** Blob Storage. **Secrets:** Key Vault. **Access to Azure resources:** Managed Identity. **Cache:** Redis.
- **Repo:** new (`skillmuni-api`), no legacy schema to follow.
- **Market:** India first. South Africa comes through market config.

## Services
| Service | Owns | Frontend areas (API_CONTRACT) | Missing APIs |
|---|---|---|---|
| **Identity** | Accounts, logins, sessions, codes, consents, institute claims | Auth, Institute claim, contact verify (`/me/phone`, `/me/contact`) | MISS-07, 08, 13 |
| **Learner** | Profile and the 8 quests: name, contact, address, education, skills, trophies, languages, drafts | Profile, Identity (`/me`), Quests, Quest 4, Drafts, Programme, Feedback, Contact & geo | MISS-09 |
| **CV** | CV upload, parsing jobs, parsed results | CV | – |
| **Assessment** | Career Drivers attempts, islands, scores, archetypes | Career assessment | – |
| **Learning** | Briefs, quizzes, progress, the Lobby feed | Learn, Lobby | MISS-04 (activity) |
| **Gamification** | **XP ledger**, levels, badges, stats, streaks, daily quests, Arena, rewards | My Stats & badges, Arena | MISS-01, 02, 03, 04, 10, 11 |
| **Careers** | Job Profile, matches, My Jobs, Hire Fest, Career Launchpad | Job Profile, Matches, My Jobs, Hire Fest, Launchpad | MISS-05 |
| **Muni** | Chat intents, replies, voice | Muni chat | MISS-14, 15 |
| **Platform** | Tenant theme, market config, geo lookup, notifications (SMS / email / push) | Contact & geo (`/geo`) | MISS-06, 12 |

**Key events:**
- `identity.learner.registered`
- `learner.quest.step-completed`
- `learner.quest.completed`
- `assessment.island.cleared`
- `assessment.attempt.submitted`
- `learning.quiz.answered`
- `careers.application.sent`
- `careers.fest.booked`
- `gamification.xp.awarded`
- `gamification.badge.earned`

Gamification consumes the progress events and is the only service that credits XP.

## Sources, in order of authority
1. **BRD doc** ("Skillmuni India · Business Requirements (Phase 1)" in this project): rules BR-xx, FR-xx, data model (section 5), open decisions (section 9).
2. **`phase-1/API_CONTRACT.md`:** 133 endpoints plus MISS-01 to MISS-15.
3. **Frontend contract:** `phase-1/FRONTEND_TYPES.md` and `phase-1/FRONTEND_MOCKS.md` (or the latest `skillmuni-*.tgz` if attached):
   - Request and response types live in `src/app/core/**/**.api.ts`.
   - The mock behaviour to copy lives in `src/mocks/*.handlers.ts`.
4. **`phase-1/ASSUMPTIONS.md`:** why things behave as they do (A-xxx).

## Must-keep behaviour
- **Errors:** ProblemDetails with `message` + `errors.code[0]`, using the same codes as the mock.
- **XP:** one ledger. **BRD section 4 is the final gamification rulebook** (14 Oct): 4.1 lists every XP source (BR-100 to BR-119); anything not listed earns 0 (BR-139). Levels 4.2, badges 4.3, streaks and daily quests 4.4, formulas 4.5, celebrations 4.7, pop-ups and toasts 4.8. The appendix workbook's "XP table" sheet matches it.
- **Login (BR-136):** send-code returns 404 `account-not-found` for an unregistered mobile or email. Codes are 6 digits; a wrong code returns 422 with no tries count (A-312). Throttle abuse silently (mock: 429 `too-many-tries` after 20 wrong codes; `triesLeft` may still be sent but is not shown; also rate-limit by IP and account). An expired code returns 410, a locked account 423. Resend after 30 s.
- **Quest XP (A-311):** +20 per part for its required fields. Parts with only optional fields pay +20 once something is added: backup contact mobile, one education entry, one certificate, one proof. +50 per quest on first completion only; the complete response returns `xpEarned: 0` on replays.
- **Progress (A-316):** `GET /api/v1/me/progress` returns the ledger total (all sources), with level, rank and next target from the level table. Profile, Stats and the logout dialog read it.
- **Missions (A-321):** each Launchpad mission pays once (profile +50, first application +100, first fest booking +100); the assessment mission is the assessment's own 100.
- **Streak (A-322):** a day counts when the learner finishes at least one brief quiz (local time); no XP, unlocks streak badges.
- **Removed for launch (A-317):** coins, the rewards store, rewards counts in nav. Keep the tables for later; no endpoints needed now.
- **Session:** idle timeout 30 min. `GET /api/v1/session` returns 401 when there is no session.
- **Privacy:** masked ID only (A-272). Consent records under DPDP and POPIA.

## Build order
1. **Foundation:** repo skeleton, BuildingBlocks, gateway, docker-compose, Bicep, CI.
2. **Identity:** login, sign-up, verify, session, Google.
3. **Learner:** the 8 quests and the profile. Then **CV**.
4. **Gamification:** XP ledger, levels, badges. Wire the events from Learner.
5. **Assessment**, then **Learning**, **Careers**, **Muni**, **Platform**.

## Still open (BRD section 9)
- Retention policy.
- Performance budgets.
- Browser support matrix.
- Which provider sends SMS and email.
- Muni: intent catalogue or LLM (MISS-14).
