import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { Game } from "../src/engine.js";
const read = (n) =>
  JSON.parse(
    readFileSync(
      new URL(`../../Assets/_Project/Data/Config/${n}.json`, import.meta.url),
    ),
  );
const data = {
  rules: read("rules"),
  items: read("items"),
  wardrobe: read("wardrobe"),
  house: read("house_pinwheel"),
};
data.houses = { pinwheel: data.house, courtyard: read("house_courtyard") };
const layout = {
  schema: 1,
  map: "pinwheel",
  name: "My Cozy House",
  props: [
    { id: "p1", word: "SOFA", x: -6, z: -6, yaw: 90, skin: "Candy" },
    { id: "p2", word: "PLANT", x: -3, z: -8, yaw: 0, skin: "Classic" },
  ],
};
test("peaceful tour uses detached exact decor and one human, without authored furniture or economy", () => {
  const g = new Game(data, { mode: "Tour", layout });
  assert.equal(g.players.length, 1);
  assert.equal(g.players[0].ai, false);
  assert.deepEqual(
    g.items.map((i) => ({
      id: i.designId,
      word: i.word,
      x: i.x,
      z: i.z,
      yaw: (i.rotation * 180) / Math.PI,
      skin: i.skin,
    })),
    layout.props,
  );
  layout.props[0].x = -7;
  assert.equal(g.items[0].x, -6);
  layout.props[0].x = -6;
  assert.equal(g.players[0].bag, "");
  assert.equal(g.minted, 0);
  assert.equal(g.audit().balanced, true);
});
test("tour input walks through the house but cannot damage, summon, collect or activate gear", () => {
  const g = new Game(data, { mode: "Tour", layout }),
    p = g.players[0],
    start = { x: p.x, z: p.z };
  for (let i = 0; i < 200; i++)
    g.tick(0.02, {
      x: 1,
      attack: true,
      interact: true,
      block: true,
      jump: true,
      dodge: true,
    });
  assert.ok(Math.hypot(p.x - start.x, p.z - start.z) > 1);
  g.hit(p, { damage: 999, attacker: -1, blockable: false });
  g.damageItem(g.items[0], 999);
  g.breakItem(g.items[0]);
  assert.equal(p.hp, 100);
  assert.equal(g.items[0].state, "world");
  assert.equal(g.items[0].invulnerable, true);
  assert.equal(g.attack(p), false);
  assert.equal(g.interact(p, 0.05), false);
  assert.notEqual(g.craft(p, "BALL"), null);
  assert.equal(g.drop(p), false);
  assert.equal(g.tossLetter(p, "A"), false);
  g.mintTiles("A", p.x, p.z);
  assert.equal(g.collect(p, g.tiles[0]), false);
  assert.equal(p.bag, "");
  assert.equal(g.zones.length, 0);
  assert.equal(g.projectiles.length, 0);
  assert.equal(g.roomStatus.size, 0);
  assert.equal(g.status, "playing");
});
test("a workshop layout option cannot replace standard match furniture or seed its inventory", () => {
  for (const mode of ["Dibs", "Duos", "MovingDay", "MovingOut"]) {
    const a = new Game(data, { mode, map: "pinwheel", layout }),
      b = new Game(data, { mode, map: "pinwheel" });
    assert.deepEqual(
      a.items.map((i) => [i.word, i.x, i.z, i.origin]),
      b.items.map((i) => [i.word, i.x, i.z, i.origin]),
    );
    assert.equal(a.peaceful, false);
    assert.equal(a.players[0].bag, "");
    assert.equal(
      a.items.some((i) => i.designId),
      false,
    );
  }
});
test("tour collisions use full quarter-rotated decor footprints even for mats and consumables", () => {
  const g = new Game(data, {
      mode: "Tour",
      layout: {
        ...layout,
        props: [
          { id: "p1", word: "TABLE", x: -6, z: -6, yaw: 90, skin: "Classic" },
          { id: "p2", word: "MAT", x: -3, z: -7, yaw: 0, skin: "Classic" },
        ],
      },
    }),
    p = g.players[0];
  p.y = 0;
  assert.equal(g.canStand(p, -6, -6), false);
  assert.equal(g.canStand(p, -3, -7), false);
  assert.equal(g.canStand(p, -7, -8), true);
});

test("portable Tour keeps props and walkers flat at the standard Pinwheel balcony coordinates", () => {
  const g = new Game(data, {
    mode: "Tour",
    layout: {
      ...layout,
      props: [{ id: "p1", word: "BOOK", x: 3, z: 2, yaw: 0, skin: "Classic" }],
    },
  });
  assert.equal(g.floorAt(3, 2), 0);
  assert.equal(g.items[0].y, 0);
  g.players[0].x = 3;
  g.players[0].z = 2;
  g.tick(0.05);
  assert.equal(g.players[0].y, 0);
  assert.equal(
    new Game(data, { mode: "Dibs", map: "pinwheel" }).floorAt(3, 2),
    1.7,
  );
});
