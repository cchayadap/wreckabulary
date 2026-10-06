import * as THREE from "three";

const SURFACES = {
  wood: { size: 256, roughness: 0.76, normal: 0.18 },
  planks: { size: 512, roughness: 0.79, normal: 0.23 },
  cloth: { size: 256, roughness: 0.94, normal: 0.2 },
  leather: { size: 256, roughness: 0.84, normal: 0.14 },
  ceramic: { size: 256, roughness: 0.31, normal: 0.08 },
  rubber: { size: 256, roughness: 0.93, normal: 0.13 },
  plaster: { size: 256, roughness: 0.9, normal: 0.13 },
  lawn: { size: 512, roughness: 0.98, normal: 0.28 },
  painted: { size: 256, roughness: 0.64, normal: 0.08 },
  vinyl: { size: 256, roughness: 0.62, normal: 0.08 },
  rug: { size: 512, roughness: 0.96, normal: 0.2 },
};

const ALIASES = {
  woodgrain: "wood",
  walnut: "wood",
  oak: "wood",
  furniture: "wood",
  floor: "planks",
  plank: "planks",
  floorboards: "planks",
  "wood-planks": "planks",
  woodplank: "planks",
  fabric: "cloth",
  canvas: "cloth",
  grass: "lawn",
  glaze: "ceramic",
  paint: "painted",
  ivory_vinyl: "vinyl",
};

const clamp = (v, min, max) => Math.max(min, Math.min(max, v));
const smoothstep = (a, b, v) => {
  const t = clamp((v - a) / (b - a), 0, 1);
  return t * t * (3 - 2 * t);
};

function hash(text) {
  let h = 2166136261;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

function seededRandom(seed) {
  let state = seed >>> 0;
  return () => {
    state += 0x6d2b79f5;
    let value = Math.imul(state ^ (state >>> 15), state | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };
}

function noiseGrid(resolution, random) {
  const grid = Float32Array.from({ length: resolution * resolution }, () =>
    random(),
  );
  return (u, v) => {
    const x = (u - Math.floor(u)) * resolution;
    const y = (v - Math.floor(v)) * resolution;
    const ix = Math.floor(x);
    const iy = Math.floor(y);
    const sx = smoothstep(0, 1, x - ix);
    const sy = smoothstep(0, 1, y - iy);
    const sample = (a, b) =>
      grid[(b % resolution) * resolution + (a % resolution)];
    const top = sample(ix, iy) * (1 - sx) + sample(ix + 1, iy) * sx;
    const bottom = sample(ix, iy + 1) * (1 - sx) + sample(ix + 1, iy + 1) * sx;
    return top * (1 - sy) + bottom * sy;
  };
}

function canvas(size) {
  const image = document.createElement("canvas");
  image.width = image.height = size;
  const context = image.getContext("2d");
  if (!context) throw new Error("Canvas 2D is required for surface materials.");
  return { image, context, pixels: context.createImageData(size, size) };
}

function pattern(kind, aspect = 1) {
  const size = SURFACES[kind].size;
  const random = seededRandom(hash(`wreckabulary:surface:v1:${kind}`));
  const broad = noiseGrid(8, random);
  const grain = noiseGrid(32, random);
  const fine = noiseGrid(128, random);
  const color = canvas(size);
  const normal = canvas(size);
  const roughness = canvas(size);
  const heights = new Float32Array(size * size);
  const twoPi = Math.PI * 2;

  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const u = x / size;
      const v = y / size;
      const large = broad(u, v) - 0.5;
      const middle = grain(u, v) - 0.5;
      const small = fine(u, v) - 0.5;
      let shade = 0.985;
      let height = 0.5;
      let rough = 0.96;

      if (kind === "wood" || kind === "planks") {
        const drift = Math.sin(u * twoPi * 2) * 0.13;
        const streak = Math.sin((v * 46 + drift + broad(u, v) * 0.55) * twoPi);
        const pores = Math.pow(Math.max(0, streak), 6);
        shade = 0.96 + large * 0.042 + streak * 0.014 - pores * 0.025;
        height = 0.5 + streak * 0.028 + small * 0.015;
        rough = 0.96 + middle * 0.045;
        if (kind === "planks") {
          const row = Math.floor(v * 4);
          const across = (v * 4) % 1;
          const along = (u * 2 + (row % 2) * 0.5) % 1;
          const seam =
            1 -
            Math.min(
              smoothstep(0.01, 0.035, Math.min(across, 1 - across)),
              smoothstep(0.008, 0.023, Math.min(along, 1 - along)),
            );
          shade -= seam * 0.075;
          height -= seam * 0.09;
          shade += (row % 2 ? 1 : -1) * 0.014;
        }
      } else if (kind === "cloth" || kind === "rug") {
        const warp = Math.cos(u * twoPi * 64);
        const weft = Math.cos(v * twoPi * 64);
        const woven = warp * weft;
        shade = 0.973 + woven * 0.012 + middle * 0.012;
        height = 0.5 + woven * 0.052 + small * 0.016;
        rough = 0.972 + small * 0.023;
        if (kind === "rug") {
          const edge = Math.min(u * aspect, (1 - u) * aspect, v, 1 - v);
          const band = 1 - smoothstep(0.039, 0.058, edge);
          const piping = Math.exp(-Math.pow((edge - 0.055) / 0.005, 2));
          const stitchPhase = edge < Math.min(v, 1 - v) ? v : u * aspect;
          const stitch =
            Math.exp(-Math.pow((edge - 0.025) / 0.0035, 2)) *
            Math.pow(Math.max(0, Math.cos(stitchPhase * twoPi * 50)), 3);
          shade -= band * 0.065;
          shade += stitch * 0.045 - piping * 0.024;
          height += piping * 0.05 + stitch * 0.035;
          shade -= Math.pow(Math.max(0, Math.cos(v * twoPi * 6)), 12) * 0.008;
        }
      } else if (kind === "leather") {
        shade = 0.975 + middle * 0.015 + small * 0.009;
        height = 0.5 + middle * 0.062 + small * 0.042;
        rough = 0.948 + middle * 0.058;
      } else if (kind === "ceramic" || kind === "vinyl" || kind === "painted") {
        shade = 0.991 + large * 0.005;
        height = 0.5 + small * (kind === "ceramic" ? 0.012 : 0.019);
        rough = 0.962 + middle * 0.045 + small * 0.018;
      } else if (kind === "rubber") {
        shade = 0.984 + small * 0.016;
        height = 0.5 + small * 0.031 + middle * 0.016;
        rough = 0.975 + small * 0.028;
      } else if (kind === "lawn") {
        const blades =
          Math.sin((u * 74 + v * 13 + middle * 0.7) * twoPi) *
          Math.cos(v * twoPi * 57);
        shade = 0.92 + large * 0.075 + middle * 0.035 + blades * 0.015;
        height = 0.5 + blades * 0.05 + small * 0.027;
        rough = 0.979 + small * 0.02;
      } else {
        shade = 0.984 + large * 0.016 + small * 0.006;
        height = 0.5 + large * 0.033 + small * 0.029;
        rough = 0.968 + middle * 0.033;
      }

      const index = y * size + x;
      const offset = index * 4;
      heights[index] = height;
      const tone = Math.round(clamp(shade, 0, 1) * 255);
      color.pixels.data.set([tone, tone, tone, 255], offset);
      const roughTone = Math.round(clamp(rough, 0, 1) * 255);
      roughness.pixels.data.set([roughTone, roughTone, roughTone, 255], offset);
    }
  }

  const at = (x, y) => {
    if (kind === "rug")
      return heights[clamp(y, 0, size - 1) * size + clamp(x, 0, size - 1)];
    return heights[((y + size) % size) * size + ((x + size) % size)];
  };
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const dx = (at(x - 1, y) - at(x + 1, y)) * 5;
      const dy = (at(x, y - 1) - at(x, y + 1)) * 5;
      const length = Math.hypot(dx, dy, 1);
      normal.pixels.data.set(
        [
          Math.round((dx / length + 1) * 127.5),
          Math.round((dy / length + 1) * 127.5),
          Math.round((1 / length + 1) * 127.5),
          255,
        ],
        (y * size + x) * 4,
      );
    }
  }
  for (const layer of [color, normal, roughness])
    layer.context.putImageData(layer.pixels, 0, 0);
  return {
    color: color.image,
    normal: normal.image,
    roughness: roughness.image,
  };
}

function canonicalKind(kind) {
  const name = String(kind ?? "plaster").toLowerCase();
  return ALIASES[name] ?? (SURFACES[name] ? name : "painted");
}

function repetition(value) {
  return Number.isFinite(Number(value))
    ? Math.round(clamp(Number(value), 0.01, 256) * 10000) / 10000
    : 1;
}

function classifiedKind(source) {
  const name = String(source.name ?? "").toLowerCase();
  if (
    /eyes?|iris|pupil|face|mouth|teeth|glow|emissi|glass|water|soap|leaf/.test(
      name,
    ) ||
    /metal|gold|steel|chrome|silver|brass|bronze|hardware/.test(name) ||
    (source.metalness ?? 0) >= 0.25 ||
    source.transparent ||
    (source.emissive && source.emissive.getHex() !== 0)
  )
    return null;
  if (/wood|walnut|oak|timber/.test(name)) return "wood";
  if (/leather|strap/.test(name)) return "leather";
  if (/fabric|cloth|canvas|rib|woven|rug|carpet/.test(name)) return "cloth";
  if (
    /cream(?:_|$)/.test(name) &&
    /fabric|cloth|woven/.test(source.map?.name ?? "")
  )
    return "cloth";
  if (/ceramic|porcelain|glaze/.test(name)) return "ceramic";
  if (/rubber/.test(name)) return "rubber";
  if (/vinyl/.test(name)) return "vinyl";
  if (/plaster|wall(?:_|$)/.test(name)) return "plaster";
  if (/painted|enamel/.test(name)) return "painted";
  return null;
}

export class MaterialPalette {
  constructor(renderer) {
    this.anisotropy = Math.max(
      1,
      Math.min(8, renderer?.capabilities?.getMaxAnisotropy?.() ?? 1),
    );
    this.materials = new Set();
    this.generatedTextures = new Set();
    this.patterns = new Map();
    this.textureCache = new Map();
    this.surfaceCache = new Map();
    this.enrichedCache = new Map();
    this.disposed = false;
  }

  assertLive() {
    if (this.disposed)
      throw new Error("This MaterialPalette has been disposed.");
  }

  textures(kind, repeatX = 1, repeatY = 1, aspect = 1) {
    this.assertLive();
    const patternKey = `${kind}:${aspect}`;
    const key = `${patternKey}:${repeatX}:${repeatY}`;
    if (this.textureCache.has(key)) return this.textureCache.get(key);
    if (!this.patterns.has(patternKey))
      this.patterns.set(patternKey, pattern(kind, aspect));
    const images = this.patterns.get(patternKey);
    const textures = {};
    for (const [channel, image] of Object.entries(images)) {
      const texture = new THREE.CanvasTexture(image);
      texture.name = `Wreckabulary original ${kind} ${channel}`;
      texture.colorSpace =
        channel === "color" ? THREE.SRGBColorSpace : THREE.NoColorSpace;
      texture.wrapS = texture.wrapT =
        kind === "rug" ? THREE.ClampToEdgeWrapping : THREE.RepeatWrapping;
      texture.repeat.set(repeatX, repeatY);
      texture.anisotropy = this.anisotropy;
      textures[channel] = texture;
      this.generatedTextures.add(texture);
    }
    this.textureCache.set(key, textures);
    return textures;
  }

  own(material, kind) {
    material.userData.paletteOwned = true;
    material.userData.surfaceKind = kind ?? "authored";
    this.materials.add(material);
    return material;
  }

  surface(kind, color = 0xffffff, { repeatX = 1, repeatY = 1 } = {}) {
    this.assertLive();
    kind = canonicalKind(kind);
    if (kind === "rug") return this.rug(color, 1);
    repeatX = repetition(repeatX);
    repeatY = repetition(repeatY);
    const tint = new THREE.Color(color);
    const key = `${kind}:${tint.getHexString()}:${repeatX}:${repeatY}`;
    if (this.surfaceCache.has(key)) return this.surfaceCache.get(key);
    const maps = this.textures(kind, repeatX, repeatY);
    const material = this.own(
      new THREE.MeshStandardMaterial({
        name: `Wreckabulary ${kind}`,
        color: tint,
        map: maps.color,
        normalMap: maps.normal,
        normalScale: new THREE.Vector2(
          SURFACES[kind].normal,
          SURFACES[kind].normal,
        ),
        roughnessMap: maps.roughness,
        roughness: SURFACES[kind].roughness,
        metalness: 0,
      }),
      kind,
    );
    this.surfaceCache.set(key, material);
    return material;
  }

  rug(color = 0xffffff, aspect = 1) {
    this.assertLive();
    aspect = Number.isFinite(Number(aspect))
      ? Math.round(clamp(Number(aspect), 0.25, 8) * 100) / 100
      : 1;
    const tint = new THREE.Color(color);
    const key = `rug:${tint.getHexString()}:${aspect}`;
    if (this.surfaceCache.has(key)) return this.surfaceCache.get(key);
    const maps = this.textures("rug", 1, 1, aspect);
    const material = this.own(
      new THREE.MeshStandardMaterial({
        name: "Wreckabulary sewn woven rug",
        color: tint,
        map: maps.color,
        normalMap: maps.normal,
        normalScale: new THREE.Vector2(
          SURFACES.rug.normal,
          SURFACES.rug.normal,
        ),
        roughnessMap: maps.roughness,
        roughness: SURFACES.rug.roughness,
        metalness: 0,
      }),
      "rug",
    );
    this.surfaceCache.set(key, material);
    return material;
  }

  enrichMaterial(source, key = "") {
    this.assertLive();
    if (!source?.isMaterial) return source;
    const cacheKey = `${source.uuid}:${String(key)}`;
    if (this.enrichedCache.has(cacheKey))
      return this.enrichedCache.get(cacheKey);
    const clone = source.clone();
    const kind = classifiedKind(source);
    if (
      kind &&
      (source.isMeshStandardMaterial || source.isMeshPhysicalMaterial)
    ) {
      const maps = this.textures(kind);
      if (!source.normalMap && !source.bumpMap) {
        clone.normalMap = maps.normal;
        clone.normalScale.set(SURFACES[kind].normal, SURFACES[kind].normal);
      }
      if (!source.roughnessMap) clone.roughnessMap = maps.roughness;
    }
    this.own(clone, kind);
    this.enrichedCache.set(cacheKey, clone);
    return clone;
  }

  info() {
    return {
      materials: this.materials.size,
      generatedTextures: this.generatedTextures.size,
      textureVariants: this.textureCache.size,
      patternCanvases: this.patterns.size * 3,
      surfaceMaterials: this.surfaceCache.size,
      enrichedMaterials: this.enrichedCache.size,
      disposed: this.disposed,
    };
  }

  dispose() {
    if (this.disposed) return;
    this.disposed = true;
    for (const material of this.materials) material.dispose();
    for (const texture of this.generatedTextures) texture.dispose();
    this.materials.clear();
    this.generatedTextures.clear();
    this.patterns.clear();
    this.textureCache.clear();
    this.surfaceCache.clear();
    this.enrichedCache.clear();
  }
}
