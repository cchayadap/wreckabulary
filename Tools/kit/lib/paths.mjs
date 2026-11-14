// Path rules and the project file layout as code. kitPaths() is the machine-readable form of CONTRACTS.md §6–§7.
// W = workspace root (where Claude runs, Docs/ and .claude/ live); U = Unity project root (W or W/<unity_subdir>).
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

/** User home. GSK_HOME overrides it so tests never touch the real ~/.claude. */
export function homeDir() {
  return process.env.GSK_HOME || os.homedir();
}

/** Expand a leading `~` or `~/` to homeDir(). @param {string} p */
export function expandHome(p) {
  if (p === '~') return homeDir();
  if (p.startsWith('~/') || p.startsWith('~\\')) return path.join(homeDir(), p.slice(2));
  return p;
}

/** Backslashes to forward slashes. @param {string} p */
export function toPosix(p) {
  return String(p).replace(/\\/g, '/');
}

const looksWindows = (p) => /^[A-Za-z]:[\\/]/.test(p) || /^\\\\/.test(p);

/**
 * Translate a shell spelling of a path into a native one (the Bash tool on Windows is Git Bash):
 * `~` and `~/x` → homeDir(); and on win32 only: `/c/x` and `/cygdrive/c/x` → `C:/x`, `/tmp/x` → `%TEMP%/x`
 * (Git Bash maps /tmp to the user's TEMP). Other paths are returned unchanged.
 * @param {string} p
 */
export function fromShellPath(p) {
  const s = String(p);
  if (s === '~' || s.startsWith('~/') || s.startsWith('~\\')) return expandHome(s);
  if (process.platform !== 'win32') return s;
  let m = /^\/(?:cygdrive\/)?([A-Za-z])(\/.*)?$/.exec(s);
  if (m) return `${m[1].toUpperCase()}:${m[2] || '/'}`;
  m = /^\/tmp(\/.*)?$/.exec(s);
  if (m) return toPosix(process.env.TEMP || process.env.TMP || os.tmpdir()) + (m[1] || '');
  return s;
}

/**
 * Absolute, POSIX-separated, no trailing slash; lower-cased when case-insensitive (Windows paths or win32).
 * Shell spellings (fromShellPath) are translated first.
 * @param {string} p
 */
export function normalizeForCompare(p) {
  const q = fromShellPath(p);
  let r = toPosix(path.resolve(q));
  if (r.length > 1 && r.endsWith('/') && !/^[A-Za-z]:\/$/.test(r)) r = r.slice(0, -1);
  if (process.platform === 'win32' || looksWindows(q)) r = r.toLowerCase();
  return r;
}

/**
 * True when `child` is `parent` or lies under it (after resolving; case-insensitive on Windows).
 * @param {string} child
 * @param {string} parent
 */
export function isInside(child, parent) {
  const c = normalizeForCompare(child);
  const p = normalizeForCompare(parent);
  if (c === p) return true;
  return c.startsWith(p.endsWith('/') ? p : p + '/');
}

/** POSIX relative path from `from` to `to`. @param {string} from @param {string} to */
export function relPosix(from, to) {
  return toPosix(path.relative(from, to));
}

/**
 * Workspace-relative POSIX path for a file under ws, or null when it is outside ws. Shell spellings are accepted.
 * @param {string} ws
 * @param {string} file absolute or ws-relative
 */
export function wsRel(ws, file) {
  const w = path.resolve(fromShellPath(ws));
  const f = fromShellPath(file);
  const abs = path.resolve(path.isAbsolute(f) ? f : path.join(w, f));
  if (!isInside(abs, w)) return null;
  return relPosix(w, abs);
}

/**
 * Glob → RegExp over POSIX relative paths: `*` and `?` stay inside one segment, `**` crosses segments, `a/**` also
 * matches `a` itself. Case-insensitive on win32 unless nocase says otherwise.
 * @param {string} glob
 * @param {{nocase?: boolean}} [opts]
 */
export function globToRegExp(glob, opts = {}) {
  const g = toPosix(glob);
  let re = '';
  for (let i = 0; i < g.length; i++) {
    const c = g[i];
    if (c === '/' && g.slice(i) === '/**') { re += '(?:/.*)?'; break; }
    if (c === '*' && g[i + 1] === '*') {
      if (g[i + 2] === '/') { re += '(?:.*/)?'; i += 2; } else { re += '.*'; i += 1; }
    } else if (c === '*') re += '[^/]*';
    else if (c === '?') re += '[^/]';
    else re += c.replace(/[.+^${}()|[\]\\]/g, '\\$&');
  }
  const nocase = opts.nocase ?? process.platform === 'win32';
  return new RegExp(`^${re}$`, nocase ? 'i' : '');
}

/** True when the POSIX relative path matches any of the globs. @param {string} rel @param {string[]} globs */
export function matchesAny(rel, globs) {
  const r = toPosix(rel);
  return globs.some((g) => globToRegExp(g).test(r));
}

/**
 * The one normaliser for kit.json unity_subdir / --unity-subdir (U relative to W). Returns '.' or 'a/b' (POSIX, no './'
 * prefix, no trailing slash). Throws (code EUNITYSUBDIR) for an absolute or drive-letter path, a `~` path, any `..`
 * segment, or characters Windows forbids in names, so U is always inside W.
 * @param {string} [sub]
 * @returns {string}
 */
export function normalizeUnitySubdir(sub = '.') {
  const raw = toPosix(sub ?? '.').trim();
  const bad = (why) => {
    const e = new Error(`invalid unity_subdir ${JSON.stringify(sub)}: ${why}`);
    e.code = 'EUNITYSUBDIR';
    return e;
  };
  if (raw.startsWith('/') || /^[A-Za-z]:/.test(raw) || raw.startsWith('~')) throw bad('it must be relative to the workspace');
  if (/[:<>"|?*\u0000-\u001f]/.test(raw)) throw bad('it contains a character Windows does not allow in a folder name');
  const parts = raw.split('/').filter((s) => s && s !== '.');
  if (parts.includes('..')) throw bad("'..' segments are not allowed (U must be W or inside W)");
  return parts.length ? parts.join('/') : '.';
}

/** File name of the installed kit marker, relative to W. */
export const KIT_JSON_REL = 'Tools/kit/kit.json';

/**
 * Find the workspace root: first ancestor (inclusive) of `start` holding Tools/kit/kit.json.
 * Defaults to CLAUDE_PROJECT_DIR, then the current directory. Returns null when there is none.
 * @param {string} [start]
 * @returns {string|null}
 */
export function findWorkspace(start) {
  let dir = path.resolve(start || process.env.CLAUDE_PROJECT_DIR || process.cwd());
  for (;;) {
    if (fs.existsSync(path.join(dir, KIT_JSON_REL))) return dir;
    const up = path.dirname(dir);
    if (up === dir) return null;
    dir = up;
  }
}

/** Parsed Tools/kit/kit.json of a workspace, or null. @param {string} ws */
export function readKitConfig(ws) {
  try {
    return JSON.parse(fs.readFileSync(path.join(ws, KIT_JSON_REL), 'utf8').replace(/^\uFEFF/, ''));
  } catch {
    return null;
  }
}

/** Longest project-folder name Claude Code uses verbatim; longer slugs are cut to this length plus `-<hash>`. */
export const CLAUDE_SLUG_MAX = 200;

/**
 * Claude Code's project folder slug for a directory: every character outside [A-Za-z0-9] becomes '-'.
 * `C:\Users\me\My Game` → `C--Users-me-My-Game`. Exact only for slugs of CLAUDE_SLUG_MAX (200) characters or fewer:
 * Claude Code cuts longer ones to 200 characters and appends `-<hash>` (hash not reproducible here), so code that must
 * find a transcript uses findTranscript() / findClaudeProjectDirs() instead of building the path.
 * @param {string} dir
 */
export function claudeProjectSlug(dir) {
  return String(dir).replace(/[^A-Za-z0-9]/g, '-');
}

/** ~/.claude/projects/<slug>/ for a working directory (exact only when the slug is ≤ 200 chars). @param {string} cwd @param {string} [home] */
export function claudeProjectDir(cwd, home = homeDir()) {
  return path.join(home, '.claude', 'projects', claudeProjectSlug(cwd));
}

/** Main-session transcript path (built, not searched; see findTranscript). @param {string} sessionId @param {string} cwd @param {string} [home] */
export function transcriptPath(sessionId, cwd, home = homeDir()) {
  return path.join(claudeProjectDir(cwd, home), `${sessionId}.jsonl`);
}

/** Subagent transcript folder of a session (`<session>/subagents/`), built. @param {string} sessionId @param {string} cwd @param {string} [home] */
export function subagentDir(sessionId, cwd, home = homeDir()) {
  return path.join(claudeProjectDir(cwd, home), sessionId, 'subagents');
}

function listProjectDirs(home) {
  try {
    return fs.readdirSync(path.join(home, '.claude', 'projects'), { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name);
  } catch {
    return [];
  }
}

/**
 * The existing ~/.claude/projects folders of a working directory: the exact slug folder, plus (slug > 200 chars) every
 * folder named `<first 200 chars>-<hash>`.
 * @param {string} cwd
 * @param {string} [home]
 * @returns {string[]}
 */
export function findClaudeProjectDirs(cwd, home = homeDir()) {
  const root = path.join(home, '.claude', 'projects');
  const slug = claudeProjectSlug(cwd);
  const out = [];
  if (fs.existsSync(path.join(root, slug))) out.push(path.join(root, slug));
  if (slug.length > CLAUDE_SLUG_MAX) {
    const cmp = (s) => (process.platform === 'win32' ? s.toLowerCase() : s);
    const prefix = cmp(`${slug.slice(0, CLAUDE_SLUG_MAX)}-`);
    for (const n of listProjectDirs(home)) if (cmp(n).startsWith(prefix) && cmp(n) !== cmp(slug)) out.push(path.join(root, n));
  }
  return out;
}

/**
 * Locate a main-session transcript: the hook-provided `hint` (stdin transcript_path, also kept in session.json and
 * confirmation lines) when it exists and is `<sessionId>.jsonl`; else the project folders of `cwd`; else any
 * ~/.claude/projects/<x>/<sessionId>.jsonl (session ids are UUIDs). Null when none exists.
 * @param {string} sessionId
 * @param {{cwd?: string, home?: string, hint?: string|null}} [opts]
 * @returns {string|null}
 */
export function findTranscript(sessionId, opts = {}) {
  const home = opts.home ?? homeDir();
  const file = `${sessionId}.jsonl`;
  if (opts.hint) {
    const h = fromShellPath(opts.hint);
    if (path.basename(h).toLowerCase() === file.toLowerCase() && fs.existsSync(h)) return path.resolve(h);
  }
  for (const d of opts.cwd ? findClaudeProjectDirs(opts.cwd, home) : []) {
    if (fs.existsSync(path.join(d, file))) return path.join(d, file);
  }
  for (const n of listProjectDirs(home)) {
    const p = path.join(home, '.claude', 'projects', n, file);
    if (fs.existsSync(p)) return p;
  }
  return null;
}

/** `<transcript folder>/<sessionId>/subagents` of the transcript findTranscript() finds, or null. @param {string} sessionId @param {{cwd?: string, home?: string, hint?: string|null}} [opts] */
export function findSubagentDir(sessionId, opts = {}) {
  const t = findTranscript(sessionId, opts);
  return t ? path.join(path.dirname(t), sessionId, 'subagents') : null;
}

/** Lower-case slug for task and file names: [a-z0-9-], at most 40 chars. @param {string} name */
export function taskSlug(name) {
  const s = String(name).toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 40).replace(/-+$/, '');
  return s || 'project';
}

/** Scratch root that G6 allows: %TEMP%/claude (TEMP, then TMP, then os.tmpdir()). */
export function scratchRoot() {
  return path.join(process.env.TEMP || process.env.TMP || os.tmpdir(), 'claude');
}

/** Workspace-relative owner-control and ledger files (guard G5b; deny rules in .claude/settings.json). */
export const OWNER_CONTROL_FILES = Object.freeze([
  '.harness/ALLOW_STOP_OWNER',
  '.harness/PAUSE',
  '.harness/HARNESS_UNLOCK',
  '.harness/owner_confirmations.jsonl',
  '.harness/overrides.jsonl',
  '.harness/policy.json',
  '.harness/reservations/**',
  'Docs/qa/gates.json',
  'Docs/qa/gates.jsonl',
  'Docs/OWNER_INBOX.md',
]);

/**
 * W-side kit-code roots (workspace-relative globs). Guard G5 protects kitCodeRoots(ws), which adds the U-side roots
 * when the Unity project is a subfolder; this constant alone is only right when U = W.
 */
export const KIT_CODE_ROOTS = Object.freeze([
  'Tools/kit/**',
  '.claude/**',
  '.mcp.json',
  'Packages/com.gamestudiokit.core/**',
]);

/**
 * Every kit-code root guard G5 protects (lifted only by a valid HARNESS_UNLOCK), as W-relative globs: KIT_CODE_ROOTS,
 * plus `<sub>/Tools/kit/**` (gate, U-side lib) and `<sub>/Packages/com.gamestudiokit.core/**` when U ≠ W.
 * @param {string} ws
 * @param {{unitySubdir?: string}} [opts] defaults to kit.json unity_subdir, else '.'
 * @returns {string[]}
 */
export function kitCodeRoots(ws, opts = {}) {
  const sub = normalizeUnitySubdir(opts.unitySubdir ?? readKitConfig(path.resolve(ws))?.unity_subdir ?? '.');
  if (sub === '.') return [...KIT_CODE_ROOTS];
  return [...KIT_CODE_ROOTS, `${sub}/Tools/kit/**`, `${sub}/Packages/com.gamestudiokit.core/**`];
}

/**
 * Every kit-defined file and folder of a project, as absolute paths. This object IS the layout contract.
 * Throws EUNITYSUBDIR when the unity_subdir (option or kit.json) is invalid (normalizeUnitySubdir).
 * @param {string} ws workspace root (W)
 * @param {{unitySubdir?: string}} [opts] defaults to kit.json unity_subdir, else '.'
 */
export function kitPaths(ws, opts = {}) {
  const W = path.resolve(ws);
  const sub = normalizeUnitySubdir(opts.unitySubdir ?? readKitConfig(W)?.unity_subdir ?? '.');
  const U = sub === '.' ? W : path.join(W, sub);
  const j = (...p) => path.join(W, ...p);
  const u = (...p) => path.join(U, ...p);
  const H = (...p) => j('.harness', ...p);
  const D = (...p) => j('Docs', ...p);
  return {
    ws: W,
    unity: U,
    unitySubdir: sub,
    // Tools/kit (W side; the gate lives on the U side)
    toolsKit: j('Tools', 'kit'),
    kitJson: j('Tools', 'kit', 'kit.json'),
    lib: j('Tools', 'kit', 'lib'),
    schemas: j('Tools', 'kit', 'schemas'),
    hooks: j('Tools', 'kit', 'hooks'),
    harnessTools: j('Tools', 'kit', 'harness'),
    host: j('Tools', 'kit', 'host'),
    bin: j('Tools', 'kit', 'bin'),
    workflows: j('Tools', 'kit', 'workflows'),
    gate: u('Tools', 'kit', 'gate'),
    gateConfig: u('Tools', 'kit', 'gate', 'gate.config.json'),
    gateForwarder: j('Tools', 'kit', 'gate', 'game_gate.mjs'),
    kitManifest: j('.kit', 'manifest.json'),
    kitBackups: j('.kit', 'backups'),
    // .harness (gitignored runtime state, §6.5)
    harness: H(),
    policy: H('policy.json'),
    run: H('run.json'),
    session: H('session.json'),
    brief: H('brief.md'),
    health: H('health.json'),
    metrics: H('metrics.jsonl'),
    alerts: H('alerts'),
    heartbeat: H('heartbeat'),
    stopGuard: H('stop_guard.json'),
    stalled: H('STALLED'),
    pause: H('PAUSE'),
    allowStopOwner: H('ALLOW_STOP_OWNER'),
    harnessUnlock: H('HARNESS_UNLOCK'),
    allowStop: H('ALLOW_STOP'),
    waitUntil: H('WAIT_UNTIL'),
    overrides: H('overrides.jsonl'),
    ownerConfirmations: H('owner_confirmations.jsonl'),
    reservations: H('reservations'),
    touched: H('touched'),
    backups: H('backups'),
    stopFailure: H('stopfailure.jsonl'),
    quota: H('quota.jsonl'),
    quotaStatus: H('quota.json'),
    supervisorLog: H('supervisor.jsonl'),
    checkpoint: H('checkpoint.json'),
    unityPid: H('unity.pid'),
    snapshots: H('snapshots'),
    csCheckLock: H('cs_check.lock'),
    csCheckLast: H('cs_check.last.json'),
    laneBlocks: H('lane_blocks.json'),
    inboxLock: H('owner_inbox.lock'),
    guardErrors: H('guard_errors.jsonl'),
    timelapsePid: H('timelapse.pid'),
    // Docs (committed state, §6.2)
    docs: D(),
    task: D('TASK.md'),
    plan: D('PLAN.md'),
    todo: D('TODO.md'),
    devlog: D('DEVLOG.md'),
    devlogArchive: D('devlog'),
    decisions: D('DECISIONS.md'),
    ownerInbox: D('OWNER_INBOX.md'),
    ownerAcks: D('owner_acks.md'),
    changeRequests: D('CHANGE_REQUESTS.md'),
    knownIssues: D('KNOWN_ISSUES.md'),
    styleBible: D('STYLE_BIBLE.md'),
    styleLock: D('style_lock.json'),
    map: D('MAP.md'),
    humanSteps: D('HUMAN_STEPS.md'),
    harnessRules: D('kit', 'HARNESS_RULES.md'),
    gatesJson: D('qa', 'gates.json'),
    gatesJsonl: D('qa', 'gates.jsonl'),
    gateCards: D('qa', 'gates'),
    probesConfig: D('qa', 'probes.config.json'),
    baselines: D('qa', 'baselines.md'),
    qaRuns: D('qa', 'runs'),
    m0Facts: D('qa', 'm0_facts.json'),
    cameras: D('progress', 'cameras.json'),
    progressLog: D('progress', 'log.jsonl'),
    licensesCsv: D('licenses.csv'),
    motionSources: D('motion_sources.csv'),
    report: D('REPORT.md'),
    stats: D('STATS.json'),
    statsDir: D('STATS'),
    generationStats: D('STATS', 'generation.json'),
    costs: D('COSTS.md'),
    waves: D('waves'),
    runs: D('runs'),
    compactions: D('compactions'),
    reviews: D('reviews'),
    ship: D('ship'),
    releaseJson: D('ship', 'release.json'),
    aiDisclosure: D('ship', 'ai_disclosure.md'),
    audioSheet: D('audio', 'AUDIO_SHEET.md'),
    design: D('design'),
    // Build notes (lanes)
    buildNotes: j('BuildNotes'),
    // Generation (ArtSource, §3.7)
    artSource: j('ArtSource'),
    gen: j('ArtSource', '_gen'),
    budget: j('ArtSource', '_gen', 'budget.json'),
    genLedger: j('ArtSource', '_gen', 'ledger.jsonl'),
    genLedgerLock: j('ArtSource', '_gen', 'ledger.lock'),
    inbox: j('_Inbox'),
    recordings: j('Recordings'),
    // Unity side
    logs: u('Logs'),
    gateHistory: u('Logs', 'gate_history.jsonl'),
    sessionsLogs: u('Logs', 'sessions'),
    builds: u('Builds'),
    locks: u('Tools', '.locks'),
    pipelinePort: u('Library', 'Pipeline', '.unity-pipeline-port'),
    unityLockfile: u('Temp', 'UnityLockfile'),
    projectVersion: u('ProjectSettings', 'ProjectVersion.txt'),
    projectSettings: u('ProjectSettings', 'ProjectSettings.asset'),
    packagesManifest: u('Packages', 'manifest.json'),
    kitPackage: u('Packages', 'com.gamestudiokit.core'),
    agentCaptures: u('Assets', '_AgentCaptures'),
    // Claude config (W)
    claudeSettings: j('.claude', 'settings.json'),
    claudeSettingsLocal: j('.claude', 'settings.local.json'),
    mcpJson: j('.mcp.json'),
    claudeMd: j('CLAUDE.md'),
  };
}

/** Done / blocked / progress / context file names of a lane unit. @param {string} ws @param {string} wave @param {string} unit */
export function unitFiles(ws, wave, unit) {
  const d = path.join(ws, 'BuildNotes', wave);
  return {
    dir: d,
    progress: path.join(d, `${unit}.progress.md`),
    done: path.join(d, `${unit}.done.json`),
    donePrev: path.join(d, `${unit}.done.prev.json`),
    blocked: path.join(d, `${unit}.blocked.json`),
    context: path.join(d, `${unit}.context.md`),
    contracts: path.join(d, 'CONTRACTS.md'),
  };
}

/** The first-line lane marker of a builder prompt. @param {string} wave @param {string} unit */
export function laneMarker(wave, unit) {
  return `GSK-LANE:${wave}/${unit}`;
}

/** Parse `GSK-LANE:<wave>/<unit>` out of text; null when absent. @param {string} text */
export function parseLaneMarker(text) {
  const m = /GSK-LANE:([A-Za-z0-9._-]+)\/([A-Za-z0-9._-]+)/.exec(String(text));
  return m ? { wave: m[1], unit: m[2] } : null;
}
