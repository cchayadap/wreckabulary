// Dependency-free PNG codec for the QA tools (node:zlib only; the kit has no npm dependencies, CONTRACTS.md §1.8).
// decodePng: every colour type (grey, RGB, palette with tRNS, grey+alpha, RGBA), bit depths 1/2/4/8/16, Adam7 interlace,
// all five row filters. Output is always 8-bit RGBA. encodePng writes 8-bit RGBA with one filter per row.
import zlib from 'node:zlib';

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

let crcTable = null;
function crc32(buf) {
  if (typeof zlib.crc32 === 'function') return zlib.crc32(buf) >>> 0;
  if (!crcTable) {
    crcTable = new Uint32Array(256);
    for (let n = 0; n < 256; n++) {
      let c = n;
      for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
      crcTable[n] = c >>> 0;
    }
  }
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = crcTable[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

/** True when the buffer starts with the PNG signature. @param {Buffer} buf */
export function isPng(buf) {
  return Buffer.isBuffer(buf) && buf.length >= 8 && buf.subarray(0, 8).equals(SIGNATURE);
}

/**
 * Width and height from the IHDR chunk, without inflating the image. Null when the buffer is not a PNG.
 * @param {Buffer} buf
 * @returns {{width: number, height: number}|null}
 */
export function pngSize(buf) {
  if (!isPng(buf) || buf.length < 24 || buf.toString('latin1', 12, 16) !== 'IHDR') return null;
  return { width: buf.readUInt32BE(16), height: buf.readUInt32BE(20) };
}

const CHANNELS = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 };
const ADAM7 = [
  [0, 0, 8, 8],
  [4, 0, 8, 8],
  [0, 4, 4, 8],
  [2, 0, 4, 4],
  [0, 2, 2, 4],
  [1, 0, 2, 2],
  [0, 1, 1, 2],
];

function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  return pb <= pc ? b : c;
}

function unfilter(data, offset, width, height, bpp, bitDepth, channels) {
  const stride = Math.ceil((width * channels * bitDepth) / 8);
  const out = Buffer.alloc(stride * height);
  let pos = offset;
  for (let y = 0; y < height; y++) {
    if (pos >= data.length) throw new Error('png: image data is truncated');
    const type = data[pos++];
    const row = y * stride;
    const prev = row - stride;
    for (let x = 0; x < stride; x++) {
      const raw = data[pos + x];
      const a = x >= bpp ? out[row + x - bpp] : 0;
      const b = y > 0 ? out[prev + x] : 0;
      const c = y > 0 && x >= bpp ? out[prev + x - bpp] : 0;
      let v;
      switch (type) {
        case 0: v = raw; break;
        case 1: v = raw + a; break;
        case 2: v = raw + b; break;
        case 3: v = raw + ((a + b) >> 1); break;
        case 4: v = raw + paeth(a, b, c); break;
        default: throw new Error(`png: unknown filter type ${type} on row ${y}`);
      }
      out[row + x] = v & 0xff;
    }
    pos += stride;
  }
  return { pixels: out, stride, next: pos };
}

function sampleAt(buf, rowStart, index, bitDepth) {
  if (bitDepth === 8) return buf[rowStart + index];
  if (bitDepth === 16) return buf.readUInt16BE(rowStart + index * 2);
  const perByte = 8 / bitDepth;
  const byte = buf[rowStart + Math.floor(index / perByte)];
  const shift = 8 - bitDepth * ((index % perByte) + 1);
  return (byte >> shift) & ((1 << bitDepth) - 1);
}

/**
 * Decode a PNG into 8-bit RGBA.
 * @param {Buffer} buf
 * @returns {{width: number, height: number, data: Buffer}} data: width*height*4 bytes, row-major RGBA
 */
export function decodePng(buf) {
  if (!isPng(buf)) throw new Error('png: not a PNG file (bad signature)');
  let pos = 8;
  let ihdr = null;
  let palette = null;
  let trns = null;
  const idat = [];
  while (pos + 8 <= buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('latin1', pos + 4, pos + 8);
    const body = buf.subarray(pos + 8, pos + 8 + len);
    if (body.length !== len) throw new Error(`png: chunk ${type} is truncated`);
    if (type === 'IHDR') {
      ihdr = {
        width: body.readUInt32BE(0),
        height: body.readUInt32BE(4),
        bitDepth: body[8],
        colorType: body[9],
        compression: body[10],
        filter: body[11],
        interlace: body[12],
      };
    } else if (type === 'PLTE') palette = body;
    else if (type === 'tRNS') trns = body;
    else if (type === 'IDAT') idat.push(body);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  if (!ihdr) throw new Error('png: missing IHDR');
  const { width, height, bitDepth, colorType, interlace } = ihdr;
  const channels = CHANNELS[colorType];
  if (!channels) throw new Error(`png: unsupported colour type ${colorType}`);
  if (![1, 2, 4, 8, 16].includes(bitDepth)) throw new Error(`png: unsupported bit depth ${bitDepth}`);
  if (colorType === 3 && !palette) throw new Error('png: palette image without PLTE');
  if (width === 0 || height === 0) throw new Error('png: empty image');
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const out = Buffer.alloc(width * height * 4);
  const bpp = Math.max(1, Math.floor((channels * bitDepth) / 8));
  const maxV = (1 << bitDepth) - 1;
  const to8 = (v) => (bitDepth === 16 ? v >> 8 : bitDepth === 8 ? v : Math.round((v * 255) / maxV));
  const trnsGrey = trns && colorType === 0 && trns.length >= 2 ? trns.readUInt16BE(0) : null;
  const trnsRgb = trns && colorType === 2 && trns.length >= 6 ? [trns.readUInt16BE(0), trns.readUInt16BE(2), trns.readUInt16BE(4)] : null;

  const writePass = (pixels, stride, pw, ph, x0, y0, dx, dy) => {
    for (let y = 0; y < ph; y++) {
      const rowStart = y * stride;
      for (let x = 0; x < pw; x++) {
        const o = ((y0 + y * dy) * width + (x0 + x * dx)) * 4;
        let r;
        let g;
        let b;
        let a = 255;
        if (colorType === 0) {
          const v = sampleAt(pixels, rowStart, x, bitDepth);
          r = g = b = to8(v);
          if (trnsGrey !== null && v === trnsGrey) a = 0;
        } else if (colorType === 2) {
          const vr = sampleAt(pixels, rowStart, x * 3, bitDepth);
          const vg = sampleAt(pixels, rowStart, x * 3 + 1, bitDepth);
          const vb = sampleAt(pixels, rowStart, x * 3 + 2, bitDepth);
          r = to8(vr); g = to8(vg); b = to8(vb);
          if (trnsRgb && vr === trnsRgb[0] && vg === trnsRgb[1] && vb === trnsRgb[2]) a = 0;
        } else if (colorType === 3) {
          const idx = sampleAt(pixels, rowStart, x, bitDepth);
          if (idx * 3 + 2 >= palette.length) throw new Error(`png: palette index ${idx} out of range`);
          r = palette[idx * 3]; g = palette[idx * 3 + 1]; b = palette[idx * 3 + 2];
          if (trns && idx < trns.length) a = trns[idx];
        } else if (colorType === 4) {
          r = g = b = to8(sampleAt(pixels, rowStart, x * 2, bitDepth));
          a = to8(sampleAt(pixels, rowStart, x * 2 + 1, bitDepth));
        } else {
          r = to8(sampleAt(pixels, rowStart, x * 4, bitDepth));
          g = to8(sampleAt(pixels, rowStart, x * 4 + 1, bitDepth));
          b = to8(sampleAt(pixels, rowStart, x * 4 + 2, bitDepth));
          a = to8(sampleAt(pixels, rowStart, x * 4 + 3, bitDepth));
        }
        out[o] = r; out[o + 1] = g; out[o + 2] = b; out[o + 3] = a;
      }
    }
  };

  if (interlace === 0) {
    const { pixels, stride } = unfilter(raw, 0, width, height, bpp, bitDepth, channels);
    writePass(pixels, stride, width, height, 0, 0, 1, 1);
  } else if (interlace === 1) {
    let offset = 0;
    for (const [x0, y0, dx, dy] of ADAM7) {
      const pw = Math.ceil((width - x0) / dx);
      const ph = Math.ceil((height - y0) / dy);
      if (pw <= 0 || ph <= 0) continue;
      const { pixels, stride, next } = unfilter(raw, offset, pw, ph, bpp, bitDepth, channels);
      writePass(pixels, stride, pw, ph, x0, y0, dx, dy);
      offset = next;
    }
  } else {
    throw new Error(`png: unknown interlace method ${interlace}`);
  }
  return { width, height, data: out };
}

function chunk(type, body) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(body.length);
  const tb = Buffer.from(type, 'latin1');
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([tb, body])));
  return Buffer.concat([len, tb, body, crc]);
}

/**
 * Encode 8-bit RGBA pixels as a PNG (colour type 6, no interlace, Sub filter on every row).
 * @param {{width: number, height: number, data: Buffer|Uint8Array}} img
 * @returns {Buffer}
 */
export function encodePng(img) {
  const { width, height } = img;
  const data = Buffer.from(img.data.buffer, img.data.byteOffset, img.data.byteLength);
  if (data.length !== width * height * 4) throw new Error('png: data length must be width*height*4');
  const stride = width * 4;
  const raw = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y++) {
    const o = y * (stride + 1);
    raw[o] = 1;
    for (let x = 0; x < stride; x++) {
      const cur = data[y * stride + x];
      const left = x >= 4 ? data[y * stride + x - 4] : 0;
      raw[o + 1 + x] = (cur - left) & 0xff;
    }
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  return Buffer.concat([
    SIGNATURE,
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 6 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}
