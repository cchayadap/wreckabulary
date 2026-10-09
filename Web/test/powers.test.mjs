import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { Game } from "../src/engine.js";
import { recipeDescription, itemActionLabel } from "../src/item-presentation.js";
import { Group, Object3D } from "three";
import { AvatarAnimator } from "../src/animator.js";
import { WorldView } from "../src/renderer.js";

const read = (name) => JSON.parse(readFileSync(new URL(`../../Assets/_Project/Data/Config/${name}.json`, import.meta.url)));
const data = { rules: read("rules"), items: read("items"), house: read("house_pinwheel"), wardrobe: read("wardrobe") };
const expansion = ["APPLE", "WATER", "CAKE", "SODA", "SHIELD", "FAN", "CLOCK", "BROOM", "HAMMER", "SPEAR", "PIE", "STOOL"];
function arena() {
  const g = new Game(data, { mode: "Dibs", seed: 42 });
  for (const item of g.items) { item.x = 100; item.z = 100; }
  g.players.forEach((p, index) => {
    p.ai = false; p.x = -8; p.z = -8 + index; p.invulnerableUntil = 0;
  });
  Object.assign(g.players[0], { x: 0, z: 0, yaw: 0, facing: { x: 0, z: 1 } });
  g.time = 3;
  return g;
}
function craft(g, word, p = g.players[0]) {
  const before = new Set(g.tiles);
  g.mintTiles(word, p.x, p.z);
  for (const tile of g.tiles.filter(tile => !before.has(tile))) assert.ok(g.collect(p, tile));
  assert.equal(g.craft(p, word), null);
  g.time = p.craft.readyAt;
  g.completeCraft(p);
  return g.held(p);
}
function complete(g, p = g.players[0]) {
  assert.ok(p.action);
  g.time = p.action.readyAt;
  g.completeAction(p);
}
function deploy(g, word) {
  const item = craft(g, word);
  assert.equal(g.deploy(g.players[0]), null);
  complete(g);
  return item;
}
const balanced = g => assert.equal(g.audit().balanced, true, JSON.stringify(g.audit()));

test("Unity and browser expose the same 24 completed recipes with item-specific descriptions", () => {
  const web = JSON.parse(readFileSync(new URL("../public/data/items.json", import.meta.url)));
  assert.deepEqual(web, data.items);
  assert.equal(web.items.filter(item => item.enabled).length, 24);
  for (const id of expansion) assert.equal(web.items.find(item => item.id === id).enabled, true, id);
  for (const [id, expected] of [["APPLE", "30 HP"], ["WATER", "18 HP"], ["CAKE", "50 HP"],
    ["SODA", "speed"], ["SHIELD", "All-around"], ["FAN", "Forward gust"], ["CLOCK", "Slowing field"], ["PIE", "One throw"]])
    assert.ok(recipeDescription(web.items.find(item => item.id === id)).includes(expected), id);
});

for (const [word, amount] of [["APPLE", 30], ["WATER", 18], ["CAKE", 50]])
  test(`${word} heals only after its channel and consumes exactly its letters`, () => {
    const g = arena(), p = g.players[0], item = craft(g, word);
    p.hp = 40;
    assert.ok(g.use(p));
    g.time = p.action.readyAt - 0.001;
    g.completeAction(p);
    assert.equal(p.hp, 40);
    assert.equal(g.spent, 0);
    complete(g);
    assert.equal(p.hp, 40 + amount);
    assert.equal(item.state, "gone");
    assert.equal(g.spent, word.length);
    balanced(g);
  });

test("healing is capped; damage interrupts the channel without consuming the food", () => {
  const g = arena(), p = g.players[0], item = craft(g, "APPLE");
  p.hp = 95;
  g.use(p);
  g.hit(p, { damage: 1, attacker: 1, dx: 1, dz: 0 });
  assert.equal(p.action, null);
  assert.equal(item.state, "held");
  assert.equal(g.spent, 0);
  g.time += 1;
  g.use(p); complete(g);
  assert.equal(p.hp, 100);
  balanced(g);
});

test("SODA and MAT keep independent expiries and compose with CLOCK slowdown", () => {
  const g = arena(), p = g.players[0];
  craft(g, "SODA"); g.use(p); complete(g);
  const sodaUntil = p.speedUntil;
  assert.equal(g.movementScale(p), 1.35);
  g.boost(p, 1.6, 1.5);
  assert.equal(g.movementScale(p), 1.6);
  g.boost(p, 1.35, 6);
  assert.equal(g.movementScale(p), 1.6, "drinking cannot weaken a stronger active boost");
  p.slowStrength = 0.6; p.slowUntil = g.time + 3;
  assert.equal(g.movementScale(p), 1.6 * 0.6);
  g.time += 1.6;
  assert.equal(g.movementScale(p), 1.35 * 0.6, "SODA resumes after MAT ends");
  g.time = sodaUntil + 0.01;
  assert.equal(g.movementScale(p), 1);
  g.boost(p, 1.35, 6); p.slowUntil = g.time + 2;
  g.respawn(p);
  assert.equal(g.movementScale(p), 1);
  balanced(g);
});

test("SHIELD blocks rear and coincident hits while PLATE remains directional", () => {
  for (const word of ["SHIELD", "PLATE"]) {
    const g = arena(), p = g.players[0], item = craft(g, word);
    p.block = true;
    g.hit(p, { damage: 10, attacker: 1, dx: 0, dz: 1, blockable: true });
    assert.equal(p.hp, word === "SHIELD" ? 100 : 90);
    if (word === "SHIELD") {
      g.hit(p, { damage: 10, attacker: 1, dx: 0, dz: 0, blockable: true });
      assert.equal(p.hp, 100);
      assert.equal(item.durability, 70);
    }
    balanced(g);
  }
});

test("SHIELD raises before it can block and release starts a fresh raise", () => {
  const g = arena(), p = g.players[0];
  craft(g, "SHIELD");
  g.tick(0.01, { block: true });
  g.hit(p, { damage: 5, attacker: 1, dx: 0, dz: 1 });
  assert.equal(p.hp, 95, "first-frame block is not ready");
  g.time += 0.23;
  g.hit(p, { damage: 5, attacker: 1, dx: 0, dz: 1 });
  assert.equal(p.hp, 95, "raised shield blocks");
  g.tick(0.01, {});
  g.tick(0.01, { block: true });
  g.hit(p, { damage: 5, attacker: 1, dx: 0, dz: 1 });
  assert.equal(p.hp, 90);
  balanced(g);
});

test("shield attack input guards without punching and its pose waits for protection", () => {
  const g = arena(), p = g.players[0], shield = craft(g, "SHIELD");
  const avatar = new AvatarAnimator(new Group(), []);
  let heldPose = 0;
  const view = {
    manifest: { items: { SHIELD: { grip: [0, 0, 0] } } },
    avatarModels: new Map([[p.id, { animator: { yaw: 0, holdTransform: () => { heldPose++; return true; } } }]]),
  };
  const node = new Object3D();
  g.tick(0.01, { attack: true, block: true });
  assert.equal(p.block, true);
  assert.equal(g.isGuardRaised(p), false);
  assert.equal(p.pendingAttack, null);
  assert.equal(g.attack(p), false, "direct attack cannot punch during the raise");
  assert.equal(itemActionLabel(shield), "BLOCK");
  avatar.update(0.05, p, g, shield);
  WorldView.prototype.attachToHands.call(view, node, shield, p, 1, g);
  assert.equal(avatar.poseWeights.get("Block_Plate"), 0);
  assert.equal(heldPose, 1, "item remains in its held pose during the raise");
  g.time += 0.23;
  g.tick(0.01, { attack: true });
  assert.equal(g.isGuardRaised(p), true, "LMB alone keeps guarding");
  avatar.update(0.05, p, g, shield);
  WorldView.prototype.attachToHands.call(view, node, shield, p, 1, g);
  assert.ok(avatar.poseWeights.get("Block_Plate") > 0);
  assert.equal(heldPose, 1, "item now uses its raised transform");
  assert.equal(g.events.some(event => event.type === "swing" && event.player === p.id), false);
  g.tick(0.01, {});
  assert.equal(g.isGuardRaised(p), false);
  g.tick(0.01, { attack: true });
  assert.equal(g.isGuardRaised(p), false, "a new press raises again");
  assert.ok(g.dodge(p));
  assert.equal(p.block, false);
  balanced(g);
});

test("shield slots cannot inherit raised protection from another shield", () => {
  const g = arena(), p = g.players[0];
  craft(g, "SHIELD"); craft(g, "PLATE");
  g.setGuard(p, true); g.time += 0.3;
  assert.equal(g.isGuardRaised(p), true);
  g.selectSlot(p, 0);
  assert.equal(g.isGuardRaised(p), false);
  g.setGuard(p, true);
  assert.equal(g.isGuardRaised(p), false);
  balanced(g);
});

test("FAN pushes players and loose letters in its forward cone, not its owner behind it", () => {
  const g = arena(), p = g.players[0], q = g.players[1];
  deploy(g, "FAN");
  Object.assign(q, { x: 0, z: 2.6, ai: true });
  g.mintTiles("A", 0.4, 2.7);
  const tile = g.tiles.at(-1); tile.x = 0.4; tile.z = 2.7;
  g.tickZones(0.05);
  assert.ok(q.z > 2.6);
  assert.ok(tile.z > 2.7);
  assert.equal(p.velocity?.z ?? 0, 0);
  assert.ok(q.slipUntil > g.time);
  const field = g.zones[0];
  assert.equal(g.fieldReaches(field, { x: 2, z: 1.6 }), false, "outside the cone");
  balanced(g);
});

test("placed fields respect walls, physical cover and storeys", () => {
  const g = arena();
  deploy(g, "CLOCK");
  const zone = g.zones[0];
  assert.equal(g.fieldReaches(zone, { x: 0, z: 2.6 }), true);
  assert.equal(g.fieldReaches(zone, { x: 3, z: 2 }), false, "balcony is a different floor");
  Object.assign(zone, { x: 2, z: 3.5, y: 0 });
  assert.equal(g.fieldReaches(zone, { x: 2, z: 4.5 }), false, "solid wall");
  Object.assign(zone, { x: 0, z: 0, y: 0 });
  const cover = g.items.find(item => item.word === "TABLE");
  Object.assign(cover, { x: 0, z: 1.3, y: 0, anchored: true });
  assert.equal(g.fieldReaches(zone, { x: 0, z: 2.6 }), false, "anchored furniture blocks the field");
});

test("FAN drifts original props and dropped gear without changing ownership or letters", () => {
  const g = arena(), p = g.players[0];
  deploy(g, "FAN");
  const original = g.items.find(item => item.word === "SODA");
  Object.assign(original, { x: -0.5, z: 2.7, y: 0 });
  const ball = craft(g, "BALL"); g.drop(p);
  Object.assign(ball, { x: 0.5, z: 2.7, y: 0 });
  const audit = g.audit(), zone = g.zones[0];
  assert.equal(g.fieldReaches(zone, ball), true, "the target must not occlude itself");
  assert.equal(g.fieldReaches(zone, { x: 0.5, z: 3.7 }), true, "loose props do not shield the wind");
  for (let i = 0; i < 6; i++) g.tickZones(0.05);
  assert.ok(original.z > 2.7);
  assert.ok(ball.z > 2.7);
  assert.equal(original.origin, "map");
  assert.equal(ball.state, "world");
  assert.equal(ball.owner, null);
  assert.deepEqual(g.audit(), audit);
  p.x = ball.x; p.z = ball.z - 0.3;
  assert.ok(g.interact(p));
  assert.equal(ball.state, "held");
  assert.equal(ball.windVelocity, null);
  const heldZ = ball.z;
  g.tickZones(0.05);
  assert.equal(ball.z, heldZ, "wind must stop driving picked-up gear");
  balanced(g);
});

for (const state of ["anchored", "deployed"])
  test(`FAN cannot move or blow through ${state} cover`, () => {
    const g = arena(); deploy(g, "FAN");
    const cover = g.items.find(item => item.word === "TABLE"), prop = g.items.find(item => item.word === "SODA");
    Object.assign(cover, { x: 0, z: 2.6, y: 0, anchored: state === "anchored", state: state === "deployed" ? "deployed" : "world" });
    Object.assign(prop, { x: 0, z: 3.7, y: 0 });
    assert.equal(g.fieldReaches(g.zones[0], prop), false);
    for (let i = 0; i < 8; i++) g.tickZones(0.05);
    assert.equal(cover.z, 2.6);
    assert.equal(prop.z, 3.7);
    balanced(g);
  });

test("wind-driven loose props stop their complete footprint at a wall", () => {
  const g = arena(); const fan = deploy(g, "FAN"), zone = g.zones[0];
  Object.assign(fan, { x: 2, z: 2 }); Object.assign(zone, { x: 2, z: 2 });
  const prop = g.items.find(item => item.word === "SODA");
  Object.assign(prop, { x: 2, z: 3.5, y: 0 });
  for (let i = 0; i < 80; i++) g.tickZones(0.05);
  assert.ok(prop.z > 3.5, "wind moves the loose prop");
  assert.ok(prop.z + prop.definition.size[2] / 2 <= 4, "whole prop stays before the wall");
  balanced(g);
});

test("CLOCK slows every eligible actor, expires after leaving, and refunds once on field expiry", () => {
  const g = arena(), p = g.players[0], q = g.players[1], item = deploy(g, "CLOCK");
  Object.assign(q, { x: 0, z: 2.6 });
  g.tickZones(0.05);
  assert.equal(g.movementScale(p), 0.6, "neutral field includes owner");
  assert.equal(g.movementScale(q), 0.6);
  q.x = -8; q.z = -8;
  g.time += 0.19;
  assert.equal(g.movementScale(q), 1);
  g.time = g.zones[0].expires;
  g.tickZones(0.05);
  assert.equal(item.state, "gone");
  assert.equal(g.tiles.length, 5);
  g.tickZones(0.05);
  assert.equal(g.tiles.length, 5);
  balanced(g);
});

test("picking up a reusable field cancels its timer without refunding held letters", () => {
  const g = arena(), p = g.players[0], item = deploy(g, "FAN"), expiry = g.zones[0].expires;
  p.z = item.z - 0.7;
  assert.ok(g.interact(p));
  assert.equal(g.zones.length, 0);
  g.time = expiry + 1;
  g.tickZones(0.05);
  assert.equal(item.state, "held");
  assert.equal(g.tiles.length, 0);
  balanced(g);
});

for (const impact of ["player", "wall", "timeout"])
  test(`PIE is spent at throw and splats once on ${impact}`, () => {
    const g = arena(), p = g.players[0], q = g.players[1], pie = craft(g, "PIE");
    if (impact === "player") Object.assign(q, { x: 0, z: 1.9 });
    if (impact === "wall") Object.assign(p, { x: 2, z: 3 });
    if (impact === "timeout") p.facing = { x: 1, z: 0 };
    assert.ok(g.throw(p));
    assert.equal(g.spent, 3);
    for (let i = 0; i < 30; i++) { g.time += 0.05; g.tickProjectiles(0.05); }
    assert.equal(pie.state, "gone");
    assert.equal(g.projectiles.length, 0);
    assert.equal(g.spent, 3);
    assert.equal(g.tiles.length, 0);
    if (impact === "player") assert.equal(q.hp, 92);
    assert.equal(g.events.filter(event => event.type === "splat").length, 1);
    balanced(g);
  });
