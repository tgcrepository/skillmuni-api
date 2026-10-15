'use strict';
// Muni chat, Platform (geo, tenant, notifications), Identity beyond sign-in (sessions, codes, consents, privacy,
// institute claims) and the gateway's edge behaviour. Drives the API as the Angular app does:
// features/muni-chat (muni.api.ts), core/contact/contact.api.ts, features/claim (claim.api.ts), auth.api.ts.
const crypto = require('crypto');
const { Client } = require('../lib');

const sleep = ms => new Promise(res => setTimeout(res, ms));
const uuid = () => crypto.randomUUID();

/** Sample data the mocks invent; a real reply must never show it (BRD: Muni speaks about this learner only). */
const INVENTED = /98\.86|Teleperformance|Reliance Trends|Customer Support Exec|Retail Associate|Concentrix|\bYash\b|The Connector|2 jobs match|#3 this week|41 XP/;
/** Muni's two "I don't know" replies. */
const FALLBACK = /I don’t have that one yet|I’m not sure about that one yet/;

module.exports = async (ctx, j) => {
  const r = ctx.report;
  const fail = (call, msg) => r.rows.push({ journey: j, call, status: 0, ms: 0, problems: [msg], note: 'check' });
  const check = (ok, call, msg) => { if (!ok) fail(call, msg); return ok; };
  /** Re-run `fn` until `ok(result)` or the time is up (events are asynchronous). */
  async function poll(fn, ok, ms = 45000) {
    const until = Date.now() + ms;
    let last;
    for (;;) {
      last = await fn();
      if (ok(last) || Date.now() > until) return last;
      await sleep(1500);
    }
  }
  /** fetch without the harness client: raw headers, any path, any origin. */
  async function raw(method, path, headers = {}, body) {
    const res = await fetch(`${ctx.base}${path}`, { method, headers, body, redirect: 'manual' });
    const text = await res.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
    return { status: res.status, headers: res.headers, body: json, text, contentType: res.headers.get('content-type') || '' };
  }
  const isProblem = (res, code) => res.contentType.includes('application/problem+json')
    && typeof res.body?.message === 'string' && Array.isArray(res.body?.errors?.code) && (!code || res.body.errors.code[0] === code);
  const codeOf = res => res.body?.errors?.code?.[0];

  // ---- JWT minting (dev signing key), identical to Identity's JwtTokenIssuer ------------------------------------
  const keyB64 = process.env.AUTH_PRIVATE_KEY_B64 || '';
  const b64url = buf => Buffer.from(buf).toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
  function mint({ sub, roles, name = 'Admin', tenant = 'skillmuni-in', market = 'IN', lifetime = 600 }) {
    const pem = Buffer.from(keyB64, 'base64').toString('utf8');
    const now = Math.floor(Date.now() / 1000);
    const header = { alg: 'RS256', kid: 'identity-1', typ: 'JWT' };
    const payload = {
      iss: 'skillmuni-identity', aud: 'skillmuni-api', iat: now, nbf: now, exp: now + lifetime,
      sub, sid: uuid(), tid: tenant, mkt: market, name, role: roles,
    };
    const input = `${b64url(JSON.stringify(header))}.${b64url(JSON.stringify(payload))}`;
    const sig = crypto.sign('RSA-SHA256', Buffer.from(input), pem);
    return `${input}.${b64url(sig)}`;
  }
  function clientWithToken(name, token) {
    const c = new Client(ctx.base, name);
    c.cookies.sm_at = token;
    return c;
  }

  // ================================================================================================================
  // Platform: geo (Q2 Hit Me Up) and tenant config — anonymous.
  // ================================================================================================================
  const anon = ctx.client('anon-platform');
  const postal = [
    ['IN', '560038', { region: 'Karnataka', district: 'Bengaluru Urban', city: 'Bengaluru', area: 'Indiranagar', areas: 7 }],
    ['IN', '400001', { region: 'Maharashtra', district: 'Mumbai', city: 'Mumbai', area: 'Fort', areas: 3 }],
    ['ZA', '2194', { region: 'Gauteng', district: 'City of Johannesburg', city: 'Johannesburg', area: 'Randburg (Ferndale)', areas: 5 }],
    ['ZA', '2001', { region: 'Gauteng', district: 'City of Johannesburg', city: 'Johannesburg', area: 'Braamfontein', areas: 2 }],
  ];
  for (const [m, code, want] of postal) {
    const p = `geo/${m}/postal/${code}`;
    const res = r.record(j, 'GET', p, await anon.call('GET', p), 200);
    if (res.status === 200) {
      const b = res.body;
      check(b.region === want.region && b.district === want.district && b.city === want.city, `GET ${p}`,
        `expected ${want.region} / ${want.district} / ${want.city}, got ${b.region} / ${b.district} / ${b.city}`);
      check(b.areas?.length >= want.areas && b.areas.some(a => a.name === want.area && a.sub === want.district), `GET ${p}`,
        `areas should include ${want.area} (sub ${want.district}) and at least ${want.areas} areas, got ${JSON.stringify(b.areas).slice(0, 160)}`);
    }
  }
  const nf = r.record(j, 'GET', 'geo/IN/postal/999999', await anon.call('GET', 'geo/IN/postal/999999'), 404, 'unknown PIN → pick the state');
  check(codeOf(nf) === 'postal-not-found', 'GET geo/IN/postal/999999', `code ${codeOf(nf)}, expected postal-not-found`);
  const inRegions = r.record(j, 'GET', 'geo/IN/regions', await anon.call('GET', 'geo/IN/regions'), 200);
  for (const s of ['Andhra Pradesh', 'Delhi', 'Karnataka', 'Maharashtra', 'Puducherry', 'Tamil Nadu', 'West Bengal', 'Jammu and Kashmir']) {
    check(Array.isArray(inRegions.body) && inRegions.body.includes(s), 'GET geo/IN/regions', `missing ${s}`);
  }
  const zaRegions = r.record(j, 'GET', 'geo/ZA/regions', await anon.call('GET', 'geo/ZA/regions'), 200);
  const provinces = ['Eastern Cape', 'Free State', 'Gauteng', 'KwaZulu-Natal', 'Limpopo', 'Mpumalanga', 'North West', 'Northern Cape', 'Western Cape'];
  check(JSON.stringify(zaRegions.body) === JSON.stringify(provinces), 'GET geo/ZA/regions', `expected the 9 provinces A–Z, got ${JSON.stringify(zaRegions.body)}`);
  const xx = r.record(j, 'GET', 'geo/XX/regions', await anon.call('GET', 'geo/XX/regions'), 200, 'unknown market → [] as the mock');
  check(Array.isArray(xx.body) && xx.body.length === 0, 'GET geo/XX/regions', `expected [], got ${xx.text.slice(0, 80)}`);

  const theme = r.record(j, 'GET', 'tenant/theme', await anon.call('GET', 'tenant/theme'), 200, 'extra: TenantTheme (anonymous)');
  check(theme.body?.id === 'skillmuni-in' && typeof theme.body?.name === 'string' && theme.body.name.length > 0, 'GET tenant/theme',
    `expected { id: skillmuni-in, name }, got ${theme.text.slice(0, 120)}`);
  for (const k of ['brand', 'accent']) {
    if (theme.body?.[k]) check(Object.values(theme.body[k]).every(v => /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i.test(v)), 'GET tenant/theme', `${k} has a non-hex colour`);
  }
  const mIn = r.record(j, 'GET', 'tenant/market', await anon.call('GET', 'tenant/market'), 200, 'extra: MarketConfig, tenant default');
  check(mIn.body?.code === 'IN' && mIn.body?.dialCode === '+91' && mIn.body?.mobile?.digits === 10 && mIn.body?.timeZone === 'Asia/Kolkata'
    && mIn.body?.idVerification === 'digilocker' && Array.isArray(mIn.body?.languages) && typeof mIn.body?.postal?.pattern === 'string',
  'GET tenant/market', `IN config wrong: ${mIn.text.slice(0, 200)}`);
  if (typeof mIn.body?.mobile?.pattern === 'string') {
    check(new RegExp(mIn.body.mobile.pattern).test('9876543210') && new RegExp(mIn.body.postal.pattern).test('560038'), 'GET tenant/market', 'IN patterns reject 9876543210 / 560038');
  }
  const mZa = r.record(j, 'GET', 'tenant/market?code=ZA', await anon.call('GET', 'tenant/market?code=ZA'), 200, 'extra: MarketConfig ZA');
  check(mZa.body?.code === 'ZA' && mZa.body?.dialCode === '+27' && mZa.body?.mobile?.digits === 9 && mZa.body?.timeZone === 'Africa/Johannesburg'
    && mZa.body?.asksRace === true, 'GET tenant/market?code=ZA', `ZA config wrong: ${mZa.text.slice(0, 200)}`);
  const mXx = r.record(j, 'GET', 'tenant/market?code=XX', await anon.call('GET', 'tenant/market?code=XX'), 404, 'extra: unknown market');
  check(codeOf(mXx) === 'unknown-market', 'GET tenant/market?code=XX', `code ${codeOf(mXx)}`);

  // ================================================================================================================
  // Muni chat (Figma Muni Chat v2): a brand-new learner.
  // ================================================================================================================
  const meera = await ctx.newLearner(j, 'Meera', 'Iyer');
  let welcome = r.record(j, 'GET', 'muni/welcome', await meera.call('GET', 'muni/welcome'), 200);
  const w = welcome.body || {};
  check(w.firstName === 'Meera', 'GET muni/welcome', `firstName ${w.firstName}, expected Meera`);
  check(/^Level 1 · \S.*, 0 XP\./.test(w.intro || ''), 'GET muni/welcome', `intro should show Level 1 and 0 XP for a new learner: ${w.intro}`);
  check(w.suggestions?.length === 3 && w.suggestions[0].id === 'drivers', 'GET muni/welcome', `new learner should get discovery suggestions: ${JSON.stringify(w.suggestions?.map(s => s.id))}`);
  check(JSON.stringify(w.topics?.map(t => t.id)) === '["jobs","learn","assess","me"]', 'GET muni/welcome', 'topics should be jobs, learn, assess, me');
  check(!INVENTED.test(welcome.text), 'GET muni/welcome', 'welcome shows invented sample data');
  check(w.unread === 0 && w.nudge === null, 'GET muni/welcome', `no nudge source yet: unread ${w.unread}, nudge ${JSON.stringify(w.nudge)}`);

  // Earn real XP (monthly feedback +5, BR-114) so Muni has something real to report.
  r.record(j, 'POST', 'me/feedback', await meera.call('POST', 'me/feedback', { mood: 5, tags: ['Fun'], comment: '', screen: '/home' }), 200);
  const prog = (await poll(() => meera.call('GET', 'me/progress'), x => x.body?.xp >= 5)).body || {};
  r.record(j, 'GET', 'me/progress', await meera.call('GET', 'me/progress'), 200);
  check(prog.xp === 5, 'GET me/progress', `xp ${prog.xp} after feedback, expected 5`);
  welcome = await poll(() => meera.call('GET', 'muni/welcome'), x => (x.body?.intro || '').includes(`, ${prog.xp} XP.`));
  check((welcome.body?.intro || '').startsWith(`Level ${prog.level} · ${prog.rank}, ${prog.xp} XP.`), 'GET muni/welcome',
    `intro should use the learner's real level/rank/XP (${prog.level} · ${prog.rank}, ${prog.xp}): ${welcome.body?.intro}`);

  // Every way the app asks: topic tiles (intent = tile id), "/" shortcuts, suggestion intents, greetings, free text,
  // the Launchpad sectors dialog, the CV attachment — then every chip any reply offers, until none are new.
  const queue = [
    ...w.topics?.map(t => ({ text: t.label, intent: t.id, from: 'tile' })) ?? [],
    ...['Jobs for me', 'Continue my quest', 'My XP & rank', 'Show my profile', 'My career archetype'].map(t => ({ text: t, from: 'shortcut' })),
    ...(w.suggestions || []).filter(s => s.intent).map(s => ({ text: s.intent, from: 'suggestion' })),
    { text: 'hi', from: 'greeting' }, { text: 'Namaste', from: 'greeting' },
    { text: 'how do i get more xp', from: 'free' }, { text: 'when is the next hire fest?', from: 'free' },
    { text: 'is my aadhaar safe here', from: 'free' }, { text: 'what are badges', from: 'free' },
    { text: 'I searched for “retail”. Which sectors fit my skills?', from: 'sectors' },
    { text: '📎 my-cv.pdf', intent: 'cv', from: 'cv' },
    { text: 'zxqv plorp', from: 'nonsense' },
  ];
  const seen = new Set();
  const replies = {};
  let lastReplyId = null;
  let asked = 0;
  while (queue.length && asked < 80) {
    const q = queue.shift();
    const key = (q.intent ?? q.text).toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    asked++;
    const body = q.intent ? { text: q.text, intent: q.intent } : { text: q.text };
    const res = r.record(j, 'POST', 'muni/messages', await meera.call('POST', 'muni/messages', body), 200, `${q.from}: ${q.text}`);
    if (res.status !== 200) continue;
    const b = res.body;
    replies[key] = b;
    lastReplyId = b.id || lastReplyId;
    const call = `POST muni/messages "${q.text}"`;
    check(b.blocks?.length > 0, call, 'reply has no blocks');
    check(Array.isArray(b.chips) && b.chips.length <= 3, call, `≤ 3 chips, got ${b.chips?.length}`);
    check(!INVENTED.test(res.text), call, `reply shows invented sample data: ${res.text.slice(0, 200)}`);
    const firstText = b.blocks?.find(x => x.kind === 'text')?.text || '';
    if (q.from === 'nonsense') check(FALLBACK.test(firstText) && firstText.includes('Meera'), call, `nonsense should get the named fallback: ${firstText}`);
    else check(!FALLBACK.test(firstText), call, `"${q.text}" (${q.from}) got the fallback instead of an answer`);
    for (const c of b.chips || []) queue.push({ text: c, from: `chip of "${q.text}"` });
  }
  check(asked >= 30, 'muni chips', `only ${asked} distinct questions reached`);

  const xpReply = replies['my xp & rank'];
  const stats = xpReply?.blocks?.find(x => x.kind === 'stats');
  if (check(!!stats, 'POST muni/messages "My XP & rank"', 'no stats card')) {
    const v = Object.fromEntries(stats.items.map(i => [i.label, i.value]));
    check(v['Total XP'] === String(prog.xp), 'POST muni/messages "My XP & rank"', `Total XP ${v['Total XP']}, expected ${prog.xp}`);
    check(v.Level === `${prog.level} · ${prog.rank}`, 'POST muni/messages "My XP & rank"', `Level ${v.Level}, expected ${prog.level} · ${prog.rank}`);
    check(v['To next level'] === String(Math.max(0, prog.target - prog.xp)), 'POST muni/messages "My XP & rank"', `To next level ${v['To next level']}, expected ${prog.target - prog.xp}`);
    check(xpReply.blocks[0].text.includes('Meera'), 'POST muni/messages "My XP & rank"', 'should greet the learner by name');
  }
  const hi = replies.hi?.blocks?.[0]?.text || '';
  check(hi.includes('Meera') && hi.includes(`${prog.xp} XP`) && hi.includes(`Level ${prog.level} · ${prog.rank}`), 'POST muni/messages "hi"', `greeting should use real name/level/XP: ${hi}`);
  const arche = replies['my career archetype']?.blocks?.[0]?.text || '';
  check(/haven’t taken Career Drivers/.test(arche), 'POST muni/messages "My career archetype"', `new learner has no archetype: ${arche}`);
  const tile = replies.jobs;
  check(JSON.stringify(tile?.chips) === '["Jobs for me","Applications","Career Compass"]', 'POST muni/messages intent jobs', `tile chips ${JSON.stringify(tile?.chips)}`);

  const profile = replies['show my profile']?.blocks?.find(x => x.kind === 'profile');
  if (check(!!profile, 'POST muni/messages "Show my profile"', 'no profile card')) {
    check(profile.name === 'Meera Iyer' && profile.initials === 'MI', 'POST muni/messages "Show my profile"', `name ${profile.name} / ${profile.initials}`);
    check(profile.subtitle === `${prog.rank} · Lvl ${prog.level}`, 'POST muni/messages "Show my profile"', `subtitle ${profile.subtitle}`);
    const email = profile.fields.find(f => f.key === 'email');
    check(email?.masked === true && email.value === `${meera.email[0]}••••@example.com`, 'POST muni/messages "Show my profile"', `email field ${JSON.stringify(email)}`);
    const since = profile.fields.find(f => f.key === 'since');
    const month = new Date().toLocaleString('en-US', { month: 'short', year: 'numeric', timeZone: 'UTC' }).replace(',', '');
    check(since?.value === month, 'POST muni/messages "Show my profile"', `member since ${since?.value}, expected ${month}`);
  }
  const rv = r.record(j, 'POST', 'muni/reveal', await meera.call('POST', 'muni/reveal', { field: 'email' }), 200);
  check(rv.body?.value === meera.email, 'POST muni/reveal', `email ${rv.body?.value}, expected ${meera.email}`);
  check((rv.headers.get('cache-control') || '').includes('no-store'), 'POST muni/reveal', 'private value must be no-store');
  r.record(j, 'POST', 'muni/reveal', await meera.call('POST', 'muni/reveal', { field: 'mobile' }), 200, 'no mobile on file yet');
  r.record(j, 'POST', 'muni/reveal', await meera.call('POST', 'muni/reveal', { field: 'aadhaar' }), 422, 'only email | mobile');
  r.record(j, 'POST', 'muni/messages', await meera.call('POST', 'muni/messages', { text: '   ' }), 422, 'empty question');

  // 👍 👎 on a reply: the app sends its own message id ("m3"); null clears.
  for (const [id, helpful] of [[lastReplyId || 'm1', true], [lastReplyId || 'm1', false], [lastReplyId || 'm1', null], ['m3', true], ['m3', null]]) {
    r.record(j, 'PUT', `muni/messages/${id}/rating`, await meera.call('PUT', `muni/messages/${encodeURIComponent(id)}/rating`, { helpful }), 204);
  }
  r.record(j, 'PUT', 'muni/messages/{long}/rating', await meera.call('PUT', `muni/messages/${'x'.repeat(65)}/rating`, { helpful: true }), 422, 'id over 64 chars');

  const hist = r.record(j, 'GET', 'muni/history', await meera.call('GET', 'muni/history'), 200);
  if (check(Array.isArray(hist.body) && hist.body.length >= 1, 'GET muni/history', 'expected at least one conversation')) {
    const h = hist.body[0];
    check(h.when === 'Today', 'GET muni/history', `when ${h.when}, expected Today`);
    check(h.title === 'Jobs', 'GET muni/history', `title should be the first question (Jobs), got ${h.title}`);
    check(h.messages.some(m => m.from === 'me' && m.text === 'My XP & rank') && h.messages.some(m => m.from === 'muni'), 'GET muni/history', 'conversation lines missing');
    check(h.messages.length === asked * 2, 'GET muni/history', `${h.messages.length} lines for ${asked} questions`);
    check(!INVENTED.test(hist.text), 'GET muni/history', 'history shows invented sample data');
  }
  ctx.state.meera = meera;

  // ================================================================================================================
  // Platform: notifications and channel preferences (MISS-06).
  // ================================================================================================================
  const nl = r.record(j, 'GET', 'me/notifications', await meera.call('GET', 'me/notifications'), 200, 'extra: bell');
  check(Array.isArray(nl.body?.items) && nl.body.page === 1 && nl.body.pageSize === 20 && typeof nl.body.total === 'number' && typeof nl.body.unread === 'number',
    'GET me/notifications', `paged shape wrong: ${nl.text.slice(0, 160)}`);
  r.record(j, 'GET', 'me/notifications?page=1&pageSize=500', await meera.call('GET', 'me/notifications?page=1&pageSize=500'), [200, 400, 422], 'extra: pageSize capped');
  const prefs = r.record(j, 'GET', 'me/notification-preferences', await meera.call('GET', 'me/notification-preferences'), 200, 'extra');
  check(JSON.stringify(prefs.body) === JSON.stringify({ push: true, whatsApp: false, sms: false, email: true }), 'GET me/notification-preferences', `defaults ${prefs.text}`);
  const want = { push: false, whatsApp: true, sms: false, email: true };
  const put = r.record(j, 'PUT', 'me/notification-preferences', await meera.call('PUT', 'me/notification-preferences', want), 200, 'extra');
  check(JSON.stringify(put.body) === JSON.stringify(want), 'PUT me/notification-preferences', `saved ${put.text}`);
  const again = await meera.call('GET', 'me/notification-preferences');
  check(JSON.stringify(again.body) === JSON.stringify(want), 'GET me/notification-preferences', `after save ${again.text}`);
  r.record(j, 'PUT', 'me/notification-preferences', await meera.call('PUT', 'me/notification-preferences', { push: true }), 422, 'extra: all four required');
  const nr = r.record(j, 'PUT', 'me/notifications/{id}/read', await meera.call('PUT', `me/notifications/${uuid()}/read`, {}), 404, 'extra: unknown notification');
  check(codeOf(nr) === 'notification-not-found', 'PUT me/notifications/{id}/read', `code ${codeOf(nr)}`);
  r.record(j, 'GET', 'me/notifications', await anon.call('GET', 'me/notifications'), 401, 'extra: signed out');

  // ================================================================================================================
  // Identity: sessions, silent refresh, consents, export, logout.
  // ================================================================================================================
  const kabir = await ctx.newLearner(j, 'Kabir', 'Shah');
  const s1 = r.record(j, 'GET', 'me/sessions', await kabir.call('GET', 'me/sessions'), 200, 'extra: device sessions');
  check(s1.body?.total === 1 && s1.body.items?.[0]?.current === true && s1.body.page === 1, 'GET me/sessions', `one current session expected: ${s1.text.slice(0, 200)}`);

  const phone2 = new Client(ctx.base, 'kabir-phone');
  r.record(j, 'POST', 'auth/login', await phone2.call('POST', 'auth/login', { email: kabir.email, password: 'Skillmuni123' }), 200, 'second device');
  const s2 = r.record(j, 'GET', 'me/sessions', await kabir.call('GET', 'me/sessions?page=1&pageSize=1'), 200, 'extra: paged');
  check(s2.body?.total === 2 && s2.body.items?.length === 1 && s2.body.pageSize === 1, 'GET me/sessions?pageSize=1', `paging wrong: ${s2.text.slice(0, 200)}`);
  const all = await kabir.call('GET', 'me/sessions');
  const other = all.body?.items?.find(s => !s.current);
  if (check(!!other, 'GET me/sessions', 'second session missing')) {
    r.record(j, 'DELETE', `me/sessions/${other.id}`, await kabir.call('DELETE', `me/sessions/${other.id}`), 204, 'extra: sign out the other device');
    r.record(j, 'GET', 'session', await phone2.call('GET', 'session'), 401, 'revoked device');
  }
  r.record(j, 'DELETE', 'me/sessions/{unknown}', await kabir.call('DELETE', `me/sessions/${uuid()}`), 404, 'extra');

  // Explicit refresh (MISS-13): new access + rotated refresh cookie.
  const at0 = kabir.cookies.sm_at;
  const rt0 = kabir.cookies.sm_rt;
  check(!!at0 && !!rt0, 'session cookies', 'sign-in should set sm_at and sm_rt');
  await sleep(1100); // a new iat, so the new token differs
  const rf = r.record(j, 'POST', 'auth/refresh', await kabir.call('POST', 'auth/refresh', {}), 204, 'extra: explicit refresh');
  check(kabir.cookies.sm_at && kabir.cookies.sm_at !== at0 && kabir.cookies.sm_rt && kabir.cookies.sm_rt !== rt0, 'POST auth/refresh', 'cookies not rotated');
  const sc = (rf.headers.getSetCookie?.() || []).join(' | ');
  check(/sm_at=[^;]+;.*httponly/i.test(sc) && /secure/i.test(sc) && /samesite=lax/i.test(sc) && /sm_rt=[^;]+;.*path=\/api/i.test(sc), 'POST auth/refresh', `cookie flags: ${sc.slice(0, 300)}`);
  r.record(j, 'GET', 'session', await kabir.call('GET', 'session'), 200, 'after refresh');

  // Silent refresh through the gateway: the browser dropped the expired access cookie; the refresh cookie remains.
  const rt1 = kabir.cookies.sm_rt;
  delete kabir.cookies.sm_at;
  const silent = r.record(j, 'GET', 'session', await kabir.call('GET', 'session'), 200, 'gateway silent refresh (sm_at expired)');
  check(!!kabir.cookies.sm_at && kabir.cookies.sm_rt !== rt1, 'GET session (silent refresh)', `gateway should set new cookies: ${(silent.headers.getSetCookie?.() || []).join(' | ').slice(0, 200)}`);
  r.record(j, 'GET', 'session', await kabir.call('GET', 'session'), 200, 'keeps working');
  r.record(j, 'GET', 'muni/welcome', await kabir.call('GET', 'muni/welcome'), 200, 'other services accept the refreshed token');

  // Consents (MISS-07) and data export (MISS-08).
  const cpost = r.record(j, 'POST', 'me/consents', await kabir.call('POST', 'me/consents', { scope: 'marketing.whatsapp', policyVersion: '2026-10', granted: true }), 201, 'extra: consent');
  check(cpost.body?.scope === 'marketing.whatsapp' && cpost.body.granted === true && typeof cpost.body.law === 'string' && !!cpost.headers.get('location'), 'POST me/consents', `consent ${cpost.text.slice(0, 160)}`);
  r.record(j, 'POST', 'me/consents', await kabir.call('POST', 'me/consents', { scope: 'Bad Scope!', policyVersion: '2026-10', granted: true }), 422, 'extra: invalid scope');
  const clist = r.record(j, 'GET', 'me/consents', await kabir.call('GET', 'me/consents'), 200, 'extra');
  check(Array.isArray(clist.body?.items) && clist.body.items.some(c => c.scope === 'marketing.whatsapp' && c.granted) && typeof clist.body.total === 'number', 'GET me/consents',
    `consent list: ${clist.text.slice(0, 200)}`);
  r.record(j, 'DELETE', 'me/consents/marketing.whatsapp', await kabir.call('DELETE', 'me/consents/marketing.whatsapp'), 204, 'extra: withdraw');
  const clist2 = await kabir.call('GET', 'me/consents');
  check(clist2.body?.items?.[0]?.scope === 'marketing.whatsapp' && clist2.body.items[0].granted === false, 'GET me/consents', `withdrawal should be the newest record: ${clist2.text.slice(0, 200)}`);
  const ex = r.record(j, 'POST', 'me/data-export', await kabir.call('POST', 'me/data-export', {}), 202, 'extra: data export');
  check(typeof ex.body?.requestedAt === 'string', 'POST me/data-export', `body ${ex.text}`);

  // Refresh-token reuse after the grace window = theft: the session ends (used below after the 30 s wait).
  const stolenRt = rt1;

  // Logout clears both cookies.
  const lo = r.record(j, 'POST', 'auth/logout', await kabir.call('POST', 'auth/logout', {}), 204);
  const loc = (lo.headers.getSetCookie?.() || []).join(' | ');
  check(/sm_at=;/.test(loc) && /sm_rt=;/.test(loc) && (loc.match(/expires=Thu, 01 Jan 1970/gi) || []).length >= 2, 'POST auth/logout', `cookies not cleared: ${loc.slice(0, 300)}`);
  check(!kabir.cookies.sm_at && !kabir.cookies.sm_rt, 'POST auth/logout', 'cookie jar still holds a session');
  r.record(j, 'GET', 'session', await kabir.call('GET', 'session'), 401, 'after logout');

  // ================================================================================================================
  // Identity: one-time codes — hidden limit of 20 wrong codes (BR-11, A-312), resend after 30 s, used code = 410.
  // ================================================================================================================
  const zoya = await ctx.newLearner(j, 'Zoya', 'Khan');
  r.record(j, 'POST', 'auth/logout', await zoya.call('POST', 'auth/logout', {}), 204);
  const lc = r.record(j, 'POST', 'auth/email/login-code', await zoya.call('POST', 'auth/email/login-code', { email: zoya.email }), 200);
  const issuedAt = Date.now();
  check(lc.body?.resendIn === 30 && lc.body?.triesLeft === 20, 'POST auth/email/login-code', `resendIn ${lc.body?.resendIn}, triesLeft ${lc.body?.triesLeft}`);
  const lc2 = r.record(j, 'POST', 'auth/email/login-code', await zoya.call('POST', 'auth/email/login-code', { email: zoya.email }), 200, 'asked again inside 30 s');
  check(lc2.body?.resendIn > 0 && lc2.body.resendIn <= 30, 'POST auth/email/login-code', `resend should still be locked: resendIn ${lc2.body?.resendIn}`);
  for (let i = 1; i <= 20; i++) {
    const wrong = await zoya.call('POST', 'auth/email/login-verify', { email: zoya.email, code: String(100000 + i) });
    const expectStatus = i < 20 ? 422 : 429;
    r.record(j, 'POST', 'auth/email/login-verify', wrong, expectStatus, `wrong code #${i}`);
    check(codeOf(wrong) === (i < 20 ? 'invalid-code' : 'too-many-tries'), `POST auth/email/login-verify #${i}`, `code ${codeOf(wrong)}`);
    check(!/\d/.test(wrong.body?.message || ''), `POST auth/email/login-verify #${i}`, `the message must not show a count: ${wrong.body?.message}`);
  }
  r.record(j, 'POST', 'auth/email/login-verify', await zoya.call('POST', 'auth/email/login-verify', { email: zoya.email, code: ctx.CODE }), 429, 'right code after the hidden limit');

  // Use the wait for the resend window to also prove refresh-token theft detection (grace = 30 s).
  await sleep(Math.max(0, 31500 - (Date.now() - issuedAt)));
  const thief = new Client(ctx.base, 'thief');
  thief.cookies.sm_rt = stolenRt;
  r.record(j, 'POST', 'auth/refresh', await thief.call('POST', 'auth/refresh', {}), 401, 'extra: replayed refresh token after the grace window');

  const lc3 = r.record(j, 'POST', 'auth/email/login-code', await zoya.call('POST', 'auth/email/login-code', { email: zoya.email }), 200, 'resend after 30 s');
  check(lc3.body?.resendIn === 30 && lc3.body?.triesLeft === 20, 'POST auth/email/login-code (resend)', `a fresh code: resendIn ${lc3.body?.resendIn}, triesLeft ${lc3.body?.triesLeft}`);
  r.record(j, 'POST', 'auth/email/login-verify', await zoya.call('POST', 'auth/email/login-verify', { email: zoya.email, code: ctx.CODE }), 200, 'fresh code works');
  const used = r.record(j, 'POST', 'auth/email/login-verify', await new Client(ctx.base, 'x').call('POST', 'auth/email/login-verify', { email: zoya.email, code: ctx.CODE }), 410, 'code already used');
  check(codeOf(used) === 'code-expired', 'POST auth/email/login-verify (used)', `code ${codeOf(used)}`);

  // ================================================================================================================
  // Identity: erasure (DELETE me) — gone everywhere, and the email can sign up again.
  // ================================================================================================================
  const ravi = await ctx.newLearner(j, 'Ravi', 'Kumar');
  const raviId = (await ravi.call('GET', 'session')).body?.id;
  r.record(j, 'POST', 'me/feedback', await ravi.call('POST', 'me/feedback', { mood: 4, tags: [], comment: 'nice', screen: '/stats' }), 200);
  await poll(() => ravi.call('GET', 'me/progress'), x => x.body?.xp >= 5);
  r.record(j, 'POST', 'muni/messages', await ravi.call('POST', 'muni/messages', { text: 'hi' }), 200);
  r.record(j, 'GET', 'profile', await ravi.call('GET', 'profile'), 200);
  const del = r.record(j, 'DELETE', 'me', await ravi.call('DELETE', 'me'), 204, 'extra: erase my account');
  check(/sm_at=;/.test((del.headers.getSetCookie?.() || []).join(' ')), 'DELETE me', 'cookies should be cleared');
  r.record(j, 'GET', 'session', await ravi.call('GET', 'session'), 401, 'after erasure');
  if (check(!!keyB64 && !!raviId, 'erasure checks', 'AUTH_PRIVATE_KEY_B64 not set: cannot look inside other services')) {
    // A still-valid token for the erased id (as if stolen before erasure): every service must have dropped the data.
    const ghost = clientWithToken('ghost', mint({ sub: raviId, roles: ['learner'], name: 'Ravi' }));
    const gp = await poll(() => ghost.call('GET', 'profile'), x => x.status === 404 || x.status === 410);
    r.record(j, 'GET', 'profile', gp, [404, 410], 'erased learner');
    const gm = await poll(() => ghost.call('GET', 'muni/history'), x => Array.isArray(x.body) && x.body.length === 0);
    check(Array.isArray(gm.body) && gm.body.length === 0, 'GET muni/history (erased)', `chat history should be gone: ${gm.text.slice(0, 120)}`);
    const gw = await ghost.call('GET', 'muni/welcome');
    check(!(gw.text || '').includes('Kumar') && !(gw.body?.intro || '').includes(', 5 XP'), 'GET muni/welcome (erased)', `Muni still knows the learner: ${gw.text.slice(0, 160)}`);
    const gx = await poll(() => ghost.call('GET', 'me/progress'), x => x.status === 404 || x.status === 410 || x.body?.xp === 0);
    check(gx.status === 404 || gx.status === 410 || gx.body?.xp === 0, 'GET me/progress (erased)', `XP should be gone: ${gx.status} ${gx.text.slice(0, 120)}`);
    const gn = await ghost.call('GET', 'me/notification-preferences');
    check(gn.status !== 200 || JSON.stringify(gn.body) === JSON.stringify({ push: true, whatsApp: false, sms: false, email: true }), 'GET me/notification-preferences (erased)', `preferences should be back to defaults: ${gn.text}`);
  }
  const again2 = new Client(ctx.base, 'Ravi again');
  r.record(j, 'POST', 'auth/register', await again2.call('POST', 'auth/register', { firstName: 'Ravi', lastName: 'Kumar', email: ravi.email, password: 'Skillmuni123', consent: true }), 200, 'same email after erasure');
  const rv2 = r.record(j, 'POST', 'auth/email/verify', await again2.call('POST', 'auth/email/verify', { email: ravi.email, code: ctx.CODE }), 200);
  check(rv2.body?.id && rv2.body.id !== raviId, 'POST auth/email/verify', 'a new account with a new id');
  const fresh = await poll(() => again2.call('GET', 'me/progress'), x => x.status === 200);
  check(fresh.body?.xp === 0, 'GET me/progress (new account)', `a new account starts at 0 XP, got ${fresh.body?.xp}`);

  // ================================================================================================================
  // Institute claim links (Figma section 11, BR-119): admin creates → learner claims → quests 1–5 done, 550 XP.
  // ================================================================================================================
  if (check(!!keyB64, 'claims', 'AUTH_PRIVATE_KEY_B64 not set: cannot create a claim as an institute admin')) {
    const admin = clientWithToken('institute-admin', mint({ sub: uuid(), roles: ['institute-admin'], name: 'Pune Skills Academy' }));
    const day = n => new Date(Date.now() + n * 86400000).toISOString().slice(0, 10);
    const randomPhone = () => `+91 9${String(Math.floor(Math.random() * 1e9)).padStart(9, '0')}`;
    const row = (label, value, source, issue) => ({ label, value, source, ...(issue ? { issue } : {}) });
    const sections = [
      { slug: 'who-dis', quest: 1, title: 'Who Dis?', summary: 'Aarav Mehta · date of birth added by you', rows: [row('Name', 'Aarav Mehta', 'institute'), row('Date of birth', '14 Mar 2005', 'you')] },
      { slug: 'hit-me-up', quest: 2, title: 'Hit Me Up', summary: 'Mobile verified · Kothrud, Pune 411038', rows: [row('Mobile', '+91 98•••• 4521', 'verified'), row('Address', 'Kothrud, Pune 411038', 'institute')] },
      { slug: 'big-brain-stuff', quest: 3, title: 'Big Brain Stuff', summary: '12th (HSC) · NSQF L4 certificate', rows: [row('Highest level', '12th (HSC) · 2023', 'institute'), row('Marks', '72% · optional', null)] },
      { slug: 'your-flex-era', quest: 4, title: 'Your Flex Era', summary: '8 skills · 3 languages', rows: [row('Skills', 'Customer service, Active listening +6', 'institute')] },
      { slug: 'work-lore', quest: 5, title: 'Work Lore', summary: 'Customer support intern · end date missing', rows: [row('Customer support intern', 'Jun 2025 – ?', 'institute', 'End date missing. Still there? Add it or tap Looks right.')] },
    ];
    const result = {
      profilePercent: 80, verifiedItems: 2, jobs: 9, xpLeft: 300, minutesLeft: 6,
      left: [
        { quest: 6, slug: 'pick-your-arc', name: 'Pick Your Arc', blurb: 'Jobs you want, where, when you can start', xp: 100 },
        { quest: 7, slug: 'drop-the-receipts', name: 'Drop the Receipts', blurb: 'Your NSQF L4 certificate is already verified ✓', xp: 100 },
        { quest: 8, slug: 'vibe-check', name: 'Vibe Check', blurb: 'Review and choose who can see you', xp: 100 },
      ],
    };
    const claimBody = (over = {}) => ({
      institute: 'Pune Skills Academy', name: 'Aarav Mehta', programme: 'Customer Care Executive (Domestic Voice) · NSQF L4',
      batch: 'Pune batch 15 · completed 30 Sep 2026', fromCv: 'Contact, education, 8 skills, 2 languages, 1 job',
      phone: randomPhone(), certificate: 'NSQF L4 certificate', deleteBy: day(15), sections, result, ...over,
    });

    r.record(j, 'POST', 'admin/claims', await meera.call('POST', 'admin/claims', claimBody()), 403, 'extra: a learner is not an institute admin');
    const created = r.record(j, 'POST', 'admin/claims', await admin.call('POST', 'admin/claims', claimBody()), 201, 'extra: institute admin creates the link');
    const token = created.body?.token;
    if (check(!!token && !!created.headers.get('location'), 'POST admin/claims', `no token / Location: ${created.text.slice(0, 160)}`)) {
      const t = encodeURIComponent(token);
      const aarav = new Client(ctx.base, 'Aarav');
      const inv = r.record(j, 'GET', `claims/${token}`, await aarav.call('GET', `claims/${t}`), 200, 'V1 opens the link (sends the code)');
      const b = inv.body || {};
      check(b.institute === 'Pune Skills Academy' && b.name === 'Aarav Mehta' && b.certificate === 'NSQF L4 certificate' && b.deleteBy === day(15), `GET claims/${token}`, `invite ${inv.text.slice(0, 200)}`);
      check(/^\+91 \d{2}•+ ?\d{4}$/.test(b.sentTo || '') && b.resendIn > 0 && b.triesLeft === 20 && !('resume' in b), `GET claims/${token}`, `challenge ${b.sentTo} / ${b.resendIn} / ${b.triesLeft} / resume ${JSON.stringify(b.resume)}`);
      const otp = r.record(j, 'POST', `claims/${token}/otp`, await aarav.call('POST', `claims/${t}/otp`, {}), 200, 'resend inside 30 s');
      check(otp.body?.resendIn > 0 && otp.body.resendIn <= 30 && otp.body.sentTo === b.sentTo, `POST claims/${token}/otp`, `challenge ${otp.text}`);
      r.record(j, 'POST', `claims/${token}/verify`, await aarav.call('POST', `claims/${t}/verify`, { code: '000001' }), 422, 'wrong code');
      r.record(j, 'POST', `claims/${token}/consent`, await aarav.call('POST', `claims/${t}/consent`, { shareProgress: true, verifyCertificate: true, applyForMe: false }), 401, 'before the code');
      const ver = r.record(j, 'POST', `claims/${token}/verify`, await aarav.call('POST', `claims/${t}/verify`, { code: ctx.CODE }), 200, 'V2 code → signed in');
      check(ver.body?.firstName === 'Aarav' && !!ver.body?.id, `POST claims/${token}/verify`, `AuthUser ${ver.text}`);
      const aaravId = ver.body?.id;
      const res1 = await aarav.call('GET', `claims/${t}`);
      check(res1.body?.resume?.stage === 'consent' && res1.body.resume.sections?.length === 0, `GET claims/${token}`, `resume after verify ${JSON.stringify(res1.body?.resume)}`);
      r.record(j, 'POST', `claims/${token}/finish`, await aarav.call('POST', `claims/${t}/finish`, {}), 422, 'before consent');
      const cons = r.record(j, 'POST', `claims/${token}/consent`, await aarav.call('POST', `claims/${t}/consent`, { shareProgress: true, verifyCertificate: true, applyForMe: false }), 200, 'V2 consent');
      const st = (cons.body || []).map(s => `${s.slug}:${s.status}`).join(',');
      check(st === 'who-dis:review,hit-me-up:review,big-brain-stuff:review,your-flex-era:review,work-lore:fix', `POST claims/${token}/consent`, `sections ${st}`);
      const intruder = await meera.call('POST', `claims/${t}/sections/who-dis/confirm`, {});
      r.record(j, 'POST', `claims/${token}/sections/who-dis/confirm`, intruder, 403, 'another learner');
      check(codeOf(intruder) === 'not-claimant', 'confirm by another learner', `code ${codeOf(intruder)}`);
      const pend = await aarav.call('POST', `claims/${t}/finish`, {});
      r.record(j, 'POST', `claims/${token}/finish`, pend, 422, 'sections pending');
      check(codeOf(pend) === 'sections-pending', `POST claims/${token}/finish`, `code ${codeOf(pend)}`);
      for (const s of sections) {
        r.record(j, 'POST', `claims/${token}/sections/${s.slug}/confirm`, await aarav.call('POST', `claims/${t}/sections/${s.slug}/confirm`, {}), 204, 'V3 Looks right');
      }
      r.record(j, 'POST', `claims/${token}/sections/nope/confirm`, await aarav.call('POST', `claims/${t}/sections/nope/confirm`, {}), 404, 'unknown section');
      const fin = r.record(j, 'POST', `claims/${token}/finish`, await aarav.call('POST', `claims/${t}/finish`, {}), 200, 'V4 confirmed');
      check(JSON.stringify(fin.body) === JSON.stringify(result), `POST claims/${token}/finish`, `result ${fin.text.slice(0, 200)}`);
      r.record(j, 'POST', `claims/${token}/finish`, await aarav.call('POST', `claims/${t}/finish`, {}), 200, 'finish again (replay) → same result');
      const reopen = await aarav.call('GET', `claims/${t}`);
      check(reopen.status === 200 && reopen.body?.resume?.stage === 'confirmed' && reopen.body.resume.sections.every(s => s.status === 'confirmed'), `GET claims/${token}`, `claimant reopens: ${reopen.status} ${JSON.stringify(reopen.body?.resume)?.slice(0, 200)}`);
      const gone = r.record(j, 'GET', `claims/${token}`, await new Client(ctx.base, 'stranger').call('GET', `claims/${t}`), 410, 'used link, someone else');
      check(codeOf(gone) === 'claimed', `GET claims/${token}`, `code ${codeOf(gone)}`);
      r.record(j, 'GET', 'session', await aarav.call('GET', 'session'), 200, 'claimed learner is signed in');

      // BR-119: quests 1–5 done with their normal step and quest XP, 550 in all.
      const cp = await poll(() => aarav.call('GET', 'me/progress'), x => x.body?.completed >= 5 && x.body?.xp >= 550, 60000);
      r.record(j, 'GET', 'me/progress', cp, 200, 'after the claim (BR-119)');
      check(cp.body?.completed === 5 && cp.body?.xp === 550 && cp.body?.badges >= 5, 'GET me/progress (claimed)', `expected quests 1–5 done, 550 XP, 5 badges: ${cp.text}`);
      // Muni knows the claimed learner's real numbers; the bell shows the quest badges.
      const mw = await poll(() => aarav.call('GET', 'muni/welcome'), x => (x.body?.intro || '').includes('550 XP'), 30000);
      check(mw.body?.firstName === 'Aarav' && (mw.body?.intro || '').includes('550 XP'), 'GET muni/welcome (claimed)', `intro ${mw.body?.intro}`);
      const bell = await poll(() => aarav.call('GET', 'me/notifications'), x => x.body?.total >= 1, 30000);
      r.record(j, 'GET', 'me/notifications', bell, 200, 'extra: badge notifications');
      if (check(bell.body?.total >= 1 && bell.body.unread >= 1, 'GET me/notifications (claimed)', `quest badges should ring the bell: ${bell.text.slice(0, 200)}`)) {
        const n = bell.body.items[0];
        check(typeof n.title === 'string' && n.read === false && !Number.isNaN(Date.parse(n.createdAt)), 'GET me/notifications (claimed)', `item ${JSON.stringify(n)}`);
        r.record(j, 'PUT', 'me/notifications/{id}/read', await aarav.call('PUT', `me/notifications/${n.id}/read`, {}), 204, 'extra: mark read');
        const after = await aarav.call('GET', 'me/notifications');
        check(after.body?.unread === bell.body.unread - 1 && after.body.items.find(x => x.id === n.id)?.read === true, 'PUT me/notifications/{id}/read', `unread ${bell.body.unread} → ${after.body?.unread}`);
        r.record(j, 'PUT', 'me/notifications/{id}/read', await meera.call('PUT', `me/notifications/${n.id}/read`, {}), 404, 'extra: someone else’s notification');
      }
      ctx.state.claimedLearnerId = aaravId;
    }

    // V5 decline: the link is gone (410 deleted); declining twice is fine.
    const c2 = await admin.call('POST', 'admin/claims', claimBody({ name: 'Nisha Patil' }));
    if (check(c2.status === 201, 'POST admin/claims (decline path)', `status ${c2.status}`)) {
      const t2 = encodeURIComponent(c2.body.token);
      const nisha = new Client(ctx.base, 'Nisha');
      r.record(j, 'GET', 'claims/{token}', await nisha.call('GET', `claims/${t2}`), 200, 'decline path: open');
      r.record(j, 'POST', 'claims/{token}/decline', await nisha.call('POST', `claims/${t2}/decline`, { reason: 'bogus' }), 422, 'unknown reason');
      r.record(j, 'POST', 'claims/{token}/decline', await nisha.call('POST', `claims/${t2}/decline`, { reason: 'not-me' }), 204, 'V5 not me');
      r.record(j, 'POST', 'claims/{token}/decline', await nisha.call('POST', `claims/${t2}/decline`, { reason: null }), 204, 'declined twice');
      const d = r.record(j, 'GET', 'claims/{token}', await nisha.call('GET', `claims/${t2}`), 410, 'declined link');
      check(codeOf(d) === 'deleted', 'GET claims/{token} (declined)', `code ${codeOf(d)}`);
      r.record(j, 'POST', 'claims/{token}/verify', await nisha.call('POST', `claims/${t2}/verify`, { code: ctx.CODE }), 410, 'declined link cannot be claimed');
    }
    const c3 = await admin.call('POST', 'admin/claims', claimBody({ name: 'Old Link', deleteBy: day(-1) }));
    if (check(c3.status === 201, 'POST admin/claims (expired)', `status ${c3.status}`)) {
      const e = r.record(j, 'GET', 'claims/{token}', await anon.call('GET', `claims/${encodeURIComponent(c3.body.token)}`), 410, 'past its delete-by date');
      check(codeOf(e) === 'expired', 'GET claims/{token} (expired)', `code ${codeOf(e)}`);
    }
    const unk = r.record(j, 'GET', 'claims/{token}', await anon.call('GET', 'claims/no-such-link'), 404, 'unknown link');
    check(codeOf(unk) === 'claim-not-found', 'GET claims/no-such-link', `code ${codeOf(unk)}`);
    r.record(j, 'POST', 'admin/claims', await admin.call('POST', 'admin/claims', claimBody({ phone: '12345' })), 422, 'extra: invalid phone');
  }

  // ================================================================================================================
  // Gateway: CORS, rate limits, internal routes, unknown routes, security headers.
  // ================================================================================================================
  const app = 'http://localhost:4200';
  const pre = await raw('OPTIONS', '/api/v1/auth/login', { Origin: app, 'Access-Control-Request-Method': 'POST', 'Access-Control-Request-Headers': 'content-type,idempotency-key' });
  check([200, 204].includes(pre.status) && pre.headers.get('access-control-allow-origin') === app && pre.headers.get('access-control-allow-credentials') === 'true'
    && /post/i.test(pre.headers.get('access-control-allow-methods') || ''), 'OPTIONS /api/v1/auth/login (CORS)',
  `preflight for the app origin: ${pre.status} ${pre.headers.get('access-control-allow-origin')} ${pre.headers.get('access-control-allow-credentials')}`);
  const evil = await raw('OPTIONS', '/api/v1/auth/login', { Origin: 'https://evil.example', 'Access-Control-Request-Method': 'POST' });
  check(!evil.headers.get('access-control-allow-origin'), 'OPTIONS /api/v1/auth/login (CORS)', 'a foreign origin must not be allowed');
  const simple = await raw('GET', '/api/v1/geo/IN/regions', { Origin: app });
  check(simple.headers.get('access-control-allow-origin') === app && /x-correlation-id/i.test(simple.headers.get('access-control-expose-headers') || ''), 'GET with Origin (CORS)',
    `ACAO ${simple.headers.get('access-control-allow-origin')}, expose ${simple.headers.get('access-control-expose-headers')}`);

  const burst = await Promise.all(Array.from({ length: 40 }, () => meera.call('GET', 'muni/welcome')));
  check(burst.every(x => x.status === 200), 'rate limits', `40 quick screen loads by one learner: ${burst.filter(x => x.status !== 200).map(x => x.status).join(',')}`);

  for (const p of ['/internal/sessions/refresh', '/internal/accounts/email-owner', '/api/v1/internal/accounts/email-owner', '/api/internal/sessions/refresh']) {
    const res = await raw('POST', p, { 'Content-Type': 'application/json' }, JSON.stringify({ email: 'x@example.com' }));
    check(res.status === 404 && isProblem(res, 'not-found'), `POST ${p}`, `internal endpoints must not be reachable through the gateway: ${res.status} ${res.contentType} ${res.text.slice(0, 80)}`);
  }
  for (const p of ['/api/v1/nope', '/api/v2/session', '/nope']) {
    const res = await raw('GET', p, { Accept: 'application/json' });
    check(res.status === 404 && isProblem(res, 'not-found'), `GET ${p}`, `unknown route → 404 problem+json: ${res.status} ${res.contentType} ${res.text.slice(0, 80)}`);
  }
  const unknownInService = await meera.call('GET', 'muni/nope');
  r.record(j, 'GET', 'muni/nope', unknownInService, 404, 'extra: unknown route inside a service');
  check(codeOf(unknownInService) === 'not-found', 'GET muni/nope', `code ${codeOf(unknownInService)}`);

  const hdr = await raw('GET', '/api/v1/session', { Origin: app });
  const h = name => hdr.headers.get(name) || '';
  check(h('x-content-type-options') === 'nosniff', 'security headers', `X-Content-Type-Options: ${h('x-content-type-options')}`);
  check(/deny/i.test(h('x-frame-options')) || /frame-ancestors 'none'/.test(h('content-security-policy')), 'security headers', 'framing should be denied');
  check(/no-referrer|strict-origin/.test(h('referrer-policy')), 'security headers', `Referrer-Policy: ${h('referrer-policy')}`);
  check(/default-src 'none'/.test(h('content-security-policy')), 'security headers', `Content-Security-Policy: ${h('content-security-policy')}`);
  check(!h('server') && !h('x-powered-by'), 'security headers', `server banner: ${h('server')} ${h('x-powered-by')}`);
  check(!!h('x-correlation-id'), 'security headers', 'every response carries a correlation id');
};
