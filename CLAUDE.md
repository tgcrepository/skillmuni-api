# SkillMuni backend · repository rules

Copy this file into the backend repo as `CLAUDE.md`. It applies to every service. When a rule here and the BRD disagree, ask; don't guess.

## 1. Stack
- **Framework:** ASP.NET Core 10 (LTS) and C# 14. Nullable reference types are on, and warnings are errors.
- **Architecture:** microservices, each built with Clean Architecture and SOLID. A service never references another service's code, only shared BuildingBlocks.
- **Hosting:** each service is one Docker image on **Azure Container Apps**, behind a **YARP gateway** (also on Container Apps).
- **Data:** **MySQL** on Azure Database for MySQL Flexible Server, with one server and **one schema per service**. A service reads and writes only its own schema.
- **Data access:** EF Core 10 with Oracle's `MySql.EntityFrameworkCore` 10.x provider (Pomelo has no EF Core 10 release; decision B-02). Migrations live per service.
- **Events:** **Azure Service Bus** topics carry cross-service events, written through an outbox table in the publishing service's schema.
- **Files:** Azure Blob Storage. **Secrets:** Azure Key Vault. **Access to Azure resources:** Managed Identity.
- **Observability:** OpenTelemetry, exported to Azure Monitor / Application Insights.
- **Conventions:** new repo, no legacy schema. The conventions are this file and the existing code once it exists.

## 2. Repo layout (monorepo `skillmuni-api`)
```
src/
  Gateway/                     YARP: routing, auth check, rate limits, CORS
  BuildingBlocks/              shared plumbing only (no business rules)
    SkillMuni.Common/          Result types, errors, paging, clock
    SkillMuni.Web/             exception handler, ProblemDetails, auth, versioning, health
    SkillMuni.Messaging/       Service Bus publisher/consumer, outbox
  Services/<Name>/
    <Name>.Api/                Controllers, request validation, DI wiring, Program.cs
    <Name>.Application/        Services (use cases), DTOs, Interfaces, validators
    <Name>.Domain/             Entities, value objects, domain rules (no framework refs)
    <Name>.Infrastructure/     Repositories, DbContext, migrations, external clients
tests/<Name>.UnitTests · <Name>.IntegrationTests (Testcontainers MySQL)
deploy/                        Bicep for Container Apps, MySQL, Service Bus, Key Vault
docker-compose.yml             local run: all services + gateway + MySQL + Service Bus emulator
```
- The dependency direction is Api → Application → Domain. Infrastructure implements Application interfaces.
- Controllers stay thin: validate, call a service, map to a DTO. No business logic or DbContext in controllers.
- Repositories return domain entities or projections, never `IQueryable` outside Infrastructure.

## 3. Coding standards
- Every service uses the same layers: Controllers → Services → Repositories, with DTOs and Interfaces. Follow SOLID and universal coding standards.
- Use C# naming (PascalCase types and members, camelCase locals, `I` prefix on interfaces, `Async` suffix on async methods).
- Use constructor DI only, with one interface per external dependency. No service locator and no `static` state.
- Use async all the way. Pass a `CancellationToken` through every async call. Never use `.Result` or `.Wait()`.
- Use records for DTOs. Validate with FluentValidation. Map by hand or with Mapster; no reflection magic in hot paths.
- No duplicated logic: shared plumbing goes in BuildingBlocks, and shared business rules belong to the one service that owns them.

## 4. API standards
- Use REST with resource nouns and plural collections. JSON is camelCase.
- **Versioning:** the URL segment `/api/v1/...` (Asp.Versioning). Breaking changes go in `v2`. Never change a published v1 contract.
- **Contract:** route paths and payloads must match `phase-1/API_CONTRACT.md` and the frontend's `src/app/core/**/**.api.ts` (the mock paths with `/v1` added). Each service publishes OpenAPI (Swashbuckle), and the gateway merges them.
- **Status codes:**
  - 200 read or update.
  - 201 create, with a `Location` header.
  - 204 when there is no body.
  - 400 for a malformed request, 422 for valid JSON that breaks a rule.
  - 401 and 403 for authentication and permission failures.
  - 404 not found, 409 conflict, 410 expired, 423 locked, 429 rate-limited, 503 dependency down.
- **Errors:** every error is RFC 7807 `application/problem+json` with two extensions the frontend already reads:
  - `message`: one human sentence.
  - `errors`: `{ "<field or code>": ["…"] }`.
  - Business codes go in `errors.code[0]`, for example `account-not-found`, `code-expired`, `too-many-tries`, `account-locked`, `email-taken`, `invalid-code`.
- **Paging:** `?page=1&pageSize=20` (max 100), and the response is `{ items, page, pageSize, total }`.
- **Idempotency:** POSTs that award XP or create records accept an `Idempotency-Key` header.

## 5. Database
- Use EF Core LINQ or `FromSql` with interpolated parameters only. Never concatenate SQL strings.
- Index every foreign key and every column used in a `WHERE` or `ORDER BY` on a hot path. Check new queries with `EXPLAIN`.
- Use one transaction per use case when it writes more than one table, together with the outbox row.
- Use `AsNoTracking()` for reads. Project only the columns you need. No N+1 queries (use `Include` or projection).
- Columns are UTC `DATETIME(6)`, utf8mb4, with surrogate `BIGINT` or `CHAR(36)` UUID keys.
- Soft delete only where the BRD needs history.
- Privacy: never store a full Indian ID number (only the masked number and last 4, A-272). PII columns are listed in the service README.

## 6. Security
- No secrets in code, config files or images. Use Key Vault references and Managed Identity for Service Bus, Blob and Key Vault. MySQL is the one exception: services sign in with a password kept in Key Vault, because Oracle's provider has no token callback (B-11).
- **Auth:**
  - The Identity service issues a short-lived JWT in an HttpOnly, Secure, SameSite=Lax cookie, plus a refresh token.
  - The gateway validates the token and forwards claims. Each service validates again (defence in depth).
  - Authorization uses policies, with roles: learner, institute-admin, tenant-admin.
- **Tenancy:** every row carries `tenantId`, and every query filters by it (an EF global query filter).
- **Least privilege:** one MySQL user per service, with grants only on its own schema. Managed Identity roles are scoped to one resource each.
- **Rate limits:** set per IP and per account at the gateway. Login codes: unlimited visible tries with no count shown; a hidden anti-abuse limit (mock: 20) returns 429 (BR-11, A-312). Resend after 30 s.
- **Logs:** no PII or secrets. Mask mobile and email. Never log codes or tokens.

## 7. Errors and logging
- Use one global exception handler (`IExceptionHandler`) per service that maps exceptions to ProblemDetails. Use no try/catch in controllers.
- Don't throw for expected outcomes (not found, validation). Return a `Result<T>` and map it to a status code.
- Use structured `ILogger` logging with message templates (`"XP awarded {LearnerId} {Reason} {Xp}"`), never string interpolation.
- Add a correlation id to every request; the gateway sets it and services pass it on.
- 5xx responses say only "Something went wrong" plus the correlation id. No stack traces, SQL or internal names.

## 8. Performance
- Async I/O everywhere, with a `CancellationToken` on every endpoint and repository method.
- **Caching:** Redis (Azure Cache for Redis) for read-heavy, rarely-changing data: tenant theme, market config, badge catalogue, leaderboards. Use output caching for public GETs.
- Paginate every list. Make no database call inside a loop.
- Health checks at `/health/live` and `/health/ready` for Container Apps probes.

## 9. Microservice rules
- A service owns its data. Other services get it through an API call or an event, never a cross-schema join.
- **Events:**
  - Events are past-tense facts named `<service>.<entity>.<verb>`, for example `assessment.attempt.submitted`.
  - Consumers must be idempotent.
- Synchronous calls between services go through typed HttpClients with Polly retry, timeout and circuit breaker. Prefer events where possible.
- **XP is credited only by the Gamification service**, from events, once per reason (the ledger has a unique key on learnerId + reason + sourceId).

## 10. Definition of done
- Unit tests cover the Application layer.
- Integration tests run each endpoint against MySQL in Testcontainers.
- The contract matches the frontend: the same paths, fields and error codes as the mock.
- `dotnet format` and analyzers are clean, and the build has no warnings.
- The OpenAPI spec is updated, and the service README lists endpoints, events, PII and config keys.
