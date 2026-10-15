'use strict';
// The 8 registration quests exactly as the app saves them (quest-store.ts, quest-page.ts, features/quests/parts/*),
// then Job Profile, Profile, the programme bonus quest and the Learner extras. BRD §4.1: BR-100…BR-103 quest XP,
// BR-108/109 profile tasks, BR-110 job profile mission, BR-113 programme, BR-114 feedback.

const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);
const PDF = Buffer.from(
  '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[]/Count 0>>endobj\n' +
    'trailer<</Root 1 0 R>>\n%%EOF\n',
);

const QUEST_XP = 50;
const STEP_XP = 20;
const sleep = ms => new Promise(r => setTimeout(r, ms));

module.exports = async (ctx, j) => {
  const r = ctx.report;
  const T = Date.now().toString().slice(-8);

  /** A custom assertion row (counts as a problem when `ok` is false). */
  const expect = (ok, label, detail = '') => {
    r.rows.push({ journey: j, call: `(check) ${label}`, status: ok ? 200 : 0, ms: 0, problems: ok ? [] : [`${label}${detail ? ': ' + detail : ''}`], note: 'extra' });
    return ok;
  };
  const call = async (c, verb, path, body, status, note = '') => r.record(j, verb, path, await c.call(verb, path, body), status, note);

  /** Saves a part like QuestStore.savePart (PUT, `{ values, done, xp }`). Not in contract.json: it goes through the OfflineQueue. */
  const part = (c, slug, n, values, done = true, xp = done) =>
    call(c, 'PUT', `me/quests/${slug}/parts/${n}`, { values, done, xp }, 204, 'extra · offline queue');
  /** QuestStore.saveBonus: `{ values: {...values, bonusXp: 0}, done: false, bonusXp: 0 }` (A-168: always 0). */
  const bonus = (c, slug, n, values) =>
    call(c, 'PUT', `me/quests/${slug}/parts/${n}`, { values: { ...values, bonusXp: 0 }, done: false, bonusXp: 0 }, 204, 'extra · offline queue (saveBonus)');
  const complete = (c, slug, xp, note = '') => {
    return (async () => {
      const res = await call(c, 'POST', `me/quests/${slug}/complete`, {}, 200, note);
      if (res.status === 200) expect(res.body?.xpEarned === xp, `${slug} complete xpEarned ${xp}`, `got ${res.body?.xpEarned}`);
      return res;
    })();
  };
  const upload = async (c, path, field, bytes, name, type, status, note = '') => {
    const form = new FormData();
    form.append(field, new Blob([bytes], { type }), name);
    return r.record(j, 'POST', path, await c.call('POST', path, form), status, note);
  };
  /** Events are async (outbox → Service Bus → Gamification): poll me/progress until `done(p)` or ~40 s. */
  const progressUntil = async (c, done) => {
    let last = null;
    for (let i = 0; i < 40; i++) {
      const res = await c.call('GET', 'me/progress');
      last = res;
      if (res.status === 200 && done(res.body)) break;
      await sleep(1000);
    }
    r.record(j, 'GET', 'me/progress', last, 200, 'polled');
    return last?.body;
  };
  const ledger = async c => {
    const res = await c.call('GET', 'me/xp/ledger?page=1&pageSize=100');
    r.record(j, 'GET', 'me/xp/ledger', res, 200, 'extra · ledger');
    return res.body?.items ?? [];
  };
  const sum = (items, reason) => items.filter(e => e.reason === reason).reduce((n, e) => n + e.xp, 0);

  // ───────────────────────── Learner A: every quest, every optional step filled ─────────────────────────
  const a = await ctx.newLearner(j, 'Asha', 'Rao');
  const mobile = `9${T}${Math.floor(Math.random() * 10)}`; // 10 digits, unique per run
  const email = a.email;

  let p = await call(a, 'GET', 'me/progress', undefined, 200, 'fresh learner');
  expect(p.body?.xp === 0 && p.body?.level === 1 && p.body?.completed === 0, 'fresh progress is 0 XP, level 1', JSON.stringify(p.body));
  const seeded = await call(a, 'GET', 'me/quests/who-dis/answers', undefined, 200);
  expect(seeded.body?.firstName === 'Asha', 'who-dis answers seeded with the registered first name', JSON.stringify(seeded.body));
  await call(a, 'GET', 'me/quests/no-such-quest/answers', undefined, 404, 'quest-not-found');
  await call(a, 'PUT', 'me/quests/who-dis/parts/9', { values: {}, done: true, xp: true }, 404, 'extra · part-not-found');

  // Out of order: Quest 3 can't be completed before 1 and 2 (quest-locked).
  await call(a, 'POST', 'me/quests/big-brain-stuff/complete', {}, 422, 'quest-locked');

  // Q1 Who Dis? — name · ID & birthday (India: DigiLocker, A-272) · the tea
  await part(a, 'who-dis', 1, { firstName: 'Priya', lastName: 'Sharma', nickname: 'Pri', nameSkipped: false });
  await call(a, 'GET', 'me/identity', undefined, 200, 'nothing linked → null');
  const start = await call(a, 'POST', 'me/identity/digilocker/start', { doc: 'aadhaar' }, 200);
  expect(typeof start.body?.authorizeUrl === 'string' && start.body.authorizeUrl.length > 0, 'DigiLocker start returns an authorizeUrl');
  const linked = await call(a, 'POST', 'me/identity/digilocker/link', { doc: 'aadhaar', code: 'sandbox-consent-code' }, 200);
  expect(/\d{4}$/.test(linked.body?.masked ?? '') && !/\d{5,}/.test((linked.body?.masked ?? '').replace(/\s/g, '')), 'linked ID is masked (never the full number)', linked.body?.masked);
  const current = await call(a, 'GET', 'me/identity', undefined, 200, 'linked');
  expect(current.body?.doc === 'aadhaar', 'me/identity returns the linked doc', JSON.stringify(current.body));
  await part(a, 'who-dis', 2, {
    idType: 'aadhaar', idNumber: '', birthday: '21 / 08 / 2004',
    idMasked: linked.body?.masked ?? 'XXXX XXXX 4821', idLast4: linked.body?.last4 ?? '4821', idVia: 'digilocker', idSkipped: false,
  });
  // Back to part 1 and forward again: saving a finished step again pays nothing (BR-100).
  await part(a, 'who-dis', 1, { firstName: 'Priya', lastName: 'Sharma', nickname: 'Pri', nameSkipped: false });
  await part(a, 'who-dis', 3, {
    gender: 'female', genderText: '', homeLanguage: 'Hindi', homeLanguageOther: '', homeLanguageText: '', race: '',
    disability: 'no', disabilityTypes: [], disabilityOther: '', disabilityPercent: '', disabilityCertificate: '', disabilitySupport: [],
    teaSkipped: false,
  });
  const q1 = await complete(a, 'who-dis', QUEST_XP);
  expect(q1.body?.progress?.completed === 1, 'after Q1 progress.completed = 1', JSON.stringify(q1.body?.progress));
  await complete(a, 'who-dis', 0, 'replay pays 0');
  const answers1 = await call(a, 'GET', 'me/quests/who-dis/answers', undefined, 200);
  expect(answers1.body?.firstName === 'Priya' && answers1.body?.idLast4 && answers1.body?.gender === 'female', 'who-dis answers merged across parts', JSON.stringify(answers1.body));

  // Q2 Hit Me Up — digits (verified by code) · backup contact (their mobile → +20) · spot
  await call(a, 'POST', 'me/phone/verify', { phone: `+91${mobile}`, code: '111111' }, [404, 410, 422], 'no code sent yet');
  const sent = await call(a, 'POST', 'me/phone/code', { phone: `+91${mobile}` }, 200);
  expect(typeof sent.body?.sentTo === 'string' && !sent.body.sentTo.includes(mobile), 'phone code sentTo is masked', sent.body?.sentTo);
  await call(a, 'POST', 'me/phone/verify', { phone: `+91${mobile}`, code: '111111' }, 422, 'invalid-code');
  await call(a, 'POST', 'me/phone/verify', { phone: `+91${mobile}`, code: ctx.CODE }, [200, 204]);
  await part(a, 'hit-me-up', 1, { phone: mobile, phoneVerified: true, email, whatsapp: true, digitsSkipped: false });
  await part(a, 'hit-me-up', 2, {
    relation: 'parent', relationText: '', backupName: 'Sunita Sharma', backupPhone: '9812345670', altEmail: `alt.${email}`, backupSkipped: false,
  });
  await part(a, 'hit-me-up', 3, {
    postal: '400001', region: 'Maharashtra', district: 'Mumbai', city: 'Mumbai', area: 'Fort',
    line1: 'Flat 12, Shree Apartments, Shahid Bhagat Singh Road', unit: '12', landmark: 'Near CST station', spotSkipped: false,
  });
  await complete(a, 'hit-me-up', QUEST_XP);

  // Q3 Big Brain Stuff — level · details · timeline (an entry → +20)
  await part(a, 'big-brain-stuff', 1, { level: 'graduate', status: 'completed', levelSkipped: false });
  await part(a, 'big-brain-stuff', 2, {
    degree: 'B.Com', branch: 'Accounting & Finance', institute: 'University of Mumbai', board: '', year: '2024', studyYear: '',
    scoreKind: 'cgpa', score: '7.8', detailsSkipped: false,
  });
  const timeline = [
    { id: 'tl1', level: '12th', board: 'Maharashtra State Board', stream: 'Commerce', name: '', institute: 'Jai Hind College', year: '2021', score: '78' },
    { id: 'tl2', level: 'course', board: '', stream: '', name: 'Tally Prime with GST', institute: 'NIIT Fort', year: '2023', score: '' },
  ];
  await bonus(a, 'big-brain-stuff', 3, { timeline }); // committing an entry autosaves, no XP (A-168)
  await part(a, 'big-brain-stuff', 3, { timeline, timelineSkipped: false });
  await complete(a, 'big-brain-stuff', QUEST_XP);

  // Q4 Your Flex Era — skill deck (first 3 = signature moves) · language passport · trophy shelf (a certificate → +20)
  const esco = await call(a, 'GET', 'skills/esco?q=excel&lang=en&limit=24', undefined, [200, 503], 'ESCO may be unreachable from CI');
  if (esco.status === 200) expect(Array.isArray(esco.body), 'skills/esco returns a list');
  const skills = [
    { id: 'customer-service', name: 'Customer service', category: 'people', level: 3, source: 'suggested' },
    { id: 'microsoft-excel', name: 'Microsoft Excel', category: 'business', level: 3, source: 'search', esco: 'http://data.europa.eu/esco/skill/excel' },
    { id: 'verbal-communication', name: 'Verbal communication', category: 'people', level: 2, source: 'suggested' },
    { id: 'data-entry', name: 'Data entry', category: 'data', level: 2, source: 'suggested' },
    { id: 'tally', name: 'Tally', category: 'business', level: 1, source: 'custom' },
  ];
  await part(a, 'your-flex-era', 1, { skills, deckSkipped: false });
  const languages = [
    { name: 'Hindi', fluency: 'native', speak: true, read: true, write: true, motherTongue: true, fromCv: false },
    { name: 'English', fluency: 'fluent', speak: true, read: true, write: true, motherTongue: false, fromCv: false },
    { name: 'Marathi', fluency: 'conversational', speak: true, read: true, write: false, motherTongue: false, fromCv: false },
  ];
  await part(a, 'your-flex-era', 2, { languages, passportSkipped: false });
  await call(a, 'POST', 'me/trophies/check-link', { url: 'https://www.credly.com/badges/not-a-real-badge/public_url' }, [200, 408, 422, 504], 'public page check');
  await call(a, 'POST', 'me/trophies/check-link', { url: 'http://localhost/admin' }, [422], 'private address refused');
  const file = await upload(a, 'me/trophies/files', 'file', PDF, 'nsdc-certificate.pdf', 'application/pdf', 200);
  expect(typeof file.body?.fileId === 'string' && file.body.fileId.length > 0, 'trophy upload returns a fileId');
  await upload(a, 'me/trophies/files', 'file', Buffer.from('not really a pdf'), 'fake.pdf', 'application/pdf', 422, 'unsupported-type');
  const digi = await call(a, 'POST', 'me/trophies/digilocker', {}, [200, 503], 'sandbox import');
  if (digi.status === 200) expect(Array.isArray(digi.body) && digi.body.every(t => typeof t.name === 'string'), 'DigiLocker import returns trophies');
  const trophies = [
    {
      id: 't1', name: 'Data capturing certificate', issuer: 'NSDC · Skill India', issuedOn: '2024-03', expiresOn: '', noExpiry: true,
      credentialUrl: '', credentialId: 'SI-DC-88213', fileName: 'nsdc-certificate.pdf', verified: false, linkedSkills: ['data-entry'], source: 'manual',
    },
    {
      id: 't2', name: 'NPTEL Programming in Java', issuer: 'NPTEL · IIT Kharagpur', issuedOn: '2023-10', expiresOn: '', noExpiry: true,
      credentialUrl: '', credentialId: 'NPTEL23CS74S4', fileName: '', verified: true, linkedSkills: ['microsoft-excel'], source: 'digilocker',
    },
  ];
  await part(a, 'your-flex-era', 3, { trophies, skills, trophiesSkipped: false });
  await complete(a, 'your-flex-era', QUEST_XP);

  // Q5 Work Lore — types · chapter · timeline
  await part(a, 'work-lore', 1, { fresher: false, workTypes: ['internship', 'part_time'], starters: [], typesSkipped: false });
  const chapters = [
    {
      id: 'c1', type: 'internship', role: 'Customer Service Intern', company: 'Teleperformance', start: '2025-01', end: '2025-06', current: false,
      mode: 'onsite', skills: ['customer-service', 'verbal-communication'], description: 'Handled 60+ inbound calls a day · 92% CSAT',
    },
    {
      id: 'c2', type: 'part_time', role: 'Sales Associate', company: 'Reliance Trends', start: '2023-03', end: '2024-04', current: false,
      mode: 'onsite', skills: ['data-entry'], description: 'Top seller 3 months running',
    },
  ];
  await part(a, 'work-lore', 2, { chapters, chapterDraft: null, chapterDrafts: null, chapterSkipped: false });
  await bonus(a, 'work-lore', 3, { chapters });
  await part(a, 'work-lore', 3, { chapters, gapReasons: {}, timelineSkipped: false });
  const q5 = await complete(a, 'work-lore', QUEST_XP);
  // 5 quests: 15 steps × 20 + 5 × 50 = 550 → Level 2 Pathfinder (Figma Q5 "You levelled up!").
  expect(q5.body?.progress?.xp === 550 && q5.body?.progress?.level === 2, 'after Q5 the completion shows 550 XP, level 2', JSON.stringify(q5.body?.progress));

  // Q6 Pick Your Arc
  await part(a, 'pick-your-arc', 1, {
    lookingFor: ['full_time', 'internship'], roles: ['Customer support executive', 'BPO · voice process'], where: ['near', 'remote'],
    shifts: ['day', 'rotational'], notice: 'immediate', salary: '3-4.5', arcSkipped: false,
  });
  await complete(a, 'pick-your-arc', QUEST_XP);

  // Q7 Drop the Receipts — one proof per claim (keys edu · tl-<id> · tr-<id> · ch-<id>)
  const proofFile = await upload(a, 'me/trophies/files', 'file', PDF, 'bcom-degree.pdf', 'application/pdf', 200, 'receipt upload');
  expect(!!proofFile.body?.fileId, 'receipt upload returns a fileId');
  const receipts = {
    edu: { status: 'checking', fileName: 'bcom-degree.pdf', at: '15 Oct 2026' },
    'ch-c1': { status: 'verified', fileName: 'tp-letter.pdf', at: '15 Oct 2026' },
  };
  await part(a, 'drop-the-receipts', 1, { receipts, receiptsSkipped: false });
  await complete(a, 'drop-the-receipts', QUEST_XP);

  // Q8 Vibe Check — settings, publish (My Matches), complete
  await part(a, 'vibe-check', 1, { discoverable: true, invites: true, vibeSkipped: false });
  await call(a, 'POST', 'me/profile/publish', { discoverable: true, invites: true }, [200, 204], 'quest-page finish(): publish before complete');
  const q8 = await complete(a, 'vibe-check', QUEST_XP);
  expect(q8.body?.progress?.completed === 8, 'after Q8 progress.completed = 8', JSON.stringify(q8.body?.progress));
  await complete(a, 'vibe-check', 0, 'replay pays 0');

  // BR-103: 18 × 20 + 8 × 50 = 760 quest XP. BR-110: the Job Profile reached 100% → the "Finish job profile" mission +50.
  const done = await progressUntil(a, b => b?.completed === 8 && b?.xp >= 810);
  const items = await ledger(a);
  const questXp = sum(items, 'quest.step') + sum(items, 'quest.complete');
  expect(questXp === 760, 'quest XP in the ledger is 760 (BR-103)', `steps ${sum(items, 'quest.step')} + quests ${sum(items, 'quest.complete')}`);
  expect(items.filter(e => e.reason === 'quest.step').length === 18, '18 paid quest steps, once each', String(items.filter(e => e.reason === 'quest.step').length));
  expect(sum(items, 'mission') === 50, 'Job Profile 100% pays the mission +50 once (BR-110)', String(sum(items, 'mission')));
  expect(done?.xp === 810 && done?.level === 3 && done?.rank === 'Trailblazer' && done?.completed === 8 && done?.target === 1200,
    'me/progress after all 8 quests: 810 XP (760 + 50), level 3 Trailblazer, completed 8', JSON.stringify(done));

  // ───────────────────────── Job Profile (built from the quests) ─────────────────────────
  const jp = await call(a, 'GET', 'job-profile', undefined, 200);
  const b = jp.body ?? {};
  expect(b.name === 'Priya Sharma', 'job profile name from Quest 1', b.name);
  expect(b.strength?.percent === 100, 'completeness 100% after all quests', JSON.stringify(b.strength));
  expect((b.byQuest ?? []).every(q => q.filled === q.total && q.left.length === 0), 'every quest step filled', JSON.stringify(b.byQuest?.filter(q => q.left.length)));
  expect(b.skills?.length === 5 && b.counts?.skills === 5, 'all 5 deck skills', JSON.stringify(b.skills));
  expect(JSON.stringify((b.signature ?? []).map(s => s.name)) === JSON.stringify(['Customer service', 'Microsoft Excel', 'Verbal communication']),
    'signature moves = the first 3 skills of the deck', JSON.stringify(b.signature));
  expect((b.skills ?? []).filter(s => s.signature).length === 3, '3 skills flagged as signature', JSON.stringify(b.skills));
  expect(b.languages?.length === 3 && b.languages[2]?.skills === 'Speak · Read' && b.languages[0]?.level === 'Native', 'languages from the passport', JSON.stringify(b.languages));
  expect(b.education?.length === 3 && b.education[0]?.title === 'B.Com · Accounting & Finance' && b.education[0]?.school === 'University of Mumbai',
    'education: highest + 12th + course', JSON.stringify(b.education));
  expect(b.education?.[1]?.title === '12th · Commerce' && b.education?.[2]?.title === 'Tally Prime with GST', 'timeline entries titled like the quest', JSON.stringify(b.education?.slice(1)));
  expect(b.education?.[0]?.proof === 'checking', 'education proof "checking" from the Q7 receipt', b.education?.[0]?.proof);
  expect(b.work?.length === 2 && b.work[0]?.employer === 'Teleperformance' && b.work[0]?.proof === 'verified' && b.work[1]?.proof === 'none',
    'work chapters with receipt proofs', JSON.stringify(b.work));
  expect(b.certificates?.length === 2 && b.certificates[1]?.status === 'verified', 'trophies as certificates', JSON.stringify(b.certificates));
  expect(typeof b.about?.text === 'string' && b.about.text.includes('B.Com') && b.about.byMuni === true, 'about written by Muni from the quests', JSON.stringify(b.about));
  expect(b.contact?.discoverable === true && b.contact?.invitesBy === 'Email · WhatsApp' && b.contact?.mobileVerified === true, 'contact from Q2/Q8', JSON.stringify(b.contact));
  expect(b.contact?.city === 'Mumbai' && b.contact?.location === 'Fort, Mumbai 400001', 'location from Quest 2', JSON.stringify(b.contact));
  const lf = Object.fromEntries((b.lookingFor ?? []).map(x => [x.label, x.value]));
  expect(lf['Looking for'] === 'Full-time · Internship' && lf['Where'] === 'Mumbai + 25 km · open to remote' && lf['Shifts'] === 'Day · Rotational' &&
    lf['Can join'] === 'Immediately' && lf['Expected pay'] === '₹3 – 4.5 LPA', 'looking-for rows from Quest 6', JSON.stringify(b.lookingFor));
  expect(b.headline === 'Customer support executive · BPO · voice process', 'headline from the arc roles', b.headline);

  const cvc = await call(a, 'GET', 'job-profile/cv-contact', undefined, 200);
  expect(cvc.body?.email === email && cvc.body?.location === 'Fort, Mumbai' && cvc.body?.expectedPay === '₹3 – 4.5 LPA', 'cv-contact from the quests', JSON.stringify(cvc.body));
  const about = await call(a, 'PUT', 'job-profile/about', { text: 'Calm on tough calls and quick on Excel.' }, 200);
  expect(about.body?.about?.text === 'Calm on tough calls and quick on Excel.' && about.body?.about?.byMuni === false, 'about rewritten by the learner', JSON.stringify(about.body?.about));
  await upload(a, 'job-profile/cv', 'file', PDF, 'priya-cv.pdf', 'application/pdf', 200, 'newer CV');
  await call(a, 'POST', 'job-profile/cv/apply', { ids: [] }, 200);
  const share = await call(a, 'GET', 'job-profile/share', undefined, 200);
  expect(/\/cv\/priya-[a-z0-9]+$/.test(share.body?.url ?? ''), 'share link names the learner', share.body?.url);
  const share2 = await call(a, 'PUT', 'job-profile/share', { hideContact: false, expiresDays: 90 }, 200);
  expect(share2.body?.hideContact === false && share2.body?.expiresDays === 90 && share2.body?.url === share.body?.url, 'share settings saved, same link', JSON.stringify(share2.body));

  // ───────────────────────── Profile (Player ID) ─────────────────────────
  const prof = await call(a, 'GET', 'profile', undefined, 200);
  expect(prof.body?.name === 'Priya Sharma', 'profile name follows Quest 1', prof.body?.name);
  const photoTask = (prof.body?.strength?.tasks ?? []).find(t => t.id === 'photo');
  expect(photoTask?.done === false && photoTask?.xp === 5, 'photo task open, +5', JSON.stringify(photoTask));
  const edit = await call(a, 'GET', 'profile/edit', undefined, 200);
  expect(Array.isArray(edit.body?.skillOptions) && edit.body.skillOptions.length > 0, 'profile/edit has skill options');
  const put1 = await call(a, 'PUT', 'profile', { skills: ['Customer service', 'MS Excel', 'Hindi'], gender: 'female', dob: '2004-08-21' }, 200);
  expect(put1.body?.xpEarned === 10, 'first top skills +10 (BR-109)', String(put1.body?.xpEarned));
  const put2 = await call(a, 'PUT', 'profile', { skills: ['Customer service', 'Sales'], gender: 'female', dob: '2004-08-21' }, 200);
  expect(put2.body?.xpEarned === 0, 'editing skills again pays 0', String(put2.body?.xpEarned));
  await call(a, 'PUT', 'profile', { skills: ['a', 'b', 'c', 'd', 'e', 'f'], gender: 'female', dob: '2004-08-21' }, [400, 422], 'more than 5 skills');
  await call(a, 'PUT', 'profile', { skills: [], gender: 'female', dob: '2999-01-01' }, 422, 'dob-in-future');
  const ph1 = await upload(a, 'profile/photo', 'photo', PNG, 'me.png', 'image/png', 200);
  expect(ph1.body?.xpEarned === 5 && !!ph1.body?.profile?.photoUrl, 'first photo +5 (BR-108)', JSON.stringify({ xp: ph1.body?.xpEarned, url: ph1.body?.profile?.photoUrl }));
  const ph2 = await upload(a, 'profile/photo', 'photo', PNG, 'me2.png', 'image/png', 200, 'second photo');
  expect(ph2.body?.xpEarned === 0, 'a second photo pays 0', String(ph2.body?.xpEarned));
  const rev = await call(a, 'POST', 'profile/reveal', { field: 'email' }, 200);
  expect(rev.body?.value === email, 'reveal email', rev.body?.value);
  const revDob = await call(a, 'POST', 'profile/reveal', { field: 'dob' }, 200);
  expect(revDob.body?.value === '21 Aug 2004', 'reveal date of birth', revDob.body?.value);
  const newEmail = `new.${email}`;
  await call(a, 'POST', 'profile/contact/code', { kind: 'email', value: 'not-an-email' }, 422, 'invalid email');
  const cc = await call(a, 'POST', 'profile/contact/code', { kind: 'email', value: newEmail }, 200);
  expect(typeof cc.body?.sentTo === 'string', 'contact code sentTo');
  await call(a, 'POST', 'profile/contact/verify', { kind: 'email', value: newEmail, code: '000001' }, 422, 'wrong code');
  await call(a, 'POST', 'profile/contact/verify', { kind: 'email', value: newEmail, code: ctx.CODE }, [200, 204]);

  // ───────────────────────── Programme bonus quest (BR-113) ─────────────────────────
  const CODE_IN = 'SKM-MUM-26-0417';
  const prog = await call(a, 'GET', `programmes/${CODE_IN}`, undefined, 200, 'seeded sample code');
  expect(prog.body?.code === CODE_IN && prog.body?.marksFrom === '12' && prog.body?.minEnglish === 50, 'programme details', JSON.stringify(prog.body));
  await call(a, 'GET', `programmes/${encodeURIComponent(CODE_IN.toLowerCase())}`, undefined, 200, 'codes are case-insensitive');
  const ifsc = await call(a, 'GET', 'banks/ifsc/HDFC0000240', undefined, [200, 503], 'Razorpay IFSC directory may be unreachable');
  if (ifsc.status === 200) expect(typeof ifsc.body?.bank === 'string' && ifsc.body.bank.length > 0, 'IFSC bank name');
  await call(a, 'GET', 'banks/ifsc/NOT-AN-IFSC', undefined, 422, 'invalid-ifsc');
  const submission = {
    code: CODE_IN, marksFrom: '12', english: '72', maths: '65', proficiency: { English: 'good', Excel: 'okay' },
    bank: { holder: 'Priya Sharma', type: 'savings', account: '50100123456712', routing: 'HDFC0000240', upi: '' },
  };
  await call(a, 'POST', 'me/programme', { ...submission, english: '40' }, 422, 'marks-too-low');
  await call(a, 'POST', 'me/programme', { ...submission, bank: null }, 422, 'bank-required (stipend)');
  const enrol = await call(a, 'POST', 'me/programme', submission, 200);
  expect(enrol.body?.xpEarned === 100 && enrol.body?.accountTail === '12', 'programme +100, account tail', JSON.stringify(enrol.body));
  await call(a, 'POST', 'me/programme', submission, 409, 'already-enrolled');

  // ───────────────────────── Feedback, drafts, preferences ─────────────────────────
  const fb1 = await call(a, 'POST', 'me/feedback', { mood: 5, tags: ['Easy to use'], comment: 'Loved the quests!', screen: '/quests/vibe-check' }, 200);
  expect(fb1.body?.xp === 5, 'feedback +5 (BR-114)', JSON.stringify(fb1.body));
  const fb2 = await call(a, 'POST', 'me/feedback', { mood: 4, tags: [], comment: '', screen: '/profile' }, 200, 'second within 30 days');
  expect(fb2.body?.xp === 0, 'second feedback within 30 days pays 0', JSON.stringify(fb2.body));
  await call(a, 'POST', 'me/feedback', { mood: 9, tags: [], comment: '', screen: '' }, [400, 422], 'mood out of range');

  await call(a, 'PUT', 'drafts/quest-1-part-1', { firstName: 'Priya', lastName: 'Sha' }, 204, 'extra · drafts autosave');
  const draft = await call(a, 'GET', 'drafts/quest-1-part-1', undefined, 200, 'extra · drafts');
  expect(draft.body?.lastName === 'Sha', 'draft read back', JSON.stringify(draft.body));
  await call(a, 'GET', 'drafts/never-saved', undefined, 404, 'extra · drafts draft-not-found');
  const prefs = await call(a, 'GET', 'me/preferences', undefined, 200, 'extra · preferences');
  expect(prefs.body?.sounds === true && prefs.body?.language === 'en', 'default preferences', JSON.stringify(prefs.body));
  const prefs2 = await call(a, 'PUT', 'me/preferences', { sounds: false, voice: true, language: 'hi' }, 200, 'extra · preferences');
  expect(prefs2.body?.sounds === false && prefs2.body?.language === 'hi', 'preferences saved', JSON.stringify(prefs2.body));

  await call(a, 'DELETE', 'me/identity', undefined, 204);
  const unlinked = await a.call('GET', 'me/identity');
  r.record(j, 'GET', 'me/identity', unlinked, 200, 'after unlink');
  expect(unlinked.body === null, 'me/identity is null after unlink', unlinked.text);

  // All extras land in the one ledger: 810 + 10 skills + 5 photo + 100 programme + 5 feedback = 930.
  const after = await progressUntil(a, x => x?.xp >= 930);
  expect(after?.xp === 930, 'me/progress after profile, programme and feedback = 930', JSON.stringify(after));
  const items2 = await ledger(a);
  expect(sum(items2, 'profile.task') === 15, 'profile tasks paid once each (+5, +10)', String(sum(items2, 'profile.task')));
  expect(sum(items2, 'programme.enrolled') === 100, 'programme +100 once', String(sum(items2, 'programme.enrolled')));
  ctx.state.questsLearner = a;

  // ───────────────────────── Learner B: skips every optional step (680) ─────────────────────────
  const s = await ctx.newLearner(j, 'Ravi', 'Kumar');
  const mobileB = `8${T}${Math.floor(Math.random() * 10)}`;
  await part(s, 'who-dis', 1, { firstName: 'Ravi', lastName: 'Kumar', nickname: '', nameSkipped: false });
  await part(s, 'who-dis', 2, { idType: 'aadhaar', idNumber: '', birthday: '02 / 01 / 2003', idMasked: 'XXXX XXXX 7731', idLast4: '7731', idVia: 'digilocker', idSkipped: false });
  await part(s, 'who-dis', 3, { gender: 'male', genderText: '', homeLanguage: 'Tamil', homeLanguageOther: '', homeLanguageText: '', race: '', disability: 'rather-not', disabilityTypes: [], disabilityOther: '', disabilityPercent: '', disabilityCertificate: '', disabilitySupport: [], teaSkipped: false });
  await complete(s, 'who-dis', QUEST_XP);
  await call(s, 'POST', 'me/phone/code', { phone: `+91${mobileB}` }, 200);
  await call(s, 'POST', 'me/phone/verify', { phone: `+91${mobileB}`, code: ctx.CODE }, [200, 204]);
  await part(s, 'hit-me-up', 1, { phone: mobileB, phoneVerified: true, email: s.email, whatsapp: false, digitsSkipped: false });
  await part(s, 'hit-me-up', 2, { backupSkipped: true }, true, false); // "Skip for now"
  await part(s, 'hit-me-up', 3, { postal: '560038', region: 'Karnataka', district: 'Bengaluru Urban', city: 'Bengaluru', area: 'Indiranagar', line1: '42 CMH Road', unit: '', landmark: '', spotSkipped: false });
  await complete(s, 'hit-me-up', QUEST_XP);
  await part(s, 'big-brain-stuff', 1, { level: '12th', status: 'completed', levelSkipped: false });
  await part(s, 'big-brain-stuff', 2, { degree: '', branch: '', institute: '', board: 'CBSE', year: '2021', studyYear: '', scoreKind: 'percentage', score: '81', detailsSkipped: false });
  await part(s, 'big-brain-stuff', 3, { timelineSkipped: true }, true, false);
  await complete(s, 'big-brain-stuff', QUEST_XP);
  const deckB = [
    { id: 'teamwork', name: 'Teamwork', category: 'people', level: 2, source: 'suggested' },
    { id: 'social-media', name: 'Social media', category: 'creative', level: 2, source: 'suggested' },
    { id: 'sales', name: 'Sales', category: 'business', level: 1, source: 'suggested' },
  ];
  await part(s, 'your-flex-era', 1, { skills: deckB, deckSkipped: false });
  await part(s, 'your-flex-era', 2, { languages: [{ name: 'Tamil', fluency: 'native', speak: true, read: true, write: true, motherTongue: true, fromCv: false }], passportSkipped: false });
  await part(s, 'your-flex-era', 3, { trophiesSkipped: true }, true, false);
  await complete(s, 'your-flex-era', QUEST_XP);
  await part(s, 'work-lore', 1, { fresher: true, workTypes: [], starters: ['project'], typesSkipped: false });
  const chB = [{ id: 'p1', type: 'project', role: 'College fest volunteer lead', company: 'Christ University', start: '2022-07', end: '2022-12', current: false, mode: 'onsite', skills: ['teamwork'], description: '' }];
  await part(s, 'work-lore', 2, { chapters: chB, chapterDraft: null, chapterDrafts: null, chapterSkipped: false });
  await part(s, 'work-lore', 3, { chapters: chB, gapReasons: {}, timelineSkipped: false });
  await complete(s, 'work-lore', QUEST_XP);
  await part(s, 'pick-your-arc', 1, { lookingFor: ['full_time'], roles: ['Retail associate'], where: ['near', 'relocate'], shifts: [], notice: '15', salary: '', arcSkipped: false });
  await complete(s, 'pick-your-arc', QUEST_XP);
  await part(s, 'drop-the-receipts', 1, { receipts: {}, receiptsSkipped: false }, true, false); // Next with nothing added
  await complete(s, 'drop-the-receipts', QUEST_XP);
  await part(s, 'vibe-check', 1, { discoverable: true, invites: false, vibeSkipped: false });
  await call(s, 'POST', 'me/profile/publish', { discoverable: true, invites: false }, [200, 204], 'quest-page finish(): publish');
  await complete(s, 'vibe-check', QUEST_XP);
  // 18 steps − 4 unpaid optional ones = 14 × 20 + 8 × 50 = 680; the Job Profile isn't 100% (3 skipped) so no mission.
  const doneB = await progressUntil(s, x => x?.completed === 8 && x?.xp >= 680);
  await sleep(3000);
  const finalB = await progressUntil(s, x => x?.completed === 8 && x?.xp >= 680);
  expect(doneB?.xp === 680 && finalB?.xp === 680 && finalB?.level === 2 && finalB?.rank === 'Pathfinder' && finalB?.completed === 8,
    'skipping every optional step: 680 XP, level 2 Pathfinder', JSON.stringify(finalB));
  const jpB = await call(s, 'GET', 'job-profile', undefined, 200, 'skipper');
  const leftB = (jpB.body?.byQuest ?? []).flatMap(q => q.left.map(x => `${q.slug}:${x.key}`));
  expect(JSON.stringify(leftB) === JSON.stringify(['hit-me-up:backup', 'big-brain-stuff:timeline', 'your-flex-era:trophies']),
    'skipped steps are what is left (keys as the quest screens)', JSON.stringify(leftB));
  expect(jpB.body?.strength?.percent === 83, 'skipper completeness 15/18 = 83%', JSON.stringify(jpB.body?.strength?.percent));
  expect(jpB.body?.contact?.invitesBy === 'Invites off', 'invites off → no invite channels', JSON.stringify(jpB.body?.contact?.invitesBy));
  const lfB = Object.fromEntries((jpB.body?.lookingFor ?? []).map(x => [x.label, x.value]));
  expect(lfB['Can join'] === '15 days' && lfB['Where'] === 'Bengaluru + 25 km · open to relocating', 'skipper looking-for labels', JSON.stringify(jpB.body?.lookingFor));
  expect(jpB.body?.education?.[0]?.title === '12th' && jpB.body?.education?.[0]?.school === 'CBSE', 'school-level education', JSON.stringify(jpB.body?.education));

  // ───────────────────────── Programme cool-down (Figma P4): 5 wrong codes → 429 ─────────────────────────
  for (let i = 1; i <= 5; i++) await call(s, 'GET', `programmes/SKM-NOPE-${i}`, undefined, 404, `wrong code ${i}`);
  await call(s, 'GET', 'programmes/SKM-NOPE-6', undefined, 429, 'too-many-tries');
  await call(s, 'GET', `programmes/${CODE_IN}`, undefined, 429, 'even a valid code waits out the cool-down');
  // A second programme never pays twice (+100 once per learner, BR-113).
  const c2 = await ctx.newLearner(j, 'Neha', 'Patel');
  const enrolZa = await call(c2, 'POST', 'me/programme', { code: 'SKM-JHB-26-0102', marksFrom: '12', english: '60', maths: '50', proficiency: {}, bank: { holder: 'Neha Patel', type: 'savings', account: '62012345678', routing: 'SBIN0005943', upi: '' } }, 200, 'a ZA sample code');
  expect(enrolZa.body?.xpEarned === 100, 'first programme +100', String(enrolZa.body?.xpEarned));
  const enrolIn = await call(c2, 'POST', 'me/programme', submission, 200, 'a second programme');
  expect(enrolIn.body?.xpEarned === 0, 'a second programme pays 0 (once per learner)', String(enrolIn.body?.xpEarned));
};
