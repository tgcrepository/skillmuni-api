# Learner service

Owns the learner profile the 8 registration quests build: quest answers and progress, the Player ID profile,
the Job Profile, drafts, the DigiLocker ID link, programme codes, feedback, Quest 4 trophies, ESCO skill search and
preferences. Schema `learner`. XP, levels and badges are credited by Gamification from Learner's events; Learner keeps
a read-only copy of the totals for the XP its own responses show. `GET me/progress` (API-125) is Gamification's.

**Job Profile lives here** (not in Careers as BACKEND_HANDOFF suggested) because Learner owns the quest answers it
renders (A-262: one record built from the 8 quests, no second form).

## Endpoints (`/api/v1`, learner session)
| Method | Path | Notes |
|---|---|---|
| GET | `me/quests/{slug}/answers` | API-126; `{}` when nothing saved |
| PUT | `me/quests/{slug}/parts/{part}` | API-127; `{ values, done, xp?, bonusXp? }`, shallow merge, 204, idempotent. The first `done` of a step in the quest in progress publishes `learner.quest.step-completed` with `paid` (BR-100/101, below) |
| POST | `me/quests/{slug}/complete` | API-128; `xpEarned` 0 on repeat; 422 `quest-locked` if earlier quests aren't done |
| PUT/GET | `drafts/{key}` | API-034/035; any JSON ≤ 64 KB; 404 `draft-not-found` |
| GET/DELETE | `me/identity` | API-064/067; JSON `null` when nothing linked |
| POST | `me/identity/digilocker/start` · `link` | API-065/066; India only; 503 `digilocker-unavailable` until partner credentials exist |
| GET | `programmes/{code}` | API-122; 404, then 429 `too-many-tries` after 5 wrong codes (30 min) |
| GET | `banks/ifsc/{code}` | API-123; Razorpay IFSC, cached 7 days; 422 `invalid-ifsc`, 404 `ifsc-not-found` |
| POST | `me/programme` | API-124; 409 `already-enrolled`, 422 `marks-too-low` / `bank-required` |
| POST | `me/feedback` | API-040; `{ xp }` 5 once per 30 days for the toast (Learner's own record); Gamification decides what is credited (BR-114) |
| GET | `profile` · `profile/edit` | API-115/117 |
| POST | `profile/reveal` | API-116; `no-store` |
| PUT | `profile` | API-120; `{ profile, xpEarned }` |
| POST/GET | `profile/photo` | API-121; multipart `photo`, ≤ 5 MB, type checked from the bytes |
| GET | `skills/esco?q&lang&limit` | API-057; 503 `esco-unavailable` |
| POST | `me/trophies/check-link` | API-058; 5 s, private addresses refused; 504 `link-timeout` |
| POST | `me/trophies/files` | API-059; multipart `file`, PDF or image ≤ 10 MB |
| POST | `me/trophies/digilocker` | API-060 |
| GET/PUT | `me/preferences` | MISS-09 `{ sounds, voice, language }` |
| GET | `job-profile` | API-068 `JobProfile` built from the quests; completeness = steps filled ÷ 18 (BR-40) |
| GET | `job-profile/cv-contact` | API-069 full contact for the learner's own CV file |
| PUT | `job-profile/about` | API-070 `{ text }` ≤ 400 chars; empty = back to Muni's summary |
| POST | `job-profile/cv` | API-071 multipart `cv`, PDF or Word ≤ 5 MB → `CvChange[]` (nothing applied); 422 `file-missing` / `too-large` / `unsupported-type` |
| POST | `job-profile/cv/apply` | API-072 `{ ids }` → `JobProfile`; jobs, skills and certificates are added to the quests (`fromCv: true`); contact changes are never applied (A-207) |
| GET/PUT | `job-profile/share` | API-073/074 `{ url, hideContact, expiresDays, viewsThisWeek }`; hide contact by default, 30 days; `expiresDays` 7 · 30 · 90 (A-208); views are 0 until the public CV page exists |
| PUT | `admin/programmes/{code}` | tenant-admin |
| GET | `/internal/learners/{id}/profile-progress` | A-262, internal ingress only |
| GET | `/internal/learners/{id}/careers-profile` | Careers matching, internal ingress only: `{ name, contact, city, state, relocate, skills: [{ name, evidence }] }` from the quests: Quest 1 name; Quest 2 mobile (masked like the Profile), city and state (`region`); Quest 6 relocate (`relocate` flag, else "relocate" in `where`, null if unanswered); Quest 4 deck, evidence `verified` when the skill's entry or a verified trophy / Quest 7 receipt naming it is verified, else `declared` (`assessed` and `level` belong to Assessment and languages, not this list); 404 `learner-not-found` |

`profile/contact/*` (API-118/119) stays in Identity (A-273).

## Quest rules
- 8 quests, 18 steps (BRD §3.3); badges per §4.3 (Main Character, Link Up, Big Brain, Player Card, Storyteller, Arc,
  Receipts, Vibe Check). Quest 5 "Work Lore" has 3 steps: work types, chapters, your timeline.
- **BR-100:** a step with required fields pays +20 the first time it is done in the quest in progress.
- **BR-101:** the all-optional steps (backup contact, education timeline, trophy shelf, receipts) pay only when the save
  adds something: not `xp: false`, no `<key>Skipped: true`, and at least one non-empty value. Otherwise the step is
  published with `paid: false` and, being done, can never pay later.
- **BR-119:** on `identity.claim.confirmed` quests 1–5 count as done and Learner publishes their step events (all paid)
  and quest events, so Gamification credits the normal 550 XP and badges. If it arrives before
  `identity.learner.registered`, the learner is started from it and the registration event fills in name and contacts.
- **BR-113:** the programme bonus pays +100 once per learner; a second programme enrols with `xpEarned: 0`.
- **BR-110:** the first time every step is filled (a skipped optional step is not), Learner publishes
  `learner.job-profile.completed` once (`learners.job_profile_completed_at`).

## Job Profile notes
- "Upload a newer CV" stores the file (Blob `job-profile/{learner}/cv-…`, the previous one is deleted) and derives the
  suggestions from the learner's latest CV read, which Learner keeps from `cv.parse.completed`
  (`job_profiles.cv_prefill`). The uploaded file is not parsed here: re-parsing goes through the CV service, whose
  next `cv.parse.completed` refreshes the suggestions. Suggestions: CV jobs not in Work Lore, skills not in the deck,
  certificates not on the trophy shelf (add), an email or mobile that differs from the verified one (keep).
- `strength.jobMatches` is 0 and `pending` is empty: matching belongs to Careers, programme requirements aren't modelled.
- The radar is the deck's skill levels per category scaled to the strongest; `archetype.strongest` is the top category.
- Field names are the quest screens' own (`features/quests/parts/*.ts`, `data/*.ts`): signature moves = the deck's
  first three skills with a level (q4-deck.ts order); `invitesBy` = "Email" (+ "WhatsApp" when Quest 2 opted in) while
  Quest 8 `invites` is on, else "Invites off"; proofs come from Quest 7 `receipts`, an object keyed `edu`, `tl-<id>`,
  `tr-<id>`, `ch-<id>` with `status` checking/verified (a verified or DigiLocker trophy is verified on its own);
  timeline entries: school levels titled `<level> · <stream>` under their board, courses by `name`, other
  qualifications (`level: "education"`) by `degree` or `qual` · `branch`; option labels from arc.ts (shifts, notice,
  salary). LinkedIn on the CV contact comes from the latest CV read (the quests don't ask for it).
- Careers profile (`internal/learners/{id}/careers-profile`): a deck skill is `verified` when linked (by skill id) from
  a verified trophy's `linkedSkills` or a work chapter (`skills`) whose `ch-<id>` receipt is verified; `relocate` =
  "relocate" in Pick Your Arc's `where`.
- Quest 1's `birthday` ("DD / MM / YYYY") and `gender` fill the Profile's date of birth and gender while those are empty.

## Events
Publishes: `learner.quest.step-completed` (`paid`), `learner.quest.completed`, `learner.profile.task-completed`,
`learner.job-profile.completed`, `learner.programme.enrolled`, `learner.feedback.sent` (no XP decision),
`learner.photo.changed`, `learner.identity.link-changed`.

Consumes (subscription `learner`): `identity.learner.registered`, `identity.contact.verified`,
`identity.claim.confirmed` (quests 1–5 done + their events), `identity.learner.erased`, `cv.parse.completed` (pre-fills
quests, the learner's own answers win; kept for Job Profile suggestions), `gamification.xp.awarded`*,
`assessment.attempt.submitted`*.
\* Payload proposed here (`Application/Events/ConsumedEvents.cs`); confirm when those services are built.

## PII
`learners`: names, phone, email, gender, date of birth · `linked_identities`: masked ID, last 4, birthday, name
(never the full number) · `enrolments`: bank holder, account (AES-256-GCM), last 4, routing, UPI · `quest_answers`,
`drafts`: free-form answers · `feedback.comment` · `job_profiles`: about text, CV read (`cv_prefill`) · Blob
`learner-files`: photos, certificates and Job Profile CVs.
Erasure (`identity.learner.erased`) deletes blobs, then every row.

## Config
| Key | Where |
|---|---|
| `ConnectionStrings:Learner` | Key Vault `learner-db-connection` |
| `Learner:Security:EncryptionKey` | Key Vault `learner-encryption-key` (32 bytes, base64) |
| `Learner:Storage:ServiceUri` (Azure, managed identity) or `ConnectionString` (Azurite) | Bicep / compose; in memory if neither (local only) |
| `Redis:Host` (Azure, managed identity) or `ConnectionStrings:Redis` (compose) | IFSC / ESCO cache and Idempotency-Key store; memory if neither |
| `Learner:DigiLocker:Mode` | `off` · `sandbox` (Development/Testing only) |
| `Learner:Quests`, `Learner:Levels`, `Learner:Xp`, `Learner:SkillOptions` | optional overrides of the code defaults |
| `Learner:JobProfile:ShareBaseUrl`, `ExpiryChoices`, `MaxCvBytes` | share link base (`https://skillmuni.app`), 7/30/90, 5 MB |
| `Learner:SampleProgrammes` | programme codes every tenant has, stored for the tenant on first lookup; empty in production, the two `programme.handlers.ts` codes in Development |
| `ServiceBus:Subscription` | `learner` |

Local `dotnet run` without compose: `dotnet user-secrets set Learner:Security:EncryptionKey "$(openssl rand -base64 32)"`.

New migration: `dotnet ef migrations add <Name> --project src/Services/Learner/Learner.Infrastructure --startup-project src/Services/Learner/Learner.Api -o Persistence/Migrations`.
