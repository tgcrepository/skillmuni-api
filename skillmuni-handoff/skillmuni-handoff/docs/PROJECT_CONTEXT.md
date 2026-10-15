# Skillmuni India · Project context

Handoff from Yash's earlier Claude account, 15 Oct 2026. Read this first.

## Who and what
- Yash, UX designer at The Gamification Company (TGC), Pune.
- Skillmuni is TGC's gamified learning app, sold white-label in South Africa. This project is the new **Skillmuni India** learner app.
- The Skillmuni Ecosystem: Skillmuni, Skillmuni Content Studio, ELAI (Enterprise Learning Administrator Interface), recruitment, partner and institute portals. ELAI's gamification settings are designed in Figma file `RkRI2o7UEflTW3h1OSOy83`. They are not part of this backend phase.

## Where things stand
- **Frontend: done.** Angular 22, dark theme only, India first with South Africa as a config switch, white-label tenant theme at runtime. It runs on a mock backend (MSW). The mocks are the reference for every endpoint. Code: `skillmuni-final-14oct.tgz`.
- **BRD: final (14 Oct).** `BRD.md` is the source of truth. Section 4 is the gamification rulebook: XP, levels, badges, missions, streaks, currencies, celebrations, pop-ups.
- **Backend: 5 services written, not yet compiled.** Foundation, Identity, Learner, CV and Gamification were written on 13 Oct (see `BACKEND_PROGRESS.md`). They were never built or tested, because that workspace had no .NET SDK or Docker. The code is in Yash's `skillmuni-api` zip.

## Backend rules (Yash's, non-negotiable)
ASP.NET Core 10 and C#; microservices; Clean Architecture + SOLID; Controllers → Services → Repositories with DTOs and Interfaces; Azure Key Vault + Managed Identity; centralised exception handling; ILogger structured logging; pagination and caching; CancellationToken throughout; universal coding standards. Full detail: `BACKEND_RULES.md` (copy it into the repo as `CLAUDE.md`).

## Changed after the backend was written: reconcile first
The final BRD (14 Oct) changed these rules after the 13 Oct backend code. **The BRD wins.**

| Topic | Backend code (13 Oct) | Final BRD (14 Oct) |
|---|---|---|
| Streak | B-34: any day with XP or learning | A day counts only when the learner finishes at least one brief's quiz (§4.4). Streaks earn no XP. |
| Claim XP | B-36: confirming a claim pays 100 XP | No claim bonus. The normal quest XP applies (550 for quests 1–5, BR-119). Missions pay once (A-321). |
| Coins / vouchers | Placeholders in Gamification | Removed for launch (A-317): no coins, store, vouchers or wallet history. |
| Code tries | Earlier rules said 5 tries, then 429 | Unlimited visible tries with no count shown. A hidden anti-abuse limit (mock: 20) returns 429 (BR-11, A-312). |
| Optional steps | — | An all-optional step pays +20 once, only when something is added. Skipping it pays 0, and the step can't be paid later (BR-101). |
| Other XP | — | Check every row of the §4.1 table against Gamification: photo +5 and top skills +10 (BR-108/109), missions +50/+100/+100 paid once (BR-110–112), programme bonus +100 (BR-113), feedback +5 every 30 days (BR-114), daily quests (BR-118). |
| `GET /me/progress` | B-13: Learner shows its own copy for 5 min | One ledger total drives level and rank everywhere, and `GET /me/progress` returns it (BR-138). |
| Non-XP actions | — | BR-139 lists what earns no XP (streak days, cheers, challenges, retakes…). |

The docs now match the code on the stack: `BACKEND_RULES.md` says .NET 10, Oracle's MySQL provider (B-02) and a Key Vault password for MySQL (B-11). `BACKEND_HANDOFF.md` and `ASSUMPTIONS.md` say .NET 10. That closes the gap noted at the end of `BACKEND_PROGRESS.md`.

## How Yash works
- Concise, actionable docs; no padding.
- For build work, show the steps with estimated time, overall and per section.
- Ask when a rule is unclear or two sources conflict. Don't guess.
- One service per chat. End each chat with an updated `BACKEND_PROGRESS.md` and the code as a zip (or a GitHub push).

## Files
| File | What |
|---|---|
| `BRD.md` | Final business requirements (source of truth) |
| `Skillmuni-India-BRD-appendix.xlsx` | XP table, badge catalogue, and the other BRD lists |
| `BACKEND_RULES.md` | Repo rules → `CLAUDE.md` |
| `BACKEND_HANDOFF.md` | Services, events, build order |
| `BACKEND_PROGRESS.md` | What is built, decisions B-01 to B-36, gaps |
| `API_CONTRACT.md` | Endpoints and payloads |
| `FRONTEND_TYPES.md`, `FRONTEND_MOCKS.md` | The frontend's types and mock backend, extracted |
| `ASSUMPTIONS.md` | Decision log A-1 to A-322 |
