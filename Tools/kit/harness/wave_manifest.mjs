// The only writer of Docs/waves/<wave>.json (lanes.schema.json#/$defs/waveManifest). ARCHITECTURE §7.2–§7.4;
// CONTRACTS.md §4.2. Subcommands:
//   init <wave> --milestone M --from <units.json> [--budget N] [--max-blocked N] [--skills a,b] [--force]
//   freeze <wave> --docs <path>…            design_hash = sha256 of sorted `path|sha256` lines
//   args <wave>                             one JSON line: build_wave args with only todo and failed units
//   update <wave> [--harvest <dir>]         done/blocked markers + harvest.json → unit status
//   mark <wave> <unit> --status todo|failed|done|blocked [--reason r]
//   status <wave>
//   snapshot <wave>  /  restore <wave> [--dry-run]     .harness/snapshots/<wave>/ (revert without git, §7.4 step 7a);
//                                           restore moves new files aside to .harness/snapshots/_removed/<wave>-<stamp>/
//   audit <wave> --transcripts <file|dir>…  replay tool calls through the guard (U-29 fallback) → Docs/qa/guard_hits.jsonl
//   resolve <wave> <GH-n> --reason r        clear a guard hit, only after a verified "OWNER: confirm GH-n"
// Exit 0 ok, 1 fail (bad marker, hits, unverified resolve), 2 usage.
import fs from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';
import { fileURLToPath } from 'node:url';
import { now, utcIso, utcFileStamp, parseUtc } from '../lib/time.mjs';
import { readJson, readJsonOr, writeJsonAtomic, appendJsonl, readJsonl, writeTextAtomic } from '../lib/jsonio.mjs';
import { sha256File, sha256Text, listFiles } from '../lib/hash.mjs';
import { findWorkspace, matchesAny, toPosix, unitFiles, wsRel, isInside } from '../lib/paths.mjs';
import { validate, formatErrors } from '../lib/schema.mjs';
import { EXIT, CliError, UsageError, parseCli, emit, isMain, runCli } from '../lib/cli.mjs';
import { withLockSync } from '../lib/locks.mjs';
import { isObj, exists, loadPolicy, redactSecrets } from '../hooks/lib/harness.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const LANES = readJson(path.join(HERE, '..', 'lib', 'schemas', 'lanes.schema.json'));
const ID_RE = /^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$/;
const STATUSES = ['todo', 'running', 'done', 'blocked', 'failed'];
const WALK_SKIP = ['.git', 'node_modules', 'Library', 'Temp', 'Obj', 'Logs', '.harness', '.kit', 'Builds', '.venv'];

function check(def, doc, what) {
  const v = validate(LANES, doc, { pointer: `#/$defs/${def}` });
  if (!v.valid) throw new CliError(`${what} does not match lanes.schema.json#/$defs/${def}: ${formatErrors(v.errors)}`, EXIT.FAIL);
}

const manifestFile = (ws, wave) => path.join(ws, 'Docs', 'waves', `${wave}.json`);

function loadManifest(ws, wave) {
  if (!ID_RE.test(wave || '')) throw new UsageError(`bad wave id: ${wave}`);
  const f = manifestFile(ws, wave);
  if (!exists(f)) throw new UsageError(`no manifest Docs/waves/${wave}.json (run wave_manifest.mjs init ${wave} first)`);
  return readJson(f);
}

function saveManifest(ws, m, t) {
  m.updated_utc = utcIso(t);
  check('waveManifest', m, `Docs/waves/${m.wave}.json`);
  writeJsonAtomic(manifestFile(ws, m.wave), m);
}

const allUnits = (m) => [...(m.contracts || []), ...(m.units || [])].filter(isObj);
const argUnit = (u) => ({ id: u.id, owned: u.owned, context: u.context, brief: u.brief, ...(Array.isArray(u.verify) ? { verify: u.verify } : {}) });

/**
 * Why an owned glob is not confined to the workspace, or null when it is: absolute, drive-letter, UNC or `~` globs and
 * any `..` segment are refused, so snapshot and restore never read or move files outside W.
 * @param {unknown} g
 * @returns {string|null}
 */
export function badOwnedGlob(g) {
  if (typeof g !== 'string' || !g.trim()) return 'empty glob';
  const p = toPosix(g.trim());
  if (p.startsWith('/') || /^[A-Za-z]:/.test(p)) return 'absolute path';
  if (p.startsWith('~')) return 'home path';
  if (p.split('/').some((s) => s === '..')) return "'..' segment";
  if (p.includes(':')) return "':' in a glob";
  return null;
}

/** Files (W-relative POSIX) matching W-relative globs. Globs that leave W (badOwnedGlob) are skipped. */
export function filesMatching(ws, globs) {
  const out = new Set();
  for (const g of globs) {
    if (badOwnedGlob(g)) continue;
    const parts = toPosix(g).split('/');
    const baseParts = [];
    for (const p of parts) { if (/[*?[\]]/.test(p)) break; baseParts.push(p); }
    const base = path.join(ws, ...baseParts);
    let st;
    try { st = fs.statSync(base); } catch { continue; }
    const baseRel = baseParts.join('/');
    if (st.isFile()) { if (matchesAny(baseRel, [g]) || baseParts.length === parts.length) out.add(baseRel); continue; }
    for (const rel of listFiles(base, { skipDirs: WALK_SKIP })) {
      const full = baseRel ? `${baseRel}/${rel}` : rel;
      if (matchesAny(full, [g])) out.add(full);
    }
  }
  return [...out].filter((r) => wsRel(ws, path.join(ws, r)) !== null).sort();
}

/** design_hash over docs (files or folders, W-relative). */
export function designHash(ws, docs) {
  const lines = [];
  for (const d of docs) {
    const abs = path.resolve(ws, d);
    let st;
    try { st = fs.statSync(abs); } catch { throw new UsageError(`design doc not found: ${d}`); }
    const rels = st.isFile() ? [wsRel(ws, abs) ?? toPosix(d)] : listFiles(abs, { skipDirs: WALK_SKIP }).map((r) => `${wsRel(ws, abs) ?? toPosix(d)}/${r}`);
    for (const r of rels) lines.push(`${r}|${sha256File(path.join(ws, r))}`);
  }
  lines.sort();
  return sha256Text(lines.join('\n'));
}

const LABEL_RE = /^(build|fix|review|refute\d*|contracts|integrate|audit)[:]([^#\s]+)(?:#(\d+))?$/;

function harvestFiles(dir) {
  const abs = path.resolve(dir);
  let st;
  try { st = fs.statSync(abs); } catch { throw new UsageError(`--harvest path not found: ${dir}`); }
  if (st.isFile()) return [abs];
  return listFiles(abs, { skipDirs: [] }).filter((r) => r.endsWith('harvest.json')).map((r) => path.join(abs, r));
}

/** {unitId: {builds, failed: [{state, class}], done}} from harvest.json files. */
function harvestByUnit(files) {
  const by = {};
  for (const f of files) {
    const h = readJsonOr(f, null, { onInvalid: 'fallback' });
    if (!isObj(h) || !Array.isArray(h.agents)) continue;
    for (const a of h.agents) {
      if (!isObj(a) || typeof a.label !== 'string') continue;
      const m = LABEL_RE.exec(a.label);
      if (!m) continue;
      const kind = m[1];
      const unit = m[2];
      const e = by[unit] || (by[unit] = { builds: 0, failed: [], done: false });
      if (kind === 'build' || kind === 'contracts') e.builds++;
      if (a.state === 'DONE' && (kind === 'build' || kind === 'fix' || kind === 'contracts')) e.done = true;
      if (a.state === 'FAILED' || a.state === 'UNFINISHED') e.failed.push({ state: a.state, class: a.class ?? 'OTHER', label: a.label });
    }
  }
  return by;
}

async function auditTranscripts(ws, m, sources, t) {
  const { evaluate } = await import('../hooks/guard.mjs');
  const STATELESS = new Set(['G3', 'G4', 'G5b', 'G6', 'G7', 'G9', 'G10', 'G10b', 'G11', 'G14', 'G15']);
  const files = [];
  for (const s of sources) {
    const abs = path.resolve(ws, s);
    let st;
    try { st = fs.statSync(abs); } catch { throw new UsageError(`--transcripts path not found: ${s}`); }
    if (st.isFile()) files.push(abs);
    else for (const r of listFiles(abs, { skipDirs: [] })) if (r.endsWith('.jsonl')) files.push(path.join(abs, r));
  }
  const hitsFile = path.join(ws, 'Docs', 'qa', 'guard_hits.jsonl');
  const { policy } = loadPolicy(ws);
  const prior = readJsonl(hitsFile).filter(isObj);
  const seen = new Set(prior.map((h) => `${h.source}|${h.tool_use_id}`));
  let nextId = prior.reduce((n, h) => Math.max(n, Number(/^GH-(\d+)$/.exec(String(h.id))?.[1] ?? 0)), 0);
  const hits = [];
  const review = [];
  const secretKinds = new Set();
  const redact = (s) => {
    const r = redactSecrets(String(s ?? ''), process.env, policy);
    for (const k of r.kinds) secretKinds.add(k);
    return r.text;
  };
  let calls = 0;
  for (const f of files) {
    const rl = readline.createInterface({ input: fs.createReadStream(f, { encoding: 'utf8' }), crlfDelay: Infinity });
    for await (const line of rl) {
      if (!line.includes('tool_use')) continue;
      let o;
      try { o = JSON.parse(line); } catch { continue; }
      const content = o?.message?.content;
      if (o?.type !== 'assistant' || !Array.isArray(content)) continue;
      for (const part of content) {
        if (!isObj(part) || part.type !== 'tool_use' || typeof part.name !== 'string') continue;
        calls++;
        const d = evaluate({ tool_name: part.name, tool_input: isObj(part.input) ? part.input : {}, cwd: typeof o.cwd === 'string' ? o.cwd : ws }, { ws, audit: true, now: t });
        if (d.decision !== 'deny') continue;
        const src = wsRel(ws, f) ?? toPosix(f);
        const cmd = String(typeof part.input?.command === 'string' ? part.input.command : (part.input?.file_path ?? part.name));
        // Commands are redacted before they reach the committed ledger or stdout (a denied call may hold a pasted key).
        const hit = {
          utc: utcIso(t), milestone: m.milestone, wave: m.wave, source: src, tool_use_id: part.id ?? null, tool_name: part.name, rule: d.rule,
          reason: redact(d.reason).slice(0, 300), command: redact(cmd).slice(0, 300), command_sha256: sha256Text(cmd), resolved: false,
        };
        if (!STATELESS.has(d.rule)) { review.push(hit); continue; }
        const key = `${src}|${hit.tool_use_id}`;
        if (seen.has(key)) continue;
        seen.add(key);
        nextId++;
        const withId = { id: `GH-${String(nextId).padStart(3, '0')}`, ...hit };
        hits.push(withId);
        withLockSync(path.join(ws, '.harness', 'guard_hits.lock'), () => appendJsonl(hitsFile, withId), { waitMs: 10000, purpose: 'wave_manifest audit' });
      }
    }
  }
  if (secretKinds.size) {
    try {
      writeJsonAtomic(path.join(ws, '.harness', 'alerts', 'secret_in_transcript'), {
        utc: utcIso(t), level: 'warn', message: `A secret (${[...secretKinds].join(', ')}) appeared in an audited tool call of wave ${m.wave}; it was redacted from Docs/qa/guard_hits.jsonl. Rotate that key.`, value: null,
      });
    } catch { /* the redaction itself already happened */ }
  }
  return { files: files.length, calls, hits, review, secrets: [...secretKinds] };
}

/**
 * Resolve one guard hit: allowed only when the owner confirmed it (`OWNER: confirm GH-n`, verified against the session
 * transcript like a human gate row) at or after the hit was recorded. Rewrites that line with resolved:true.
 */
async function resolveHit(ws, m, id, reason, t) {
  const hitsFile = path.join(ws, 'Docs', 'qa', 'guard_hits.jsonl');
  const lines = readJsonl(hitsFile);
  const hit = lines.find((h) => isObj(h) && h.id === id);
  if (!hit) throw new UsageError(`no guard hit ${id} in Docs/qa/guard_hits.jsonl`);
  if (hit.wave !== undefined && hit.wave !== m.wave) throw new UsageError(`guard hit ${id} belongs to wave ${hit.wave}, not ${m.wave}`);
  if (hit.resolved === true) return { ok: true, exit: 0, wave: m.wave, id, already_resolved: true };
  const { policy } = loadPolicy(ws);
  const nonInteractive = policy.owner?.noninteractive_entrypoints || ['sdk-cli', 'sdk-ts', 'sdk-py'];
  const { verifyConfirmations } = await import('./gate_close.mjs');
  const v = await verifyConfirmations(ws, id, readJsonl(path.join(ws, '.harness', 'owner_confirmations.jsonl')), nonInteractive);
  const since = parseUtc(hit.utc);
  const ok = v.verified.filter((c) => { const d = parseUtc(c.utc); return d && (!since || d >= since); });
  if (!ok.length) {
    const why = v.status === 'none' ? `no "OWNER: confirm ${id}" line` : (v.status === 'unverified' ? v.why.slice(0, 3).join('; ') : 'the confirmation is older than the hit');
    return { ok: false, exit: EXIT.FAIL, wave: m.wave, id, error: `guard hit ${id} needs the owner to type "OWNER: confirm ${id}" in the session after reviewing it (${why})` };
  }
  const conf = ok[ok.length - 1];
  withLockSync(path.join(ws, '.harness', 'guard_hits.lock'), () => {
    const cur = readJsonl(hitsFile);
    const out = cur.map((h) => (isObj(h) && h.id === id
      ? { ...h, resolved: true, resolved_utc: utcIso(t), resolution: String(reason).slice(0, 300), resolved_inbox_id: conf.inbox_id ?? null }
      : h));
    writeTextAtomic(hitsFile, out.map((h) => JSON.stringify(h)).join('\n') + '\n');
  }, { waitMs: 10000, purpose: 'wave_manifest resolve' });
  return { ok: true, exit: 0, wave: m.wave, id, resolved: true, inbox_id: conf.inbox_id ?? null };
}

function snapshotPaths(ws, m) {
  const all = [...new Set(allUnits(m).flatMap((u) => (Array.isArray(u.owned) ? u.owned : [])))];
  const bad = all.filter((g) => badOwnedGlob(g));
  if (bad.length) throw new UsageError(`owned glob(s) outside the workspace: ${bad.map((g) => `${JSON.stringify(g)} (${badOwnedGlob(g)})`).join(', ')}`);
  const globs = all;
  const files = new Set(filesMatching(ws, globs));
  for (const u of allUnits(m)) {
    const done = readJsonOr(unitFiles(ws, m.wave, u.id).done, null, { onInvalid: 'fallback' });
    for (const f of (isObj(done) && Array.isArray(done.files) ? done.files : [])) {
      const rel = wsRel(ws, path.resolve(ws, f));
      if (rel && exists(path.join(ws, rel))) files.add(rel);
    }
  }
  return { globs, files: [...files].sort() };
}

export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    milestone: { type: 'string' }, from: { type: 'string' }, budget: { type: 'string' }, 'max-blocked': { type: 'string' },
    skills: { type: 'string' }, force: { type: 'boolean', default: false }, docs: { type: 'string', multiple: true },
    harvest: { type: 'string' }, status: { type: 'string' }, reason: { type: 'string' }, 'dry-run': { type: 'boolean', default: false },
    transcripts: { type: 'string', multiple: true },
  });
  const [cmd, wave, ...rest] = positionals;
  const ws = findWorkspace() ?? process.cwd();
  const t = now();
  const out = (r, text) => { emit(r, { json: values.json, text }); return r.exit ?? 0; };
  if (!cmd) throw new UsageError('usage: wave_manifest.mjs init|freeze|args|update|mark|status|snapshot|restore|audit|resolve <wave> …');
  if (!ID_RE.test(wave || '')) throw new UsageError(`bad or missing wave id: ${wave ?? ''}`);

  if (cmd === 'init') {
    if (!values.milestone || !values.from) throw new UsageError('init needs --milestone M --from <units.json>');
    const f = manifestFile(ws, wave);
    if (exists(f) && !values.force) throw new UsageError(`Docs/waves/${wave}.json exists (use --force to replace it)`);
    const src = readJson(path.resolve(values.from));
    const units = Array.isArray(src) ? src : src.units;
    if (!Array.isArray(units) || !units.length) throw new UsageError('--from must hold an array of units or {units:[…]}');
    const mk = (u) => ({ ...argUnit(u), status: 'todo', attempts: 0 });
    for (const u of [...units, ...(src.contracts || [])]) {
      check('argUnit', u, `unit ${u?.id}`);
      for (const g of u.owned) if (badOwnedGlob(g)) throw new UsageError(`unit ${u.id}: owned glob ${JSON.stringify(g)} leaves the workspace (${badOwnedGlob(g)})`);
    }
    const m = { schema: 1, wave, milestone: values.milestone, design_hash: null, design_docs: [], frozen_utc: null, units: units.map(mk) };
    if (Array.isArray(src.contracts) && src.contracts.length) m.contracts = src.contracts.map(mk);
    const gateRows = src.gate_rows;
    if (Array.isArray(gateRows)) m.gate_rows = gateRows;
    if (values.budget !== undefined) m.unity_launch_budget = Number(values.budget);
    if (values['max-blocked'] !== undefined) m.max_blocked = Number(values['max-blocked']);
    const skills = values.skills ? values.skills.split(',').map((s) => s.trim()).filter(Boolean) : (Array.isArray(src.skills) ? src.skills : []);
    if (skills.length) m.skills = skills;
    const ids = allUnits(m).map((u) => u.id);
    if (new Set(ids).size !== ids.length) throw new UsageError('duplicate unit ids');
    saveManifest(ws, m, t);
    return out({ ok: true, exit: 0, wave, units: m.units.length, contracts: (m.contracts || []).length, file: `Docs/waves/${wave}.json` }, [`wrote Docs/waves/${wave}.json (${m.units.length} units)`]);
  }

  const m = loadManifest(ws, wave);

  if (cmd === 'freeze') {
    const docs = [...(values.docs || []), ...rest];
    if (!docs.length) throw new UsageError('freeze needs --docs <path>…');
    m.design_hash = designHash(ws, docs);
    m.design_docs = docs.map((d) => toPosix(d));
    m.frozen_utc = utcIso(t);
    saveManifest(ws, m, t);
    return out({ ok: true, exit: 0, wave, design_hash: m.design_hash, docs: m.design_docs }, [`design_hash ${m.design_hash}`]);
  }

  if (cmd === 'args') {
    const pick = (list) => (list || []).filter((u) => isObj(u) && (u.status === 'todo' || u.status === 'failed')).map(argUnit);
    const a = { wave: m.wave, workspace: toPosix(ws), milestone: m.milestone, design_hash: m.design_hash ?? null, units: pick(m.units) };
    const c = pick(m.contracts);
    if (c.length) a.contracts = c;
    if (Array.isArray(m.gate_rows)) a.gate_rows = m.gate_rows;
    if (m.unity_launch_budget) a.unity_launch_budget = m.unity_launch_budget;
    if (m.max_blocked !== undefined) a.max_blocked = m.max_blocked;
    check('workflowArgs', a, 'args');
    process.stdout.write(JSON.stringify(a) + '\n');
    return 0;
  }

  if (cmd === 'update') {
    const hv = values.harvest ? harvestByUnit(harvestFiles(values.harvest)) : {};
    const changes = [];
    let bad = 0;
    for (const u of allUnits(m)) {
      const f = unitFiles(ws, m.wave, u.id);
      const before = u.status;
      const h = hv[u.id];
      if (h) u.attempts = Math.max(u.attempts || 0, h.builds);
      if (exists(f.done)) {
        const doc = readJsonOr(f.done, null, { onInvalid: 'fallback' });
        const v = validate(LANES, doc, { pointer: '#/$defs/doneMarker' });
        if (v.valid) { u.status = 'done'; u.last_failure = null; u.attempts = Math.max(u.attempts || 0, 1); } else { u.status = 'failed'; u.last_failure = 'invalid done marker'; bad++; }
      } else if (exists(f.blocked)) {
        const doc = readJsonOr(f.blocked, null, { onInvalid: 'fallback' });
        u.status = 'blocked';
        u.last_failure = isObj(doc) && typeof doc.reason === 'string' ? doc.reason.slice(0, 200) : 'blocked';
      } else if (h && h.failed.length) {
        u.status = 'failed';
        u.last_failure = h.failed[h.failed.length - 1].class ?? 'OTHER';
      } else if (h && h.done) {
        u.status = 'failed';
        u.last_failure = 'no done marker';
      } else if (u.status === 'done') {
        u.status = 'failed';
        u.last_failure = 'done marker missing';
      }
      if (u.status !== before) { u.updated_utc = utcIso(t); changes.push({ unit: u.id, from: before, to: u.status, last_failure: u.last_failure ?? null }); }
    }
    saveManifest(ws, m, t);
    return out({ ok: bad === 0, exit: bad ? 1 : 0, wave, changes, invalid_markers: bad }, [`${changes.length} unit(s) changed`, ...changes.map((c) => `- ${c.unit}: ${c.from} -> ${c.to}${c.last_failure ? ` (${c.last_failure})` : ''}`)]);
  }

  if (cmd === 'mark') {
    const id = rest[0];
    const st = values.status;
    if (!id || !['todo', 'failed', 'done', 'blocked'].includes(st)) throw new UsageError('mark <wave> <unit> --status todo|failed|done|blocked [--reason r]');
    const u = allUnits(m).find((x) => x.id === id);
    if (!u) throw new UsageError(`no unit ${id} in wave ${wave}`);
    u.status = st;
    u.updated_utc = utcIso(t);
    if (st === 'failed' || st === 'blocked') u.last_failure = values.reason ?? u.last_failure ?? null;
    else if (values.reason) u.last_failure = values.reason;
    else u.last_failure = null;
    saveManifest(ws, m, t);
    return out({ ok: true, exit: 0, wave, unit: id, status: st }, [`${id}: ${st}`]);
  }

  if (cmd === 'status') {
    const counts = Object.fromEntries(STATUSES.map((s) => [s, 0]));
    for (const u of allUnits(m)) counts[u.status] = (counts[u.status] || 0) + 1;
    const r = { ok: true, exit: 0, wave, milestone: m.milestone, design_hash: m.design_hash ?? null, counts, units: allUnits(m).map((u) => ({ id: u.id, status: u.status, attempts: u.attempts ?? 0, last_failure: u.last_failure ?? null })) };
    return out(r, [`wave ${wave} (${m.milestone}): ${STATUSES.map((s) => `${s} ${counts[s]}`).join(', ')}`, ...r.units.map((u) => `- ${u.id}: ${u.status}${u.last_failure ? ` (${u.last_failure})` : ''}`)]);
  }

  const snapDir = path.join(ws, '.harness', 'snapshots', wave);
  const filesDir = path.join(snapDir, 'files');
  if (cmd === 'snapshot') {
    const { globs, files } = snapshotPaths(ws, m);
    // Only the previous copy and manifest are replaced; files an earlier restore moved aside are never deleted here.
    fs.rmSync(filesDir, { recursive: true, force: true });
    fs.rmSync(path.join(snapDir, 'manifest.json'), { force: true });
    const entries = [];
    for (const rel of files) {
      const src = path.join(ws, rel);
      const dst = path.join(filesDir, rel);
      if (wsRel(ws, src) === null || !isInside(dst, filesDir)) throw new CliError(`refusing to copy ${rel}: outside the workspace or the snapshot folder`, EXIT.FAIL);
      fs.mkdirSync(path.dirname(dst), { recursive: true });
      fs.copyFileSync(src, dst);
      entries.push({ path: rel, existed: true, sha256: sha256File(src) });
    }
    const man = { wave, utc: utcIso(t), files: entries, globs };
    const hv = validate(readJson(path.join(HERE, '..', 'lib', 'schemas', 'harness.schema.json')), man, { pointer: '#/$defs/snapshotManifest' });
    if (!hv.valid) throw new CliError(`snapshot manifest invalid: ${formatErrors(hv.errors)}`, EXIT.FAIL);
    writeJsonAtomic(path.join(snapDir, 'manifest.json'), man);
    return out({ ok: true, exit: 0, wave, files: entries.length, dir: `.harness/snapshots/${wave}` }, [`snapshot of ${entries.length} file(s) in .harness/snapshots/${wave}`]);
  }

  if (cmd === 'restore') {
    const manFile = path.join(snapDir, 'manifest.json');
    if (!exists(manFile)) throw new UsageError(`no snapshot for wave ${wave} (.harness/snapshots/${wave}/manifest.json)`);
    const man = readJson(manFile);
    const badGlobs = (Array.isArray(man.globs) ? man.globs : []).filter((g) => badOwnedGlob(g));
    if (badGlobs.length) throw new CliError(`snapshot manifest has globs outside the workspace: ${badGlobs.join(', ')}`, EXIT.FAIL);
    const known = new Set(man.files.map((f) => f.path));
    const restored = [];
    let unchanged = 0;
    for (const f of man.files) {
      const cur = path.join(ws, f.path);
      const copy = path.join(filesDir, f.path);
      if (wsRel(ws, cur) === null || !isInside(copy, filesDir)) throw new CliError(`refusing to restore ${f.path}: outside the workspace or the snapshot folder`, EXIT.FAIL);
      const same = exists(cur) && f.sha256 && sha256File(cur) === f.sha256;
      if (same) { unchanged++; continue; }
      restored.push(f.path);
      if (!values['dry-run']) {
        fs.mkdirSync(path.dirname(cur), { recursive: true });
        fs.copyFileSync(copy, cur);
      }
    }
    const created = filesMatching(ws, Array.isArray(man.globs) ? man.globs : []).filter((r) => !known.has(r));
    // Outside the per-wave folder, so a later `snapshot <wave>` never touches work moved aside here.
    const removedRoot = path.join(ws, '.harness', 'snapshots', '_removed');
    const removedDir = path.join(removedRoot, `${wave}-${utcFileStamp(t)}`);
    if (!values['dry-run']) {
      for (const r of created) {
        const src = path.join(ws, r);
        const dst = path.join(removedDir, r);
        if (wsRel(ws, src) === null || !isInside(dst, removedDir)) throw new CliError(`refusing to move ${r}: outside the workspace or the removed folder`, EXIT.FAIL);
        fs.mkdirSync(path.dirname(dst), { recursive: true });
        fs.renameSync(src, dst);
      }
      for (const u of allUnits(m)) if (u.status === 'done' || u.status === 'running') { u.status = 'failed'; u.last_failure = 'REVERTED'; u.updated_utc = utcIso(t); }
      saveManifest(ws, m, t);
    }
    return out({ ok: true, exit: 0, wave, dry_run: values['dry-run'], restored, moved_aside: created, unchanged, moved_to: created.length ? toPosix(path.relative(ws, removedDir)) : null },
      [`${values['dry-run'] ? 'would restore' : 'restored'} ${restored.length} file(s), ${values['dry-run'] ? 'would move' : 'moved'} ${created.length} new file(s) aside, ${unchanged} unchanged`]);
  }

  if (cmd === 'audit') {
    const sources = [...(values.transcripts || []), ...rest];
    if (!sources.length) throw new UsageError('audit needs --transcripts <file|dir>…');
    const r = await auditTranscripts(ws, m, sources, t);
    const res = {
      ok: r.hits.length === 0, exit: r.hits.length ? EXIT.FAIL : 0, wave, milestone: m.milestone, files: r.files, tool_calls: r.calls, hits: r.hits.length,
      hit_ids: r.hits.map((h) => h.id), review: r.review.map((h) => ({ rule: h.rule, source: h.source, command: h.command })), ...(r.secrets.length ? { secret_alert: '.harness/alerts/secret_in_transcript' } : {}),
    };
    return out(res, [`audited ${r.calls} tool call(s) in ${r.files} transcript(s): ${r.hits.length} guard hit(s) written to Docs/qa/guard_hits.jsonl, ${r.review.length} owner-token rule(s) to review`,
      ...r.hits.map((h) => `- ${h.id} ${h.rule} ${h.source}: ${h.command.slice(0, 120)}`),
      ...(r.secrets.length ? ['A secret appeared in an audited call and was redacted; see .harness/alerts/secret_in_transcript and rotate that key.'] : [])]);
  }

  if (cmd === 'resolve') {
    const id = rest[0];
    if (!id || !/^GH-\d+$/.test(id) || !values.reason) throw new UsageError('resolve <wave> <GH-n> --reason "<why>" (after the owner typed "OWNER: confirm GH-n")');
    const r = await resolveHit(ws, m, id, values.reason, t);
    return out(r, [r.ok ? `${id}: ${r.already_resolved ? 'already resolved' : 'resolved'}` : `${id}: not resolved: ${r.error}`]);
  }

  throw new UsageError(`unknown subcommand ${cmd}`);
}

if (isMain(import.meta.url)) await runCli(main);
