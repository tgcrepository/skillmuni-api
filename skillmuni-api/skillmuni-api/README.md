# skillmuni-api

SkillMuni Phase 1 backend: ASP.NET Core 10 microservices behind a YARP gateway, MySQL per-service schemas,
Azure Service Bus events through an outbox. Rules: [`CLAUDE.md`](CLAUDE.md).

| Path | What |
|---|---|
| `src/Gateway/SkillMuni.Gateway` | YARP `/api/v1/*`, JWT check, silent session refresh, rate limits, CORS |
| `src/BuildingBlocks` | `Common` (Result, paging, tenancy, events, blob contract) · `Web` (errors, auth, versioning, health) · `Messaging` (outbox, inbox, Service Bus) · `Storage` (Blob Storage) |
| `src/Services/Identity` | Login, sign-up, sessions, Google, contact verification, institute claims, consents — see its [README](src/Services/Identity/README.md) |
| `src/Services/Cv` | CV upload, background parsing (Document Intelligence), summary, quest pre-fill — see its [README](src/Services/Cv/README.md) |
| `src/Services/Gamification` | XP ledger, levels, badges, streaks, daily quests, Arena, Rewards — see its [README](src/Services/Gamification/README.md) |
| `src/Services/Learner` | 8 quests, progress, profile + photo, drafts, DigiLocker, programme codes, trophies, ESCO, preferences — see its [README](src/Services/Learner/README.md) |
| `tests` | Unit tests (Application layer, gateway), integration tests (Testcontainers MySQL), `SkillMuni.Testing` helpers |
| `deploy` | `bicep/` for Azure, `local/` for docker compose |

## Run locally
```bash
./scripts/dev-setup.sh          # writes .env: local passwords, RSA signing keys, HMAC key, AES key
docker compose up --build       # gateway on http://localhost:5080
```
Every login code is `123456` locally (Development only; start-up fails if it's set anywhere else).
Frontend: set `environment.apiBaseUrl` to `/api/v1` and proxy `/api` to `http://localhost:5080`.
OpenAPI UI: `http://localhost:5080/swagger`. DigiLocker and the CV reader run as sandboxes locally (see each service's README).

## Build and test
```bash
dotnet build SkillMuni.sln          # warnings are errors
dotnet test SkillMuni.sln           # integration tests need Docker
dotnet format SkillMuni.sln --verify-no-changes
```

## Migrations
```bash
svc=Identity   # or Learner, Cv, Gamification
dotnet ef migrations add <Name> --project src/Services/$svc/$svc.Infrastructure --startup-project src/Services/$svc/$svc.Api -o Persistence/Migrations
dotnet ef migrations bundle --project src/Services/$svc/$svc.Infrastructure --startup-project src/Services/$svc/$svc.Api -o efbundle-$svc
```
Locally and in tests the service migrates on start-up. In Azure the release pipeline runs the bundle once
before rolling out new revisions, so replicas never race.

## Adding a service
Copy the Identity layout (`Api → Application → Domain`, `Infrastructure` implements Application interfaces),
call `AddSkillMuniServiceDefaults("<name>")` / `UseSkillMuniServiceDefaults()`, add its schema to
`deploy/local/mysql/01-schemas.sh`, its routes to the gateway's `ReverseProxy` config and a module call in `deploy/bicep/main.bicep`.
