'use strict';
// Career Drivers, Learning, the Lobby's first moves, My Stats, badges and the Arena — driven like the Angular app
// (features/assessment, features/learn, features/home, features/stats, features/arena), then checked in Gamification:
// XP from the ledger, streak (B-45), daily quests (BR-118, B-44), first-moves bonus (BR-107), levels from the ladder.
// Gamification works from events (outbox → Service Bus → consumer), so its numbers are polled.

const LADDER = [
  { level: 1, min: 0, rank: 'Rookie' },
  { level: 2, min: 400, rank: 'Pathfinder' },
  { level: 3, min: 800, rank: 'Trailblazer' },
  { level: 4, min: 1200, rank: 'Legend' },
  { level: 5, min: 1600, rank: 'Icon' },
];

function levelFor(xp) {
  const step = [...LADDER].reverse().find(s => xp >= s.min);
  const next = LADDER.find(s => s.min > xp);
  return { level: step.level, rank: step.rank, target: next ? next.min : step.min + 400 };
}

const sleep = ms => new Promise(res => setTimeout(res, ms));

module.exports = async (ctx, j) => {
  const r = ctx.report;

  /** A business-rule check that isn't a status/shape problem. */
  const expect = (call, ok, problem) => {
    if (!ok) r.rows.push({ journey: j, call, status: 0, ms: 0, problems: [problem], note: 'rule' });
    return ok;
  };

  /** GET until `pred(body)` holds (events are asynchronous), up to ~30 s. Records the last response. */
  const poll = async (c, path, pred, note, timeoutMs = 30000) => {
    const started = Date.now();
    let res;
    for (;;) {
      res = await c.call('GET', path);
      let ok = false;
      try { ok = res.status === 200 && pred(res.body); } catch { ok = false; }
      if (ok || Date.now() - started > timeoutMs) {
        r.record(j, 'GET', path, res, 200, note);
        return { res, ok };
      }
      await sleep(1000);
    }
  };

  const ledgerAll = async (c, note) => {
    const res = r.record(j, 'GET', 'me/xp/ledger?page=1&pageSize=100', await c.call('GET', 'me/xp/ledger?page=1&pageSize=100'), 200, `extra: ${note}`);
    return res.body?.items ?? [];
  };
  const sumBy = (items, f) => items.filter(f).reduce((a, e) => a + e.xp, 0);

  const me = await ctx.newLearner(j, 'Kavya', 'Iyer');
  const rival = await ctx.newLearner(j, 'Rohan', 'Mehta');

  // ---------- Baseline ----------
  // The Player row is created from identity.learner.registered; wait for it so the totals below start from it.
  const base = await poll(me, 'me/progress', b => typeof b.xp === 'number', 'baseline');
  const xp0 = base.res.body?.xp ?? 0;
  expect('GET me/progress', xp0 === 0, `a new learner starts at 0 XP, got ${xp0}`);
  const lvl0 = levelFor(xp0);
  expect('GET me/progress', base.res.body?.level === lvl0.level && base.res.body?.rank === lvl0.rank && base.res.body?.target === lvl0.target,
    `level/rank/target ${JSON.stringify(base.res.body)} ≠ ladder ${JSON.stringify(lvl0)}`);

  // ---------- Lobby, first visit ----------
  let home = r.record(j, 'GET', 'home', await me.call('GET', 'home'), 200, 'new learner');
  const steps = home.body?.firstSteps ?? [];
  expect('GET home', steps.length === 3 && steps.every(s => !s.done), `3 first moves, none done; got ${JSON.stringify(steps.map(s => [s.id, s.done]))}`);
  expect('GET home', steps.find(s => s.id === 'assessment')?.xp === 100, 'the Career Drivers move offers 100 XP');
  expect('GET home', home.body?.firstMovesBonus?.xp === 50 && home.body?.firstMovesBonus?.claimed === false, `bonus { xp: 50, claimed: false }, got ${JSON.stringify(home.body?.firstMovesBonus)}`);
  expect('GET home', home.body?.resume === null, 'no "pick up where you left off" before any brief');
  r.record(j, 'POST', 'home/first-moves/claim', await me.call('POST', 'home/first-moves/claim', {}), 409, 'before the moves are done → first-moves-not-done');

  // ---------- Career Drivers ----------
  r.record(j, 'GET', 'assessments/career/result', await me.call('GET', 'assessments/career/result'), 404, 'before the first submit');
  const st = r.record(j, 'GET', 'assessments/career', await me.call('GET', 'assessments/career'), 200);
  const pairs = st.body?.pairs ?? [];
  const islands = st.body?.islands ?? [];
  expect('GET assessments/career', st.body?.status === 'new' && pairs.length === 36 && islands.length === 4 && st.body?.earnsXp === true && st.body?.xp === 0 && st.body?.attempts === 0,
    `new state: status new, 36 pairs, 4 islands, earnsXp, xp 0; got ${JSON.stringify({ ...st.body, pairs: pairs.length, islands: islands.length, answers: undefined })}`);
  expect('GET assessments/career', Array.isArray(st.body?.answers) && st.body.answers.length === 36 && st.body.answers.every(a => a === null), 'answers start as 36 nulls');

  let rm = r.record(j, 'PUT', 'assessments/career/remind', await me.call('PUT', 'assessments/career/remind', { remind: false }), 200);
  expect('PUT assessments/career/remind', rm.body?.remind === false, 'remind off');
  rm = r.record(j, 'PUT', 'assessments/career/remind', await me.call('PUT', 'assessments/career/remind', { remind: true }), 200);
  expect('PUT assessments/career/remind', rm.body?.remind === true, 'remind back on');

  // A few picks, then "Start over" (restart): answers clear, nothing earned yet.
  let answers = pairs.map(() => null);
  for (let i = 0; i < 3; i++) {
    answers[i] = i % 4;
    r.record(j, 'PUT', 'assessments/career/answers', await me.call('PUT', 'assessments/career/answers', { answers, current: i + 1 }), 200);
  }
  const resumed = r.record(j, 'GET', 'assessments/career', await me.call('GET', 'assessments/career'), 200, 'resume');
  expect('GET assessments/career', resumed.body?.status === 'in-progress' && resumed.body?.current === 3, `in-progress at pair 3, got ${resumed.body?.status} ${resumed.body?.current}`);
  const rs = r.record(j, 'POST', 'assessments/career/restart', await me.call('POST', 'assessments/career/restart', {}), 200);
  expect('POST assessments/career/restart', rs.body?.answers?.every(a => a === null) && rs.body?.current === 0, 'restart clears answers and current');

  // The first attempt, one pick per pair (notches 0–3), pair 5 skipped and answered at the final check.
  answers = pairs.map(() => null);
  let saveXp = 0;
  const lastIsland = Math.max(...islands.map(i => i.n));
  const pick = i => [0, 3, 1, 2][i % 4];
  const islandDoneAt = idx => islands.filter(is => is.n !== lastIsland && answers.slice(is.from - 1, is.to).every(a => a !== null && a !== 'skip') && idx >= is.from - 1 && idx <= is.to - 1);
  const paid = new Set();
  for (let i = 0; i < pairs.length; i++) {
    answers[i] = i === 4 ? 'skip' : pick(i);
    const res = r.record(j, 'PUT', 'assessments/career/answers', await me.call('PUT', 'assessments/career/answers', { answers, current: Math.min(i + 1, 35) }), 200);
    const due = islandDoneAt(i).filter(is => !paid.has(is.n));
    due.forEach(is => paid.add(is.n));
    expect(`PUT assessments/career/answers #${i + 1}`, res.body?.xpEarned === due.length * 25, `xpEarned ${res.body?.xpEarned}, expected ${due.length * 25} (islands cleared: ${due.map(d => d.n).join(',') || 'none'})`);
    saveXp += res.body?.xpEarned ?? 0;
  }
  r.record(j, 'POST', 'assessments/career/submit', await me.call('POST', 'assessments/career/submit', { answers }), 422, 'a skipped pair → incomplete');
  answers[4] = 2;
  const fix = r.record(j, 'PUT', 'assessments/career/answers', await me.call('PUT', 'assessments/career/answers', { answers, current: 35 }), 200, 'final check answers the skipped pair');
  expect('PUT assessments/career/answers (final check)', fix.body?.xpEarned === 25 && fix.body?.xp === 75, `island 1 cleared now: xpEarned 25, xp 75; got ${JSON.stringify(fix.body)}`);
  saveXp += fix.body?.xpEarned ?? 0;
  expect('PUT assessments/career/answers', saveXp === 75, `islands 1–3 show 75 XP in all, got ${saveXp}`);
  r.record(j, 'PUT', 'assessments/career/answers', await me.call('PUT', 'assessments/career/answers', { answers: [0, 1], current: 2 }), 422, 'wrong answer count → validation');

  const sub = r.record(j, 'POST', 'assessments/career/submit', await me.call('POST', 'assessments/career/submit', { answers }), 200, 'open my jars');
  expect('POST assessments/career/submit', sub.body?.attempt === 1 && sub.body?.xpEarned === 100 && sub.body?.previous === null && sub.body?.ranking?.length === 9,
    `attempt 1, xpEarned 100, previous null, 9 drivers ranked; got ${JSON.stringify({ attempt: sub.body?.attempt, xpEarned: sub.body?.xpEarned, previous: sub.body?.previous })}`);
  const res1 = r.record(j, 'GET', 'assessments/career/result', await me.call('GET', 'assessments/career/result'), 200);
  expect('GET assessments/career/result', res1.body?.attempt === 1 && res1.body?.archetype?.id === sub.body?.archetype?.id, 'the latest result is attempt 1, same archetype');
  const done = r.record(j, 'GET', 'assessments/career', await me.call('GET', 'assessments/career'), 200, 'after submit');
  expect('GET assessments/career', done.body?.status === 'done' && done.body?.attempts === 1 && done.body?.earnsXp === false && done.body?.xp === 0, `done, 1 attempt, earnsXp false; got ${JSON.stringify({ ...done.body, pairs: undefined, islands: undefined, answers: undefined })}`);

  // Retake: no XP anywhere.
  const rt = r.record(j, 'POST', 'assessments/career/retake', await me.call('POST', 'assessments/career/retake', {}), 200);
  expect('POST assessments/career/retake', rt.body?.earnsXp === false && rt.body?.xp === 0 && rt.body?.answers?.every(a => a === null), 'retake: fresh answers, earns nothing');
  answers = pairs.map((_, i) => [3, 2, 1, 0][i % 4]);
  let retakeXp = 0;
  for (const is of islands.filter(x => x.n !== lastIsland)) {
    const res = r.record(j, 'PUT', 'assessments/career/answers', await me.call('PUT', 'assessments/career/answers', { answers: answers.map((a, i) => (i < is.to ? a : null)), current: is.to }), 200, `retake island ${is.n}`);
    retakeXp += res.body?.xpEarned ?? 0;
  }
  expect('PUT assessments/career/answers (retake)', retakeXp === 0, `a retake shows no island XP, got ${retakeXp}`);
  const sub2 = r.record(j, 'POST', 'assessments/career/submit', await me.call('POST', 'assessments/career/submit', { answers }), 200, 'retake submit');
  expect('POST assessments/career/submit (retake)', sub2.body?.attempt === 2 && sub2.body?.xpEarned === 0 && sub2.body?.previous !== null && sub2.body?.attempts?.length === 2,
    `attempt 2, xpEarned 0, previous set, 2 attempts listed; got ${JSON.stringify({ attempt: sub2.body?.attempt, xpEarned: sub2.body?.xpEarned, attempts: sub2.body?.attempts })}`);
  const old = r.record(j, 'GET', 'assessments/career/result?attempt=1', await me.call('GET', 'assessments/career/result?attempt=1'), 200, 'compare: attempt 1');
  expect('GET assessments/career/result?attempt=1', old.body?.attempt === 1 && old.body?.xpEarned === 100, 'attempt 1 still shows its 100 XP');

  // Gamification: exactly +100, keyed per island and the submit; Career Clarity progress.
  const afterCa = await poll(me, 'me/progress', b => b.xp >= xp0 + 100, 'after Career Drivers');
  await sleep(3000); // let any (wrong) retake credit land before checking it didn't
  let prog = r.record(j, 'GET', 'me/progress', await me.call('GET', 'me/progress'), 200, 'after the retake');
  expect('GET me/progress', afterCa.ok && prog.body?.xp === xp0 + 100, `Career Drivers pays exactly 100 XP (retake 0): ${xp0} → ${prog.body?.xp}`);
  let ledger = await ledgerAll(me, 'after Career Drivers');
  const caRows = ledger.filter(e => e.reason === 'assessment');
  expect('GET me/xp/ledger', caRows.length === 4 && sumBy(ledger, e => e.reason === 'assessment') === 100
    && ['island:1', 'island:2', 'island:3', 'submitted'].every(s => caRows.some(e => e.sourceId === s)),
  `assessment credits island:1-3 + submitted, 25 each; got ${JSON.stringify(caRows.map(e => [e.sourceId, e.xp]))}`);
  let badges = r.record(j, 'GET', 'badges', await me.call('GET', 'badges'), 200, 'after Career Drivers');
  const clarity = badges.body?.badges?.find(b => b.id === 'career-clarity');
  expect('GET badges', clarity && (clarity.state === 'earned' || clarity.percent === 100), `Career Clarity complete after the assessment, got ${JSON.stringify(clarity && { state: clarity.state, percent: clarity.percent, progressLabel: clarity.progressLabel })}`);
  r.record(j, 'GET', 'stats/assessments', await me.call('GET', 'stats/assessments'), 200);

  // The Lobby ticks "Discover your Career Drivers" (Learning consumes assessment.attempt.submitted).
  await poll(me, 'home', b => b.firstSteps?.find(s => s.id === 'assessment')?.done === true, 'Career Drivers move ticked');
  r.record(j, 'POST', 'home/first-moves/claim', await me.call('POST', 'home/first-moves/claim', {}), 409, 'one of three done → still 409');

  // ---------- Learning ----------
  const bv = r.record(j, 'GET', 'learn/modules/brain-vita', await me.call('GET', 'learn/modules/brain-vita'), 200);
  expect('GET learn/modules/brain-vita', bv.body?.quizzesDone === 0 && bv.body?.topics?.length >= 1 && bv.body?.xp === 0, 'a fresh Brain Vita: nothing done');
  expect('GET learn/modules/brain-vita', bv.body?.resume?.brief?.index === 1 && typeof bv.body?.resume?.link === 'string', `B-44 resume points to brief 1 of the first topic, got ${JSON.stringify(bv.body?.resume)}`);
  r.record(j, 'GET', 'learn/modules/no-such-module', await me.call('GET', 'learn/modules/no-such-module'), 404);
  const tiles = (home.body?.knooks ?? []).flatMap(k => k.modules);
  for (const t of tiles) {
    const slug = t.link.replace(/^\/learn\//, '');
    const m = r.record(j, 'GET', `learn/modules/${slug}`, await me.call('GET', `learn/modules/${encodeURIComponent(slug)}`), 200, `Lobby tile ${t.title}`);
    expect(`GET learn/modules/${slug}`, m.body?.topics?.length > 0, 'every Lobby tile opens a module with topics');
  }

  const M = 'brain-vita';
  const T = 'non-verbal-reasoning';
  let topic = r.record(j, 'GET', `learn/modules/${M}/topics/${T}`, await me.call('GET', `learn/modules/${M}/topics/${T}`), 200);
  const briefs = topic.body?.briefs ?? [];
  expect(`GET learn/modules/${M}/topics/${T}`, briefs[0]?.status === 'current' && briefs.slice(1).every(b => b.status === 'locked'), 'brief 1 current, the rest locked');
  r.record(j, 'GET', `learn/modules/${M}/topics/nope`, await me.call('GET', `learn/modules/${M}/topics/nope`), 404);
  const b0 = briefs[0]?.id;
  r.record(j, 'PUT', `learn/briefs/${b0}/bookmark`, await me.call('PUT', `learn/briefs/${encodeURIComponent(b0)}/bookmark`, { on: true }), 204);
  r.record(j, 'PUT', `learn/briefs/${b0}/feedback`, await me.call('PUT', `learn/briefs/${encodeURIComponent(b0)}/feedback`, { helpful: true }), 204);
  topic = r.record(j, 'GET', `learn/modules/${M}/topics/${T}`, await me.call('GET', `learn/modules/${M}/topics/${T}`), 200, 'after bookmark + feedback');
  expect('GET topic', topic.body?.briefs?.[0]?.bookmarked === true && topic.body?.briefs?.[0]?.helpful === true, 'bookmark and rating are kept');
  r.record(j, 'PUT', `learn/briefs/${b0}/feedback`, await me.call('PUT', `learn/briefs/${encodeURIComponent(b0)}/feedback`, { helpful: null }), 204, 'clear the rating');
  r.record(j, 'PUT', `learn/briefs/${b0}/bookmark`, await me.call('PUT', `learn/briefs/${encodeURIComponent(b0)}/bookmark`, { on: false }), 204);
  r.record(j, 'PUT', 'learn/briefs/x~y~0/bookmark', await me.call('PUT', 'learn/briefs/x~y~0/bookmark', { on: true }), 404, 'unknown brief');

  let quiz = r.record(j, 'GET', `learn/modules/${M}/topics/${T}/quiz`, await me.call('GET', `learn/modules/${M}/topics/${T}/quiz`), 200);
  expect('GET quiz', quiz.body?.id === briefs[0]?.quiz?.id && quiz.body?.alreadyPassed === false && quiz.body?.questions?.length > 0, `the quiz of brief 1, not passed; got ${quiz.body?.id}`);
  const q0 = quiz.body;
  const firstPick = Object.fromEntries((q0?.questions ?? []).map(q => [q.id, q.options[0].id]));
  const oneDraft = Object.fromEntries(Object.entries(firstPick).slice(0, 1));
  r.record(j, 'PUT', `learn/quizzes/${q0?.id}/draft`, await me.call('PUT', `learn/quizzes/${encodeURIComponent(q0?.id)}/draft`, { answers: oneDraft }), 204);
  quiz = r.record(j, 'GET', `learn/modules/${M}/topics/${T}/quiz`, await me.call('GET', `learn/modules/${M}/topics/${T}/quiz`), 200, 'draft restored');
  expect('GET quiz', JSON.stringify(quiz.body?.draft) === JSON.stringify(oneDraft), `draft kept: ${JSON.stringify(quiz.body?.draft)}`);
  r.record(j, 'POST', `learn/quizzes/${briefs[1]?.quiz?.id}/submit`, await me.call('POST', `learn/quizzes/${encodeURIComponent(briefs[1]?.quiz?.id)}/submit`, { answers: firstPick }), 422, 'a locked brief → quiz-locked');

  const s0 = r.record(j, 'POST', `learn/quizzes/${q0?.id}/submit`, await me.call('POST', `learn/quizzes/${encodeURIComponent(q0?.id)}/submit`, { answers: firstPick }), 200, 'first submit');
  const c0 = s0.body?.correct ?? 0;
  expect('POST quiz submit', s0.body?.xp === c0 && s0.body?.total === q0?.questions?.length, `+1 per correct on the first submit: xp ${s0.body?.xp}, correct ${c0}`);
  const right = Object.fromEntries((s0.body?.results ?? []).map(x => [x.questionId, x.correctOptionId]));
  const again = r.record(j, 'POST', `learn/quizzes/${q0?.id}/submit`, await me.call('POST', `learn/quizzes/${encodeURIComponent(q0?.id)}/submit`, { answers: right }), 200, 'retake = practice');
  expect('POST quiz submit (retake)', again.body?.xp === 0 && again.body?.correct === again.body?.total, `a retake pays 0, got ${again.body?.xp}`);

  quiz = r.record(j, 'GET', `learn/modules/${M}/topics/${T}/quiz`, await me.call('GET', `learn/modules/${M}/topics/${T}/quiz`), 200, 'next brief');
  const q1 = quiz.body;
  expect('GET quiz', q1?.id === briefs[1]?.quiz?.id && q1?.alreadyPassed === false, `the quiz moves to brief 2, got ${q1?.id}`);
  const allRight = Object.fromEntries((q1?.questions ?? []).map(q => [q.id, right[q.id] ?? q.options[0].id]));
  const s1 = r.record(j, 'POST', `learn/quizzes/${q1?.id}/submit`, await me.call('POST', `learn/quizzes/${encodeURIComponent(q1?.id)}/submit`, { answers: allRight }), 200, 'first submit, all right');
  const c1 = s1.body?.correct ?? 0;
  expect('POST quiz submit', s1.body?.xp === c1 && c1 === s1.body?.total, `100%: xp ${s1.body?.xp} of ${s1.body?.total}`);

  // Learning minutes: 10 + 6 → "Learn for 15 minutes" done once.
  r.record(j, 'POST', 'learn/activity', await me.call('POST', 'learn/activity', { minutes: 10 }), [200, 204], 'extra: MISS-04 learning minutes');
  r.record(j, 'POST', 'learn/activity', await me.call('POST', 'learn/activity', { minutes: 6 }), [200, 204], 'extra: MISS-04 learning minutes');
  r.record(j, 'POST', 'learn/activity', await me.call('POST', 'learn/activity', { minutes: 0 }), 422, 'extra: minutes 1–60');

  // XP: 100 (Career Drivers) + quizzes + daily quests finish-bv 10, quiz-80 5, learn-15 5.
  const dailyXp = 20;
  const wantAfterLearn = xp0 + 100 + c0 + c1 + dailyXp;
  const afterLearn = await poll(me, 'me/progress', b => b.xp >= wantAfterLearn, 'after Learning');
  expect('GET me/progress', afterLearn.ok && afterLearn.res.body?.xp === wantAfterLearn, `after 2 quizzes and 3 daily quests: expected ${wantAfterLearn}, got ${afterLearn.res.body?.xp}`);
  const dq = r.record(j, 'GET', 'me/daily-quests', await me.call('GET', 'me/daily-quests'), 200, 'extra: MISS-03');
  const dqItems = dq.body?.items ?? [];
  for (const id of ['finish-bv', 'learn-15', 'quiz-80']) {
    expect('GET me/daily-quests', dqItems.find(q => q.id === id)?.done === true, `${id} done today, got ${JSON.stringify(dqItems.find(q => q.id === id))}`);
  }
  const streak = r.record(j, 'GET', 'me/streak', await me.call('GET', 'me/streak'), 200, 'extra: MISS-04');
  expect('GET me/streak', streak.body?.days === 1 && streak.body?.week?.includes('done'), `a first-attempt quiz today → streak 1, got ${JSON.stringify(streak.body)}`);

  // Another Brain-Vita brief and more minutes the same day: the dailies don't pay twice.
  quiz = r.record(j, 'GET', `learn/modules/${M}/topics/${T}/quiz`, await me.call('GET', `learn/modules/${M}/topics/${T}/quiz`), 200, 'brief 3');
  const q2 = quiz.body;
  const s2 = r.record(j, 'POST', `learn/quizzes/${q2?.id}/submit`, await me.call('POST', `learn/quizzes/${encodeURIComponent(q2?.id)}/submit`, { answers: Object.fromEntries((q2?.questions ?? []).map(q => [q.id, right[q.id] ?? q.options[0].id])) }), 200, 'brief 3 first submit');
  const c2 = s2.body?.correct ?? 0;
  r.record(j, 'POST', 'learn/activity', await me.call('POST', 'learn/activity', { minutes: 20 }), [200, 204], 'extra: more minutes');
  const want2 = wantAfterLearn + c2;
  const after2 = await poll(me, 'me/progress', b => b.xp >= want2, 'after brief 3');
  await sleep(3000);
  prog = r.record(j, 'GET', 'me/progress', await me.call('GET', 'me/progress'), 200, 'dailies once per day');
  expect('GET me/progress', after2.ok && prog.body?.xp === want2, `brief 3 adds only its quiz XP (${c2}); expected ${want2}, got ${prog.body?.xp}`);
  ledger = await ledgerAll(me, 'after Learning');
  for (const reason of ['daily.finish-bv', 'daily.learn-15', 'daily.quiz-80']) {
    expect('GET me/xp/ledger', ledger.filter(e => e.reason === reason).length === 1, `${reason} credited once, got ${ledger.filter(e => e.reason === reason).length}`);
  }
  expect('GET me/xp/ledger', sumBy(ledger, e => e.reason === 'quiz.correct') === c0 + c1 + c2 && ledger.filter(e => e.reason === 'quiz.correct' && e.sourceId === q0?.id).length <= 1,
    `quiz XP = first submits only (${c0}+${c1}+${c2}), got ${sumBy(ledger, e => e.reason === 'quiz.correct')}`);
  expect('GET me/xp/ledger', ledger.reduce((a, e) => a + e.xp, 0) === prog.body?.xp, 'the ledger adds up to the total');
  const streak2 = r.record(j, 'GET', 'me/streak', await me.call('GET', 'me/streak'), 200, 'extra: still 1 the same day');
  expect('GET me/streak', streak2.body?.days === 1, `more quizzes the same day keep streak 1, got ${streak2.body?.days}`);

  // Where to resume (B-44): brief 4 of the topic, on the module, the Lobby card and the topic.
  const bv2 = r.record(j, 'GET', `learn/modules/${M}`, await me.call('GET', `learn/modules/${M}`), 200, 'resume point');
  expect('GET learn/modules/brain-vita', bv2.body?.quizzesDone === 3 && bv2.body?.resume?.topic?.slug === T && bv2.body?.resume?.brief?.index === 4 && bv2.body?.resume?.brief?.id === briefs[3]?.id,
    `3 done; resume at ${T} brief 4, got ${JSON.stringify(bv2.body?.resume)}`);
  topic = r.record(j, 'GET', `learn/modules/${M}/topics/${T}`, await me.call('GET', `learn/modules/${M}/topics/${T}`), 200, 'after 3 quizzes');
  expect('GET topic', topic.body?.briefs?.slice(0, 3).every(b => b.status === 'done') && topic.body?.briefs?.[3]?.status === 'current' && topic.body?.quizzesDone === 3,
    'briefs 1–3 done, brief 4 current');
  expect('GET topic', topic.body?.briefs?.[0]?.quiz?.result?.correct === again.body?.correct, 'brief 1 shows the last checked score');
  home = r.record(j, 'GET', 'home', await me.call('GET', 'home'), 200, 'resume card');
  expect('GET home', home.body?.resume?.brief === 4 && home.body?.resume?.link === bv2.body?.resume?.link, `Lobby resume card at brief 4 → ${bv2.body?.resume?.link}, got ${JSON.stringify(home.body?.resume)}`);
  expect('GET home', home.body?.firstSteps?.find(s => s.id === 'learn')?.done === true, 'the learning move is ticked');
  const sl = r.record(j, 'GET', 'stats/learning', await me.call('GET', 'stats/learning'), 200);
  const bvCourse = sl.body?.courses?.find(c => c.id === M);
  expect('GET stats/learning', bvCourse?.status === 'in-progress' && sl.body?.courses?.[0]?.id === M && sl.body?.currentId === M, `Brain Vita in progress, first and current; got ${JSON.stringify(bvCourse && { status: bvCourse.status, percent: bvCourse.percent })}, currentId ${sl.body?.currentId}`);

  // ---------- First moves bonus (BR-107) ----------
  r.record(j, 'POST', 'home/first-steps/bogus', await me.call('POST', 'home/first-steps/bogus', {}), 404, 'unknown move');
  r.record(j, 'POST', 'home/first-moves/claim', await me.call('POST', 'home/first-moves/claim', {}), 409, 'jobs not visited yet');
  r.record(j, 'POST', 'home/first-steps/jobs', await me.call('POST', 'home/first-steps/jobs', {}), 204, 'opened My Jobs from the Lobby');
  const claim = r.record(j, 'POST', 'home/first-moves/claim', await me.call('POST', 'home/first-moves/claim', {}), 200, 'all three done');
  expect('POST home/first-moves/claim', claim.body?.xp === 50, `+50, got ${JSON.stringify(claim.body)}`);
  const claim2 = r.record(j, 'POST', 'home/first-moves/claim', await me.call('POST', 'home/first-moves/claim', {}), 200, 'second claim');
  expect('POST home/first-moves/claim', claim2.body?.xp === 0, `a second claim adds 0, got ${JSON.stringify(claim2.body)}`);
  home = r.record(j, 'GET', 'home', await me.call('GET', 'home'), 200, 'after the bonus');
  expect('GET home', home.body?.firstSteps == null && home.body?.firstMovesBonus == null, 'the first-moves card makes way after the claim');
  const want3 = want2 + 50;
  await poll(me, 'me/progress', b => b.xp >= want3, 'after the bonus');
  await sleep(2000);
  prog = r.record(j, 'GET', 'me/progress', await me.call('GET', 'me/progress'), 200, 'bonus once');
  expect('GET me/progress', prog.body?.xp === want3, `first-moves pays 50 once: expected ${want3}, got ${prog.body?.xp}`);
  const lvl = levelFor(prog.body?.xp ?? 0);
  expect('GET me/progress', prog.body?.level === lvl.level && prog.body?.rank === lvl.rank && prog.body?.target === lvl.target, `level from the ladder ${JSON.stringify(lvl)}, got ${JSON.stringify(prog.body)}`);
  ledger = await ledgerAll(me, 'first moves');
  expect('GET me/xp/ledger', ledger.filter(e => e.reason === 'first-moves').length === 1, 'first-moves credited once');

  // ---------- My Stats: Rewards ----------
  const rw = r.record(j, 'GET', 'stats/rewards', await me.call('GET', 'stats/rewards'), 200);
  const totalXp = prog.body?.xp;
  expect('GET stats/rewards', rw.body?.player?.xp === totalXp && rw.body?.wallet?.xp === totalXp && rw.body?.player?.level === lvl.level && rw.body?.player?.title === lvl.rank && rw.body?.player?.nextLevelXp === lvl.target,
    `player/wallet XP ${totalXp}, level ${lvl.level} ${lvl.rank}; got ${JSON.stringify(rw.body?.player)}`);
  expect('GET stats/rewards', rw.body?.player?.name === 'Kavya Iyer' && rw.body?.player?.initials === 'KI', `name and initials from sign-up, got ${rw.body?.player?.name} ${rw.body?.player?.initials}`);
  expect('GET stats/rewards', rw.body?.streak?.days === 1 && rw.body?.streak?.week?.length === 7, `streak 1, 7-day week; got ${JSON.stringify(rw.body?.streak)}`);
  expect('GET stats/rewards', (rw.body?.quests?.items ?? []).length === 3 && rw.body.quests.items.every(q => q.done), 'the three daily quests show done');
  expect('GET stats/rewards', rw.body?.badges?.showcase?.length === 3, 'always 3 showcase slots');
  expect('GET stats/rewards', rw.body?.learningHours > 0, `36 minutes of learning → learningHours > 0, got ${rw.body?.learningHours}`);

  // ---------- Badges and the showcase ----------
  badges = r.record(j, 'GET', 'badges', await me.call('GET', 'badges'), 200);
  const all = badges.body?.badges ?? [];
  expect('GET badges', badges.body?.total === all.length && badges.body?.earned === all.filter(b => b.state === 'earned').length, 'earned/total match the list');
  const notEarned = all.find(b => b.state !== 'earned');
  if (notEarned) r.record(j, 'PUT', 'badges/showcase', await me.call('PUT', 'badges/showcase', { ids: [notEarned.id] }), 422, 'only earned badges → badge-not-earned');
  r.record(j, 'PUT', 'badges/showcase', await me.call('PUT', 'badges/showcase', { ids: all.slice(0, 4).map(b => b.id) }), 422, 'more than 3 → too-many-pinned');
  const earnedIds = all.filter(b => b.state === 'earned').map(b => b.id).slice(0, 3);
  const sc = r.record(j, 'PUT', 'badges/showcase', await me.call('PUT', 'badges/showcase', { ids: earnedIds }), 200, `pin ${earnedIds.length} earned`);
  expect('PUT badges/showcase', (sc.body?.badges ?? []).filter(b => b.pinned).map(b => b.id).sort().join() === [...earnedIds].sort().join(), 'pinned flags follow the showcase');
  r.record(j, 'PUT', 'badges/showcase', await me.call('PUT', 'badges/showcase', { ids: [] }), 200, 'unpin all');

  // ---------- Arena ----------
  await poll(rival, 'me/progress', b => typeof b.xp === 'number', 'rival player exists');
  for (const scope of ['everyone', 'batch', 'friends']) {
    for (const period of ['week', 'month', 'all']) {
      const b = r.record(j, 'GET', `arena?scope=${scope}&period=${period}`, await me.call('GET', `arena?scope=${scope}&period=${period}`), 200);
      const you = b.body?.players?.find(p => p.you);
      expect(`GET arena ${scope}/${period}`, b.body?.scope === scope && b.body?.period === period && you && b.body?.me?.rank === you.rank && b.body?.me?.xp === you.xp,
        `the board names the scope/period and lists you with me.rank; got me ${JSON.stringify(b.body?.me)}`);
      if (period === 'all' && scope !== 'friends') expect(`GET arena ${scope}/all`, you?.xp === totalXp && you?.level === lvl.level && you?.title === lvl.rank, `all-time XP ${totalXp}, level ${lvl.level}; got ${JSON.stringify(you)}`);
      expect(`GET arena ${scope}/${period}`, b.body?.week?.length === 7 && b.body?.rewards?.coins === 0 && b.body?.rewards?.voucher === null, 'week of 7, no coins/vouchers (A-317)');
    }
  }
  const fresh = r.record(j, 'GET', 'arena?scope=everyone&period=week', await rival.call('GET', 'arena?scope=everyone&period=week'), 200, 'a learner with no XP yet');
  const freshYou = fresh.body?.players?.find(p => p.you);
  expect('GET arena (no XP)', freshYou && freshYou.xp === 0 && fresh.body?.me?.rank === freshYou.rank && fresh.body?.me?.rank > 1, `a learner with 0 XP still sees themself below the earners, got me ${JSON.stringify(fresh.body?.me)}`);
  r.record(j, 'GET', 'arena?scope=galaxy&period=week', await me.call('GET', 'arena?scope=galaxy&period=week'), 422, 'unknown scope');
  const rivalId = (await rival.call('GET', 'session')).body?.id;
  const meId = (await me.call('GET', 'session')).body?.id;
  let ch = r.record(j, 'POST', `arena/players/${rivalId}/cheer`, await me.call('POST', `arena/players/${rivalId}/cheer`, { on: true }), 200, 'cheer');
  expect('POST cheer', ch.body?.cheers === 1, `1 cheer, got ${JSON.stringify(ch.body)}`);
  ch = r.record(j, 'POST', `arena/players/${rivalId}/cheer`, await me.call('POST', `arena/players/${rivalId}/cheer`, { on: true }), 200, 'cheer again (idempotent)');
  expect('POST cheer', ch.body?.cheers === 1, `still 1, got ${JSON.stringify(ch.body)}`);
  const friends = r.record(j, 'GET', 'arena?scope=friends&period=week', await me.call('GET', 'arena?scope=friends&period=week'), 200, 'friends = cheered');
  expect('GET arena friends', friends.body?.players?.some(p => p.id === rivalId && p.cheered), 'a cheered player shows on Friends, cheered');
  ch = r.record(j, 'POST', `arena/players/${rivalId}/cheer`, await me.call('POST', `arena/players/${rivalId}/cheer`, { on: false }), 200, 'un-cheer');
  expect('POST cheer', ch.body?.cheers === 0, `0 after un-cheer, got ${JSON.stringify(ch.body)}`);
  r.record(j, 'POST', `arena/players/${meId}/cheer`, await me.call('POST', `arena/players/${meId}/cheer`, { on: true }), 422, 'not-yourself');
  r.record(j, 'POST', 'arena/players/00000000-0000-0000-0000-000000000001/cheer', await me.call('POST', 'arena/players/00000000-0000-0000-0000-000000000001/cheer', { on: true }), 404, 'unknown player');
  r.record(j, 'POST', `arena/players/${rivalId}/challenge`, await me.call('POST', `arena/players/${rivalId}/challenge`, {}), 204, '1-week race');
  r.record(j, 'POST', `arena/players/${rivalId}/challenge`, await me.call('POST', `arena/players/${rivalId}/challenge`, {}), 409, 'race already open');
};
