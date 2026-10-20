// CLI conventions shared by every kit node script (CONTRACTS.md §1.5): exit codes, --json result line, main() export.
import fs from 'node:fs';
import { parseArgs } from 'node:util';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

/** Exit codes of kit CLIs. Hooks have their own rules (CONTRACTS.md §5). */
export const EXIT = Object.freeze({
  OK: 0, // PASS / success
  FAIL: 1, // the check ran and found problems (FAIL verdict)
  USAGE: 2, // bad arguments
  INFRA: 3, // environment or infrastructure: missing tool, key, licence, kit not installed; never a code failure
  BLOCKED: 75, // a resource is held (editor holds the project, lock wait expired): stop and report
});

/** Thrown for bad arguments; runCli maps it to EXIT.USAGE. */
export class UsageError extends Error {
  constructor(message) {
    super(message);
    this.name = 'UsageError';
    this.exitCode = EXIT.USAGE;
  }
}

/** An error that carries its exit code. */
export class CliError extends Error {
  /** @param {string} message @param {number} [exitCode] @param {object} [result] extra fields for the --json line */
  constructor(message, exitCode = EXIT.FAIL, result = undefined) {
    super(message);
    this.name = 'CliError';
    this.exitCode = exitCode;
    this.result = result;
  }
}

/**
 * node:util parseArgs with the kit's defaults (strict, positionals allowed). `--json` is always accepted.
 * @param {string[]} argv arguments after the script path
 * @param {Record<string, {type: 'string'|'boolean', short?: string, multiple?: boolean, default?: any}>} [options]
 * @returns {{values: Record<string, any>, positionals: string[], rest: string[]}} rest: arguments after a bare `--`
 */
export function parseCli(argv, options = {}) {
  const dd = argv.indexOf('--');
  const head = dd >= 0 ? argv.slice(0, dd) : argv;
  const rest = dd >= 0 ? argv.slice(dd + 1) : [];
  try {
    const { values, positionals } = parseArgs({
      args: head,
      options: { json: { type: 'boolean', default: false }, ...options },
      allowPositionals: true,
      strict: true,
    });
    return { values, positionals, rest };
  } catch (e) {
    throw new UsageError(e.message);
  }
}

/**
 * Print a result. With json: exactly one JSON line on stdout (human text belongs on stderr). Without: `text` lines, or key: value.
 * @param {object} result
 * @param {{json?: boolean, text?: string|string[]}} [opts]
 */
export function emit(result, opts = {}) {
  if (opts.json) {
    process.stdout.write(JSON.stringify(result) + '\n');
    return;
  }
  const text = opts.text ?? Object.entries(result).map(([k, v]) => `${k}: ${typeof v === 'object' ? JSON.stringify(v) : v}`);
  process.stdout.write((Array.isArray(text) ? text.join('\n') : text) + '\n');
}

/** Human progress to stderr (keeps stdout clean for --json). @param {...unknown} parts */
export function note(...parts) {
  process.stderr.write(parts.map(String).join(' ') + '\n');
}

/**
 * True when the module at `metaUrl` is the process entry point (so importing it for tests or a forwarder does not run it).
 * Compares real paths: Node resolves import.meta.url through junctions and symlinks but leaves process.argv[1] as typed,
 * so a script started through a junction (`mklink /J`) or symlink still counts as main. Falls back to comparing URLs.
 * @param {string} metaUrl import.meta.url
 */
export function isMain(metaUrl) {
  const argv1 = process.argv[1];
  if (!argv1) return false;
  const norm = (p) => (process.platform === 'win32' ? p.toLowerCase() : p);
  try {
    const entry = fs.realpathSync.native(path.resolve(argv1));
    const self = fs.realpathSync.native(fileURLToPath(metaUrl));
    if (norm(entry) === norm(self)) return true;
  } catch {
    /* fall back to the URL comparison */
  }
  try {
    return pathToFileURL(path.resolve(argv1)).href.toLowerCase() === String(metaUrl).toLowerCase();
  } catch {
    return false;
  }
}

/**
 * Run `main(argv) → exit code` with the kit's error mapping, and set process.exitCode.
 * UsageError → 2; CliError → its code (and its result as the --json line when --json was passed); other errors → 1.
 * @param {(argv: string[]) => Promise<number>|number} main
 * @param {string[]} [argv]
 */
export async function runCli(main, argv = process.argv.slice(2)) {
  try {
    process.exitCode = (await main(argv)) ?? EXIT.OK;
  } catch (e) {
    const code = typeof e.exitCode === 'number' ? e.exitCode : EXIT.FAIL;
    if (argv.includes('--json')) {
      process.stdout.write(JSON.stringify({ ok: false, exit: code, error: e.message, ...(e.result || {}) }) + '\n');
    }
    process.stderr.write(`${e.name === 'UsageError' ? 'usage' : 'error'}: ${e.message}\n`);
    process.exitCode = code;
  }
  return process.exitCode;
}
