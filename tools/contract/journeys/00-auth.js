'use strict';
// Sign-in and sign-up (BRD §3.1, auth.handlers.ts).
module.exports = async (ctx, j) => {
  const r = ctx.report;
  const anon = ctx.client('anon');
  r.record(j, 'GET', 'session', await anon.call('GET', 'session'), 401);
  r.record(j, 'POST', 'auth/otp', await anon.call('POST', 'auth/otp', { phone: '+919000000001' }), 404, 'unregistered mobile → account-not-found');
  r.record(j, 'POST', 'auth/email/login-code', await anon.call('POST', 'auth/email/login-code', { email: 'nobody@example.com' }), 404);

  const c = await ctx.newLearner(j);
  const me = r.record(j, 'GET', 'session', await c.call('GET', 'session'), 200);
  ctx.state.learnerId = me.body?.id;

  r.record(j, 'POST', 'auth/logout', await c.call('POST', 'auth/logout', {}), [200, 204]);
  r.record(j, 'GET', 'session', await c.call('GET', 'session'), 401, 'after logout');

  r.record(j, 'POST', 'auth/email/login-code', await c.call('POST', 'auth/email/login-code', { email: c.email }), 200);
  r.record(j, 'POST', 'auth/email/login-verify', await c.call('POST', 'auth/email/login-verify', { email: c.email, code: '000000' }), 422, 'wrong code');
  r.record(j, 'POST', 'auth/email/login-verify', await c.call('POST', 'auth/email/login-verify', { email: c.email, code: ctx.CODE }), 200);
  r.record(j, 'POST', 'auth/logout', await c.call('POST', 'auth/logout', {}), [200, 204]);
  r.record(j, 'POST', 'auth/login', await c.call('POST', 'auth/login', { email: c.email, password: 'wrong-pass1' }), [401, 422], 'wrong password');
  r.record(j, 'POST', 'auth/login', await c.call('POST', 'auth/login', { email: c.email, password: 'Skillmuni123' }), 200);
  r.record(j, 'POST', 'auth/password-reset', await anon.call('POST', 'auth/password-reset', { email: 'nobody@example.com' }), [200, 202]);
  r.record(j, 'POST', 'auth/register', await anon.call('POST', 'auth/register', { firstName: 'A', lastName: 'B', email: c.email, password: 'Skillmuni123', consent: true }), 409, 'email-taken');
  r.record(j, 'POST', 'auth/email/resend', await anon.call('POST', 'auth/email/resend', { email: ctx.unique('x') + '@example.com' }), [200, 404, 422]);
  r.record(j, 'POST', 'auth/google', await anon.call('POST', 'auth/google', { credential: 'not-a-real-token' }), [401, 422]);
  ctx.state.main = c;
};
