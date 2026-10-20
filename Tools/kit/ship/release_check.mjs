#!/usr/bin/env node
// release_check.mjs: what still blocks a release (CONTRACTS.md §4.9; research_ship.md §8 checklist; Critique C-13, C-16).
//   node Tools/kit/ship/release_check.mjs [--phase A|B|C|D] [--build <Builds/dir|latest>] [--checklist Docs/ship/checklist.json] [--json]
// --phase X evaluates the checklist items of phases A..X (cumulative); owners: A = agent, H = human only, A→H = agent
// drafts, human decides. Items with a machine check (licences, notices, AI disclosure, smoke, perf, app id) are judged
// from files; the rest come from Docs/ship/checklist.json {schema:1, items:{"B2":{"status":"done"|"todo"|"n/a", utc, by, note}}}.
// --build X runs the build checks: it refuses dev-* and qa-* builds and development players, a Unity Pipeline runtime
// assembly, QaRunner in a shipped player (OWNER_DEFAULTS C-16 option b strips it), steam_appid.txt, unlicensed or blocked
// files, tool binaries, and a THIRD_PARTY_NOTICES.txt that is missing, differs from Docs/ship's generated copy, or lacks
// a shipped CC BY/OGA-BY/MIT/OFL component. No flag at all means --phase A.
// Phase items that judge a build (licences A3-A5, stripped smoke A10) use --build, else the newest release build under
// U/Builds; with no release build they are blocked. A10 needs a PASS for that build id (gates.jsonl row naming it, e.g.
// through its perf.json build_id, or Docs/ship/smoke/*.json {verdict:"PASS", build_id}). A7/B10 compare the AI-row digest
// recorded in ai_disclosure.md with the ledger.
// Result: {ok, blockers:[{id, owner, message}], warnings[], items:[{id, phase, owner, status, title}]}. Exit 1 on any blocker.
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, parseCli, emit, isMain, runCli, UsageError } from '../lib/cli.mjs';
import { readJsonOr, readJsonl, readText } from '../lib/jsonio.mjs';
import { shipContext, loadRelease, relOrAbs, readLedger, normRowPath } from './lib/ledger.mjs';
import { resolveBuildDir, buildIdentity, dataFolder, findInPlayer, latestReleaseBuild } from './lib/build.mjs';
import { runLicencesCheck } from './licenses_check.mjs';
import { runBuildJoin } from './build_join.mjs';
import { needsNotice } from './notices_gen.mjs';
import { aiLedgerDigest, parseDraft } from './ai_disclosure.mjs';

/** The phased checklist (paraphrased from research_ship.md §8). check: machine check id, else the checklist file decides. */
export const CHECKLIST = Object.freeze([
  { id: 'A1', phase: 'A', owner: 'A', title: 'Company and product set in Player Settings and the active Build Profile (not template defaults); semver version', check: 'company_product' },
  { id: 'A2', phase: 'A', owner: 'H', title: 'Splash screen decision (optional on Unity 6 Personal) recorded' },
  { id: 'A3', phase: 'A', owner: 'A', title: 'Every shipped file has a licence row; no blocked licence (licenses_check / build_join)', check: 'licences' },
  { id: 'A4', phase: 'A', owner: 'A→H', title: 'Mixamo clips re-downloaded through the owner\'s own Adobe ID (no mirror sources)', check: 'licences' },
  { id: 'A5', phase: 'A', owner: 'A', title: 'MCP attribution: AnkleBreaker credit line, CoplayDev MIT notice or package removed', check: 'licences' },
  { id: 'A6', phase: 'A', owner: 'A', title: 'CREDITS.md, THIRD_PARTY_NOTICES.txt and credits.json generated after the last ledger change', check: 'notices' },
  { id: 'A7', phase: 'A', owner: 'A', title: 'AI provenance: every generated asset has a ledger row; disclosure drafted after the last ledger change', check: 'ai_disclosure' },
  { id: 'A8', phase: 'A', owner: 'A', title: 'Name pre-screen report (store, domain and handle collisions) in Docs/ship/name_check.md', check: 'name_check' },
  { id: 'A9', phase: 'A', owner: 'H', title: 'Formal trademark search (USPTO classes 9/41, TMview, WIPO) and filing decision' },
  { id: 'A10', phase: 'A', owner: 'A', title: 'Front-end smoke of the stripped release build passed (game_gate smoke --stripped)', check: 'smoke' },
  { id: 'A11', phase: 'A', owner: 'A', title: 'Release-build perf against the per-machine baseline; min specs from measurements (Docs/ship/perf.md)', check: 'perf' },
  { id: 'B1', phase: 'B', owner: 'H', title: 'Steamworks account, distribution agreement, tax, bank and identity interview' },
  { id: 'B2', phase: 'B', owner: 'H', title: 'Steam Direct fee paid ($100; starts the 30-day clock)' },
  { id: 'B3', phase: 'B', owner: 'A', title: 'Real AppID in release.json and the game; steam_appid.txt dev-only', check: 'app_id' },
  { id: 'B4', phase: 'B', owner: 'H', title: 'Builder account created; steamcmd logged in once by the human (cached config.vdf)' },
  { id: 'B5', phase: 'B', owner: 'A', title: 'Depot VDFs rendered; Preview dry run; upload to a beta branch (upload.ps1)' },
  { id: 'B6', phase: 'B', owner: 'A→H', title: 'Store capsules and library art rendered, validated and approved' },
  { id: 'B7', phase: 'B', owner: 'A→H', title: 'At least 5 gameplay screenshots (4 all-ages), no cinematic stills' },
  { id: 'B8', phase: 'B', owner: 'A→H', title: 'Gameplay trailer first, cinematic second; a human watched every frame' },
  { id: 'B9', phase: 'B', owner: 'A→H', title: 'Short and long descriptions approved (no links, no time-based text)' },
  { id: 'B10', phase: 'B', owner: 'A→H', title: 'Content Survey with the AI disclosure drafted from the ledger; the human submits', check: 'ai_disclosure' },
  { id: 'B11', phase: 'B', owner: 'A→H', title: 'Accessibility tags: only features with a test or a human check' },
  { id: 'B12', phase: 'B', owner: 'A→H', title: 'Languages ticked only when 100% translated and human-reviewed' },
  { id: 'B13', phase: 'B', owner: 'H', title: 'Store page submitted for review at least 7 business days early; Coming Soon published' },
  { id: 'C1', phase: 'C', owner: 'H', title: 'Hallway tests: 3 rounds of 5 players (agent builds the builds and reports)' },
  { id: 'C2', phase: 'C', owner: 'H', title: 'Steam Playtest app created; keys granted in batches' },
  { id: 'C3', phase: 'C', owner: 'A→H', title: 'Demo AppID decisions: separate lobbies, achievements off, shared cloud save, demo stats' },
  { id: 'C4', phase: 'C', owner: 'H', title: 'Next Fest registration (one fest per game)' },
  { id: 'C5', phase: 'C', owner: 'H', title: 'Demo live well before the fest' },
  { id: 'C6', phase: 'C', owner: 'A→H', title: 'Press kit page and key list; the human sends keys' },
  { id: 'D1', phase: 'D', owner: 'A→H', title: 'Release candidate on a beta branch; a human plays 30 minutes from a Steam install' },
  { id: 'D2', phase: 'D', owner: 'H', title: 'Default branch set live and Release pressed (never scripted)' },
  { id: 'D3', phase: 'D', owner: 'A→H', title: 'Day-1 watch: crashes, refunds and reviews summarised daily' },
  { id: 'D4', phase: 'D', owner: 'A→H', title: 'Patch flow: beta branch, human check, then default' },
]);

const PHASES = ['A', 'B', 'C', 'D'];
const mtime = (f) => { try { return fs.statSync(f).mtimeMs; } catch { return null; } };

/** The build the phase checks judge: --build, else the newest release build under U/Builds, else null. */
function targetBuild(ctx, cache) {
  if (cache.target === undefined) {
    const dir = cache.buildArg ? resolveBuildDir(ctx, cache.buildArg) : latestReleaseBuild(ctx);
    cache.target = dir ? { dir, id: buildIdentity(dir).id, shown: relOrAbs(ctx.ws, dir) } : null;
  }
  return cache.target;
}

/** The build_id a JSON evidence file names (perf.json from game_gate smoke --stripped), or null. */
function evidenceBuildId(ctx, rel) {
  if (typeof rel !== 'string' || !/\.json$/i.test(rel)) return null;
  for (const base of [ctx.ws, ctx.unity]) {
    const doc = readJsonOr(path.resolve(base, rel), null, { onInvalid: 'fallback' });
    if (doc && typeof doc === 'object') return typeof doc.build_id === 'string' ? doc.build_id : null;
  }
  return null;
}

/**
 * A passing stripped-build smoke for build `id`: the latest gates.jsonl row whose cmd says smoke and stripped and whose
 * cmd, run, notes or evidence (perf.json build_id) names the build is a PASS, or a Docs/ship/smoke/*.json has verdict
 * PASS and that build_id.
 */
function strippedSmokeFor(ctx, id) {
  const names = (s) => typeof s === 'string' && s.includes(id);
  const rows = readJsonl(path.join(ctx.ws, 'Docs', 'qa', 'gates.jsonl')).filter((r) => r && /smoke/i.test(`${r.cmd || ''}`) && /stripped/i.test(`${r.cmd || ''} ${r.notes || ''}`)
    && (names(r.cmd) || names(r.run) || names(r.notes) || (Array.isArray(r.evidence) && r.evidence.some((e) => names(e) || evidenceBuildId(ctx, e) === id))));
  if (rows.length && rows[rows.length - 1].verdict === 'PASS') return true;
  let files = [];
  try { files = fs.readdirSync(path.join(ctx.docsShip, 'smoke')).filter((n) => /\.json$/i.test(n)); } catch { /* none */ }
  return files.some((n) => {
    const doc = readJsonOr(path.join(ctx.docsShip, 'smoke', n), null, { onInvalid: 'fallback' });
    if (!doc || typeof doc !== 'object') return false;
    const pass = doc.verdict === 'PASS' || doc.result === 'PASS';
    return pass && (doc.build_id === id || names(doc.build) || names(doc.build_dir));
  });
}

function machineCheck(id, ctx, cache) {
  const docs = ctx.docsShip;
  switch (id) {
    case 'licences': {
      // The ships column only records intent; the BuildReport join of a real release build decides the ship scope.
      const t = targetBuild(ctx, cache);
      if (!t) return 'no release build joined: run game_gate build --profile release, or pass --build <dir> (the ships column alone is not the ship scope)';
      if (!cache.licences) {
        try { cache.licences = runLicencesCheck({ scope: `build:${t.dir}` }); } catch (e) { cache.licences = { error: e.message, violations: [] }; }
      }
      if (cache.licences.error) return `licences of ${t.shown} could not be checked: ${cache.licences.error}`;
      const f = cache.licences.violations.filter((v) => v.severity === 'FAIL');
      return f.length ? `${f.length} licence problem(s) in ${t.shown}, e.g. ${f[0].rule} ${f[0].path}` : null;
    }
    case 'notices': {
      const files = ['CREDITS.md', 'THIRD_PARTY_NOTICES.txt', 'credits.json'].map((n) => path.join(docs, n));
      const missing = files.filter((f) => !fs.existsSync(f));
      if (missing.length) return `missing ${missing.map((f) => relOrAbs(ctx.ws, f)).join(', ')} (run notices_gen.mjs)`;
      const ledgerTime = mtime(ctx.csv);
      if (ledgerTime && files.some((f) => mtime(f) < ledgerTime)) return 'notices are older than Docs/licenses.csv (re-run notices_gen.mjs)';
      return null;
    }
    case 'ai_disclosure': {
      const f = path.join(docs, 'ai_disclosure.md');
      if (!fs.existsSync(f)) return 'Docs/ship/ai_disclosure.md is missing (run ai_disclosure.mjs)';
      const d = parseDraft(readText(f));
      if (!d.marker) return 'Docs/ship/ai_disclosure.md has no gsk:generated marker (run ai_disclosure.mjs; --force replaces a hand-written file)';
      // Only a change to the AI rows asks for a new draft; the digest is recorded in the file's first line.
      if (d.ledger !== aiLedgerDigest(readLedger(ctx.csv).rows)) return 'the AI rows of Docs/licenses.csv changed since ai_disclosure.md was drafted (re-run ai_disclosure.mjs; it keeps the human blocks)';
      return null;
    }
    case 'name_check':
      return fs.existsSync(path.join(docs, 'name_check.md')) ? null : 'Docs/ship/name_check.md is missing';
    case 'smoke': {
      const t = targetBuild(ctx, cache);
      if (!t) return 'no release build under Builds/: run game_gate build --profile release, then game_gate smoke --stripped --row …';
      return strippedSmokeFor(ctx, t.id) ? null : `no passing stripped smoke for ${t.id} (game_gate smoke --stripped --build ${t.id} --row …, or Docs/ship/smoke/*.json with verdict PASS and build_id ${t.id})`;
    }
    case 'perf':
      return fs.existsSync(path.join(docs, 'perf.md')) ? null : 'Docs/ship/perf.md is missing (release-build perf table and min specs)';
    case 'app_id':
      return cache.release?.app_id ? null : 'Docs/ship/release.json has no app_id';
    case 'company_product': {
      try {
        const t = fs.readFileSync(path.join(ctx.unity, 'ProjectSettings', 'ProjectSettings.asset'), 'utf8');
        const company = (/^\s*companyName:\s*(.*)$/m.exec(t) || [])[1]?.trim();
        const product = (/^\s*productName:\s*(.*)$/m.exec(t) || [])[1]?.trim();
        if (company === 'DefaultCompany' || company === 'Unity Technologies' || /^com\.unity\.template/i.test(product || '')) return `ProjectSettings still has template names (company "${company}", product "${product}")`;
        return null;
      } catch {
        return 'ProjectSettings/ProjectSettings.asset not found (the Unity side is not readable here)';
      }
    }
    default:
      return null;
  }
}

function buildChecks(ctx, buildArg, release) {
  const blockers = [];
  const warnings = [];
  const add = (id, message) => blockers.push({ id, owner: 'A', message });
  const dir = resolveBuildDir(ctx, buildArg);
  if (!dir) throw new UsageError(`build folder not found: ${buildArg}`);
  const idn = buildIdentity(dir);
  const shown = relOrAbs(ctx.ws, dir);
  if (/^dev-/i.test(idn.name) || idn.kind === 'dev') add('build_dev', `${shown} is a development build: it carries the Unity Pipeline runtime and must never ship (use game_gate build --profile release)`);
  else if (/^qa-/i.test(idn.name) || idn.kind === 'release-qa') add('build_qa', `${shown} is a Release-QA build (GSK_QA, QaRunner inside): ship the stripped release build instead`);
  if (idn.development && idn.kind !== 'dev') add('build_development_flag', `${shown} was built as a development player (${idn.reasons.join(', ')})`);
  if (!dataFolder(dir)) warnings.push(`${shown} has no <Product>_Data folder: is it a Windows player build?`);
  const hits = findInPlayer(dir, ['Unity.Pipeline', 'QaRunner']);
  if (hits.get('Unity.Pipeline').length) add('pipeline_runtime', `Unity Pipeline runtime found in the player (${hits.get('Unity.Pipeline').slice(0, 3).join(', ')}): only development players carry it; rebuild without --dev`);
  if (hits.get('QaRunner').length) add('qa_runner_in_release', `QaRunner is compiled into the player (${hits.get('QaRunner').slice(0, 3).join(', ')}): the shipped profile must not define GSK_QA (OWNER_DEFAULTS C-16)`);
  if (fs.existsSync(path.join(dir, 'steam_appid.txt'))) add('steam_appid_txt', 'steam_appid.txt is in the build folder: Valve says never ship it');
  if (!release) add('release_json', 'Docs/ship/release.json is missing or invalid (platforms, steam_drm_wrapper, development_build)');
  else if (release.development_build === true) add('release_json', 'release.json says development_build true');
  let join = null;
  try {
    join = runBuildJoin({ build: buildArg, withRows: true });
  } catch (e) {
    add('build_join', `build_join could not run: ${e.message}`);
  }
  if (join) {
    if (join.unlicensed.length) add('unlicensed_files', `${join.unlicensed.length} shipped file(s) have no licence row, e.g. ${join.unlicensed.slice(0, 3).join(', ')}`);
    if (join.blocked.length) add('blocked_licences', `${join.blocked.length} blocked licence finding(s), e.g. ${join.blocked[0].rule} ${join.blocked[0].path}`);
    if (join.tool_binaries.length) add('tool_binaries', `dev-only tool binaries in the build: ${join.tool_binaries.slice(0, 3).join(', ')}`);
    warnings.push(...join.warnings.slice(0, 20));
  }
  for (const b of noticeChecks(ctx, dir, join?.rows_in_scope ?? null)) add(b.id, b.message);
  return { blockers, warnings, build: { dir: shown, kind: idn.kind, development: idn.development } };
}

const noticeLines = (t) => String(t).replace(/\r\n/g, '\n').split('\n').filter((l) => !/^Generated /.test(l)).map((l) => l.trimEnd()).join('\n').trim();

/**
 * The build's THIRD_PARTY_NOTICES.txt must exist, equal the generated Docs/ship copy (the "Generated <utc>" line
 * aside), and name every shipped component that needs a notice (needsNotice: CC BY, OGA-BY, MIT, OFL, …).
 * @param {{ws: string, docsShip: string}} ctx
 * @param {string} dir build folder
 * @param {Record<string,string>[]|null} rowsInScope ledger rows the build ships (null when the join failed)
 * @returns {{id: string, message: string}[]}
 */
export function noticeChecks(ctx, dir, rowsInScope) {
  const out = [];
  const inBuildFile = path.join(dir, 'THIRD_PARTY_NOTICES.txt');
  if (!fs.existsSync(inBuildFile)) return [{ id: 'notices_missing_in_build', message: 'THIRD_PARTY_NOTICES.txt is not in the build root (run notices_gen.mjs --build <dir> and copy Docs/ship/THIRD_PARTY_NOTICES.txt there in the post-build step)' }];
  const inBuild = readText(inBuildFile);
  const docsFile = path.join(ctx.docsShip, 'THIRD_PARTY_NOTICES.txt');
  if (!fs.existsSync(docsFile)) out.push({ id: 'notices_stale_in_build', message: 'Docs/ship/THIRD_PARTY_NOTICES.txt is missing, so the copy in the build cannot be checked (run notices_gen.mjs --build <dir>, then copy it into the build root)' });
  else if (noticeLines(readText(docsFile)) !== noticeLines(inBuild)) out.push({ id: 'notices_stale_in_build', message: "the build's THIRD_PARTY_NOTICES.txt differs from Docs/ship/THIRD_PARTY_NOTICES.txt (a stub or an older copy): run notices_gen.mjs --build <dir> and copy the result into the build root" });
  if (rowsInScope) {
    const text = inBuild.toLowerCase();
    const missing = rowsInScope.filter(needsNotice).filter((r) => {
      const p = normRowPath(r.path);
      const stem = path.posix.basename(p.replace(/\/$/, '')).replace(/\.[^.]+$/, '');
      return ![stem, r.author, p].some((k) => k && k.trim().length >= 2 && text.includes(k.trim().toLowerCase()));
    });
    if (missing.length) out.push({ id: 'notices_incomplete', message: `${missing.length} shipped component(s) that need a notice are missing from the build's THIRD_PARTY_NOTICES.txt, e.g. ${missing.slice(0, 3).map((r) => `${normRowPath(r.path)} (${r.licence_id})`).join(', ')}: run notices_gen.mjs --build <dir> and copy the result into the build root` });
  }
  return out;
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, { phase: { type: 'string' }, build: { type: 'string' }, checklist: { type: 'string' } });
  if (positionals.length) throw new UsageError('usage: release_check.mjs [--phase A|B|C|D] [--build <dir>] [--json]');
  const phase = values.phase ? values.phase.toUpperCase() : values.build ? null : 'A';
  if (phase && !PHASES.includes(phase)) throw new UsageError('--phase must be A, B, C or D');
  const ctx = shipContext({});
  const { release } = loadRelease(ctx.releaseFile);
  const blockers = [];
  const warnings = [];
  const items = [];
  let build = null;
  if (values.build) {
    const b = buildChecks(ctx, values.build, release);
    blockers.push(...b.blockers);
    warnings.push(...b.warnings);
    build = b.build;
  }
  if (phase) {
    const clFile = values.checklist ? path.resolve(values.checklist) : path.join(ctx.docsShip, 'checklist.json');
    const cl = readJsonOr(clFile, { schema: 1, items: {} }, { onInvalid: 'fallback' });
    const cache = { buildArg: values.build || null, release };
    const upto = PHASES.slice(0, PHASES.indexOf(phase) + 1);
    for (const it of CHECKLIST.filter((c) => upto.includes(c.phase))) {
      const recorded = cl.items?.[it.id]?.status;
      let status;
      let message = null;
      if (it.check) {
        message = machineCheck(it.check, ctx, cache);
        status = message ? 'blocked' : 'done';
      } else {
        status = recorded === 'done' || recorded === 'n/a' ? recorded : 'todo';
        if (status === 'todo') message = it.owner === 'H' ? `human step not recorded as done in ${relOrAbs(ctx.ws, clFile)}: ${it.title}` : `not recorded as done: ${it.title}`;
      }
      items.push({ id: it.id, phase: it.phase, owner: it.owner, status, title: it.title });
      if (message) blockers.push({ id: it.id, owner: it.owner, message });
    }
  }
  const ok = blockers.length === 0;
  const result = { ok, exit: ok ? EXIT.OK : EXIT.FAIL, phase, build, blockers, warnings, items };
  const text = [
    `${ok ? 'READY' : 'BLOCKED'}${phase ? ` through phase ${phase}` : ''}${build ? ` (build ${build.dir}, ${build.kind})` : ''}: ${blockers.length} blocker(s)`,
    ...blockers.map((b) => `  [${b.owner}] ${b.id}: ${b.message}`),
    ...warnings.map((w) => `  warning: ${w}`),
  ];
  emit(result, { json: values.json, text });
  return result.exit;
}

if (isMain(import.meta.url)) await runCli(main);
