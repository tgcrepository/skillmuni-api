#!/usr/bin/env node
// Builds tools/contract/contract.json from the Angular app's API layer (src/app/core/**/*.api.ts):
// for every Http*Api method: HTTP verb, path template and a JSON Schema of the response type the screens expect.
// usage: node extract-contract.js <path-to-typescript-package> <path-to-skillmuni-web> > contract.json
'use strict';
const path = require('path');
const fs = require('fs');
const ts = require(path.resolve(process.argv[2], 'lib/typescript.js'));
const web = path.resolve(process.argv[3]);

const files = [];
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p);
    else if (e.name.endsWith('.api.ts')) files.push(p);
  }
})(path.join(web, 'src/app/core'));

const program = ts.createProgram(files, {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, moduleResolution: ts.ModuleResolutionKind.Bundler,
  strict: true, noEmit: true, skipLibCheck: true, baseUrl: path.join(web, 'src'),
});
const checker = program.getTypeChecker();

const defs = {};
function schemaOf(type, depth = 0) {
  if (depth > 25) return {};
  const f = type.flags;
  if (f & ts.TypeFlags.Any || f & ts.TypeFlags.Unknown) return {};
  if (f & ts.TypeFlags.Void || f & ts.TypeFlags.Undefined) return { void: true };
  if (f & ts.TypeFlags.Null) return { type: 'null' };
  if (f & ts.TypeFlags.StringLiteral) return { const: type.value };
  if (f & ts.TypeFlags.NumberLiteral) return { const: type.value };
  if (f & ts.TypeFlags.BooleanLiteral) return { const: type.intrinsicName === 'true' };
  if (f & ts.TypeFlags.String || f & ts.TypeFlags.TemplateLiteral) return { type: 'string' };
  if (f & ts.TypeFlags.Number) return { type: 'number' };
  if (f & ts.TypeFlags.Boolean) return { type: 'boolean' };
  if (type.isUnion()) {
    const parts = type.types.map(t => schemaOf(t, depth + 1)).filter(s => !s.void);
    const consts = parts.filter(p => 'const' in p);
    if (consts.length === parts.length && parts.length > 0) {
      if (consts.every(c => typeof c.const === 'boolean')) return { type: 'boolean' };
      return { enum: consts.map(c => c.const) };
    }
    return parts.length === 1 ? parts[0] : { anyOf: parts };
  }
  if (checker.isArrayType(type) || checker.isTupleType(type)) {
    const args = checker.getTypeArguments(type);
    return { type: 'array', items: args.length ? schemaOf(args[0], depth + 1) : {} };
  }
  const name = type.aliasSymbol?.name ?? type.symbol?.name;
  if (name === 'Date') return { type: 'string' };
  if (name === 'Blob' || name === 'File') return {};
  if (f & ts.TypeFlags.Object) {
    const props = checker.getPropertiesOfType(type);
    const index = checker.getIndexInfoOfType(type, ts.IndexKind.String);
    const s = { type: 'object', properties: {}, required: [] };
    for (const p of props) {
      const decl = p.valueDeclaration ?? p.declarations?.[0];
      const pt = decl ? checker.getTypeOfSymbolAtLocation(p, decl) : checker.getTypeOfSymbol(p);
      if (pt.getCallSignatures().length) continue; // methods
      s.properties[p.name] = schemaOf(pt, depth + 1);
      if (!(p.flags & ts.SymbolFlags.Optional)) s.required.push(p.name);
    }
    if (index) s.additionalProperties = schemaOf(index.type, depth + 1);
    return s;
  }
  return {};
}

function pathTemplate(arg) {
  if (!arg) return null;
  if (ts.isStringLiteral(arg) || ts.isNoSubstitutionTemplateLiteral(arg)) return arg.text;
  if (ts.isTemplateExpression(arg)) {
    let s = arg.head.text;
    for (const span of arg.templateSpans) {
      let e = span.expression;
      while (ts.isCallExpression(e) && e.arguments.length) e = e.arguments[0];
      s += '{' + (ts.isIdentifier(e) ? e.text : ts.isPropertyAccessExpression(e) ? e.name.text : 'x') + '}' + span.literal.text;
    }
    return s;
  }
  return '<dynamic>';
}

const out = [];
for (const sf of program.getSourceFiles()) {
  if (!files.includes(path.resolve(sf.fileName))) continue;
  ts.forEachChild(sf, node => {
    if (!ts.isClassDeclaration(node) || !node.name || !/^Http\w+Api$/.test(node.name.text)) return;
    for (const m of node.members) {
      if (!ts.isMethodDeclaration(m) || !m.body || !m.name) continue;
      const calls = [];
      (function find(n) {
        if (ts.isCallExpression(n) && ts.isPropertyAccessExpression(n.expression)
          && ['get', 'post', 'put', 'patch', 'delete'].includes(n.expression.name.text)
          && n.expression.expression.getText(sf).startsWith('this.')) calls.push(n);
        ts.forEachChild(n, find);
      })(m.body);
      // rxjs isn't installed, so unwrap `Observable<T>` from the annotation syntactically.
      let ret = null;
      if (m.type && ts.isTypeReferenceNode(m.type) && m.type.typeName.getText(sf) === 'Observable' && m.type.typeArguments?.length) {
        ret = checker.getTypeFromTypeNode(m.type.typeArguments[0]);
      }
      for (const c of calls) {
        out.push({
          file: path.relative(web, sf.fileName),
          api: node.name.text,
          method: m.name.getText(sf),
          verb: c.expression.name.text.toUpperCase(),
          path: pathTemplate(c.arguments[0]),
          signature: m.parameters.map(p => p.getText(sf)).join(', '),
          request: c.arguments.slice(1).map(a => a.getText(sf).replace(/\s+/g, ' ')).join(' | '),
          response: ret ? schemaOf(ret) : {},
        });
      }
    }
  });
}
process.stdout.write(JSON.stringify(out, null, 1) + '\n');
