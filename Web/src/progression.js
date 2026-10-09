export const RANKED_MODES = ["Dibs", "Duos", "MovingOut", "MovingDay"];

export const FREE_COLOURS = ["pool", "tomato", "tangerine", "sunflower", "mint"];
export const SHOP = [
  { id: "skin:Candy", kind: "skin", value: "Candy", price: 250, name: "Candy gear" },
  { id: "skin:Arcade", kind: "skin", value: "Arcade", price: 400, name: "Arcade gear" },
  { id: "colour:sky", kind: "colour", value: "sky", price: 120, name: "Sky" },
  { id: "colour:periwinkle", kind: "colour", value: "periwinkle", price: 120, name: "Periwinkle" },
  { id: "colour:grape", kind: "colour", value: "grape", price: 150, name: "Grape" },
  { id: "colour:bubblegum", kind: "colour", value: "bubblegum", price: 150, name: "Bubblegum" },
  { id: "colour:oat", kind: "colour", value: "oat", price: 100, name: "Oat" },
  { id: "colour:charcoal", kind: "colour", value: "charcoal", price: 180, name: "Charcoal" },
];

export function newProgress(random = Math.random) {
  const id = Array.from({ length: 16 }, () => Math.floor(random() * 36).toString(36)).join("");
  return {
    name: `Housemate${id.slice(0, 4).toUpperCase()}`,
    player: id,
    coins: 0,
    owned: [],
    bests: {},
  };
}

export function loadProgress(saved, inUse = {}, random = Math.random) {
  const base = newProgress(random);
  const p = saved && typeof saved === "object" ? saved : {};
  const progress = {
    name: typeof p.name === "string" && p.name.trim().length >= 2 ? p.name.slice(0, 16) : base.name,
    player: typeof p.player === "string" && p.player.length >= 8 ? p.player : base.player,
    coins: Number.isFinite(p.coins) && p.coins >= 0 ? Math.floor(p.coins) : 0,
    owned: Array.isArray(p.owned) ? p.owned.filter((id) => SHOP.some((s) => s.id === id)) : [],
    bests: p.bests && typeof p.bests === "object" ? { ...p.bests } : {},
  };
  if (inUse.skin && inUse.skin !== "Classic") grant(progress, `skin:${inUse.skin}`);
  if (inUse.colour && !FREE_COLOURS.includes(inUse.colour)) grant(progress, `colour:${inUse.colour}`);
  return progress;
}

function grant(progress, id) {
  if (SHOP.some((s) => s.id === id) && !progress.owned.includes(id)) progress.owned.push(id);
}

export const owns = (progress, kind, value) =>
  (kind === "skin" && value === "Classic") ||
  (kind === "colour" && FREE_COLOURS.includes(value)) ||
  progress.owned.includes(`${kind}:${value}`);

export function buy(progress, id) {
  const item = SHOP.find((s) => s.id === id);
  if (!item) return { ok: false, error: "That isn't in the shop." };
  if (progress.owned.includes(id)) return { ok: false, error: "You already own it." };
  if (progress.coins < item.price)
    return { ok: false, error: `You need ${item.price - progress.coins} more coins.` };
  progress.coins -= item.price;
  progress.owned.push(id);
  return { ok: true, item };
}

export function matchReward(stats, won) {
  const score = Math.max(
    0,
    Math.round(
      (stats.broken ?? 0) * 10 + (stats.crafted ?? 0) * 25 + (stats.damage ?? 0) + (won ? 150 : 0),
    ),
  );
  return { score, coins: Math.floor(score / 10) + (won ? 20 : 5) };
}

export function recordMatch(progress, mode, reward) {
  progress.coins += reward.coins;
  const best = progress.bests[mode] ?? 0;
  if (reward.score > best) {
    progress.bests[mode] = reward.score;
    return true;
  }
  return false;
}
