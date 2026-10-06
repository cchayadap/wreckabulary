import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  createLayout,
  validateLayout,
  placementCheck,
  addWords,
  importLayout,
  exportLayout,
} from "../src/home-design.js";

const read = (name) =>
  JSON.parse(
    readFileSync(
      new URL(
        `../../Assets/_Project/Data/Config/${name}.json`,
        import.meta.url,
      ),
    ),
  );
const data = {
  items: read("items"),
  houses: {
    pinwheel: read("house_pinwheel"),
    courtyard: read("house_courtyard"),
  },
};
const fixtures = JSON.parse(
  readFileSync(
    new URL("../../Tools/HomeDesign/fixtures.json", import.meta.url),
  ),
);
const empty = (map = "pinwheel") => createLayout(map, "Cozy");
const prop = (patch = {}) => ({
  id: "p1",
  word: "BALL",
  x: 0,
  z: 0,
  yaw: 0,
  skin: "Classic",
  ...patch,
});
const withProps = (...props) => ({ ...empty(), props });
const refused = (result, code) => {
  assert.equal(result.ok, false);
  assert.equal(
    result.layout,
    undefined,
    "A rejected operation cannot replace the current layout",
  );
  assert.ok(
    result.errors.some((e) => e.startsWith(code)),
    result.errors.join("; "),
  );
};
const roomData = (bounds = [-5, -5, 5, 5], doors = [], spawns = []) => ({
  ...data,
  houses: { pinwheel: { rooms: [{ name: "Room", bounds }], doors, spawns } },
});

for (const c of fixtures.imports)
  test(`portable import: ${c.name}`, () => {
    const result = importLayout(c.json, data);
    assert.equal(result.ok, c.ok, result.errors.join("; "));
    if (c.ok)
      assert.deepEqual(
        importLayout(exportLayout(result.layout, data).json, data).layout,
        result.layout,
      );
    else {
      assert.equal(result.layout, undefined);
      if (c.errorPrefix)
        assert.ok(
          result.errors.some((e) => e.startsWith(c.errorPrefix)),
          result.errors.join("; "),
        );
    }
  });

for (const c of fixtures.batches)
  test(`portable deterministic word batch: ${c.name}`, () => {
    const layout = empty(c.map),
      before = structuredClone(layout);
    const result = addWords(layout, c.text, c.room, data);
    assert.equal(result.ok, true, result.errors.join("; "));
    assert.deepEqual(result.added, c.expected);
    assert.deepEqual(
      result.rejected.map((r) => r.word),
      c.rejected,
    );
    assert.deepEqual(layout, before);
    assert.equal(validateLayout(result.layout, data).ok, true);
  });

for (const c of fixtures.creations)
  test(`portable creation: ${c.name}`, () => {
    const layout = createLayout(c.map, c.input);
    assert.equal(layout.name, c.expectedName);
    assert.equal(validateLayout(layout, data).ok, true);
  });

test("first-fit rotates when the authored room shape requires it", () => {
  const result = addWords(
    empty(),
    "BED",
    "Room",
    roomData([-2, -0.75, 2, 0.75]),
  );
  assert.equal(result.ok, true);
  assert.deepEqual(result.added, [prop({ word: "BED", x: -1, yaw: 90 })]);
});

test("all forty supplied models are decor; unmodelled legacy words remain unavailable", () => {
  const supplied = data.items.items.filter((i) => i.model);
  assert.equal(supplied.length, 40);
  for (const item of supplied)
    assert.equal(
      validateLayout(withProps(prop({ word: item.id })), data).ok,
      true,
      item.id,
    );
  for (const item of data.items.items.filter((i) => !i.model))
    refused(validateLayout(withProps(prop({ word: item.id })), data), "word:");
});

test("quarter-turn swaps the entire footprint, including at a narrow wall", () => {
  const narrow = roomData([-0.75, -2, 0.75, 2]);
  for (const yaw of [0, 180])
    assert.equal(
      validateLayout(withProps(prop({ word: "BED", yaw })), narrow).ok,
      true,
    );
  for (const yaw of [90, 270])
    refused(
      validateLayout(withProps(prop({ word: "BED", yaw })), narrow),
      "room:",
    );
});

test("reserved doorway checks the rectangle, and permits a genuinely clear corner", () => {
  const door = roomData(undefined, [{ at: [0, 0], width: 1.4 }]);
  refused(validateLayout(withProps(prop({ x: 1, z: 0.5 })), door), "door:");
  assert.equal(validateLayout(withProps(prop({ x: 1, z: 1 })), door).ok, true);
});

test("reserved spawns and 0.4m minimum footprints apply even to tiny supplied objects", () => {
  const spawn = roomData(undefined, [], [{ at: [0, 0] }]);
  refused(
    validateLayout(withProps(prop({ word: "APPLE", x: 0.5, z: 0.5 })), spawn),
    "spawn:",
  );
  assert.equal(
    validateLayout(withProps(prop({ word: "APPLE", x: 1 })), spawn).ok,
    true,
  );
});

test("one tenth metre of clearance is allowed; overlapping footprints are refused", () => {
  assert.equal(
    validateLayout(
      withProps(
        prop({ word: "APPLE" }),
        prop({ id: "p2", word: "APPLE", x: 0.5 }),
      ),
      data,
    ).ok,
    true,
  );
  refused(
    validateLayout(withProps(prop(), prop({ id: "p2" })), data),
    "overlap:",
  );
});

test("snapshots are detached and rejected import or batch never changes current state", () => {
  const current = withProps(prop()),
    before = structuredClone(current);
  const accepted = validateLayout(current, data);
  accepted.layout.props[0].skin = "Candy";
  assert.deepEqual(current, before);
  const bad = structuredClone(current);
  bad.props[0].x = 0.25;
  refused(importLayout(JSON.stringify(bad), data), "grid:");
  const batch = addWords(current, "SOFA", "LivingRoom", data);
  batch.added[0].skin = "Arcade";
  assert.equal(batch.layout.props.at(-1).skin, "Classic");
  refused(addWords(current, "BALL ".repeat(65), "LivingRoom", data), "text:");
  assert.deepEqual(current, before);
});

test("partial word batch preserves order and allocates the smallest unused ID", () => {
  const layout = withProps(prop(), prop({ id: "p3", x: 1 }));
  const result = addWords(layout, "axE SOFA unknown PLANT", "LivingRoom", data);
  assert.equal(result.ok, true);
  assert.deepEqual(
    result.added.map((p) => p.id),
    ["p2", "p4"],
  );
  assert.deepEqual(
    result.rejected.map((p) => p.word),
    ["AXE", "UNKNOWN"],
  );
  assert.equal(layout.props.length, 2);
});

test("oversized batches, unknown rooms and malformed inputs are atomic refusals", () => {
  const layout = empty(),
    before = structuredClone(layout);
  refused(addWords(layout, "a".repeat(2049), "LivingRoom", data), "text:");
  refused(addWords(layout, "BALL ".repeat(65), "LivingRoom", data), "text:");
  refused(addWords(layout, "SOFA", "Attic", data), "room:");
  refused(addWords(layout, null, "LivingRoom", data), "text:");
  assert.deepEqual(layout, before);
});

test("a full 64-object design can be edited, and cannot accept a 65th object", () => {
  const filled = addWords(
    empty("courtyard"),
    "APPLE ".repeat(64),
    "Garden",
    data,
  );
  assert.equal(filled.added.length, 64);
  assert.equal(
    placementCheck(
      filled.layout,
      { ...filled.layout.props[0], skin: "Candy" },
      data,
      "p1",
    ).ok,
    true,
  );
  refused(
    placementCheck(filled.layout, prop({ id: "new", x: 3, z: 3 }), data),
    "props:",
  );
  const batch = addWords(filled.layout, "APPLE", "Garden", data);
  assert.equal(batch.ok, true);
  assert.equal(batch.added.length, 0);
  assert.equal(batch.rejected.length, 1);
  const oversized = structuredClone(filled.layout);
  oversized.props.push(prop({ id: "overflow" }));
  refused(validateLayout(oversized, data), "props:");
});

test("non-finite, coercible and almost-snapped coordinates are refused", () => {
  for (const x of [NaN, Infinity, -Infinity, 1e308, 0.500000001, "0", null])
    refused(validateLayout(withProps(prop({ x })), data), "grid:");
});

test("IDs must be whole ASCII identifiers; duplicate IDs and trailing newline are invalid", () => {
  for (const id of ["", "has space", "p1\n", "a".repeat(49), "🏡"])
    refused(validateLayout(withProps(prop({ id })), data), "id:");
  refused(validateLayout(withProps(prop(), prop({ x: 1 })), data), "id:");
});

test("name trims only at creation, respects UTF-16 bounds, and roundtrips escapes", () => {
  assert.equal(createLayout("pinwheel", "\ufeff Cozy \u00a0").name, "Cozy");
  assert.equal(
    validateLayout(createLayout("pinwheel", "🏡".repeat(24)), data).ok,
    true,
  );
  refused(
    validateLayout(createLayout("pinwheel", "🏡".repeat(25)), data),
    "name:",
  );
  refused(validateLayout(createLayout("pinwheel", "  "), data), "name:");
  refused(
    importLayout(JSON.stringify({ ...empty(), name: " Cozy " }), data),
    "name:",
  );
  const layout = {
    ...empty("courtyard"),
    name: 'My "Cozy" \\ House 🏡',
    props: [prop({ word: "SOFA", x: -11, z: -8, yaw: 270, skin: "Arcade" })],
  };
  assert.deepEqual(
    importLayout(exportLayout(layout, data).json, data).layout,
    layout,
  );
});

test("import rejects deep, oversized, missing, extra and wrong-type fields", () => {
  refused(importLayout(" ".repeat(65537), data), "json:");
  refused(importLayout("[".repeat(17) + "0" + "]".repeat(17), data), "json:");
  for (const layout of [
    null,
    [],
    {},
    { ...empty(), props: null },
    { ...empty(), extra: 1 },
    withProps({ ...prop(), extra: 1 }),
    withProps({ ...prop(), word: null }),
  ])
    assert.equal(importLayout(JSON.stringify(layout), data).ok, false);
});

test("no safe grid space rejects individual words without moving existing decor", () => {
  const tiny = roomData([-0.5, -0.5, 0.5, 0.5]),
    layout = withProps(prop({ word: "APPLE" }));
  const before = structuredClone(layout),
    result = addWords(layout, "APPLE SOFA", "Room", tiny);
  assert.equal(result.ok, true);
  assert.equal(result.added.length, 0);
  assert.equal(result.rejected.length, 2);
  assert.deepEqual(layout, before);
  assert.deepEqual(result.layout, before);
});
