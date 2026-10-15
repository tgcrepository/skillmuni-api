# Start here · move Skillmuni backend to the Max account

## Step 1 · Get the backend code (old account)
Open the 13 Oct backend chat and download its `skillmuni-api` zip (5 services written).

## Step 2 · Create the project (Max account)
1. Projects → New project → name it **Skillmuni Development**.
2. Upload every file in `docs/` to the project files.
3. Paste this into the project's **custom instructions**:

```
Skillmuni India backend (skillmuni-api). Always follow BACKEND_RULES.md (the repo's CLAUDE.md):
ASP.NET Core 10 and C#, microservices, Clean Architecture + SOLID, Controllers → Services →
Repositories with DTOs and Interfaces, Azure Key Vault + Managed Identity, centralised exception
handling, ILogger structured logging, pagination and caching, CancellationToken throughout,
universal coding standards. BRD.md section 4 is the final gamification rulebook; the frontend
mocks (FRONTEND_MOCKS.md) define how each endpoint behaves. When a rule is unclear or sources
conflict, ask; don't guess. One service per chat. Show the steps with time estimates. End every
chat with an updated BACKEND_PROGRESS.md and the code as a zip.
```

## Step 3 · First chat
Attach `skillmuni-api.zip` (from step 1) and `skillmuni-final-14oct.tgz`, then paste:

```
We're continuing the Skillmuni India backend (skillmuni-api) from another account.
Read PROJECT_CONTEXT.md, BACKEND_RULES.md, BACKEND_PROGRESS.md and BRD.md section 4 first.

Attached: the backend code (5 services written, never compiled) and the finished frontend.

Today:
1. Copy BACKEND_RULES.md into the repo as CLAUDE.md, replacing the old one.
2. Check the code against every rule in CLAUDE.md and list anything that breaks one.
3. Fix the gaps listed in the "Changed after the backend was written" table in
   PROJECT_CONTEXT.md, so Identity, Learner and Gamification match the final BRD.
4. Build and run the tests if this workspace can (needs the .NET 10 SDK and Docker). If it
   can't, say so and give me the exact commands to run on a machine that has them.
5. Give me the steps with time estimates before you start, then work through them.
End with an updated BACKEND_PROGRESS.md and the code as a zip.
```

## Step 4 · Every chat after that
Attach the latest zip, then paste:

```
Continue skillmuni-api from BACKEND_PROGRESS.md. Follow CLAUDE.md (BACKEND_RULES.md) strictly.
Latest code attached. Today: <next service from BACKEND_PROGRESS.md>.
```

The order after the first chat: Assessment → Learning → Careers → Muni → Platform.

## Note
The old chat's workspace couldn't download .NET packages, so the code was never compiled. If the
Max account's workspace can't either, run `dotnet build SkillMuni.sln && dotnet test SkillMuni.sln`
on a laptop with the .NET 10 SDK and Docker (or ask a developer), and paste any errors into the chat.
