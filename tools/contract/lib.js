'use strict';
// Regression harness core: an HTTP client with a cookie jar (like the browser), a JSON Schema checker for the
// frontend contract (contract.json, extracted from the Angular API layer), and a report.
const fs = require('fs');
const path = require('path');

const contract = JSON.parse(fs.readFileSync(path.join(__dirname, 'contract.json'), 'utf8'));

/** Find the contract entry for a call, matching the frontend path template (`{x}` = any segment). */
function contractFor(verb, apiPath) {
  const clean = apiPath.split('?')[0];
  // The most specific template wins: `fests/bookings` is not `fests/{id}`.
  const params = c => (c.path.replace(/^\{apiBaseUrl\}\//, '').match(/\{/g) || []).length;
  return contract.filter(c => c.verb === verb && c.path && new RegExp(
    '^' + c.path.replace(/^\{apiBaseUrl\}\//, '').replace(/[.*+?^$()|[\]\\]/g, '\\$&').replace(/\\?\{[^}]+\\?\}/g, '[^/]+') + '$',
  ).test(clean)).sort((a, b) => params(a) - params(b))[0];
}

/** Validate `value` against a schema produced by extract-contract.js. Returns a list of problems. */
function check(schema, value, at = '$') {
  const out = [];
  if (!schema || Object.keys(schema).length === 0) return out;
  if (schema.void) return out;
  if (schema.anyOf) {
    const results = schema.anyOf.map(s => check(s, value, at));
    if (results.some(r => r.length === 0)) return out;
    const best = results.sort((a, b) => a.length - b.length)[0];
    return best.length ? best : [`${at}: matches no allowed shape`];
  }
  if ('const' in schema) {
    if (value !== schema.const) out.push(`${at}: expected ${JSON.stringify(schema.const)}, got ${JSON.stringify(value)}`);
    return out;
  }
  if (schema.enum) {
    if (!schema.enum.includes(value)) out.push(`${at}: expected one of ${JSON.stringify(schema.enum).slice(0, 160)}, got ${JSON.stringify(value)}`);
    return out;
  }
  const t = schema.type;
  if (t === 'null') { if (value !== null) out.push(`${at}: expected null, got ${JSON.stringify(value)?.slice(0, 60)}`); return out; }
  if (t === 'string' && typeof value !== 'string') out.push(`${at}: expected string, got ${JSON.stringify(value)?.slice(0, 60)}`);
  if (t === 'number' && typeof value !== 'number') out.push(`${at}: expected number, got ${JSON.stringify(value)?.slice(0, 60)}`);
  if (t === 'boolean' && typeof value !== 'boolean') out.push(`${at}: expected boolean, got ${JSON.stringify(value)?.slice(0, 60)}`);
  if (t === 'array') {
    if (!Array.isArray(value)) out.push(`${at}: expected array, got ${JSON.stringify(value)?.slice(0, 60)}`);
    else value.slice(0, 5).forEach((v, i) => out.push(...check(schema.items, v, `${at}[${i}]`)));
  }
  if (t === 'object') {
    if (value === null || typeof value !== 'object' || Array.isArray(value)) {
      out.push(`${at}: expected object, got ${JSON.stringify(value)?.slice(0, 60)}`);
    } else {
      for (const r of schema.required || []) {
        const ps = schema.properties[r];
        const optionalByType = ps && (ps.void || (ps.anyOf && ps.anyOf.some(x => x.void)));
        if (!(r in value) && !optionalByType) out.push(`${at}.${r}: missing`);
      }
      for (const [k, ps] of Object.entries(schema.properties || {})) {
        if (k in value && value[k] !== undefined) out.push(...check(ps, value[k], `${at}.${k}`));
      }
      if (schema.additionalProperties) {
        for (const [k, v] of Object.entries(value)) {
          if (!(k in (schema.properties || {}))) out.push(...check(schema.additionalProperties, v, `${at}.${k}`));
        }
      }
    }
  }
  return out;
}

class Client {
  constructor(base, name) { this.base = base.replace(/\/$/, ''); this.name = name; this.cookies = {}; }

  async call(verb, apiPath, body, opts = {}) {
    const headers = { Accept: 'application/json', ...(opts.headers || {}) };
    let payload;
    if (body instanceof FormData) payload = body;
    else if (body !== undefined) { headers['Content-Type'] = 'application/json'; payload = JSON.stringify(body); }
    const cookie = Object.entries(this.cookies).map(([k, v]) => `${k}=${v}`).join('; ');
    if (cookie) headers.Cookie = cookie;
    const started = Date.now();
    let res;
    try {
      res = await fetch(`${this.base}/api/v1/${apiPath}`, { method: verb, headers, body: payload, redirect: 'manual' });
    } catch (e) {
      return { status: 0, body: null, text: String(e), ms: Date.now() - started };
    }
    for (const c of res.headers.getSetCookie?.() ?? []) {
      const [pair] = c.split(';'); const i = pair.indexOf('=');
      const k = pair.slice(0, i).trim(); const v = pair.slice(i + 1);
      if (/max-age=0|expires=thu, 01 jan 1970/i.test(c) || v === '') delete this.cookies[k]; else this.cookies[k] = v;
    }
    const text = await res.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
    return { status: res.status, body: json, text, ms: Date.now() - started, contentType: res.headers.get('content-type') || '', headers: res.headers };
  }
}

class Report {
  constructor() { this.rows = []; this.covered = new Set(); }

  /**
   * Record one call. `expect` = expected status (number or array). Checks the body against the frontend's type when
   * the call succeeded, and the error shape (problem+json with message + errors.code) when it failed.
   */
  record(journey, verb, apiPath, r, expect, note = '') {
    const exp = Array.isArray(expect) ? expect : [expect];
    const problems = [];
    if (!exp.includes(r.status)) problems.push(`status ${r.status}, expected ${exp.join('/')}${r.body?.errors?.code ? ' (' + r.body.errors.code[0] + ')' : ''}${r.status >= 400 && r.body?.message ? ': ' + r.body.message : ''}${r.status === 0 ? ': ' + r.text : ''}`);
    const c = contractFor(verb, apiPath);
    if (c) this.covered.add(`${c.verb} ${c.path}`);
    if (r.status >= 200 && r.status < 300 && r.status !== 204 && c) {
      problems.push(...check(c.response, r.body).slice(0, 12));
    }
    if (r.status >= 400 && r.status !== 0) {
      if (!r.contentType.includes('application/problem+json')) problems.push(`error content-type ${r.contentType}`);
      else if (typeof r.body?.message !== 'string' || !Array.isArray(r.body?.errors?.code)) problems.push('error body lacks message / errors.code');
      if (r.status >= 500 && !exp.includes(r.status)) problems.push(`server error ${r.status}`);
    }
    if (!c && !note.includes('extra')) problems.push('no frontend contract entry for this call');
    this.rows.push({ journey, call: `${verb} ${apiPath}`, status: r.status, ms: r.ms, problems, note });
    return r;
  }

  summary() {
    const failed = this.rows.filter(r => r.problems.length);
    const uncovered = contract.filter(c => !this.covered.has(`${c.verb} ${c.path}`)).map(c => `${c.verb} ${c.path}`);
    const lines = [];
    lines.push(`calls: ${this.rows.length}, with problems: ${failed.length}, frontend endpoints covered: ${this.covered.size}/${contract.length}`);
    for (const f of failed) {
      lines.push(`✗ [${f.journey}] ${f.call} → ${f.status}${f.note ? ' (' + f.note + ')' : ''}`);
      for (const p of f.problems) lines.push(`    - ${p}`);
    }
    if (uncovered.length) lines.push(`not exercised: ${uncovered.join(', ')}`);
    return { text: lines.join('\n'), failed: failed.length };
  }
}

module.exports = { Client, Report, check, contractFor, contract };
