// Kit clock and UTC formatting. Every kit script reads "now" through now(), so tests can pin it with GSK_NOW.
// Contract: CONTRACTS.md §1.3 (time) and §2 (lib API).

/** @returns {Date} the current time, or the time in env GSK_NOW (ISO 8601) when it is set and valid. */
export function now() {
  const pinned = process.env.GSK_NOW;
  if (pinned) {
    const d = new Date(pinned);
    if (!Number.isNaN(d.getTime())) return d;
  }
  return new Date();
}

/**
 * Kit timestamp: UTC, second precision, `YYYY-MM-DDTHH:MM:SSZ`.
 * @param {Date} [d]
 * @returns {string}
 */
export function utcIso(d = now()) {
  return d.toISOString().replace(/\.\d{3}Z$/, 'Z');
}

/**
 * File-name stamp: `YYYYMMDDTHHMMSSZ` (no separators, sorts lexically).
 * @param {Date} [d]
 * @returns {string}
 */
export function utcFileStamp(d = now()) {
  return utcIso(d).replace(/[-:]/g, '');
}

const ISO_RE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$/;
const STAMP_RE = /^(\d{4})(\d{2})(\d{2})T(\d{2})(\d{2})(\d{2})Z$/;

/** @param {unknown} s @returns {boolean} true for an ISO 8601 date-time with an explicit zone. */
export function isUtcIso(s) {
  return typeof s === 'string' && ISO_RE.test(s) && !Number.isNaN(Date.parse(s));
}

/**
 * Parse an ISO 8601 date-time that carries a zone (Z or ±hh:mm). Anything else returns null.
 * @param {unknown} s
 * @returns {Date|null}
 */
export function parseUtc(s) {
  if (!isUtcIso(s)) return null;
  return new Date(/** @type {string} */ (s));
}

/**
 * Parse a file-name stamp (`YYYYMMDDTHHMMSSZ`), also when embedded in a longer name such as `backup-20261001T115500Z.bundle`.
 * @param {string} s
 * @returns {Date|null}
 */
export function parseFileStamp(s) {
  const m = /(\d{8}T\d{6}Z)/.exec(String(s));
  if (!m) return null;
  const p = STAMP_RE.exec(m[1]);
  if (!p) return null;
  const d = new Date(Date.UTC(+p[1], +p[2] - 1, +p[3], +p[4], +p[5], +p[6]));
  return Number.isNaN(d.getTime()) ? null : d;
}

/** @param {Date} d @param {number} minutes @returns {Date} */
export function addMinutes(d, minutes) {
  return new Date(d.getTime() + minutes * 60000);
}

/** @param {Date} a @param {Date} b @returns {number} (b - a) in minutes, fractional. */
export function minutesBetween(a, b) {
  return (b.getTime() - a.getTime()) / 60000;
}
