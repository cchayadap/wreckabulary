export const MODES = ["Dibs", "Duos", "MovingOut", "MovingDay"];
export const TOP_SIZE = 50;
export const MAX_SCORE = 100000;
const RATE_SECONDS = 20;

const json = (body, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
    },
  });

export function cleanEntry(body) {
  if (!body || typeof body !== "object") return { error: "Send a JSON body." };
  const name = String(body.name ?? "")
    .replace(/[^A-Za-z0-9 _-]/g, "")
    .trim()
    .slice(0, 16);
  if (name.length < 2) return { error: "Names need 2 to 16 letters or digits." };
  if (!MODES.includes(body.mode)) return { error: "Unknown mode." };
  const score = Math.floor(Number(body.score));
  if (!Number.isFinite(score) || score < 0 || score > MAX_SCORE)
    return { error: "That score is out of range." };
  const player = String(body.player ?? "").replace(/[^a-z0-9]/gi, "").slice(0, 32);
  if (player.length < 8) return { error: "Missing player id." };
  return { entry: { name, mode: body.mode, score, player } };
}

export function mergeTop(top, entry, at = Date.now()) {
  const list = top.filter((e) => e.player !== entry.player);
  const previous = top.find((e) => e.player === entry.player);
  const best =
    previous && previous.score >= entry.score
      ? previous
      : { name: entry.name, score: entry.score, player: entry.player, at };
  if (previous && previous.score >= entry.score) best.name = entry.name;
  list.push(best);
  list.sort((a, b) => b.score - a.score || a.at - b.at);
  return list.slice(0, TOP_SIZE);
}

export const publicRows = (top, player = "") =>
  top.map((e, i) => ({
    rank: i + 1,
    name: e.name,
    score: e.score,
    you: !!player && e.player === player,
  }));

export async function handleScores(request, env) {
  const store = env.LEADERBOARD;
  if (!store) return json({ error: "The online leaderboard is not set up yet." }, 503);
  const url = new URL(request.url);
  if (request.method === "GET") {
    const mode = url.searchParams.get("mode") ?? "Dibs";
    if (!MODES.includes(mode)) return json({ error: "Unknown mode." }, 400);
    const top = (await store.get(`top:${mode}`, "json")) ?? [];
    return json({ mode, rows: publicRows(top, url.searchParams.get("player") ?? "") });
  }
  if (request.method === "POST") {
    let body;
    try {
      body = await request.json();
    } catch {
      return json({ error: "Send a JSON body." }, 400);
    }
    const { entry, error } = cleanEntry(body);
    if (error) return json({ error }, 400);
    const ip = request.headers.get("cf-connecting-ip") ?? "local";
    const rateKey = `rate:${entry.player}:${ip}`;
    if (await store.get(rateKey)) return json({ error: "Slow down a little." }, 429);
    await store.put(rateKey, "1", { expirationTtl: Math.max(60, RATE_SECONDS) });
    const top = mergeTop((await store.get(`top:${entry.mode}`, "json")) ?? [], entry);
    await store.put(`top:${entry.mode}`, JSON.stringify(top));
    const rank = top.findIndex((e) => e.player === entry.player) + 1;
    return json({ ok: true, rank: rank || null, rows: publicRows(top, entry.player) });
  }
  return json({ error: "Use GET or POST." }, 405);
}

export default {
  fetch(request, env) {
    if (new URL(request.url).pathname === "/api/scores") return handleScores(request, env);
    return env.ASSETS.fetch(request);
  },
};
