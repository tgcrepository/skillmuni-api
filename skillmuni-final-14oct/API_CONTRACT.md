# SkillMuni India · API contract (Phase 1)

Generated 13 Oct 2026 from the BRD appendix. Source of truth for the .NET backend: every route below is what the Angular app calls today (served by the MSW mock in `skillmuni-web/src/mocks`). Base path `/api`, JSON, cookie session.

## Endpoints

| ID | Area | Method | Path | Client method | Response | Errors the UI handles |
|---|---|---|---|---|---|---|
| API-001 | Arena | GET | /api/arena | arena.board(scope: ArenaScope, period: ArenaPeriod) | ArenaBoard | 503 |
| API-002 | Arena | POST | /api/arena/players/:id/cheer | arena.cheer(playerId, on) | { cheers } | 503 |
| API-003 | Arena | POST | /api/arena/players/:id/challenge | arena.challenge(playerId: string) | void | 503 |
| API-004 | Career assessment | GET | /api/assessments/career | assessment.state() | AssessmentState | 404, 422 incomplete, 503, offline |
| API-005 | Career assessment | PUT | /api/assessments/career/answers | assessment.save(body: SaveRequest) | SaveResponse | 404, 422 incomplete, 503, offline |
| API-006 | Career assessment | POST | /api/assessments/career/submit | assessment.submit(answers: readonly Answer[]) | AssessmentResult | 404, 422 incomplete, 503, offline |
| API-007 | Career assessment | GET | /api/assessments/career/result | assessment.result(attempt?: number) | AssessmentResult | 404, 422 incomplete, 503, offline |
| API-008 | Career assessment | POST | /api/assessments/career/restart | assessment.restart() | AssessmentState | 404, 422 incomplete, 503, offline |
| API-009 | Career assessment | POST | /api/assessments/career/retake | assessment.retake() | AssessmentState | 404, 422 incomplete, 503, offline |
| API-010 | Career assessment | PUT | /api/assessments/career/remind | assessment.setRemind(on) | { remind } | 404, 422 incomplete, 503, offline |
| API-011 | Auth | POST | /api/auth/register | auth.register(data: Registration) | EmailChallenge | 401, 409 email taken, 410 code expired, 422 wrong code, 423 locked, 429 too many tries |
| API-012 | Auth | POST | /api/auth/email/resend | auth.resendEmailCode(email: string) | EmailChallenge | 401, 409 email taken, 410 code expired, 422 wrong code, 423 locked, 429 too many tries |
| API-013 | Auth | POST | /api/auth/email/verify | auth.verifyEmail(email: string, code: string) | AuthUser | 401, 409 email taken, 410 code expired, 422 wrong code, 423 locked, 429 too many tries |
| API-014 | Auth | POST | /api/auth/otp | auth.sendOtp(phone: string) | OtpChallenge | 404 account-not-found (mobile not registered → Sign up, BR-136), 423 locked |
| API-015 | Auth | POST | /api/auth/otp/verify | auth.verifyOtp(phone: string, code: string) | AuthUser | 410 code expired, 422 wrong code, 429 throttled (hidden limit, A-312) |
| API-016 | Auth | POST | /api/auth/email/login-code | auth.sendEmailLoginCode(email: string) | OtpChallenge | 404 account-not-found (email not registered → Sign up, BR-136), 423 locked |
| API-017 | Auth | POST | /api/auth/email/login-verify | auth.verifyEmailLogin(email: string, code: string) | AuthUser | 401, 409 email taken, 410 code expired, 422 wrong code, 423 locked, 429 too many tries |
| API-018 | Auth | POST | /api/auth/google | auth.googleSignIn(credential: string) | GoogleSignIn | 401, 409 email taken, 410 code expired, 422 wrong code, 423 locked, 429 too many tries |
| API-019 | Auth | POST | /api/auth/login | auth.loginWithPassword(email: string, password: string) | AuthUser | 401, 409 email taken, 410 code expired, 422 wrong code, 423 locked, 429 too many tries |
| API-020 | Auth | POST | /api/auth/password-reset | auth.requestPasswordReset(email: string) | ResetRequested | 401, 409 email taken, 410 code expired, 422 wrong code, 423 locked, 429 too many tries |
| API-021 | Auth | POST | /api/auth/logout | auth.logout() | void | 401, 409 email taken, 410 code expired, 422 wrong code, 423 locked, 429 too many tries |
| API-022 | Auth | GET | /api/session | auth.me() | AuthUser | 401, 409 email taken, 410 code expired, 422 wrong code, 423 locked, 429 too many tries |
| API-023 | Institute claim | GET | /api/claims/:token | claim.invite(token: string) | ClaimInvite | 404, 410, 422, 429 |
| API-024 | Institute claim | POST | /api/claims/:token/otp | claim.resend(token: string) | OtpChallenge | 404, 410, 422, 429 |
| API-025 | Institute claim | POST | /api/claims/:token/verify | claim.verify(token: string, code: string) | AuthUser | 404, 410, 422, 429 |
| API-026 | Institute claim | POST | /api/claims/:token/consent | claim.consent(token: string, choices: ClaimConsent) | readonly ClaimSection[] | 404, 410, 422, 429 |
| API-027 | Institute claim | POST | /api/claims/:token/sections/:slug/confirm | claim.confirmSection(token: string, slug: string) | void | 404, 410, 422, 429 |
| API-028 | Institute claim | POST | /api/claims/:token/finish | claim.finish(token: string) | ClaimResult | 404, 410, 422, 429 |
| API-029 | Institute claim | POST | /api/claims/:token/decline | claim.decline(token: string, reason: DeclineReason \| null) | void | 404, 410, 422, 429 |
| API-030 | Contact & geo | POST | /api/me/phone/code | contact.sendPhoneCode(phone: string) | OtpChallenge | 404, 410, 422 |
| API-031 | Contact & geo | POST | /api/me/phone/verify | contact.verifyPhone(phone: string, code: string) | void | 404, 410, 422 |
| API-032 | Contact & geo | GET | /api/geo/:market/postal/:code | contact.lookupPostal(market: MarketCode, code: string) | PostalLookup | 404, 410, 422 |
| API-033 | Contact & geo | GET | /api/geo/:market/regions | contact.regions(market: MarketCode) | readonly string[] | 404, 410, 422 |
| API-034 | Drafts | PUT | /api/drafts/:key | OfflineQueue.save (network/offline-queue.ts) | void | 404 |
| API-035 | Drafts | GET | /api/drafts/:key | Draft restore (dev states preview) | draft value | 404 |
| API-036 | CV | POST | /api/cv | cv.upload(file \| files[]) — multipart, progress events | { id } | 404; parse outcomes in status |
| API-037 | CV | GET | /api/cv/:id/status | cv.status(cvId: string) | ParseStatus | 404; parse outcomes in status |
| API-038 | CV | GET | /api/cv/:id/summary | cv.summary(cvId: string) | CvSummary | 404; parse outcomes in status |
| API-039 | CV | DELETE | /api/cv/:id | cv.remove(cvId: string) | void | 404; parse outcomes in status |
| API-040 | Feedback | POST | /api/me/feedback | feedback.send(answer) | { xp } | — |
| API-041 | Hire Fest | GET | /api/fests/summary | fest.summary() | FestSummary | 404, 500, 503 |
| API-042 | Hire Fest | GET | /api/fests | fest.fests(filter: string) | readonly Fest[] | 404, 500, 503 |
| API-043 | Hire Fest | GET | /api/fests/bookings | fest.bookings() | readonly Booking[] | 404, 500, 503 |
| API-044 | Hire Fest | GET | /api/fests/bookings/:id | fest.booking(id: string) | Booking | 404, 500, 503 |
| API-045 | Hire Fest | POST | /api/fests/bookings/:id/cancel | fest.cancel(id: string, employerId: string \| null, reason: string) | Booking \| null | 404, 500, 503 |
| API-046 | Hire Fest | GET | /api/fests/past | fest.past() | readonly PastFest[] | 404, 500, 503 |
| API-047 | Hire Fest | PUT | /api/fests/past/:id/outcome | fest.outcome(festId: string, employerId: string, outcome: Outcome) | PastFest | 404, 500, 503 |
| API-048 | Hire Fest | PUT | /api/fests/past/:id/rating | fest.rate(festId: string, stars: number) | PastFest | 404, 500, 503 |
| API-049 | Hire Fest | GET | /api/fests/alerts | fest.alerts() | FestAlerts | 404, 500, 503 |
| API-050 | Hire Fest | PUT | /api/fests/alerts | fest.setAlerts(a: FestAlerts) | FestAlerts | 404, 500, 503 |
| API-051 | Hire Fest | GET | /api/fests/:id | fest.fest(id: string) | Fest | 404, 500, 503 |
| API-052 | Hire Fest | PUT | /api/fests/:id/saved | fest.save(id: string, saved: boolean) | Fest | 404, 500, 503 |
| API-053 | Hire Fest | GET | /api/fests/:id/slots | fest.slots(id: string, employers: readonly string[]) | SlotGrid | 404, 500, 503 |
| API-054 | Hire Fest | POST | /api/fests/:id/book | fest.book(id: string, body: BookRequest) | Booking | 404, 500, 503 |
| API-055 | Hire Fest | GET | /api/fests/:id/waitlist | fest.waitlistInfo(festId: string, employerId: string) | WaitlistInfo | 404, 500, 503 |
| API-056 | Hire Fest | POST | /api/fests/:id/waitlist | fest.waitlist( festId: string, employerId: string, auto: boolean, whatsapp: boolean, ) | WaitlistInfo | 404, 500, 503 |
| API-057 | Quest 4 (skills, trophies) | GET | /api/skills/esco | esco.search(text: string, language: string, limit = 24) | EscoSearch | 503, 504 link check timeout |
| API-058 | Quest 4 (skills, trophies) | POST | /api/me/trophies/check-link | flex.checkLink(url: string) | LinkCheck | 503, 504 link check timeout |
| API-059 | Quest 4 (skills, trophies) | POST | /api/me/trophies/files | flex.upload(file) — multipart, progress events | { fileId } | 503, 504 link check timeout |
| API-060 | Quest 4 (skills, trophies) | POST | /api/me/trophies/digilocker | flex.importDigilocker() | readonly ImportedTrophy[] | 503, 504 link check timeout |
| API-061 | Lobby | POST | /api/home/first-steps/:id | home.completeStep(id: FirstStep['id']) | void | 503; 409 claim before done |
| API-062 | Lobby | POST | /api/home/first-moves/claim | home.claimFirstMoves() | { xp } | 503; 409 claim before done |
| API-063 | Lobby | GET | /api/home | home.feed() | HomeFeed | 503; 409 claim before done |
| API-064 | Identity | GET | /api/me/identity | identity.current() | LinkedId \| null | 400 |
| API-065 | Identity | POST | /api/me/identity/digilocker/start | identity.start(doc) | { authorizeUrl } | 400 |
| API-066 | Identity | POST | /api/me/identity/digilocker/link | identity.link(doc: DigiDoc, code: string) | LinkedId | 400 |
| API-067 | Identity | DELETE | /api/me/identity | identity.unlink() | void | 400 |
| API-068 | Job Profile | GET | /api/job-profile | job-profile.profile() | JobProfile | 503 |
| API-069 | Job Profile | GET | /api/job-profile/cv-contact | job-profile.cvContact() | CvContact | 503 |
| API-070 | Job Profile | PUT | /api/job-profile/about | job-profile.saveAbout(text: string) | JobProfile | 503 |
| API-071 | Job Profile | POST | /api/job-profile/cv | jobProfile.uploadCv(file) | CvChange[] | 503 |
| API-072 | Job Profile | POST | /api/job-profile/cv/apply | job-profile.applyChanges(ids: readonly string[]) | JobProfile | 503 |
| API-073 | Job Profile | GET | /api/job-profile/share | job-profile.share() | ShareSettings | 503 |
| API-074 | Job Profile | PUT | /api/job-profile/share | job-profile.updateShare(s: Pick<ShareSettings, 'hideContact' \| 'expiresDays'>) | ShareSettings | 503 |
| API-075 | My Jobs | GET | /api/jobs/summary | jobs.summary() | JobsSummary | 404, 503 |
| API-076 | My Jobs | GET | /api/jobs/list/:tab | jobs.jobs(tab: 'available' \| 'matching' \| 'bookmarked', q: ListQuery) | ListResult<Job> | 404, 503 |
| API-077 | My Jobs | GET | /api/jobs/invitations | jobs.invitations(q: ListQuery) | ListResult<Invitation> | 404, 503 |
| API-078 | My Jobs | GET | /api/jobs/applications | jobs.applications(q: ListQuery) | ListResult<Application> | 404, 503 |
| API-079 | My Jobs | GET | /api/jobs/job/:id | jobs.job(id: string) | Job | 404, 503 |
| API-080 | My Jobs | GET | /api/jobs/invitations/:id | jobs.invitation(id: string) | Invitation | 404, 503 |
| API-081 | My Jobs | GET | /api/jobs/applications/:id | jobs.application(id: string) | Application | 404, 503 |
| API-082 | My Jobs | PUT | /api/jobs/job/:id/saved | jobs.save(id: string, saved: boolean) | Job | 404, 503 |
| API-083 | My Jobs | GET | /api/jobs/job/:id/apply | jobs.applyInfo(id: string) | ApplyInfo | 404, 503 |
| API-084 | My Jobs | POST | /api/jobs/cv | jobs.uploadCv(file) | { id, name, meta } | 404, 503 |
| API-085 | My Jobs | POST | /api/jobs/job/:id/apply | jobs.apply(id: string, body: ApplyRequest) | Application | 404, 503 |
| API-086 | My Jobs | POST | /api/jobs/invitations/:id/:action | jobs.respond( id: string, action: 'accept' \| 'decline' \| 'suggest', note?: string, ) | Invitation | 404, 503 |
| API-087 | My Jobs | POST | /api/jobs/applications/:id/withdraw | jobs.withdraw(id: string) | Application | 404, 503 |
| API-088 | My Jobs | GET | /api/jobs/alerts | jobs.alerts() | AlertSettings | 404, 503 |
| API-089 | My Jobs | PUT | /api/jobs/alerts | jobs.setAlerts(body: AlertSettings) | AlertSettings | 404, 503 |
| API-090 | Launchpad | GET | /api/launchpad/readiness | launchpad.readiness() | Readiness | 503 |
| API-091 | Launchpad | GET | /api/launchpad/profile | launchpad.profile() | ProfileCard | 503 |
| API-092 | Launchpad | GET | /api/launchpad/assessment | launchpad.assessment() | AssessmentCard | 503 |
| API-093 | Launchpad | GET | /api/launchpad/jobs | launchpad.jobs() | JobsCard | 503 |
| API-094 | Launchpad | GET | /api/launchpad/fest | launchpad.fest() | FestCard | 503 |
| API-095 | Launchpad | GET | /api/launchpad/sectors | launchpad.sectors() | readonly Sector[] | 503 |
| API-096 | Launchpad | PUT | /api/launchpad/fest/alerts | launchpad.setFestAlerts(on: boolean) | FestCard | 503 |
| API-097 | Learn | GET | /api/learn/modules/:module | learn.module(slug: string) | ModuleDetail | 404, 503 |
| API-098 | Learn | GET | /api/learn/modules/:module/topics/:topic | learn.topic(module: string, topic: string) | TopicDetail | 404, 503 |
| API-099 | Learn | PUT | /api/learn/briefs/:id/bookmark | learn.bookmark(briefId: string, on: boolean) | void | 404, 503 |
| API-100 | Learn | PUT | /api/learn/briefs/:id/feedback | learn.feedback(briefId: string, helpful: boolean \| null) | void | 404, 503 |
| API-101 | Learn | GET | /api/learn/modules/:module/topics/:topic/quiz | learn.quiz(module: string, topic: string) | Quiz | 404, 503 |
| API-102 | Learn | PUT | /api/learn/quizzes/:id/draft | learn.saveDraft(quizId: string, answers: Readonly<Record<string, string>>) | void | 404, 503 |
| API-103 | Learn | POST | /api/learn/quizzes/:id/submit | learn.submit(quizId: string, answers: Readonly<Record<string, string>>) | QuizResult | 404, 503 |
| API-104 | Matches | GET | /api/me/matches/preview | matches.preview() | MatchPreview | 500 |
| API-105 | Matches | POST | /api/me/profile/publish | matches.publish(settings: VisibilitySettings) | void | 500 |
| API-106 | Matches | GET | /api/me/matches | matches.board() | MatchBoard | 500 |
| API-107 | Matches | POST | /api/me/matches/:id/apply | matches.apply(id: string) | void | 500 |
| API-108 | Matches | POST | /api/me/matches/:id/save | matches.save(id: string, saved: boolean) | void | 500 |
| API-109 | Matches | POST | /api/me/matches/:id/accept | matches.acceptInvite(id: string) | void | 500 |
| API-110 | Muni chat | GET | /api/muni/welcome | muni.welcome() | MuniWelcome | 503 |
| API-111 | Muni chat | POST | /api/muni/messages | muni.ask(input: { readonly text: string; readonly intent?: string }) | MuniReply | 503 |
| API-112 | Muni chat | POST | /api/muni/reveal | muni.reveal(field) | { value } | 503 |
| API-113 | Muni chat | PUT | /api/muni/messages/:id/rating | muni.rate(messageId: string, helpful: boolean \| null) | void | 503 |
| API-114 | Muni chat | GET | /api/muni/history | muni.history() | readonly MuniHistoryItem[] | 503 |
| API-115 | Profile | GET | /api/profile | profile.profile() | PlayerProfile | 400, 422, 503 |
| API-116 | Profile | POST | /api/profile/reveal | profile.reveal(field) | { value } | 400, 422, 503 |
| API-117 | Profile | GET | /api/profile/edit | profile.editable() | { … email, mobile } | 400, 422, 503 |
| API-118 | Profile | POST | /api/profile/contact/code | profile.sendContactCode(kind, value) | { sentTo } | 400, 422, 503 |
| API-119 | Profile | POST | /api/profile/contact/verify | profile.verifyContact(kind: ContactKind, value: string, code: string) | void | 400, 422, 503 |
| API-120 | Profile | PUT | /api/profile | profile.save(edit: ProfileEdit) | ProfileSaved | 400, 422, 503 |
| API-121 | Profile | POST | /api/profile/photo | profile.uploadPhoto(file) | ProfileSaved | 400, 422, 503 |
| API-122 | Programme | GET | /api/programmes/:code | programme.check(code: string) | Programme | 404, 429 cool-down |
| API-123 | Programme | GET | /api/banks/ifsc/:code | programme.ifsc(code: string) | BankBranch | 404, 429 cool-down |
| API-124 | Programme | POST | /api/me/programme | programme.submit(body: ProgrammeSubmission) | ProgrammeResult | 404, 429 cool-down |
| API-125 | Quests | GET | /api/me/progress | progress.get() | LearnerProgress (xp = ledger total, A-316) | — |
| API-126 | Quests | GET | /api/me/quests/:slug/answers | quest.answers(slug: string) | QuestAnswers | — |
| API-127 | Quests | PUT | /api/me/quests/:slug/parts/:part | QuestStore.savePart via OfflineQueue (quest.api.ts partPath) | void (204) | — |
| API-128 | Quests | POST | /api/me/quests/:slug/complete | quest.complete(slug: string) | QuestCompletion | — |
| API-129 | My Stats & badges | GET | /api/stats/rewards | stats.rewards() | RewardsDashboard | 503 |
| API-130 | My Stats & badges | GET | /api/stats/learning | stats.learning() | LearningDashboard | 503 |
| API-131 | My Stats & badges | GET | /api/stats/assessments | stats.assessments() | AssessmentsDashboard | 503 |
| API-132 | My Stats & badges | GET | /api/badges | stats.badges() | BadgeCatalogue | 503 |
| API-133 | My Stats & badges | PUT | /api/badges/showcase | stats.setShowcase(ids: readonly string[]) | BadgeCatalogue | 503 |

## Missing APIs (needed, not in the mock)

| ID | Area | Endpoint(s) | Why |
|---|---|---|---|
| MISS-01 | XP | GET /api/me/xp/ledger; server-side XP award for every BR-1xx action | XP must have one source of truth with reasons (BR-115–117) |
| MISS-02 | Badges | Badge rules engine + GET /api/me/badges/events; award on learning, quiz, streak, Arena, assessment events | 48 catalogue badges have text only |
| MISS-03 | Daily quests | GET /api/me/daily-quests, POST /api/me/daily-quests/:id/claim, chest | Daily quests show XP but nothing credits it |
| MISS-04 | Streaks | GET /api/me/streak; learning-minute tracking (POST /api/learn/activity) | Streak and "Learn 15 min" need minutes |
| MISS-05 | Launchpad | POST /api/launchpad/missions/:id/claim (or auto-credit) | Mission XP shown, never credited |
| MISS-06 | Notifications | GET /api/me/notifications, PUT read, push/WhatsApp/SMS preferences | Bell is a placeholder |
| MISS-07 | Consent | POST /api/me/consents (policy version, scope, time); GET for audit; withdraw | DPDP/POPIA proof of consent |
| MISS-08 | Privacy | POST /api/me/data-export, DELETE /api/me (erasure), retention jobs | DPDP/POPIA rights; retention switch removed (A-287) |
| MISS-09 | Settings | GET/PUT /api/me/preferences (sounds, voice, language) | Audio prefs live only in the browser |
| MISS-10 | Rewards | Rewards store: GET /api/rewards/store, POST redeem; wallet history | Placeholder screens; coins not earned in Phase 1 |
| MISS-11 | Arena | League ladder, weekly reset job, challenge results | Arena rules flagged "to confirm" |
| MISS-12 | Tenant | GET /api/tenant/theme (logo, colours), market config from server | Theme and market are client config today |
| MISS-13 | Auth | Token refresh, device sessions, rate limits per IP | Mock uses a cookie flag |
| MISS-14 | Muni | Real intent catalogue / LLM endpoint, safety, history retention | Mock keyword intents |
| MISS-15 | TTS | Muni voice clips or streaming TTS | A-290 open |
