export const HOME_LIMITS = Object.freeze({
  props: 64,
  text: 2048,
  words: 64,
  json: 65536,
  grid: 0.5,
  roomMargin: 0.15,
  propGap: 0.1,
  spawnRadius: 0.75,
  doorMargin: 0.35,
});
const EPS = 1e-6;
const angles = [0, 90, 180, 270];
const skins = ["Classic", "Candy", "Arcade"];
const documentKeys = ["schema", "map", "name", "props"];
const propKeys = ["id", "word", "x", "z", "yaw", "skin"];
const object = (value) =>
  value !== null && typeof value === "object" && !Array.isArray(value);
const exactKeys = (value, keys) =>
  object(value) &&
  keys.every((k) => Object.hasOwn(value, k)) &&
  Object.keys(value).every((k) => keys.includes(k));
const houseFor = (map, data) =>
  map === "pinwheel" || map === "courtyard"
    ? (data?.houses?.[map] ?? (map === "pinwheel" ? data?.house : null))
    : null;
const supplied = (word, data) => {
  const items = Array.isArray(data?.items) ? data.items : data?.items?.items;
  return items?.find(
    (i) =>
      i.id === word &&
      typeof i.model === "string" &&
      i.model.length &&
      Array.isArray(i.size) &&
      i.size.length === 3 &&
      Number.isFinite(i.size[0]) &&
      i.size[0] > 0 &&
      Number.isFinite(i.size[2]) &&
      i.size[2] > 0,
  );
};
const cloneProp = (p) => ({
  id: p.id,
  word: p.word,
  x: p.x === 0 ? 0 : p.x,
  z: p.z === 0 ? 0 : p.z,
  yaw: p.yaw,
  skin: p.skin,
});
const cloneLayout = (layout) => ({
  schema: layout.schema,
  map: layout.map,
  name: layout.name,
  props: layout.props.map(cloneProp),
});
const failure = (error) => ({ ok: false, errors: [error] });

export function createLayout(map, name = "My Cozy House") {
  return {
    schema: 1,
    map,
    name: typeof name === "string" ? name.trim() : "",
    props: [],
  };
}

function shape(prop, data, errors) {
  if (!exactKeys(prop, propKeys)) {
    errors.push("prop: expected exactly id, word, x, z, yaw, skin.");
    return null;
  }
  if (
    typeof prop.id !== "string" ||
    !/^[A-Za-z0-9_-]{1,48}$/.test(prop.id) ||
    /[^A-Za-z0-9_-]/.test(prop.id)
  )
    errors.push("id: use 1–48 letters, digits, underscores or hyphens.");
  const item = typeof prop.word === "string" && supplied(prop.word, data);
  if (!item) errors.push("word: choose one of the 40 supplied object words.");
  const grid =
    Number.isFinite(prop.x) &&
    Number.isInteger(prop.x * 2) &&
    Number.isFinite(prop.z) &&
    Number.isInteger(prop.z * 2);
  if (!grid) errors.push("grid: use finite half-metre coordinates.");
  const yaw = angles.includes(prop.yaw);
  if (!yaw) errors.push("yaw: use 0, 90, 180 or 270 degrees.");
  if (!skins.includes(prop.skin))
    errors.push("skin: choose Classic, Candy or Arcade.");
  if (!item || !grid || !yaw) return null;
  let width = Math.max(0.4, item.size[0]),
    depth = Math.max(0.4, item.size[2]);
  if (prop.yaw % 180) [width, depth] = [depth, width];
  return {
    minX: prop.x - width / 2,
    maxX: prop.x + width / 2,
    minZ: prop.z - depth / 2,
    maxZ: prop.z + depth / 2,
  };
}

const inside = (room, r) =>
  r.minX >= room.bounds[0] + HOME_LIMITS.roomMargin - EPS &&
  r.maxX <= room.bounds[2] - HOME_LIMITS.roomMargin + EPS &&
  r.minZ >= room.bounds[1] + HOME_LIMITS.roomMargin - EPS &&
  r.maxZ <= room.bounds[3] - HOME_LIMITS.roomMargin + EPS;
const circleDistance = (r, x, z) =>
  Math.hypot(
    Math.max(r.minX - x, 0, x - r.maxX),
    Math.max(r.minZ - z, 0, z - r.maxZ),
  );
const rectDistance = (a, b) =>
  Math.hypot(
    Math.max(a.minX - b.maxX, 0, b.minX - a.maxX),
    Math.max(a.minZ - b.maxZ, 0, b.minZ - a.maxZ),
  );
function geometry(house, r, others, errors) {
  if (!house.rooms.some((room) => inside(room, r)))
    errors.push("room: keep the entire footprint 0.15 m inside one room.");
  if (
    house.doors.some(
      (d) =>
        circleDistance(r, d.at[0], d.at[1]) <
        d.width / 2 + HOME_LIMITS.doorMargin - EPS,
    )
  )
    errors.push("door: this footprint blocks a reserved doorway.");
  if (
    house.spawns.some(
      (s) =>
        circleDistance(r, s.at[0], s.at[1]) < HOME_LIMITS.spawnRadius - EPS,
    )
  )
    errors.push("spawn: this footprint blocks a reserved spawn.");
  if (
    others.some((other) => rectDistance(r, other) < HOME_LIMITS.propGap - EPS)
  )
    errors.push("overlap: leave at least 0.1 m between object footprints.");
}

export function validateLayout(layout, data) {
  if (!exactKeys(layout, documentKeys))
    return failure("layout: expected exactly schema, map, name, props.");
  const errors = [],
    house = houseFor(layout.map, data);
  if (layout.schema !== 1) errors.push("schema: only schema 1 is supported.");
  if (!house) errors.push("map: choose pinwheel or courtyard.");
  if (
    typeof layout.name !== "string" ||
    !layout.name.length ||
    layout.name.length > 48 ||
    layout.name !== layout.name.trim()
  )
    errors.push("name: use a trimmed name of 1–48 characters.");
  if (!Array.isArray(layout.props) || layout.props.length > HOME_LIMITS.props) {
    errors.push("props: provide an array of at most 64 objects.");
    return { ok: false, errors };
  }
  const ids = new Set(),
    rectangles = [];
  for (let i = 0; i < layout.props.length; i++) {
    const p = layout.props[i],
      problems = [],
      rect = shape(p, data, problems);
    if (p && typeof p.id === "string") {
      if (ids.has(p.id)) problems.push("id: prop IDs must be unique.");
      ids.add(p.id);
    }
    if (rect) {
      if (house) geometry(house, rect, rectangles, problems);
      rectangles.push(rect);
    }
    errors.push(...problems.map((e) => `${e} [prop ${i + 1}]`));
  }
  return errors.length
    ? { ok: false, errors }
    : { ok: true, errors, layout: cloneLayout(layout) };
}

export function placementCheck(layout, prop, data, ignoreId = null) {
  const valid = validateLayout(layout, data);
  if (!valid.ok) return { ok: false, errors: valid.errors };
  const errors = [],
    replacing =
      ignoreId !== null && layout.props.some((p) => p.id === ignoreId);
  if (!replacing && layout.props.length >= HOME_LIMITS.props)
    errors.push("props: the house already has 64 objects.");
  const rect = shape(prop, data, errors),
    others = layout.props.filter((p) => p.id !== ignoreId);
  if (prop && others.some((p) => p.id === prop.id))
    errors.push("id: prop IDs must be unique.");
  if (rect)
    geometry(
      houseFor(layout.map, data),
      rect,
      others.map((p) => shape(p, data, [])),
      errors,
    );
  return { ok: !errors.length, errors };
}

export function addWords(layout, text, room, data) {
  const valid = validateLayout(layout, data);
  const result = {
    ok: false,
    added: [],
    rejected: [],
    errors: [...valid.errors],
  };
  if (!valid.ok) return result;
  if (typeof text !== "string" || text.length > HOME_LIMITS.text) {
    result.errors.push("text: enter at most 2048 characters.");
    return result;
  }
  const words = text
    .split(/[\s,;]+/u)
    .filter(Boolean)
    .map((w) => w.replace(/[a-z]/g, (c) => c.toUpperCase()));
  if (words.length > HOME_LIMITS.words) {
    result.errors.push("text: enter at most 64 words per batch.");
    return result;
  }
  const house = houseFor(layout.map, data),
    target = house.rooms.find((r) => r.name === room);
  if (!target) {
    result.errors.push("room: choose an authored room.");
    return result;
  }
  const next = valid.layout,
    occupied = next.props.map((p) => shape(p, data, []));
  const ids = new Set(next.props.map((p) => p.id));
  for (const word of words) {
    const reason = !supplied(word, data)
      ? "word: choose one of the 40 supplied object words."
      : next.props.length >= HOME_LIMITS.props
        ? "props: the house already has 64 objects."
        : null;
    if (reason) {
      result.rejected.push({ word, reason });
      continue;
    }
    let serial = 1;
    while (ids.has(`p${serial}`)) serial++;
    const candidate = {
      id: `p${serial}`,
      word,
      x: 0,
      z: 0,
      yaw: 0,
      skin: "Classic",
    };
    const firstX = Math.ceil(
      (target.bounds[0] + HOME_LIMITS.roomMargin) / HOME_LIMITS.grid,
    );
    const lastX = Math.floor(
      (target.bounds[2] - HOME_LIMITS.roomMargin) / HOME_LIMITS.grid,
    );
    const firstZ = Math.ceil(
      (target.bounds[1] + HOME_LIMITS.roomMargin) / HOME_LIMITS.grid,
    );
    const lastZ = Math.floor(
      (target.bounds[3] - HOME_LIMITS.roomMargin) / HOME_LIMITS.grid,
    );
    let placed = false;
    for (let z = firstZ; z <= lastZ && !placed; z++)
      for (let x = firstX; x <= lastX && !placed; x++)
        for (const yaw of angles) {
          candidate.x = x * HOME_LIMITS.grid;
          candidate.z = z * HOME_LIMITS.grid;
          candidate.yaw = yaw;
          const rect = shape(candidate, data, []),
            errors = [];
          if (!inside(target, rect)) continue;
          geometry(house, rect, occupied, errors);
          if (errors.length) continue;
          next.props.push(cloneProp(candidate));
          occupied.push(rect);
          ids.add(candidate.id);
          result.added.push(cloneProp(candidate));
          placed = true;
          break;
        }
    if (!placed)
      result.rejected.push({
        word,
        reason: "room: no safe snapped position remains in this room.",
      });
  }
  result.ok = true;
  result.layout = next;
  return result;
}

function parseDocument(text) {
  if (typeof text !== "string" || text.length > HOME_LIMITS.json)
    throw new Error("provide at most 65536 JSON characters.");
  let pos = 0;
  const space = () => {
    while (/[ \t\r\n]/.test(text[pos] ?? "!")) pos++;
  };
  const string = () => {
    const start = pos++;
    while (pos < text.length) {
      const c = text[pos++];
      if (c === '"') return JSON.parse(text.slice(start, pos));
      if (c === "\\") pos++;
    }
    throw new Error("unterminated string.");
  };
  const value = (depth) => {
    space();
    if (text[pos] === "{" || text[pos] === "[") {
      if (depth > 16) throw new Error("JSON nesting exceeds 16 levels.");
      const isObject = text[pos++] === "{",
        end = isObject ? "}" : "]",
        keys = new Set();
      space();
      if (text[pos] === end) {
        pos++;
        return;
      }
      for (;;) {
        space();
        if (isObject) {
          if (text[pos] !== '"') throw new Error("expected a quoted key.");
          const key = string();
          if (keys.has(key))
            throw new Error(`duplicate key ${JSON.stringify(key)}.`);
          keys.add(key);
          space();
          if (text[pos++] !== ":") throw new Error("expected ':' after key.");
        }
        value(depth + 1);
        space();
        const next = text[pos++];
        if (next === end) return;
        if (next !== ",") throw new Error("expected comma or closing bracket.");
      }
    }
    if (text[pos] === '"') {
      string();
      return;
    }
    const token =
      /^(?:-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null)/.exec(
        text.slice(pos),
      );
    if (!token) throw new Error("invalid JSON token.");
    pos += token[0].length;
  };
  value(1);
  space();
  if (pos !== text.length) throw new Error("unexpected text after document.");
  return JSON.parse(text);
}

export function importLayout(text, data) {
  try {
    return validateLayout(parseDocument(text), data);
  } catch (e) {
    return failure(`json: ${e.message}`);
  }
}

export function exportLayout(layout, data) {
  const valid = validateLayout(layout, data);
  return valid.ok ? { ...valid, json: JSON.stringify(valid.layout) } : valid;
}
