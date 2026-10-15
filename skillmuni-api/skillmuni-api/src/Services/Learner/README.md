# Learner service

Owns the learner profile the 8 registration quests build: quest answers and progress, the Player ID profile,
drafts, the DigiLocker ID link, programme codes, feedback, Quest 4 trophies, ESCO skill search and preferences.
Schema `learner`. XP, levels and badges are credited by Gamification from Learner's events; Learner keeps a
read-only copy of the totals.

## Endpoints (`/api/v1`, learner session)
| Method | Path | Notes |
|---|---|---|
| GET | `me/progress` | API-125 |
| GET | `me/quests/{slug}/answers` | API-126; `{}` when nothing saved |
| PUT | `me/quests/{slug}/parts/{part}` | API-127; `{ values, done, xp?, bonusXp? }`, shallow merge, 204, idempotent |
| POST | `me/quests/{slug}/complete` | API-128; `xpEarned` 0 on repeat; 422 `quest-locked` if earlier quests aren't done |
| PUT/GET | `drafts/{key}` | API-034/035; any JSON ≤ 64 KB; 404 `draft-not-found` |
| GET/DELETE | `me/identity` | API-064/067; JSON `null` when nothing linked |
| POST | `me/identity/digilocker/start` · `link` | API-065/066; India only; 503 `digilocker-unavailable` until partner credentials exist |
| GET | `programmes/{code}` | API-122; 404, then 429 `too-many-tries` after 5 wrong codes (30 min) |
| GET | `banks/ifsc/{code}` | API-123; Razorpay IFSC, cached 7 days; 422 `invalid-ifsc`, 404 `ifsc-not-found` |
| POST | `me/programme` | API-124; 409 `already-enrolled`, 422 `marks-too-low` / `bank-required` |
| POST | `me/feedback` | API-040; `{ xp }` 5 once per 30 days |
| GET | `profile` · `profile/edit` | API-115/117 |
| POST | `profile/reveal` | API-116; `no-store` |
| PUT | `profile` | API-120; `{ profile, xpEarned }` |
| POST/GET | `profile/photo` | API-121; multipart `photo`, ≤ 5 MB, type checked from the bytes |
| GET | `skills/esco?q&lang&limit` | API-057; 503 `esco-unavailable` |
| POST | `me/trophies/check-link` | API-058; 5 s, private addresses refused; 504 `link-timeout` |
| POST | `me/trophies/files` | API-059; multipart `file`, PDF or image ≤ 10 MB |
| POST | `me/trophies/digilocker` | API-060 |
| GET/PUT | `me/preferences` | MISS-09 `{ sounds, voice, language }` |
| PUT | `admin/programmes/{code}` | tenant-admin |
| GET | `/internal/learners/{id}/profile-progress` | A-262, internal ingress only |

`profile/contact/*` (API-118/119) stays in Identity (A-273).

## Events
Publishes: `learner.quest.step-completed`, `learner.quest.completed`, `learner.profile.task-completed`,
`learner.programme.enrolled`, `learner.feedback.sent`, `learner.photo.changed`, `learner.identity.link-changed`.

Consumes (subscription `learner`): `identity.learner.registered`, `identity.contact.verified`,
`identity.claim.confirmed`, `identity.learner.erased`, `cv.parse.completed` (pre-fills quests; the learner's own
answers win), `gamification.xp.awarded`*, `assessment.attempt.submitted`*.
\* Payload proposed here (`Application/Events/ConsumedEvents.cs`); confirm when those services are built.

## PII
`learners`: names, phone, email, gender, date of birth · `linked_identities`: masked ID, last 4, birthday, name
(never the full number) · `enrolments`: bank holder, account (AES-256-GCM), last 4, routing, UPI · `quest_answers`,
`drafts`: free-form answers · `feedback.comment` · Blob `learner-files`: photos and certificates.
Erasure (`identity.learner.erased`) deletes blobs, then every row.

## Config
| Key | Where |
|---|---|
| `ConnectionStrings:Learner` | Key Vault `learner-db-connection` |
| `Learner:Security:EncryptionKey` | Key Vault `learner-encryption-key` (32 bytes, base64) |
| `Learner:Storage:ServiceUri` (Azure, managed identity) or `ConnectionString` (Azurite) | Bicep / compose; in memory if neither (local only) |
| `ConnectionStrings:Redis` | optional; memory cache if empty |
| `Learner:DigiLocker:Mode` | `off` · `sandbox` (Development/Testing only) |
| `Learner:Quests`, `Learner:Levels`, `Learner:Xp`, `Learner:SkillOptions` | optional overrides of the code defaults |
| `ServiceBus:Subscription` | `learner` |

Local `dotnet run` without compose: `dotnet user-secrets set Learner:Security:EncryptionKey "$(openssl rand -base64 32)"`.

New migration: `dotnet ef migrations add <Name> --project src/Services/Learner/Learner.Infrastructure --startup-project src/Services/Learner/Learner.Api -o Persistence/Migrations`.
