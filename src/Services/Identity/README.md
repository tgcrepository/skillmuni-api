# Identity service

Owns accounts, logins, sessions, one-time codes, consents and institute claim links. Schema: `identity`.

## Endpoints (`/api/v1`)
| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `auth/otp` | – | Code to a registered mobile. 404 `account-not-found`, 423 `account-locked` (administratively locked account only) |
| POST | `auth/otp/verify` | – | 422 `invalid-code` (+`triesLeft`), 410 `code-expired`, 429 `too-many-tries` after the hidden limit of 20 wrong codes (BR-11, A-312); sets cookies |
| POST | `auth/email/login-code` · `auth/email/login-verify` | – | Same rules by email (A-159) |
| POST | `auth/login` | – | Password. 401 `wrong-credentials`; wrong passwords never lock the account (B-46: the gateway's per-IP and per-account rate limits apply); 423 only for an administratively locked account |
| POST | `auth/password-reset` | – | Always `{ resendIn, validFor }` |
| POST | `auth/password-reset/confirm` | – | **New.** `{ token, password }` from the emailed link; signs out every device |
| POST | `auth/register` · `auth/email/resend` · `auth/email/verify` | – | Sign-up. 409 `email-taken`, 422 `consent-required` |
| POST | `auth/google` | – | `{ user, isNew }`. 401 `google-invalid`, 503 `google-unavailable` |
| POST | `auth/refresh` | refresh cookie | **New (MISS-13).** The gateway normally refreshes silently |
| POST | `auth/logout` | – | 204, clears cookies |
| GET | `session` | session | 401 `no-session`; `photoUrl` from `learner.photo.changed` |
| POST | `me/phone/code` · `me/phone/verify` | learner | Quest 2 mobile; 409 `phone-taken` |
| POST | `profile/contact/code` · `profile/contact/verify` | learner | A-273 mobile/email change |
| GET | `claims/{token}` | – | 404 `claim-not-found`; 410 `claimed` · `expired` · `deleted`. Sends the code on first open |
| POST | `claims/{token}/otp` · `/verify` · `/decline` | – | |
| POST | `claims/{token}/consent` · `/sections/{slug}/confirm` · `/finish` | claimant | 403 `not-claimant`, 422 `sections-pending` |
| GET/POST/DELETE | `me/consents[/{scope}]` | learner | **New (MISS-07)** |
| GET/DELETE | `me/sessions[/{id}]` | learner | **New (MISS-13)** device sessions |
| POST | `me/data-export` | learner | **New (MISS-08)** 202 |
| DELETE | `me` | learner | **New (MISS-08)** erasure |
| POST | `admin/claims` | institute-admin | Creates a claim link; the token is returned once |

Internal, reachable only inside the Container Apps environment (internal ingress); the gateway proxies only
`/api/v1/…` (checked by `RouteTableTests`): `POST /internal/sessions/refresh` (gateway's silent refresh, called
directly) and `POST /internal/accounts/email-owner` `{ email }` → `{ accountId }` (CV's S9 check; the email travels in
the body, never in the URL). Health: `/health/live`, `/health/ready`.

## Codes
6 digits, 10 minutes, resend after 30 s, at most 5 codes an hour per target (B-06). Wrong codes: unlimited visible
tries with no count shown in the app; a hidden anti-abuse limit of 20 wrong codes in a row returns 429
`too-many-tries` (BR-11, A-312). `triesLeft` stays in the payloads the app types (challenges and `invalid-code`),
counted from that limit.

## Sessions
Access JWT (RS256, 10 min) in `sm_at`; rotating refresh token in `sm_rt` (path `/api`). Both HttpOnly, Secure,
SameSite=Lax. The gateway swaps the refresh cookie once the access token is 2 minutes old, which records
activity: 30 minutes idle or 30 days in all ends the session. A rotated token keeps working for 30 seconds
(parallel tabs); replaying it later revokes the session.

## Events (topic `skillmuni-events`, subject = event type)
`identity.learner.registered` · `identity.contact.verified` · `identity.claim.consented` · `identity.claim.confirmed` ·
`identity.claim.declined` · `identity.consent.changed` · `identity.data-export.requested` · `identity.learner.erased`.
Codes and reset links go to the `notifications` queue for the Platform service (message expires with the code).

Consumes (subscription `identity`, inbox table): `learner.photo.changed` → `accounts.photo_url`, returned as
`AuthUser.photoUrl` (A-277).

## PII
`accounts`: first/last name, email, phone, photo URL · `pending_registrations`: name, email · `claim_invites`: learner name,
phone, CV sections · `sessions.user_agent` · `verification_codes.target`. Codes, refresh tokens and link tokens are
stored only as HMAC-SHA256 hashes. Logs mask contacts and never contain codes or tokens. Erasure blanks the account
row (other services keep its id) and revokes every session.

## Configuration
| Key | Where | |
|---|---|---|
| `ConnectionStrings:Identity` | Key Vault `identity-db-connection` | MySQL, user `identity_svc` |
| `Auth:SigningPrivateKeyPem` / `Auth:SigningPublicKeyPem` | Key Vault | RSA key pair (raw or base64 PEM) |
| `Identity:Security:HashKey` | Key Vault `identity-hash-key` | base64, ≥ 32 bytes |
| `Identity:Security:DevelopmentFixedCode` | Development / Testing only | `123456` |
| `ServiceBus:FullyQualifiedNamespace` | app setting | managed identity; `ConnectionString` for the emulator |
| `ServiceBus:Subscription` | app setting | `identity` |
| `Tenancy:Tenants:<id>:Market` · `GoogleClientId` | app setting | `IN` / `ZA`; Google client per tenant (A-253) |
| `Identity:*` | app setting | `TriesPerCode` (hidden limit, 20), lifetimes, idle timeout, policy version |
| `Database:MigrateOnStartup` | Development / Testing | production uses the migration bundle |
| `Redis:Host` (Azure, managed identity) or `ConnectionStrings:Redis` (compose) | Bicep / compose | shared cache and Idempotency-Key store; memory if neither |
