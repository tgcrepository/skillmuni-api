# SkillMuni frontend contract · types

Request/response types the Angular app sends and expects (`src/app/core/**/*.api.ts`, 14 Oct 2026, final). The .NET services must serialise to exactly these shapes (camelCase).

## `src/app/core/arena/arena.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/** Who is on the board (Figma Leaderboard v2 · scope tabs). */
export type ArenaScope = 'everyone' | 'batch' | 'friends';
/** Which window the XP counts over (period tabs). */
export type ArenaPeriod = 'week' | 'month' | 'all';

/** One player on the board. `movement` = places gained (+) or lost (−) since yesterday. */
export interface ArenaPlayer {
  readonly id: string;
  readonly rank: number;
  readonly name: string;
  readonly initials: string;
  readonly photo?: string;
  readonly xp: number;
  readonly streak: number;
  readonly modulesDone: number;
  readonly movement: number;
  readonly you: boolean;
  /** Player Peek (58:445) */
  readonly title: string;
  readonly level: number;
  readonly badges: number;
  readonly topSkill: string;
  readonly cheers: number;
  readonly cheered: boolean;
}

export type StreakDay = 'done' | 'today' | 'upcoming' | 'missed';

export interface ArenaBoard {
  readonly scope: ArenaScope;
  readonly period: ArenaPeriod;
  /** Ranked by XP, ties to the earlier timestamp (already sorted by the server). */
  readonly players: readonly ArenaPlayer[];
  /** The learner's own standing, even when they're far down the list. */
  readonly me: {
    readonly rank: number;
    readonly xp: number;
    readonly rankToday: number;
    readonly streak: number;
    /** Name of the player just above, for "N XP to pass X". */
    readonly nextName: string | null;
    readonly nextXp: number | null;
    readonly topXp: number;
  };
  /** Mon → Sun */
  readonly week: readonly StreakDay[];
  /** Top N move up a league. */
  readonly promotionRank: number;
  readonly league: string;
  /** The learner passed someone since their last visit. */
  readonly rankUp: { readonly to: number; readonly passed: string } | null;
  readonly rewards: {
    readonly coins: number;
    readonly voucher: {
      readonly discount: string;
      readonly title: string;
      readonly cost: number;
      readonly ready: number;
    } | null;
  };
}

export interface ArenaApi {
  board(scope: ArenaScope, period: ArenaPeriod): Observable<ArenaBoard>;
  /** Toggle a cheer; returns the new count. */
  cheer(playerId: string, on: boolean): Observable<{ cheers: number }>;
  /** Invite to a friendly 1-week XP race. */
  challenge(playerId: string): Observable<void>;
}

@Service()
export class HttpArenaApi implements ArenaApi {
  private readonly api = inject(ApiClient);

  board(scope: ArenaScope, period: ArenaPeriod): Observable<ArenaBoard> {
    return this.api.get('arena', { params: { scope, period } });
  }

  cheer(playerId: string, on: boolean): Observable<{ cheers: number }> {
    return this.api.post(`arena/players/${encodeURIComponent(playerId)}/cheer`, { on });
  }

  challenge(playerId: string): Observable<void> {
    return this.api.post(`arena/players/${encodeURIComponent(playerId)}/challenge`, {});
  }
}

export const ARENA_API = new InjectionToken<ArenaApi>('ARENA_API', {
  factory: () => inject(HttpArenaApi),
});
```

## `src/app/core/assessment/assessment.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/** The 9 Career Drivers from the Career Drivers Survey 2025. */
export type DriverId = 'A' | 'B' | 'C' | 'D' | 'E' | 'F' | 'G' | 'H' | 'I';
export const DRIVER_IDS: readonly DriverId[] = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I'];

/**
 * One tap per pair (Figma note 268:22368): the notch picks one of the 4 valid 3-coin splits.
 * 0 = All in This (3·0) · 1 = Lean This (2·1) · 2 = Lean That (1·2) · 3 = All in That (0·3).
 * `null` = not reached yet, `'skip'` = skipped for now (asked again at the final check).
 */
export type Notch = 0 | 1 | 2 | 3;
export type Answer = Notch | 'skip' | null;

export interface Statement {
  readonly text: string;
  /** Hidden from the learner until the reveal (anti-bias). */
  readonly driver: DriverId;
}

export interface Pair {
  readonly n: number;
  readonly this: Statement;
  readonly that: Statement;
}

export interface Island {
  readonly n: number;
  readonly name: string;
  readonly emoji: string;
  /** First and last pair number (1-based, inclusive). */
  readonly from: number;
  readonly to: number;
}

export interface DriverInfo {
  readonly id: DriverId;
  readonly name: string;
  readonly emoji: string;
  readonly focus: string;
  readonly definition: string;
  readonly blurb: string;
  readonly strengthZones: string;
  readonly families: string;
}

export interface Archetype {
  readonly id: string;
  readonly name: string;
  readonly emoji: string;
  readonly dominant: readonly DriverId[];
  readonly secondary: readonly DriverId[];
  /** "± D or G": the stronger of these counts as a secondary driver. */
  readonly eitherOf?: readonly DriverId[];
  readonly roles: string;
  readonly redFlags: string;
  readonly summary: string;
}

export type DriverScores = Readonly<Record<DriverId, number>>;

export interface AssessmentState {
  readonly status: 'new' | 'in-progress' | 'done';
  readonly pairs: readonly Pair[];
  readonly islands: readonly Island[];
  readonly answers: readonly Answer[];
  /** 0-based index of the pair to show next. */
  readonly current: number;
  /** XP earned in this attempt so far (0 on retakes). */
  readonly xp: number;
  /** First attempt earns XP; retakes don't (Figma S2). */
  readonly earnsXp: boolean;
  readonly remind: boolean;
  readonly attempts: number;
}

export interface AttemptRef {
  readonly n: number;
  readonly date: string;
  readonly archetype: string;
}

export interface AssessmentResult {
  readonly attempt: number;
  readonly date: string;
  readonly scores: DriverScores;
  /** Highest first; ties keep survey order. */
  readonly ranking: readonly DriverId[];
  readonly archetype: Archetype;
  readonly drivers: readonly DriverInfo[];
  readonly previous: DriverScores | null;
  readonly attempts: readonly AttemptRef[];
  readonly xpEarned: number;
  readonly mandatoryCourses: number;
}

export interface SaveRequest {
  readonly answers: readonly Answer[];
  readonly current: number;
}

export interface SaveResponse {
  readonly xp: number;
  /** XP just earned by this save (an island cleared). */
  readonly xpEarned: number;
}

export interface AssessmentApi {
  state(): Observable<AssessmentState>;
  save(body: SaveRequest): Observable<SaveResponse>;
  submit(answers: readonly Answer[]): Observable<AssessmentResult>;
  result(attempt?: number): Observable<AssessmentResult>;
  restart(): Observable<AssessmentState>;
  retake(): Observable<AssessmentState>;
  setRemind(on: boolean): Observable<{ remind: boolean }>;
}

@Service()
export class HttpAssessmentApi implements AssessmentApi {
  private readonly api = inject(ApiClient);

  state(): Observable<AssessmentState> {
    return this.api.get('assessments/career');
  }

  save(body: SaveRequest): Observable<SaveResponse> {
    return this.api.put('assessments/career/answers', body);
  }

  submit(answers: readonly Answer[]): Observable<AssessmentResult> {
    return this.api.post('assessments/career/submit', { answers });
  }

  result(attempt?: number): Observable<AssessmentResult> {
    return this.api.get('assessments/career/result', attempt ? { params: { attempt } } : {});
  }

  restart(): Observable<AssessmentState> {
    return this.api.post('assessments/career/restart', {});
  }

  retake(): Observable<AssessmentState> {
    return this.api.post('assessments/career/retake', {});
  }

  setRemind(on: boolean): Observable<{ remind: boolean }> {
    return this.api.put('assessments/career/remind', { remind: on });
  }
}

export const ASSESSMENT_API = new InjectionToken<AssessmentApi>('ASSESSMENT_API', {
  providedIn: 'root',
  factory: () => inject(HttpAssessmentApi),
});
```

## `src/app/core/auth/auth.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

export interface AuthUser {
  readonly id: string;
  readonly firstName: string;
  readonly email?: string;
  readonly phone?: string;
  /** Profile photo for the top bar (A-277). */
  readonly photoUrl?: string | null;
}

export interface OtpChallenge {
  /** Masked or formatted number the code went to, e.g. "+91 98765 43210". */
  readonly sentTo: string;
  /** Seconds until "Resend" unlocks. */
  readonly resendIn: number;
  readonly triesLeft: number;
}

export interface ResetRequested {
  readonly resendIn: number;
  /** Minutes the reset link stays valid. */
  readonly validFor: number;
}

export interface Registration {
  readonly firstName: string;
  readonly lastName: string;
  readonly email: string;
  readonly password: string;
  readonly consent: boolean;
}

export interface EmailChallenge {
  readonly email: string;
  readonly resendIn: number;
  /** Minutes the code stays valid. */
  readonly validFor: number;
  readonly triesLeft: number;
}

/**
 * Error codes in ApiError.fieldErrors['code'] the auth screens map to states:
 * invalid-code · code-expired · too-many-tries · account-locked · wrong-credentials ·
 * account-not-found (404 from a send-code call: that mobile / email isn't registered, A-308).
 */
export type AuthErrorCode =
  | 'invalid-code'
  | 'code-expired'
  | 'too-many-tries'
  | 'account-locked'
  | 'wrong-credentials'
  | 'account-not-found';

/**
 * A-308: what login hands sign-up (router navigation state, never the URL) when the mobile or
 * email it was given has no account.
 */
export interface SignupPrefill {
  readonly from: 'login';
  readonly email?: string;
  /** Formatted for display, e.g. "+91 91234 56789". */
  readonly phone?: string;
}

export interface AuthApi {
  /** 404 account-not-found when no learner has this mobile (A-308). */
  sendOtp(phone: string): Observable<OtpChallenge>;
  verifyOtp(phone: string, code: string): Observable<AuthUser>;
  /** Email login (A-159): a 6-digit code to the inbox, no password. 404 account-not-found (A-308). */
  sendEmailLoginCode(email: string): Observable<OtpChallenge>;
  verifyEmailLogin(email: string, code: string): Observable<AuthUser>;
  loginWithPassword(email: string, password: string): Observable<AuthUser>;
  requestPasswordReset(email: string): Observable<ResetRequested>;
  logout(): Observable<void>;
  /** The signed-in learner from the session cookie, or a 401 when there is none. */
  me(): Observable<AuthUser>;
  register(data: Registration): Observable<EmailChallenge>;
  verifyEmail(email: string, code: string): Observable<AuthUser>;
  resendEmailCode(email: string): Observable<EmailChallenge>;
  /** A-253: exchange a Google ID token; `isNew` = this Google account just created a learner. */
  googleSignIn(credential: string): Observable<GoogleSignIn>;
}

export interface GoogleSignIn {
  readonly user: AuthUser;
  readonly isNew: boolean;
}

/** HTTP implementation. The mock backend (MSW) serves the same routes in development. */
@Service()
export class HttpAuthApi implements AuthApi {
  private readonly api = inject(ApiClient);
  private readonly opts = { skipSessionCheck: true } as const;

  sendOtp(phone: string): Observable<OtpChallenge> {
    return this.api.post('auth/otp', { phone }, this.opts);
  }

  verifyOtp(phone: string, code: string): Observable<AuthUser> {
    return this.api.post('auth/otp/verify', { phone, code }, this.opts);
  }

  sendEmailLoginCode(email: string): Observable<OtpChallenge> {
    return this.api.post('auth/email/login-code', { email }, this.opts);
  }

  verifyEmailLogin(email: string, code: string): Observable<AuthUser> {
    return this.api.post('auth/email/login-verify', { email, code }, this.opts);
  }

  loginWithPassword(email: string, password: string): Observable<AuthUser> {
    return this.api.post('auth/login', { email, password }, this.opts);
  }

  requestPasswordReset(email: string): Observable<ResetRequested> {
    return this.api.post('auth/password-reset', { email }, this.opts);
  }

  logout(): Observable<void> {
    return this.api.post('auth/logout', {}, this.opts);
  }

  me(): Observable<AuthUser> {
    return this.api.get('session', this.opts);
  }

  register(data: Registration): Observable<EmailChallenge> {
    return this.api.post('auth/register', data, this.opts);
  }

  verifyEmail(email: string, code: string): Observable<AuthUser> {
    return this.api.post('auth/email/verify', { email, code }, this.opts);
  }

  googleSignIn(credential: string): Observable<GoogleSignIn> {
    return this.api.post('auth/google', { credential }, this.opts);
  }

  resendEmailCode(email: string): Observable<EmailChallenge> {
    return this.api.post('auth/email/resend', { email }, this.opts);
  }
}

export const AUTH_API = new InjectionToken<AuthApi>('AUTH_API', {
  factory: () => inject(HttpAuthApi),
});
```

## `src/app/core/claim/claim.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { AuthUser, OtpChallenge } from '../auth/auth.api';

/** A CV an institute uploaded for a learner (Figma section 11: V1 200:36751 · V2 200:36844). */
export interface ClaimInvite {
  readonly institute: string;
  /** Name as the institute entered it. */
  readonly name: string;
  readonly programme: string;
  readonly batch: string;
  /** e.g. "Contact, education, 8 skills, 2 languages, 1 job" */
  readonly fromCv: string;
  /** Masked number the code goes to, e.g. "+91 98•••• 4521". */
  readonly sentTo: string;
  readonly resendIn: number;
  readonly triesLeft: number;
  /** ISO date the CV is deleted if nobody claims it. */
  readonly deleteBy: string;
  /** e.g. "NSQF L4 certificate" — shown on the verify-certificate choice. */
  readonly certificate: string;
  /**
   * Present when this session already verified the code (e.g. the learner left V3 to edit a quest and
   * opened the link again): where to pick up.
   */
  readonly resume?: ClaimResume;
}

export interface ClaimResume {
  readonly stage: 'consent' | 'validate' | 'confirmed';
  readonly sections: readonly ClaimSection[];
  readonly result: ClaimResult | null;
}

/** Where a value in a V3 section came from. */
export type ClaimSource = 'institute' | 'verified' | 'you' | null;

export interface ClaimRow {
  readonly label: string;
  readonly value: string;
  readonly source: ClaimSource;
  /** e.g. "end date missing" — flags the row in a "Fix this" section. */
  readonly issue?: string;
}

export type ClaimSectionStatus = 'confirmed' | 'review' | 'fix';

/** V3 · one quest's worth of what the institute added. */
export interface ClaimSection {
  /** Quest slug, e.g. "big-brain-stuff". */
  readonly slug: string;
  readonly quest: number;
  readonly title: string;
  readonly summary: string;
  readonly status: ClaimSectionStatus;
  readonly rows: readonly ClaimRow[];
}

export interface ClaimLeftQuest {
  readonly quest: number;
  readonly slug: string;
  readonly name: string;
  readonly blurb: string;
  readonly xp: number;
}

/** V4 · Confirmed — next steps. */
export interface ClaimResult {
  readonly profilePercent: number;
  readonly verifiedItems: number;
  readonly jobs: number;
  readonly xpLeft: number;
  readonly minutesLeft: number;
  readonly left: readonly ClaimLeftQuest[];
}

export type DeclineReason = 'not-me' | 'not-now' | 'has-account' | 'other';

export interface ClaimConsent {
  readonly shareProgress: boolean;
  readonly verifyCertificate: boolean;
  readonly applyForMe: boolean;
}

/**
 * Institute claim link. `invite` 404s for unknown links and 410s for claimed / expired / deleted ones
 * (`errors.code`: claimed · expired · deleted). `verify` signs the learner in (session cookie).
 */
export interface ClaimApi {
  invite(token: string): Observable<ClaimInvite>;
  resend(token: string): Observable<OtpChallenge>;
  verify(token: string, code: string): Observable<AuthUser>;
  /** Saves the choices and returns the sections to validate (V3). */
  consent(token: string, choices: ClaimConsent): Observable<readonly ClaimSection[]>;
  /** V3 "Looks right" on one section. */
  confirmSection(token: string, slug: string): Observable<void>;
  /** All sections confirmed → V4 numbers. */
  finish(token: string): Observable<ClaimResult>;
  /** V5 · deletes the CV and everything read from it; the institute sees "Declined", not why. */
  decline(token: string, reason: DeclineReason | null): Observable<void>;
}

@Service()
export class HttpClaimApi implements ClaimApi {
  private readonly api = inject(ApiClient);

  invite(token: string): Observable<ClaimInvite> {
    return this.api.get(`claims/${encodeURIComponent(token)}`);
  }

  resend(token: string): Observable<OtpChallenge> {
    return this.api.post(`claims/${encodeURIComponent(token)}/otp`, {});
  }

  verify(token: string, code: string): Observable<AuthUser> {
    return this.api.post(`claims/${encodeURIComponent(token)}/verify`, { code });
  }

  consent(token: string, choices: ClaimConsent): Observable<readonly ClaimSection[]> {
    return this.api.post(`claims/${encodeURIComponent(token)}/consent`, choices);
  }

  confirmSection(token: string, slug: string): Observable<void> {
    return this.api.post(
      `claims/${encodeURIComponent(token)}/sections/${encodeURIComponent(slug)}/confirm`,
      {},
    );
  }

  finish(token: string): Observable<ClaimResult> {
    return this.api.post(`claims/${encodeURIComponent(token)}/finish`, {});
  }

  decline(token: string, reason: DeclineReason | null): Observable<void> {
    return this.api.post(`claims/${encodeURIComponent(token)}/decline`, { reason });
  }
}

export const CLAIM_API = new InjectionToken<ClaimApi>('CLAIM_API', {
  factory: () => inject(HttpClaimApi),
});
```

## `src/app/core/contact/contact.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { OtpChallenge } from '../auth/auth.api';
import { MarketCode } from '../market/market';

export interface PostalArea {
  readonly name: string;
  /** District / municipality shown under the area name. */
  readonly sub: string;
}

export interface PostalLookup {
  /** State / UT (IN) or Province (ZA). */
  readonly region: string;
  /** District (IN) or Municipality (ZA). */
  readonly district: string;
  readonly city: string;
  readonly areas: readonly PostalArea[];
}

/** Q2 Hit Me Up: verify the contact number and look addresses up by PIN / postal code. */
export interface ContactApi {
  sendPhoneCode(phone: string): Observable<OtpChallenge>;
  verifyPhone(phone: string, code: string): Observable<void>;
  /** 404 → "PIN not found" state (pick the state yourself). */
  lookupPostal(market: MarketCode, code: string): Observable<PostalLookup>;
  regions(market: MarketCode): Observable<readonly string[]>;
}

@Service()
export class HttpContactApi implements ContactApi {
  private readonly api = inject(ApiClient);

  sendPhoneCode(phone: string): Observable<OtpChallenge> {
    return this.api.post('me/phone/code', { phone });
  }

  verifyPhone(phone: string, code: string): Observable<void> {
    return this.api.post('me/phone/verify', { phone, code });
  }

  lookupPostal(market: MarketCode, code: string): Observable<PostalLookup> {
    return this.api.get(`geo/${market}/postal/${encodeURIComponent(code)}`);
  }

  regions(market: MarketCode): Observable<readonly string[]> {
    return this.api.get(`geo/${market}/regions`);
  }
}

export const CONTACT_API = new InjectionToken<ContactApi>('CONTACT_API', {
  factory: () => inject(HttpContactApi),
});

/** Common email-domain typos → suggestion ("Did you mean …@gmail.com?"). */
const DOMAIN_FIXES: Readonly<Record<string, string>> = {
  'gmial.com': 'gmail.com',
  'gmal.com': 'gmail.com',
  'gmai.com': 'gmail.com',
  'gamil.com': 'gmail.com',
  'gnail.com': 'gmail.com',
  'gmail.co': 'gmail.com',
  'yaho.com': 'yahoo.com',
  'yahooo.com': 'yahoo.com',
  'hotmal.com': 'hotmail.com',
  'hotmial.com': 'hotmail.com',
  'outlok.com': 'outlook.com',
  'rediffmai.com': 'rediffmail.com',
};

export function emailSuggestion(email: string): string | null {
  const at = email.lastIndexOf('@');
  if (at < 1) return null;
  const domain = email.slice(at + 1).toLowerCase();
  const fix = DOMAIN_FIXES[domain];
  return fix ? `${email.slice(0, at)}@${fix}` : null;
}
```

## `src/app/core/cv/cv.api.ts`

```ts
import { HttpClient, HttpEvent, HttpEventType } from '@angular/common/http';
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable, catchError, filter, map, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiClient } from '../api/api-client';
import { toApiError } from '../api/api-error';
import { Connectivity } from '../network/connectivity';

/** Upload progress (0–100) and, at the end, the stored CV id. */
export type UploadEvent =
  | { readonly kind: 'progress'; readonly percent: number }
  | { readonly kind: 'done'; readonly cvId: string };

/**
 * Outcome codes the parser can return — Figma S1–S9:
 * unsupported-type (S1) · too-large (S2) · locked-pdf (S3) · blurry-photo (S4) · other-language (S5) ·
 * parse-failed (S6) · no-work-history (S7) · little-found (S8) · email-taken (S9).
 */
export type CvOutcome =
  | 'unsupported-type'
  | 'too-large'
  | 'locked-pdf'
  | 'blurry-photo'
  | 'other-language'
  | 'parse-failed'
  | 'no-work-history'
  | 'little-found'
  | 'email-taken';

export interface ParseStatus {
  readonly state: 'parsing' | 'done' | 'failed';
  /** 0–100 */
  readonly percent: number;
  /** Index of the step in progress (0–5); steps before it are done. */
  readonly step: number;
  /** Muni's live reaction to what was found, e.g. "Ooh, a call-centre internship? Love that for you." */
  readonly quip?: string;
  readonly outcome?: CvOutcome;
  /** Detail for the outcome copy: language name, details found, support reference. */
  readonly detail?: string;
}

export interface CvField {
  readonly key: string;
  readonly label: string;
  readonly value: string;
  /** Amber "Check" rows: why it needs a look, e.g. "Found a suburb only — add your postal code". */
  readonly check?: string;
}

export interface CvSection {
  readonly quest: number;
  readonly title: string;
  /** Badge text, e.g. "4 found · 1 to check", "9 skills · 3 languages". */
  readonly badge: string;
  readonly fields: readonly CvField[];
  readonly chips?: readonly string[];
}

export interface CvSummary {
  readonly questsPrefilled: number;
  readonly detailsFound: number;
  readonly toCheck: number;
  readonly skillsMatched: number;
  readonly sections: readonly CvSection[];
  /** Personal details the parser skipped and did not keep, e.g. ["photo", "ID number"]. */
  readonly skipped: readonly string[];
}

export interface CvApi {
  /** One file, or several photographed pages (F1) sent together as `pages`. */
  upload(file: File | readonly File[]): Observable<UploadEvent>;
  status(cvId: string): Observable<ParseStatus>;
  summary(cvId: string): Observable<CvSummary>;
  remove(cvId: string): Observable<void>;
}

@Service()
export class HttpCvApi implements CvApi {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiClient);
  private readonly connectivity = inject(Connectivity);

  upload(file: File | readonly File[]): Observable<UploadEvent> {
    const body = new FormData();
    if (Array.isArray(file))
      for (const page of file as readonly File[]) body.append('pages', page, page.name);
    else body.append('file', file as File, (file as File).name);
    return this.http
      .post<{ cvId: string }>(`${environment.apiBaseUrl}/cv`, body, {
        reportProgress: true,
        observe: 'events',
        withCredentials: true,
      })
      .pipe(
        filter(
          (e: HttpEvent<{ cvId: string }>) =>
            e.type === HttpEventType.UploadProgress || e.type === HttpEventType.Response,
        ),
        map((e): UploadEvent => {
          if (e.type === HttpEventType.UploadProgress) {
            return {
              kind: 'progress',
              percent: e.total ? Math.round((e.loaded / e.total) * 100) : 0,
            };
          }
          return { kind: 'done', cvId: (e.type === HttpEventType.Response && e.body?.cvId) || '' };
        }),
        catchError((err: unknown) => throwError(() => toApiError(err, this.connectivity.online()))),
      );
  }

  status(cvId: string): Observable<ParseStatus> {
    return this.api.get(`cv/${cvId}/status`);
  }

  summary(cvId: string): Observable<CvSummary> {
    return this.api.get(`cv/${cvId}/summary`);
  }

  remove(cvId: string): Observable<void> {
    return this.api.delete(`cv/${cvId}`);
  }
}

export const CV_API = new InjectionToken<CvApi>('CV_API', { factory: () => inject(HttpCvApi) });

/** Client-side checks before upload (S1 / S2). 10 MB per the dropzone copy. */
export const CV_MAX_BYTES = 10 * 1024 * 1024;
export const CV_ACCEPT = '.pdf,.doc,.docx,image/jpeg,image/png,image/heic,image/webp';
const CV_EXTENSIONS = ['pdf', 'doc', 'docx', 'jpg', 'jpeg', 'png', 'heic', 'webp'];

export function checkCvFile(file: File): CvOutcome | null {
  const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
  if (!CV_EXTENSIONS.includes(ext)) return 'unsupported-type';
  if (file.size > CV_MAX_BYTES) return 'too-large';
  return null;
}
```

## `src/app/core/feedback/feedback.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

export type FeedbackMood = 1 | 2 | 3 | 4 | 5;

export interface FeedbackAnswer {
  readonly mood: FeedbackMood;
  readonly tags: readonly string[];
  readonly comment: string;
  /** Screen it was given on, for context. */
  readonly screen: string;
}

export interface FeedbackApi {
  /** Resolves with the XP earned (+5 at most once a month). */
  send(answer: FeedbackAnswer): Observable<{ readonly xp: number }>;
}

@Service()
export class HttpFeedbackApi implements FeedbackApi {
  private readonly api = inject(ApiClient);

  send(answer: FeedbackAnswer): Observable<{ readonly xp: number }> {
    return this.api.post('me/feedback', answer);
  }
}

export const FEEDBACK_API = new InjectionToken<FeedbackApi>('FEEDBACK_API', {
  factory: () => inject(HttpFeedbackApi),
});
```

## `src/app/core/fest/fest.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/** Hire Fest v2 (Figma 315:44812, handoff 318:55923). */

export type FestMode = 'in-person' | 'online';

export interface FestEmployer {
  readonly id: string;
  readonly company: string;
  readonly initials: string;
  readonly role: string;
  readonly openings: number;
  readonly slotsLeft: number;
  /** "Day 1–3" for multi-day fests. */
  readonly days: string;
  readonly match: number;
  readonly full: boolean;
}

export interface Fest {
  readonly id: string;
  readonly name: string;
  readonly mode: FestMode;
  readonly city: string;
  /** ISO dates (first and last day). */
  readonly start: string;
  readonly end: string;
  /** "1 – 3 Dec 2026", "Wed, 14 Jan 2027". */
  readonly dateLabel: string;
  readonly hours: string;
  readonly venue: string;
  readonly venueNote: string;
  readonly distance: string | null;
  readonly organiser: string;
  readonly employers: readonly FestEmployer[];
  readonly employerCount: number;
  readonly roleCount: number;
  /** Display price: "Free", "₹199". */
  readonly price: string;
  readonly free: boolean;
  readonly seatsLeft: number;
  readonly matchingRoles: number;
  readonly bookingCloses: string;
  readonly cancelPolicy: string;
  readonly beforeYouGo: readonly string[];
  readonly booked: boolean;
  readonly waitlistOpen: boolean;
  readonly saved: boolean;
}

export interface FestDay {
  readonly date: string;
  readonly label: string;
  readonly fewLeft: boolean;
}

export interface Slot {
  /** "10:30" (24 h, fest local time). */
  readonly time: string;
  readonly label: string;
  readonly left: number;
}

export interface SlotGrid {
  readonly days: readonly FestDay[];
  /** employerId → date → slots. */
  readonly slots: Readonly<Record<string, Readonly<Record<string, readonly Slot[]>>>>;
  /** Interview length and the gap kept between two interviews (minutes). */
  readonly minutes: number;
  readonly buffer: number;
  readonly maxEmployers: number;
}

export interface BookedInterview {
  readonly employerId: string;
  readonly company: string;
  readonly initials: string;
  readonly role: string;
  readonly date: string;
  readonly dayLabel: string;
  readonly time: string;
  readonly range: string;
  readonly desk: string;
}

export interface Booking {
  readonly id: string;
  readonly code: string;
  readonly fest: Fest;
  readonly interviews: readonly BookedInterview[];
  readonly daysToGo: number;
  readonly reminders: Reminders;
  readonly cv: string;
  readonly live: LiveState | null;
}

export interface LiveState {
  readonly checkedInAt: string;
  readonly current: {
    readonly employerId: string;
    readonly position: number;
    readonly waitMinutes: number;
    readonly lateBy: number;
    readonly progress: number;
  };
}

export interface Reminders {
  readonly whatsapp: boolean;
  readonly sms: boolean;
  readonly calendar: boolean;
}

export type Outcome = 'offer' | 'waiting' | 'no' | null;

export interface PastFest {
  readonly id: string;
  readonly name: string;
  readonly date: string;
  readonly day: string;
  readonly month: string;
  readonly year: string;
  readonly interviews: readonly {
    readonly employerId: string;
    readonly company: string;
    readonly initials: string;
    readonly role: string;
    readonly outcome: Outcome;
  }[];
  readonly rating: number | null;
}

export interface FestSummary {
  readonly upcoming: number;
  readonly bookings: number;
  readonly past: number;
  readonly profilePercent: number;
  readonly unlockAt: number;
  readonly missing: readonly {
    readonly title: string;
    readonly detail: string;
    readonly xp: number;
    readonly link: string;
  }[];
  readonly city: string;
}

export interface WaitlistInfo {
  readonly position: number;
  readonly odds: string;
}

export interface FestAlerts {
  readonly city: boolean;
  readonly online: boolean;
}

export interface BookRequest {
  readonly picks: readonly {
    readonly employerId: string;
    readonly date: string;
    readonly time: string;
  }[];
  readonly reminders: Reminders;
  readonly cvTemplate: string;
}

export interface FestApi {
  summary(): Observable<FestSummary>;
  fests(filter: string): Observable<readonly Fest[]>;
  fest(id: string): Observable<Fest>;
  slots(id: string, employers: readonly string[]): Observable<SlotGrid>;
  book(id: string, body: BookRequest): Observable<Booking>;
  bookings(): Observable<readonly Booking[]>;
  booking(id: string): Observable<Booking>;
  cancel(id: string, employerId: string | null, reason: string): Observable<Booking | null>;
  waitlist(
    festId: string,
    employerId: string,
    auto: boolean,
    whatsapp: boolean,
  ): Observable<WaitlistInfo>;
  waitlistInfo(festId: string, employerId: string): Observable<WaitlistInfo>;
  past(): Observable<readonly PastFest[]>;
  outcome(festId: string, employerId: string, outcome: Outcome): Observable<PastFest>;
  rate(festId: string, stars: number): Observable<PastFest>;
  alerts(): Observable<FestAlerts>;
  setAlerts(a: FestAlerts): Observable<FestAlerts>;
  save(id: string, saved: boolean): Observable<Fest>;
}

@Service()
export class HttpFestApi implements FestApi {
  private readonly api = inject(ApiClient);

  summary(): Observable<FestSummary> {
    return this.api.get('fests/summary');
  }

  fests(filter: string): Observable<readonly Fest[]> {
    return this.api.get('fests', { params: { filter } });
  }

  fest(id: string): Observable<Fest> {
    return this.api.get(`fests/${id}`);
  }

  slots(id: string, employers: readonly string[]): Observable<SlotGrid> {
    return this.api.get(`fests/${id}/slots`, { params: { employers: employers.join(',') } });
  }

  book(id: string, body: BookRequest): Observable<Booking> {
    return this.api.post(`fests/${id}/book`, body);
  }

  bookings(): Observable<readonly Booking[]> {
    return this.api.get('fests/bookings');
  }

  booking(id: string): Observable<Booking> {
    return this.api.get(`fests/bookings/${id}`);
  }

  cancel(id: string, employerId: string | null, reason: string): Observable<Booking | null> {
    return this.api.post(`fests/bookings/${id}/cancel`, { employerId, reason });
  }

  waitlist(
    festId: string,
    employerId: string,
    auto: boolean,
    whatsapp: boolean,
  ): Observable<WaitlistInfo> {
    return this.api.post(`fests/${festId}/waitlist`, { employerId, auto, whatsapp });
  }

  waitlistInfo(festId: string, employerId: string): Observable<WaitlistInfo> {
    return this.api.get(`fests/${festId}/waitlist`, { params: { employerId } });
  }

  past(): Observable<readonly PastFest[]> {
    return this.api.get('fests/past');
  }

  outcome(festId: string, employerId: string, outcome: Outcome): Observable<PastFest> {
    return this.api.put(`fests/past/${festId}/outcome`, { employerId, outcome });
  }

  rate(festId: string, stars: number): Observable<PastFest> {
    return this.api.put(`fests/past/${festId}/rating`, { stars });
  }

  alerts(): Observable<FestAlerts> {
    return this.api.get('fests/alerts');
  }

  setAlerts(a: FestAlerts): Observable<FestAlerts> {
    return this.api.put('fests/alerts', a);
  }

  save(id: string, saved: boolean): Observable<Fest> {
    return this.api.put(`fests/${id}/saved`, { saved });
  }
}

export const FEST_API = new InjectionToken<FestApi>('FEST_API', {
  providedIn: 'root',
  factory: () => inject(HttpFestApi),
});
```

## `src/app/core/flex/flex.api.ts`

```ts
import { HttpClient, HttpEvent, HttpEventType } from '@angular/common/http';
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable, catchError, filter, map, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiClient } from '../api/api-client';
import { toApiError } from '../api/api-error';
import { Connectivity } from '../network/connectivity';

/** Trophy file upload progress, then the stored file id. */
export type TrophyUploadEvent =
  | { readonly kind: 'progress'; readonly percent: number }
  | { readonly kind: 'done'; readonly fileId: string };

export interface LinkCheck {
  /** The learner's name was found on the page → "Verified". */
  readonly verified: boolean;
}

export interface ImportedTrophy {
  readonly name: string;
  readonly issuer: string;
  readonly issuedOn: string;
  readonly credentialId: string;
}

/**
 * Q4 trophy shelf backend (Figma §7): server-side credential link check (5 s timeout, never blocks saving),
 * certificate file upload, and the DigiLocker import (Skill India / NPTEL).
 */
export interface FlexApi {
  checkLink(url: string): Observable<LinkCheck>;
  upload(file: File): Observable<TrophyUploadEvent>;
  importDigilocker(): Observable<readonly ImportedTrophy[]>;
}

@Service()
export class HttpFlexApi implements FlexApi {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiClient);
  private readonly connectivity = inject(Connectivity);

  checkLink(url: string): Observable<LinkCheck> {
    return this.api.post('me/trophies/check-link', { url });
  }

  upload(file: File): Observable<TrophyUploadEvent> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http
      .post<{ fileId: string }>(`${environment.apiBaseUrl}/me/trophies/files`, body, {
        reportProgress: true,
        observe: 'events',
        withCredentials: true,
      })
      .pipe(
        filter(
          (e: HttpEvent<{ fileId: string }>) =>
            e.type === HttpEventType.UploadProgress || e.type === HttpEventType.Response,
        ),
        map((e): TrophyUploadEvent => {
          if (e.type === HttpEventType.UploadProgress)
            return {
              kind: 'progress',
              percent: e.total ? Math.round((e.loaded / e.total) * 100) : 0,
            };
          return {
            kind: 'done',
            fileId: (e.type === HttpEventType.Response && e.body?.fileId) || '',
          };
        }),
        catchError((err: unknown) => throwError(() => toApiError(err, this.connectivity.online()))),
      );
  }

  importDigilocker(): Observable<readonly ImportedTrophy[]> {
    return this.api.post('me/trophies/digilocker', {});
  }
}

export const FLEX_API = new InjectionToken<FlexApi>('FLEX_API', {
  factory: () => inject(HttpFlexApi),
});
```

## `src/app/core/home/home.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/** Which greeting to show. The server decides from the learner's local time zone (A-170). */
export type Daypart = 'morning' | 'afternoon' | 'evening';

/** Colour family of a pathway card (Figma Pathway Card: Arcane · Ember · Success). */
export type PathwayTone = 'arcane' | 'ember' | 'success';

/** "Pick up where you left off" (Figma Home v2 · Continue learning 129:3784). */
export interface ResumeCard {
  /** e.g. "Brain Vita" */
  readonly module: string;
  /** e.g. "Non-Verbal Reasoning" */
  readonly topic: string;
  readonly brief: number;
  readonly briefs: number;
  /** e.g. "1 quiz away from 100% mastery" */
  readonly note: string;
  /** 0–100 */
  readonly percent: number;
  /** XP the next step earns. */
  readonly xp: number;
  readonly art: string;
  /** App route to continue. */
  readonly link: string;
}

export interface Pathway {
  readonly slug: string;
  readonly title: string;
  readonly blurb: string;
  readonly tone: PathwayTone;
  readonly art: string;
  /** App route, or null while that pathway isn't open yet. */
  readonly link: string | null;
}

/** One module tile inside a Knook (Figma Knook Tile 48:207). */
export interface KnookModule {
  readonly slug: string;
  readonly title: string;
  readonly art: string;
  readonly link: string;
}

/** A shelf of modules (Skill Knook, Industry Knook…). */
export interface Knook {
  readonly slug: string;
  readonly title: string;
  readonly icon: string;
  /** Open on first load (Figma: Skill and Industry open, the rest collapsed). */
  readonly expanded: boolean;
  readonly modules: readonly KnookModule[];
}

/** A first-time learner's next move on the Lobby (A-233). */
export interface FirstStep {
  readonly id: 'assessment' | 'learn' | 'jobs';
  readonly title: string;
  /** e.g. "36 quick this-or-that picks · ~12 min" */
  readonly meta: string;
  /** XP on offer, or null when the step earns none. */
  readonly xp: number | null;
  readonly link: string;
  readonly done: boolean;
}

/** Everything the Lobby needs in one call. */
export interface HomeFeed {
  readonly daypart: Daypart;
  readonly resume: ResumeCard | null;
  /** New learners only: three guided first moves. Null for a returning learner. */
  readonly firstSteps?: readonly FirstStep[] | null;
  /** A-266: bonus for finishing all first moves; `claimed` once the learner has collected it. */
  readonly firstMovesBonus?: { readonly xp: number; readonly claimed: boolean } | null;
  readonly pathways: readonly Pathway[];
  readonly knooks: readonly Knook[];
  /** Vouchers ready to redeem (badge on Rewards). */
  readonly rewardsReady: number;
  readonly unreadNotifications: number;
}

export interface HomeApi {
  feed(): Observable<HomeFeed>;
  /** A first move done by visiting it (e.g. opening My Jobs from the Lobby). */
  completeStep(id: FirstStep['id']): Observable<void>;
  /** Collect the first-moves bonus; returns the XP added. */
  claimFirstMoves(): Observable<{ readonly xp: number }>;
}

@Service()
export class HttpHomeApi implements HomeApi {
  private readonly api = inject(ApiClient);

  feed(): Observable<HomeFeed> {
    return this.api.get('home');
  }

  completeStep(id: FirstStep['id']): Observable<void> {
    return this.api.post(`home/first-steps/${id}`, {});
  }

  claimFirstMoves(): Observable<{ readonly xp: number }> {
    return this.api.post('home/first-moves/claim', {});
  }
}

export const HOME_API = new InjectionToken<HomeApi>('HOME_API', {
  factory: () => inject(HttpHomeApi),
});
```

## `src/app/core/identity/identity.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/** Documents DigiLocker issues that can confirm identity in India (A-272). */
export type DigiDoc = 'aadhaar' | 'pan' | 'driving-licence';

/**
 * A government ID linked through DigiLocker (A-272). Skillmuni never receives or stores the full
 * number or a copy of the document: only which document, its last 4 characters, and the verified
 * details the learner agreed to share.
 */
export interface LinkedId {
  readonly doc: DigiDoc;
  /** e.g. "XXXX XXXX 1234" (Aadhaar) or "XXXXXX123F" (PAN) — the server masks it. */
  readonly masked: string;
  readonly last4: string;
  /** Verified by the issuer, ISO date (yyyy-mm-dd). */
  readonly birthday: string | null;
  readonly name: string | null;
  readonly verifiedAt: string;
}

export interface IdentityApi {
  /** Real flow: the server returns DigiLocker's consent URL for this document. */
  start(doc: DigiDoc): Observable<{ readonly authorizeUrl: string }>;
  /** Swap the consent code DigiLocker returned for the masked, verified record. */
  link(doc: DigiDoc, code: string): Observable<LinkedId>;
  current(): Observable<LinkedId | null>;
  unlink(): Observable<void>;
}

@Service()
export class HttpIdentityApi implements IdentityApi {
  private readonly api = inject(ApiClient);

  start(doc: DigiDoc): Observable<{ readonly authorizeUrl: string }> {
    return this.api.post('me/identity/digilocker/start', { doc });
  }

  link(doc: DigiDoc, code: string): Observable<LinkedId> {
    return this.api.post('me/identity/digilocker/link', { doc, code });
  }

  current(): Observable<LinkedId | null> {
    return this.api.get('me/identity');
  }

  unlink(): Observable<void> {
    return this.api.delete('me/identity');
  }
}

export const IDENTITY_API = new InjectionToken<IdentityApi>('IDENTITY_API', {
  factory: () => inject(HttpIdentityApi),
});
```

## `src/app/core/job-profile/job-profile.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/**
 * My Job Profile v2 — the Living CV (Figma 308:43436, note 310:55447). The registration quests ARE
 * the job profile: one record, no second form. Every section says which quest collected it, and Edit
 * deep-links back into that quest part.
 */

export type ProofStatus = 'verified' | 'checking' | 'none';

/** Where Edit sends the learner: `/quests/<slug>?part=<part>`. */
export interface QuestRef {
  readonly slug: string;
  readonly part: number;
  readonly name: string;
}

export interface RadarStats {
  readonly people: number;
  readonly business: number;
  readonly data: number;
  readonly creative: number;
  readonly tech: number;
}

export interface WorkItem {
  readonly id: string;
  readonly initials: string;
  readonly role: string;
  readonly employer: string;
  readonly city: string;
  /** "Jan 2025 – Jun 2025 · 6 mos" */
  readonly period: string;
  readonly bullets: readonly string[];
  readonly proof: ProofStatus;
}

export interface EducationItem {
  readonly id: string;
  readonly initials: string;
  readonly title: string;
  readonly school: string;
  readonly detail: string;
  readonly proof: ProofStatus;
}

export interface LanguageItem {
  readonly name: string;
  readonly skills: string;
  readonly level: string;
}

export interface SkillItem {
  readonly name: string;
  /** 1–3 power pips; null = level not set yet. */
  readonly power: number | null;
  readonly signature: boolean;
}

export interface CertificateItem {
  readonly id: string;
  readonly title: string;
  readonly issuer: string;
  readonly status: ProofStatus;
  /** "48 h" while checking. */
  readonly eta: string | null;
}

export interface QuickWin {
  readonly id: string;
  readonly title: string;
  readonly impact: string;
  readonly link: string;
  readonly query: Readonly<Record<string, string | number>>;
}

export interface PendingQuest {
  readonly slug: string;
  readonly title: string;
  readonly detail: string;
  readonly xp: number;
}

/** A-262: one row per registration quest — which steps are in and which are still empty. */
export interface QuestFill {
  readonly id: number;
  readonly slug: string;
  readonly name: string;
  readonly filled: number;
  readonly total: number;
  readonly left: readonly { readonly key: string; readonly label: string; readonly part: number }[];
}

export interface JobProfile {
  readonly name: string;
  readonly initials: string;
  readonly headline: string;
  readonly archetype: { readonly name: string; readonly strongest: string } | null;
  readonly radar: RadarStats | null;
  readonly signature: readonly { readonly name: string; readonly power: number }[];
  readonly counts: {
    readonly skills: number;
    readonly languages: number;
    readonly trophies: number;
  };
  readonly byQuest: readonly QuestFill[];
  readonly strength: {
    readonly percent: number;
    readonly label: string;
    readonly jobMatches: number;
    readonly quickWins: readonly QuickWin[];
  };
  readonly contact: {
    readonly mobile: string;
    readonly mobileVerified: boolean;
    readonly email: string;
    readonly location: string;
    readonly city: string;
    readonly discoverable: boolean;
    readonly invitesBy: string;
    readonly neverShown: string;
  };
  readonly about: {
    readonly text: string;
    readonly byMuni: boolean;
    readonly tags: readonly string[];
  };
  readonly work: readonly WorkItem[];
  readonly education: readonly EducationItem[];
  readonly languages: readonly LanguageItem[];
  readonly skills: readonly SkillItem[];
  readonly certificates: readonly CertificateItem[];
  /** `private` rows (expected pay) never show in the employer view. */
  readonly lookingFor: readonly {
    readonly label: string;
    readonly value: string;
    readonly good?: boolean;
    readonly private?: boolean;
  }[];
  /** Short code on the CV footer: skillmuni.app/v/<code>. */
  readonly verifyCode: string;
  readonly verifiedCount: number;
  /** ISO date-time of the last change to the record. */
  readonly updatedAt: string;
  /** P6: sections a newly-added programme still needs (Career Launchpad added later). */
  readonly pending: readonly PendingQuest[];
  /** Section → quest that collected it. */
  readonly sources: Readonly<Record<SectionId, QuestRef>>;
}

export type SectionId =
  | 'about'
  | 'contact'
  | 'work'
  | 'education'
  | 'languages'
  | 'skills'
  | 'certificates'
  | 'lookingFor';

/** P5: one difference between the newer CV and the record. */
export interface CvChange {
  readonly id: string;
  readonly kind: 'job' | 'skill' | 'certificate' | 'contact';
  readonly title: string;
  readonly detail: string;
  /** Contact changes default to "keep current" (the record holds the verified value). */
  readonly suggest: 'add' | 'keep';
}

export interface ShareSettings {
  readonly url: string;
  readonly hideContact: boolean;
  readonly expiresDays: number;
  readonly viewsThisWeek: number;
}

/** Full contact for the learner's own CV file (never part of the profile payload or employer view). */
export interface CvContact {
  readonly phone: string;
  readonly email: string;
  readonly location: string;
  readonly linkedin: string | null;
  readonly expectedPay: string;
}

export interface JobProfileApi {
  cvContact(): Observable<CvContact>;
  profile(): Observable<JobProfile>;
  saveAbout(text: string): Observable<JobProfile>;
  /** Upload a newer CV; resolves with what changed (nothing is applied yet). */
  uploadCv(file: File): Observable<readonly CvChange[]>;
  applyChanges(ids: readonly string[]): Observable<JobProfile>;
  share(): Observable<ShareSettings>;
  updateShare(s: Pick<ShareSettings, 'hideContact' | 'expiresDays'>): Observable<ShareSettings>;
}

@Service()
export class HttpJobProfileApi implements JobProfileApi {
  private readonly api = inject(ApiClient);

  profile(): Observable<JobProfile> {
    return this.api.get('job-profile');
  }

  cvContact(): Observable<CvContact> {
    return this.api.get('job-profile/cv-contact');
  }

  saveAbout(text: string): Observable<JobProfile> {
    return this.api.put('job-profile/about', { text });
  }

  uploadCv(file: File): Observable<readonly CvChange[]> {
    const body = new FormData();
    body.append('cv', file);
    return this.api.post('job-profile/cv', body);
  }

  applyChanges(ids: readonly string[]): Observable<JobProfile> {
    return this.api.post('job-profile/cv/apply', { ids });
  }

  share(): Observable<ShareSettings> {
    return this.api.get('job-profile/share');
  }

  updateShare(s: Pick<ShareSettings, 'hideContact' | 'expiresDays'>): Observable<ShareSettings> {
    return this.api.put('job-profile/share', s);
  }
}

export const JOB_PROFILE_API = new InjectionToken<JobProfileApi>('JOB_PROFILE_API', {
  factory: () => inject(HttpJobProfileApi),
});
```

## `src/app/core/jobs/jobs.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/** My Jobs v2 tabs, in their fixed order (Figma 272:22353, handoff 282:26513). */
export type JobsTab = 'available' | 'matching' | 'invitations' | 'bookmarked' | 'applied';

export type WorkMode = 'onsite' | 'hybrid' | 'remote';
export type JobType = 'full-time' | 'part-time' | 'learnership' | 'contract';

export interface MatchInfo {
  readonly percent: number;
  /** "Matches: Communication, English, Customer service". */
  readonly skills: readonly string[];
  readonly reasons: readonly { readonly ok: boolean; readonly text: string }[];
  readonly gap: { readonly label: string; readonly link: string } | null;
}

export interface Job {
  readonly id: string;
  readonly title: string;
  readonly company: string;
  readonly initials: string;
  readonly city: string;
  readonly state: string;
  /** "Pune, Maharashtra" or "Remote · India". */
  readonly place: string;
  readonly sector: string;
  readonly role: string;
  readonly workMode: WorkMode;
  readonly jobType: JobType;
  /** "Contract · 6 months", "Learnership". Display label from the server. */
  readonly jobTypeLabel: string;
  /** Display pay: "₹2.4–3.0 LPA", "₹15,000 / month stipend" (format per country, A-221). */
  readonly pay: string;
  readonly postedDays: number;
  readonly applicants: number;
  readonly applyBy: string;
  /** Days until applications close; null when not closing soon. */
  readonly closingInDays: number | null;
  readonly closed: boolean;
  readonly isNew: boolean;
  readonly saved: boolean;
  readonly savedDays: number | null;
  readonly appliedOn: string | null;
  readonly experience: string;
  readonly openings: number;
  readonly shift: string;
  readonly about: string;
  readonly duties: readonly string[];
  readonly requirements: readonly string[];
  readonly skills: readonly string[];
  readonly companyAbout: string;
  readonly match: MatchInfo | null;
}

export type InvitationStatus = 'new' | 'accepted' | 'declined' | 'expired';

export interface Invitation {
  readonly id: string;
  readonly job: Job;
  readonly recruiter: { readonly name: string; readonly initials: string; readonly title: string };
  readonly type: 'apply' | 'interview';
  /** ISO; set for interview invites. */
  readonly interviewAt: string | null;
  readonly message: string;
  readonly respondBy: string;
  readonly daysLeft: number;
  readonly status: InvitationStatus;
}

export type ApplicationStatus =
  'applied' | 'viewed' | 'shortlisted' | 'interview' | 'offer' | 'not-selected' | 'withdrawn';

export interface TimelineStep {
  readonly label: string;
  readonly date: string | null;
  readonly state: 'done' | 'current' | 'todo' | 'failed';
}

export interface Application {
  readonly id: string;
  readonly job: Job;
  readonly appliedOn: string;
  readonly updated: string;
  readonly cv: string;
  readonly status: ApplicationStatus;
  readonly timeline: readonly TimelineStep[];
  readonly interview: {
    readonly at: string;
    readonly mode: string;
    readonly detail: string;
  } | null;
  readonly answers: readonly { readonly q: string; readonly a: string }[];
  readonly similar: number;
}

export interface JobsSummary {
  readonly available: number;
  readonly matching: number;
  readonly invitations: number;
  readonly invitationsNew: number;
  readonly bookmarked: number;
  readonly applied: number;
  /** Matching Jobs unlock at `unlockAt`% profile (J8). */
  readonly profilePercent: number;
  readonly unlockAt: number;
  readonly missing: readonly string[];
  readonly minutesToUnlock: number;
  readonly defaultPlace: string;
}

/** Filter values per key; every value is a list of option ids (single values use one). */
export type FilterState = Readonly<Record<string, readonly string[]>>;

export interface FacetOption {
  readonly id: string;
  readonly label: string;
  readonly count: number;
}

export interface Facet {
  readonly key: string;
  readonly options: readonly FacetOption[];
}

export interface ListQuery {
  readonly q: string;
  readonly place: string;
  readonly filters: FilterState;
  readonly sort: string;
  readonly limit: number;
}

export interface Suggestion {
  readonly key: string;
  readonly id: string;
  readonly label: string;
  readonly count: number;
}

export interface ListResult<T> {
  readonly items: readonly T[];
  readonly total: number;
  readonly facets: readonly Facet[];
  /** J6: filters to remove and the jobs each would bring back. */
  readonly suggestions: readonly Suggestion[];
  /** "1 closing soon", "1 interview coming up". */
  readonly note: string | null;
}

export interface ApplyInfo {
  readonly name: string;
  readonly contact: string;
  readonly cvs: readonly { readonly id: string; readonly name: string; readonly meta: string }[];
  readonly questions: readonly {
    readonly id: string;
    readonly text: string;
    readonly kind: 'yes-no' | 'text';
  }[];
}

export interface ApplyRequest {
  readonly cvId: string;
  readonly answers: Readonly<Record<string, string>>;
  readonly invitationId?: string;
}

export interface AlertSettings {
  readonly on: boolean;
  readonly frequency: 'daily' | 'weekly';
  readonly searches: readonly string[];
}

export interface JobsApi {
  summary(): Observable<JobsSummary>;
  jobs(tab: 'available' | 'matching' | 'bookmarked', q: ListQuery): Observable<ListResult<Job>>;
  invitations(q: ListQuery): Observable<ListResult<Invitation>>;
  applications(q: ListQuery): Observable<ListResult<Application>>;
  job(id: string): Observable<Job>;
  invitation(id: string): Observable<Invitation>;
  application(id: string): Observable<Application>;
  save(id: string, saved: boolean): Observable<Job>;
  applyInfo(id: string): Observable<ApplyInfo>;
  uploadCv(file: File): Observable<{ id: string; name: string; meta: string }>;
  apply(id: string, body: ApplyRequest): Observable<Application>;
  respond(
    id: string,
    action: 'accept' | 'decline' | 'suggest',
    note?: string,
  ): Observable<Invitation>;
  withdraw(id: string): Observable<Application>;
  alerts(): Observable<AlertSettings>;
  setAlerts(body: AlertSettings): Observable<AlertSettings>;
}

const toParams = (q: ListQuery): Record<string, string | number> => ({
  q: q.q,
  place: q.place,
  sort: q.sort,
  limit: q.limit,
  filters: JSON.stringify(q.filters),
});

@Service()
export class HttpJobsApi implements JobsApi {
  private readonly api = inject(ApiClient);

  summary(): Observable<JobsSummary> {
    return this.api.get('jobs/summary');
  }

  jobs(tab: 'available' | 'matching' | 'bookmarked', q: ListQuery): Observable<ListResult<Job>> {
    return this.api.get(`jobs/list/${tab}`, { params: toParams(q) });
  }

  invitations(q: ListQuery): Observable<ListResult<Invitation>> {
    return this.api.get('jobs/invitations', { params: toParams(q) });
  }

  applications(q: ListQuery): Observable<ListResult<Application>> {
    return this.api.get('jobs/applications', { params: toParams(q) });
  }

  job(id: string): Observable<Job> {
    return this.api.get(`jobs/job/${id}`);
  }

  invitation(id: string): Observable<Invitation> {
    return this.api.get(`jobs/invitations/${id}`);
  }

  application(id: string): Observable<Application> {
    return this.api.get(`jobs/applications/${id}`);
  }

  save(id: string, saved: boolean): Observable<Job> {
    return this.api.put(`jobs/job/${id}/saved`, { saved });
  }

  applyInfo(id: string): Observable<ApplyInfo> {
    return this.api.get(`jobs/job/${id}/apply`);
  }

  uploadCv(file: File): Observable<{ id: string; name: string; meta: string }> {
    const body = new FormData();
    body.append('file', file);
    return this.api.post('jobs/cv', body);
  }

  apply(id: string, body: ApplyRequest): Observable<Application> {
    return this.api.post(`jobs/job/${id}/apply`, body);
  }

  respond(
    id: string,
    action: 'accept' | 'decline' | 'suggest',
    note?: string,
  ): Observable<Invitation> {
    return this.api.post(`jobs/invitations/${id}/${action}`, { note: note ?? '' });
  }

  withdraw(id: string): Observable<Application> {
    return this.api.post(`jobs/applications/${id}/withdraw`, {});
  }

  alerts(): Observable<AlertSettings> {
    return this.api.get('jobs/alerts');
  }

  setAlerts(body: AlertSettings): Observable<AlertSettings> {
    return this.api.put('jobs/alerts', body);
  }
}

export const JOBS_API = new InjectionToken<JobsApi>('JOBS_API', {
  providedIn: 'root',
  factory: () => inject(HttpJobsApi),
});
```

## `src/app/core/launchpad/launchpad.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/** The four launch missions, in order (Figma Career Launchpad v2 · 241:18014, note 247:19784). */
export type MissionId = 'assessment' | 'profile' | 'apply' | 'fest';

export interface Mission {
  readonly id: MissionId;
  readonly label: string;
  readonly xp: number;
  readonly done: boolean;
}

export interface Readiness {
  /** 0–4, from the server (level rule "to confirm"). */
  readonly level: number;
  readonly levelName: string;
  /** Weighted: assessment 25 · profile 25 × profile % · applied 25 · fest 25. */
  readonly percent: number;
  readonly missions: readonly Mission[];
}

export interface ProfileCard {
  readonly percent: number;
  readonly sectionsLeft: number;
  readonly sectionsTotal: number;
  /** Quest steps still empty (skipped optional steps), A-262. */
  readonly stepsLeft: number;
  /** Names of sections still to do, e.g. Documents · Skills · Availability. */
  readonly missing: readonly string[];
}

export interface AssessmentCard {
  readonly status: 'not-started' | 'paused' | 'done';
  readonly answered: number;
  readonly total: number;
  readonly minutes: number;
  readonly archetype: {
    readonly name: string;
    readonly emoji: string;
    readonly traits: readonly string[];
  } | null;
  readonly mandatoryCourses: number;
}

export interface JobsCard {
  /** My Jobs unlocks at `unlockAt`% profile (rule "to confirm"). */
  readonly locked: boolean;
  readonly unlockAt: number;
  readonly matched: number;
  readonly applied: number;
  readonly saved: number;
  readonly newMatches: number;
  readonly interviews: number;
  readonly top: {
    readonly title: string;
    readonly employer: string;
    readonly city: string;
    readonly match: number;
  } | null;
}

export interface FestCard {
  readonly fest: {
    readonly id: string;
    readonly name: string;
    /** ISO date */
    readonly date: string;
    readonly daysAway: number;
    readonly recruiters: number;
    readonly free: boolean;
    readonly mode: string;
    readonly slotsLeft: number;
    readonly slotsTotal: number;
    /** "11:30 AM" once booked. */
    readonly bookedSlot: string | null;
  } | null;
  readonly alertsOn: boolean;
}

export interface Sector {
  readonly id: string;
  readonly name: string;
  /** Placeholder icon set (note: "sector icon set — emoji placeholders now"). */
  readonly emoji: string;
  readonly jobs: number;
  readonly newJobs: number;
  /** 0–100 match from the assessment; null before it's taken. */
  readonly fit: number | null;
}

export interface LaunchpadApi {
  readiness(): Observable<Readiness>;
  profile(): Observable<ProfileCard>;
  assessment(): Observable<AssessmentCard>;
  jobs(): Observable<JobsCard>;
  fest(): Observable<FestCard>;
  sectors(): Observable<readonly Sector[]>;
  setFestAlerts(on: boolean): Observable<FestCard>;
}

@Service()
export class HttpLaunchpadApi implements LaunchpadApi {
  private readonly api = inject(ApiClient);

  readiness(): Observable<Readiness> {
    return this.api.get('launchpad/readiness');
  }

  profile(): Observable<ProfileCard> {
    return this.api.get('launchpad/profile');
  }

  assessment(): Observable<AssessmentCard> {
    return this.api.get('launchpad/assessment');
  }

  jobs(): Observable<JobsCard> {
    return this.api.get('launchpad/jobs');
  }

  fest(): Observable<FestCard> {
    return this.api.get('launchpad/fest');
  }

  sectors(): Observable<readonly Sector[]> {
    return this.api.get('launchpad/sectors');
  }

  setFestAlerts(on: boolean): Observable<FestCard> {
    return this.api.put('launchpad/fest/alerts', { on });
  }
}

export const LAUNCHPAD_API = new InjectionToken<LaunchpadApi>('LAUNCHPAD_API', {
  factory: () => inject(HttpLaunchpadApi),
});
```

## `src/app/core/learn/learn.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/* ---------- Module (subcategory) — Figma Brain Vita 67:1228 ---------- */

export interface TopicSummary {
  readonly slug: string;
  readonly title: string;
  readonly art: string;
  readonly quizzesDone: number;
  readonly quizzesTotal: number;
  readonly xp: number;
}

export interface ModuleDetail {
  readonly slug: string;
  readonly title: string;
  readonly tagline: string;
  readonly knook: { readonly slug: string; readonly title: string };
  readonly quizzesDone: number;
  readonly quizzesTotal: number;
  readonly xp: number;
  readonly topics: readonly TopicSummary[];
}

/* ---------- Topic (brief reader) — Figma 78:2520, states 79:3005 ---------- */

export type BriefStatus = 'done' | 'current' | 'locked';

export interface BriefMedia {
  readonly kind: 'image' | 'video';
  readonly src: string;
  readonly alt: string;
  readonly poster?: string;
  /** "4:12" for videos. */
  readonly duration?: string;
}

export interface Brief {
  readonly id: string;
  /** 1-based position in the topic. */
  readonly index: number;
  readonly title: string;
  readonly status: BriefStatus;
  readonly body: string;
  readonly example?: string;
  readonly keyTerm?: string;
  readonly videoUrl?: string;
  readonly media?: BriefMedia;
  readonly bookmarked: boolean;
  readonly helpful: boolean | null;
  /** XP the brief's quiz paid out (0 until passed). */
  readonly xp: number;
  readonly quiz: {
    readonly id: string;
    readonly questions: number;
    /** Last checked score, null until submitted. */
    readonly result: { readonly correct: number; readonly total: number } | null;
  };
}

export interface TopicDetail {
  readonly slug: string;
  readonly title: string;
  readonly module: { readonly slug: string; readonly title: string };
  readonly knook: { readonly title: string };
  readonly quizzesDone: number;
  readonly quizzesTotal: number;
  readonly xp: number;
  readonly briefs: readonly Brief[];
  /** "Next · Course" card — only where the qualification has courses. */
  readonly course: {
    readonly title: string;
    readonly unlocked: boolean;
    readonly link: string;
  } | null;
}

/* ---------- Quiz — Figma 132:2709 (QZ1–QZ5) ---------- */

export interface QuizOption {
  readonly id: string;
  readonly text: string;
}

export interface QuizQuestion {
  readonly id: string;
  readonly prompt: string;
  readonly options: readonly QuizOption[];
}

export interface Quiz {
  readonly id: string;
  readonly title: string;
  readonly module: { readonly slug: string; readonly title: string };
  readonly topic: { readonly slug: string; readonly title: string };
  readonly knook: { readonly title: string };
  readonly quizzesDone: number;
  readonly quizzesTotal: number;
  readonly xp: number;
  readonly questions: readonly QuizQuestion[];
  /** Autosaved picks from an earlier visit. */
  readonly draft: Readonly<Record<string, string>>;
  /** Already checked once: answering again is practice (no XP). */
  readonly alreadyPassed: boolean;
}

export interface QuestionResult {
  readonly questionId: string;
  readonly correctOptionId: string;
  readonly pickedOptionId: string;
  readonly correct: boolean;
  /** Why the right answer is right (always shown). */
  readonly explanation: string;
  /** One line on why the learner's pick was wrong (misses only). */
  readonly pickNote?: string;
}

export interface QuizResult {
  readonly correct: number;
  readonly total: number;
  /** +1 per correct answer, no bonus. */
  readonly xp: number;
  readonly results: readonly QuestionResult[];
  /** Muni's speech bubble. */
  readonly muni: string;
}

export interface LearnApi {
  module(slug: string): Observable<ModuleDetail>;
  topic(module: string, topic: string): Observable<TopicDetail>;
  bookmark(briefId: string, on: boolean): Observable<void>;
  feedback(briefId: string, helpful: boolean | null): Observable<void>;
  quiz(module: string, topic: string): Observable<Quiz>;
  saveDraft(quizId: string, answers: Readonly<Record<string, string>>): Observable<void>;
  submit(quizId: string, answers: Readonly<Record<string, string>>): Observable<QuizResult>;
}

@Service()
export class HttpLearnApi implements LearnApi {
  private readonly api = inject(ApiClient);

  module(slug: string): Observable<ModuleDetail> {
    return this.api.get(`learn/modules/${encodeURIComponent(slug)}`);
  }

  topic(module: string, topic: string): Observable<TopicDetail> {
    return this.api.get(
      `learn/modules/${encodeURIComponent(module)}/topics/${encodeURIComponent(topic)}`,
    );
  }

  bookmark(briefId: string, on: boolean): Observable<void> {
    return this.api.put(`learn/briefs/${encodeURIComponent(briefId)}/bookmark`, { on });
  }

  feedback(briefId: string, helpful: boolean | null): Observable<void> {
    return this.api.put(`learn/briefs/${encodeURIComponent(briefId)}/feedback`, { helpful });
  }

  quiz(module: string, topic: string): Observable<Quiz> {
    return this.api.get(
      `learn/modules/${encodeURIComponent(module)}/topics/${encodeURIComponent(topic)}/quiz`,
    );
  }

  saveDraft(quizId: string, answers: Readonly<Record<string, string>>): Observable<void> {
    return this.api.put(`learn/quizzes/${encodeURIComponent(quizId)}/draft`, { answers });
  }

  submit(quizId: string, answers: Readonly<Record<string, string>>): Observable<QuizResult> {
    return this.api.post(`learn/quizzes/${encodeURIComponent(quizId)}/submit`, { answers });
  }
}

export const LEARN_API = new InjectionToken<LearnApi>('LEARN_API', {
  factory: () => inject(HttpLearnApi),
});

/** Topic status for cards (Figma Topic Card: Mastered / N quiz left / not started). */
export function topicStatus(t: Pick<TopicSummary, 'quizzesDone' | 'quizzesTotal'>) {
  return t.quizzesDone >= t.quizzesTotal ? 'mastered' : t.quizzesDone > 0 ? 'in-progress' : 'new';
}
```

## `src/app/core/matches/matches.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

export type MatchKind = 'job' | 'learnership' | 'internship' | 'apprenticeship' | 'course';
/** Why it's on your list (Figma M1 filter chips). */
export type MatchStatus =
  'invited' | 'shortlisted' | 'recommended' | 'saved' | 'applied' | 'accepted';
/** Evidence behind a skill (A-108: Assessed / Verified count 1.0, Declared 0.8). */
export type Evidence = 'assessed' | 'verified' | 'declared' | 'level';

export interface MatchSkill {
  readonly name: string;
  readonly evidence: Evidence;
  /** e.g. "C2 / C1" for languages. */
  readonly note?: string;
}

export interface MatchJob {
  readonly id: string;
  readonly title: string;
  readonly employer: string;
  readonly kind: MatchKind;
  readonly location: string;
  readonly distanceKm: number | null;
  /** 0–100 */
  readonly match: number;
  readonly pay: string;
  readonly status: MatchStatus;
  /** ISO date-time of the interview slot when invited. */
  readonly interviewAt: string | null;
  readonly skills: readonly MatchSkill[];
  /** Power-up or gap, e.g. "Add Call centre systems proof → +3%". */
  readonly tip: string;
  readonly tipKind: 'boost' | 'gap';
  readonly industry: string;
}

export interface IndustryFit {
  readonly id: string;
  readonly name: string;
  readonly fit: number;
  readonly why: string;
  readonly roles: string;
  /** Next step to get in, e.g. "Short course: Retail NQF 2 (8 weeks)". */
  readonly path: string;
  /** The learner already holds this path. */
  readonly pathDone: boolean;
  readonly jobs: number;
}

export interface MatchBoard {
  readonly total: number;
  readonly shortlisted: number;
  readonly invites: number;
  readonly profileComplete: number;
  readonly visible: boolean;
  readonly jobs: readonly MatchJob[];
  readonly industries: readonly IndustryFit[];
  /** Career Compass summary, e.g. "Top interests: Social, Enterprising…". */
  readonly compass: { readonly takenOn: string; readonly summary: string } | null;
}

export interface MatchPreview {
  readonly count: number;
  readonly radiusKm: number;
  readonly top: readonly Pick<MatchJob, 'id' | 'title' | 'kind' | 'match'>[];
}

export interface VisibilitySettings {
  /** Employers on Skillmuni can find me (needed for matches). */
  readonly discoverable: boolean;
  /** Employers can invite me by email and WhatsApp. */
  readonly invites: boolean;
}

/** Matches and profile publishing (Figma Q8 Vibe Check 200:35577, My Matches §12). */
export interface MatchesApi {
  preview(): Observable<MatchPreview>;
  publish(settings: VisibilitySettings): Observable<void>;
  board(): Observable<MatchBoard>;
  apply(id: string): Observable<void>;
  save(id: string, saved: boolean): Observable<void>;
  acceptInvite(id: string): Observable<void>;
}

@Service()
export class HttpMatchesApi implements MatchesApi {
  private readonly api = inject(ApiClient);

  preview(): Observable<MatchPreview> {
    return this.api.get('me/matches/preview');
  }

  publish(settings: VisibilitySettings): Observable<void> {
    return this.api.post('me/profile/publish', settings);
  }

  board(): Observable<MatchBoard> {
    return this.api.get('me/matches');
  }

  save(id: string, saved: boolean): Observable<void> {
    return this.api.post(`me/matches/${id}/save`, { saved });
  }

  apply(id: string): Observable<void> {
    return this.api.post(`me/matches/${id}/apply`, {});
  }

  acceptInvite(id: string): Observable<void> {
    return this.api.post(`me/matches/${id}/accept`, {});
  }
}

export const MATCHES_API = new InjectionToken<MatchesApi>('MATCHES_API', {
  factory: () => inject(HttpMatchesApi),
});
```

## `src/app/core/muni/muni.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { IconName } from '../../shared/ui/icon/icons';

/** "For you right now" card (Figma Muni Chat v2 · D1 180:5917). */
export interface MuniSuggestion {
  readonly id: string;
  readonly icon: IconName;
  readonly tone: 'success' | 'accent' | 'brand';
  readonly title: string;
  readonly detail: string;
  readonly cta: string;
  /** App route the CTA opens, or an intent to ask Muni. */
  readonly link?: string;
  readonly intent?: string;
}

/** "Or pick a vibe" topic tile — opens its own options as chips. */
export interface MuniTopic {
  readonly id: 'jobs' | 'learn' | 'assess' | 'me';
  readonly icon: IconName;
  readonly label: string;
  readonly options: number;
}

export interface MuniWelcome {
  readonly firstName: string;
  readonly intro: string;
  readonly suggestions: readonly MuniSuggestion[];
  readonly topics: readonly MuniTopic[];
  /** Launcher badge + one nudge per session. */
  readonly unread: number;
  readonly nudge: { readonly title: string; readonly body: string } | null;
}

/** Profile card: private fields arrive masked; the eye asks the server for the real value. */
export interface MuniProfileCard {
  readonly kind: 'profile';
  readonly initials: string;
  readonly name: string;
  readonly subtitle: string;
  readonly fields: readonly {
    readonly key: 'email' | 'mobile' | 'since';
    readonly label: string;
    readonly value: string;
    readonly masked: boolean;
  }[];
  readonly completeness: number;
  readonly nextStep: string;
}

export interface MuniStatsCard {
  readonly kind: 'stats';
  readonly items: readonly {
    readonly label: string;
    readonly value: string;
    readonly icon: IconName;
  }[];
  readonly link: { readonly label: string; readonly url: string };
}

export interface MuniListCard {
  readonly kind: 'list';
  readonly items: readonly {
    readonly title: string;
    readonly detail: string;
    readonly tag?: string;
    readonly url: string;
  }[];
}

export type MuniBlock =
  { readonly kind: 'text'; readonly text: string } | MuniProfileCard | MuniStatsCard | MuniListCard;

export interface MuniReply {
  readonly blocks: readonly MuniBlock[];
  /** Up to 3 follow-ups, each answerable in one tap. */
  readonly chips: readonly string[];
}

export interface MuniHistoryItem {
  readonly id: string;
  readonly title: string;
  readonly when: string;
  readonly messages: readonly { readonly from: 'me' | 'muni'; readonly text: string }[];
}

export interface MuniApi {
  welcome(): Observable<MuniWelcome>;
  /** `intent` is a topic id or chip text; free text goes in `text`. */
  ask(input: { readonly text: string; readonly intent?: string }): Observable<MuniReply>;
  reveal(field: 'email' | 'mobile'): Observable<{ readonly value: string }>;
  rate(messageId: string, helpful: boolean | null): Observable<void>;
  history(): Observable<readonly MuniHistoryItem[]>;
}

@Service()
export class HttpMuniApi implements MuniApi {
  private readonly api = inject(ApiClient);

  welcome(): Observable<MuniWelcome> {
    return this.api.get('muni/welcome');
  }

  ask(input: { readonly text: string; readonly intent?: string }): Observable<MuniReply> {
    return this.api.post('muni/messages', input);
  }

  reveal(field: 'email' | 'mobile'): Observable<{ readonly value: string }> {
    return this.api.post('muni/reveal', { field });
  }

  rate(messageId: string, helpful: boolean | null): Observable<void> {
    return this.api.put(`muni/messages/${encodeURIComponent(messageId)}/rating`, { helpful });
  }

  history(): Observable<readonly MuniHistoryItem[]> {
    return this.api.get('muni/history');
  }
}

export const MUNI_API = new InjectionToken<MuniApi>('MUNI_API', {
  factory: () => inject(HttpMuniApi),
});
```

## `src/app/core/profile/profile.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { IconName } from '../../shared/ui/icon/icons';

/** Badge rarity = how many learners own it (Figma Badge Medal 169:4668). */
export type BadgeRarity = 'common' | 'rare' | 'epic' | 'legendary';

export interface ShowcaseBadge {
  readonly id: string;
  readonly name: string;
  readonly icon: IconName;
  /** Artwork for the badge (quest badges use the quest's 3D illustration, corrections #24). */
  readonly image?: string | null;
  readonly rarity: BadgeRarity;
}

export type StrengthTaskId = 'contact' | 'name' | 'photo' | 'skills' | 'drivers';

export interface StrengthTask {
  readonly id: StrengthTaskId;
  readonly label: string;
  readonly xp: number;
  readonly done: boolean;
}

export type PrivateField = 'mobile' | 'email' | 'gender' | 'dob' | 'id';

/** Contact details change only after a one-time code to the new address (A-273). */
export type ContactKind = 'mobile' | 'email';

/** Private details always arrive masked; the eye asks the server for the real value. */
export interface MaskedField {
  readonly key: PrivateField;
  readonly label: string;
  readonly masked: string;
  /** Nothing to reveal yet (e.g. date of birth never given). */
  readonly empty: boolean;
}

/** Profile v2 · Player ID (Figma 186:6669). */
export interface PlayerProfile {
  readonly playerId: string;
  readonly name: string;
  readonly initials: string;
  readonly title: string;
  /** ISO date */
  readonly joined: string;
  readonly photoUrl: string | null;
  readonly level: number;
  readonly xp: number;
  /** XP total where the next level starts. */
  readonly nextLevelXp: number;
  /** XP total where the current level started. */
  readonly levelStartXp: number;
  readonly batchRank: number | null;
  readonly bestSkill: string | null;
  readonly archetype: {
    readonly name: string;
    readonly blurb: string;
    /** Corrections #21: shown as a showcase card — the archetype's emoji and top 3 drivers. */
    readonly emoji?: string;
    readonly traits?: readonly string[];
  } | null;
  readonly stats: {
    readonly streakDays: number;
    readonly badges: number;
    readonly learningHours: number;
  };
  readonly strength: { readonly percent: number; readonly tasks: readonly StrengthTask[] };
  readonly showcase: {
    readonly pinned: readonly ShowcaseBadge[];
    readonly next: {
      readonly name: string;
      readonly icon: IconName;
      /** Artwork for the badge (quest badges use the quest's 3D illustration, corrections #24). */
      readonly image?: string | null;
      readonly percent: number;
    } | null;
  };
  readonly privateFields: readonly MaskedField[];
  readonly skills: readonly string[];
}

export type Gender = 'female' | 'male' | 'non-binary' | 'rather-not';

export interface ProfileEdit {
  readonly skills: readonly string[];
  readonly gender: Gender;
  /** ISO date or '' */
  readonly dob: string;
}

/** Saving a change that completes a strength task returns the XP it earned. */
export interface ProfileSaved {
  readonly profile: PlayerProfile;
  readonly xpEarned: number;
}

export interface ProfileApi {
  profile(): Observable<PlayerProfile>;
  reveal(field: PrivateField): Observable<{ readonly value: string }>;
  /** Current editable values (unmasked: only fetched when the learner opens Edit). */
  editable(): Observable<
    ProfileEdit & {
      readonly skillOptions: readonly string[];
      readonly mobile: string;
      readonly email: string;
    }
  >;
  save(edit: ProfileEdit): Observable<ProfileSaved>;
  /** Send a code to the new mobile or email; resolves with where it went. */
  sendContactCode(kind: ContactKind, value: string): Observable<{ readonly sentTo: string }>;
  /** Check the code; the new value is saved only when it matches. */
  verifyContact(kind: ContactKind, value: string, code: string): Observable<void>;
  uploadPhoto(file: File): Observable<ProfileSaved>;
}

@Service()
export class HttpProfileApi implements ProfileApi {
  private readonly api = inject(ApiClient);

  profile(): Observable<PlayerProfile> {
    return this.api.get('profile');
  }

  reveal(field: PrivateField): Observable<{ readonly value: string }> {
    return this.api.post('profile/reveal', { field });
  }

  editable(): Observable<
    ProfileEdit & {
      readonly skillOptions: readonly string[];
      readonly mobile: string;
      readonly email: string;
    }
  > {
    return this.api.get('profile/edit');
  }

  sendContactCode(kind: ContactKind, value: string): Observable<{ readonly sentTo: string }> {
    return this.api.post('profile/contact/code', { kind, value });
  }

  verifyContact(kind: ContactKind, value: string, code: string): Observable<void> {
    return this.api.post('profile/contact/verify', { kind, value, code });
  }

  save(edit: ProfileEdit): Observable<ProfileSaved> {
    return this.api.put('profile', edit);
  }

  uploadPhoto(file: File): Observable<ProfileSaved> {
    const body = new FormData();
    body.append('photo', file);
    return this.api.post('profile/photo', body);
  }
}

export const PROFILE_API = new InjectionToken<ProfileApi>('PROFILE_API', {
  factory: () => inject(HttpProfileApi),
});
```

## `src/app/core/programme/programme.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { LearnerProgress } from '../progress/progress.api';

/** A programme / project unlocked by a code (Figma section 13, P1–P5). */
export interface Programme {
  readonly code: string;
  readonly name: string;
  /** e.g. "NSQF Level 4 · Aarambh Foundation × Skill India" */
  readonly partner: string;
  readonly centre: string;
  /** ISO date */
  readonly batchStarts: string;
  readonly duration: string;
  /** Empty when the programme pays no stipend (then no bank section). */
  readonly stipend: string;
  /** Marks rule, e.g. Class 12 with 50% in English and Maths. */
  readonly marksFrom: '10' | '12';
  readonly minEnglish: number;
  readonly minMaths: number;
  readonly eligibility: string;
}

export interface BankBranch {
  readonly bank: string;
  readonly branch: string;
}

export type Proficiency = 'basic' | 'okay' | 'good' | 'pro';

export interface ProgrammeSubmission {
  readonly code: string;
  readonly marksFrom: '10' | '12';
  readonly english: string;
  readonly maths: string;
  readonly proficiency: Readonly<Record<string, Proficiency>>;
  readonly bank: {
    readonly holder: string;
    readonly type: 'savings' | 'current' | 'cheque';
    readonly account: string;
    /** India IFSC, or the South African universal branch code. */
    readonly routing: string;
    readonly upi: string;
  } | null;
}

export interface ProgrammeResult {
  readonly xpEarned: number;
  readonly progress: LearnerProgress;
  /** Last 2 digits of the account the ₹1 / R1 check was sent to. */
  readonly accountTail: string;
}

/**
 * Programme code bonus quest. `check` 404s for unknown codes and 429s after 5 wrong tries
 * (30-minute cool-down, Figma P4). `ifsc` looks up bank + branch (India).
 */
export interface ProgrammeApi {
  check(code: string): Observable<Programme>;
  ifsc(code: string): Observable<BankBranch>;
  submit(body: ProgrammeSubmission): Observable<ProgrammeResult>;
}

@Service()
export class HttpProgrammeApi implements ProgrammeApi {
  private readonly api = inject(ApiClient);

  check(code: string): Observable<Programme> {
    return this.api.get(`programmes/${encodeURIComponent(code)}`);
  }

  ifsc(code: string): Observable<BankBranch> {
    return this.api.get(`banks/ifsc/${encodeURIComponent(code)}`);
  }

  submit(body: ProgrammeSubmission): Observable<ProgrammeResult> {
    return this.api.post('me/programme', body);
  }
}

export const PROGRAMME_API = new InjectionToken<ProgrammeApi>('PROGRAMME_API', {
  factory: () => inject(HttpProgrammeApi),
});
```

## `src/app/core/progress/progress.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

export interface LearnerProgress {
  /** Highest completed quest id (0 = none). */
  readonly completed: number;
  /** Parts done in the current quest. */
  readonly partsDone: number;
  readonly xp: number;
  readonly badges: number;
  readonly level: number;
  readonly rank: string;
  /** XP needed for the next level. */
  readonly target: number;
}

export interface ProgressApi {
  get(): Observable<LearnerProgress>;
}

@Service()
export class HttpProgressApi implements ProgressApi {
  private readonly api = inject(ApiClient);

  get(): Observable<LearnerProgress> {
    return this.api.get('me/progress');
  }
}

export const PROGRESS_API = new InjectionToken<ProgressApi>('PROGRESS_API', {
  factory: () => inject(HttpProgressApi),
});
```

## `src/app/core/quests/quest.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { LearnerProgress } from '../progress/progress.api';

/** One quest's saved answers, keyed by field. Values are plain JSON. */
export type QuestAnswers = Readonly<Record<string, unknown>>;

export interface QuestCompletion {
  readonly xpEarned: number;
  readonly progress: LearnerProgress;
}

export interface QuestApi {
  answers(slug: string): Observable<QuestAnswers>;
  /** Saving goes through the OfflineQueue with this path (PUT, idempotent per part). */
  partPath(slug: string, part: number): string;
  complete(slug: string): Observable<QuestCompletion>;
}

@Service()
export class HttpQuestApi implements QuestApi {
  private readonly api = inject(ApiClient);

  answers(slug: string): Observable<QuestAnswers> {
    return this.api.get(`me/quests/${slug}/answers`);
  }

  partPath(slug: string, part: number): string {
    return `me/quests/${slug}/parts/${part}`;
  }

  complete(slug: string): Observable<QuestCompletion> {
    return this.api.post(`me/quests/${slug}/complete`, {});
  }
}

export const QUEST_API = new InjectionToken<QuestApi>('QUEST_API', {
  factory: () => inject(HttpQuestApi),
});
```

## `src/app/core/skills/esco.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';

/**
 * ESCO — the European Skills, Competences, Qualifications and Occupations taxonomy (A-245).
 * Our server proxies ESCO's public search (`/search?type=skill&text=…&language=…`), keeps the
 * concept URI and preferred label, and maps each concept to one of our six deck categories (from
 * its ESCO skill group). Skill decks store the URI, so matching and analytics use ESCO codes.
 */
export type EscoSkillType = 'skill' | 'knowledge' | 'transversal';
export type EscoCategory = 'tech' | 'data' | 'creative' | 'business' | 'people' | 'trades';

export interface EscoSkill {
  /** ESCO concept URI, e.g. http://data.europa.eu/esco/skill/… */
  readonly uri: string;
  /** ESCO preferred label in the requested language. */
  readonly label: string;
  readonly type: EscoSkillType;
  readonly category: EscoCategory;
  /** ESCO category name, e.g. "Programming languages" (A-285). */
  readonly escoCategory?: string;
  /** ESCO alternative labels, used for "matched on …". */
  readonly alt?: readonly string[];
}

export interface EscoSearch {
  readonly total: number;
  readonly results: readonly EscoSkill[];
}

export interface EscoApi {
  search(text: string, language: string, limit?: number): Observable<EscoSearch>;
}

@Service()
export class HttpEscoApi implements EscoApi {
  private readonly api = inject(ApiClient);

  search(text: string, language: string, limit = 24): Observable<EscoSearch> {
    return this.api.get('skills/esco', { params: { q: text, lang: language, limit } });
  }
}

export const ESCO_API = new InjectionToken<EscoApi>('ESCO_API', {
  providedIn: 'root',
  factory: () => inject(HttpEscoApi),
});

/** "communicate with customers" → "Communicate with customers" (ESCO labels are lower case). */
export function escoDisplay(label: string): string {
  return label.charAt(0).toUpperCase() + label.slice(1);
}
```

## `src/app/core/stats/stats.api.ts`

```ts
import { InjectionToken, Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api/api-client';
import { StreakDay } from '../arena/arena.api';
import { BadgeRarity } from '../profile/profile.api';
import { IconName } from '../../shared/ui/icon/icons';

/* ---------- 1 · Rewards — Player HQ (Figma 161:4281) ---------- */

export interface DailyQuest {
  readonly id: string;
  readonly icon: IconName;
  /** Artwork for the badge (quest badges use the quest's 3D illustration, corrections #24). */
  readonly image?: string | null;
  readonly title: string;
  /** "98.86% done", "6 / 15 min" */
  readonly progressLabel: string;
  readonly percent: number;
  /** XP for clearing it. Coins aren't earned for now (A-302). */
  readonly xp: number;
  readonly done: boolean;
  readonly link: string;
}

export interface BadgeProgress {
  readonly id: string;
  readonly name: string;
  readonly icon: IconName;
  /** Artwork for the badge (quest badges use the quest's 3D illustration, corrections #24). */
  readonly image?: string | null;
  /** "98.86%", "2 / 3 days" */
  readonly progressLabel: string;
  readonly percent: number;
}

export interface ShowcaseSlot {
  readonly id: string;
  readonly name: string;
  readonly icon: IconName;
  /** Artwork for the badge (quest badges use the quest's 3D illustration, corrections #24). */
  readonly image?: string | null;
  readonly rarity: BadgeRarity;
}

export interface RewardsDashboard {
  readonly player: {
    readonly name: string;
    readonly initials: string;
    readonly title: string;
    readonly photoUrl: string | null;
    readonly level: number;
    readonly xp: number;
    readonly nextLevelXp: number;
    readonly batchRank: number | null;
  };
  readonly streak: { readonly days: number; readonly week: readonly StreakDay[] };
  readonly wallet: {
    readonly xp: number;
    readonly coins: number;
    readonly coinsEarned: number;
    readonly coinsSpent: number;
    readonly credits: number;
  };
  /** Three daily quests, reset at midnight. */
  readonly quests: { readonly resetsAt: string; readonly items: readonly DailyQuest[] };
  /** Always 6 slots: 3 pinned (null = empty) + 3 closest to unlock (note 177:7597). */
  readonly badges: {
    readonly earned: number;
    readonly total: number;
    readonly showcase: readonly (ShowcaseSlot | null)[];
    readonly upNext: readonly BadgeProgress[];
  };
  readonly rank: {
    readonly rank: number;
    readonly gapXp: number;
    readonly nextName: string;
  } | null;
  readonly learningHours: number;
}

/* ---------- 2 · Learning — Quest Log (Figma 161:4359) ---------- */

export type CourseStatus = 'in-progress' | 'not-started' | 'done';
export type CourseTone = 'arcane' | 'ember' | 'green' | 'amber' | 'pink' | 'blue';

export interface LearningCourse {
  readonly id: string;
  readonly title: string;
  readonly tone: CourseTone;
  readonly percent: number;
  readonly status: CourseStatus;
  readonly kind: string;
  readonly xpOnFinish: number;
  /** Days left; null = no deadline. */
  readonly deadlineDays: number | null;
  readonly timeEstHours: number | null;
  readonly cpdHours: number | null;
  readonly summary: string;
  readonly link: string;
}

export interface LearningDashboard {
  /** Sorted by the server: closest to done first. */
  readonly courses: readonly LearningCourse[];
  readonly currentId: string | null;
  /** Finish-line nudge (e.g. a module at 98.86%). */
  readonly nudge: string | null;
}

/* ---------- 3 · Assessments — Skill Stats (Figma 161:4439 · empty 161:4514) ---------- */

export type SkillTier = 'beginner' | 'intermediate' | 'advanced' | 'expert';

export interface AssessmentResult {
  readonly id: string;
  readonly title: string;
  readonly subtitle: string;
  readonly tier: SkillTier;
  /** 0–100 */
  readonly score: number;
  readonly correct: number;
  readonly total: number;
  /** ISO date */
  readonly completed: string;
  readonly retakeUrl: string;
}

export interface AssessmentsDashboard {
  readonly results: readonly AssessmentResult[];
  readonly available: number;
  readonly firstCardXp: number;
  readonly recommended: { readonly title: string; readonly url: string; readonly why: string };
}

/* ---------- My Badges (Figma 170:4660 · sheet 175:5480) ---------- */

export type BadgeCategory =
  'quests' | 'streaks' | 'learning' | 'quizzes' | 'assessments' | 'social' | 'special';
export type BadgeState = 'earned' | 'progress' | 'locked' | 'secret';

export interface Badge {
  readonly id: string;
  readonly name: string;
  readonly icon: IconName;
  /** Artwork for the badge (quest badges use the quest's 3D illustration, corrections #24). */
  readonly image?: string | null;
  readonly category: BadgeCategory;
  readonly rarity: BadgeRarity;
  readonly state: BadgeState;
  /** ISO date when earned. */
  readonly earnedOn: string | null;
  /** "3 / 5 Knooks"; null when there's nothing to count. */
  readonly progressLabel: string | null;
  readonly percent: number;
  readonly howTo: string;
  /** % of the batch that owns it (drives rarity). */
  readonly ownedPct: number;
  readonly cta: { readonly label: string; readonly url: string } | null;
  /** Tiered badges (Streak Master 3 · 7 · 30): tiers reached of total. */
  readonly tiers: { readonly reached: number; readonly total: number } | null;
  readonly pinned: boolean;
}

export interface BadgeCatalogue {
  readonly earned: number;
  readonly total: number;
  readonly badges: readonly Badge[];
}

export interface StatsApi {
  rewards(): Observable<RewardsDashboard>;
  learning(): Observable<LearningDashboard>;
  assessments(): Observable<AssessmentsDashboard>;
  badges(): Observable<BadgeCatalogue>;
  /** Up to 3 earned badge ids, in showcase order. */
  setShowcase(ids: readonly string[]): Observable<BadgeCatalogue>;
}

@Service()
export class HttpStatsApi implements StatsApi {
  private readonly api = inject(ApiClient);

  rewards(): Observable<RewardsDashboard> {
    return this.api.get('stats/rewards');
  }

  learning(): Observable<LearningDashboard> {
    return this.api.get('stats/learning');
  }

  assessments(): Observable<AssessmentsDashboard> {
    return this.api.get('stats/assessments');
  }

  badges(): Observable<BadgeCatalogue> {
    return this.api.get('badges');
  }

  setShowcase(ids: readonly string[]): Observable<BadgeCatalogue> {
    return this.api.put('badges/showcase', { ids });
  }
}

export const STATS_API = new InjectionToken<StatsApi>('STATS_API', {
  factory: () => inject(HttpStatsApi),
});

/** Placeholder tier floors (note 166:4703 "to confirm"): Expert 90 · Advanced 80 · Intermediate 60. */
export const TIER_FLOORS: Readonly<Record<SkillTier, number>> = {
  beginner: 0,
  intermediate: 60,
  advanced: 80,
  expert: 90,
};
export const TIER_ORDER: readonly SkillTier[] = ['beginner', 'intermediate', 'advanced', 'expert'];
```
