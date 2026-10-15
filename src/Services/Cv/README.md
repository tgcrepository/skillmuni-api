# CV service

Reads a learner's CV so the 8 quests start pre-filled (A-245). Upload → background parse → status with Muni's
quips → "Here's what I found" summary → answers merged into Learner's quests by event. Schema `cv`.

## Endpoints (`/api/v1`, learner session)
| Method | Path | Notes |
|---|---|---|
| POST | `cv` | API-036; multipart `file`, or several `pages` (photos). 201 `{ cvId }`, `Location: …/status`. 422 `unsupported-type` (S1) / `too-large` (S2) / `file-missing` / `too-many-pages`; 429 `too-many-uploads` (10 a day) |
| GET | `cv/{id}/status` | API-037; `ParseStatus`: `parsing` with step 0–5 and a quip, then `done` or `failed` with the outcome |
| GET | `cv/{id}/summary` | API-038; `CvSummary`; 409 `cv-not-ready`, 410 `cv-parse-failed` |
| DELETE | `cv/{id}` | API-039; the file and the extract; 204 |

Someone else's CV id is always 404 `cv-not-found`.

## Parsing
- **Types** come from the bytes: PDF, DOCX, JPG, PNG, HEIC. Legacy `.doc` and WEBP are refused (S1): Document
  Intelligence can't read them (flagged: the dropzone still lists them).
- **Queue:** the `cv_documents` table. A background worker per replica claims jobs with a conditional UPDATE, requeues
  jobs a crashed worker left (5 min), and fails them after 3 attempts.
- **Reading:** Azure AI Document Intelligence `prebuilt-read` (managed identity, no keys). `sandbox` mode locally
  returns a sample CV; name the file like the frontend mock (`stuck`, `afrikaans`, `nowork`, `little`, `taken`,
  `blurry`, `za`) to see each outcome.
- **Interpreting:** rule-based (`CvInterpreter`): contact patterns per market, section headings, date ranges, a starter
  skill and language vocabulary. Replaceable by an LLM interpreter behind `ICvInterpreter` (flagged).
- **Outcomes:** S3 `locked-pdf`, S4 `blurry-photo`, S6 `parse-failed` + support ref fail the parse; S5
  `other-language`, S9 `email-taken`, S8 `little-found`, S7 `no-work-history` finish it with what was found.
- **Privacy:** ID numbers, marital status, date of birth and religion are listed under `skipped` and never stored;
  the raw text is never stored. Original files are deleted 30 days after parsing.

## Events
Publishes `cv.parse.completed` (quest slug → answers; Learner merges without overwriting) and `cv.document.deleted`.
Consumes `identity.learner.erased` (subscription `cv`). Calls Identity's internal `GET /internal/accounts/email-owner`
for S9.

## PII
`cv_documents.file_name`, `cv_documents.extract` (name, contact, education, work, skills, languages); Blob
`cv-files` (original files, 30 days).

## Config
| Key | Where |
|---|---|
| `ConnectionStrings:Cv` | Key Vault `cv-db-connection` |
| `Cv:Storage:ServiceUri` (Azure) or `ConnectionString` (Azurite) | Bicep / compose; in memory if neither (local only) |
| `Cv:Reader:Mode` · `Endpoint` | `document-intelligence` + endpoint (Bicep), or `sandbox` (Development/Testing only) |
| `Cv:IdentityBaseAddress` | `http://ca-identity/` in Azure |
| `Cv:MaxBytes`, `MaxPages`, `UploadsPerDay`, `SupportedLanguages`, `KeepOriginalFor`, `PollInterval` | optional |
| `Cv:Worker:Enabled` | `false` to run an API-only replica |
| `ServiceBus:Subscription` | `cv` |
| `Redis:Host` (Azure, managed identity) or `ConnectionStrings:Redis` (compose) | shared cache and Idempotency-Key store; memory if neither |
