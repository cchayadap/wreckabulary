// Pixel diff for golden images, written for the kit so visual_diff.mjs needs no npm package.
// Method credit: pixelmatch (Mapbox, ISC licence) popularised this combination: a perceptual colour distance in YIQ space
// (Kotsarenko and Ramos, "Measuring perceived color difference using YIQ NTSC transmission color space in mobile
// applications", 2010) and anti-aliased pixel detection (Vysniauskas, "Anti-aliased Pixel and Intensity Slope Detector",
// 2009). This is an independent implementation of those published methods; thresholds mean the same as pixelmatch's
// (threshold 0..1, default 0.1; anti-aliased pixels are not counted unless countAA).

const MAX_YIQ_DELTA = 35215; // squared YIQ distance between black and white with the weights below

function blendWhite(c, a) {
  return 255 + ((c - 255) * a) / 255;
}

function yiq(r, g, b) {
  return [
    r * 0.29889531 + g * 0.58662247 + b * 0.11448223,
    r * 0.59597799 - g * 0.2741761 - b * 0.32180189,
    r * 0.21147017 - g * 0.52261711 + b * 0.31114694,
  ];
}

/**
 * Signed perceptual distance between pixel i of a and pixel j of b (alpha blended over white).
 * Negative when a is brighter than b. With lumaOnly, only the luma difference is returned.
 */
function pixelDelta(a, i, b, j, lumaOnly) {
  let r1 = a[i], g1 = a[i + 1], b1 = a[i + 2];
  let r2 = b[j], g2 = b[j + 1], b2 = b[j + 2];
  const a1 = a[i + 3], a2 = b[j + 3];
  if (r1 === r2 && g1 === g2 && b1 === b2 && a1 === a2) return 0;
  if (a1 < 255) { r1 = blendWhite(r1, a1); g1 = blendWhite(g1, a1); b1 = blendWhite(b1, a1); }
  if (a2 < 255) { r2 = blendWhite(r2, a2); g2 = blendWhite(g2, a2); b2 = blendWhite(b2, a2); }
  const [y1, i1, q1] = yiq(r1, g1, b1);
  const [y2, i2, q2] = yiq(r2, g2, b2);
  const dy = y1 - y2;
  if (lumaOnly) return dy;
  const d = 0.5053 * dy * dy + 0.299 * (i1 - i2) * (i1 - i2) + 0.1957 * (q1 - q2) * (q1 - q2);
  return y1 > y2 ? -d : d;
}

function sameAsNeighbours(img, width, height, x, y) {
  // True when at least 3 of the 8 neighbours have exactly this pixel's RGBA (a flat region, not an edge ramp).
  const o = (y * width + x) * 4;
  let same = x === 0 || x === width - 1 || y === 0 || y === height - 1 ? 1 : 0;
  for (let ny = Math.max(0, y - 1); ny <= Math.min(height - 1, y + 1); ny++) {
    for (let nx = Math.max(0, x - 1); nx <= Math.min(width - 1, x + 1); nx++) {
      if (nx === x && ny === y) continue;
      const p = (ny * width + nx) * 4;
      if (img[o] === img[p] && img[o + 1] === img[p + 1] && img[o + 2] === img[p + 2] && img[o + 3] === img[p + 3]) {
        same++;
        if (same > 2) return true;
      }
    }
  }
  return false;
}

function looksAntialiased(img, other, width, height, x, y) {
  // A pixel on an intensity slope between a darker and a brighter neighbour, where the extreme neighbour sits in a flat
  // region in both images, is treated as anti-aliasing rather than a content change.
  const o = (y * width + x) * 4;
  let equal = x === 0 || x === width - 1 || y === 0 || y === height - 1 ? 1 : 0;
  let minD = 0, maxD = 0, minX = -1, minY = -1, maxX = -1, maxY = -1;
  for (let ny = Math.max(0, y - 1); ny <= Math.min(height - 1, y + 1); ny++) {
    for (let nx = Math.max(0, x - 1); nx <= Math.min(width - 1, x + 1); nx++) {
      if (nx === x && ny === y) continue;
      const d = pixelDelta(img, o, img, (ny * width + nx) * 4, true);
      if (d === 0) {
        equal++;
        if (equal > 2) return false;
      } else if (d < minD) { minD = d; minX = nx; minY = ny; }
      else if (d > maxD) { maxD = d; maxX = nx; maxY = ny; }
    }
  }
  if (minD === 0 || maxD === 0) return false;
  return (
    (sameAsNeighbours(img, width, height, minX, minY) && sameAsNeighbours(other, width, height, minX, minY)) ||
    (sameAsNeighbours(img, width, height, maxX, maxY) && sameAsNeighbours(other, width, height, maxX, maxY))
  );
}

/**
 * Compare two RGBA images of the same size.
 * @param {{width: number, height: number, data: Uint8Array}} a baseline
 * @param {{width: number, height: number, data: Uint8Array}} b current
 * @param {{threshold?: number, countAA?: boolean, diff?: boolean}} [opts]
 * @returns {{changed: number, antialiased: number, total: number, diff: Buffer|null}} diff: RGBA image (red = changed,
 *   yellow = anti-aliased, faded grey = unchanged) when opts.diff is true
 */
export function diffImages(a, b, opts = {}) {
  if (a.width !== b.width || a.height !== b.height) throw new Error('imgdiff: images differ in size');
  const { width, height } = a;
  const threshold = opts.threshold ?? 0.1;
  const maxDelta = MAX_YIQ_DELTA * threshold * threshold;
  const A = a.data;
  const B = b.data;
  const out = opts.diff ? Buffer.alloc(width * height * 4) : null;
  let changed = 0;
  let antialiased = 0;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const i = (y * width + x) * 4;
      const d = pixelDelta(A, i, B, i, false);
      if (Math.abs(d) > maxDelta) {
        const aa = !opts.countAA && (looksAntialiased(A, B, width, height, x, y) || looksAntialiased(B, A, width, height, x, y));
        if (aa) {
          antialiased++;
          if (out) { out[i] = 255; out[i + 1] = 255; out[i + 2] = 0; out[i + 3] = 255; }
        } else {
          changed++;
          if (out) { out[i] = 255; out[i + 1] = 0; out[i + 2] = 0; out[i + 3] = 255; }
        }
      } else if (out) {
        const r = blendWhite(A[i], A[i + 3]), g = blendWhite(A[i + 1], A[i + 3]), bl = blendWhite(A[i + 2], A[i + 3]);
        const grey = Math.round(blendWhite(yiq(r, g, bl)[0], 0.1 * 255));
        out[i] = grey; out[i + 1] = grey; out[i + 2] = grey; out[i + 3] = 255;
      }
    }
  }
  return { changed, antialiased, total: width * height, diff: out };
}

/**
 * Mean SSIM over 8x8 luma windows (stride 4). 1 = identical. Images must be the same size and at least 8x8.
 * @param {{width: number, height: number, data: Uint8Array}} a
 * @param {{width: number, height: number, data: Uint8Array}} b
 * @returns {number|null} null for images smaller than one window
 */
export function meanSsim(a, b) {
  const { width, height } = a;
  if (width < 8 || height < 8 || width !== b.width || height !== b.height) return null;
  const luma = (img) => {
    const l = new Float64Array(width * height);
    for (let p = 0; p < width * height; p++) {
      const i = p * 4;
      l[p] = yiq(blendWhite(img[i], img[i + 3]), blendWhite(img[i + 1], img[i + 3]), blendWhite(img[i + 2], img[i + 3]))[0];
    }
    return l;
  };
  const la = luma(a.data);
  const lb = luma(b.data);
  const C1 = (0.01 * 255) ** 2;
  const C2 = (0.03 * 255) ** 2;
  let sum = 0;
  let n = 0;
  for (let y = 0; y + 8 <= height; y += 4) {
    for (let x = 0; x + 8 <= width; x += 4) {
      let ma = 0, mb = 0;
      for (let j = 0; j < 8; j++) for (let i = 0; i < 8; i++) { const p = (y + j) * width + x + i; ma += la[p]; mb += lb[p]; }
      ma /= 64; mb /= 64;
      let va = 0, vb = 0, cov = 0;
      for (let j = 0; j < 8; j++) {
        for (let i = 0; i < 8; i++) {
          const p = (y + j) * width + x + i;
          const da = la[p] - ma, db = lb[p] - mb;
          va += da * da; vb += db * db; cov += da * db;
        }
      }
      va /= 63; vb /= 63; cov /= 63;
      sum += ((2 * ma * mb + C1) * (2 * cov + C2)) / ((ma * ma + mb * mb + C1) * (va + vb + C2));
      n++;
    }
  }
  return n ? sum / n : null;
}
