import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';

const root = process.cwd();
const packagePath = 'BuildNotes/developer-workspace/home-layout';
const draft = `${packagePath}/draft`;
const baseline = `${packagePath}/baseline`;
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const fileHash = file => hash(fs.readFileSync(file));
const run = args => {
  const result = spawnSync('git', args, { cwd: root, encoding: 'utf8', windowsHide: true });
  if (result.error) throw result.error;
  return { exit: result.status, stdout: result.stdout, stderr: result.stderr };
};
const requirePass = result => { if (result.exit !== 0) throw new Error(JSON.stringify(result)); return result; };
const files = fs.readdirSync(`${draft}/Assets`, { recursive: true, withFileTypes: true })
  .filter(entry => entry.isFile() && entry.name.endsWith('.cs'))
  .map(entry => path.relative(draft, path.join(entry.parentPath, entry.name)).replaceAll('\\', '/')).sort();
if (files.length !== 8) throw new Error(`Expected eight reviewed C# files; found ${files.length}`);
const original = JSON.parse(fs.readFileSync(`${packagePath}/manifest.json`, 'utf8'));
const oldPatchHash = fileHash(`${packagePath}/home-layout.patch`);
if (oldPatchHash !== original.patch_sha256) throw new Error('Original patch changed');
const baselines = original.baselines.map(entry => {
  const baselineHash = fileHash(`${baseline}/${entry.path}`);
  const liveHash = fileHash(entry.path);
  if (baselineHash !== entry.sha256 || liveHash !== entry.sha256) throw new Error(`Baseline/live mismatch: ${entry.path}`);
  requirePass(run(['diff', '--exit-code', 'HEAD', '--', entry.path]));
  return { ...entry, baseline_unchanged: true, live_unchanged: true, live_matches_head: true };
});
let patch = '';
for (const file of files) {
  const old = fs.existsSync(`${baseline}/${file}`) ? `${baseline}/${file}` : '/dev/null';
  if (old === '/dev/null' && fs.existsSync(file)) throw new Error(`New path already exists: ${file}`);
  const result = run(['diff', '--no-index', '--no-renames', '--no-color', '--', old, `${draft}/${file}`]);
  if (![0, 1].includes(result.exit)) throw new Error(JSON.stringify(result));
  patch += result.stdout.split('\n').map(line => {
    if (!line.startsWith('diff --git ') && !line.startsWith('--- ') && !line.startsWith('+++ ')) return line;
    return line.replaceAll(`a/${baseline}/`, 'a/').replaceAll(`b/${draft}/`, 'b/').replaceAll(`a/${draft}/`, 'a/');
  }).join('\n');
}
const reviewedPatch = `${packagePath}/reviewed-home-layout.patch`;
fs.writeFileSync(reviewedPatch, patch);
const apply = requirePass(run(['apply', '--check', '--index', '--whitespace=error', reviewedPatch]));
const syntax = JSON.parse(fs.readFileSync(`${draft}/review-syntax.json`, 'utf8'));
if (syntax.after.length !== 8 || syntax.after.some(file => file.errors.length)) throw new Error('Reviewed syntax failed');
const dependencies = [
  'Assets/_Project/Scripts/UI/Lobby/LobbyMenu.cs',
  'Assets/_Project/Scripts/UI/Lobby/LobbyKit.cs',
  'Assets/_Project/Scripts/UI/Lobby/LobbyRuntimeTypography.cs',
  'Assets/_Project/Scripts/UI/Lobby/LobbyShopActionView.cs',
  'Assets/_Project/Scripts/UI/Lobby/LobbyShopLayout.cs',
  'Assets/_Project/Tests/Editor/ShopLayout/Wreckabulary.ShopLayout.Editor.Tests.asmdef',
  'Assets/_Project/Editor/Wreckabulary.Editor.asmdef',
  'Assets/_Project/Tests/TestScenes.cs'
].map(file => ({ path: file, sha256: fileHash(file) }));
const manifest = {
  status: 'SYNTAX-REPAIRED; SOURCE-REVIEWED; NOT SEMANTICALLY COMPILED OR NATIVELY VERIFIED',
  recorded_utc: new Date().toISOString(),
  head: requirePass(run(['rev-parse', 'HEAD'])).stdout.trim(),
  original_patch: { path: `${packagePath}/home-layout.patch`, sha256: oldPatchHash, preserved: true },
  reviewed_patch: { path: reviewedPatch, sha256: fileHash(reviewedPatch) },
  patch_apply_check: { verdict: 'PASS', method: 'git apply --check --index --whitespace=error; no patch applied', ...apply },
  syntax_review: { path: `${draft}/review-syntax.json`, sha256: fileHash(`${draft}/review-syntax.json`), original_errors: syntax.before.errors.length, reviewed_errors: 0, reviewed_files: 8 },
  repairs: [{ path: 'Assets/_Project/Editor/Authoring/LobbyHomeLayoutSetup.cs', lines: [54,75], detail: 'Escape backslash character literals in Replace and Contains. Original patch has ten parser errors; reviewed draft has none.' }],
  baselines,
  files: files.map(file => ({ path: file, sha256: fileHash(`${draft}/${file}`) })),
  dependencies,
  review: {
    callbacks: 'Owned ShopAction listener is replaced on rebind and removed on destruction. Hidden/unavailable actions bind null, including all actions during countdown. Stable focus names and Menu callbacks retained.',
    authoring: 'Shared default factory for fallback/setup; saved profile is authoritative. Malformed assigned profile fails. Existing profile/prefab/dependencies are validated and preserved; setup publishes profile last.',
    typography: 'Runtime saved-prefab instantiation uses inactive staging and owned font/material copies. Factory Text assigns owned font before activation. Authoritative geometry/font/material references not reset by Bind.',
    shared_files: 'Only HomePage.Build changes in LobbyPages; only SaveTemplate visibility changes in ShopLayoutSetup. Current live hashes equal captured baselines and HEAD.',
    workspace: 'No automatic workspace registration included. Root must wire workspace setup and profile/prefab shortcuts separately.',
    limitations: 'Syntax parsing does not resolve Unity types or execute tests. Root must run official R0, native five Editor/two Play cases and broader lobby regressions, byte-preservation checks and visual/direct input review.'
  },
  proposed_tests: {
    editmode_cases: 5,
    editor_expectations: [
      'Authored dock/hint/action size, Start text and font/material persist through save/reimport/bind/repeat; profile/prefab/material bytes and GUID/file IDs remain unchanged.',
      'Callback rebinding invokes only newest callbacks once; blocked Start and hidden optional actions retain no invoked callbacks; countdown removes all callbacks and hides dock.',
      'Hint-owning-dock hierarchy is rejected and saved malformed bytes remain unchanged.',
      'Optional-house-owning-Start hierarchy is rejected and saved malformed bytes remain unchanged.',
      'Creation preserves typography/dependency bytes, active scene/roots/dirty state, preferences, scene count and leaves no temporary scene.'
    ],
    playmode_cases: 2,
    play_expectations: [
      'Explicit saved Home layout present; authored style retained; mode/house route and focus correct through party join/leave; legacy online block and Tutorial visibility retained; outfit/coins unchanged.',
      'Real drag handlers dismiss hint/persist seen; Start countdown hides dock and focuses CANCEL; cancel restores GO in Hub. Delay/preferences/scenes restored by finally/teardown.'
    ],
    execution: 'NOT-RUN'
  }
};
fs.writeFileSync(`${packagePath}/reviewed-home-layout.manifest.json`, JSON.stringify(manifest, null, 2) + '\n');
process.stdout.write(JSON.stringify({ files: files.length, originalPatchPreserved: true, syntaxErrors: 0, patchApplyCheck: 'PASS', patchSha256: manifest.reviewed_patch.sha256 }, null, 2));
