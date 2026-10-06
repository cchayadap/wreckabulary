#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import crypto from 'node:crypto';

const TICKS = 46186158000;
const LEG_NAMES = ['thigh_L', 'shin_L', 'foot_L', 'thigh_R', 'shin_R', 'foot_R'];
const EPS = 1e-6;
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
function demand(ok, message) { if (!ok) throw new Error(message); }
function near(a, b, epsilon = EPS) { return Math.abs(a - b) <= epsilon; }
function finite(values) { return values.every(Number.isFinite); }
const clean = value => String(value).split('\0')[0].split('::')[0];
const canonical = value => clean(value).split('|').at(-1);
const id = value => String(value);

function parseFbx(buffer) {
  demand(buffer.subarray(0, 23).equals(Buffer.from('Kaydara FBX Binary  \0\x1a\0')), 'Not binary FBX.');
  const version = buffer.readUInt32LE(23);
  demand(version >= 7400 && version < 7800, 'Unsupported FBX version: ' + version);
  const wide = version >= 7500, headerSize = wide ? 25 : 13;
  let at = 27;
  function uint(offset) {
    const n = wide ? Number(buffer.readBigUInt64LE(offset)) : buffer.readUInt32LE(offset);
    demand(Number.isSafeInteger(n), 'FBX offset/count exceeds safe integer range.');
    return n;
  }
  function property() {
    const start = at, type = String.fromCharCode(buffer[at++]);
    let value, encoding;
    const read = (method, bytes) => { const v = buffer[method](at); at += bytes; return v; };
    switch (type) {
      case 'Y': value = read('readInt16LE', 2); break;
      case 'C': value = read('readUInt8', 1); break;
      case 'I': value = read('readInt32LE', 4); break;
      case 'F': value = read('readFloatLE', 4); break;
      case 'D': value = read('readDoubleLE', 8); break;
      case 'L': value = read('readBigInt64LE', 8); break;
      case 'S': case 'R': {
        const count = read('readUInt32LE', 4);
        demand(at + count <= buffer.length, 'Truncated FBX string/raw property.');
        value = type === 'S' ? buffer.toString('utf8', at, at + count) : buffer.subarray(at, at + count);
        at += count; break;
      }
      default: {
        const layouts = { f: ['readFloatLE', 4], d: ['readDoubleLE', 8],
          i: ['readInt32LE', 4], l: ['readBigInt64LE', 8], b: ['readUInt8', 1], c: ['readUInt8', 1] };
        demand(layouts[type], 'Unsupported FBX property type: ' + type);
        const count = read('readUInt32LE', 4); encoding = read('readUInt32LE', 4);
        const bytes = read('readUInt32LE', 4);
        demand((encoding === 0 || encoding === 1) && at + bytes <= buffer.length, 'Unsupported/truncated FBX array.');
        const payload = buffer.subarray(at, at + bytes); at += bytes;
        const decoded = encoding ? zlib.inflateSync(payload) : payload;
        const [method, stride] = layouts[type];
        demand(decoded.length === count * stride, 'FBX array decoded length mismatch.');
        value = Array.from({ length: count }, (_, k) => decoded[method](k * stride));
      }
    }
    return { type, value, encoding, raw: buffer.subarray(start, at), replacement: null };
  }
  function node() {
    const start = at;
    demand(at + headerSize <= buffer.length, 'Truncated FBX node header.');
    const end = uint(at); at += wide ? 8 : 4;
    const count = uint(at); at += wide ? 8 : 4;
    const propertyBytes = uint(at); at += wide ? 8 : 4;
    const nameBytes = buffer[at++];
    if (end === 0) {
      demand(buffer.subarray(start, at).every(v => v === 0), 'Malformed FBX null header.');
      return null;
    }
    demand(end > at && end <= buffer.length, 'Invalid FBX node end offset.');
    const nameRaw = buffer.subarray(at, at + nameBytes), name = nameRaw.toString('utf8'); at += nameBytes;
    const propertyStart = at, properties = Array.from({ length: count }, property);
    demand(at - propertyStart === propertyBytes, 'FBX property section byte mismatch.');
    const children = [];
    let suffix = Buffer.alloc(0);
    while (at < end) {
      const childStart = at, child = node();
      if (!child) {
        suffix = buffer.subarray(childStart, end);
        demand(suffix.length === headerSize && suffix.every(v => v === 0), 'Unsupported FBX node terminator.');
        at = end; break;
      }
      children.push(child);
    }
    demand(at === end, 'FBX child end mismatch.');
    return { name, nameRaw, properties, children, suffix };
  }
  const nodes = [];
  let footerStart;
  while (at < buffer.length) {
    const start = at, item = node();
    if (!item) { footerStart = start; break; }
    nodes.push(item);
  }
  demand(footerStart !== undefined, 'Missing FBX root terminator.');
  const result = { version, wide, headerSize, header: buffer.subarray(0, 27), nodes,
    footerOffset: footerStart, footer: buffer.subarray(footerStart) };
  footerParts(result);
  return result;
}
function footerParts(fbx) {
  const prefixBytes = fbx.headerSize + 20, tailBytes = 140;
  const padding = fbx.footer.length - prefixBytes - tailBytes;
  demand(padding >= 1 && padding <= 16, 'Unsupported FBX producer trailer layout.');
  demand(fbx.footer.subarray(0, fbx.headerSize).every(v => v === 0), 'Invalid root terminator.');
  demand(fbx.footer.subarray(fbx.headerSize + 16, prefixBytes).every(v => v === 0), 'Unsupported footer separator.');
  demand(fbx.footer.subarray(prefixBytes, prefixBytes + padding).every(v => v === 0), 'Nonzero footer alignment padding.');
  const tail = fbx.footer.subarray(prefixBytes + padding);
  demand(tail.readUInt32LE(0) === fbx.version && (fbx.footerOffset + prefixBytes + padding) % 16 === 0, 'Invalid/misaligned footer version.');
  demand(tail.subarray(4, 124).every(v => v === 0), 'Unsupported footer payload.');
  demand(tail.subarray(124).equals(Buffer.from('f85a8c6adef5d97eece90ce3758f290b', 'hex')), 'Unsupported footer magic.');
  return { prefix: fbx.footer.subarray(0, prefixBytes), tail, padding };
}
const values = node => node.properties.map(p => p.value);
function oneChild(node, name) {
  const found = node.children.filter(n => n.name === name);
  demand(found.length === 1, 'Expected unique ' + name + ' below ' + node.name);
  return found[0];
}
function leafValue(node, name) {
  const leaf = oneChild(node, name);
  demand(leaf.properties.length === 1, 'Expected one property in ' + name);
  return leaf.properties[0].value;
}
const propertyCache = new WeakMap();
function props70(node) {
  if (propertyCache.has(node)) return propertyCache.get(node);
  const roots = node.children.filter(n => n.name === 'Properties70');
  demand(roots.length <= 1, 'Duplicate Properties70.');
  const result = new Map((roots[0]?.children || []).map(n => {
    const p = values(n); return [p[0], p.slice(4)];
  }));
  propertyCache.set(node, result); return result;
}
function vector(props, key, fallback) {
  const v = props.get(key) || fallback;
  demand(v && v.length === 3 && finite(v), 'Invalid transform vector: ' + key);
  return v.slice();
}
function describe(fbx) {
  const objects = fbx.nodes.filter(n => n.name === 'Objects');
  const connections = fbx.nodes.filter(n => n.name === 'Connections');
  demand(objects.length === 1 && connections.length === 1, 'Unique Objects/Connections required.');
  const byId = new Map(objects[0].children.map(n => [id(n.properties[0]?.value), n]));
  demand(byId.size === objects[0].children.length, 'Duplicate FBX object IDs.');
  const links = connections[0].children.filter(n => n.name === 'C').map(values);
  const byChild = new Map(), byParent = new Map();
  for (const link of links) {
    const child = id(link[1]), parent = id(link[2]);
    if (!byChild.has(child)) byChild.set(child, []); byChild.get(child).push(link);
    if (!byParent.has(parent)) byParent.set(parent, []); byParent.get(parent).push(link);
  }
  const children = key => byParent.get(id(key)) || [];
  const parents = key => byChild.get(id(key)) || [];
  const objectId = node => id(node.properties[0].value);
  const models = objects[0].children.filter(n => n.name === 'Model');
  const byName = new Map();
  for (const n of models) {
    const name = clean(n.properties[1].value);
    demand(!byName.has(name), 'Duplicate model name: ' + name); byName.set(name, n);
  }
  const stacks = objects[0].children.filter(n => n.name === 'AnimationStack');
  const target = stacks.filter(n => canonical(n.properties[1].value) === 'Pickup');
  demand(target.length === 1, 'Expected exactly one Pickup animation stack.');
  const layers = children(objectId(target[0])).map(c => byId.get(id(c[1]))).filter(n => n?.name === 'AnimationLayer');
  demand(layers.length === 1, 'Pickup must have exactly one animation layer.');
  const layerId = objectId(layers[0]);
  const layerStacks = parents(layerId).filter(c => byId.get(id(c[2]))?.name === 'AnimationStack');
  demand(layerStacks.length === 1 && id(layerStacks[0][2]) === objectId(target[0]), 'Pickup animation layer is shared with another clip.');
  const curveNodes = children(layerId).map(c => byId.get(id(c[1]))).filter(n => n?.name === 'AnimationCurveNode');
  const tracks = new Map();
  for (const cn of curveNodes) {
    const targets = parents(objectId(cn)).filter(c => c[0] === 'OP' && byId.get(id(c[2]))?.name === 'Model');
    if (targets.length === 0) {
      const morphTargets = parents(objectId(cn)).filter(c => c[0] === 'OP' && byId.get(id(c[2]))?.name === 'Deformer'
        && byId.get(id(c[2])).properties[2]?.value === 'BlendShapeChannel');
      const geometryMorphs = parents(objectId(cn)).filter(c => c[0] === 'OP'
        && byId.get(id(c[2]))?.name === 'Geometry' && byId.get(id(c[2])).properties[2]?.value === 'Mesh'
        && clean(byId.get(id(c[2])).properties[1].value) !== 'SK_Boots' && ['Blink', 'BrowRelax'].includes(c[3]));
      demand(morphTargets.length + geometryMorphs.length === 1, 'Unsupported non-transform Pickup curve node: ' + clean(cn.properties[1].value));
      continue;
    }
    demand(targets.length === 1, 'Curve node must target exactly one model.');
    const targetModel = byId.get(id(targets[0][2])), propertyName = targets[0][3], modelName = clean(targetModel.properties[1].value);
    demand(['Lcl Translation', 'Lcl Rotation', 'Lcl Scaling'].includes(propertyName), 'Unsupported Pickup transform channel: ' + propertyName);
    const axes = new Map();
    for (const c of children(objectId(cn))) {
      const curve = byId.get(id(c[1]));
      if (curve?.name !== 'AnimationCurve') continue;
      demand(c[0] === 'OP' && /^d\|[XYZ]$/.test(c[3]), 'Unsupported curve axis connection.');
      const axis = c[3].slice(-1);
      demand(!axes.has(axis), 'Duplicate curve axis.');
      const ticks = leafValue(curve, 'KeyTime').map(Number), keys = leafValue(curve, 'KeyValueFloat');
      demand(ticks.length === keys.length && ticks.length >= 2 && finite(keys), 'Invalid animation key array.');
      demand(ticks.every((t, k) => Number.isSafeInteger(t) && (k === 0 || t > ticks[k - 1])), 'Key times must be strictly increasing.');
      const flags = leafValue(curve, 'KeyAttrFlags'), counts = leafValue(curve, 'KeyAttrRefCount');
      demand(flags.length === counts.length && counts.reduce((a, b) => a + b, 0) === ticks.length, 'Unsupported key attribute grouping.');
      demand(flags.every(f => (f & 0x0e) === 4), 'Only LINEAR baked FBX curves are supported.');
      axes.set(axis, { node: curve, ticks, times: ticks.map(t => t / TICKS), keys });
    }
    demand(axes.size === 3, 'XYZ curves required for ' + modelName + ' ' + propertyName);
    const key = modelName + '/' + propertyName;
    demand(!tracks.has(key), 'Duplicate model/property track: ' + key);
    tracks.set(key, axes);
  }
  return { objects: objects[0], byId, byName, links, children, parents, objectId, tracks, stacks, stack: target[0], layerId };
}
function mm(a, b) {
  const out = Array(16).fill(0);
  for (let r = 0; r < 4; r++) for (let c = 0; c < 4; c++) for (let k = 0; k < 4; k++) out[c * 4 + r] += a[k * 4 + r] * b[c * 4 + k];
  return out;
}
function inverse(m) {
  const rows = Array.from({ length: 4 }, (_, r) => [...Array.from({ length: 4 }, (_, c) => m[c * 4 + r]), ...Array.from({ length: 4 }, (_, c) => +(r === c))]);
  for (let c = 0; c < 4; c++) {
    let pivot = c;
    for (let r = c + 1; r < 4; r++) if (Math.abs(rows[r][c]) > Math.abs(rows[pivot][c])) pivot = r;
    demand(Math.abs(rows[pivot][c]) > 1e-12, 'Singular bind matrix.');
    [rows[c], rows[pivot]] = [rows[pivot], rows[c]];
    const divisor = rows[c][c]; rows[c] = rows[c].map(v => v / divisor);
    for (let r = 0; r < 4; r++) if (r !== c) {
      const factor = rows[r][c]; rows[r] = rows[r].map((v, k) => v - factor * rows[c][k]);
    }
  }
  return Array.from({ length: 16 }, (_, k) => rows[k % 4][4 + Math.floor(k / 4)]);
}
function transform(matrix, vertex) {
  return [0, 1, 2].map(r => matrix[r] * vertex[0] + matrix[4 + r] * vertex[1] + matrix[8 + r] * vertex[2] + matrix[12 + r]);
}
function trs(t, deg, s) {
  const [x, y, z] = deg.map(v => v * Math.PI / 180);
  const cx = Math.cos(x), sx = Math.sin(x), cy = Math.cos(y), sy = Math.sin(y), cz = Math.cos(z), sz = Math.sin(z);
  const rx = [1, 0, 0, 0, 0, cx, sx, 0, 0, -sx, cx, 0, 0, 0, 0, 1];
  const ry = [cy, 0, -sy, 0, 0, 1, 0, 0, sy, 0, cy, 0, 0, 0, 0, 1];
  const rz = [cz, sz, 0, 0, -sz, cz, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
  const m = mm(mm(rz, ry), rx);
  for (let c = 0; c < 3; c++) for (let r = 0; r < 3; r++) m[c * 4 + r] *= s[c];
  [m[12], m[13], m[14]] = t;
  return m;
}
function sample(curve, time) {
  const ts = curve.times, vs = curve.keys;
  if (time <= ts[0]) return vs[0];
  if (time >= ts.at(-1)) return vs.at(-1);
  let lo = 0, hi = ts.length - 1;
  while (hi - lo > 1) { const k = (lo + hi) >> 1; if (ts[k] <= time) lo = k; else hi = k; }
  const t = (time - ts[lo]) / (ts[hi] - ts[lo]); return vs[lo] * (1 - t) + vs[hi] * t;
}
function chainModels(d, name) {
  const chain = []; let node = d.byName.get(name);
  demand(node, 'Missing model ' + name);
  while (node) {
    demand(!chain.includes(node), 'Cyclic model hierarchy.'); chain.push(node);
    const p = d.parents(d.objectId(node)).map(c => d.byId.get(id(c[2]))).filter(n => n?.name === 'Model');
    demand(p.length <= 1, 'Model has multiple parents.'); node = p[0];
  }
  return chain.reverse();
}
function validateTransforms(d) {
  const required = new Set(['pelvis', 'SK_Boots', ...LEG_NAMES]);
  for (const foot of ['foot_L', 'foot_R']) for (const model of chainModels(d, foot)) required.add(clean(model.properties[1].value));
  for (const name of required) {
    const model = d.byName.get(name), p = props70(model);
    demand((p.get('RotationOrder')?.[0] || 0) === 0, 'Only XYZ rotation order is supported.');
    demand((p.get('InheritType')?.[0] ?? 1) === 1, 'Unsupported transform inheritance.');
    for (const key of ['PreRotation', 'PostRotation', 'RotationPivot', 'RotationOffset', 'ScalingPivot', 'ScalingOffset', 'GeometricTranslation', 'GeometricRotation'])
      demand(vector(p, key, [0, 0, 0]).every(v => Math.abs(v) < 1e-6), 'Unsupported nonzero ' + key + ' on ' + name);
    demand(vector(p, 'GeometricScaling', [1, 1, 1]).every(v => near(v, 1)), 'Unsupported geometric scale.');
    demand(vector(p, 'Lcl Scaling', [1, 1, 1]).every(v => near(v, 1)), 'Non-unit static bone scale.');
    for (const property of ['Lcl Translation', 'Lcl Rotation', 'Lcl Scaling']) {
      const axes = d.tracks.get(name + '/' + property);
      if (!axes) continue;
      for (const axis of ['X', 'Y', 'Z']) {
        const curve = axes.get(axis);
        const allowed = property === 'Lcl Rotation' && LEG_NAMES.includes(name) && axis === 'X'
          || property === 'Lcl Translation' && name === 'pelvis' && axis === 'Y';
        if (!allowed) demand(curve.keys.every(v => near(v, curve.keys[0], property === 'Lcl Rotation' ? 1e-4 : 1e-6)), 'Unsupported changing foot-chain channel: ' + name + ' ' + property + ' ' + axis);
        if (property === 'Lcl Scaling') demand(curve.keys.every(v => near(v, 1)), 'Non-unit animated bone scale.');
      }
    }
  }
  const globals = fbxGlobals(d);
  demand(globals.get('UpAxis')?.[0] === 1 && globals.get('UpAxisSign')?.[0] === 1, 'Only positive Y-up FBX is supported.');
  demand(near(globals.get('UnitScaleFactor')?.[0], 100), 'Only metre-sized FBX source units are supported.');
}
function fbxGlobals(d) {
  return d.globalProperties;
}
function world(d, name, time = null, cache = new Map()) {
  if (cache.has(name)) return cache.get(name);
  const node = d.byName.get(name); demand(node, 'Missing model: ' + name);
  const p = props70(node);
  const base = [vector(p, 'Lcl Translation', [0, 0, 0]), vector(p, 'Lcl Rotation', [0, 0, 0]), vector(p, 'Lcl Scaling', [1, 1, 1])];
  if (time !== null) ['Lcl Translation', 'Lcl Rotation', 'Lcl Scaling'].forEach((property, q) => {
    const axes = d.tracks.get(name + '/' + property);
    if (axes) base[q] = ['X', 'Y', 'Z'].map(axis => sample(axes.get(axis), time));
  });
  let m = trs(...base);
  const parent = d.parents(d.objectId(node)).map(c => d.byId.get(id(c[2]))).filter(n => n?.name === 'Model');
  demand(parent.length <= 1, 'Multiple model parents.');
  if (parent[0]) m = mm(world(d, clean(parent[0].properties[1].value), time, cache), m);
  cache.set(name, m); return m;
}
function footGeometry(d) {
  const model = d.byName.get('SK_Boots'); demand(model, 'Missing SK_Boots model.');
  const geos = d.children(d.objectId(model)).map(c => d.byId.get(id(c[1]))).filter(n => n?.name === 'Geometry');
  demand(geos.length === 1 && geos[0].properties[2].value === 'Mesh', 'Expected one SK_Boots mesh geometry.');
  const geometry = geos[0], vertices = leafValue(geometry, 'Vertices');
  demand(vertices.length % 3 === 0 && finite(vertices), 'Invalid boot geometry.');
  const skins = d.children(d.objectId(geometry)).map(c => d.byId.get(id(c[1]))).filter(n => n?.name === 'Deformer' && n.properties[2].value === 'Skin');
  demand(skins.length === 1, 'Expected one boot skin.');
  const clusters = d.children(d.objectId(skins[0])).map(c => d.byId.get(id(c[1]))).filter(n => n?.name === 'Deformer' && n.properties[2].value === 'Cluster');
  const coverage = Array(vertices.length / 3).fill(0), feet = [];
  const meshRest = world(d, 'SK_Boots');
  for (const cluster of clusters) {
    const indexNodes = cluster.children.filter(n => n.name === 'Indexes'), weightNodes = cluster.children.filter(n => n.name === 'Weights');
    demand(indexNodes.length <= 1 && weightNodes.length <= 1, 'Duplicate cluster weight arrays.');
    if (!indexNodes.length && !weightNodes.length) continue;
    demand(indexNodes.length === 1 && weightNodes.length === 1, 'Incomplete cluster weight arrays.');
    const indices = leafValue(cluster, 'Indexes'), weights = leafValue(cluster, 'Weights');
    demand(indices.length === weights.length, 'Boot cluster lengths differ.');
    const active = indices.map((i, k) => [i, weights[k]]).filter(([, w]) => w > 0);
    if (!active.length) continue;
    const bones = d.children(d.objectId(cluster)).map(c => d.byId.get(id(c[1]))).filter(n => n?.name === 'Model');
    demand(bones.length === 1, 'Boot cluster requires one bone.');
    const name = clean(bones[0].properties[1].value);
    demand(['foot_L', 'foot_R'].includes(name) && !feet.some(f => f.name === name), 'Boot must be rigidly assigned once to each foot.');
    const link = leafValue(cluster, 'TransformLink'), stored = leafValue(cluster, 'Transform');
    demand(link.length === 16 && stored.length === 16 && finite(link) && finite(stored), 'Invalid boot bind matrices.');
    const staticFoot = world(d, name), startTime = d.tracks.get('pelvis/Lcl Translation')?.get('Y')?.times[0];
    demand(startTime !== undefined, 'Missing animated start for bind validation.');
    const animatedFoot = world(d, name, startTime);
    demand(link.every((v, k) => near(v, staticFoot[k], 2e-5)), 'Foot static transform does not match stored bind.');
    demand(link.every((v, k) => near(v, animatedFoot[k], 1e-5)), 'Foot start pose does not match stored bind.');
    const restored = mm(link, stored);
    demand(restored.every((v, k) => near(v, meshRest[k], 1e-5)), 'Unsupported cluster Transform convention.');
    const toBone = mm(inverse(link), meshRest), points = [];
    for (const [i, weight] of active) {
      demand(Number.isInteger(i) && i >= 0 && i < coverage.length && near(weight, 1, 1e-8), 'Boot requires unit rigid weights.');
      demand(++coverage[i] === 1, 'Boot vertex has multiple influences.');
      points.push(transform(toBone, vertices.slice(i * 3, i * 3 + 3)));
    }
    feet.push({ name, points, link });
  }
  demand(feet.length === 2 && coverage.every(v => v === 1), 'All boot control points must belong to one of two feet.');
  return feet;
}
function replaceFloatArray(property, newValues) {
  demand(property.type === 'f' && property.value.length === newValues.length && finite(newValues), 'Expected finite KeyValueFloat array.');
  const decoded = Buffer.alloc(newValues.length * 4);
  newValues.forEach((v, k) => decoded.writeFloatLE(v, k * 4));
  const values32 = newValues.map((_, k) => decoded.readFloatLE(k * 4));
  const same = values32.every((v, k) => Object.is(v, property.value[k]));
  if (!same) {
    const payload = property.encoding ? zlib.deflateSync(decoded) : decoded;
    const prefix = Buffer.alloc(13); prefix[0] = 'f'.charCodeAt(0);
    prefix.writeUInt32LE(newValues.length, 1); prefix.writeUInt32LE(property.encoding, 5); prefix.writeUInt32LE(payload.length, 9);
    property.replacement = Buffer.concat([prefix, payload]);
  }
  property.value = values32;
  return !same;
}
function solve(d) {
  const pelvisAxes = d.tracks.get('pelvis/Lcl Translation'); demand(pelvisAxes, 'Pickup pelvis translation is required.');
  const py = pelvisAxes.get('Y'), start = py.times[0], end = py.times.at(-1);
  demand(near(start, 0, 1e-9) && end > 0 && near(py.keys[0], py.keys.at(-1), 1e-8), 'Pickup must return to its original pelvis height.');
  demand(py.keys.every(y => y <= py.keys[0] + 1e-8), 'Unsupported Pickup pelvis rise above its endpoint.');
  const changedProperties = new Set(), summaries = [], replacements = [];
  for (const side of ['L', 'R']) {
    const upper = vector(props70(d.byName.get('shin_' + side)), 'Lcl Translation', null);
    const lower = vector(props70(d.byName.get('foot_' + side)), 'Lcl Translation', null);
    const u = upper.slice(1), v = lower.slice(1), lu = Math.hypot(...u), lv = Math.hypot(...v);
    const gu = Math.atan2(u[1], u[0]), gv = Math.atan2(v[1], v[0]), initialDelta = gv - gu;
    demand(initialDelta > 0 && initialDelta < Math.PI / 2, 'Unsupported rest-knee analytic branch.');
    const corrected = [[], [], []];
    for (let k = 0; k < py.keys.length; k++) {
      if (k === 0 || k === py.keys.length - 1) { corrected.forEach(a => a.push(0)); continue; }
      const drop = py.keys[0] - py.keys[k], dy = u[0] + v[0] + drop, dz = u[1] + v[1];
      const distance = Math.hypot(dy, dz);
      demand(distance > Math.abs(lu - lv) && distance < lu + lv, 'Unreachable Pickup stance at key ' + k);
      const cosine = (distance * distance - lu * lu - lv * lv) / (2 * lu * lv);
      demand(cosine >= -1 && cosine <= 1, 'Invalid two-link cosine.');
      const delta = Math.acos(cosine);
      const a = Math.atan2(dz, dy) - gu - Math.atan2(lv * Math.sin(delta), lu + lv * Math.cos(delta));
      const b = delta - initialDelta, c = -(a + b);
      [a, b, c].forEach((r, q) => corrected[q].push(r * 180 / Math.PI));
    }
    ['thigh_', 'shin_', 'foot_'].forEach((prefix, q) => {
      const name = prefix + side, axes = d.tracks.get(name + '/Lcl Rotation'); demand(axes, 'Missing rotation track: ' + name);
      const curve = axes.get('X');
      demand(curve.ticks.length === py.ticks.length && curve.ticks.every((t, k) => t === py.ticks[k]), 'Leg and pelvis key times must match.');
      demand(d.parents(d.objectId(curve.node)).length === 1, 'Target rotation curve is shared.');
      const cn = d.byId.get(id(d.parents(d.objectId(curve.node))[0][2]));
      const layerParents = d.parents(d.objectId(cn)).filter(cnLink => d.byId.get(id(cnLink[2]))?.name === 'AnimationLayer');
      demand(layerParents.length === 1 && id(layerParents[0][2]) === d.layerId, 'Target rotation curve is shared with another clip.');
      const leaf = oneChild(curve.node, 'KeyValueFloat'), prop = leaf.properties[0];
      changedProperties.add(prop);
      replacements.push({ prop, values: corrected[q], curve });
      summaries.push({ bone: name, keys: corrected[q].length, minimumDegrees: Math.min(...corrected[q]), maximumDegrees: Math.max(...corrected[q]), endpointsDegrees: [0, 0] });
    });
  }
  demand(replacements.length === 6 && changedProperties.size === 6, 'Exactly six independent Pickup X rotation curves required.');
  return { py, start, end, changedProperties, summaries, replacements };
}
function auditPose(d, feet, times, keyTimes) {
  const results = feet.map(f => ({ bone: f.name, vertices: f.points.length, minimumY: Infinity, maximumMinimumY: -Infinity, worstTime: null, maximumKeyPositionDrift: 0, maximumKeyOrientationElementDrift: 0 }));
  const start = times[0], baseline = feet.map(f => world(d, f.name, start));
  const keySet = new Set(keyTimes);
  for (const time of times) {
    const cache = new Map();
    feet.forEach((foot, q) => {
      const m = world(d, foot.name, time, cache), r = results[q];
      let lowest = Infinity;
      for (const p of foot.points) lowest = Math.min(lowest, m[1] * p[0] + m[5] * p[1] + m[9] * p[2] + m[13]);
      if (lowest < r.minimumY) { r.minimumY = lowest; r.worstTime = time; }
      r.maximumMinimumY = Math.max(r.maximumMinimumY, lowest);
      if (keySet.has(time)) {
        r.maximumKeyPositionDrift = Math.max(r.maximumKeyPositionDrift, Math.hypot(...[12, 13, 14].map(k => m[k] - baseline[q][k])));
        r.maximumKeyOrientationElementDrift = Math.max(r.maximumKeyOrientationElementDrift, ...[0, 1, 2, 4, 5, 6, 8, 9, 10].map(k => Math.abs(m[k] - baseline[q][k])));
      }
    });
  }
  return { samples: times.length, rangeSeconds: [times[0], times.at(-1)], feet: results };
}
function serialize(fbx) {
  const raw = p => p.replacement || p.raw;
  function size(n) { return fbx.headerSize + n.nameRaw.length + n.properties.reduce((s, p) => s + raw(p).length, 0) + n.children.reduce((s, c) => s + size(c), 0) + n.suffix.length; }
  function emit(n, at) {
    const properties = n.properties.map(raw), propertyBytes = properties.reduce((s, p) => s + p.length, 0);
    const header = Buffer.alloc(fbx.headerSize), end = at + size(n);
    if (fbx.wide) {
      header.writeBigUInt64LE(BigInt(end), 0); header.writeBigUInt64LE(BigInt(n.properties.length), 8); header.writeBigUInt64LE(BigInt(propertyBytes), 16);
    } else {
      demand(end <= 0xffffffff, 'FBX exceeds 32-bit offset limit.');
      header.writeUInt32LE(end, 0); header.writeUInt32LE(n.properties.length, 4); header.writeUInt32LE(propertyBytes, 8);
    }
    header[fbx.headerSize - 1] = n.nameRaw.length;
    let childAt = at + fbx.headerSize + n.nameRaw.length + propertyBytes;
    const children = n.children.map(c => { const b = emit(c, childAt); childAt += b.length; return b; });
    return Buffer.concat([header, n.nameRaw, ...properties, ...children, n.suffix]);
  }
  let at = 27;
  const nodes = fbx.nodes.map(n => { const b = emit(n, at); at += b.length; return b; });
  const footer = footerParts(fbx), unpaddedOffset = at + footer.prefix.length;
  let pad = (16 - unpaddedOffset % 16) % 16; if (pad === 0) pad = 16;
  return Buffer.concat([fbx.header, ...nodes, footer.prefix, Buffer.alloc(pad), footer.tail]);
}
function propertyInventory(fbx) {
  const result = new Map();
  function walk(nodes, parent) {
    nodes.forEach((node, index) => {
      const key = parent + '/' + node.name + '[' + index + ']';
      node.properties.forEach((p, q) => result.set(key + '/property[' + q + ']', p));
      walk(node.children, key);
    });
  }
  walk(fbx.nodes, ''); return result;
}
function correction(input) {
  const fbx = parseFbx(input), d = describe(fbx);
  const globalSettings = fbx.nodes.filter(n => n.name === 'GlobalSettings');
  demand(globalSettings.length === 1, 'GlobalSettings required.');
  d.globalProperties = props70(globalSettings[0]);
  validateTransforms(d);
  const feet = footGeometry(d), plan = solve(d);
  const times = [];
  for (let k = 0; k < plan.py.times.length - 1; k++) for (let n = 0; n < 256; n++)
    times.push(plan.py.times[k] + (plan.py.times[k + 1] - plan.py.times[k]) * n / 256);
  times.push(plan.end);
  const originalInventory = propertyInventory(fbx);
  const allowedPaths = [...originalInventory].filter(([, p]) => plan.changedProperties.has(p)).map(([key]) => key);
  demand(allowedPaths.length === 6, 'Target property inventory mismatch.');
  const before = auditPose(d, feet, times, plan.py.times);
  let changedCurves = 0;
  for (const replacement of plan.replacements) {
    if (replaceFloatArray(replacement.prop, replacement.values)) changedCurves++;
    replacement.curve.keys = replacement.prop.value;
  }
  const expected = serialize(fbx), parsed = parseFbx(expected), afterDescription = describe(parsed);
  afterDescription.globalProperties = props70(parsed.nodes.find(n => n.name === 'GlobalSettings'));
  validateTransforms(afterDescription);
  const afterInventory = propertyInventory(parsed), changedPaths = [];
  demand(originalInventory.size === afterInventory.size, 'Property inventory size changed.');
  for (const [key, p] of originalInventory) {
    const p2 = afterInventory.get(key); demand(p2, 'Property disappeared: ' + key);
    if (!p.raw.equals(p2.raw)) { demand(allowedPaths.includes(key), 'Unexpected property mutation: ' + key); changedPaths.push(key); }
  }
  demand(changedPaths.length === changedCurves, 'Unexpected changed-property count.');
  const oldFooter = footerParts(fbx), newFooter = footerParts(parsed);
  demand(oldFooter.prefix.equals(newFooter.prefix) && oldFooter.tail.equals(newFooter.tail), 'Opaque FBX trailer payload changed.');
  demand(d.stacks.length === afterDescription.stacks.length, 'Clip inventory changed.');
  const afterFeet = footGeometry(afterDescription), after = auditPose(afterDescription, afterFeet, times, plan.py.times);
  for (const foot of after.feet) {
    demand(foot.minimumY >= 0.0005, 'Dense source boot clearance below 0.5mm: ' + foot.bone);
    demand(foot.maximumKeyPositionDrift < 1e-5, 'Keyframe ankle stance drift: ' + foot.bone);
    demand(foot.maximumKeyOrientationElementDrift < 1e-5, 'Keyframe foot orientation drift: ' + foot.bone);
  }
  const secondPlan = solve(afterDescription);
  demand(secondPlan.replacements.every(r => r.values.every((v, k) => Object.is(Math.fround(v), r.curve.keys[k]))), 'Correction is not idempotent.');
  demand(serialize(parsed).equals(expected), 'Reparsed unchanged FBX serializer is not byte-idempotent.');
  return { output: expected, report: {
    schema: 'wreckabulary.pickup-contact-source-audit.v1', sourceSha256: sha(input), outputSha256: sha(expected),
    sourceBytes: input.length, outputBytes: expected.length, fbxVersion: fbx.version,
    status: 'PASS', changedCurves, changedPropertyPaths: changedPaths,
    preservedPropertyCount: originalInventory.size - changedPaths.length,
    clipCount: d.stacks.length, nonTargetPropertyBytesPreserved: true, opaqueTrailerPreserved: true,
    footerAlignmentRecomputed: true, idempotent: true,
    branch: 'Continuous positive internal knee angle, zero endpoint rotations; foot X cancels thigh plus shin X.',
    interpolation: 'Existing LINEAR baked FBX tracks; 256 interior subdivisions per source interval, both actual rigid boot vertex sets.',
    motion: plan.summaries, originalPose: before, correctedPose: after,
    limitations: ['Source FBX FK audit only; Unity import/compression, gameplay transitions and rendered appearance require separate validation.',
      'The crouch flexion direction changes from the original thigh-positive/shin-negative trajectory; visual review is required.',
      'Dense samples are not a formal continuum proof. Source clearance margin is at least 0.5mm at sampled times.',
      'Only six Pickup X KeyValueFloat arrays change; mesh/bind data, pelvis/upper body, other channels/clips and raw property compression bytes remain unchanged.']
  } };
}
function atomicWrite(output, bytes, inputPath, inputBytes) {
  const resolved = path.resolve(output), directory = path.dirname(resolved);
  demand(fs.existsSync(directory) && fs.statSync(directory).isDirectory(), 'Output directory must already exist.');
  demand(fs.readFileSync(inputPath).equals(inputBytes), 'Input changed during audit; refusing output.');
  if (fs.existsSync(resolved) && fs.readFileSync(resolved).equals(bytes)) return false;
  const temporary = path.join(directory, '.' + path.basename(resolved) + '.pickup-' + process.pid + '-' + crypto.randomBytes(6).toString('hex') + '.tmp');
  let fd;
  try {
    fd = fs.openSync(temporary, 'wx'); fs.writeFileSync(fd, bytes); fs.fsyncSync(fd); fs.closeSync(fd); fd = undefined;
    demand(fs.readFileSync(temporary).equals(bytes), 'Temporary output verification failed.');
    demand(fs.readFileSync(inputPath).equals(inputBytes), 'Input changed before atomic replacement.');
    fs.renameSync(temporary, resolved);
  } finally { if (fd !== undefined) fs.closeSync(fd); if (fs.existsSync(temporary)) fs.unlinkSync(temporary); }
  return true;
}
function physicalKey(file) {
  const resolved = path.resolve(file);
  const physical = fs.existsSync(resolved) ? fs.realpathSync.native(resolved)
    : path.join(fs.realpathSync.native(path.dirname(resolved)), path.basename(resolved));
  return process.platform === 'win32' ? physical.toLowerCase() : physical;
}
function sameFile(a, b) {
  if (physicalKey(a) === physicalKey(b)) return true;
  if (!fs.existsSync(a) || !fs.existsSync(b)) return false;
  const aa = fs.statSync(a, { bigint: true }), bb = fs.statSync(b, { bigint: true });
  return aa.ino !== 0n && aa.ino === bb.ino && aa.dev === bb.dev;
}
function validateDestination(file) {
  const dir = path.dirname(path.resolve(file));
  demand(fs.existsSync(dir) && fs.statSync(dir).isDirectory(), 'Destination directory must already exist.');
  fs.accessSync(dir, fs.constants.W_OK);
  if (fs.existsSync(file)) {
    demand(fs.statSync(file).isFile(), 'Destination must be a regular file.');
    fs.accessSync(file, fs.constants.W_OK);
  }
}
function main() {
  const args = process.argv.slice(2), options = {};
  if (args.includes('--help')) {
    console.log('node correct_pickup_contact.mjs --input Avatar.fbx [--output Avatar.fbx] [--report audit.json]\nNo output means an in-memory dry run. All checks must pass before output.'); return;
  }
  for (let k = 0; k < args.length; k += 2) {
    demand(['--input', '--output', '--report'].includes(args[k]) && args[k + 1] && !args[k + 1].startsWith('--'), 'Expected --input/--output/--report path pairs.');
    demand(!options[args[k]], 'Duplicate option: ' + args[k]); options[args[k]] = args[k + 1];
  }
  demand(options['--input'], '--input is required.');
  const inputPath = path.resolve(options['--input']), bytes = fs.readFileSync(inputPath);
  if (options['--output']) validateDestination(options['--output']);
  let reportPath;
  if (options['--report']) {
    reportPath = path.resolve(options['--report']); validateDestination(reportPath);
    demand(!sameFile(reportPath, inputPath) && !sameFile(reportPath, options['--output'] || inputPath),
      'Report must differ physically from input/output asset, including Windows case aliases and hard links.');
  }
  const result = correction(bytes), dryRun = !options['--output'];
  let wroteOutput = false;
  if (!dryRun) wroteOutput = atomicWrite(options['--output'], result.output, inputPath, bytes);
  const report = { ...result.report, dryRun, wroteOutput };
  if (reportPath) {
    fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
  }
  console.log(JSON.stringify({ status: report.status, dryRun, wroteOutput, sourceSha256: report.sourceSha256,
    outputSha256: report.outputSha256, changedCurves: report.changedCurves, nonTargetPropertyBytesPreserved: true,
    idempotent: true, sourceMinimumBootY: report.originalPose.feet.map(f => f.minimumY),
    correctedMinimumBootY: report.correctedPose.feet.map(f => f.minimumY), denseSamples: report.correctedPose.samples }));
}
try { main(); } catch (error) { console.error(JSON.stringify({ status: 'FAIL', error: error.message })); process.exitCode = 1; }
