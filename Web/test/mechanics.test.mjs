import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { Game, rulesFor, canSpell, inArc } from "../src/engine.js";
const read = (n) =>
  JSON.parse(
    readFileSync(
      new URL(`../../Assets/_Project/Data/Config/${n}.json`, import.meta.url),
    ),
  );
const data = {
  rules: read("rules"),
  items: read("items"),
  house: read("house_pinwheel"),
  wardrobe: read("wardrobe"),
};
data.houses = { pinwheel: data.house, courtyard: read("house_courtyard") };
const game = (mode = "Dibs", map = "pinwheel") =>
  new Game(data, { mode, map, seed: 42 });
function give(g, p, word) {
  g.mintTiles(word, p.x, p.z);
  for (const tile of [...g.tiles]) g.collect(p, tile);
}
function craft(g, p, word) {
  give(g, p, word);
  assert.equal(g.craft(p, word), null);
  g.time = p.craft.readyAt;
  g.completeCraft(p);
  return g.held(p);
}
function finishAction(g, p) {
  if (p.action) {
    g.time = p.action.readyAt;
    g.completeAction(p);
  }
}
const balanced = (g) =>
  assert.equal(g.audit().balanced, true, JSON.stringify(g.audit()));
test("canonical values: 100HP, 18-letter bag, zero letters lost per hit", () => {
  const g = game(),
    p = g.players[0];
  assert.equal(p.hp, 100);
  assert.equal(g.rules.maxLetters, 18);
  assert.equal(g.rules.lettersDroppedPerHit, 0);
  give(g, p, "TABLE");
  g.time = 3;
  g.hit(p, { damage: 14, attacker: 1, dx: 1, dz: 0 });
  assert.equal(p.hp, 86);
  assert.equal(p.bag, "TABLE");
  balanced(g);
});
test("mode overlays preserve the nested clear-out defaults", () => {
  const r = rulesFor(data, "MovingOut");
  assert.equal(r.clearOut.firstAt, 60);
  assert.equal(r.clearOut.interval, 40);
  assert.equal(r.clearOut.enabled, true);
  assert.equal(r.maxLetters, 18);
  assert.equal(r.maxHealth, 100);
});
test("letter multiset requires duplicates and matching exact letters", () => {
  assert.equal(canSpell("BOM", "BOMB"), false);
  assert.equal(canSpell("BBOM", "BOMB"), true);
  assert.equal(canSpell("BAT", "TABLE"), false);
});
test("bag limit includes reserved craft letters; excess tile stays in world", () => {
  const g = game(),
    p = g.players[0];
  give(g, p, "BATTABLEFOAMMATSOAP");
  assert.equal(p.bag.length, 18);
  g.craft(p, "BAT");
  const tile = { id: 999, char: "A", x: p.x, z: p.z };
  g.tiles.push(tile);
  g.minted++;
  assert.equal(g.collect(p, tile), false);
  assert.ok(g.tiles.includes(tile));
  assert.equal(p.bag.length, 15);
  balanced(g);
});
test("craft reserves, cancellation refunds, completion holds exact letters without duplication", () => {
  const g = game(),
    p = g.players[0];
  give(g, p, "TABLE");
  assert.equal(g.craft(p, "TABLE"), null);
  assert.equal(p.bag, "");
  assert.equal(p.craft.readyAt, 1.2);
  balanced(g);
  g.cancelCraft(p);
  assert.equal(p.bag, "TABLE");
  g.craft(p, "TABLE");
  g.time = 1.19;
  g.completeCraft(p);
  assert.ok(p.craft);
  g.time = 1.2;
  g.completeCraft(p);
  assert.equal(g.held(p).word, "TABLE");
  balanced(g);
});
test("disabled recipes refuse atomically", () => {
  const g = game(),
    p = g.players[0];
  give(g, p, "APPLE");
  const audit = g.audit();
  assert.match(g.craft(p, "APPLE"), /not available/);
  assert.equal(p.bag, "APPLE");
  assert.deepEqual(g.audit(), audit);
});
test("breaking original furniture returns its exact word and is idempotent", () => {
  const g = game(),
    item = g.items.find((i) => i.word === "TABLE"),
    before = g.tiles.length;
  g.breakItem(item);
  assert.equal(g.tiles.length - before, 5);
  assert.equal(g.tiles.map((t) => t.char).join(""), "TABLE");
  g.breakItem(item);
  assert.equal(g.tiles.length - before, 5);
  balanced(g);
});
test("two inventory slots and two deployments are independent limits", () => {
  const g = game(),
    p = g.players[0];
  craft(g, p, "TABLE");
  g.drop(p);
  craft(g, p, "SOFA");
  g.drop(p);
  craft(g, p, "BED");
  assert.equal(g.deploy(p), null);
  finishAction(g, p);
  craft(g, p, "MAT");
  assert.equal(g.deploy(p), null);
  finishAction(g, p);
  craft(g, p, "TABLE");
  assert.match(g.deploy(p), /Two tools/);
  assert.equal(g.held(p).word, "TABLE");
  balanced(g);
});
test("PLATE blocks front only and wears by blocked damage", () => {
  const g = game(),
    p = g.players[0],
    plate = craft(g, p, "PLATE");
  g.time = 4;
  p.facing = { x: 0, z: 1 };
  p.block = true;
  const result = g.hit(p, {
    damage: 14,
    attacker: 1,
    dx: 0,
    dz: -1,
    blockable: true,
  });
  assert.equal(result.blocked, 14);
  assert.equal(p.hp, 100);
  assert.equal(plate.durability, 46);
  g.hit(p, { damage: 14, attacker: 1, dx: 0, dz: 1, blockable: true });
  assert.equal(p.hp, 86);
  balanced(g);
});
test("hazards bypass a frontal shield", () => {
  const g = game(),
    p = g.players[0];
  craft(g, p, "PLATE");
  g.time = 4;
  p.block = true;
  p.facing = { x: 0, z: 1 };
  g.hit(p, { damage: 8, attacker: -1, dx: 0, dz: -1, blockable: false });
  assert.equal(p.hp, 92);
});
test("FOAM refreshes instead of stacking and expires at the latest timestamp", () => {
  const g = game(),
    p = g.players[0];
  craft(g, p, "FOAM");
  g.use(p);
  finishAction(g, p);
  assert.equal(p.bubble, 35);
  const first = p.bubbleUntil;
  g.time += 1;
  craft(g, p, "FOAM");
  g.use(p);
  finishAction(g, p);
  assert.equal(p.bubble, 35);
  assert.ok(p.bubbleUntil > first);
  g.time = first + 0.01;
  g.tick(0.01);
  assert.equal(p.bubble, 35);
  g.time = p.bubbleUntil;
  g.tick(0.01);
  assert.equal(p.bubble, 0);
  assert.equal(g.spent, 8);
  balanced(g);
});
test("dodge invulnerability is brief; cooldown is enforced", () => {
  const g = game(),
    p = g.players[0];
  g.time = 3;
  assert.equal(g.dodge(p), true);
  assert.equal(g.dodge(p), false);
  g.hit(p, { damage: 40, attacker: 1 });
  assert.equal(p.hp, 100);
  g.time = 3.16;
  g.hit(p, { damage: 40, attacker: 1 });
  assert.equal(p.hp, 60);
  g.time = 4.5;
  assert.equal(g.dodge(p), true);
});
test("friendly fire rejects an ally but allows self-damage from a bomb", () => {
  const g = game("Duos"),
    p = g.players[0];
  g.time = 3;
  assert.equal(g.hit(p, { damage: 45, attacker: 1 }).ignored, true);
  assert.equal(p.hp, 100);
  g.hit(p, { damage: 45, attacker: 0 });
  assert.equal(p.hp, 55);
});
test("Duos downed player can be revived only by the nearby live teammate", () => {
  const g = game("Duos"),
    p = g.players[0],
    buddy = g.players[1];
  g.time = 3;
  g.hit(p, { damage: 200, attacker: 2 });
  assert.equal(p.state, "downed");
  assert.equal(p.bleedAt, 23);
  buddy.x = p.x;
  buddy.z = p.z;
  g.interact(buddy);
  g.time += 2.9;
  g.interact(buddy);
  assert.equal(p.state, "downed");
  g.time += 0.1;
  g.interact(buddy);
  assert.equal(p.state, "alive");
  assert.equal(p.hp, 30);
  assert.equal(p.invulnerableUntil, g.time + 1);
  balanced(g);
});
test("knockout drops letters and held items once", () => {
  const g = game(),
    p = g.players[0],
    item = craft(g, p, "BAT");
  give(g, p, "FOAM");
  g.time = 5;
  g.hit(p, { damage: 200, attacker: 1 });
  assert.equal(p.state, "eliminated");
  assert.equal(p.bag, "");
  assert.equal(item.state, "world");
  assert.equal(g.tiles.map((t) => t.char).join(""), "FOAM");
  g.eliminate(p);
  balanced(g);
});
test("BOMB spends letters once and blast hits each entity once", () => {
  const g = game(),
    p = g.players[0],
    bomb = craft(g, p, "BOMB");
  g.time = 5;
  p.x = p.z = 0;
  p.facing = { x: 0, z: 1 };
  g.throw(p);
  assert.equal(g.spent, 4);
  const pr = g.projectiles[0];
  pr.vx = pr.vz = 0;
  pr.x = pr.z = 0;
  const victim = g.players[1];
  victim.x = 0.01;
  victim.z = 0;
  victim.invulnerableUntil = 0;
  g.time = pr.fuseAt;
  g.tickProjectiles(0);
  assert.ok(victim.hp > 54 && victim.hp < 56);
  assert.equal(g.projectiles.length, 0);
  assert.equal(bomb.state, "gone");
  balanced(g);
});
test("SOAP letters stay spent after zone expiry; reusable pads return letters", () => {
  const g = game(),
    p = g.players[0],
    soap = craft(g, p, "SOAP");
  assert.equal(g.deploy(p), null);
  finishAction(g, p);
  assert.equal(g.spent, 4);
  g.time += 8;
  g.tick(0.01);
  assert.equal(soap.state, "gone");
  const bed = craft(g, p, "BED");
  g.deploy(p);
  finishAction(g, p);
  g.breakItem(bed);
  balanced(g);
});
test("all enabled recipe families have an actual usable action", () => {
  for (const definition of data.items.items.filter((i) => i.enabled)) {
    const g = game(),
      p = g.players[0],
      item = craft(g, p, definition.id);
    g.time = 4;
    if (definition.use) assert.equal(g.use(p), true);
    else if (definition.thrown) assert.equal(g.throw(p), true);
    else if (definition.deploy) assert.equal(g.deploy(p), null);
    else if (definition.shield) {
      p.block = true;
      assert.ok(item.definition.shield.frontArc > 0);
    } else assert.equal(g.attack(p), true);
    finishAction(g, p);
    balanced(g);
  }
});
test("clear-out announces before damage, leaves protected center open", () => {
  const g = game(),
    p = g.players[0];
  p.x = 7;
  p.z = 7;
  g.time = 35;
  g.clearOut(0);
  assert.equal(g.roomStatus.get("Kitchen").warning, true);
  assert.equal(p.hp, 100);
  g.time = 45;
  g.clearOut(1);
  assert.equal(g.roomStatus.get("Kitchen").filling, true);
  assert.equal(g.roomStatus.get("Kitchen").closed, false);
  assert.equal(p.hp, 92);
  g.time = 51;
  g.clearOut(0);
  assert.equal(g.roomStatus.get("Kitchen").closed, true);
  assert.equal(g.roomStatus.has("Playroom"), false);
});
test("Dibs reaches a complete match after three round wins", () => {
  const g = game();
  for (let n = 0; n < 3; n++) {
    g.time += 3;
    g.players.slice(1).forEach((p) => g.eliminate(p));
    g.checkWin();
    assert.equal(g.wins[0], n + 1);
    if (n < 2) {
      assert.equal(g.status, "roundOver");
      g.nextRound();
    } else assert.equal(g.status, "finished");
    balanced(g);
  }
});
test("MovingOut requires every configured keepsake and all noneliminated teammates alive at extraction", () => {
  const g = game("MovingOut");
  assert.ok(g.players.every((p) => p.team === 0));
  g.keepsakes.forEach((k) => (k.collected = true));
  g.checkWin();
  assert.equal(g.status, "playing");
  g.players.forEach((p) => {
    p.x = g.extraction.x;
    p.z = g.extraction.z;
  });
  g.checkWin();
  assert.equal(g.status, "finished");
  assert.equal(g.winner, 0);
});
test("MovingDay exact checklist placements do not count against deployed tools", () => {
  const g = game("MovingDay"),
    p = g.players[0];
  for (const goal of g.objectives) {
    craft(g, p, goal.word);
    p.x = goal.x;
    p.z = goal.z;
    p.facing = { x: 0, z: 1 };
    assert.equal(g.deploy(p), null);
    finishAction(g, p);
    assert.equal(goal.done, true);
    balanced(g);
  }
  g.checkWin();
  assert.equal(g.status, "finished");
  assert.equal(g.winner, 0);
});
test("maps use distinct canonical spatial layouts and safe traversable doors", () => {
  for (const map of ["pinwheel", "courtyard"]) {
    const g = game("Dibs", map);
    assert.ok(g.walls.length > 8);
    assert.equal(g.house.rooms.length, 5);
    for (const door of g.house.doors) {
      const perpendicular =
        door.at[0] ===
          g.house.rooms.find((r) => r.name === door.between[0]).bounds[2] ||
        door.at[0] ===
          g.house.rooms.find((r) => r.name === door.between[0]).bounds[0];
      const p = g.players[0];
      p.x = door.at[0] - (perpendicular ? 0.5 : 0);
      p.z = door.at[1] - (perpendicular ? 0 : 0.5);
      const before = { x: p.x, z: p.z };
      g.move(p, perpendicular ? 1 : 0, perpendicular ? 0 : 1);
      assert.ok(distanceSq(p, before) > 0.5);
    }
  }
  function distanceSq(a, b) {
    return (a.x - b.x) ** 2 + (a.z - b.z) ** 2;
  }
});
test("seeded full simulation preserves letters through combat, AI crafting and hazards", () => {
  for (const mode of ["Dibs", "Duos", "MovingOut", "MovingDay"]) {
    const g = game(mode, "courtyard");
    for (let n = 0; n < 5000 && g.status === "playing"; n++) {
      g.tick(0.04, {
        x: Math.sin(n * 0.03),
        z: Math.cos(n * 0.03),
        attack: n % 10 === 0,
        dodge: n % 100 === 0,
      });
      if (n % 100 === 0) balanced(g);
    }
    balanced(g);
  }
});

test("time limit draws; low health does not award a win", () => {
  for (const mode of ["Dibs", "Duos"]) {
    const g = game(mode);
    g.players[0].hp = 1;
    g.time = g.rules.roundTimeLimit;
    g.checkWin();
    assert.equal(g.winner, -1);
    assert.equal(g.status, "roundOver");
    assert.deepEqual(g.wins, [0, 0, 0, 0]);
  }
});
test("a downed crew member outside or inside the van prevents escape", () => {
  const g = game("MovingOut");
  g.keepsakes.forEach((k) => (k.collected = true));
  g.players.forEach((p) => {
    p.x = g.extraction.x;
    p.z = g.extraction.z;
  });
  g.players[1].state = "downed";
  g.checkWin();
  assert.equal(g.status, "playing");
  g.players[1].state = "alive";
  g.checkWin();
  assert.equal(g.status, "finished");
});
test("walls stop melee and thrown projectiles; doors retain line of sight", () => {
  const g = game();
  assert.equal(g.segmentBlocked({ x: 2, z: 3.5 }, { x: 2, z: 4.5 }), true);
  assert.equal(g.segmentBlocked({ x: 0, z: 3.5 }, { x: 0, z: 4.5 }), false);
  const p = g.players[0],
    q = g.players[1];
  p.x = 2;
  p.z = 3.5;
  q.x = 2;
  q.z = 4.5;
  q.invulnerableUntil = 0;
  g.resolveAttack(p, { stats: g.rules.unarmed, dx: 0, dz: 1 });
  assert.equal(q.hp, 100);
});

test("use/deploy channels retain ownership until ready and cancel cleanly on dodge", () => {
  const g = game(),
    p = g.players[0],
    foam = craft(g, p, "FOAM");
  assert.equal(g.use(p), true);
  assert.equal(p.bubble, 0);
  assert.equal(g.spent, 0);
  assert.equal(foam.state, "held");
  g.time = p.action.readyAt - 0.001;
  g.completeAction(p);
  assert.equal(p.bubble, 0);
  g.dodge(p);
  assert.equal(p.action, null);
  assert.equal(foam.state, "held");
  assert.equal(g.spent, 0);
  g.time = p.dodgingUntil;
  g.use(p);
  finishAction(g, p);
  assert.equal(p.bubble, 35);
  assert.equal(g.spent, 4);
  balanced(g);
});
test("dodge refunds a craft reservation and action eligibility respects hit stun", () => {
  const g = game(),
    p = g.players[0];
  give(g, p, "BAT");
  g.craft(p, "BAT");
  assert.equal(p.bag, "");
  g.dodge(p);
  assert.equal(p.craft, null);
  assert.equal(p.bag, "BAT");
  g.time = 4;
  p.stunUntil = 5;
  assert.equal(g.dodge(p), false);
  assert.equal(g.jump(p), false);
  assert.equal(g.attack(p), false);
  assert.match(g.craft(p, "BAT"), /act again/);
  balanced(g);
});
test("changing aim does not steer an already committed dash", () => {
  const g = game(),
    p = g.players[0];
  g.time = 3;
  p.x = p.z = 0;
  p.facing = { x: 1, z: 0 };
  g.dodge(p);
  g.tick(0.05, { aim: { x: 0, z: 5 } });
  assert.ok(p.x > 0.5);
  assert.equal(p.z, 0);
});
test("dropping or swapping a windup weapon cancels its attack; active hits are deduplicated", () => {
  const g = game(),
    p = g.players[0],
    q = g.players[1];
  craft(g, p, "BLADE");
  g.time = 3;
  p.x = p.z = 0;
  q.x = 0;
  q.z = 1;
  q.invulnerableUntil = 0;
  p.facing = { x: 0, z: 1 };
  g.attack(p);
  g.drop(p);
  g.time += 0.29;
  g.tick(0.01);
  assert.equal(q.hp, 100);
  const hit = {
    stats: data.items.items.find((i) => i.id === "BAT").melee,
    dx: 0,
    dz: 1,
  };
  g.resolveAttack(p, hit);
  const hp = q.hp;
  g.resolveAttack(p, hit);
  assert.equal(q.hp, hp);
});
test("broken or picked-up reusable tools cannot leave ghost zones", () => {
  const g = game(),
    p = g.players[0],
    mat = craft(g, p, "MAT");
  g.deploy(p);
  finishAction(g, p);
  assert.equal(g.zones.length, 1);
  g.breakItem(mat);
  assert.equal(g.zones.length, 0);
  p.x = mat.x;
  p.z = mat.z;
  g.tick(0.01, { z: 1 });
  assert.equal(p.speedStrength, undefined);
  const bed = craft(g, p, "BED");
  g.deploy(p);
  finishAction(g, p);
  p.x = bed.x;
  p.z = bed.z;
  g.interact(p);
  assert.equal(g.zones.length, 0);
  assert.equal(bed.state, "held");
  balanced(g);
});
test("MAT only boosts movement along its local arrow", () => {
  const g = game(),
    p = g.players[0],
    mat = craft(g, p, "MAT");
  p.x = p.z = 0;
  p.facing = { x: 0, z: 1 };
  p.yaw = 0;
  g.deploy(p);
  finishAction(g, p);
  p.x = mat.x;
  p.z = mat.z;
  g.tick(0.01, { z: -1 });
  assert.equal(p.speedStrength, undefined);
  p.x = mat.x;
  p.z = mat.z;
  g.tick(0.01, { z: 1 });
  assert.equal(p.speedStrength, 1.6);
  assert.ok(p.speedUntil > g.time);
});
test("BED uses its 11m/s launch strength and never puts the player under the floor", () => {
  const g = game(),
    p = g.players[0],
    bed = craft(g, p, "BED");
  p.x = p.z = 0;
  p.facing = { x: 0, z: 1 };
  g.deploy(p);
  finishAction(g, p);
  p.x = bed.x;
  p.z = bed.z;
  g.tick(0.01);
  assert.equal(p.jumpVelocity, 11);
  assert.ok(p.jumpDuration > 0.9);
  for (let n = 0; n < 90; n++) {
    g.tick(0.01);
    assert.ok(p.y >= 0);
  }
  assert.ok(p.jumpVelocity === 11);
});
test("clear-out has separate warning/filling/closed phases and damage grows with elapsed time", () => {
  const g = game(),
    p = g.players[0];
  p.x = 7;
  p.z = 7;
  g.time = 35;
  g.clearOut(0);
  assert.equal(g.roomStatus.get("Kitchen").warning, true);
  g.time = 45;
  g.clearOut(0);
  assert.equal(g.roomStatus.get("Kitchen").filling, true);
  assert.equal(g.roomStatus.get("Kitchen").damage, 8);
  g.time = 48;
  g.clearOut(0);
  assert.equal(g.roomStatus.get("Kitchen").damage, 14);
  assert.equal(g.roomStatus.get("Kitchen").closed, false);
  g.time = 51;
  g.clearOut(0);
  assert.equal(g.roomStatus.get("Kitchen").closed, true);
  assert.equal(g.roomStatus.get("Kitchen").damage, 20);
});
test("keepsakes require physical carrying and a drop inside the van; they cannot be smashed", () => {
  const g = game("MovingOut"),
    p = g.players[0],
    k = g.keepsakes[0],
    item = g.item(k.itemId);
  p.x = k.x - 0.8;
  p.z = k.z;
  g.interact(p);
  assert.equal(item.state, "carried");
  assert.equal(p.carried, item.id);
  assert.equal(k.collected, false);
  g.damageItem(item, 1000);
  assert.equal(item.state, "carried");
  p.x = g.extraction.x;
  p.z = g.extraction.z;
  p.facing = { x: 0, z: 1 };
  g.tick(0.01);
  assert.equal(k.collected, false);
  g.drop(p);
  g.tick(0.01);
  assert.equal(k.collected, true);
  assert.equal(item.state, "packed");
  balanced(g);
});
test("Moving Day resupplies a spent P and never sends extra parcels mid-craft", () => {
  for (const map of ["pinwheel", "courtyard"]) {
    const g = game("MovingDay", map),
      p = g.players[0];
    g.players.slice(1).forEach((q) => (q.ai = false));
    g.items.forEach((i) => g.breakItem(i));
    for (const char of "SOAP") {
      const tile = g.tiles.find((t) => t.char === char);
      assert.ok(tile);
      g.collect(p, tile);
    }
    assert.equal(g.craft(p, "SOAP"), null);
    g.time = p.craft.readyAt;
    g.completeCraft(p);
    g.deploy(p);
    finishAction(g, p);
    assert.equal(g.spent, 4);
    balanced(g);
    g.time = Math.max(g.time, g.nextResupply);
    g.resupply();
    const parcel = g.items.find(
      (i) => i.delivery && i.word === "LAMP" && i.state === "world",
    );
    assert.ok(parcel, "a real LAMP delivery restores the spent P");
    g.breakItem(parcel);
    for (const char of "LAMP") {
      const tile = g.tiles.find((t) => t.char === char);
      assert.ok(tile);
      g.collect(p, tile);
    }
    assert.equal(canSpell(p.bag, "LAMP"), true);
    assert.equal(g.craft(p, "LAMP"), null);
    const count = g.items.filter((i) => i.word === "LAMP").length;
    g.time = g.nextResupply;
    g.resupply();
    assert.equal(g.items.filter((i) => i.word === "LAMP").length, count);
    balanced(g);
  }
});

test("BALL hits physical cover first and uses BreakPower rather than player damage", () => {
  const g = game(),
    p = g.players[0],
    q = g.players[1];
  g.items = [];
  g.tiles = [];
  g.minted = 0;
  craft(g, p, "BALL");
  p.x = p.z = 0;
  p.facing = { x: 0, z: 1 };
  q.x = 0;
  q.z = 2.5;
  q.invulnerableUntil = 0;
  const cover = g.addItem("TABLE", 0, 1.3);
  g.throw(p);
  for (let n = 0; n < 4; n++) {
    g.time += 0.05;
    g.tickProjectiles(0.05);
  }
  assert.equal(q.hp, 100);
  assert.equal(cover.durability, 119);
  assert.equal(g.projectiles.length, 0);
});
test("helper AI can finish each cooperative mode on both real layouts without teleporting", () => {
  for (const mode of ["MovingDay", "MovingOut"])
    for (const map of ["pinwheel", "courtyard"]) {
      const g = game(mode, map);
      g.players[0].ai = true;
      for (let n = 0; n < 6000 && g.status === "playing"; n++) g.tick(0.04);
      assert.equal(g.status, "finished");
      assert.equal(g.winner, 0, `${mode} ${map}: ${g.result}`);
      balanced(g);
    }
});
test("craft waits through melee recovery, and releasing a weapon cancels that channel", () => {
  const g = game(),
    p = g.players[0];
  give(g, p, "BAT");
  g.time = 3;
  g.attack(p);
  g.time += g.rules.unarmed.windup + g.rules.unarmed.active + 0.001;
  p.pendingAttack = null;
  assert.match(g.craft(p, "BAT"), /current action/);
  g.time = p.meleeUntil;
  assert.equal(g.craft(p, "BAT"), null);
  g.time = p.craft.readyAt;
  g.completeCraft(p);
  g.attack(p);
  g.drop(p);
  assert.equal(p.pendingAttack, null);
  assert.equal(p.meleeUntil, 0);
  balanced(g);
});
