import test from "node:test";
import assert from "node:assert/strict";
import { newProgress, loadProgress, owns, buy, matchReward, recordMatch } from "../src/progression.js";

const fixed = () => 0.5;

test("a new record has a name, a private id and no coins", () => {
  const p = newProgress(fixed);
  assert.equal(p.coins, 0);
  assert.ok(p.player.length >= 8);
  assert.match(p.name, /^Housemate/);
});

test("saved looks from before the shop stay unlocked; junk is repaired", () => {
  const p = loadProgress({ coins: -4, owned: ["colour:sky", "bogus"], name: "x" }, { skin: "Candy", colour: "grape" }, fixed);
  assert.equal(p.coins, 0);
  assert.deepEqual(p.owned.sort(), ["colour:grape", "colour:sky", "skin:Candy"]);
  assert.match(p.name, /^Housemate/);
  assert.ok(owns(p, "skin", "Classic") && owns(p, "colour", "tomato"));
  assert.ok(!owns(p, "skin", "Arcade"));
});

test("buying spends coins once and refuses when short", () => {
  const p = newProgress(fixed);
  assert.match(buy(p, "skin:Candy").error, /250 more/);
  p.coins = 300;
  assert.equal(buy(p, "skin:Candy").ok, true);
  assert.equal(p.coins, 50);
  assert.match(buy(p, "skin:Candy").error, /already/);
  assert.ok(owns(p, "skin", "Candy"));
});

test("match rewards follow the stats and only better scores become bests", () => {
  const win = matchReward({ broken: 3, crafted: 2, damage: 41.6 }, true);
  assert.deepEqual(win, { score: 30 + 50 + 42 + 150, coins: 27 + 20 });
  const p = newProgress(fixed);
  assert.equal(recordMatch(p, "Dibs", win), true);
  assert.equal(recordMatch(p, "Dibs", matchReward({ broken: 1 }, false)), false);
  assert.equal(p.bests.Dibs, 272);
  assert.equal(p.coins, 47 + 6);
});
