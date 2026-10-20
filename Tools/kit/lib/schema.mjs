// A small JSON Schema (draft 2020-12 subset) validator, so kit scripts need no npm dependency.
// Supported: $ref (same-document JSON pointers, incl. #/$defs/x), type, enum, const, properties, required,
// additionalProperties, patternProperties, propertyNames, min/maxProperties, dependentRequired, items, prefixItems,
// min/maxItems, uniqueItems, contains, minimum, maximum, exclusiveMinimum, exclusiveMaximum, multipleOf,
// min/maxLength, pattern, format (date-time, date, uri, sha256), allOf, anyOf, oneOf, not, if/then/else.
// Unknown keywords (title, description, default, examples, $comment, x-*) are ignored.
import fs from 'node:fs';

/** @typedef {{path: string, keyword: string, message: string}} SchemaError */

const typeOf = (v) => {
  if (v === null) return 'null';
  if (Array.isArray(v)) return 'array';
  if (typeof v === 'number') return Number.isInteger(v) ? 'integer' : 'number';
  return typeof v;
};

const matchesType = (v, t) => {
  const actual = typeOf(v);
  if (t === 'number') return actual === 'number' || actual === 'integer';
  return actual === t;
};

const equal = (a, b) => JSON.stringify(canon(a)) === JSON.stringify(canon(b));
function canon(v) {
  if (Array.isArray(v)) return v.map(canon);
  if (v && typeof v === 'object') {
    const o = {};
    for (const k of Object.keys(v).sort()) o[k] = canon(v[k]);
    return o;
  }
  return v;
}

const FORMATS = {
  'date-time': (s) => /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$/.test(s) && !Number.isNaN(Date.parse(s)),
  date: (s) => /^\d{4}-\d{2}-\d{2}$/.test(s) && !Number.isNaN(Date.parse(s + 'T00:00:00Z')),
  uri: (s) => /^[A-Za-z][A-Za-z0-9+.-]*:[^\s]*$/.test(s),
  sha256: (s) => /^[0-9a-f]{64}$/.test(s),
};

/** Resolve a same-document JSON pointer ('#', '#/$defs/x'). @param {object} root @param {string} ref */
export function resolvePointer(root, ref) {
  if (ref === '#' || ref === '') return root;
  if (!ref.startsWith('#/')) throw new Error(`schema: only same-document $ref is supported, got ${ref}`);
  let cur = root;
  for (const raw of ref.slice(2).split('/')) {
    const key = decodeURIComponent(raw.replace(/~1/g, '/').replace(/~0/g, '~'));
    if (cur === null || typeof cur !== 'object' || !(key in cur)) throw new Error(`schema: $ref ${ref} does not resolve`);
    cur = cur[key];
  }
  return cur;
}

function check(schema, data, ptr, root, errors, depth) {
  if (depth > 200) throw new Error('schema: $ref recursion too deep');
  if (schema === true || schema === undefined) return;
  if (schema === false) { errors.push({ path: ptr || '/', keyword: 'false', message: 'no value allowed here' }); return; }
  if (typeof schema !== 'object' || schema === null) throw new Error('schema: a schema must be an object or boolean');
  const err = (keyword, message) => errors.push({ path: ptr || '/', keyword, message });

  if (schema.$ref) check(resolvePointer(root, schema.$ref), data, ptr, root, errors, depth + 1);

  if (schema.type !== undefined) {
    const types = Array.isArray(schema.type) ? schema.type : [schema.type];
    if (!types.some((t) => matchesType(data, t))) {
      err('type', `expected ${types.join('|')}, got ${typeOf(data)}`);
      return;
    }
  }
  if (schema.enum && !schema.enum.some((e) => equal(e, data))) err('enum', `must be one of ${JSON.stringify(schema.enum)}`);
  if ('const' in schema && !equal(schema.const, data)) err('const', `must equal ${JSON.stringify(schema.const)}`);

  const t = typeOf(data);
  if (t === 'number' || t === 'integer') {
    if (schema.minimum !== undefined && data < schema.minimum) err('minimum', `must be >= ${schema.minimum}`);
    if (schema.maximum !== undefined && data > schema.maximum) err('maximum', `must be <= ${schema.maximum}`);
    if (schema.exclusiveMinimum !== undefined && data <= schema.exclusiveMinimum) err('exclusiveMinimum', `must be > ${schema.exclusiveMinimum}`);
    if (schema.exclusiveMaximum !== undefined && data >= schema.exclusiveMaximum) err('exclusiveMaximum', `must be < ${schema.exclusiveMaximum}`);
    if (schema.multipleOf !== undefined) {
      const q = data / schema.multipleOf;
      if (Math.abs(q - Math.round(q)) > 1e-9) err('multipleOf', `must be a multiple of ${schema.multipleOf}`);
    }
  }
  if (t === 'string') {
    const len = [...data].length;
    if (schema.minLength !== undefined && len < schema.minLength) err('minLength', `length must be >= ${schema.minLength}`);
    if (schema.maxLength !== undefined && len > schema.maxLength) err('maxLength', `length must be <= ${schema.maxLength}`);
    if (schema.pattern !== undefined && !new RegExp(schema.pattern, 'u').test(data)) err('pattern', `must match ${schema.pattern}`);
    if (schema.format && FORMATS[schema.format] && !FORMATS[schema.format](data)) err('format', `must be a valid ${schema.format}`);
  }
  if (t === 'array') {
    const prefix = Array.isArray(schema.prefixItems) ? schema.prefixItems : [];
    prefix.forEach((s, i) => { if (i < data.length) check(s, data[i], `${ptr}/${i}`, root, errors, depth + 1); });
    if (schema.items !== undefined) {
      for (let i = prefix.length; i < data.length; i++) check(schema.items, data[i], `${ptr}/${i}`, root, errors, depth + 1);
    }
    if (schema.minItems !== undefined && data.length < schema.minItems) err('minItems', `must have >= ${schema.minItems} items`);
    if (schema.maxItems !== undefined && data.length > schema.maxItems) err('maxItems', `must have <= ${schema.maxItems} items`);
    if (schema.uniqueItems) {
      const seen = new Set();
      for (const it of data) {
        const k = JSON.stringify(canon(it));
        if (seen.has(k)) { err('uniqueItems', 'items must be unique'); break; }
        seen.add(k);
      }
    }
    if (schema.contains !== undefined) {
      const hits = data.filter((it) => validate(schema.contains, it, { root }).valid).length;
      const min = schema.minContains ?? 1;
      if (hits < min) err('contains', `must contain >= ${min} matching item(s)`);
      if (schema.maxContains !== undefined && hits > schema.maxContains) err('maxContains', `must contain <= ${schema.maxContains} matching items`);
    }
  }
  if (t === 'object') {
    const keys = Object.keys(data);
    for (const r of schema.required || []) if (!(r in data)) err('required', `missing required property "${r}"`);
    if (schema.minProperties !== undefined && keys.length < schema.minProperties) err('minProperties', `must have >= ${schema.minProperties} properties`);
    if (schema.maxProperties !== undefined && keys.length > schema.maxProperties) err('maxProperties', `must have <= ${schema.maxProperties} properties`);
    if (schema.dependentRequired) {
      for (const [k, deps] of Object.entries(schema.dependentRequired)) {
        if (k in data) for (const d of deps) if (!(d in data)) err('dependentRequired', `"${k}" requires "${d}"`);
      }
    }
    const props = schema.properties || {};
    const pats = Object.entries(schema.patternProperties || {}).map(([p, s]) => [new RegExp(p, 'u'), s]);
    for (const k of keys) {
      const sub = `${ptr}/${k.replace(/~/g, '~0').replace(/\//g, '~1')}`;
      if (schema.propertyNames !== undefined) {
        const pe = [];
        check(schema.propertyNames, k, sub, root, pe, depth + 1);
        if (pe.length) err('propertyNames', `property name "${k}" is invalid: ${pe[0].message}`);
      }
      let matched = false;
      if (k in props) { matched = true; check(props[k], data[k], sub, root, errors, depth + 1); }
      for (const [re, s] of pats) if (re.test(k)) { matched = true; check(s, data[k], sub, root, errors, depth + 1); }
      if (!matched && schema.additionalProperties !== undefined) {
        if (schema.additionalProperties === false) err('additionalProperties', `unexpected property "${k}"`);
        else check(schema.additionalProperties, data[k], sub, root, errors, depth + 1);
      }
    }
  }
  if (schema.allOf) for (const s of schema.allOf) check(s, data, ptr, root, errors, depth + 1);
  if (schema.anyOf && !schema.anyOf.some((s) => validate(s, data, { root }).valid)) err('anyOf', 'must match at least one schema in anyOf');
  if (schema.oneOf) {
    const n = schema.oneOf.filter((s) => validate(s, data, { root }).valid).length;
    if (n !== 1) err('oneOf', `must match exactly one schema in oneOf (matched ${n})`);
  }
  if (schema.not !== undefined && validate(schema.not, data, { root }).valid) err('not', 'must not match the "not" schema');
  if (schema.if !== undefined) {
    const branch = validate(schema.if, data, { root }).valid ? schema.then : schema.else;
    if (branch !== undefined) check(branch, data, ptr, root, errors, depth + 1);
  }
}

/**
 * Validate data against a schema.
 * @param {object|boolean} schema
 * @param {any} data
 * @param {{root?: object, pointer?: string}} [opts] root: the document $refs resolve against (defaults to schema);
 *   pointer: validate against a sub-schema of root, e.g. '#/$defs/ledgerRow'
 * @returns {{valid: boolean, errors: SchemaError[]}}
 */
export function validate(schema, data, opts = {}) {
  const root = opts.root ?? schema;
  const target = opts.pointer ? resolvePointer(root, opts.pointer) : schema;
  const errors = [];
  check(target, data, '', root, errors, 0);
  return { valid: errors.length === 0, errors };
}

/** Human-readable one-line summary of errors. @param {SchemaError[]} errors @param {number} [max] */
export function formatErrors(errors, max = 10) {
  const shown = errors.slice(0, max).map((e) => `${e.path}: ${e.message}`);
  if (errors.length > max) shown.push(`… ${errors.length - max} more`);
  return shown.join('; ');
}

/** Throw an Error (code ESCHEMA, .errors) when invalid. @param {object} schema @param {any} data @param {string} [label] @param {{root?: object, pointer?: string}} [opts] */
export function assertValid(schema, data, label = 'document', opts = {}) {
  const r = validate(schema, data, opts);
  if (!r.valid) {
    const e = new Error(`${label} is invalid: ${formatErrors(r.errors)}`);
    e.code = 'ESCHEMA';
    e.errors = r.errors;
    throw e;
  }
}

/** Read a schema file (JSON). @param {string} file */
export function loadSchema(file) {
  return JSON.parse(fs.readFileSync(file, 'utf8').replace(/^﻿/, ''));
}
