# Skillmuni India · Business Requirements (Phase 1)

Oct 14, 2026 · @Yash · Final for the backend build

## 1. Overview

This BRD is the backend’s build contract for the Skillmuni learner web app as it stands on **14 Oct 2026 (final for the backend build)**. It describes what is built in skillmuni-web and its mock backend. Where this BRD and any other document disagree, this BRD wins, then docs/ASSUMPTIONS.md. \[TBD\] marks only what still needs a business decision (9.2); “Server rule” marks a rule the backend must implement that the mock does not simulate.

**Purpose.** Skillmuni is a white-label, gamified learning and employability app. A learner registers through 8 short quests (which also build their Job Profile), then learns, takes the Career Drivers assessment, competes in the Arena, and finds jobs and hiring fests through the Career Launchpad. Muni, a mascot guide, talks to the learner in short written lines on every screen (no voice, A-314).

**Scope (Phase 1).** India first; South Africa is the same app switched by market config (`src/app/core/market/market.ts`).

| Item | India (IN) | South Africa (ZA) |
| --- | --- | --- |
| Locale · dial code | en-IN · +91 | en-ZA · +27 |
| Mobile | 10 digits, starts 6–9 | market pattern |
| Postal code | PIN, 6 digits, not starting 0 | Postal code, 4 digits |
| Consent law | DPDP Act 2023 | POPIA |
| Identity | DigiLocker link (Aadhaar, PAN, driving licence); only doc type, masked number (last 4), verified name and DOB kept (A-272) | Typed SA ID (13 digits, Luhn) or passport |
| Race question | Not asked | Asked |
| Languages offered | 22 scheduled languages + Bhojpuri, Rajasthani, Tulu (A-243) | ZA official languages |

**Personas (A-232).**

| Persona | How they arrive | What they see |
| --- | --- | --- |
| New learner | Signs up ("Join the squad") and verifies email | Every feature in its first-time state: no assessment, applications, bookings, badges or streak; Lobby "Your first moves"; Launchpad Level 0; Arena "wide open" |
| Returning learner | Logs in with mobile or email OTP, or Google | Their own history (the mock uses the Figma sample learner) |
| Institute-claimed learner | Opens an SMS claim link from their institute (`/claim/:token`) | Pre-filled quests 1–5 to confirm, then the normal app |

**Out of scope for Phase 1.** Rewards store redemption, Wallet, Goals and Certificates screens ("on its way" placeholders, A-172); coins, the rewards store and vouchers (A-302, A-317); Muni’s spoken voice (A-314); employer, institute and admin portals; native apps; payments for paid fests (price shown only, A-230).

**How to read this.** IDs: **FR-xx** feature requirements, **BR-xx** business rules, **API-xx** endpoints (numbered in the .xlsx appendix). Every number cites its source: a file path under `src/`, or an assumption number (A-nnn in `docs/ASSUMPTIONS.md`), which overrides older design notes.

## 2. Information architecture

The app has 51 route entries (`src/app/features/*/*.routes.ts`); 5 screens are public, the rest need a session (`requireSession`). Registration is linear (login → onboarding → 8 quests → Lobby); after that the learner moves freely between four top-level destinations (Lobby, Arena, My Stats, Rewards), with the avatar opening the account menu (My profile, Log out) and Muni opening the chat drawer. The top bar shows on every signed-in screen at every size (A-315).

&#91;embedded content: learner journey · registration, then the Lobby hub\]

The top row happens once; after it, the Lobby links to four everyday areas, and the Launchpad opens the career screens.

| Area | Route(s) | Reached from | Session |
| --- | --- | --- | --- |
| Login | `/login`, `/login/forgot` | App start (`/` redirects here), sign-out | Public |
| Signup | `/signup` | Login “Join the squad” | Public |
| Legal | `/legal/terms`, `/legal/privacy` (`/legal` → terms) | Signup consent link (opens full screen), direct link | Public |
| Institute claim | `/claim/:token` | SMS link from the institute | Public |
| Welcome back | `/welcome-back` | Login when registration is unfinished; after Save & exit | Yes |
| Muni intro | `/start` | First login of a new learner | Yes |
| CV onboarding | `/onboarding/cv`, `/snap`, `/reading`, `/summary` | Muni intro “Upload my CV” | Yes |
| Quests | `/quests/:slug?part=n[&edit=1][&exit=url]` | Onboarding, Welcome back, Quest Lineup, Job Profile “Edit” | Yes |
| Matches | `/matches` | Vibe Check “Send it” | Yes |
| Programme bonus quest | `/programme` | Registration complete dialog “Got a programme code?” | Yes |
| Lobby | `/home`, `/knooks/:slug` | Nav “Lobby”; end of registration | Yes |
| Learn | `/learn/:module`, `/learn/:module/:topic`, `.../quiz`, `.../course`, `/learn/bookmarks` | Lobby pathways and knooks | Yes |
| Career Launchpad | `/launchpad` | Lobby pathway card | Yes |
| Job Profile | `/job-profile`, `/job-profile/cv`, `/job-profile/employer` | Launchpad, Profile | Yes |
| Career assessment | `/assessments/career`, `/assessments/career/report` | Launchpad, Lobby first moves, Profile | Yes |
| My Jobs | `/jobs` (+ `/matching`, `/invitations`, `/bookmarked`, `/applied`), `/jobs/view/:id`, `/jobs/invitations/:id`, `/jobs/applied/:id` | Launchpad, Muni nudge | Yes |
| Hire Fest | `/hire-fest` (+ `/bookings`, `/past`), `/hire-fest/fest/:id`; `/career-fest` redirects | Launchpad | Yes |
| Arena | `/arena` | Nav “Arena” | Yes |
| My Stats | `/stats`, `/stats/learning`, `/stats/assessments` (one route, A-267), `/stats/badges` | Nav “My Stats”, Profile | Yes |
| Profile | `/profile` | Avatar in the top bar | Yes |
| Placeholders | `/rewards`, `/goals`, `/certificates`, `/wallet`, any unknown path | Nav “Rewards”, Stats quick links | Yes |

**Navigation rules.**

- **BR-01** Back walks browser history and never pushes a new entry, so A ↔ B loops can’t happen; a deep link with no history goes to the screen’s parent and replaces the entry (A-234).
- **BR-02** Tabs that have their own URL are one screen: Hire Fest (Upcoming, My bookings, Past), My Jobs (5 tabs) and My Stats (3 views). Switching tabs replaces the history entry, and Back always leaves the section to where the learner came from (Launchpad by default for Hire Fest), never tab to tab, even after a booking opened My bookings (A-305, A-320).
- **BR-03** A quest opened from a platform screen shows “Save & back to \<screen>” and returns there on save; a section edit (`?edit=1`) opens just that part, pre-filled, with autosave off until Save (A-252, A-260, A-286).
- **BR-04** Go back on part 1 of a quest opens the last part of the previous quest; on Quest 1 it goes to Welcome back (A-293).
- **BR-05** During registration, Save & exit saves the step, signs the learner out and lands on Login; logging in resumes at the same step (A-252).
- **BR-06** A session idles out after 30 minutes (`src/environments/environment.ts` `sessionIdleMinutes`); expiry is a critical dialog that returns to Login with the return URL.

**BR-07** A signed-in learner opening Log in, Forgot password or Sign up goes to the Lobby instead (requireGuest in auth.guard.ts). Log out always ends the local session, even if the server call fails.

## 3. Feature specs

Each area lists what the learner can do (FR), the rules the server must enforce (BR), and the data it reads and writes. Every data screen has five states: loading (skeleton), empty (Muni with a line and next steps), error (“Try again”), offline (banner, answers queued), and first-time (A-232). These are not repeated per area unless something differs.

### 3.1 Sign-in and sign-up

| ID | What the learner can do |
| --- | --- |
| FR-01 | Log in with mobile number + 6-digit SMS code (registered mobile only, BR-136) |
| FR-02 | Log in with email + 6-digit email code (no password, A-159; registered email only, BR-136) |
| FR-03 | Continue with Google |
| FR-04 | Sign up: first name, last name (helper “Needed for your records.”), email, password, consent to Privacy Policy & Terms; then verify email with a 6-digit code. “You’re in” waits for the learner to press Continue; nothing moves on by itself (A-313) |
| FR-05 | Reset a forgotten password by email link |
| FR-06 | Log out from the avatar menu or Profile → Settings, always after the log-out dialog (4.8); or Save & exit during registration |

| Rule | Detail | Source |
| --- | --- | --- |
| BR-10 Code length | 6 digits for every code (SMS, email, claim, contact change) | `src/mocks/auth.handlers.ts` |
| BR-11 Wrong codes | Unlimited tries, no count shown; a wrong code says “Check it and try again.” A hidden anti-abuse limit (mock: 20) returns 429 and “Let’s take a breather…”; an old code returns 410 “Code expired” (A-312) | `auth.handlers.ts` `MAX_TRIES` |
| BR-12 Resend | Allowed after 30 s (SMS and email login), 45 s for password reset; reset link valid 30 min | `auth.handlers.ts` |
| BR-13 Locked account | Returns 423; the screen says so and offers support | `auth.handlers.ts` |
| BR-136 Login needs an account | Send-code (mobile or email) first checks the account exists. Unknown: 404 account-not-found, no code sent, and the app opens Sign up with an info banner; an email is pre-filled, a mobile gets “sign up with email, add your mobile in Profile”. The typed value travels in navigation state, never the URL. Google sign-in is unchanged (A-308) | `auth.handlers.ts` |
| BR-14 Email in use | Sign-up returns 409 “Already in the squad” with a link to log in | `auth.handlers.ts` |
| BR-15 Password | 8+ characters, at least 1 number and 1 letter; strength meter 0–4 | `src/app/features/auth/signup/password-rules.ts` |
| BR-16 Mobile and email format | IN mobile 10 digits starting 6–9; email checked with typo suggestions | `src/app/core/market/market.ts` |
| BR-17 Persona | Sign-up = new learner; mobile, email or Google login = returning learner; a new learner who left registration midway logs back in as the same new learner | A-232, A-252 |
| BR-18 Reset privacy | The reset response never reveals whether the email has an account | `forgot-password-page.ts` |

The login screen shows no SMS-rates line (A-287). The consent link opens Privacy Policy & Terms full screen, and typed sign-up data is kept (A-289, A-313).

&#91;embedded content: log in and sign-up confirmation · 2 flows\]

A login code goes only to a registered mobile or email; an unknown one gets 404 and the learner is taken to Sign up with what they typed kept (BR-136). Logging in lands on the Lobby, or on the link the learner was opening; a new Google account and every sign-up go to the Muni intro. A locked account gets 423 (BR-13).

### 3.2 Onboarding: Muni intro and CV

| ID | What the learner can do |
| --- | --- |
| FR-07 | Meet Muni (`/start`), then choose “Upload my CV” or “Type it myself” |
| FR-08 | Upload a CV file, or photograph a paper CV page by page |
| FR-09 | Watch Muni read the CV, then review a summary of what was found, with each item linked to the quest it fills |
| FR-10 | Delete the uploaded CV at any time |

| Rule | Detail | Source |
| --- | --- | --- |
| BR-20 File types | PDF, DOC, DOCX, JPG, PNG, HEIC, WEBP | `src/app/core/cv/cv.api.ts` `CV_ACCEPT` |
| BR-21 Size | Up to 10 MB | `cv.api.ts` `CV_MAX_BYTES` |
| BR-22 Photo mode | Up to 10 pages | `cv-snap-page.ts` `MAX_PAGES` |
| BR-23 Consent | Upload is blocked until the learner ticks consent to Skillmuni reading the CV | `cv-start-page.html` |
| BR-24 Slow parse | After 20 s Muni says it’s taking longer; the learner can continue by hand | `cv-reading-page.ts` |
| BR-25 Parse outcomes the server must return | Locked PDF, blurry photo, unsupported language, stuck, no work history, little found, CV already linked to another account | `src/mocks/cv.handlers.ts` |
| BR-26 Sensitive fields | The parser should not store photo, ID number, religion or marital status; the UI no longer advertises this (A-287) **\[TBD: confirm parser behaviour\]** | A-287 |

- **BR-27** The country on the CV start screen is locked to the learner’s market (India or South Africa) and can’t be changed there (A-313).

Parsed data pre-fills Quests 1–5 (name, contact, education, skills with ESCO ids, languages, work chapters). Pre-filled answers still need the learner to confirm each step.

### 3.3 The 8 registration quests

Registration is 8 quests with 18 steps (“parts”). Quests unlock in order; a finished quest can be reopened. Every step autosaves; offline saves queue and replay (`src/app/core/quests/quest-store.ts`). Finishing all 8 publishes the profile to employers (Quest 8) and lands the learner in the Lobby.

| # | Quest | Steps | What is captured | Key rules |
| --- | --- | --- | --- | --- |
| 1 | Who Dis? | Your name · ID & birthday · The tea | First and last name (required, used on certificates), nickname (≤ 30 chars); IN: DigiLocker link (Aadhaar, PAN or driving licence) giving masked number + DOB; ZA: typed ID + birthday; gender, home language, disability (types, %, certificate, support), race (ZA only) | Age 13–80 (`birthday.ts`); IN never stores a full ID number (A-272) |
| 2 | Hit Me Up | Your digits · Backup contact (optional) · Your spot | Mobile (verified by code), email (required), WhatsApp opt-in; backup contact relation, name, phone, alternate email (all optional; the step pays XP only with the backup mobile, BR-101); address: postal code, state, district, city, area, line 1, unit, landmark | Backup email can’t match the main email; a valid postal code auto-fills state and city (`/api/geo`) |
| 3 | Big Brain Stuff | Your level · The details · Timeline (optional) | Highest level (IN: 10th, 12th, ITI, Diploma, Graduate, Postgraduate, PhD, None) + completed/studying; degree, branch, institute, board, year, study year, score as percentage, CGPA or grade; earlier education and courses | Timeline max 6 entries including the highest level (`education.ts` `MAX_TIMELINE`, A-244) |
| 4 | Your Flex Era | Skill deck · Language passport · Trophy shelf (optional) | Skills from the ESCO list or custom, each with level 1–3 (Just started, Getting there, Confident); top 3 = signature moves; languages with fluency (basic to native) and speak/read/write; certificates as trophies (link, file or DigiLocker) | Deck 3–50 skills (`skills.ts`, A-287); custom skill ≤ 40 chars; trophies ≤ 20, files PDF/JPG/PNG ≤ 5 MB (`flex.ts`); ends with the Player Card reveal |
| 5 | Work Lore | Work types · Chapters · Your timeline | Fresher flag or work types; each chapter: role, company, dates, description, skills used; reasons for gaps | Max 10 chapters, description ≤ 300 chars, ≤ 5 skills per chapter, names ≤ 60 chars (`work.ts`); a gap of 3+ months asks for a reason |
| 6 | Pick Your Arc | Your arc | Looking for (full-time, part-time, internship, apprenticeship, freelance, short course), roles, where (near, region, relocate, remote), shifts, notice, expected pay | Up to 8 roles, each ≤ 60 chars (`q6-arc.ts`); roles suggested from the CV |
| 7 | Drop the Receipts | Receipts | One proof per earlier claim (education, timeline, trophies, work) | Uploads go to “Checking”; verified proof counts 1.0 in matching vs 0.8 declared; ID documents are never shown to employers (`q7-receipts.ts`) |
| 8 | Vibe Check | Vibe check | Review of all answers; “Let employers on Skillmuni find me” (default on); “Employers can invite me by email and WhatsApp” (default on) | “Send it” publishes the profile (`POST /api/me/profile/publish`); no retention switch (A-287) |

Quest rules:

- **BR-30** Each step shows “X of Y answered”, and every field carries a “Needed” or “Optional” tag (read-only and add-more fields carry none). Needed fields block “Next part”; optional steps can be skipped and filled later from Job Profile. Long titles wrap to 2 lines on phones and footers wrap rather than cut a button (A-311, A-319).
- **BR-31** Pre-filled answers (CV or institute claim) still need the learner to confirm each step.
- **BR-32** Editing a section later (`?edit=1`) earns no XP and saves only on Save (A-286).
- **BR-33** Quest XP (BR-100 to BR-103), badges (4.3) and the celebrations after each quest (4.7) are in section 4. The quest side panel never scrolls: Muni compacts and the Player Card scales to fit short screens.

### 3.4 Job Profile

The Job Profile is not a second form: the server builds it from the 8 quests (A-262).

| ID | What the learner can do |
| --- | --- |
| FR-11 | See their profile as employers will: Player Card, strength, “Built from your quests” (one row per quest with filled/total and what’s left), sections, contact & visibility |
| FR-12 | Edit any section; it opens that quest step pre-filled (`?edit=1`) and returns here on Save (A-286) |
| FR-13 | Rewrite “About me” (≤ 400 chars, `about-dialog.ts`) |
| FR-14 | Upload a newer CV (≤ 5 MB) and accept or reject each suggested change |
| FR-15 | Preview, download (PDF) and share the CV; choose to hide contact details on the shared link; links expire after 30 days by default |
| FR-16 | See the employer view (`/job-profile/employer`) |

- **BR-40** Completeness % = quest steps filled ÷ all steps (18). A skipped optional step counts as “left” (A-262). The same number is used by the Launchpad, My Jobs and Hire Fest.
- **BR-41** Contact & visibility shows masked mobile and email, location, “employers can find me”, invite channels, and what is never shown (ID number, full address) (A-305).

### 3.5 Lobby (home)

| ID | What the learner can do |
| --- | --- |
| FR-17 | Returning learner: resume the last learning item (“Pick up where you left off”) |
| FR-18 | New learner: “Your first moves” card with 3 steps: Discover your Career Drivers, Start your first learning module, See jobs that fit you (A-233, A-266) |
| FR-19 | Collect the +50 XP bonus once all 3 first moves are done; the card then disappears |
| FR-20 | Open pathways: Career Launchpad (live), Grad Gateway and Startup Street (“Soon”) |
| FR-21 | Browse knooks (topic shelves): up to 6 modules each on the Lobby, “Show more” opens `/knooks/:slug` (A-255) |

- **BR-42** First move “assessment” is done when the assessment is submitted; “learn” when any brief quiz is passed; “jobs” when My Jobs is opened from the card (`src/mocks/home.handlers.ts`).
- **BR-43** The bonus can be claimed once; claiming before all 3 are done returns 409.
- **BR-44** The “Pick a module below to start learning” hint (shown when nothing is in progress) disappears 1 minute after first view and never returns (A-304).

### 3.6 Learn

Content is Module → Topic → Briefs (short reads), each brief followed by a quiz; a topic’s full course unlocks when every brief is passed (`src/mocks/learn.handlers.ts`).

| ID | What the learner can do |
| --- | --- |
| FR-22 | Open a module, then a topic, and read briefs in order (done · current · locked) |
| FR-23 | Take the brief’s quiz; answers autosave as a draft; see right/wrong with explanations |
| FR-24 | Bookmark a brief; see all bookmarks at `/learn/bookmarks` |
| FR-25 | Rate a brief helpful or not |
| FR-26 | Open the full course once all briefs in the topic are passed |

- **BR-45** A brief’s quiz pays +1 XP per correct answer, only the first time it is submitted; retakes pay nothing.
- **BR-46** Submitting a quiz marks that brief passed whatever the score and unlocks the next brief **\[TBD: confirm there is no pass mark\]**.

### 3.7 Career assessment (Career Drivers)

Built as “Muni’s Balance”: a see-saw game over the Career Drivers Survey 2025 (A-210, A-211).

| ID | What the learner can do |
| --- | --- |
| FR-27 | Answer 36 “this or that” pairs on a 4-notch see-saw (All in This · Lean This · Lean That · All in That), by tap or keyboard |
| FR-28 | Undo the last pick; skip a pair for now |
| FR-29 | Pause (Save & exit) and resume later at the same pair |
| FR-30 | Clear 4 islands of 9 pairs each: Vibe Check (1–9), Main Character (10–18), Plot Twist (19–27), Final Form (28–36) |
| FR-31 | Final check: answer skipped pairs, then “Open my jars” |
| FR-32 | See the reveal (archetype + top drivers), then the full report (`/assessments/career/report`): radar, breakdown, compare attempts |
| FR-33 | Retake (no XP), or restart an in-progress attempt; turn a retake reminder on or off |

Scoring (`src/app/core/assessment/scoring.ts`):

- **BR-50** Each pair splits 3 points (drawn as coins in the game; not a currency) between its two drivers: notch 0 → 3·0, 1 → 2·1, 2 → 1·2, 3 → 0·3. 36 pairs = 108 points. Each of the 9 drivers appears in 8 pairs, so scores run 0–24.
- **BR-51** Drivers: A Material Rewards, B Power / Influence, C Meaning / Purpose, D Expertise, E Creativity, F Affiliation, G Autonomy, H Security, I Status. Ties rank A → I.
- **BR-52** Archetype = highest sum of its 2 dominant drivers; tie-break by the average of its secondary drivers, then table order (A-211, tie-break **\[TBD: confirm\]**).
- **BR-53** Score bands for report copy: 17–24 high, 9–16 mid, 0–8 low (A-212).
- **BR-54** Submit is refused (422) unless all 36 pairs are answered.
- **BR-55** Time estimate = 10 s per remaining pair, rounded up to whole minutes.

| Archetype | Dominant | Secondary |
| --- | --- | --- |
| The People-First Service Builder | F, H | C, B |
| The Commercial Rainmaker | A, I | B, G |
| The Specialist Operator | D, H | G |
| The Creative Pathfinder | E, G | I, A |
| The Purpose-Driven Community Leader | C, F | D, B |
| The Strategic Leader | B, I | A, plus the stronger of D or G |

The assessment plays sound cues per pick, coin drop and island cleared; Muni never speaks (A-299, A-314). A “How to play” panel with the 4 notches is open until pair 9, then folds away. “Back” on the results returns to the screen the learner opened the assessment from (Lobby, Launchpad or Profile), not to the assessment intro (A-319). Time estimates show without “\~” (“6 min”).

### 3.8 Career Launchpad

One screen that shows how ready the learner is for a job, and the next step in each area.

| ID | What the learner can do |
| --- | --- |
| FR-34 | See Launch readiness: level 0–4, a % bar and 4 missions (Take assessment, Finish job profile, Apply to a job, Book a Hire Fest slot) |
| FR-35 | Career Prep: Job Profile card (%, quests with gaps, “See what’s left”) and Career Assessment card (start, resume, or archetype + report) |
| FR-36 | Career Opportunities: My Jobs card (matched, applied, saved, top match, new-match count) and Hire Fest card (next fest, slots left, booked slot, alerts) |
| FR-37 | Placement Zone: 14 sectors sorted by assessment fit; “View all” with search and A–Z sort |

- **BR-60** Missions tick from real activity: assessment submitted, Job Profile at 100%, at least 1 application not withdrawn, at least 1 fest booking (A-306).
- **BR-61** Readiness level = missions done (0 On the launchpad, 1 Engines warming up, 2 Lift-off, 3 Almost in orbit, 4 Launched!). Readiness % = XP of done missions ÷ 350 (A-306, `src/mocks/launchpad.handlers.ts`). Each mission pays its XP once, the first time it is done (BR-110 to BR-112, A-321).
- **BR-62** Visiting My Jobs clears the “new matches” count (A-306).
- **BR-63** Sector fit is blank until the assessment is done.

### 3.9 My Jobs

| ID | What the learner can do |
| --- | --- |
| FR-38 | Browse 5 tabs: Available, Matching, Invitations, Bookmarked, Applied (tab switches don’t add history) |
| FR-39 | Search and filter: sector, role (depends on sector), location (city depends on state), work mode, job type, company, salary, date posted, match %, plus tab-specific filters (invite type, job status, date applied). Each option shows the count it would return; zero results suggest which filter to drop |
| FR-40 | Open a job: details, match box (why it matches), closing-soon banner |
| FR-41 | Bookmark a job |
| FR-42 | Apply: pick or upload a CV (≤ 5 MB, `jobs-dialogs.ts`), answer the employer’s questions, send |
| FR-43 | Respond to an invitation: accept, decline, or suggest another time, with an optional note |
| FR-44 | Track an application through applied → viewed → shortlisted → interview → offer (or not selected); withdraw it |
| FR-45 | Turn job alerts on or off, daily or weekly |

- **BR-64** My Jobs is never locked; below 100% profile, Matching shows a nudge linking to what’s left (A-261).
- **BR-65** Matching lists jobs at 60%+ match; “Most relevant” ranks by match, then newest (A-222).
- **BR-66** Withdraw is allowed until an offer, even after an interview is booked (it warns the interview is cancelled). Reapply cool-off 30 days; invitations expire after 7 days. The server enforces both (A-223, **\[TBD: confirm\]**).
- **BR-67** Pay is a display string from the server; the salary filter works on annual pay, stipends count as monthly × 12 (A-221).

### 3.10 Hire Fest

| ID | What the learner can do |
| --- | --- |
| FR-46 | Browse upcoming fests (city or online), save a fest, set fest alerts |
| FR-47 | Book interviews: pick up to 2 employers and a 30-minute slot with each; get a ticket with a QR code and reminders (WhatsApp, SMS, calendar) |
| FR-48 | Join a waitlist when an employer is full (position shown, auto-book when a slot frees) |
| FR-49 | Cancel one interview or the whole booking; reschedule a time |
| FR-50 | On fest day: check in and see the live queue position |
| FR-51 | After the fest: mark each interview Offer / Waiting / No, and rate the fest 1–5 stars |

- **BR-68** Booking needs a Job Profile of 60%+; the locked dialog lists the missing quests (A-230).
- **BR-69** Max 2 employers per fest; a time is blocked if it overlaps or sits within 30 minutes of the other interview; one ticket per fest; rebooking replaces that employer’s time (A-228, `fest.handlers.ts`).
- **BR-70** Marking “Offer” adds the offer to My Jobs → Applied (A-230).
- **BR-71** Paid fests show a price only; payment is out of scope (A-230).

### 3.11 My Matches

- **FR-52** After Vibe Check “Send it”, the learner sees matched jobs and industries (from the assessment), with filters and sort; they can apply, save or accept.
- **BR-72** Matches appear only once the profile is published, or after a confirmed institute claim (`src/mocks/matches.handlers.ts`).

### 3.12 Institute claim and programme code

| ID | What the learner can do |
| --- | --- |
| FR-53 | Open an institute’s SMS link, verify with a code sent to the number the institute holds |
| FR-54 | Give consent section by section, then confirm each pre-filled quest (1–5) and finish |
| FR-55 | Decline (“This isn’t me”) or ask to delete their data |
| FR-56 | Bonus quest: enter a programme code, subject scores and stipend bank details (IN: IFSC with branch lookup, or UPI; ZA: bank + branch code) |

- **BR-73** Claim links can be valid, already claimed, expired or not found; codes follow BR-10/BR-11.
- **BR-74** Programme code: 5 wrong tries → 429 cool-down (`programme.handlers.ts`).
- **BR-75** Only the last 2 digits of the bank account are shown back.

### 3.13 Arena (leaderboard)

| ID | What the learner can do |
| --- | --- |
| FR-57 | See the top 3 on a podium and everyone else in a ranked list (7 rows; “You” pinned at the bottom when further down) |
| FR-58 | Filter by scope (Everyone, My batch, Friends) and period (This week, This month, All time) |
| FR-59 | See “Your spot”: rank, XP to the next player, streak, rank change today, “Earn XP now” |
| FR-60 | Open a player’s card; cheer (and undo); challenge |
| FR-61 | When the podium scrolls away, a standings strip slides in: #1 · #2 · #3 with medals, you highlighted, plus your own rank if outside the top 3 (A-301) |
| FR-62 | First time (no XP yet this week): an empty podium, “Try the crown on”, Muni and “Claim the #1 spot”, all on one screen (A-271, A-307) |

- **BR-76** The week resets every Monday at 12:00 am (screen copy). The top 3 move up a league (“Gold League” in the sample) (`arena.handlers.ts` `promotionRank: 3`) **\[TBD: league ladder and reset timezone\]**.
- **BR-77** A rank-up on the weekly Everyone board shows a banner “Rank up! You’re #N now · You just passed \<name>. Keep going.” for 5 s, with Dismiss (4.7).
- **BR-78** Movement, streaks and leagues are flagged “to confirm” by the design handoff **\[TBD\]**.

### 3.14 My Stats

One route, three views that slide (A-267): **Rewards** (`/stats`), **Learning** (`/stats/learning`), **Assessments** (`/stats/assessments`).

| ID | What the learner can do |
| --- | --- |
| FR-63 | Rewards: player card (level ladder, XP to next level, batch rank), streak week, wallet (XP, credits), 3 daily quests, badge showcase and “up next” badges, quick links. No coins at launch (A-317) |
| FR-64 | Learning: time invested, modules and courses in progress, recommended next |
| FR-65 | Assessments: results per assessment with retake links |
| FR-66 | My Badges (`/stats/badges`): all badges by category, filters (all, earned, in progress, locked), sort (closest to unlock, A–Z), detail panel, pin up to 3 to the showcase |

### 3.15 Profile

| ID | What the learner can do |
| --- | --- |
| FR-67 | See their player ID, level and XP with the level ladder, stats (streak, earned badges, learning hours), profile strength with tasks (a slim “Profile 100% complete” strip with “Your next career move”: Jobs and Launchpad once at 100%), badge showcase, and the career archetype card (emoji, traits) that opens the Career Drivers report (A-318) |
| FR-68 | Edit private details (“Edit” on Private details opens only those fields) and top skills (“Edit my skills” on the Player Card opens only skills, up to 5); add a photo (≤ 5 MB) |
| FR-69 | Reveal their masked mobile or email; change mobile or email with a 6-digit code |
| FR-70 | Link or unlink identity through DigiLocker (IN) |
| FR-71 | Settings menu: Sound effects (on/off switch), Give feedback, Log out (with the log-out dialog) |

- **BR-79** Profile strength weights: contact 30, name 30, photo 10, skills 15, Career Drivers 15 = 100% (`src/mocks/profile.handlers.ts`).
- **BR-80** The career archetype shows the learner’s own result once the assessment is submitted; before that it reads “???” with a link to take it (A-306).
- **BR-81** The photo in the top bar updates as soon as it is uploaded.

### 3.16 Muni chat

| ID | What the learner can do |
| --- | --- |
| FR-72 | Open Muni from the top bar; ask anything about the platform; get answers as short bubbles, rich cards and up to 3 suggestion chips |
| FR-73 | Rate an answer thumbs up or down; see today’s history |
| FR-74 | Ask Muni to reveal their own masked email or mobile |

- **BR-82** Answers are personalised (name, progress, XP); a new learner gets starter suggestions. The mock uses keyword intents (`src/mocks/muni-knowledge.ts`) **\[TBD: real intents and model\]**.
- **BR-83** One proactive nudge per session (e.g. “2 new jobs match you”), shown only on the Lobby as a toast with “Ask Muni” for 6 s; it closes as soon as the learner leaves the Lobby (A-256, A-288, A-315, A-320).
- **BR-84** Muni never speaks aloud, in chat or anywhere else (A-314).

### 3.17 Feedback

- **FR-75** “How’s Skillmuni treating you?”: a non-modal corner card with mood, “what stood out” and an optional note; also always available from Profile.
- **BR-85** Shown only when: the account is 3+ days old and used on 3+ days; right after a success (quiz, quest, assessment, apply, fest booking, first-moves bonus); on a calm screen (Lobby, Stats, Arena, Launchpad, Profile, Learn) 2 s later with nothing open; at most once a month and 3 times a year. Dismiss → wait 30 days; dismissed twice in a row → 90 days (A-274, `src/app/core/feedback/feedback-policy.ts`).

### 3.18 Notifications

- **FR-76** A bell in the top bar with an unread dot and a menu.
- **\[TBD\]** There is no notifications API or list yet; the bell is a placeholder. Events that should notify (invitation received, application status change, fest reminder, rank change, badge earned) are not defined.

### 3.19 Legal

- **FR-77** Terms of Use and Privacy Policy on one screen with two tabs, a jump list and numbered sections; public at `/legal/terms` and `/legal/privacy`; opens full screen from the sign-up consent link, with “Got it” to return (A-289, A-313).
- **BR-86** Copy is the v1.0 (2025) document word for word, generated by `tools/legal/legal-gen.py`. Legal review is open: the source names “Think and Learn Private Limited”, links to a flinnt.com policy page, and has drafting slips.
- **\[TBD\]** The server does not yet record which policy version a learner accepted, or when.

### 3.20 Sound effects (Muni has no voice)

- **FR-78** Sound effects switch on or off in Profile → Settings; the choice is remembered on the device. There is no sound button in the top bar (A-314).
- **BR-87** Nothing plays until the learner has interacted with the page. Muni does not speak anywhere (A-314); the lesson “read aloud” button stays as an accessibility aid.
- **Later phase: a custom Muni voice can return** with a neural text-to-speech provider and the voice owner’s consent.

### 3.21 Empty and first-time screens

- **FR-79** Full-screen empty states show Muni on a lit stage with a “Tap me” hint, catchable sparks (fun only, no XP), new lines on each tap, and “Meanwhile, try one of these” links to 3 other screens (A-283, A-303).
- **FR-80** Placeholder screens (Rewards, Goals, Certificates, Wallet, unknown links) show “\<Area> is on its way” with a Back button in the header, “Go back” when the learner came from another screen, and “Back to the Lobby” (A-172, A-319).

### 3.22 App shell: top bar, account menu, toasts and dialogs

- **FR-81** The top bar shows on every signed-in screen at every size: logo (to the Lobby), Lobby, Arena, My Stats, Rewards, the notification bell, Muni and the avatar. Quests use their own bar: back, quest name, XP HUD and “Save & exit” (A-315).
- **FR-82** The avatar opens the account menu: My profile, Log out. Log out always asks first (4.8).
- **BR-88** Toasts and dialogs follow BR-140 and BR-141 (4.8): toasts top-centre under the top bar; dialog primary button on the right, or on top when stacked.
- **BR-89** While offline, a banner floats above the bottom bar, answers queue, and the page gets room at the end so the banner never covers a button (A-320).
- **BR-90** Scrolled content never shows above or under the floating top bar on phones and tablets (A-320). Buttons never wrap to 2 lines or get cut off; at narrow widths an XP chip on a button hides first (A-275, A-319).

## 4. Gamification rulebook

XP is the only currency in Phase 1 (A-302, A-317). The Gamification service is the single source of truth: every award is one ledger entry (learner, reason, source id, amount) with a unique key on learner + reason + source id, so nothing pays twice and a level, a rank and a total never disagree (A-249, A-316). **This section is the final rulebook: an action not listed in 4.1 earns no XP.** Rows marked “Server rule” are not simulated by the mock backend; the backend implements them as written.

### 4.1 XP table

| ID | Action | XP | When it is credited | Repeats? | What the learner sees | Source |
| --- | --- | --- | --- | --- | --- | --- |
| BR-100 | Finish a quest step that has required fields, on the quest in progress | +20 | On “Next part” (step saved complete) | Once per step. Going back and forward, re-editing, or editing later from Job Profile pays 0 (BR-32) | XP burst into the HUD and toast “+20 XP · \<step> locked in” | levels.ts STEP\_XP, A-249 |
| BR-101 | Finish an all-optional step with something added: Backup contact (their mobile), Education timeline (1 entry), Trophy shelf (1 certificate), Receipts (1 proof) | +20 | On “Next part”, only when the condition is met | Once. “Skip for now”, or Next with nothing added, pays 0 and the step can’t be paid later | Footer hint “Add … to earn +20 XP”; the button shows +20 XP only once the condition is met; unpaid toast “\<step> locked in” | A-311 |
| BR-102 | Finish a quest | +50 | On the quest’s last step | Once per quest; a replay returns xpEarned 0 | Quest Complete modal; Quest 4 opens the Player Card reveal; Quest 8 opens Registration complete (4.7) | QUEST\_XP, A-249 |
| BR-103 | All 8 quests (18 steps) | 760 at most (18 × 20 + 8 × 50) | Sum of BR-100 to BR-102 | — | HUD journey bar “x / 760 XP” during registration; each quest shows its total (+110, or +70 for one-step quests) | quest-catalog.ts TOTAL\_XP |
| BR-104 | Clear assessment islands 1–3 | +25 each | When the island’s 9th pair is answered | First attempt only | Island cleared screen | assessment.handlers.ts XP\_ISLAND |
| BR-105 | Submit Career Drivers (pays island 4) | +25 (100 in all, A-309) | On “Open my jars” | First attempt only; retakes and restarts pay 0 | Reveal “+100 XP · Mission complete” | XP\_DONE |
| BR-106 | Brief quiz | +1 per correct answer | First submit of that brief’s quiz | No; retakes and practice pay 0 | Quiz results and XP burst | learn.handlers.ts |
| BR-107 | Lobby first-moves bonus (all 3 moves done) | +50 | On “Collect +50 XP” | Once per account; collecting early returns 409 | Confetti and reward toast “First-moves bonus collected. Flying start! +50 XP” | home.handlers.ts, A-266 |
| BR-108 | Add a profile photo, first time | +5 | On upload | Once | Toast “+5 XP” | profile.handlers.ts |
| BR-109 | Save top skills on Profile, first time | +10 | On save | Once | Toast “+10 XP” | profile.handlers.ts |
| BR-110 | Launchpad mission “Finish job profile” | +50 | When the Job Profile first reaches 100% | Once | Mission ticks on the Launchpad | launchpad.handlers.ts, A-321 |
| BR-111 | Launchpad mission “Apply to a job” | +100 | On the first application sent | Once | Mission ticks | A-321 |
| BR-112 | Launchpad mission “Book a Hire Fest slot” | +100 | On the first fest booking | Once | Mission ticks | A-321 |
| BR-113 | Programme bonus quest (code, scores, bank) | +100 | On submit | Once per learner | Bonus quest complete modal | programme.handlers.ts |
| BR-114 | Send feedback | +5 | On send | At most once every 30 days | Toast | feedback.handlers.ts, A-274 |
| BR-118 | Daily quests: Finish Brain-Vita +10, Learn for 15 minutes +5, Score 80%+ on any quiz +5 | As shown | Server rule: when the goal is reached that day | Once per quest per day; resets at the learner’s local midnight | My Stats → Rewards, “Today’s quests” | stats.handlers.ts, MISS-03 |
| BR-119 | Institute-claimed learner confirms quests 1–5 | Normal step and quest XP (550 for quests 1–5) | As each claimed quest is confirmed | Once | Same as BR-100 to BR-102 | claim.handlers.ts, A-321 |

The Career Drivers mission on the Launchpad is paid by BR-104 and BR-105 only, never twice. Rules that apply to all XP:

- **BR-115** XP starts at 0 for a new learner and never expires or decreases, even when a mission un-ticks later (an application withdrawn, a booking cancelled).
- **BR-116** Every XP-earning call must be idempotent: an offline save replayed twice credits once (`quest-store.ts` sends absolute values).
- **BR-117** Each award returns `xpEarned` and the new totals, so the client can play the burst and update the HUD in one step.
- **BR-139** These earn no XP: streak days (they unlock streak badges only), Arena cheers and challenges, attending or rating a Hire Fest, the “Take Career Drivers” profile-strength task (the assessment pays itself), Muni’s empty-state sparks, any retake, replay or later edit, and anything not in the table above.

### 4.2 Levels and ranks

One ladder everywhere: quests HUD, Profile, My Stats and Rewards (A-249, `src/app/core/quests/levels.ts`).

| Level | Rank | XP from | Next level at |
| --- | --- | --- | --- |
| 1 | Rookie | 0 | 400 |
| 2 | Pathfinder | 400 | 800 |
| 3 | Trailblazer | 800 | 1,200 |
| 4 | Legend | 1,200 | 1,600 |
| 5 | Icon | 1,600 | 2,000 (last level: its own start + 400) |

A learner who finishes all 8 quests (760 XP) is a level 2 Pathfinder. The rank name always comes from this ladder, from the one XP total (BR-138); no screen keeps its own rank. The Player Card also carries a class title from the skill deck (e.g. “Crowd Whisperer” when People is the strongest stat); class titles are client-side (D-17).

- **BR-125** Level ladder: Profile, My Stats → Rewards, Quest Complete and the Player Card reveal show all 5 levels as a ladder (name and minimum XP, past levels ticked, the current one highlighted) with the caption “Level N · Rank since X XP. Y XP to Level N+1 · Next rank.” At level 5: “You’ve reached the top level!” (level-track.ts).
- **BR-126** Level-up: when an award crosses a threshold, the celebration for that award shows the pill “Level N · Rank unlocked” and the ladder at the new level; the HUD orb and every rank label update at once. A level-up never has a pop-up of its own.
- **BR-127** The quest HUD shows rank · XP and a bar over the 760 quest XP during registration (journey bar, A-264); after registration every bar spans the current level.

### 4.3 Badges

The library holds 56 badges in 7 categories: 8 profile-quest badges plus a 48-badge catalogue (6 categories, one secret badge each, src/mocks/badges.data.ts). Phase 1 awards the 8 quest badges; the 48 catalogue badges are shown with their conditions and progress and are awarded once the badge rules engine ships (MISS-02, D-02). The full catalogue, with IDs, rarity, conditions and progress formulas, is in the .xlsx appendix.

| Category | Badges | Earned how (examples) |
| --- | --- | --- |
| Profile quests | 8 | Finish each registration quest (A-292) |
| Learning | 14 (incl. 1 secret) | First Steps (first brief), Brain-Vita Boss (all Brain-Vita modules), Module Master (10 modules), Bookworm (10 bookmarks) |
| Quizzes | 10 (incl. 1 secret) | Quiz Starter (first pass), Quiz Regular (10), Quiz Legend (50), Perfect Score, Midnight Quizzer |
| Streaks | 6 (incl. 1 secret) | Streak Starter (3 days), Streak Master (tiers 3 · 7 · 30 days), Early Bird, Night Owl, Comeback Kid |
| Assessments | 8 (incl. 1 secret) | Career Clarity (finish Career Drivers), Card Collector (25 skill cards), Expert Eye, Digital Native |
| Social | 6 (incl. 1 secret) | Cheerleader (cheer 10 players), Challenger (win 3), Top 10 (week in batch top 10), Squad Up |
| Special & seasonal | 4 (incl. 1 secret) | XP 1K Club (1,000 XP), Founding Learner, Festive Feat |

Profile-quest badges:

| Quest | Badge | Rarity | State rules |
| --- | --- | --- | --- |
| Who Dis? | Main Character Badge | Common | Earned on quest complete; the current quest shows “X / Y parts”; later quests are locked |
| Hit Me Up | Link Up Badge | Common | Same |
| Big Brain Stuff | Big Brain Badge | Common | Same |
| Your Flex Era | Player Card | Rare | Same |
| Work Lore | Storyteller Badge | Rare | Same |
| Pick Your Arc | Arc Badge | Rare | Same |
| Drop the Receipts | Receipts Badge | Epic | Same |
| Vibe Check | Vibe Check Badge | Legendary | Same |

Badge rules:

- **BR-120** A badge has 4 states: earned (date shown), in progress (label and %), locked, secret (name hidden until earned).
- **BR-121** Rarity is fixed per badge (common, rare, epic, legendary); “Owned by N% of your batch” is shown alongside.
- **BR-122** Tiered badges (Streak Master) show tiers reached of total.
- **BR-123** Only earned badges can be pinned; the showcase holds up to 3, shared by My Stats and Profile.
- **BR-124** Earning a badge plays the badge sound, shows the toast “\<badge> unlocked +XP” (or the quest’s celebration, 4.7) and shows it in My Badges at once.
- **BR-128** Quest badges show the quest’s own artwork (assets/quests/quest-N.png), in full colour when earned and greyscale when locked; catalogue badges show an icon.
- **BR-129** The badge count shown anywhere (Profile stats, My Stats, My Badges “X / 56”) counts earned badges only.
- **BR-130** Until the learner pins badges, the showcase shows their 3 most recently earned quest badges; once they pin, only their pins show.
- **BR-131** My Badges “Closest” names the unearned badge with the highest progress, with its progress label when it has one.
- Server rule: the badge rules engine (MISS-02) listens to learning, quiz, streak, Arena and assessment events and awards the 48 catalogue badges by the conditions in the appendix; D-02 decides which launch first. Until then they stay “in progress” or “locked”.

### 4.4 Missions, daily quests and streaks

- Launchpad missions (BR-60, BR-61): Take assessment, Finish job profile, Apply to a job, Book a Hire Fest slot. Each ticks from real activity and pays its XP once (BR-104/105, BR-110 to BR-112); readiness % = XP of done missions ÷ 350 (100 + 50 + 100 + 100).
- First moves (BR-42, BR-43): 3 one-time steps for new learners (Discover your Career Drivers, Start your first learning module, See jobs that fit you); all 3 done unlocks “Collect +50 XP” (BR-107); the card then leaves the Lobby.
- Daily quests (BR-118): 3 quests that reset at the learner’s local midnight. Clearing all 3 shows a mystery chest that reads “Mystery chest: rewards are coming soon”; it gives nothing in Phase 1.
- Streak: a day counts when the learner finishes at least one brief’s quiz that day (learner’s local time); missing a day resets it to 0. Arena, My Stats and Muni all say this (“Finish a brief’s quiz today to make it N”). The week view shows done, today and upcoming days. Streaks earn no XP; they unlock the streak badges (BR-139, MISS-02, MISS-04).

### 4.5 Readiness and completeness formulas

| Measure | Formula | Source |
| --- | --- | --- |
| Job Profile completeness | quest steps filled ÷ 18 (a skipped optional step counts as left) | A-262 |
| Launch readiness % | XP of done missions ÷ 350 | A-306 |
| Launch readiness level | number of missions done (0–4) | A-306 |
| Profile strength | contact 30 + name 30 + photo 10 + skills 15 + Career Drivers 15; at 100% the card becomes the slim “Profile 100% complete” strip | profile.handlers.ts, A-318 |
| Level and rank | the ladder in 4.2 applied to the one XP total | levels.ts, A-316 |
| XP to next level | next level’s minimum − total XP | levels.ts |
| Badge count | earned badges only | A-318 |
| Arena weekly XP | XP credited since Monday 12:00 am | A-271 |
| Hire Fest booking gate | Job Profile ≥ 60% | A-230 |
| Matching list | match ≥ 60% | A-222 |
| Matching weight of proof | verified 1.0, declared 0.8 | q7-receipts.ts |

### 4.6 Currencies

| Currency | Purpose | Phase 1 status |
| --- | --- | --- |
| XP | Score: levels, ranks, Arena; never spent | Earned (4.1) |
| Coins | Spend in a rewards store | Removed for launch (A-317): no balance, store, vouchers, wallet link or nav count is shown. The assessment’s “3 coins per pair” are game points, not a currency (BR-50) |
| Credits | Recognise finished courses and certificates on the CV | Shown as 0 in the My Stats wallet; no earning rule in Phase 1 (D-10) |

### 4.7 Celebrations and reward moments

Every moment where the learner is rewarded or told about progress. The server returns xpEarned and the new totals with the award (BR-117); the client shows the moment. Sounds play only when sound effects are on (FR-78).

| Moment | Trigger | What the learner sees | Buttons and closing | Source |
| --- | --- | --- | --- | --- |
| Step done | “Next part” on a quest step | XP burst from the button into the HUD; toast “+20 XP · \<step> locked in”, or “\<step> locked in” when the step pays nothing (BR-101) | Toast closes after 3 s | quest-page.ts |
| Quest complete (Quests 1–3, 5–7) | Last step of the quest | Modal “You ate that. Badge unlocked!”: badge art, +50 XP pill, “Rank · Lvl N”, the level ladder, the level-up pill when a level was crossed (BR-126), “Up next” with the next quest’s name, time and total XP | “Double-check my answers” (secondary) · “Let’s gooo” (primary, opens the next quest) | quest-complete.ts |
| Player Card reveal (Quest 4) | Last step of Your Flex Era | Full modal: “Meet \<class title>” (or “Your Player Card is live” without one), the card with radar and signature moves; “Badge earned” block with the Player Card badge, XP count-up, level-up chip and ladder; Muni beside a speech bubble | “Share my card” (saves an image) · “Edit my deck” · “Back to my quests”; on close, toast “\<badge> unlocked +XP” | player-card-reveal.ts |
| Registration complete (Quest 8) | Vibe Check “Send it” | Modal “Yippee! You’re officially registered!” with the badge, XP and a bonus card “Got a programme or project code? Bonus quest · +100 XP” | “I have a programme code” (to /programme) · “Take me home” (primary, to the Lobby) | quest-page.ts |
| Programme bonus done | Programme submit | Quest Complete modal with the pill “Bonus quest · Programme unlocked” and +100 XP | “Let’s gooo” to the Lobby | programme-page.ts |
| Island cleared | 9th pair of islands 1–3 | In-page panel “Island N cleared · \<name>: conquered! 🎉”, pairs done, XP, minutes left; island sound | “Sail to Island N+1” · “Take a break” (Save & exit dialog) | assessment-page.html |
| Assessment reveal | “Open my jars” | Archetype, top drivers and “+100 XP · Mission complete” (first attempt) | “See my full results” | assessment-page.html |
| Quiz results | Quiz submit | Score, right and wrong answers with explanations; XP burst on the first submit | Next brief, or practice the missed ones | quiz-page.ts |
| First-moves bonus | All 3 first moves done | “Collect +50 XP” on the Lobby card, then confetti and the reward toast | Card leaves the Lobby | home-page.ts |
| Profile XP | First photo, first top skills | Toast “+5 XP” or “+10 XP”; strength card updates | — | profile-page.ts |
| Arena rank-up | Learner’s rank improves on the weekly Everyone board | Banner “Rank up! You’re #N now · You just passed \<name>. Keep going.” | Closes after 5 s or with Dismiss | arena-page.ts |
| Badge unlocked | Any badge earned | Toast “\<badge> unlocked +XP” and the badge sound (quest badges use the quest celebration instead) | — | BR-124 |

### 4.8 Pop-ups, toasts and messages

**Toast rules (BR-140).** Toasts sit top-centre, just under the top bar, at every screen size. Success and info close after 3 s, errors after 5 s; the timer pauses while a toast is hovered or focused. At most 3 show at once and the same message never stacks. A toast with an action (“View”, “Undo”, “Ask Muni”) also has a close button (A-315).

**Dialog rules (BR-141).** On desktop, dialogs open centred; on phones, as bottom sheets. Buttons: secondary on the left, primary on the right; when stacked (and in sheets) the primary is on top (A-319). Escape and the close button dismiss every dialog except session expiry. Privacy Policy & Terms open full screen (A-313).

| Area | Pop-up | Opens when | Buttons |
| --- | --- | --- | --- |
| Shell | Log out (“Leaving so soon, \<name>?”) | Log out from the avatar menu or Profile → Settings | Shows level · rank, XP earned and XP to level up (or “You’re at the top of the ladder”), “Your progress is saved either way.” · “Stay and keep going” (primary) · “Log out anyway”, then toast “Logged out. See you soon!” (A-315) |
| Shell | Session expired | 30 min idle | Critical: Escape doesn’t close · “Log in again” (keeps the return URL) |
| Shell | Offline banner | Connection lost | Bottom banner “You’re offline…”; answers queue; toast “Back online. Your answers are saved.” The page gets room so the banner never covers a button (A-320) |
| Shell | Muni nudge | First Lobby visit in a session when Muni has a nudge | Toast “\<title> · \<body>” with “Ask Muni”; 6 s; closes when the learner leaves the Lobby; once per session (A-315, A-320) |
| Shell | Feedback card | BR-85 | Corner card: mood, what stood out, note · Send (+5 XP, BR-114) · close |
| Shell | Permission primer | Before asking for camera or notifications | Muni explains why · allow · “Upload a file instead” (camera) |
| Auth | Code entry (login, quests, profile) | Send code | “Verify & log in”, resend after the countdown, Edit; messages for wrong code, expired code and “Let’s take a breather” (hidden limit, BR-11) |
| Auth | Privacy Policy & Terms | Consent link on sign-up | Full screen · “Got it” |
| Auth | Google test account picker | Continue with Google before a client id exists | Pick an account (test mode only) |
| Quests | Birthday picker | Birthday field | Year → month → day · “That’s my birthday” |
| Quests | DigiLocker consent | “Link with DigiLocker” | Lists what is shared · Cancel · Approve |
| Quests | Quest map sheet (phones) | “See all” on the quest lineup | List of the 8 quests and their state |
| Quests | Skill level sheet | Tap a skill in the deck | Level 1–3 · make it a signature move (max 3) · remove |
| Quests | Trophy | Add or edit a certificate | Name, issuer, dates, link or file, skills · Cancel · Save; “Discard this trophy?” when leaving with changes |
| Quests | Leave chapter | Leaving a half-filled work chapter | “Save draft” · “Ditch it” |
| Onboarding | CV outcome sheet | CV parse ends in one of the BR-25 outcomes | Retry, type it myself, or continue |
| Assessment | Save & exit | “Take a break” or exit | “Remind me tomorrow” (one nudge at 6 PM) · “Keep going” · “Save & exit” |
| Assessment | Why pairs? | “Why pairs?” link | Explains the 3 points per pair · “Got it” |
| Assessment | Restart or retake | Restart an attempt, or retake | Explains what is kept and that retakes earn no XP · confirm · cancel |
| Job Profile | About me, CV changes, Share, Finish | Rewrite About me; upload a newer CV; share; a programme needs missing quests | Save · pick changes to add · copy link, WhatsApp, LinkedIn, email, hide contact · “Start now” / “Later” |
| My Jobs | All filters, Apply, Decline, Suggest a time, Withdraw, Application, Interview tips, Job alerts | The matching action | Each with Cancel and one primary action; results toasts in BR-140 style |
| Hire Fest | Locked, Waitlist, Cancel, Fest alerts | Booking under 60% profile; employer full; cancel; alerts | Lists missing quests · join waitlist (auto-book, WhatsApp) · cancel one or all (reschedule instead) · save alerts |
| Launchpad | All sectors | “View all” sectors | Search, sort, Ask Muni |
| Matches | Accept interview | Accept on a match | “Accept interview” · “Not now” |
| Profile | Edit private details / Edit your top skills | “Edit” on Private details / “Edit my skills” on the Player Card | Only that section’s fields · Cancel · Save (A-318) |
| Stats | Badge detail sheet (phones) | Tap a badge | Details · pin or unpin |

Every other confirmation is a toast in the area’s own words (for example “Application sent.” with “View in Applied Jobs”, “Interview confirmed”, “\<skill> removed from your deck” with Undo). Every failure toast says what failed, that the learner’s input is kept, and to try again.

## 5. Data model

Everything hangs off one **Learner**, scoped to a **Tenant** (the white-label client) and a **Market** (IN or ZA). Field names follow the client interfaces in `src/app/core/**/*.api.ts`. Types are given loosely (text, number, date, list, enum) for business readers; the interfaces are the exact contract.

| Entity | Key fields | Relationships | Owner |
| --- | --- | --- | --- |
| Tenant | id, name, logo, theme colours, market | has many Learners | Tenant (white-label client) |
| Market config | code (IN/ZA), locale, dial code, mobile and postal patterns, ID types, ID verification (digilocker / number), languages, consent law | used by Tenant | Platform |
| Learner (account) | id, first name, last name, email, phone, phone verified, photo URL, persona (new/returning), created at | belongs to Tenant | Learner |
| Session | token, idle timeout 30 min | belongs to Learner | Platform |
| Consent record | policy version, accepted at, scope (CV reading, publishing, invites) **\[TBD: not stored today\]** | belongs to Learner | Learner |
| Identity link | doc type (Aadhaar / PAN / DL), masked number, last 4, verified name, DOB, linked via | belongs to Learner (IN) | Learner |
| Quest progress | quests completed (0–8), parts done on current quest, XP, level, rank | one per Learner | Platform |
| Quest answers | quest slug, part, values (key-value), done flag, saved at | many per Learner | Learner |
| Education entry | level, degree, branch, institute, board, year, score kind, score | up to 6 per Learner | Learner |
| Skill (deck) | ESCO id or custom, name, category, ESCO group, level 1–3, source (cv / user), signature order | 3–50 per Learner | Learner |
| Language | name, fluency (basic–native), speak, read, write, mother tongue, from CV | many per Learner | Learner |
| Trophy (certificate) | title, issuer, year, link or file, verified status, expiry | up to 20 per Learner | Learner |
| Work chapter | kind, role, company, start, end, description, skills (≤ 5); gap reasons | up to 10 per Learner | Learner |
| Receipt (proof) | claim it proves, file, status (checking / verified) | many per Learner | Learner |
| Arc (preferences) | looking for, roles (≤ 8), where, shifts, notice, expected pay | one per Learner | Learner |
| Visibility | discoverable, invites by email/WhatsApp, published at | one per Learner | Learner |
| CV file | id, file, parse status, parsed summary | many per Learner | Learner |
| Job Profile share | link, hide contact, expires in days (30) | one per Learner | Learner |
| XP ledger entry | learner, amount, reason (BR-100 to BR-119), source id, created at; unique on learner + reason + source id | many per Learner | Platform |
| Badge definition | id, name, icon, category, rarity, condition, tiers, CTA | catalogue (56) | Platform / Tenant |
| Badge award | badge id, learner, earned on, progress %, tier reached, pinned order | many per Learner | Platform |
| Assessment attempt | answers (36 notches or skip), current pair, islands paid, earns XP, scores per driver (A–I), archetype, date | many per Learner | Learner |
| Learning content | Module → Topic → Brief → Quiz → Question (with explanation) | catalogue | Tenant / Platform |
| Learning progress | passed briefs (XP each), quiz results, drafts, bookmarks, helpful votes | per Learner per topic | Learner |
| Job | id, title, employer, sector, role, location, work mode, type, pay text, posted, closes, match % | catalogue | Employer |
| Application | job, CV, answers, status (applied → offer / not selected / withdrawn), timeline | many per Learner | Learner + Employer |
| Invitation | job, type, status (new / accepted / declined / expired), note, expires in 7 days | many per Learner | Employer |
| Saved job, job alerts | job id; alerts on/off, daily/weekly, saved searches | per Learner | Learner |
| Fest | id, name, date, city/online, recruiters, free or price, employers with 30-min slots | catalogue | Tenant / Employer |
| Fest booking | fest, ticket code, picks (≤ 2 employer + time), reminders, CV | one per Learner per fest | Learner |
| Waitlist, outcome, rating | position; Offer / Waiting / No per interview; 1–5 stars | per booking | Learner |
| Arena standing | scope, period, rank, XP, streak, movement, cheers | per Learner per period | Platform |
| Cheer, challenge | from, to, on/off | many | Learner |
| Daily quest | id, title, progress, XP, done, resets at | 3 per Learner per day | Platform |
| Streak | days, week states | one per Learner | Platform |
| Muni message | from (me / muni), blocks, chips, time, rating | many per Learner | Learner |
| Feedback | mood, what stood out, note, moment, sent at | many per Learner | Learner |
| Institute claim | token, institute, phone on file, sections, status (valid / claimed / expired) | one per Learner | Institute |
| Programme enrolment | code, subject scores, bank (IFSC or UPI; ZA branch code), account tail | one per Learner | Learner + Institute |
| Draft | key, value (autosave) | many per Learner | Learner |
| Notification | **\[TBD: not modelled\]** | — | Platform |

## 6. API contract

The mock backend implements 133 endpoints (API-001 to API-133). The full list, with the client method, response type and errors for each, is on the **API** sheet of the .xlsx appendix; this section gives the conventions and the map.

**Conventions every endpoint follows** (`src/app/core/api/api-client.ts`, `api-error.ts`):

- **BR-130** Base path `/api`, JSON in and out; file uploads are multipart with upload progress (CV, trophy files, photo).
- **BR-131** The session is a cookie sent with every call (`withCredentials`); `GET /api/session` returns the signed-in learner.
- **BR-132** Errors return `{ message, errors: { <field or code>: […] } }`. The client maps status to one state: 0 offline or server, 401 unauthorized, 403 forbidden, 404 not found, 409 conflict, 400/422 validation (field messages shown inline), 429 rate limited, 5xx server.
- **BR-133** Writes that can be replayed offline (quest parts, drafts, assessment answers) must be idempotent and accept absolute values, not increments.
- **BR-134** Any call that awards XP returns `xpEarned` plus the new totals (BR-117).
- **BR-135** Page-URL scenario params (`?launch=`, `?ca=`, `?jobs=` and so on) are mock-only test switches; the real backend ignores them.
- **BR-137** A quest part pays +20 XP for its required fields. A part with only optional fields pays +20 once something is added (backup contact mobile, one education entry, one certificate, one proof); until then the footer says how to earn it (A-311).
- **BR-138** One XP ledger total drives level and rank everywhere; `GET /api/me/progress` returns it (A-316). Sign-up “You’re in” waits for the learner to press Continue (A-313). Log out asks first (A-315).

| Area | IDs | Count | Main endpoints |
| --- | --- | --- | --- |
| Auth | API-011–022 | 12 | register, email verify/resend, SMS OTP + verify, email login code + verify, Google, password login and reset, logout, session |
| Quests | API-125–128 | 4 | progress, answers per quest, save a part, complete a quest |
| Drafts | API-034–035 | 2 | save and restore autosaved drafts |
| CV | API-036–039 | 4 | upload, parse status, summary, delete |
| Contact & geo | API-030–033 | 4 | phone code + verify, postal-code lookup, regions |
| Identity | API-064–067 | 4 | DigiLocker start, link, read, unlink |
| Quest 4 | API-057–060 | 4 | ESCO skill search, trophy link check, file upload, DigiLocker certificates |
| Matches | API-104–109 | 6 | preview, publish profile, list, apply, save, accept |
| Institute claim | API-023–029 | 7 | invite, OTP, verify, consent, confirm section, finish, decline |
| Programme | API-122–124 | 3 | look up code, IFSC lookup, submit |
| Lobby | API-061–063 | 3 | feed, mark a first step, claim the bonus |
| Learn | API-097–103 | 7 | module, topic, bookmark, brief feedback, quiz, quiz draft, submit |
| Career assessment | API-004–010 | 7 | state, save answers, submit, result, restart, retake, reminder |
| Launchpad | API-090–096 | 7 | readiness, profile, assessment, jobs, fest cards, sectors, fest alerts |
| Job Profile | API-068–074 | 7 | profile, CV contact, about me, CV upload + apply changes, share settings |
| My Jobs | API-075–089 | 15 | summary, lists per tab with facets, detail, save, apply, CV upload, invitation reply, withdraw, alerts |
| Hire Fest | API-041–056 | 16 | summary, list, detail, slots, book, bookings, cancel, waitlist, past, outcome, rating, alerts, save |
| Arena | API-001–003 | 3 | board (scope, period), cheer, challenge |
| My Stats & badges | API-129–133 | 5 | rewards, learning, assessments, badge catalogue, showcase |
| Profile | API-115–121 | 7 | profile, reveal, edit data, contact code + verify, save, photo |
| Muni chat | API-110–114 | 5 | welcome, send message, reveal, rate, history |
| Feedback | API-040 | 1 | send feedback |

**Endpoints the real backend still needs** (also on the **Missing APIs** sheet):

| ID | Need | Why |
| --- | --- | --- |
| MISS-01 | XP ledger + server-side awards for every action in 4.1 (BR-100 to BR-119) | One source of truth for XP |
| MISS-02 | Badge rules engine listening to learning, quiz, streak, Arena and assessment events | 48 catalogue badges have text only |
| MISS-03 | Daily quests: list, progress and auto-credit (BR-118); the mystery chest gives nothing in Phase 1 | The server tracks the daily goals |
| MISS-04 | Streak tracking (a day = at least one brief quiz finished) and learning minutes for the “Learn for 15 minutes” daily quest | Streak badges and the daily quest need them |
| MISS-05 | Launchpad mission credit, once each (BR-110 to BR-112) | Credited by the mock today; the server must do the same from events |
| MISS-06 | Notifications: list, mark read, channel preferences | Bell is a placeholder |
| MISS-07 | Consent records (policy version, scope, time, withdraw) | DPDP / POPIA proof |
| MISS-08 | Data export and account deletion | DPDP / POPIA rights |
| MISS-09 | Preferences (sound effects, language) | Only in the browser today |
| MISS-10 | Rewards store, coins and wallet history: later phase (A-317), not needed for launch | Placeholder screens |
| MISS-11 | Arena leagues, weekly reset, challenge results | Rules “to confirm” |
| MISS-12 | Tenant theme and market config from the server | Client config today |
| MISS-13 | Token refresh, device sessions, rate limits | Mock uses a cookie flag |
| MISS-14 | Muni intents or model endpoint, safety, history retention | Mock keyword intents |
| MISS-15 | Muni voice (TTS): later phase, not needed for launch (A-314) | Closed for Phase 1 (A-314) |

## 7. Non-functional requirements

| Area | Requirement | How it is met today |
| --- | --- | --- |
| Accessibility | WCAG 2.2 AA on desktop (1536), tablet (834) and phone (390) | Automated axe checks in every end-to-end test and on 68 Storybook stories; keyboard paths for every action (including the assessment see-saw); focus never hidden under sticky bars (A-256); reduced-motion respected; sound effects can be switched off (WCAG 1.4.2) |
| Screen sizes | Usable from 360 px phones to wide desktops, including short laptops (650 px tall) | Layout audit across 7 sizes; narrow tablets switch the top nav to icons (A-298); buttons never wrap to 2 lines (A-275) |
| Language | Every visible string translatable; no text baked into images | Angular `$localize` with a unique id per string; RTL-safe layout |
| White label | Tenant logo, colours and name without code changes | Design tokens + `TenantTheme` (logo URL, colours); market config per country **\[TBD: served by the backend, MISS-12\]** |
| Performance | Fast first load on mid-range phones | Lazy-loaded routes; optimised images; synthesised sounds (no audio files) **\[TBD: numeric budgets, e.g. LCP < 2.5 s on 4G\]** |
| Offline | No answer lost on a bad connection | Offline banner; quest parts, drafts and assessment answers queue and replay with a toast when back online |
| Security | Least data, least exposure | IN never stores a full ID number, only masked + last 4 (A-272); email and mobile masked until the learner taps reveal; 6-digit codes with unlimited visible tries and a hidden throttle (A-312); session idle timeout 30 min; cookies sent with credentials |
| Privacy and consent | DPDP Act 2023 (IN), POPIA (ZA) | Consent before CV reading; consent law named on sign-up and Vibe Check; “Delete my data” on claims; ID documents never shown to employers **\[TBD: consent records, export, erasure, MISS-07/08\]** |
| Data retention | Defined per data type | **\[TBD\]** The “keep my profile 12 months” switch was removed (A-287); a retention policy is needed from legal |
| Browser support | Current Chrome, Edge, Safari, Firefox; Android and iOS browsers | Built and tested on Chromium **\[TBD: support matrix\]** |
| Audio | Never surprise the learner | Nothing plays before the first interaction; all sound is quiet and switchable (A-279) |
| Technical stack | Decided 13 Oct (A-310): ASP.NET Core 10 (LTS) microservices (Clean Architecture) on Azure Container Apps behind a YARP gateway at /api/v1; MySQL Flexible Server with one schema per service; Service Bus for events; Blob Storage, Redis, Key Vault + Managed Identity | Frontend: Angular 22 on its mock backend today. The backend is built in its own repo against the API contract (section 6) so either side can change without breaking the other |

&#91;embedded content: backend architecture · 9 services, 1 gateway\]

Each service owns its own schema and reads other services' data only through APIs or events. Progress events flow to Gamification, the only service that credits XP. Repository rules: `phase-1/BACKEND_RULES.md`.

## 8. Integrations

| Integration | Used for | Status | What the backend must do |
| --- | --- | --- | --- |
| DigiLocker (IN) | Identity in Quest 1 (Aadhaar, PAN, driving licence) and certificates in Quest 4 | Built against a mock; needs a partner client id (`digilockerClientId`) | Run the OAuth consent, pull the document, keep only doc type, masked number, last 4, verified name and DOB (A-272) |
| Google sign-in | Continue with Google | Built; needs a Google client id | Verify the credential, create or match the account |
| SMS OTP | Login codes, phone verification, claim codes | Mock (code 123456) | Choose an SMS provider (and DLT templates for India); enforce BR-10 to BR-12 |
| Email | Sign-up and login codes, password reset links | Mock | Choose an email provider; never reveal whether an email exists on reset (BR-18) |
| WhatsApp | Course updates (Quest 2 opt-in), employer invites, fest reminders | UI opt-ins only | **\[TBD\]** provider and templates |
| CV parsing | Pre-fill Quests 1–5 from a CV file or photos | Mock with 7 outcome states | Parse PDF, Word and images (OCR); return the outcome states in BR-25; map skills to ESCO ids |
| ESCO taxonomy | Skill deck search and categories; languages | Generated from the ESCO sheet v1.2.1 (985 entries) into the client (A-285) | Serve search from the server so the list can grow; Indian languages missing from ESCO (Malayalam, Punjabi, Odia and others) need adding |
| Postal lookup | Auto-fill state and city from a PIN or postal code | Mock with a few known codes | Use an India Post / ZA postal data source |
| IFSC lookup | Bank and branch for the stipend (IN) | Mock | Use an RBI IFSC data source |
| Text-to-speech | Muni’s voice | Not used in Phase 1: Muni has no voice (A-314) | Later phase: a neural TTS provider and the voice owner’s consent (A-314) |

## 9. Open decisions, conflicts and risks

XP is settled: section 4.1 is the final table and every conflict found between the screens and the rules is resolved (9.1). What remains are business decisions that don’t block the build (9.2), each with the default the build uses. Owners are placeholders to fill in.

### 9.1 Conflicts (the code and the screens disagree)

| ID | Conflict | Where | Owner |
| --- | --- | --- | --- |
| C-01 | Settled 13 Oct (A-309): the assessment pays 100 XP in all, 25 per island; Lobby, Launchpad, results and Muni now all say 100 | `home.handlers.ts` vs `launchpad.handlers.ts` | \[Owner\] |
| C-02 | Resolved 14 Oct: no “+10 XP per level”; the timeline step pays +20 once with at least one entry (BR-101, A-311) | `education.ts` `TIMELINE_XP` vs A-249 | \[Owner\] |
| C-03 | Resolved 14 Oct: the +20 shown on Job Profile is the unfinished quest step’s own XP (BR-100), paid once; a later edit pays 0 (BR-32) | A-209 | \[Owner\] |
| C-04 | Resolved 14 Oct: each mission pays once (BR-110 to BR-112, A-321); the assessment mission is the assessment’s own 100 | `launchpad.handlers.ts` | \[Owner\] |
| C-05 | Resolved 14 Oct: one ledger total everywhere (BR-138, A-316); photo and skills XP go into it (BR-108, BR-109) | `profile.handlers.ts` | \[Owner\] |
| C-06 | Resolved: the strength task carries no XP; the assessment pays its own 100 (BR-139) | `profile.handlers.ts` | \[Owner\] |
| C-07 | Resolved 14 Oct: coins, the Arena rewards store and vouchers are removed for launch (A-317) | `arena.handlers.ts` | \[Owner\] |
| C-08 | Resolved 14 Oct: streaks earn no XP; a day = one brief quiz finished; Arena, My Stats and Muni now say the same (4.4) | `muni-knowledge.ts` | \[Owner\] |
| C-09 | A-222 locked My Jobs under 60% profile; A-261 removed the lock. The code follows A-261 (resolved, listed for awareness) | A-222, A-261 | — |

Also resolved on 14 Oct while checking every screen against this rulebook: the My Stats streak line said “Learn 5 min today” (now the quiz rule, 4.4); the Launchpad card said “Career Fest” (now “Hire Fest” everywhere); Quest Complete’s “Up next” showed only the next quest’s +50 bonus (now its full XP, +110 or +70, matching the quest lineup); Muni’s “How do I earn XP?” answer now lists every row of 4.1.

### 9.2 Decisions needed

| ID | Decision | Default in the build | Owner |
| --- | --- | --- | --- |
| D-01 | Decided 14 Oct: the XP table in 4.1 is final | Section 4.1 | \[Owner\] |
| D-02 | Badge rules engine scope for Phase 1: which of the 48 catalogue badges launch | Only quest badges awarded | \[Owner\] |
| D-03 | Archetype tie-break | Average of secondary drivers, then table order (A-211) | \[Owner\] |
| D-04 | Matching threshold for the Matching tab | 60% (A-222) | \[Owner\] |
| D-05 | Hire Fest: max employers per learner per fest | 2 (A-228) | \[Owner\] |
| D-06 | Reapply cool-off and invitation expiry | 30 days, 7 days (A-223) | \[Owner\] |
| D-07 | Arena leagues, weekly reset time and timezone | Top 3 promoted; Monday 12:00 am | \[Owner\] |
| D-08 | Daily quest list, reset timezone and mystery-chest contents | 3 quests, local midnight | \[Owner\] |
| D-09 | Decided 14 Oct: a streak day = at least one brief quiz finished, learner’s local time, no grace day | As decided (4.4) | \[Owner\] |
| D-10 | Credits: how they are earned | Shown as 0 | \[Owner\] |
| D-11 | Notification events and channels | None | \[Owner\] |
| D-12 | Data retention periods per data type | None (A-287 removed the 12-month switch) | \[Owner · Legal\] |
| D-13 | Legal review of Terms and Privacy v1.0 (wrong company name, flinnt.com link, drafting slips) | Shown word for word (A-289) | \[Owner · Legal\] |
| D-14 | Closed 14 Oct: no Muni voice in Phase 1 (A-314) | No voice | \[Owner\] |
| D-15 | Integration credentials: DigiLocker partner id, Google client id, SMS (with India DLT), email, WhatsApp providers | Mocks | \[Owner\] |
| D-16 | ESCO gaps: Indian languages missing from the ESCO sheet | Not offered | \[Owner\] |
| D-17 | Player Card class titles: owned by the server or the client | Client | \[Owner\] |

### 9.3 Risks

| ID | Risk | Impact | Mitigation |
| --- | --- | --- | --- |
| R-01 | XP shown but not credited erodes trust (closed 14 Oct) | Learners feel cheated; support load | Closed: 4.1 is final and only listed actions show XP; one ledger (MISS-01) |
| R-02 | DigiLocker partner approval takes weeks | Quest 1 identity blocked in India | Apply now; allow “link later” with the profile marked unverified **\[TBD\]** |
| R-03 | SMS DLT template registration in India | OTP delivery fails at launch | Register sender id and templates early |
| R-04 | No consent records or erasure flow | DPDP / POPIA non-compliance | MISS-07, MISS-08 before launch |
| R-05 | Badge engine and streaks are new backend work | Gamification feels thin at launch | Phase the 48 badges (D-02) |
| R-06 | Closed: no voice in Phase 1 | Muni doesn’t sound the same everywhere | D-14 |

## 10. Appendix

### 10.1 Glossary

| Term | Meaning |
| --- | --- |
| Muni | Skillmuni’s mascot guide; talks in short written lines on every screen and answers in Muni chat (no voice) |
| Quest | One of 8 registration sections; each has 1–3 steps (“parts”) |
| Player Card | The learner’s skill card built in Quest 4: class title, radar across 6 stats, top 3 signature skills |
| Skill deck | The learner’s list of 3–50 skills, each with a level 1–3 |
| ESCO | European skills taxonomy used for skill names, ids and categories |
| Knook | A themed shelf of learning modules on the Lobby |
| Brief | A short read inside a topic, followed by a quiz |
| Career Drivers | The 36-pair assessment that finds a learner’s top drivers and archetype |
| Island | A group of 9 assessment pairs (4 islands) |
| Archetype | One of 6 career personalities matched from driver scores |
| Launch readiness | Launchpad level (0–4) from 4 career missions |
| Hire Fest | A hiring event with booked 30-minute interviews |
| Arena | Weekly, monthly and all-time leaderboard by XP |
| XP | Experience points; the score that sets level and Arena rank |
| Showcase | Up to 3 earned badges pinned on Profile and My Stats |
| Tenant | The organisation whose branded copy of Skillmuni the learner uses |
| Persona | New learner (just signed up) vs returning learner (logged in) |
| \[TBD\] | Still needs a business decision (9.2); the build uses the stated default meanwhile |

### 10.2 Traceability

| Screen | Route | Requirements |
| --- | --- | --- |
| Login, sign-up, reset | `/login`, `/signup`, `/login/forgot` | FR-01–06 |
| Muni intro, CV onboarding | `/start`, `/onboarding/cv/*` | FR-07–10 |
| Quests | `/quests/:slug` | Section 3.3, BR-30–33 |
| Job Profile | `/job-profile/*` | FR-11–16 |
| Lobby, knooks | `/home`, `/knooks/:slug` | FR-17–21 |
| Learn | `/learn/*` | FR-22–26 |
| Career assessment | `/assessments/career/*` | FR-27–33 |
| Career Launchpad | `/launchpad` | FR-34–37 |
| My Jobs | `/jobs/*` | FR-38–45 |
| Hire Fest | `/hire-fest/*` | FR-46–51 |
| My Matches | `/matches` | FR-52 |
| Institute claim, programme | `/claim/:token`, `/programme` | FR-53–56 |
| Arena | `/arena` | FR-57–62 |
| My Stats, My Badges | `/stats/*` | FR-63–66 |
| Profile | `/profile` | FR-67–71 |
| Muni chat, feedback, notifications | top bar, corner card | FR-72–76 |
| Legal | `/legal/*` | FR-77 |
| Audio, empty and placeholder screens | top bar, account menu, toasts, dialogs; `/rewards`, `/goals`, `/certificates`, `/wallet` | FR-78–82, sections 4.7–4.8 |

### 10.3 Acceptance criteria

Each FR (FR-01 to FR-82) is testable as written in section 3. The criteria below cover the gamification and shell rules, which carry the most risk:

| FR | Acceptance criterion |
| --- | --- |
| FR-19 | With all 3 first moves done, “Collect” credits +50 XP once; collecting earlier returns 409 |
| FR-23 | The first submit of a brief quiz credits +1 XP per correct answer; later submits credit 0 |
| FR-30 | Finishing islands 1–3 for the first time credits +25 XP each; a retake credits nothing |
| FR-32 | The first submit credits +100 XP and names the archetype per BR-52 |
| FR-34 | Missions tick from real activity; level = missions done; % = mission XP done ÷ 350 |
| FR-47 | Booking needs profile ≥ 60%, allows up to 2 employers and blocks times < 30 min apart |
| FR-66 | 56 badges in 7 categories; only earned badges can be pinned; up to 3 pins |
| FR-75 | Feedback appears only when BR-85 allows; sending credits +5 XP at most once every 30 days |

| FR | Acceptance criterion |
| --- | --- |
| Section 3.3 | Next on Backup contact without their mobile, or on Timeline, Trophy shelf or Receipts with nothing added, credits 0 and shows “\<step> locked in”; with it, +20 once |
| FR-34 | Each mission credits its XP once; un-ticking later (withdraw, cancel) takes no XP back |
| FR-06, FR-82 | Log out always opens the log-out dialog; “Stay and keep going” keeps the session, “Log out anyway” ends it and shows “Logged out. See you soon!” |
| FR-04 | After the email code, “You’re in” stays until Continue is pressed |
| BR-11 | Wrong codes never show a count; the hidden limit returns 429 |
| BR-02 | From Launchpad → Hire Fest, any mix of tab switches, then Back, lands on Launchpad |
| BR-138 | Profile, My Stats, the quest HUD, Arena and the log-out dialog show the same XP, level and rank for one learner |
| FR-67 | Badge counts include earned badges only; at 100% strength the slim complete strip shows |

### 10.4 Sources

- Code: `skillmuni-web` (Angular), final launch build of 14 Oct 2026 (checkpoint `skillmuni-launch-ready-14oct`); mock backend in `src/mocks`.
- Decisions: `docs/ASSUMPTIONS.md`, A-1 to A-322 (project doc `phase-1/ASSUMPTIONS.md`).
- Legal: Skillmuni Terms of Use & Privacy Policy v1.0 (2025).
- Appendix workbook: `Skillmuni-India-BRD-appendix.xlsx` (XP table, badges, API, missing APIs), regenerated 14 Oct to match this BRD.
