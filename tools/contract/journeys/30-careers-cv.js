'use strict';
// Careers, CV and the Gamification mission XP (BRD §3.5–3.8, §4: BR-60–72, BR-110–115; jobs/fest/launchpad/matches/cv
// handlers). One new learner, in the order the app walks them: CV onboarding → Launchpad → My Jobs (apply, +100 once,
// withdraw, cool-off) → Hire Fest (60 % gate, quests, book, +100 once, cancel) → Launchpad again → My Matches.

const sleep = ms => new Promise(res => setTimeout(res, ms));

/** A one-page PDF with real text, like a learner's exported CV. */
function samplePdf(pad = 0) {
  const text = 'BT /F1 12 Tf 72 720 Td (Asha Rao - Customer Support - Pune) Tj ET';
  const objs = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
    `<< /Length ${text.length} >>\nstream\n${text}\nendstream`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
  ];
  let out = '%PDF-1.4\n';
  const offsets = [];
  objs.forEach((o, i) => { offsets.push(out.length); out += `${i + 1} 0 obj\n${o}\nendobj\n`; });
  if (pad) out += `%${'x'.repeat(pad)}\n`;
  const xref = out.length;
  out += `xref\n0 ${objs.length + 1}\n0000000000 65535 f \n${offsets.map(o => String(o).padStart(10, '0') + ' 00000 n \n').join('')}`;
  out += `trailer\n<< /Size ${objs.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return Buffer.from(out, 'latin1');
}

/** A 1×1 PNG: a photographed CV page as far as the type check is concerned. */
const samplePng = () => Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==', 'base64');

function form(field, files) {
  const fd = new FormData();
  for (const [name, type, bytes] of files) fd.append(field, new Blob([bytes], { type }), name);
  return fd;
}

module.exports = async (ctx, j) => {
  const r = ctx.report;
  const problem = (call, msg) => r.rows.push({ journey: j, call, status: 0, ms: 0, problems: [msg], note: 'check' });
  const expectThat = (cond, call, msg) => { if (!cond) problem(call, msg); return !!cond; };
  const c = await ctx.newLearner(j, 'Asha', 'Rao');
  const call = async (verb, path, body, expect, note = '', opts) => r.record(j, verb, path, await c.call(verb, path, body, opts), expect, note);
  const code = res => res.body?.errors?.code?.[0];
  const qs = q => new URLSearchParams({ q: q.q ?? '', place: q.place ?? '', sort: q.sort ?? '', limit: String(q.limit ?? 20), filters: JSON.stringify(q.filters ?? {}) }).toString();

  /** Polls `fn` until `ok(result)` or ~30 s (events are asynchronous). */
  async function poll(fn, ok, ms = 30000) {
    const end = Date.now() + ms;
    let last;
    for (;;) {
      last = await fn();
      if (ok(last) || Date.now() > end) return last;
      await sleep(1000);
    }
  }
  const ledger = async () => (await c.call('GET', 'me/xp/ledger?page=1&pageSize=100')).body?.items ?? [];
  const missionCredits = (items, key) => items.filter(e => `${e.reason}:${e.sourceId}`.includes(key));
  const xpNow = async () => (await c.call('GET', 'me/progress')).body?.xp ?? 0;

  /* ---------- CV onboarding (cv.api.ts) ---------- */
  // The CV upload's frontend type is the progress stream it builds; the HTTP body is { cvId } (cv.api.ts maps it).
  const upload = async (fd, expect, note) => {
    const res = r.record(j, 'POST', 'cv', await c.call('POST', 'cv', fd), expect, note);
    const row = r.rows[r.rows.length - 1];
    row.problems = row.problems.filter(p => !p.startsWith('$'));
    if (res.status === 201) expectThat(typeof res.body?.cvId === 'string' && res.body.cvId.length > 0, 'POST cv', `body lacks cvId: ${res.text.slice(0, 120)}`);
    return res;
  };

  const cv = await upload(form('file', [['Asha_Rao_CV.pdf', 'application/pdf', samplePdf()]]), 201, 'PDF CV');
  const cvId = cv.body?.cvId;
  if (cvId) {
    const done = await poll(() => c.call('GET', `cv/${cvId}/status`), s => s.body?.state !== 'parsing');
    r.record(j, 'GET', `cv/${cvId}/status`, done, 200, 'polled until parsed');
    expectThat(done.body?.state === 'done' && done.body?.percent === 100, `GET cv/${cvId}/status`, `parse ended ${JSON.stringify(done.body)}`);
    const sum = await call('GET', `cv/${cvId}/summary`, undefined, 200);
    expectThat(sum.body?.detailsFound > 0 && sum.body?.sections?.length > 0, `GET cv/${cvId}/summary`, `nothing found: ${JSON.stringify(sum.body).slice(0, 200)}`);
  }

  // F1: photographed pages travel together as `pages`.
  const photo = await upload(form('pages', [['page-1.png', 'image/png', samplePng()], ['page-2.png', 'image/png', samplePng()]]), 201, 'photographed pages');
  const photoId = photo.body?.cvId;
  if (photoId) {
    const st = await poll(() => c.call('GET', `cv/${photoId}/status`), s => s.body?.state !== 'parsing');
    r.record(j, 'GET', `cv/${photoId}/status`, st, 200, 'photo parse');
    expectThat(['done', 'failed'].includes(st.body?.state), `GET cv/${photoId}/status`, `still ${st.body?.state}`);
    await call('DELETE', `cv/${photoId}`, undefined, 204);
    await call('GET', `cv/${photoId}/status`, undefined, 404, 'deleted CV is gone');
    await call('GET', `cv/${photoId}/summary`, undefined, 404, 'deleted CV is gone');
  }
  const s1 = await upload(form('file', [['notes.txt', 'text/plain', Buffer.from('just some text, not a CV')]]), 422, 'S1 unsupported-type');
  expectThat(code(s1) === 'unsupported-type', 'POST cv (S1)', `code ${code(s1)}`);
  const s2 = await upload(form('file', [['Big_CV.pdf', 'application/pdf', samplePdf(10 * 1024 * 1024 + 4096)]]), 422, 'S2 too-large');
  expectThat(code(s2) === 'too-large', 'POST cv (S2)', `code ${code(s2)}`);
  await call('GET', 'cv/00000000-0000-0000-0000-000000000001/status', undefined, 404, 'unknown CV');

  /* ---------- Launchpad, before anything (launchpad.api.ts) ---------- */
  const ready0 = await call('GET', 'launchpad/readiness', undefined, 200);
  expectThat(ready0.body?.level === 0 && ready0.body?.percent === 0, 'GET launchpad/readiness', `new learner ${JSON.stringify(ready0.body)}`);
  expectThat(JSON.stringify(ready0.body?.missions?.map(m => [m.id, m.xp])) === JSON.stringify([['assessment', 100], ['profile', 50], ['apply', 100], ['fest', 100]]),
    'GET launchpad/readiness', `missions ${JSON.stringify(ready0.body?.missions)}`);
  await call('GET', 'launchpad/profile', undefined, 200);
  const asmt = await call('GET', 'launchpad/assessment', undefined, 200);
  expectThat(asmt.body?.status === 'not-started', 'GET launchpad/assessment', `status ${asmt.body?.status}`);
  await call('GET', 'launchpad/jobs', undefined, 200);
  await call('GET', 'launchpad/fest', undefined, 200);
  const sectors = await call('GET', 'launchpad/sectors', undefined, 200);
  expectThat(Array.isArray(sectors.body) && sectors.body.length > 0 && sectors.body.every(s => s.fit === null), 'GET launchpad/sectors', 'fit must be null before the assessment');

  /* ---------- My Jobs (jobs.api.ts, toParams) ---------- */
  const sum0 = await call('GET', 'jobs/summary', undefined, 200);
  expectThat(sum0.body?.available === 128 && sum0.body?.unlockAt === 0 && sum0.body?.applied === 0, 'GET jobs/summary', `summary ${JSON.stringify(sum0.body)}`);
  const place = sum0.body?.defaultPlace ?? '';
  const avail = await call('GET', `jobs/list/available?${qs({ place, sort: 'relevant' })}`, undefined, 200);
  expectThat(avail.body?.items?.length <= 20 && avail.body?.total > 0 && avail.body?.facets?.length > 0, 'GET jobs/list/available', `list ${avail.body?.items?.length}/${avail.body?.total}`);
  const countOnly = await call('GET', `jobs/list/available?${qs({ place, sort: 'relevant', limit: 0 })}`, undefined, 200, 'count-only (limit 0)');
  expectThat(countOnly.body?.items?.length === 0 && countOnly.body?.total === avail.body?.total, 'GET jobs/list/available (limit 0)', `items ${countOnly.body?.items?.length}, total ${countOnly.body?.total} vs ${avail.body?.total}`);
  const all = await call('GET', `jobs/list/available?${qs({ sort: 'newest', limit: 200 })}`, undefined, 200, 'no place');
  expectThat(all.body?.total === 128, 'GET jobs/list/available (all)', `total ${all.body?.total}`);
  const bpo = await call('GET', `jobs/list/available?${qs({ sort: 'salary', filters: { sector: ['BPO / Contact centre'], workMode: ['onsite', 'hybrid'] } })}`, undefined, 200, 'sector + work mode');
  expectThat(bpo.body?.items?.every(x => x.sector === 'BPO / Contact centre' && x.workMode !== 'remote'), 'GET jobs/list/available (filters)', 'filter not applied');
  const none = await call('GET', `jobs/list/available?${qs({ q: 'zzqx-no-such-job', sort: 'relevant' })}`, undefined, 200, 'no hits');
  expectThat(none.body?.total === 0 && none.body?.items?.length === 0, 'GET jobs/list/available (no hits)', `total ${none.body?.total}`);
  await call('GET', `jobs/list/matching?${qs({ sort: 'match' })}`, undefined, 200);
  await call('GET', `jobs/list/bookmarked?${qs({ sort: 'saved' })}`, undefined, 200);
  await call('GET', `jobs/list/unknown?${qs({})}`, undefined, 404, 'unknown tab');
  await call('GET', `jobs/invitations?${qs({ sort: 'respond' })}`, undefined, 200);
  await call('GET', `jobs/applications?${qs({ sort: 'updated' })}`, undefined, 200);

  const job = await call('GET', 'jobs/job/job-1', undefined, 200);
  expectThat(job.body?.id === 'job-1' && job.body?.saved === false && job.body?.appliedOn === null, 'GET jobs/job/job-1', `job ${JSON.stringify(job.body).slice(0, 200)}`);
  await call('GET', 'jobs/job/job-999', undefined, 404, 'unknown job');
  const saved = await call('PUT', 'jobs/job/job-1/saved', { saved: true }, 200);
  expectThat(saved.body?.saved === true && saved.body?.savedDays === 0, 'PUT jobs/job/job-1/saved', `saved ${saved.body?.saved}, savedDays ${saved.body?.savedDays}`);
  const marks = await call('GET', `jobs/list/bookmarked?${qs({ sort: 'closing' })}`, undefined, 200, 'after saving');
  expectThat(marks.body?.total === 1 && marks.body?.items?.[0]?.id === 'job-1' && typeof marks.body?.note === 'string', 'GET jobs/list/bookmarked', `bookmarked ${marks.body?.total}`);
  await call('PUT', 'jobs/job/job-3/saved', { saved: true }, 200);
  const unsaved = await call('PUT', 'jobs/job/job-3/saved', { saved: false }, 200);
  expectThat(unsaved.body?.saved === false && unsaved.body?.savedDays === null, 'PUT jobs/job/job-3/saved', 'unsave');

  const info = await call('GET', 'jobs/job/job-1/apply', undefined, 200);
  expectThat(info.body?.questions?.length === 2 && info.body?.name?.length > 0, 'GET jobs/job/job-1/apply', `apply info ${JSON.stringify(info.body).slice(0, 200)}`);
  const up = await call('POST', 'jobs/cv', form('file', [['Asha_Rao_CV.pdf', 'application/pdf', samplePdf()]]), [200, 201]);
  expectThat(/KB$/.test(up.body?.meta ?? ''), 'POST jobs/cv', `meta ${up.body?.meta}`);
  const badUp = await call('POST', 'jobs/cv', form('file', [['photo.png', 'image/png', samplePng()]]), 422, 'not a PDF/Word file');
  expectThat(code(badUp) === 'unsupported-type', 'POST jobs/cv (type)', `code ${code(badUp)}`);
  const info2 = await call('GET', 'jobs/job/job-1/apply', undefined, 200, 'uploaded CV listed');
  expectThat(info2.body?.cvs?.some(x => x.id === up.body?.id), 'GET jobs/job/job-1/apply', 'uploaded CV not offered');

  const prog = await call('GET', 'me/progress', undefined, 200, 'before applying');
  const xpBeforeApply = prog.body?.xp ?? 0;
  const answers = { q1: 'Yes', q2: 'Immediately' };
  const app1 = await call('POST', 'jobs/job/job-1/apply', { cvId: up.body?.id, answers }, [200, 201]);
  expectThat(app1.body?.status === 'applied' && app1.body?.answers?.length === 2 && app1.body?.job?.appliedOn, 'POST jobs/job/job-1/apply', `application ${JSON.stringify(app1.body).slice(0, 200)}`);
  const again = await call('POST', 'jobs/job/job-1/apply', { cvId: up.body?.id, answers }, 409, 'already-applied');
  expectThat(code(again) === 'already-applied', 'POST jobs/job/job-1/apply (again)', `code ${code(again)}`);
  await call('POST', 'jobs/job/job-2/apply', { cvId: 'not-a-cv', answers: {} }, 422, 'unknown CV');
  // BR-111: +100 on the first application, once.
  const firstApp = await poll(ledger, l => missionCredits(l, 'first-application').length > 0);
  expectThat(missionCredits(firstApp, 'first-application').length === 1 && missionCredits(firstApp, 'first-application')[0].xp === 100, 'GET me/xp/ledger',
    `first application credit: ${JSON.stringify(missionCredits(firstApp, 'first-application'))}`);
  const xpAfterApply = await poll(xpNow, x => x >= xpBeforeApply + 100);
  expectThat(xpAfterApply >= xpBeforeApply + 100, 'GET me/progress', `xp ${xpBeforeApply} → ${xpAfterApply} after the first application`);
  const app2 = await call('POST', 'jobs/job/job-2/apply', { cvId: up.body?.id, answers: { q1: 'No' } }, [200, 201], 'second application');
  await sleep(6000);
  expectThat(missionCredits(await ledger(), 'first-application').length === 1, 'GET me/xp/ledger', 'second application must not pay the mission again');

  const apps = await call('GET', `jobs/applications?${qs({ sort: 'updated' })}`, undefined, 200, 'after applying');
  expectThat(apps.body?.total === 2, 'GET jobs/applications', `total ${apps.body?.total}`);
  const appsFiltered = await call('GET', `jobs/applications?${qs({ sort: 'newest', filters: { status: ['applied'], dateApplied: ['7'] } })}`, undefined, 200, 'status filter');
  expectThat(appsFiltered.body?.total === 2, 'GET jobs/applications (filters)', `total ${appsFiltered.body?.total}`);
  if (app1.body?.id) await call('GET', `jobs/applications/${app1.body.id}`, undefined, 200);
  await call('GET', 'jobs/applications/00000000-0000-0000-0000-000000000001', undefined, 404);
  const sum1 = await call('GET', 'jobs/summary', undefined, 200, 'after applying');
  expectThat(sum1.body?.applied === 2 && sum1.body?.bookmarked === 1, 'GET jobs/summary', `applied ${sum1.body?.applied}, bookmarked ${sum1.body?.bookmarked}`);

  const ready1 = await call('GET', 'launchpad/readiness', undefined, 200, 'after applying');
  expectThat(ready1.body?.missions?.find(m => m.id === 'apply')?.done === true && ready1.body?.percent === 29 && ready1.body?.level === 1, 'GET launchpad/readiness', `after applying ${JSON.stringify(ready1.body)}`);
  const lpJobs = await call('GET', 'launchpad/jobs', undefined, 200, 'after applying');
  expectThat(lpJobs.body?.applied === 2 && lpJobs.body?.saved === 1 && lpJobs.body?.newMatches === 0, 'GET launchpad/jobs', `card ${JSON.stringify(lpJobs.body)}`);

  // BR-66 withdraw; BR-115 XP never decreases; 30-day cool-off before applying again.
  if (app1.body?.id) {
    const xpBeforeWithdraw = await xpNow();
    const w = await call('POST', `jobs/applications/${app1.body.id}/withdraw`, {}, 200);
    expectThat(w.body?.status === 'withdrawn' && w.body?.job?.appliedOn === null, 'POST jobs/applications/{id}/withdraw', `withdrawn ${w.body?.status}`);
    await sleep(3000);
    expectThat((await xpNow()) >= xpBeforeWithdraw, 'GET me/progress', 'XP went down after a withdrawal');
    const cool = await call('POST', 'jobs/job/job-1/apply', { cvId: up.body?.id, answers }, 409, 'reapply-cool-off');
    expectThat(code(cool) === 'reapply-cool-off', 'POST jobs/job/job-1/apply (cool-off)', `code ${code(cool)}`);
    expectThat(missionCredits(await ledger(), 'first-application').length === 1, 'GET me/xp/ledger', 'mission paid again after withdraw');
  }
  if (app2.body?.id) {
    const w2 = await call('POST', `jobs/applications/${app2.body.id}/withdraw`, {}, 200, 'withdraw the second one too');
    expectThat(w2.body?.status === 'withdrawn', 'POST jobs/applications/{id}/withdraw', 'second withdraw');
    const readyW = await call('GET', 'launchpad/readiness', undefined, 200, 'no active application');
    expectThat(readyW.body?.missions?.find(m => m.id === 'apply')?.done === false, 'GET launchpad/readiness', 'apply mission should untick with no active application (A-306)');
    await call('POST', 'jobs/job/job-4/apply', { cvId: up.body?.id, answers }, [200, 201], 'apply again elsewhere');
  }

  const alerts = await call('GET', 'jobs/alerts', undefined, 200);
  expectThat(['daily', 'weekly'].includes(alerts.body?.frequency), 'GET jobs/alerts', `frequency ${alerts.body?.frequency}`);
  const setAlerts = await call('PUT', 'jobs/alerts', { on: true, frequency: 'weekly', searches: ['Customer service · Pune'] }, 200);
  expectThat(setAlerts.body?.frequency === 'weekly' && setAlerts.body?.searches?.[0] === 'Customer service · Pune', 'PUT jobs/alerts', 'not saved');
  await call('PUT', 'jobs/alerts', { on: true, frequency: 'hourly', searches: [] }, 422, 'bad frequency');
  await call('GET', 'jobs/invitations/00000000-0000-0000-0000-000000000001', undefined, 404, 'no invitations for a new learner');
  await call('POST', 'jobs/invitations/00000000-0000-0000-0000-000000000001/accept', { note: '' }, 404);

  /* ---------- Hire Fest (fest.api.ts) ---------- */
  const fs0 = await call('GET', 'fests/summary', undefined, 200);
  expectThat(fs0.body?.unlockAt === 60 && fs0.body?.profilePercent < 60 && fs0.body?.missing?.length > 0 && fs0.body?.bookings === 0, 'GET fests/summary', `gate ${JSON.stringify(fs0.body).slice(0, 300)}`);
  const fests = await call('GET', 'fests?filter=all', undefined, 200);
  expectThat(fests.body?.length > 0, 'GET fests', 'no fests');
  for (const f of ['free', 'in-person', 'online', 'city']) {
    const res = await call('GET', `fests?filter=${f}`, undefined, 200, `filter ${f}`);
    if (f === 'online') expectThat(res.body?.every(x => x.mode === 'online'), `GET fests?filter=${f}`, 'filter not applied');
    if (f === 'free') expectThat(res.body?.every(x => x.free), `GET fests?filter=${f}`, 'filter not applied');
  }
  const festId = 'tech-bpo-pune';
  const fest = await call('GET', `fests/${festId}`, undefined, 200);
  expectThat(fest.body?.employers?.length > 0 && fest.body?.booked === false, `GET fests/${festId}`, 'fest detail');
  await call('GET', 'fests/no-such-fest', undefined, 404);
  const fsave = await call('PUT', `fests/${festId}/saved`, { saved: true }, 200);
  expectThat(fsave.body?.saved === true, `PUT fests/${festId}/saved`, 'not saved');
  await call('PUT', `fests/${festId}/saved`, { saved: false }, 200);

  const pickable = (fest.body?.employers ?? []).filter(e => !e.full && e.slotsLeft > 0).map(e => e.id);
  const [empA, empB] = pickable;
  const grid = await call('GET', `fests/${festId}/slots?employers=${encodeURIComponent([empA, empB].join(','))}`, undefined, 200);
  expectThat(grid.body?.maxEmployers === 2 && grid.body?.days?.length > 0, `GET fests/${festId}/slots`, `grid ${JSON.stringify(grid.body).slice(0, 200)}`);
  const free = emp => Object.entries(grid.body?.slots?.[emp] ?? {}).flatMap(([date, list]) => list.filter(s => s.left > 0).map(s => ({ employerId: emp, date, time: s.time })));
  const a = free(empA)[0];
  const minutes = t => Number(t.split(':')[0]) * 60 + Number(t.split(':')[1]);
  const b = free(empB).find(s => s.date !== a?.date || Math.abs(minutes(s.time) - minutes(a.time)) >= 120);
  const clash = free(empB).find(s => s.date === a?.date && Math.abs(minutes(s.time) - minutes(a.time)) < 30);
  const reminders = { whatsapp: true, sms: false, calendar: true };

  const gated = await call('POST', `fests/${festId}/book`, { picks: [a], reminders, cvTemplate: 'Classic' }, 422, 'BR-68: profile under 60 %');
  expectThat(code(gated) === 'profile-incomplete', `POST fests/${festId}/book`, `code ${code(gated)}`);

  // Fill Quests 1–4 the way the quest screens save them (12 of 18 steps = 67 %).
  const quests = [
    ['who-dis', [
      { firstName: 'Asha', lastName: 'Rao', nickname: 'Ash' },
      { idType: 'aadhaar', idNumber: '', birthday: '14/05/2003', idMasked: 'XXXX XXXX 4821', idLast4: '4821', idVia: 'digilocker' },
      { gender: 'female', genderText: '', homeLanguage: 'hi', homeLanguageOther: '', homeLanguageText: '', race: '', disability: 'no', disabilityTypes: [], disabilityOther: '', disabilityPercent: '' },
    ]],
    ['hit-me-up', [
      { phone: '+919876540697', phoneVerified: false, email: c.email, whatsapp: true },
      { relation: 'mother', relationText: '', backupName: 'Sunita Rao', backupPhone: '+919876500011', altEmail: '' },
      { postal: '411038', region: 'Maharashtra', district: 'Pune', city: 'Pune', area: 'Kothrud', line1: '12, Paud Road', unit: '', landmark: 'Near the metro' },
    ]],
    ['big-brain-stuff', [
      { level: 'graduate', status: 'completed' },
      { degree: 'B.Com', branch: 'Commerce', institute: 'Savitribai Phule Pune University', board: '', year: '2024', studyYear: '', scoreKind: 'percentage', score: '72' },
      { timeline: [{ id: 't1', level: 'class12', title: 'Class 12', institute: 'Abhinav Vidyalaya', year: '2021' }] },
    ]],
    ['your-flex-era', (() => {
      const deck = ['Customer service', 'Communication', 'English', 'Hindi', 'MS Excel', 'Typing', 'Data entry']
        .map(name => ({ id: name.toLowerCase().replace(/\W+/g, '-'), name, category: 'people', level: 2, source: 'suggested' }));
      return [
        { skills: deck },
        { languages: [{ name: 'Hindi', speak: true, read: true, write: true }, { name: 'English', speak: true, read: true, write: true }] },
        { trophies: [{ id: 'tr1', title: 'Skill India: Customer Care Executive', issuer: 'NSDC · Skill India', year: '2024', skills: ['Customer service'] }], skills: deck },
      ];
    })()],
  ];
  for (const [slug, parts] of quests) {
    for (let i = 0; i < parts.length; i++) {
      await call('PUT', `me/quests/${slug}/parts/${i + 1}`, { values: parts[i], done: true, xp: true }, [200, 204], 'extra: quest step');
    }
    await call('POST', `me/quests/${slug}/complete`, {}, 200);
  }
  const deck = await call('GET', 'me/quests/your-flex-era/answers', undefined, 200, 'skill deck saved');
  expectThat(deck.body?.skills?.length === 7, 'GET me/quests/your-flex-era/answers', `skills ${deck.body?.skills?.length}`);
  const fs1 = await poll(() => c.call('GET', 'fests/summary'), s => s.body?.profilePercent >= 60);
  r.record(j, 'GET', 'fests/summary', fs1, 200, 'after Quests 1–4');
  expectThat(fs1.body?.profilePercent >= 60 && fs1.body?.city === 'Pune', 'GET fests/summary', `after quests ${JSON.stringify(fs1.body).slice(0, 200)}`);
  const cityFests = await call('GET', 'fests?filter=city', undefined, 200, 'city = Pune now');
  expectThat(cityFests.body?.length > 0 && cityFests.body.every(f => f.city === 'Pune'), 'GET fests?filter=city', `city fests ${cityFests.body?.length}`);
  const lpProfile = await call('GET', 'launchpad/profile', undefined, 200, 'after quests');
  expectThat(lpProfile.body?.percent >= 60, 'GET launchpad/profile', `percent ${lpProfile.body?.percent}`);

  const xpBeforeBook = await xpNow();
  const booked = await call('POST', `fests/${festId}/book`, { picks: [a], reminders, cvTemplate: 'Classic' }, 201);
  const bookingId = booked.body?.id;
  expectThat(/^HF-[A-Z0-9]{4}-\d{4}$/.test(booked.body?.code ?? '') && booked.body?.interviews?.length === 1 && booked.body?.fest?.booked === true, `POST fests/${festId}/book`, `booking ${JSON.stringify(booked.body).slice(0, 200)}`);
  if (clash) {
    const ov = await call('POST', `fests/${festId}/book`, { picks: [clash], reminders, cvTemplate: 'Classic' }, 422, 'BR-69 overlap');
    expectThat(code(ov) === 'slot-overlap', `POST fests/${festId}/book (overlap)`, `code ${code(ov)}`);
  }
  if (b) {
    const added = await call('POST', `fests/${festId}/book`, { picks: [b], reminders, cvTemplate: 'Classic' }, 200, 'add a second employer');
    expectThat(added.body?.interviews?.length === 2 && added.body?.id === bookingId, `POST fests/${festId}/book (add)`, `interviews ${added.body?.interviews?.length}`);
  }
  const third = pickable.find(e => e !== empA && e !== empB);
  if (third) {
    const g3 = await c.call('GET', `fests/${festId}/slots?employers=${third}`);
    const s3 = Object.entries(g3.body?.slots?.[third] ?? {}).flatMap(([date, list]) => list.filter(s => s.left > 0).map(s => ({ employerId: third, date, time: s.time })))[0];
    if (s3) {
      const tm = await call('POST', `fests/${festId}/book`, { picks: [s3], reminders, cvTemplate: 'Classic' }, 422, 'max 2 employers');
      expectThat(code(tm) === 'too-many-employers', `POST fests/${festId}/book (3rd)`, `code ${code(tm)}`);
    }
  }
  // BR-112: +100 on the first booking, once.
  const firstFest = await poll(ledger, l => missionCredits(l, 'first-fest-booking').length > 0);
  expectThat(missionCredits(firstFest, 'first-fest-booking').length === 1 && missionCredits(firstFest, 'first-fest-booking')[0].xp === 100, 'GET me/xp/ledger',
    `first booking credit: ${JSON.stringify(missionCredits(firstFest, 'first-fest-booking'))}`);
  const xpAfterBook = await poll(xpNow, x => x >= xpBeforeBook + 100);
  expectThat(xpAfterBook >= xpBeforeBook + 100, 'GET me/progress', `xp ${xpBeforeBook} → ${xpAfterBook} after the first booking`);

  const list = await call('GET', 'fests/bookings', undefined, 200);
  expectThat(list.body?.length === 1 && list.body[0].id === bookingId, 'GET fests/bookings', `bookings ${list.body?.length}`);
  if (bookingId) await call('GET', `fests/bookings/${bookingId}`, undefined, 200);
  await call('GET', 'fests/bookings/00000000-0000-0000-0000-000000000001', undefined, 404);
  const fs2 = await call('GET', 'fests/summary', undefined, 200, 'after booking');
  expectThat(fs2.body?.bookings === 1, 'GET fests/summary', `bookings ${fs2.body?.bookings}`);

  const ready2 = await call('GET', 'launchpad/readiness', undefined, 200, 'after booking');
  expectThat(ready2.body?.missions?.find(m => m.id === 'fest')?.done === true && ready2.body?.percent === 57 && ready2.body?.level === 2, 'GET launchpad/readiness', `after booking ${JSON.stringify(ready2.body)}`);
  const lpFest = await call('GET', 'launchpad/fest', undefined, 200, 'after booking');
  expectThat(lpFest.body?.fest?.id === festId && typeof lpFest.body?.fest?.bookedSlot === 'string', 'GET launchpad/fest', `card ${JSON.stringify(lpFest.body)}`);

  const fullEmp = (fest.body?.employers ?? []).find(e => e.full)?.id ?? empA;
  const wl0 = await call('GET', `fests/${festId}/waitlist?employerId=${fullEmp}`, undefined, 200);
  expectThat(wl0.body?.position >= 1, `GET fests/${festId}/waitlist`, `position ${wl0.body?.position}`);
  const wl = await call('POST', `fests/${festId}/waitlist`, { employerId: fullEmp, auto: true, whatsapp: true }, 200);
  expectThat(wl.body?.position >= 1 && typeof wl.body?.odds === 'string', `POST fests/${festId}/waitlist`, 'waitlist');
  await call('POST', `fests/${festId}/waitlist`, { employerId: 'nobody', auto: false, whatsapp: false }, 422, 'unknown employer');

  if (bookingId && b) {
    const one = await call('POST', `fests/bookings/${bookingId}/cancel`, { employerId: b.employerId, reason: 'Clashes with college' }, 200, 'cancel one interview');
    expectThat(one.body?.interviews?.length === 1, `POST fests/bookings/${bookingId}/cancel`, 'one interview should remain');
  }
  if (bookingId) {
    await call('POST', `fests/bookings/${bookingId}/cancel`, { employerId: null, reason: '' }, [200, 204], 'cancel the whole booking');
    await call('GET', `fests/bookings/${bookingId}`, undefined, 404, 'cancelled');
    const after = await call('GET', 'fests/bookings', undefined, 200, 'after cancelling');
    expectThat(after.body?.length === 0, 'GET fests/bookings', 'booking still listed');
    await sleep(3000);
    expectThat(missionCredits(await ledger(), 'first-fest-booking').length === 1 && (await xpNow()) >= xpAfterBook, 'GET me/xp/ledger', 'cancelling must not change the mission XP');
    const rebook = await call('POST', `fests/${festId}/book`, { picks: [a], reminders, cvTemplate: 'Classic' }, 201, 'book again');
    await sleep(4000);
    expectThat(missionCredits(await ledger(), 'first-fest-booking').length === 1, 'GET me/xp/ledger', 'second booking paid the mission again');
    if (rebook.body?.id) ctx.state.careersBookingId = rebook.body.id;
  }

  const past = await call('GET', 'fests/past', undefined, 200);
  expectThat(Array.isArray(past.body) && past.body.length === 0, 'GET fests/past', 'a new learner has no past fests');
  // Outcomes and ratings need an attended (ended) fest; the seed fests are all upcoming.
  await call('PUT', `fests/past/${festId}/outcome`, { employerId: empA, outcome: 'offer' }, 404, 'fest not ended');
  await call('PUT', `fests/past/${festId}/rating`, { stars: 5 }, 404, 'fest not ended');
  await call('PUT', `fests/past/${festId}/rating`, { stars: 9 }, 422, 'stars 1–5');
  const fa = await call('GET', 'fests/alerts', undefined, 200);
  expectThat(typeof fa.body?.city === 'boolean', 'GET fests/alerts', 'alerts');
  const fa2 = await call('PUT', 'fests/alerts', { city: true, online: false }, 200);
  expectThat(fa2.body?.city === true && fa2.body?.online === false, 'PUT fests/alerts', 'not saved');
  const lpAlerts = await call('PUT', 'launchpad/fest/alerts', { on: true }, 200);
  expectThat(lpAlerts.body?.alertsOn === true, 'PUT launchpad/fest/alerts', 'alertsOn');
  const fa3 = await call('GET', 'fests/alerts', undefined, 200, 'after the launchpad toggle');
  expectThat(fa3.body?.city === true && fa3.body?.online === true, 'GET fests/alerts', 'launchpad toggle should switch both');

  /* ---------- My Matches (matches.api.ts) ---------- */
  const preview = await call('GET', 'me/matches/preview', undefined, 200);
  expectThat(preview.body?.count > 0 && preview.body?.top?.length > 0, 'GET me/matches/preview', `preview ${JSON.stringify(preview.body)}`);
  const board0 = await call('GET', 'me/matches', undefined, 200, 'before publishing');
  expectThat(board0.body?.visible === false && board0.body?.jobs?.length === 0, 'GET me/matches', `before publish ${JSON.stringify(board0.body).slice(0, 200)}`);
  await call('POST', 'me/profile/publish', { discoverable: true, invites: true }, [200, 204]);
  const board = await call('GET', 'me/matches', undefined, 200, 'published');
  expectThat(board.body?.visible === true && board.body?.jobs?.length > 0 && board.body?.profileComplete >= 60, 'GET me/matches', `after publish ${JSON.stringify(board.body).slice(0, 200)}`);
  const m = board.body?.jobs?.[0]?.id;
  if (m) {
    await call('POST', `me/matches/${m}/save`, { saved: true }, [200, 204]);
    const b1 = await c.call('GET', 'me/matches');
    expectThat(b1.body?.jobs?.find(x => x.id === m)?.status === 'saved', 'GET me/matches', 'save not reflected');
    await call('POST', `me/matches/${m}/apply`, {}, [200, 204]);
    await call('POST', `me/matches/${m}/accept`, {}, [200, 204]);
    const b2 = await call('GET', 'me/matches', undefined, 200, 'after accept');
    expectThat(b2.body?.jobs?.find(x => x.id === m)?.status === 'accepted', 'GET me/matches', 'accept not reflected');
  }
  await call('POST', 'me/matches/no-such-match/apply', {}, 404);

  /* ---------- Matching jobs now the skills are known ---------- */
  const match = await call('GET', `jobs/list/matching?${qs({ sort: 'match', filters: { match: ['60'] } })}`, undefined, 200, 'with skills');
  expectThat(match.body?.total > 0 && match.body.items.every(x => x.match?.percent >= 60), 'GET jobs/list/matching', `matching ${match.body?.total}`);
  const j1 = await call('GET', 'jobs/job/job-1', undefined, 200, 'with skills');
  expectThat(j1.body?.match?.percent > 0 && j1.body?.match?.skills?.length > 0, 'GET jobs/job/job-1', `match ${JSON.stringify(j1.body?.match)}`);
  const lpJobs2 = await call('GET', 'launchpad/jobs', undefined, 200, 'with skills');
  expectThat(lpJobs2.body?.matched > 0 && lpJobs2.body?.top !== null, 'GET launchpad/jobs', `card ${JSON.stringify(lpJobs2.body)}`);

  if (cvId) {
    await call('DELETE', `cv/${cvId}`, undefined, 204, 'remove the CV at the end');
    await call('DELETE', `cv/${cvId}`, undefined, 404, 'already deleted');
  }
};
