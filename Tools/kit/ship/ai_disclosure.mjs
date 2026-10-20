#!/usr/bin/env node
// ai_disclosure.mjs: draft the Steam Content Survey AI disclosure from the ledgers (CONTRACTS.md §4.9; research_ship.md §3.4).
//   node Tools/kit/ship/ai_disclosure.mjs [--build <Builds/dir|latest>] [--out Docs/ship/ai_disclosure.md] [--csv …] [--force] [--json]
// Pre-Generated = AI-made content that ships and players consume: the ai_generated rows the build packs (--build, the
// BuildReport join) or, without a build, the rows marked ships=true. Store page and marketing assets are disclosed too
// (asset.json ai_outputs_at_accept marketing_only, or store/trailer/press paths). Reference-only generations are listed
// separately. Live-Generated is flagged when the build or the package manifest carries a runtime AI or inference library.
// The draft is for the human to review and paste; the agent never submits the survey (some answers lock after approval).
// The human edits only the <!-- human:direction|guardrails|notes --> blocks; a re-run keeps them. The first line records
// a hash of the generated text and aiLedgerDigest() of the ledger: when the text outside the blocks was edited (or the
// file has no marker), the run refuses with exit 1 and changes nothing, unless --force.
// Result: {ok, out, human_blocks_kept, scope, pre_generated:{count, categories}, marketing:{count}, reference_only:{count},
//   live_generated:{detected, hints}, answers:{q1, q2, q3}, warnings}. Exit 0; 1 refused (human edits outside the
//   blocks); 3 when the build cannot be joined.
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, parseCli, emit, isMain, runCli, UsageError, CliError } from '../lib/cli.mjs';
import { readText, writeTextAtomic } from '../lib/jsonio.mjs';
import { utcIso } from '../lib/time.mjs';
import { sha256Text } from '../lib/hash.mjs';
import { shipContext, readLedger, normRowPath, pathKey, relOrAbs } from './lib/ledger.mjs';
import { assetIndex, providerOf } from './lib/rules.mjs';
import { resolveBuildDir, joinBuild, findInPlayer } from './lib/build.mjs';

const RUNTIME_AI = /openai|anthropic|llmunity|llama|onnxruntime|com\.unity\.sentis|com\.unity\.ai\.inference|inworld|convai|gemini|ollama|whisper/i;
const MARKETING_PATH = /(^|\/)(marketing|store|storepage|store_page|press|presskit|trailers?|capsules?|recordings)(\/|$)|(^|\/)docs\/ship\/(store|media)\//i;
const MARKETING_NOTE = /\b(marketing|store page|capsule|key ?art|trailer|press kit)\b/i;
const CLASS_TO_CATEGORY = {
  concept: 'Concept art', sheet: 'Character and prop sheets', prop_mesh: '3D models', character_mesh: '3D models', texture: 'Textures and materials',
  tile: 'Textures and materials', panorama: 'Skies and panoramas', hdri: 'Skies and panoramas', icon: 'Icons and UI art', ui: 'Icons and UI art',
  vfx: 'Visual effects', audio: 'Sound effects', music: 'Music', voice: 'Voice', motion: 'Animation',
};

/** Content category for a ledger row (asset.json class first, then extension and model hints). */
export function categoryOf(row, asset) {
  if (asset?.class && CLASS_TO_CATEGORY[asset.class]) return CLASS_TO_CATEGORY[asset.class];
  const p = normRowPath(row.path).toLowerCase();
  const ext = path.posix.extname(p);
  const model = `${row.generator_model} ${row.provider}`.toLowerCase();
  if (/\.(fbx|glb|gltf|obj|blend|dae|usdz?|3mf|stl)$/.test(p)) return '3D models';
  if (/\.(anim|bvh)$/.test(p)) return 'Animation';
  if (/\.(wav|ogg|mp3|flac|m4a|aiff?)$/.test(p)) {
    if (/music|lyria|sonilo|stable-audio|suno|udio/.test(model) || /(^|\/)music\//.test(p)) return 'Music';
    if (/tts|voice|speech|inworld/.test(model) || /(^|\/)(voice|vo)\//.test(p)) return 'Voice';
    return 'Sound effects';
  }
  if (/\.(mp4|mov|webm|avi)$/.test(p)) return 'Video';
  if (/\.(hdr|exr)$/.test(p) || /skybox|panorama|hdri/.test(model)) return 'Skies and panoramas';
  if (/\.(png|jpe?g|tga|psd|tiff?|webp|bmp|gif|svg)$/.test(p)) return /(^|\/)(ui|icons?)\//.test(p) ? 'Icons and UI art' : 'Images and textures';
  if (/\.(csv|json|po|xliff|txt|asset)$/.test(p) && /locali[sz]|string/.test(p)) return 'Localization and text';
  return ext ? `Other (${ext})` : 'Other';
}

function tally(rows, assets) {
  const cats = {};
  for (const r of rows) {
    const c = categoryOf(r, assets.get(pathKey(r.path)));
    cats[c] ||= { files: 0, providers: new Set() };
    cats[c].files++;
    const who = [providerOf(r) || r.provider, r.generator_model].filter(Boolean).join(' ');
    if (who) cats[c].providers.add(who);
  }
  const out = {};
  for (const [c, v] of Object.entries(cats).sort()) out[c] = { files: v.files, providers: [...v.providers].sort() };
  return out;
}

function table(cats) {
  const rows = Object.entries(cats);
  if (!rows.length) return ['None.'];
  return ['| Category | Files | Providers and models |', '|---|---|---|', ...rows.map(([c, v]) => `| ${c} | ${v.files} | ${v.providers.join(', ') || '(not recorded)'} |`)];
}

/**
 * Compute the disclosure.
 * @param {{ws: string, unity: string, unitySubdir: string, cwd: string}} ctx
 * @param {Record<string,string>[]} allRows
 * @param {{build?: string|null}} [opts]
 */
export function draftDisclosure(ctx, allRows, opts = {}) {
  const warnings = [];
  const assets = assetIndex(ctx.ws);
  const ai = allRows.filter((r) => r.ai_generated === 'true');
  let inGame;
  let scope;
  const hints = [];
  if (opts.build) {
    const dir = resolveBuildDir(ctx, opts.build);
    if (!dir) throw new UsageError(`build folder not found: ${opts.build}`);
    const j = joinBuild(ctx, allRows, dir);
    if (!j.ok) throw new CliError(j.error, EXIT.INFRA);
    inGame = ai.filter((r) => j.rowsInScope.has(r));
    scope = `build ${relOrAbs(ctx.ws, dir)} (BuildReport join)`;
    if (j.unlicensed.length) warnings.push(`${j.unlicensed.length} shipped file(s) have no ledger row, so whether they are AI-made is unknown: run build_join.mjs and add rows`);
    for (const s of j.shipped) if (RUNTIME_AI.test(path.posix.basename(s.path))) hints.push(`runtime library in the build: ${s.path}`);
    const found = findInPlayer(dir, ['OpenAI', 'Anthropic', 'LLMUnity', 'Unity.Sentis', 'Unity.InferenceEngine', 'Inworld', 'Convai']);
    for (const [needle, where] of found) if (where.length) hints.push(`"${needle}" found in ${where.slice(0, 3).join(', ')}`);
  } else {
    inGame = ai.filter((r) => r.ships === 'true');
    scope = 'the ships column of Docs/licenses.csv (pass --build for the real ship scope)';
  }
  try {
    const manifest = readText(path.join(ctx.unity, 'Packages', 'manifest.json'));
    const m = manifest.match(new RegExp(RUNTIME_AI.source, 'gi'));
    if (m) hints.push(`Packages/manifest.json names ${[...new Set(m.map((x) => x.toLowerCase()))].join(', ')}`);
  } catch { /* no Unity manifest */ }
  const inSet = new Set(inGame);
  const rest = ai.filter((r) => !inSet.has(r));
  const marketing = rest.filter((r) => assets.get(pathKey(r.path))?.ai_outputs_at_accept === 'marketing_only' || MARKETING_PATH.test(normRowPath(r.path)) || MARKETING_NOTE.test(r.notes || ''));
  const mSet = new Set(marketing);
  const reference = rest.filter((r) => !mSet.has(r));
  for (const r of ai) if (!r.provider || !r.generator_model) warnings.push(`${normRowPath(r.path)}: ai_generated row without provider or generator_model`);

  const pre = tally(inGame, assets);
  const mk = tally(marketing, assets);
  const ref = tally(reference, assets);
  const providers = [...new Set(inGame.map((r) => providerOf(r) || r.provider).filter(Boolean))].sort();
  const liveDetected = hints.length > 0;
  const answers = {
    q1: inGame.length || marketing.length ? 'Yes' : 'No',
    q2: inGame.length || marketing.length ? 'Yes' : 'No',
    q3: liveDetected ? 'Review' : 'No',
  };
  return { scope, ai_rows: ai.length, inGame, marketing, reference, pre, mk, ref, providers, hints, liveDetected, answers, warnings };
}

// The human writes only inside these blocks; a re-run keeps their text. Edits anywhere else make the next run refuse
// (exit 1) until they are moved into a block or --force discards them.
export const HUMAN_BLOCKS = Object.freeze({
  direction: '[Describe the human direction, selection and editing of this content here.]\n',
  guardrails: '[Guardrails: …]\n',
  notes: '(Your own notes; ai_disclosure.mjs keeps this block when it regenerates the file.)\n',
});
const MARKER_RE = /^<!-- gsk:generated sha256=([0-9a-f]{64}) ledger=([0-9a-f]{64}) -->\n/;
const HUMAN_RE = /<!-- human:([a-z]+) -->\n?([\s\S]*?)<!-- \/human:\1 -->/g;

/**
 * Digest of the ledger rows the disclosure is drafted from (ai_generated rows: path, provider, model, licence, ships,
 * notes). release_check compares it with the one recorded in the file, so unrelated ledger edits never ask for a
 * regeneration. @param {Record<string,string>[]} rows
 */
export function aiLedgerDigest(rows) {
  const ai = rows.filter((r) => r.ai_generated === 'true')
    .map((r) => [normRowPath(r.path), r.provider || '', r.generator_model || '', r.licence_id || '', r.ships || '', r.notes || ''])
    .sort((a, b) => (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0));
  return sha256Text(JSON.stringify(ai));
}

/** Hash of a draft body with the human blocks emptied (edits inside them do not count). @param {string} body */
function bodyHash(body) {
  const t = body.replace(/\r\n/g, '\n').replace(HUMAN_RE, '<!-- human:$1 -->\n<!-- /human:$1 -->').trimEnd();
  return sha256Text(`${t}\n`);
}

/**
 * Read an existing draft: its marker, whether the generated text is untouched, and the human blocks.
 * @param {string} text
 * @returns {{marker: boolean, intact: boolean, ledger: string|null, blocks: Record<string,string>}}
 */
export function parseDraft(text) {
  const t = String(text).replace(/\r\n/g, '\n');
  const blocks = {};
  for (const m of t.matchAll(HUMAN_RE)) blocks[m[1]] = m[2];
  const mk = MARKER_RE.exec(t);
  if (!mk) return { marker: false, intact: false, ledger: null, blocks };
  return { marker: true, intact: bodyHash(t.slice(mk[0].length)) === mk[1], ledger: mk[2], blocks };
}

const block = (id, blocks) => [`<!-- human:${id} -->`, `${(blocks[id] ?? HUMAN_BLOCKS[id]).replace(/\n$/, '')}`, `<!-- /human:${id} -->`];

function renderMarkdown(d, meta, blocks) {
  const cats = Object.keys(d.pre);
  const para = d.inGame.length
    ? `Pre-Generated: ${cats.join(', ').toLowerCase()} in the game were created with the help of generative AI tools (${d.providers.join(', ') || 'providers not recorded'}) during development.${d.liveDetected ? '' : ' No content is generated by AI while the game runs.'}`
    : 'Pre-Generated: no AI-generated content ships in the game (per the ledger).';
  const L = [
    '# Steam Content Survey: AI disclosure (draft)',
    '',
    '> DRAFT for the human to review, edit and paste into Steamworks (App Admin, Content Survey). The agent never submits it.',
    '> Some survey answers cannot be edited after the build and store page are approved without contacting Steam Support,',
    '> so finish this before the first review. Re-run `node Tools/kit/ship/ai_disclosure.mjs` after any new generated asset.',
    '> Write only inside the `<!-- human:… -->` blocks: a re-run keeps them and refuses to overwrite edits made elsewhere.',
    '',
    `- Generated: ${utcIso()} by \`Tools/kit/ship/ai_disclosure.mjs\``,
    `- Ship scope: ${d.scope}`,
    `- Ledger: ${meta.rows} row(s) in \`${meta.csv}\`, ${d.ai_rows} marked ai_generated`,
    '',
    '## Survey questions (January 2026 form; wording from press reports, re-check it in Steamworks)',
    '',
    `1. Does the game use generative AI for content, including the store page, community assets and marketing? **${d.answers.q1}**`,
    `2. Is AI used to generate pre-rendered content for the game, store page, marketing or community assets? **${d.answers.q2}**`,
    `3. Is AI used to generate content or code live, during gameplay? **${d.answers.q3 === 'Review' ? 'Review before answering' : 'No'}**${d.liveDetected ? ' (see Live-Generated below)' : ' (no runtime AI library detected)'}`,
    '',
    '## Pre-Generated content that ships in the game',
    '',
    `Suggested text: ${para}`,
    '',
    'Human direction, selection and editing (paste it after the suggested text):',
    '',
    ...block('direction', blocks),
    '',
    ...table(d.pre),
    '',
    '## Store page, trailers and marketing materials',
    '',
    d.marketing.length ? 'These AI-made assets appear outside the game (store capsules, trailers, press). Disclose them with the Pre-Generated answer.' : 'No AI-generated marketing asset is recorded in the ledger. Check capsules, trailers and store GIFs by hand.',
    '',
    ...table(d.mk),
    '',
    '## Generated but not shipped (reference only)',
    '',
    'Not consumed by players, so disclosure is a judgement call; the conservative choice is a short mention.',
    '',
    ...table(d.ref),
    '',
    '## Live-Generated content',
    '',
    ...(d.liveDetected
      ? ['Possible runtime AI found. If the game generates content while it runs, Steam asks for the guardrails that keep it from generating illegal content; describe them in the block below:', '', ...d.hints.map((h) => `- ${h}`)]
      : ['None detected (no runtime AI or inference library in the build or the package manifest). The block below is needed only if the game does generate content while it runs.']),
    '',
    ...block('guardrails', blocks),
    '',
    '## Notes',
    '',
    '- AI coding tools used to write the game\'s code are outside the survey\'s focus per Valve; a one-line mention is optional.',
    '- AI-assisted translation counts as Pre-Generated content (Valve lists localisation among the examples): add it when used.',
    '- Every shipped AI asset needs a ledger row with provider, generator_model and job_id; `licenses_check.mjs` blocks research-only and blocked-plan outputs.',
    ...d.warnings.map((w) => `- Warning: ${w}`),
    '',
    ...block('notes', blocks),
    '',
  ];
  const body = L.join('\n');
  return `<!-- gsk:generated sha256=${bodyHash(body)} ledger=${meta.digest} -->\n${body}`;
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, { build: { type: 'string' }, out: { type: 'string' }, csv: { type: 'string' }, force: { type: 'boolean', default: false } });
  if (positionals.length) throw new UsageError('usage: ai_disclosure.mjs [--build <dir>] [--out Docs/ship/ai_disclosure.md] [--force] [--json]');
  const ctx = shipContext({ csv: values.csv });
  const ledger = readLedger(ctx.csv);
  const d = draftDisclosure(ctx, ledger.rows, { build: values.build || null });
  if (!ledger.exists) d.warnings.unshift('Docs/licenses.csv does not exist: nothing to disclose from; the draft says No everywhere, which is only true if nothing was generated');
  const out = values.out ? path.resolve(values.out) : path.join(ctx.docsShip, 'ai_disclosure.md');
  let blocks = {};
  if (fs.existsSync(out)) {
    const prev = parseDraft(readText(out));
    const shown = relOrAbs(ctx.ws, out);
    if (!values.force && !prev.marker) throw new CliError(`${shown} has no gsk:generated marker (hand-written, or from an older kit): nothing was changed. Move your text into the <!-- human:… --> blocks of a fresh draft (write it with --out elsewhere first) or pass --force to replace the file`, EXIT.FAIL, { out: shown, refused: 'no_marker' });
    if (!values.force && !prev.intact) throw new CliError(`${shown} was edited outside its <!-- human:… --> blocks: nothing was changed. Move those edits into a block (blocks survive a re-run) or pass --force to discard them`, EXIT.FAIL, { out: shown, refused: 'human_edits' });
    blocks = Object.fromEntries(Object.entries(prev.blocks).filter(([k]) => k in HUMAN_BLOCKS));
  }
  fs.mkdirSync(path.dirname(out), { recursive: true });
  writeTextAtomic(out, renderMarkdown(d, { rows: ledger.rows.length, csv: relOrAbs(ctx.ws, ctx.csv), digest: aiLedgerDigest(ledger.rows) }, blocks));
  const result = {
    ok: true,
    exit: EXIT.OK,
    out: relOrAbs(ctx.ws, out),
    human_blocks_kept: Object.keys(blocks).filter((k) => blocks[k] !== HUMAN_BLOCKS[k]),
    scope: d.scope,
    ai_rows: d.ai_rows,
    pre_generated: { count: d.inGame.length, categories: d.pre, providers: d.providers },
    marketing: { count: d.marketing.length, categories: d.mk },
    reference_only: { count: d.reference.length },
    live_generated: { detected: d.liveDetected, hints: d.hints },
    answers: d.answers,
    warnings: d.warnings,
  };
  emit(result, { json: values.json, text: [`wrote ${result.out}: Pre-Generated ${d.inGame.length} file(s), marketing ${d.marketing.length}, reference-only ${d.reference.length}; live ${d.liveDetected ? 'REVIEW' : 'none detected'}`, ...d.warnings.map((w) => `  warning: ${w}`)] });
  return EXIT.OK;
}

if (isMain(import.meta.url)) await runCli(main);
