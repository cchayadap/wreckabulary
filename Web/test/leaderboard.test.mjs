import test from "node:test";
import assert from "node:assert/strict";
import worker, { cleanEntry, mergeTop, handleScores, TOP_SIZE } from "../worker.mjs";

function memoryKv() {
  const map = new Map();
  return {
    map,
    async get(key, type) {
      const value = map.get(key);
      if (value === undefined) return null;
      return type === "json" ? JSON.parse(value) : value;
    },
    async put(key, value) {
      map.set(key, value);
    },
  };
}
const post = (body, ip = "1.1.1.1") =>
  new Request("https://w.test/api/scores", {
    method: "POST",
    headers: { "content-type": "application/json", "cf-connecting-ip": ip },
    body: JSON.stringify(body),
  });

test("score entries are cleaned and range-checked", () => {
  assert.deepEqual(
    cleanEntry({ name: " Pip<script> ", mode: "Dibs", score: 120.9, player: "abcdefgh12" }).entry,
    { name: "Pipscript", mode: "Dibs", score: 120, player: "abcdefgh12" },
  );
  assert.match(cleanEntry({ name: "x", mode: "Dibs", score: 1, player: "abcdefgh" }).error, /Names/);
  assert.match(cleanEntry({ name: "Pip", mode: "Chess", score: 1, player: "abcdefgh" }).error, /mode/);
  assert.match(cleanEntry({ name: "Pip", mode: "Dibs", score: -5, player: "abcdefgh" }).error, /range/);
  assert.match(cleanEntry({ name: "Pip", mode: "Dibs", score: 1e9, player: "abcdefgh" }).error, /range/);
  assert.match(cleanEntry({ name: "Pip", mode: "Dibs", score: 5, player: "x" }).error, /player/);
});

test("the top list keeps one best score per player, sorted and capped", () => {
  let top = [];
  top = mergeTop(top, { name: "A", score: 50, player: "p1" }, 1);
  top = mergeTop(top, { name: "B", score: 80, player: "p2" }, 2);
  top = mergeTop(top, { name: "A2", score: 30, player: "p1" }, 3);
  assert.deepEqual(top.map((e) => [e.player, e.score, e.name]), [["p2", 80, "B"], ["p1", 50, "A2"]]);
  top = mergeTop(top, { name: "A3", score: 90, player: "p1" }, 4);
  assert.equal(top[0].player, "p1");
  for (let n = 0; n < TOP_SIZE + 10; n++) top = mergeTop(top, { name: "Z", score: n, player: `z${n}` }, 10 + n);
  assert.equal(top.length, TOP_SIZE);
});

test("the API stores scores, hides player ids, rate-limits and reports missing storage", async () => {
  const kv = memoryKv(),
    env = { LEADERBOARD: kv };
  const first = await (await handleScores(post({ name: "Pip", mode: "Dibs", score: 70, player: "player0001" }), env)).json();
  assert.equal(first.rank, 1);
  const again = await handleScores(post({ name: "Pip", mode: "Dibs", score: 90, player: "player0001" }), env);
  assert.equal(again.status, 429);
  await handleScores(post({ name: "Miso", mode: "Dibs", score: 120, player: "player0002" }, "2.2.2.2"), env);
  const board = await (
    await handleScores(new Request("https://w.test/api/scores?mode=Dibs&player=player0001"), env)
  ).json();
  assert.deepEqual(board.rows, [
    { rank: 1, name: "Miso", score: 120, you: false },
    { rank: 2, name: "Pip", score: 70, you: true },
  ]);
  assert.ok(!JSON.stringify(board).includes("player0002"));
  const missing = await worker.fetch(new Request("https://w.test/api/scores"), {});
  assert.equal(missing.status, 503);
});
