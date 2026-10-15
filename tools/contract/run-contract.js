#!/usr/bin/env node
'use strict';
// Full regression against a running stack (docker compose): node run-contract.js http://localhost:5080 [journey...]
// Every journey in ./journeys drives the API the way the Angular app does and checks each response against the
// frontend's types. Exit code 1 when any call has a problem.
const fs = require('fs');
const path = require('path');
const { Client, Report } = require('./lib');

const base = process.argv[2] || 'http://localhost:5080';
const only = process.argv.slice(3);
const report = new Report();
const CODE = '123456'; // Identity:Security:DevelopmentFixedCode (Development only)
let n = 0;

const ctx = {
  base, report, CODE,
  client: name => new Client(base, name),
  unique: prefix => `${prefix}${Date.now().toString(36)}${(n++).toString(36)}`,
  /** Sign up and verify a brand-new learner (Join the squad → email code), returning a signed-in client. */
  async newLearner(journey, first = 'Asha', last = 'Rao') {
    const c = new Client(base, first);
    const email = `${ctx.unique('learner')}@example.com`;
    report.record(journey, 'POST', 'auth/register', await c.call('POST', 'auth/register', { firstName: first, lastName: last, email, password: 'Skillmuni123', consent: true }), [200, 201]);
    report.record(journey, 'POST', 'auth/email/verify', await c.call('POST', 'auth/email/verify', { email, code: CODE }), 200);
    c.email = email;
    return c;
  },
  state: {},
};

(async () => {
  const dir = path.join(__dirname, 'journeys');
  const files = fs.readdirSync(dir).filter(f => f.endsWith('.js')).sort();
  for (const f of files) {
    const name = f.replace(/^\d+-|\.js$/g, '');
    if (only.length && !only.includes(name)) continue;
    const started = Date.now();
    try {
      await require(path.join(dir, f))(ctx, name);
    } catch (e) {
      report.rows.push({ journey: name, call: '(journey crashed)', status: 0, ms: 0, problems: [String(e.stack || e).split('\n').slice(0, 4).join(' | ')], note: '' });
    }
    console.error(`journey ${name}: ${Date.now() - started} ms`);
  }
  const { text, failed } = report.summary();
  console.log(text);
  process.exit(failed ? 1 : 0);
})();
