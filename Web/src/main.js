import "./style.css";
import { recipeDescription, itemActionLabel } from "./item-presentation.js";
import { Game, MODES, MAPS, canSpell, distance } from "./engine.js";
import { WorldView } from "./renderer.js";
import {
  SHOP,
  RANKED_MODES,
  loadProgress,
  owns,
  buy,
  matchReward,
  recordMatch,
} from "./progression.js";
import {
  createLayout,
  validateLayout,
  placementCheck,
  addWords,
  importLayout,
  exportLayout,
} from "./home-design.js";
const ui = document.querySelector("#ui"),
  announcer = document.querySelector("#announcer");
const fetchData = async (name) =>
  fetch(new URL(`data/${name}.json`, document.baseURI)).then((r) => {
    if (!r.ok) throw new Error(`Missing game data: ${name}`);
    return r.json();
  });
let game,
  view,
  data,
  mode = "Dibs",
  map = "pinwheel",
  screen = "loading",
  craftOpen = false,
  bagOpen = false,
  lastComposerKey = "",
  paused = false,
  muted = false,
  last = performance.now(),
  hudAt = 0,
  profile,
  progress,
  lobbyTab = "play",
  lobbyPicker = null,
  boardMode = "Dibs",
  skin = "Classic";
const input = {
  x: 0,
  z: 0,
  aim: null,
  attack: false,
  dodge: false,
  jump: false,
  block: false,
  interact: false,
};
const keys = new Set(),
  touch = { x: 0, y: 0 };
let pointerAim = null,
  soundContext;
const esc = (s) =>
  String(s).replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
const iconHtml = (word, cls = "") =>
  `<img class="item-image ${cls}" data-icon="${word}" alt="${word.toLowerCase()}" src="${view?.icons.get(word) ?? view?.manifest.items[word]?.icon ?? ""}">`;
const ACTION_CROPS = {
  smash: [34, 19, 391, 376],
  dodge: [453, 100, 369, 286],
  jump: [925, 28, 280, 373],
  craft: [39, 445, 365, 336],
  block: [499, 434, 313, 353],
  interact: [874, 439, 333, 346],
  drop: [487, 813, 281, 405],
};
function actionArt(name, size = 32) {
  const [x, y, w, h] = ACTION_CROPS[name],
    scale = size / Math.max(w, h);
  return `<span class="action-art" role="img" aria-label="${name}" style="width:${w * scale}px;height:${h * scale}px;background-size:${1254 * scale}px ${1254 * scale}px;background-position:${-x * scale}px ${-y * scale}px"></span>`;
}
function sound(type) {
  if (muted) return;
  try {
    soundContext ??= new (window.AudioContext || window.webkitAudioContext)();
    if (soundContext.state === "suspended") soundContext.resume();
    const now = soundContext.currentTime,
      o = soundContext.createOscillator(),
      g = soundContext.createGain();
    o.connect(g).connect(soundContext.destination);
    const tones = {
      pickup: [600, 1100, 0.06],
      break: [120, 55, 0.13],
      hit: [190, 75, 0.07],
      craft: [450, 900, 0.21],
      dodge: [250, 650, 0.09],
      jump: [320, 750, 0.12],
      buff: [450, 1200, 0.25],
      explosion: [80, 30, 0.3],
      objective: [500, 1200, 0.3],
      keepsake: [450, 1000, 0.25],
      block: [450, 140, 0.1],
      swing: [180, 70, 0.05],
      result: [400, 800, 0.4],
      refused: [220, 150, 0.08],
    };
    const [a, b, d] = tones[type] ?? [230, 400, 0.08];
    o.type = ["break", "hit", "explosion"].includes(type) ? "triangle" : "sine";
    o.frequency.setValueAtTime(a, now);
    o.frequency.exponentialRampToValueAtTime(b, now + d);
    g.gain.setValueAtTime(0.055, now);
    g.gain.exponentialRampToValueAtTime(0.001, now + d);
    o.start(now);
    o.stop(now + d);
  } catch {}
}
function toast(text) {
  let node = document.querySelector("#toast");
  if (!node) {
    node = document.createElement("div");
    node.id = "toast";
    ui.append(node);
  }
  node.textContent = text;
  node.classList.add("show");
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => node.classList.remove("show"), 2500);
  announcer.textContent = text;
}
function saveProfile() {
  localStorage.setItem(
    "wreckabulary.profile.v1",
    JSON.stringify({ wardrobe: profile, skin, muted }),
  );
}
function resetInputs() {
  keys.clear();
  touch.x = touch.y = 0;
  for (const k of ["attack", "dodge", "jump", "block", "interact"])
    input[k] = false;
  input.x = input.z = 0;
  input.aim = null;
  pointerAim = null;
  if (bagOpen) setBag(false);
}
const MOUSE_SENSITIVITY = 0.0024;
const worldCanvas = () => document.querySelector("#world");
const mouseLocked = () => document.pointerLockElement === worldCanvas();
let lockRefusals = 0;
let lockPending = false;
function refuseLock() {
  if (!lockPending) return;
  lockPending = false;
  lockRefusals++;
}
document.addEventListener("pointerlockerror", refuseLock);
function lockMouse() {
  if (matchMedia("(pointer: coarse)").matches || mouseLocked() || navigator.webdriver)
    return false;
  lockPending = true;
  try {
    worldCanvas().requestPointerLock()?.catch?.(refuseLock);
  } catch {
    refuseLock();
  }
  return lockRefusals < 2;
}
function releaseMouse() {
  if (mouseLocked()) document.exitPointerLock();
}
function fullscreenLandscape() {
  if (!matchMedia("(pointer: coarse)").matches || navigator.webdriver) return;
  const root = document.documentElement;
  if (document.fullscreenElement || !root.requestFullscreen) return;
  root
    .requestFullscreen({ navigationUI: "hide" })
    .then(() => window.screen.orientation?.lock?.("landscape"))
    .catch(() => {});
}
function resetLook() {
  const p = game.players[0];
  view.look.yaw = p.yaw ?? Math.atan2(p.facing?.x ?? 0, p.facing?.z ?? 1);
  view.look.pitch = 0.16;
}
function turnLook(dx, dy) {
  view.look.yaw -= dx;
  view.look.pitch = Math.min(0.95, Math.max(-0.45, view.look.pitch + dy));
}
function start() {
  view.setWorkshop?.({ enabled: false, selectedId: null, ghost: null });
  resetInputs();
  craftOpen = false;
  paused = false;
  game = new Game(data, {
    mode,
    map,
    wardrobe: structuredClone(profile),
    skin,
  });
  screen = "game";
  view.rebuild(game);
  resetLook();
  renderHud();
  lockMouse();
  fullscreenLandscape();
  toast(
    mode === "MovingOut"
      ? `Carry ${game.keepsakes.length} keepsakes to the van. Drop them inside the circle.`
      : "Break furniture → collect its letters → spell new gear.",
  );
  sound("craft");
}
const LOBBY_TABS = [
  ["play", "lobbyPlay", "Play"],
  ["locker", "closet", "Locker"],
  ["shop", "shop", "Shop"],
  ["recipes", "lobbyRecipes", "Recipes"],
  ["leaderboard", "leaderboard", "Leaderboard"],
];
const ICON_PATHS = {
  play: '<path d="M8 5.5l11 6.5-11 6.5z" fill="currentColor" stroke="none"/>',
  locker: '<path d="M10 6.5a2 2 0 1 1 2.6 1.9c-.4.2-.6.6-.6 1V10L3.5 16.5c-.6.5-.3 1.5.5 1.5h16c.8 0 1.1-1 .5-1.5L12 10"/>',
  shop: '<path d="M5 8.5h14l-1.2 11.5H6.2zM9 8.5V7a3 3 0 0 1 6 0v1.5"/>',
  recipes: '<path d="M3.5 5.5H10a2 2 0 0 1 2 2V20a2 2 0 0 0-2-2H3.5zM20.5 5.5H14a2 2 0 0 0-2 2V20a2 2 0 0 1 2-2h6.5z"/>',
  leaderboard: '<path d="M7.5 4h9v4.5a4.5 4.5 0 0 1-9 0zM7.5 6H4.5a3 3 0 0 0 3.3 3.8M16.5 6h3a3 3 0 0 1-3.3 3.8M12 13v3.5M8 20h8l-1-3.5H9z"/>',
  crown: '<path d="M3.5 8.5l4.2 3.8L12 6l4.3 6.3 4.2-3.8-1.8 9.5H5.3z" fill="currentColor"/>',
  pencil: '<path d="M4.5 19.5l1-4.2L15.8 5a1.8 1.8 0 0 1 2.6 0l.6.6a1.8 1.8 0 0 1 0 2.6L8.7 18.5z"/>',
  hammer: '<path d="M13 4.5l6.5 6.5-2.6 2.6L10.4 7zM11.8 8.4L3.6 16.6a1.8 1.8 0 0 0 0 2.6l1.2 1.2a1.8 1.8 0 0 0 2.6 0l8.2-8.2"/>',
  soundOn: '<path d="M4 9.5h3.5L12 5.5v13l-4.5-4H4z" fill="currentColor"/><path d="M15.5 9a4 4 0 0 1 0 6M18 6.5a7.5 7.5 0 0 1 0 11"/>',
  soundOff: '<path d="M4 9.5h3.5L12 5.5v13l-4.5-4H4z" fill="currentColor"/><path d="M15.5 9.5l5 5M20.5 9.5l-5 5"/>',
  help: '<path d="M9.2 9.2a2.9 2.9 0 1 1 4 2.7c-.8.4-1.2 1-1.2 1.9v.7"/><circle cx="12" cy="17.6" r=".6" fill="currentColor"/>',
  lock: '<path d="M7.5 11V8.5a4.5 4.5 0 0 1 9 0V11"/><rect x="5" y="11" width="14" height="9.5" rx="2.5" fill="currentColor"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
  glasses: '<circle cx="7" cy="13" r="3.5"/><circle cx="17" cy="13" r="3.5"/><path d="M10.5 13h3M3.5 12l-1-3M20.5 12l1-3"/>',
  satchel: '<path d="M4.5 10h15v9.5h-15zM8 10V8a4 4 0 0 1 8 0v2M4.5 13.5h15"/><rect x="10.5" y="12.5" width="3" height="2.5" rx=".6" fill="currentColor"/>',
  badge: '<rect x="4.5" y="4.5" width="15" height="15" rx="3.5"/><path d="M8.5 9h7M12 9v7"/>',
  arrow: '<path d="M5 12h13M13 6.5l5.5 5.5-5.5 5.5"/>',
  party: '<circle cx="9" cy="8" r="3.2" fill="currentColor"/><path d="M2.8 19.5a6.2 6.2 0 0 1 12.4 0z" fill="currentColor"/><circle cx="17" cy="9" r="2.5"/><path d="M16.5 14a5 5 0 0 1 5 5.5"/>',
  house: '<path d="M3.5 11.5L12 4l8.5 7.5"/><path d="M5.5 10v10h13V10"/><path d="M10 20v-5.5h4V20"/>',
  close: '<path d="M6.5 6.5l11 11M17.5 6.5l-11 11"/>',
};
const icon = (name, cls = "") =>
  `<svg class="ico ${cls}" viewBox="0 0 24 24" aria-hidden="true">${ICON_PATHS[name]}</svg>`;
const COIN_ICON =
  '<svg class="coin" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10.2"/><circle class="coin-rim" cx="12" cy="12" r="7.2"/><path class="coin-w" d="M7.6 9.2l1.6 5.8 1.6-4.1 1.2 0 1.6 4.1 1.6-5.8"/></svg>';
const MODE_ART = {
  Dibs: ["BAT", "#ef5b2b"],
  Duos: ["BALL", "#3fa9dd"],
  MovingOut: ["BOX", "#6fa957"],
  MovingDay: ["SOFA", "#9471dc"],
  Tutorial: ["BOOK", "#f2b230"],
};
const HOUSE_PLANS = {
  pinwheel:
    '<rect x="8" y="8" width="56" height="30" rx="4"/><rect x="64" y="8" width="28" height="56" rx="4"/><rect x="36" y="64" width="56" height="28" rx="4"/><rect x="8" y="38" width="28" height="54" rx="4"/><rect class="plan-hall" x="36" y="38" width="28" height="26" rx="3"/><circle class="plan-dot" cx="22" cy="22" r="4"/><circle class="plan-dot" cx="78" cy="48" r="4"/><circle class="plan-dot" cx="64" cy="78" r="4"/><circle class="plan-dot" cx="22" cy="66" r="4"/>',
  courtyard:
    '<rect x="8" y="8" width="84" height="84" rx="6"/><rect class="plan-garden" x="30" y="30" width="40" height="40" rx="4"/><circle class="plan-tree" cx="50" cy="50" r="9"/><path d="M30 8v22M70 8v22M30 70v22M70 70v22M8 30h22M8 70h22M70 30h22M70 70h22"/><circle class="plan-dot" cx="19" cy="19" r="4"/><circle class="plan-dot" cx="81" cy="81" r="4"/>',
};
const housePlan = (id) =>
  HOUSE_PLANS[id]
    ? `<svg class="plan" viewBox="0 0 100 100" aria-hidden="true">${HOUSE_PLANS[id]}</svg>`
    : icon("house");
const paletteCss = (id) => {
  const c = data.wardrobe.palettes.Top.find((p) => p.id === id);
  return c ? `rgb(${c.rgb.map((n) => Math.round(n * 255)).join(",")})` : "#e9b46a";
};
function home(tab = "play") {
  view.setWorkshop?.({ enabled: false, selectedId: null, ghost: null });
  resetInputs();
  releaseMouse();
  const entering =
    !["home", "closet"].includes(screen) ||
    !game ||
    game.status !== "preview" ||
    game.mode === "Tour" ||
    game.map.id !== map;
  const switched = lobbyTab !== tab || entering;
  lobbyTab = tab;
  if (tab !== "play") lobbyPicker = null;
  screen = tab === "locker" ? "closet" : "home";
  craftOpen = false;
  paused = false;
  if (entering) {
    game = new Game(data, { preview: true, map, wardrobe: profile });
    view.rebuild(game);
  }
  view.closet = false;
  stageLobby(tab);
  ui.innerHTML = `<main class="lobby lobby-${tab} ${switched ? "lobby-enter" : ""}">
<header class="lobby-top">
  <div class="lobby-logo" aria-label="Wreckabulary"><b>WRECKABULARY</b></div>
  <nav class="lobby-tabs" aria-label="Lobby">${LOBBY_TABS.map(([id, action, label]) => `<button class="lobby-tab ${id === tab ? "on" : ""}" data-action="${action}" aria-current="${id === tab ? "page" : "false"}"><span>${icon(id)}${label}</span></button>`).join("")}</nav>
  <div class="lobby-meta">
    <span class="meta-chip coins" title="Coins">${COIN_ICON}<b id="coin-count">${progress.coins.toLocaleString()}</b></span>
    <span class="meta-chip party-count" title="Housemates">${icon("party")}<b>${game.players.length}</b></span>
    <button class="round-button" data-action="sound" aria-label="${muted ? "Sound off" : "Sound on"}" title="${muted ? "Sound off" : "Sound on"}">${icon(muted ? "soundOff" : "soundOn")}</button>
    <button class="round-button" data-action="how" aria-label="How to play" title="How to play">${icon("help")}</button>
  </div>
</header>
${tab === "play" ? lobbyStageUi() : `<section class="lobby-panel" aria-label="${LOBBY_TABS.find(([id]) => id === tab)[2]}">${lobbyPanel(tab)}</section>`}
</main>`;
  bindUi();
  bindLobby(tab);
  placeLobbyTags();
}
function lobbyStageUi() {
  const [art, colour] = MODE_ART[mode] ?? ["BOX", "#ffd21f"],
    house = MAPS.find((m) => m.id === map) ?? MAPS[0];
  const tags = game.players
    .map((p, i) =>
      i === 0
        ? `<div class="stage-tag you" data-tag="0"><span class="tag-crown">${icon("crown")}</span><label class="tag-name"><input id="player-name" maxlength="16" value="${esc(progress.name)}" aria-label="Your name" spellcheck="false">${icon("pencil", "tag-edit")}</label><span class="tag-status leader">Party leader</span></div>`
        : `<div class="stage-tag" data-tag="${i}"><span class="tag-name"><strong>${esc(p.name)}</strong></span><span class="tag-status ready">Ready</span></div>`,
    )
    .join("");
  return `<div class="stage-tags" aria-label="Party">${tags}</div>
<footer class="lobby-dock">
  <button class="workshop-sign" data-action="workshop">${icon("hammer")}<span>Creative<br>Workshop</span></button>
  <div class="match-dock">
    <button class="dock-pick dock-mode" data-action="modes" style="--mode:${colour}" aria-label="Game mode: ${esc(MODES[mode].title)}. Change mode">
      <span class="dock-art">${iconHtml(art)}</span>
      <span class="dock-text"><small>Game mode</small><strong>${MODES[mode].title}</strong><em>${MODES[mode].tag}</em></span>
      <span class="dock-change">Change</span>
    </button>
    <button class="dock-pick dock-map" data-action="maps" aria-label="House: ${esc(house.name)}. Change house">
      ${icon("house", "dock-house")}
      <span class="dock-text"><small>House</small><b>${esc(house.name)}</b></span>
      <span class="dock-change">Change</span>
    </button>
    <button class="play-button start" data-action="start" aria-label="Play ${esc(MODES[mode].title)}"><span class="play-tiles" aria-hidden="true">${[..."PLAY"].map((c, i) => `<i style="--i:${i}">${c}</i>`).join("")}</span>${icon("arrow", "play-arrow")}</button>
  </div>
</footer>
${lobbyPicker ? lobbyPickerUi() : ""}`;
}
function lobbyPickerUi() {
  const body =
    lobbyPicker === "mode"
      ? `<div class="picker-modes">${Object.entries(MODES)
          .map(([id, m]) => {
            const [art, colour] = MODE_ART[id] ?? ["BOX", "#ffd21f"];
            return `<button class="mode-poster ${id === mode ? "selected" : ""}" data-mode="${id}" aria-pressed="${id === mode}" style="--mode:${colour}"><span class="poster-art">${iconHtml(art)}</span><strong>${m.title}</strong><em>${m.tag}</em><span class="poster-blurb">${m.description}</span>${id === mode ? `<span class="poster-check">${icon("check")}</span>` : ""}</button>`;
          })
          .join("")}</div>`
      : `<div class="picker-houses">${MAPS.map(
          (m, i) =>
            `<button class="house-card ${m.id === map ? "selected" : ""}" data-map="${m.id}" aria-pressed="${m.id === map}" style="--mode:${["#ff5a1f", "#7be03a"][i % 2]}"><span class="house-art">${housePlan(m.id)}</span><strong>${esc(m.name)}</strong><span class="poster-blurb">${esc(m.subtitle ?? "")}</span>${m.id === map ? `<span class="poster-check">${icon("check")}</span>` : ""}</button>`,
        ).join("")}</div>`;
  return `<div class="picker-shade" id="picker-shade"><section class="picker" role="dialog" aria-modal="true" aria-label="${lobbyPicker === "mode" ? "Choose a mode" : "Choose a house"}"><header class="picker-head"><h2>${lobbyPicker === "mode" ? "Pick your chaos" : "Pick a house"}</h2><button class="round-button" data-action="closePicker" aria-label="Close">${icon("close")}</button></header>${body}</section></div>`;
}
const panelHead = (title, sub) =>
  `<header class="panel-head"><h2>${title}</h2>${sub ? `<p>${sub}</p>` : ""}</header>`;
function lobbyPanel(tab) {
  if (tab === "locker") return lockerPanel();
  if (tab === "shop")
    return `${panelHead("Item shop", "Looks only. Never stats.")}<div class="shop-grid">${SHOP.map((item) => {
      const owned = owns(progress, item.kind, item.value),
        short = progress.coins < item.price,
        art =
          item.kind === "colour"
            ? `<span class="paint-blob" style="--paint:${paletteCss(item.value)}"></span>`
            : `<span class="skin-art skin-${item.value.toLowerCase()}">${iconHtml("BAT")}</span>`;
      return `<div class="shop-item ${owned ? "owned" : ""} ${item.kind}" data-item="${item.id}"><div class="shop-art">${art}</div><strong>${item.name}</strong><small>${item.kind === "skin" ? "Crafted gear style" : "Hoodie colour"}</small>${owned ? `<span class="owned-stamp">Owned</span>` : `<button class="price-tag ${short ? "short" : ""}" data-buy="${item.id}" ${short ? "disabled" : ""} aria-label="Buy ${item.name} for ${item.price} coins">${COIN_ICON}<b>${item.price}</b></button>`}</div>`;
    }).join("")}</div><p class="panel-foot">${COIN_ICON} Earn coins in matches: smash, spell and win for more.</p>`;
  if (tab === "recipes") {
    const count = data.items.items.filter((i) => i.enabled).length;
    return `${panelHead("Recipe book", `${count} words you can spell. In a match press <kbd>Q</kbd> and type one.`)}<div class="recipe-grid lobby-recipes">${recipeBook("")}</div>`;
  }
  return `${panelHead("Leaderboards", "Best single-match score, worldwide.")}<div class="board-modes" role="tablist">${RANKED_MODES.map((m) => `<button class="board-chip ${m === boardMode ? "on" : ""}" data-board="${m}" role="tab" aria-selected="${m === boardMode}" style="--mode:${MODE_ART[m][1]}">${iconHtml(MODE_ART[m][0])}<span>${MODES[m].title}</span></button>`).join("")}</div><ol class="board" id="board"><li class="board-note">Loading…</li></ol><p class="board-best">${icon("leaderboard")}<span>Your best</span><b>${(progress.bests[boardMode] ?? 0).toLocaleString()}</b></p>`;
}
function lockerPanel() {
  return `${panelHead("Locker", "All style. Zero stats.")}<div class="locker-row"><label class="wood-select">Top<select id="outfit-top">${data.wardrobe.pieces
    .filter((p) => p.slot === "Top")
    .map((p) => `<option ${profile.pieces.Top === p.id ? "selected" : ""}>${p.id}</option>`)
    .join("")}</select></label><label class="wood-select">Headwear<select id="outfit-head"><option value="">Bare head</option>${data.wardrobe.pieces
    .filter((p) => p.slot === "Headwear")
    .map((p) => `<option ${profile.pieces.Headwear === p.id ? "selected" : ""}>${p.id}</option>`)
    .join("")}</select></label></div><h3 class="locker-label">Signature colour</h3><div class="swatches paint-swatches">${data.wardrobe.palettes.Top.map((c) => {
    const owned = owns(progress, "colour", c.id),
      price = SHOP.find((s) => s.id === `colour:${c.id}`)?.price;
    return `<button data-color="${c.id}" class="swatch ${profile.colours.Top === c.id ? "selected" : ""} ${owned ? "" : "locked"}" style="--paint:${paletteCss(c.id)}" title="${c.name}${owned ? "" : ` · ${price} coins in the shop`}" aria-label="${c.name}${owned ? "" : ", locked"}">${owned ? "" : icon("lock")}</button>`;
  }).join("")}</div><h3 class="locker-label">Extras</h3><div class="accessories extra-toggles">${[
    ["Glasses", "Face", "glasses", "Glasses"],
    ["Satchel", "Back", "satchel", "Satchel"],
    ["TBadge", "Badge", "badge", "Letter badge"],
  ]
    .map(
      ([id, slot, glyphName, label]) =>
        `<label class="extra"><input type="checkbox" data-piece="${id}" data-slot="${slot}" ${profile.pieces[slot] === id ? "checked" : ""}>${icon(glyphName)}<span>${label}</span></label>`,
    )
    .join("")}</div><label class="wood-select wide">Crafted gear style<select id="skin-choice">${["Classic", "Candy", "Arcade"]
    .filter((s) => owns(progress, "skin", s))
    .map((s) => `<option ${skin === s ? "selected" : ""}>${s}</option>`)
    .join("")}</select></label><button class="sticker-button" data-action="home">That’s my look ${icon("check")}</button>`;
}
const LOBBY_SPOTS = [
  [0, 0],
  [-1.3, -0.45],
  [1.3, -0.45],
  [2.45, -1.25],
];
function stageLobby(tab) {
  const extent = game.extent,
    angle = 1.374,
    f = { x: Math.sin(angle), z: Math.cos(angle) },
    r = { x: -f.z, z: f.x },
    floor = -0.65,
    top = floor + 0.12,
    c = { x: f.x * (extent + 6), z: f.z * (extent + 6) };
  game.players.forEach((p, i) => {
    const [side, back] = LOBBY_SPOTS[i] ?? [0, -2.4];
    p.x = c.x + r.x * side + f.x * back;
    p.z = c.z + r.z * side + f.z * back;
    p.y = top;
    p.yaw = angle - side * 0.12;
    p.facing = { x: Math.sin(p.yaw), z: Math.cos(p.yaw) };
  });
  const aspect = innerWidth / Math.max(1, innerHeight),
    portrait = aspect < 1,
    halfTan = Math.tan((20 * Math.PI) / 180),
    clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v)),
    fit = (width) => clamp(width / (2 * halfTan * aspect), 3.2, 9.5),
    widthAt = (d) => 2 * halfTan * aspect * d;
  let distance, shift, eye, look;
  if (tab === "play") {
    distance = fit(portrait ? 2.3 : 5.6);
    shift = 0;
    eye = portrait ? 1.05 : 0.95;
    look = portrait ? 0.95 : 0.78;
  } else if (tab === "locker") {
    distance = portrait ? 5.2 : 3.3;
    shift = portrait ? 0 : widthAt(distance) * 0.17;
    eye = portrait ? 1.1 : 0.95;
    look = portrait ? -0.2 : 0.68;
  } else {
    distance = portrait ? 7 : fit(9.5);
    shift = portrait ? 0 : widthAt(distance) * 0.23;
    eye = 1.0;
    look = portrait ? -0.55 : 0.8;
  }
  const at = (along, height, sideways) => ({
    x: c.x + f.x * along - r.x * sideways,
    y: top + height,
    z: c.z + f.z * along - r.z * sideways,
  });
  view.setLobby({
    centre: c,
    yaw: angle,
    floor,
    spots: LOBBY_SPOTS,
    camera: at(distance, eye, shift),
    target: at(0, look, shift),
  });
}
function placeLobbyTags() {
  if (screen !== "home" || lobbyTab !== "play") return;
  ui.querySelectorAll("[data-tag]").forEach((tag) => {
    const p = game.players[Number(tag.dataset.tag)];
    if (!p) return;
    const s = view.screenOf(p.x, p.y + 1.42, p.z);
    tag.style.transform = `translate(${s.x.toFixed(1)}px, ${s.y.toFixed(1)}px) translate(-50%, -100%)`;
    tag.style.visibility = s.visible ? "visible" : "hidden";
  });
}
function bindLobby(tab) {
  const name = document.querySelector("#player-name");
  if (name)
    name.onchange = () => {
      const clean = name.value.replace(/[^A-Za-z0-9 _-]/g, "").trim().slice(0, 16);
      if (clean.length >= 2) progress.name = clean;
      name.value = progress.name;
      saveProgress();
    };
  if (tab === "locker") bindLocker();
  ui.querySelectorAll("[data-mode]").forEach(
    (b) =>
      (b.onclick = () => {
        mode = b.dataset.mode;
        lobbyPicker = null;
        sound("craft");
        home("play");
      }),
  );
  ui.querySelectorAll("[data-map]").forEach(
    (b) =>
      (b.onclick = () => {
        map = b.dataset.map;
        lobbyPicker = null;
        home("play");
      }),
  );
  const shade = document.querySelector("#picker-shade");
  if (shade)
    shade.onclick = (e) => {
      if (e.target === shade) actions.closePicker();
    };
  ui.querySelectorAll("[data-buy]").forEach(
    (b) =>
      (b.onclick = () => {
        const result = buy(progress, b.dataset.buy);
        if (!result.ok) return toast(result.error);
        saveProgress();
        sound("buff");
        toast(`${result.item.name} unlocked. Equip it in the Locker.`);
        home("shop");
      }),
  );
  ui.querySelectorAll("[data-board]").forEach(
    (b) =>
      (b.onclick = () => {
        boardMode = b.dataset.board;
        home("leaderboard");
      }),
  );
  if (tab === "leaderboard") loadBoard(boardMode);
}
function bindLocker() {
  const update = () => {
    if (profile.pieces.Headwear === "Hood" && profile.pieces.Top !== "Hoodie")
      profile.pieces.Top = "Hoodie";
    saveProfile();
    game.wardrobe = profile;
    game.players[0].wardrobe = profile;
    view.applyWardrobe(view.entities.get("p0").userData.avatar, profile);
  };
  document.querySelector("#outfit-top").onchange = (e) => {
    profile.pieces.Top = e.target.value;
    if (e.target.value !== "Hoodie" && profile.pieces.Headwear === "Hood")
      delete profile.pieces.Headwear;
    update();
  };
  document.querySelector("#outfit-head").onchange = (e) => {
    if (e.target.value) profile.pieces.Headwear = e.target.value;
    else delete profile.pieces.Headwear;
    update();
  };
  document.querySelector("#skin-choice").onchange = (e) => {
    skin = e.target.value;
    saveProfile();
  };
  ui.querySelectorAll("[data-color]").forEach(
    (b) =>
      (b.onclick = () => {
        if (b.classList.contains("locked")) {
          toast("That colour is in the Shop.");
          return home("shop");
        }
        profile.colours.Top = b.dataset.color;
        ui.querySelectorAll("[data-color]").forEach((n) =>
          n.classList.toggle("selected", n === b),
        );
        update();
      }),
  );
  ui.querySelectorAll("[data-piece]").forEach(
    (b) =>
      (b.onchange = () => {
        if (b.checked) profile.pieces[b.dataset.slot] = b.dataset.piece;
        else delete profile.pieces[b.dataset.slot];
        update();
      }),
  );
}
function closet() {
  home("locker");
}
function saveProgress() {
  try {
    localStorage.setItem("wreckabulary.progress.v1", JSON.stringify(progress));
  } catch {}
}
async function loadBoard(boardFor) {
  const list = () => document.querySelector("#board");
  try {
    const response = await fetch(
      `/api/scores?mode=${encodeURIComponent(boardFor)}&player=${encodeURIComponent(progress.player)}`,
      { headers: { accept: "application/json" } },
    );
    const body = response.headers.get("content-type")?.includes("json") ? await response.json() : null;
    if (!response.ok || !body?.rows) throw new Error(body?.error ?? "offline");
    if (lobbyTab !== "leaderboard" || boardMode !== boardFor || !list()) return;
    list().innerHTML = body.rows.length
      ? body.rows
          .map(
            (row) =>
              `<li class="${row.you ? "you" : ""} ${row.rank <= 3 ? `podium top-${row.rank}` : ""}"><span class="rank">${row.rank}</span><strong>${esc(row.name)}</strong><b>${row.score.toLocaleString()}</b></li>`,
          )
          .join("")
      : `<li class="board-note">No scores yet. Be the first!</li>`;
  } catch {
    if (lobbyTab !== "leaderboard" || boardMode !== boardFor || !list()) return;
    const best = progress.bests[boardFor];
    list().innerHTML = `<li class="board-note">The online board is offline. Showing this device.</li>${best ? `<li class="you"><span class="rank">–</span><strong>${esc(progress.name)}</strong><b>${best.toLocaleString()}</b></li>` : ""}`;
  }
}
function rewardMatch(won) {
  if (game.rewarded) return game.rewarded;
  const reward = matchReward(game.stats, won),
    best = recordMatch(progress, game.mode, reward);
  game.rewarded = { ...reward, best };
  saveProgress();
  if (RANKED_MODES.includes(game.mode))
    fetch("/api/scores", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        name: progress.name,
        mode: game.mode,
        score: reward.score,
        player: progress.player,
      }),
    }).catch(() => {});
  return game.rewarded;
}
function renderHud() {
  const m = MODES[game.mode];
  ui.innerHTML = `<div class="hud"><header class="hud-top"><button class="brand-small" data-action="pause" aria-label="Pause game">W<span>!</span></button><div class="match-label"><span class="eyebrow">${m.tag}</span><strong>${m.title}</strong><span id="round-text"></span></div></header><aside class="hud-side"><div class="minimap" id="minimap" aria-label="House map"></div><div class="side-info"><div class="side-row"><div class="alive-count" id="alive-count" role="img" aria-label="Housemates up"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="9" cy="7" r="3.2"/><path d="M2.5 20a6.5 6.5 0 0 1 13 0z"/><circle cx="17" cy="8" r="2.6"/><path d="M16 13.2a5.5 5.5 0 0 1 6 5.8h-4.4"/></svg><b>4/4</b></div><div class="timer" id="timer">2:30</div></div><div class="room-pill" id="room-pill">Bedroom</div><div class="objective-top" id="objective-top"></div></div></aside><div class="status-message" id="status-message"></div><section class="vitals"><div class="vitals-hp"><span class="hp-cross" aria-hidden="true"></span><div class="hp-label"><strong id="hp-value">100</strong><span>HP</span></div><div class="hp-side"><span id="shield-text"></span></div></div><div class="equipment"><button data-action="slot0" class="gear-slot" id="slot0"><kbd>1</kbd><span>Empty hand</span></button><button data-action="slot1" class="gear-slot" id="slot1"><kbd>2</kbd><span>Empty hand</span></button></div></section><section class="letter-tray"><div class="tray-header"><span>YOUR LETTERS</span><strong id="bag-count">0 / 10</strong><button class="link" data-action="craft">Spell <kbd>Q</kbd></button><button class="link bag-link" data-action="bag">Bag <kbd>Tab</kbd></button></div><div id="letters"></div><div id="craft-progress"></div></section><section class="actions"><button class="action small" data-hold="block" id="block-action">${actionArt("block")}<small>BLOCK</small><kbd>RMB</kbd></button><button class="action small" data-hold="interact" id="interact-action">${actionArt("interact")}<small>INTERACT</small><kbd>E</kbd></button><button class="action attack" data-hold="attack" id="attack-action"><span id="attack-icon">${actionArt("smash", 42)}</span><small id="attack-label">SMASH</small><kbd>LMB</kbd></button><button class="action small" data-action="jump" id="jump-action">${actionArt("jump")}<small>JUMP</small><kbd>Space</kbd></button><button class="action small" data-action="dodge" id="dodge-action">${actionArt("dodge")}<small>DODGE</small><kbd>Shift</kbd></button><button class="action craft-button" data-action="craft">${actionArt("craft", 34)}<small>SPELL</small><kbd>Q</kbd></button><button class="action small drop-action" data-action="drop">${actionArt("drop")}<small>DROP</small><kbd>R</kbd></button></section><div class="joystick" id="joystick" aria-label="Touch movement joystick"><div class="joystick-knob"></div></div><p class="control-hint"><kbd>WASD</kbd> move <span>·</span> mouse look <span>·</span> <kbd>LMB</kbd> smash, throw, place <span>·</span> <kbd>Q</kbd> spell <span>·</span> <kbd>E</kbd> interact <span>·</span> <kbd>Tab</kbd> bag &amp; map <span>·</span> <kbd>Esc</kbd> pause</p><div class="crosshair" aria-hidden="true"><i></i><i></i><i></i><i></i></div><div id="bag-root"></div></div><div id="drawer-root"></div><div id="modal-root"></div>`;
  bindUi();
  bindJoystick();
  updateHud();
}
function updateHud() {
  if (screen !== "game") return;
  const p = game.players[0],
    held = game.held(p),
    remaining = Math.max(
      0,
      game.rules.roundTimeLimit - (game.time - game.roundStart),
    );
  document.querySelector("#hp-value").textContent = Math.ceil(p.hp);
  document.querySelector(".vitals-hp").classList.toggle("low", p.hp < 30);
  document.querySelector("#shield-text").textContent =
    p.bubble > 0 ? `+ ${Math.ceil(p.bubble)} BUBBLE` : "";
  document.querySelector("#bag-count").textContent =
    `${p.bag.length + (p.craft?.word.length ?? 0)} / ${game.rules.maxLetters}`;
  const letters = document.querySelector("#letters"),
    bag = [...p.bag].sort().join(""),
    reserved = p.craft?.word ?? "",
    key = `${bag}|${reserved}|${game.rules.maxLetters}`;
  if (letters.dataset.bag !== key) {
    letters.dataset.bag = key;
    letters.title = bag ? "" : "Smash furniture to find your first word.";
    letters.innerHTML = bagCells(bag, reserved, true);
    letters.querySelectorAll("[data-letter]").forEach(
      (b) =>
        (b.onclick = () => {
          game.tossLetter(p, b.dataset.letter);
          toast(`Tossed ${b.dataset.letter}. Walk over it to pick it up.`);
        }),
    );
  }
  for (let slot = 0; slot < 2; slot++) {
    const node = document.querySelector(`#slot${slot}`),
      item = game.item(p.slots[slot]);
    node.classList.toggle("active", p.slot === slot);
    if (node.dataset.item !== String(item?.id)) {
      node.dataset.item = String(item?.id);
      node.innerHTML = `<kbd>${slot + 1}</kbd>${item ? `${iconHtml(item.word)}<span>${item.word}</span>` : "<span>Empty hand</span>"}`;
    }
  }
  document.querySelector("#timer").textContent =
    game.mode === "Tutorial"
      ? "FREE PLAY"
      : `${Math.floor(remaining / 60)}:${String(Math.floor(remaining % 60)).padStart(2, "0")}`;
  document.querySelector("#round-text").textContent = ["Dibs", "Duos"].includes(
    game.mode,
  )
    ? `ROUND ${game.round} · ${game.wins[0]} / 3 WINS`
    : game.mode === "Tutorial"
      ? "NO PRESSURE. JUST PLAY."
      : game.map.name;
  document.querySelector("#room-pill").textContent =
    game.roomAt(p)?.name.replace(/([a-z])([A-Z])/g, "$1 $2") ?? "House";
  const attackIcon = document.querySelector("#attack-icon");
  if (attackIcon.dataset.word !== held?.word) {
    attackIcon.dataset.word = held?.word ?? "";
    attackIcon.innerHTML = held ? iconHtml(held.word) : actionArt("smash", 42);
  }
  document.querySelector("#attack-label").textContent = itemActionLabel(held, game.placesObjective(p, held));
  document
    .querySelector("#block-action")
    .classList.toggle("disabled", !held?.definition?.shield);
  document
    .querySelector("#dodge-action")
    .classList.toggle(
      "cooldown",
      game.time - p.lastDodge < game.rules.dodgeCooldown,
    );
  const craft = document.querySelector("#craft-progress");
  craft.innerHTML = p.action
    ? `<span>${p.action.kind === "use" ? "Using" : "Placing"} ${held?.word}…</span><div class="progress"><i style="width:${((game.time - p.action.start) / (p.action.readyAt - p.action.start)) * 100}%"></i></div>`
    : p.craft
      ? `<span>Spelling ${p.craft.word}…</span><div class="progress"><i style="width:${((game.time - p.craft.start) / (p.craft.readyAt - p.craft.start)) * 100}%"></i></div>`
      : "";
  const objective = document.querySelector("#objective-top");
  if (game.mode === "MovingOut")
    objective.innerHTML = `<span class="eyebrow">THE GREAT ESCAPE</span><strong>${game.keepsakes.filter((k) => k.collected).length} / ${game.keepsakes.length} KEEPSAKES ${game.keepsakes.every((k) => k.collected) ? "→ GET TO THE VAN" : ""}</strong>`;
  else if (game.mode === "MovingDay")
    objective.innerHTML = `<span class="eyebrow">MAKE YOURSELF AT HOME</span><strong>${game.objectives.filter((o) => o.done).length} / 4 PLACED</strong><span>${game.objectives
      .filter((o) => !o.done)
      .map((o) => `${o.word} → ${o.room}`)
      .join(" · ")}</span>`;
  else objective.innerHTML = "";
  const alive = game.players.filter((q) => q.state === "alive").length,
    count = document.querySelector("#alive-count");
  count.querySelector("b").textContent = `${alive}/${game.players.length}`;
  count.setAttribute("aria-label", `${alive} of ${game.players.length} housemates up`);
  const warning = [...game.roomStatus.entries()]
      .filter(([r, s]) => s.warning)
      .map(([r]) => r)
      .join(", "),
    status = document.querySelector("#status-message");
  if (p.state === "downed")
    status.innerHTML = `<strong>Down, but not out.</strong> Pip can help you up · ${Math.ceil(p.bleedAt - game.time)}s`;
  else if (p.state === "eliminated")
    status.innerHTML =
      game.rules.respawn >= 0
        ? "Back on your feet in a moment."
        : "Out for this round. Your housemates are still making a mess.";
  else if (warning)
    status.innerHTML = `<strong>Movers incoming!</strong> Leave ${warning} before it gets packed.`;
  else if (p.reviveTarget !== null)
    status.innerHTML = `<strong>Helping ${game.players[p.reviveTarget].name}…</strong> Keep holding interact.`;
  else if (p.carried !== null)
    status.innerHTML = `<strong>Carrying ${held?.word}</strong> Set it down with R inside the van circle · click to throw`;
  else if (game.mode === "Tutorial")
    status.innerHTML =
      game.stats.broken === 0
        ? "Step 1 · Walk to a piece of furniture. Aim and smash it."
        : game.stats.collected === 0
          ? "Step 2 · Walk over the letter tiles."
          : game.stats.crafted === 0
            ? "Step 3 · Press Q (or SPELL) and type a word you have the letters for. Tab shows the recipe book."
            : "Nice wordwork! Try placing, throwing, dodging and different recipes.";
  else status.innerHTML = "";
  updateMinimap();
  const composerKey = p.bag + !!p.craft + p.slots.join(",");
  if (craftOpen && composerKey !== lastComposerKey) {
    lastComposerKey = composerKey;
    updateComposer();
  }
  if (bagOpen) updateBag();
  if (
    ["roundOver", "finished"].includes(game.status) &&
    !document.querySelector(".result-card")
  )
    result();
}
function bagCells(bag, reserved, tossable) {
  const cells = [...bag].map((c) =>
    tossable
      ? `<button class="letter" data-letter="${c}" title="Toss ${c} (free bag space)">${c}</button>`
      : `<span class="letter">${c}</span>`,
  );
  for (const c of reserved)
    cells.push(`<span class="letter-cell reserved" title="Being spelled">${c}</span>`);
  while (cells.length < game.rules.maxLetters)
    cells.push(`<span class="letter-cell empty" aria-hidden="true"></span>`);
  return cells.join("");
}
const WEAR_SLOTS = ["Headwear", "Face", "Top", "Gloves", "Bottoms", "Footwear", "Back", "Badge"];
function setBag(open, recipesOnly = false) {
  const showing = open && screen === "game";
  if (showing) {
    if (craftOpen) drawer();
    resetInputs();
    releaseMouse();
  }
  bagOpen = showing;
  const recipes = bagOpen && recipesOnly;
  const root = document.querySelector("#bag-root");
  if (!root) return;
  root.innerHTML = bagOpen
    ? `<section class="bag-panel${recipes ? " recipes-only" : ""}" aria-label="${recipes ? "Recipe book" : "Bag and map"}"><div class="bag-inv"><div class="inv-row inv-letters" aria-label="Letters">${glyph("bag")}<div class="bag-letters" id="bag-letters"></div><b class="inv-count" id="bag-panel-count"></b></div><div class="inv-row bag-hands" id="bag-hands" aria-label="Hands"></div><div class="inv-row bag-wear" id="bag-wear" aria-label="Wearing"></div><div class="inv-row bag-effects" id="bag-effects" aria-label="Effects"></div></div><div class="bag-map-slot" aria-hidden="true"></div><aside class="bag-book" aria-label="Recipe book">${recipes ? '<h2 class="recipe-book-heading">Recipe book</h2>' : glyph("book")}<div class="recipe-grid" id="bag-recipes"></div></aside><button class="close bag-close" data-action="bag" aria-label="Close bag">×</button></section>`
    : "";
  root.querySelectorAll("[data-action]").forEach(
    (b) => (b.onclick = () => actions[b.dataset.action]()),
  );
  document.querySelector("#minimap")?.classList.toggle("zoomed", bagOpen && !recipes);
  document.querySelector(".hud")?.classList.toggle("bag-open", bagOpen);
  document.querySelector(".hud")?.classList.toggle("recipes-open", recipes);
  if (bagOpen) updateBag();
  updateMinimap();
}
const GLYPHS = {
  bag: '<path d="M7 8V6a5 5 0 0 1 10 0v2"/><rect x="4" y="8" width="16" height="13" rx="3"/>',
  book: '<path d="M4 5a2 2 0 0 1 2-2h6v17H6a2 2 0 0 0-2 2z"/><path d="M20 5a2 2 0 0 0-2-2h-6v17h6a2 2 0 0 1 2 2z"/>',
  hand: '<path d="M8 13V5a1.5 1.5 0 0 1 3 0v6m0-1V3.5a1.5 1.5 0 0 1 3 0V11m0-6a1.5 1.5 0 0 1 3 0v8a7 7 0 0 1-7 7h-1a6 6 0 0 1-5-3l-2.5-4a1.5 1.5 0 0 1 2.5-1.6L8 14"/>',
  Headwear: '<path d="M4 16a8 8 0 0 1 16 0z"/><path d="M2 16h20"/>',
  Face: '<circle cx="8" cy="12" r="3"/><circle cx="16" cy="12" r="3"/><path d="M11 12h2"/>',
  Top: '<path d="M8 3 3 7l3 3 2-1v12h8V9l2 1 3-3-5-4a4 4 0 0 1-8 0z"/>',
  Gloves: '<path d="M7 21v-6l-3-4 2-2 3 3V4a1.5 1.5 0 0 1 3 0v5-6a1.5 1.5 0 0 1 3 0v6-4a1.5 1.5 0 0 1 3 0v10a6 6 0 0 1-4 6z"/>',
  Bottoms: '<path d="M6 3h12l1 18h-5l-2-11-2 11H5z"/>',
  Footwear: '<path d="M3 17V9h6l2 4 8 1a2 2 0 0 1 2 2v1z"/><path d="M3 20h18"/>',
  Back: '<rect x="6" y="5" width="12" height="16" rx="3"/><path d="M9 5a3 3 0 0 1 6 0M9 13h6"/>',
  Badge: '<circle cx="12" cy="9" r="6"/><path d="m9 14-2 7 5-3 5 3-2-7"/>',
  bubble: '<circle cx="12" cy="12" r="9"/><path d="M8 9a4 4 0 0 1 4-3"/>',
  speed: '<path d="M13 2 4 14h7l-1 8 9-12h-7z"/>',
  carry: '<path d="M3 8 12 3l9 5v9l-9 5-9-5z"/><path d="m3 8 9 5 9-5M12 13v9"/>',
  timer: '<circle cx="12" cy="13" r="8"/><path d="M12 9v4l3 2M9 2h6"/>',
};
function glyph(name, label = "") {
  return `<svg class="glyph" viewBox="0 0 24 24" ${label ? `role="img" aria-label="${esc(label)}"` : 'aria-hidden="true"'}>${label ? `<title>${esc(label)}</title>` : ""}${GLYPHS[name] ?? ""}</svg>`;
}
function updateBag() {
  if (!bagOpen || !document.querySelector("#bag-letters")) return;
  const p = game.players[0],
    word = (w) => esc(w.replace(/([a-z])([A-Z])/g, "$1 $2"));
  document.querySelector("#bag-panel-count").textContent =
    `${p.bag.length + (p.craft?.word.length ?? 0)}/${game.rules.maxLetters}`;
  document.querySelector("#bag-letters").innerHTML = bagCells(
    [...p.bag].sort().join(""),
    p.craft?.word ?? "",
    false,
  );
  document.querySelector("#bag-hands").innerHTML = [0, 1]
    .map((slot) => {
      const item = game.item(p.slots[slot]),
        wear = item?.maxDurability
          ? Math.max(0, item.durability / item.maxDurability)
          : 0;
      return `<div class="bag-hand ${p.slot === slot ? "active" : ""} ${item ? "" : "empty"}" title="${item ? item.word : "Empty hand"}"><kbd>${slot + 1}</kbd>${item ? `${iconHtml(item.word)}<div class="progress"><i style="width:${wear * 100}%"></i></div>` : glyph("hand", "Empty hand")}</div>`;
    })
    .join("");
  const pieces = p.wardrobe?.pieces ?? {};
  document.querySelector("#bag-wear").innerHTML = WEAR_SLOTS.filter(
    (s) => pieces[s],
  )
    .map(
      (s) =>
        `<span class="wear-chip" title="${pieces[s] === "TBadge" ? "Letter badge" : word(pieces[s])}">${glyph(s, `${s}: ${pieces[s] === "TBadge" ? "Letter badge" : word(pieces[s])}`)}</span>`,
    )
    .join("");
  const effects = [];
  if (p.bubble > 0 && game.time < p.bubbleUntil)
    effects.push(
      `<span class="effect-chip bubble" title="Bubble">${glyph("bubble", "Bubble")}<b>${Math.ceil(p.bubbleUntil - game.time)}</b></span>`,
    );
  if (game.time < (p.speedUntil ?? 0))
    effects.push(
      `<span class="effect-chip speed" title="Speed">${glyph("speed", "Speed")}<b>${Math.ceil(p.speedUntil - game.time)}</b></span>`,
    );
  if (game.time < (p.slowUntil ?? 0))
    effects.push(
      `<span class="effect-chip slow" title="Slowed">${iconHtml("CLOCK")}<b>${Math.ceil(p.slowUntil - game.time)}</b></span>`,
    );
  if (p.carried !== null) {
    const carried = game.item(p.carried)?.word;
    effects.push(
      `<span class="effect-chip carry" title="Carrying">${carried && view?.manifest.items[carried] ? iconHtml(carried) : glyph("carry", "Carrying")}</span>`,
    );
  }
  document.querySelector("#bag-effects").innerHTML = effects.join("");
  const book = document.querySelector("#bag-recipes");
  if (book.dataset.bag !== p.bag) {
    book.dataset.bag = p.bag;
    book.innerHTML = recipeBook(p.bag);
    book.querySelectorAll("[data-recipe]").forEach(
      (b) =>
        (b.onclick = () => {
          setBag(false);
          const input = document.querySelector("#spell-word");
          if (!craftOpen) drawer(b.dataset.recipe);
          else if (input) {
            input.value = b.dataset.recipe;
            updateComposer();
            input.focus();
          }
        }),
    );
  }
}
function updateMinimap(root = document.querySelector("#minimap"), full = false) {
  if (!root || !game) return;
  const extent = game.extent;
  root.innerHTML = `${game.house.rooms
    .map((r) => {
      const b = r.bounds,
        state = game.roomStatus.get(r.name),
        here = game.roomAt(game.players[0]) === r;
      return `<div class="map-room ${state?.closed ? "closed" : state?.warning ? "warning" : ""} ${here ? "here" : ""}" style="left:${((b[0] + extent) / extent) * 50}%;top:${((b[1] + extent) / extent) * 50}%;width:${((b[2] - b[0]) / extent) * 50}%;height:${((b[3] - b[1]) / extent) * 50}%"><span>${full ? esc(r.name.replace(/([a-z])([A-Z])/g, "$1 $2")) : r.name[0]}</span></div>`;
    })
    .join("")}${game.players
    .filter((p) => p.state !== "eliminated")
    .map(
      (p) =>
        `<i class="map-player ${p.id === 0 ? "you" : ""} ${p.state === "downed" ? "downed" : ""}" style="left:${((p.x + extent) / extent) * 50}%;top:${((p.z + extent) / extent) * 50}%">${full ? `<b>${p.id === 0 ? "You" : esc(p.name)}</b>` : ""}</i>`,
    )
    .join("")}${game.keepsakes
    .filter((k) => !k.collected)
    .map(
      (k) =>
        `<i class="map-keepsake" style="left:${((k.x + extent) / extent) * 50}%;top:${((k.z + extent) / extent) * 50}%">◆</i>`,
    )
    .join("")}`;
}
function letterCover(bag, word) {
  const copy = [...bag];
  return [...word].map((c) => {
    const index = copy.indexOf(c);
    if (index < 0) return false;
    copy.splice(index, 1);
    return true;
  });
}
function recipeBook(bag = "") {
  return data.items.items
    .filter((i) => i.enabled)
    .map((item) => {
      const cover = letterCover(bag, item.id),
        ready = bag && cover.every(Boolean);
      return `<button class="recipe ${ready ? "available" : ""}" data-recipe="${item.id}" title="${recipeDescription(item)}" aria-label="${item.id}${ready ? ", you have the letters" : ""}">${iconHtml(item.id)}<div class="recipe-letters">${[...item.id].map((c, i) => `<span class="${cover[i] ? "got" : ""}">${c}</span>`).join("")}</div></button>`;
    })
    .join("");
}
function drawer(prefill = "") {
  if (!craftOpen && bagOpen) setBag(false);
  craftOpen = !craftOpen;
  resetInputs();
  if (craftOpen) releaseMouse();
  const root = document.querySelector("#drawer-root");
  if (!craftOpen) {
    root.innerHTML = "";
    return;
  }
  root.innerHTML = `<section class="spell-composer" aria-label="Spell a word"><div class="composer-head"><span class="eyebrow">SPELL A WORD</span><button class="link" type="button" data-action="recipes">Recipe book <kbd>Tab</kbd></button><button class="close" data-action="craft" aria-label="Close spelling">×</button></div><form id="spell-form"><label class="composer-field" for="spell-word"><span class="composer-tiles" id="composer-tiles" aria-hidden="true"></span><input id="spell-word" maxlength="10" autocomplete="off" autocapitalize="characters" spellcheck="false" enterkeyhint="go" aria-describedby="composer-status"></label><button class="primary" type="submit">SPELL <kbd>Enter</kbd></button></form><p class="composer-status" id="composer-status"></p></section>`;
  bindUi();
  const input = document.querySelector("#spell-word");
  input.value = prefill;
  input.oninput = () => {
    input.value = input.value.toUpperCase().replace(/[^A-Z]/g, "");
    updateComposer();
  };
  input.onkeydown = (e) => {
    if (e.code === "Escape") {
      e.preventDefault();
      if (bagOpen) setBag(false);
      else drawer();
    } else if (e.code === "Tab") {
      e.preventDefault();
      if (!e.repeat) setBag(true, true);
    }
  };
  document.querySelector("#spell-form").onsubmit = (e) => {
    e.preventDefault();
    craftWord(input.value);
  };
  updateComposer();
  input.focus();
}
function updateComposer(error = "") {
  const input = document.querySelector("#spell-word");
  if (!input) return;
  const p = game.players[0],
    word = input.value,
    cover = letterCover(p.bag, word),
    recipe = data.items.items.find((i) => i.enabled && i.id === word),
    partial = word && data.items.items.some((i) => i.enabled && i.id.startsWith(word)),
    missing = [...word].filter((c, i) => !cover[i]);
  document.querySelector("#composer-tiles").innerHTML =
    [...word]
      .map((c, i) => `<span class="letter ${cover[i] ? "" : "missing"}">${c}</span>`)
      .join("") + `<span class="composer-caret"></span>`;
  let text, state;
  if (error) [text, state] = [error, "bad"];
  else if (!word) [text, state] = ["Type a word you can make from your letters.", ""];
  else if (recipe && !missing.length && p.craft) [text, state] = ["Already spelling something. Hang on.", "bad"];
  else if (recipe && !missing.length && game.freeSlot(p) < 0) [text, state] = ["Both hands are full. Drop something first.", "bad"];
  else if (recipe && !missing.length) [text, state] = [`${word} is ready. Press Enter!`, "ready"];
  else if (recipe) [text, state] = [`${word} needs ${missing.join(" ")}. Smash more furniture.`, "bad"];
  else if (partial) [text, state] = ["Keep going…", ""];
  else [text, state] = ["That isn't a recipe. Check the book with Tab.", "bad"];
  const status = document.querySelector("#composer-status");
  status.textContent = text;
  status.dataset.state = state;
  document.querySelector(".spell-composer").dataset.state = state;
}
function craftWord(word) {
  const error = game.craft(game.players[0], word.trim());
  if (error) {
    updateComposer(error);
    document.querySelector(".spell-composer")?.animate(
      [{ transform: "translateX(-50%)" }, { transform: "translateX(calc(-50% - 7px))" }, { transform: "translateX(calc(-50% + 7px))" }, { transform: "translateX(-50%)" }],
      { duration: 240 },
    );
    return;
  }
  toast(`Spelling ${word.toUpperCase()}…`);
  drawer();
}
function result() {
  resetInputs();
  releaseMouse();
  craftOpen = false;
  document.querySelector("#drawer-root").innerHTML = "";
  const win = game.winner === game.players[0].team,
    reward = game.status === "finished" ? rewardMatch(win) : null;
  document.querySelector("#modal-root").innerHTML =
    `<div class="modal-shade"><section class="result-card"><span class="result-spark">${win ? "✦" : "→"}</span><span class="eyebrow">${game.status === "roundOver" ? `ROUND ${game.round} COMPLETE` : "HOUSE PARTY COMPLETE"}</span><h2>${game.result}</h2><p>${win ? "A little imagination goes a long way." : "Every good mess teaches you a new trick."}</p><div class="result-stats"><div><strong>${game.stats.broken}</strong><span>objects wrecked</span></div><div><strong>${game.stats.crafted}</strong><span>words crafted</span></div><div><strong>${Math.ceil(game.stats.damage)}</strong><span>damage dealt</span></div></div>${reward ? `<div class="result-reward"><span class="coins">${COIN_ICON}<b>+${reward.coins}</b></span><span>Score <b>${reward.score}</b>${reward.best ? ` <em>New best!</em>` : ""}</span></div>` : ""}<button class="primary" data-action="${game.status === "roundOver" ? "next" : "start"}">${game.status === "roundOver" ? "NEXT ROUND" : "PLAY AGAIN"} →</button><button class="quiet" data-action="home">Back to the house party</button></section></div>`;
  bindUi();
}
function pause() {
  if (screen !== "game") return;
  resetInputs();
  if (craftOpen) {
    drawer();
    return;
  }
  paused = !paused;
  if (paused) releaseMouse();
  else lockMouse();
  document.querySelector("#modal-root").innerHTML = paused
    ? `<div class="modal-shade"><section class="result-card"><span class="eyebrow">TAKE A BREATHER</span><h2>The mess can wait.</h2><p>${MODES[game.mode].description}</p><button class="primary" data-action="pause">KEEP PLAYING →</button><button class="quiet" data-action="how">Controls & recipes</button><button class="quiet" data-action="home">Back to the house party</button></section></div>`
    : "";
  bindUi();
}
function how() {
  const previous = screen;
  const layer = document.createElement("div");
  layer.className = "modal-shade help-shade";
  layer.innerHTML = `<section class="help-card"><button class="close" id="help-close" aria-label="Close instructions">×</button><span class="eyebrow">A HOUSE FULL OF POSSIBILITIES</span><h2>Everything starts with a word.</h2><div class="how-steps"><div><b>01</b><strong>Break it.</strong><p>Smash original furniture. A TABLE breaks into T, A, B, L, E.</p></div><div><b>02</b><strong>Spell it.</strong><p>Walk over letters. Your bag holds 10. Use SPELL to craft any of the ${data.items.items.filter(item => item.enabled).length} enabled recipes.</p></div><div><b>03</b><strong>Bring it.</strong><p>Click to swing gear, throw it or place a tool. Reusable gear returns its letters when broken. Consumables spend them.</p></div></div><div class="key-table"><span><kbd>WASD / arrows</kbd> move</span><span><kbd>Mouse / touch</kbd> aim</span><span><kbd>LMB / J</kbd> smash · throw · place · use</span><span><kbd>RMB / K</kbd> block with PLATE / SHIELD</span><span><kbd>Shift</kbd> dodge</span><span><kbd>Space</kbd> jump</span><span><kbd>Q / C</kbd> spell</span><span><kbd>1 / 2</kbd> switch hand</span><span><kbd>Tab</kbd> hold for bag &amp; map</span><span><kbd>E</kbd> pickup / hold to revive</span><span><kbd>R</kbd> drop gear</span></div><p class="fineprint">Health is 100 HP. Hits never remove letters. Cosmetics change your look, never your stats. The Movers announce room closures before dealing hazard damage.</p><button class="primary" id="help-done">GOT IT. LET’S PLAY. →</button></section>`;
  ui.append(layer);
  const close = () => layer.remove();
  layer.querySelector("#help-close").onclick = close;
  layer.querySelector("#help-done").onclick = close;
}
const workshopSessions = new Map();
let workshop = null;
const designData = () => ({ houses: data.houses, items: data.items });
const designKey = (id) => `wreckabulary.workshop.v1.${id}`;
const designText = (layout) => JSON.stringify(layout);
const designError = (errors) =>
  errors.map((e) => e.replace(/^[a-z]+:\s*/i, "")).join(" ");
function workshopMessage(message, error = false) {
  const node = document.querySelector("#workshop-message");
  if (node) {
    node.textContent = message;
    node.classList.toggle("error", error);
  }
  announcer.textContent = message;
}
function loadWorkshopSession(id) {
  if (workshopSessions.has(id))
    return { session: workshopSessions.get(id), error: null };
  let layout = createLayout(id),
    saved = false;
  try {
    const raw = localStorage.getItem(designKey(id));
    if (raw) {
      const result = importLayout(raw, designData());
      if (!result.ok || result.layout.map !== id)
        return {
          session: null,
          error:
            "Your saved house could not be opened. " +
            (result.ok
              ? "It belongs to another map."
              : designError(result.errors)),
        };
      layout = result.layout;
      saved = true;
    }
  } catch {
    return {
      session: null,
      error:
        "Your browser could not read saved houses. You can still design and export a copy.",
    };
  }
  return {
    session: {
      layout,
      history: [structuredClone(layout)],
      at: 0,
      savedText: saved ? designText(layout) : null,
      selectedId: null,
      ghost: null,
      room: data.houses[id].rooms[0].name,
    },
    error: null,
  };
}
function openWorkshop(id = map) {
  view.setLobby(null);
  releaseMouse();
  resetInputs();
  craftOpen = false;
  paused = false;
  view.closet = false;
  const loaded = loadWorkshopSession(id);
  if (!loaded.session) {
    const layout = createLayout(id);
    loaded.session = {
      layout,
      history: [structuredClone(layout)],
      at: 0,
      savedText: null,
      selectedId: null,
      ghost: null,
      room: data.houses[id].rooms[0].name,
    };
  }
  workshopSessions.set(id, loaded.session);
  map = id;
  workshop = loaded.session;
  screen = "workshop";
  rebuildWorkshop();
  renderWorkshop();
  if (loaded.error) workshopMessage(loaded.error, true);
}
function rebuildWorkshop() {
  game = new Game(data, {
    mode: "Tour",
    layout: workshop.layout,
    preview: true,
    wardrobe: structuredClone(profile),
  });
  view.rebuild(game);
  refreshWorkshopView();
}
function refreshWorkshopView() {
  if (screen !== "workshop") return;
  const panel = document
      .querySelector(".workshop-panel")
      ?.getBoundingClientRect(),
    head = document.querySelector(".workshop-head")?.getBoundingClientRect(),
    portrait = innerWidth <= 760;
  view.setWorkshop({
    enabled: true,
    selectedId: workshop.selectedId,
    ghost: workshop.ghost
      ? {
          ...workshop.ghost,
          valid:
            !workshop.invalidInputs &&
            placementCheck(
              workshop.layout,
              workshop.ghost,
              designData(),
              workshop.selectedId,
            ).ok,
        }
      : null,
    insets: {
      left: 12,
      right: !portrait && panel ? panel.width + 28 : 12,
      top: (head?.bottom ?? 78) + 58,
      bottom: portrait && panel ? panel.height + 80 : 76,
    },
  });
}
function commitDesign(candidate, message, refresh = true) {
  const result = validateLayout(candidate, designData());
  if (!result.ok) {
    workshopMessage(designError(result.errors), true);
    return false;
  }
  if (designText(result.layout) === designText(workshop.layout)) {
    if (refresh) renderWorkshop();
    workshopMessage("This is already your arrangement.");
    return true;
  }
  workshop.layout = result.layout;
  workshop.history = workshop.history.slice(0, workshop.at + 1);
  workshop.history.push(structuredClone(result.layout));
  if (workshop.history.length > 100) workshop.history.shift();
  workshop.at = workshop.history.length - 1;
  if (!workshop.layout.props.some((p) => p.id === workshop.selectedId))
    workshop.selectedId = null;
  if (refresh) {
    workshop.invalidInputs = false;
    workshop.ghost = workshop.selectedId
      ? structuredClone(
          workshop.layout.props.find((p) => p.id === workshop.selectedId),
        )
      : null;
    rebuildWorkshop();
    renderWorkshop();
  } else {
    const state = document.querySelector(".design-save-state");
    if (state)
      state.textContent = `${designText(workshop.layout) !== workshop.savedText ? "Unsaved changes" : "Saved on this browser"} · ${workshop.layout.props.length} / 64 objects`;
    const undo = document.querySelector("#design-undo"),
      redo = document.querySelector("#design-redo");
    if (undo) undo.disabled = workshop.at === 0;
    if (redo) redo.disabled = workshop.at >= workshop.history.length - 1;
  }
  workshopMessage(message);
  return true;
}
function workshopHistory(direction) {
  const next = workshop.at + direction;
  if (next < 0 || next >= workshop.history.length) return;
  workshop.at = next;
  workshop.layout = structuredClone(workshop.history[next]);
  workshop.selectedId = null;
  workshop.ghost = null;
  rebuildWorkshop();
  renderWorkshop();
  workshopMessage(
    direction < 0 ? "Undone. Your previous arrangement is back." : "Redone.",
  );
}
function selectDesignProp(id) {
  const prop = workshop.layout.props.find((p) => p.id === id);
  workshop.selectedId = prop?.id ?? null;
  workshop.invalidInputs = false;
  workshop.ghost = prop ? structuredClone(prop) : null;
  if (prop) workshop.room = game.roomAt(prop)?.name ?? workshop.room;
  renderWorkshop();
  workshopMessage(
    prop
      ? `${prop.word.toLowerCase()} selected. Rotate, change its finish or move it.`
      : "Pick a model or click an object in your house.",
  );
}
function previewDesignWord(word) {
  const result = addWords(workshop.layout, word, workshop.room, designData());
  if (!result.ok || !result.added.length) {
    workshopMessage(
      result.rejected?.[0]?.reason ?? designError(result.errors),
      true,
    );
    return;
  }
  workshop.selectedId = null;
  workshop.invalidInputs = false;
  workshop.ghost = structuredClone(result.added[0]);
  renderWorkshop();
  view.focusWorkshop(workshop.room);
  workshopMessage(
    `Previewing ${word.toLowerCase()}. Click a clear spot in the house, or choose Place here.`,
  );
}
function updateDesignGhost() {
  if (!workshop.ghost) return;
  const x = document.querySelector("#prop-x").value,
    z = document.querySelector("#prop-z").value;
  if (
    !x.trim() ||
    !z.trim() ||
    !Number.isFinite(Number(x)) ||
    !Number.isFinite(Number(z))
  ) {
    workshop.invalidInputs = true;
    refreshWorkshopView();
    workshopMessage("Enter a number for both positions.", true);
    return false;
  }
  workshop.invalidInputs = false;
  workshop.ghost.x = Number(x);
  workshop.ghost.z = Number(z);
  workshop.ghost.skin = document.querySelector("#prop-skin").value;
  const result = placementCheck(
    workshop.layout,
    workshop.ghost,
    designData(),
    workshop.selectedId,
  );
  refreshWorkshopView();
  workshopMessage(
    result.ok
      ? "This spot is clear. Apply it when you are happy."
      : designError(result.errors),
    !result.ok,
  );
  return result.ok;
}
function applyDesignGhost() {
  if (!workshop.ghost) return;
  if (!updateDesignGhost()) return;
  const candidate = structuredClone(workshop.layout),
    prop = structuredClone(workshop.ghost);
  const result = placementCheck(
    candidate,
    prop,
    designData(),
    workshop.selectedId,
  );
  if (!result.ok) {
    workshopMessage(designError(result.errors), true);
    return;
  }
  const index = candidate.props.findIndex((p) => p.id === workshop.selectedId);
  if (index >= 0) candidate.props[index] = prop;
  else candidate.props.push(prop);
  workshop.selectedId = prop.id;
  commitDesign(
    candidate,
    `${prop.word.toLowerCase()} ${index >= 0 ? "updated" : "placed"}.`,
  );
}
function rotateDesignProp() {
  if (!workshop.ghost) return;
  workshop.ghost.yaw = (workshop.ghost.yaw + 90) % 360;
  if (workshop.selectedId) applyDesignGhost();
  else {
    renderWorkshop();
    workshopMessage(`Turned to ${workshop.ghost.yaw}°. Choose a clear spot.`);
  }
}
function saveDesign() {
  const nameInput = document.querySelector("#design-name");
  if (nameInput) {
    const candidate = structuredClone(workshop.layout);
    candidate.name = nameInput.value.trim();
    const checked = validateLayout(candidate, designData());
    if (!checked.ok) {
      workshopMessage(designError(checked.errors), true);
      return false;
    }
    if (
      candidate.name !== workshop.layout.name &&
      !commitDesign(candidate, "House renamed.", false)
    )
      return false;
  }
  const result = exportLayout(workshop.layout, designData());
  if (!result.ok) {
    workshopMessage(designError(result.errors), true);
    return false;
  }
  try {
    localStorage.setItem(designKey(workshop.layout.map), result.json);
  } catch {
    workshopMessage(
      "Your browser could not save this house. Export a copy so you can keep it.",
      true,
    );
    return false;
  }
  workshop.savedText = designText(workshop.layout);
  renderWorkshop();
  workshopMessage(
    "Saved on this browser. Your house will be here when you return.",
  );
  return true;
}
function tourWorkshop() {
  if (!saveDesign()) return;
  resetInputs();
  screen = "tour";
  game = new Game(data, {
    mode: "Tour",
    layout: workshop.layout,
    wardrobe: structuredClone(profile),
  });
  view.setWorkshop({ enabled: false, selectedId: null, ghost: null });
  view.rebuild(game);
  resetLook();
  ui.innerHTML = `<main class="tour-hud"><header class="tour-head"><div><span class="eyebrow">YOUR HOUSE · PEACEFUL TOUR</span><strong>${esc(workshop.layout.name)}</strong></div><button class="primary" data-action="workshop-return">Back to decorating</button></header><p class="tour-note">Take a look around. <span class="desktop-tour-help">WASD or arrows to walk.</span><span class="touch-tour-help">Drag the joystick to walk.</span></p><div class="joystick" id="joystick" aria-label="Touch movement joystick"><div class="joystick-knob"></div></div></main>`;
  bindUi();
  bindJoystick();
}
function designTransfer(kind) {
  let json = "";
  if (kind === "export") {
    const result = exportLayout(workshop.layout, designData());
    if (!result.ok) {
      workshopMessage(designError(result.errors), true);
      return;
    }
    json = result.json;
    const blob = new Blob([json], { type: "application/json" }),
      url = URL.createObjectURL(blob),
      link = document.createElement("a");
    link.href = url;
    link.download = `${workshop.layout.map}-cozy-house.json`;
    link.hidden = true;
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  const overlay = document.createElement("section");
  overlay.className = "workshop-transfer";
  overlay.innerHTML = `<div class="workshop-transfer-card" role="dialog" aria-modal="true" aria-labelledby="transfer-title"><button class="close" id="transfer-close" aria-label="Close">×</button><span class="eyebrow">KEEP YOUR CREATION</span><h2 id="transfer-title">${kind === "export" ? "Your house, ready to share." : "Open a house from JSON."}</h2><p>${kind === "export" ? "A JSON copy has downloaded. You can also copy the text below." : "Paste your exported house, or choose a JSON file. Your current arrangement stays safe if the file cannot be opened."}</p>${kind === "import" ? '<label class="file-label">Choose a file<input id="design-file" type="file" accept="application/json,.json"></label>' : ""}<textarea id="design-json" aria-label="House JSON" ${kind === "export" ? "readonly" : ""}>${esc(json)}</textarea><p id="transfer-error" role="alert"></p>${kind === "import" ? '<button class="primary" id="design-import">Open this house</button>' : ""}</div>`;
  ui.append(overlay);
  overlay.querySelector("#transfer-close").onclick = () => overlay.remove();
  overlay
    .querySelector("#design-file")
    ?.addEventListener("change", async (e) => {
      const file = e.target.files[0];
      if (!file) return;
      if (file.size > 65536) {
        overlay.querySelector("#transfer-error").textContent =
          "This file is too large. Choose a house JSON file under 64 KB.";
        return;
      }
      try {
        overlay.querySelector("#design-json").value = await file.text();
      } catch {
        overlay.querySelector("#transfer-error").textContent =
          "The file could not be read. Try pasting its text instead.";
      }
    });
  overlay.querySelector("#design-import")?.addEventListener("click", () => {
    const result = importLayout(
      overlay.querySelector("#design-json").value,
      designData(),
    );
    if (!result.ok) {
      overlay.querySelector("#transfer-error").textContent = designError(
        result.errors,
      );
      return;
    }
    if (result.layout.map !== workshop.layout.map) {
      const loaded = loadWorkshopSession(result.layout.map);
      if (!loaded.session) {
        overlay.querySelector("#transfer-error").textContent = loaded.error;
        return;
      }
      map = result.layout.map;
      workshopSessions.set(map, loaded.session);
      workshop = loaded.session;
    }
    workshop.selectedId = null;
    workshop.ghost = null;
    commitDesign(
      result.layout,
      "House opened. Save it when you are ready to keep this arrangement.",
    );
  });
}
function renderWorkshop() {
  const w = workshop,
    prop = w.ghost,
    dirty = designText(w.layout) !== w.savedText,
    objects = data.items.items.filter((i) => i.model && Array.isArray(i.size));
  ui.innerHTML = `<main class="workshop"><header class="workshop-head"><button class="quiet" data-action="home">← House party</button><div><span class="eyebrow">CREATIVE WORKSHOP</span><strong>Your words. Your cozy house.</strong></div><div class="workshop-head-actions"><button class="quiet" id="design-save">Save</button><button class="primary" id="design-tour">Save & explore</button></div></header><aside class="workshop-panel"><div class="workshop-identity"><label>House name<input id="design-name" maxlength="48" value="${esc(w.layout.name)}"></label><label>Map<select id="design-map">${MAPS.map((m) => `<option value="${m.id}" ${m.id === w.layout.map ? "selected" : ""}>${esc(data.houses[m.id].name ?? m.name)}</option>`).join("")}</select></label><span class="design-save-state">${dirty ? "Unsaved changes" : "Saved on this browser"} · ${w.layout.props.length} / 64 objects</span></div><section class="design-tools"><label>Choose a room<select id="design-room">${data.houses[w.layout.map].rooms.map((r) => `<option ${r.name === w.room ? "selected" : ""} value="${r.name}">${r.name.replace(/([a-z])([A-Z])/g, "$1 $2")}</option>`).join("")}</select></label><form id="design-word-form"><label>Furnish from words<input id="design-words" maxlength="2048" placeholder="SOFA TABLE PLANT" autocomplete="off"></label><div class="design-row"><button type="button" class="quiet" id="design-preview">Preview word</button><button class="primary" type="submit">Furnish room</button></div></form><p class="design-tip">Choose a model to preview, then click a clear spot. Furnish room arranges your words for you.</p><div class="design-suggestions" aria-label="Room ideas"><button data-design-idea="reading"><strong>Cozy reading nook</strong><span>Books, soft seats & light</span></button><button data-design-idea="party"><strong>Garden party</strong><span>A table full of treats</span></button><button data-design-idea="study"><strong>Midnight study</strong><span>A quiet place to create</span></button></div><div class="design-catalog" aria-label="Furniture models">${objects.map((i) => `<button data-design-word="${i.id}" class="${prop?.word === i.id ? "selected" : ""}" title="Preview ${i.id.toLowerCase()}">${iconHtml(i.id)}<span>${i.id}</span></button>`).join("")}</div></section><section class="design-inspector"><label>Objects in your house<select id="design-selection"><option value="">Click an object to select it</option>${w.layout.props.map((p) => `<option value="${p.id}" ${p.id === w.selectedId ? "selected" : ""}>${p.word} · ${p.x}, ${p.z}</option>`).join("")}</select></label>${prop ? `<div class="inspector-title"><strong>${prop.word}</strong><span>${w.selectedId ? "Selected object" : "New object preview"}</span></div><div class="design-row coordinates"><label>Across<input type="number" step="0.5" id="prop-x" value="${prop.x}"></label><label>Along<input type="number" step="0.5" id="prop-z" value="${prop.z}"></label><label>Finish<select id="prop-skin">${["Classic", "Candy", "Arcade"].map((s) => `<option ${s === prop.skin ? "selected" : ""}>${s}</option>`).join("")}</select></label></div><div class="design-row"><button class="quiet" id="design-rotate">Rotate 90°</button><button class="primary" id="design-place">${w.selectedId ? "Apply changes" : "Place here"}</button></div><div class="design-row"><button class="quiet" id="design-clear">Deselect</button><button class="quiet" id="design-delete" ${!w.selectedId ? "disabled" : ""}>Remove object</button></div>` : '<p class="design-tip">All 40 supplied models are decor here. Click one to get started.</p>'}</section><div class="design-row history-row"><button class="quiet" id="design-undo" ${w.at === 0 ? "disabled" : ""}>Undo</button><button class="quiet" id="design-redo" ${w.at >= w.history.length - 1 ? "disabled" : ""}>Redo</button><button class="quiet" id="design-export">Export</button><button class="quiet" id="design-import-open">Import</button></div></aside><p id="workshop-message" class="workshop-message" role="status">Make room for something lovely. Doors and walking space stay clear.</p><nav class="workshop-camera" aria-label="House view"><button class="quiet" id="design-view-all">Whole house</button><button class="quiet" id="design-focus-room">Room view</button><button class="quiet" id="design-zoom-in" aria-label="Zoom in">+</button><button class="quiet" id="design-zoom-out" aria-label="Zoom out">−</button></nav></main>`;
  bindUi();
  document.querySelector("#design-map").onchange = (e) =>
    openWorkshop(e.target.value);
  document.querySelector("#design-room").onchange = (e) => {
    w.room = e.target.value;
    view.focusWorkshop(w.room);
  };
  document.querySelector("#design-name").onchange = (e) => {
    const candidate = structuredClone(w.layout);
    candidate.name = e.target.value.trim();
    commitDesign(candidate, "House renamed.", false);
  };
  document.querySelector("#design-word-form").onsubmit = (e) => {
    e.preventDefault();
    const words = document.querySelector("#design-words").value;
    if (!words.trim()) {
      workshopMessage(
        "Type one or more furniture words to furnish this room.",
        true,
      );
      return;
    }
    const result = addWords(w.layout, words, w.room, designData());
    if (!result.ok || !result.added.length) {
      workshopMessage(
        result.errors?.length
          ? designError(result.errors)
          : result.rejected.map((r) => `${r.word}: ${r.reason}`).join(" "),
        true,
      );
      return;
    }
    w.selectedId = result.added.at(-1).id;
    if (
      commitDesign(
        result.layout,
        `Added ${result.added.length} object${result.added.length === 1 ? "" : "s"}.${result.rejected.length ? " " + result.rejected.map((r) => `${r.word}: ${r.reason}`).join(" ") : ""}`,
      )
    )
      view.focusWorkshop(w.room);
  };
  document.querySelector("#design-preview").onclick = () => {
    const word = document
      .querySelector("#design-words")
      .value.trim()
      .toUpperCase();
    if (!word || /[\s,;]/.test(word)) {
      workshopMessage(
        "Preview one word at a time, or use Furnish room for a list.",
        true,
      );
      return;
    }
    previewDesignWord(word);
  };
  document.querySelectorAll("[data-design-idea]").forEach(
    (b) =>
      (b.onclick = () => {
        const ideas = {
            reading: ["LivingRoom", "SOFA TABLE LAMP BOOK PLANT"],
            party: [
              data.houses[w.layout.map].rooms.some((r) => r.name === "Garden")
                ? "Garden"
                : "Kitchen",
              "TABLE CHAIR CAKE MUG PLANT",
            ],
            study: ["Study", "DESK CHAIR BOOK CLOCK LAMP"],
          },
          [room, words] = ideas[b.dataset.designIdea];
        w.room = room;
        document.querySelector("#design-room").value = room;
        document.querySelector("#design-words").value = words;
        view.focusWorkshop(room);
        workshopMessage(
          "An idea to start with. Choose Furnish room to add it to your house.",
        );
      }),
  );
  document
    .querySelectorAll("[data-design-word]")
    .forEach(
      (b) => (b.onclick = () => previewDesignWord(b.dataset.designWord)),
    );
  document.querySelector("#design-selection").onchange = (e) =>
    selectDesignProp(e.target.value);
  for (const id of ["prop-x", "prop-z", "prop-skin"])
    document
      .querySelector(`#${id}`)
      ?.addEventListener("change", updateDesignGhost);
  document
    .querySelector("#design-place")
    ?.addEventListener("click", applyDesignGhost);
  document
    .querySelector("#design-rotate")
    ?.addEventListener("click", rotateDesignProp);
  document.querySelector("#design-delete")?.addEventListener("click", () => {
    const candidate = structuredClone(w.layout);
    candidate.props = candidate.props.filter((p) => p.id !== w.selectedId);
    commitDesign(candidate, "Object removed. Undo brings it back.");
  });
  document
    .querySelector("#design-clear")
    ?.addEventListener("click", () => selectDesignProp(null));
  document.querySelector("#design-undo").onclick = () => workshopHistory(-1);
  document.querySelector("#design-redo").onclick = () => workshopHistory(1);
  document.querySelector("#design-save").onclick = saveDesign;
  document.querySelector("#design-tour").onclick = tourWorkshop;
  document.querySelector("#design-export").onclick = () =>
    designTransfer("export");
  document.querySelector("#design-import-open").onclick = () =>
    designTransfer("import");
  document.querySelector("#design-view-all").onclick = () =>
    view.focusWorkshop(null);
  document.querySelector("#design-focus-room").onclick = () =>
    view.focusWorkshop(w.room);
  document.querySelector("#design-zoom-in").onclick = () =>
    view.zoomWorkshop(-150);
  document.querySelector("#design-zoom-out").onclick = () =>
    view.zoomWorkshop(150);
  refreshWorkshopView();
}

const actions = {
  workshop: () => openWorkshop(),
  "workshop-return": () => openWorkshop(workshop.layout.map),
  start,
  home,
  closet,
  lobbyPlay: () => home("play"),
  shop: () => home("shop"),
  lobbyRecipes: () => home("recipes"),
  leaderboard: () => home("leaderboard"),
  modes: () => {
    lobbyPicker = "mode";
    home("play");
  },
  maps: () => {
    lobbyPicker = "map";
    home("play");
  },
  closePicker: () => {
    lobbyPicker = null;
    home("play");
  },
  how,
  craft: () => drawer(),
  bag: () => setBag(!bagOpen),
  recipes: () => setBag(true, true),
  pause,
  sound: () => {
    muted = !muted;
    saveProfile();
    home(lobbyTab);
  },
  next: () => {
    game.nextRound();
    view.rebuild(game);
    resetLook();
    renderHud();
    lockMouse();
  },
  dodge: () => game.dodge(game.players[0]),
  jump: () => game.jump(game.players[0]),
  drop: () => {
    if (!game.drop(game.players[0])) toast("Your hand is empty.");
  },
  slot0: () => game.selectSlot(game.players[0], 0),
  slot1: () => game.selectSlot(game.players[0], 1),
};
function bindUi() {
  ui.querySelectorAll("[data-action]").forEach(
    (b) =>
      (b.onclick = () => {
        if (b.dataset.action !== "closet") view.closet = false;
        actions[b.dataset.action]?.();
      }),
  );
  ui.querySelectorAll("[data-hold]").forEach((b) => {
    b.onpointerdown = (e) => {
      e.preventDefault();
      b.setPointerCapture(e.pointerId);
      input[b.dataset.hold] = true;
      if (b.dataset.hold === "attack" && e.pointerType === "touch" && aimsAtTargets())
        autoAim();
    };
    b.onpointerup = b.onpointercancel = () => (input[b.dataset.hold] = false);
  });
}
function aimsAtTargets() {
  const p = game.players[0],
    held = game.held(p),
    def = held?.origin === "map" ? null : held?.definition;
  return !def?.use && !(def?.deploy && !def?.thrown) && !game.placesObjective(p, held);
}
function autoAim() {
  const p = game.players[0],
    target = [
      ...game.players.filter(
        (q) => q.id !== 0 && q.team !== p.team && q.state === "alive",
      ),
      ...game.items.filter((i) => i.state === "world"),
    ].sort((a, b) => distance(a, p) - distance(b, p))[0];
  if (target) {
    input.aim = { x: target.x, z: target.z };
    view.look.yaw = Math.atan2(target.x - p.x, target.z - p.z);
  }
}
function bindJoystick() {
  const joy = document.querySelector("#joystick"),
    knob = joy.querySelector(".joystick-knob");
  const move = (e) => {
    const r = joy.getBoundingClientRect(),
      x = (e.clientX - r.left - r.width / 2) / 45,
      y = (e.clientY - r.top - r.height / 2) / 45,
      l = Math.max(1, Math.hypot(x, y));
    touch.x = x / l;
    touch.y = -y / l;
    knob.style.transform = `translate(${touch.x * 35}px,${-touch.y * 35}px)`;
  };
  joy.onpointerdown = (e) => {
    e.preventDefault();
    joy.setPointerCapture(e.pointerId);
    move(e);
  };
  joy.onpointermove = (e) => {
    if (joy.hasPointerCapture(e.pointerId)) move(e);
  };
  joy.onpointerup = joy.onpointercancel = () => {
    touch.x = touch.y = 0;
    knob.style.transform = "";
  };
}
window.addEventListener("keydown", (e) => {
  if (e.target.matches("input,select,textarea")) return;
  if (screen === "home" && lobbyPicker && e.code === "Escape") {
    e.preventDefault();
    return actions.closePicker();
  }
  if (screen === "workshop") {
    if ((e.ctrlKey || e.metaKey) && ["KeyZ", "KeyY"].includes(e.code)) {
      e.preventDefault();
      workshopHistory(e.code === "KeyY" || e.shiftKey ? 1 : -1);
    } else if (e.code === "Delete" && workshop.selectedId)
      document.querySelector("#design-delete").click();
    else if (e.code === "Escape") selectDesignProp(null);
    return;
  }
  if (screen === "tour") {
    if (
      ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Space"].includes(
        e.code,
      )
    )
      e.preventDefault();
    if (e.code === "Escape") openWorkshop(workshop.layout.map);
    else if (
      [
        "KeyW",
        "KeyA",
        "KeyS",
        "KeyD",
        "ArrowUp",
        "ArrowDown",
        "ArrowLeft",
        "ArrowRight",
      ].includes(e.code)
    )
      keys.add(e.code);
    return;
  }
  if (screen !== "game") return;
  if (
    ["Tab", " ", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(
      e.key,
    )
  )
    e.preventDefault();
  if (e.repeat) return;
  if ((paused || game.status !== "playing") && e.code !== "Escape") return;
  keys.add(e.code);
  if (e.code === "KeyQ" || e.code === "KeyC") {
    e.preventDefault();
    drawer();
    return;
  }
  if (e.code === "Tab") {
    if (!craftOpen && !paused) setBag(true);
    return;
  }
  if (e.code === "Escape" && bagOpen) {
    setBag(false);
    return;
  }
  if (e.code === "Escape" && craftOpen) {
    drawer();
    return;
  }
  if (e.code === "Escape") {
    if (game.players[0].craft) game.cancelCraft(game.players[0]);
    pause();
  }
  if (craftOpen || paused) return;
  const keyActions = {
    Space: "jump",
    ShiftLeft: "dodge",
    ShiftRight: "dodge",
    KeyR: "drop",
    Digit1: "slot0",
    Digit2: "slot1",
  };
  if (keyActions[e.code]) actions[keyActions[e.code]]();
});
window.addEventListener("keyup", (e) => {
  keys.delete(e.code);
  if (e.code === "Tab" && bagOpen) setBag(false);
});
window.addEventListener("blur", () => {
  resetInputs();
  if (screen === "game" && game.status === "playing" && !paused) pause();
});
const upright = matchMedia("(orientation: portrait) and (pointer: coarse)");
upright.addEventListener("change", () => {
  if (!upright.matches) return;
  resetInputs();
  if (screen === "game" && game.status === "playing" && !paused) pause();
});
window.addEventListener("contextmenu", (e) => e.preventDefault());
viewCanvasEvents();
function viewCanvasEvents() {
  const canvas = document.querySelector("#world");
  let drag = null,
    look = null;
  const pointGhost = (point) => {
    if (!workshop.ghost) return;
    workshop.ghost.x = Math.round(point.x * 2) / 2;
    workshop.ghost.z = Math.round(point.z * 2) / 2;
    document.querySelector("#prop-x").value = workshop.ghost.x;
    document.querySelector("#prop-z").value = workshop.ghost.z;
    updateDesignGhost();
  };
  canvas.addEventListener("pointermove", (e) => {
    if (screen === "workshop") {
      if (drag?.id === e.pointerId) {
        if (
          drag.pan ||
          Math.hypot(e.clientX - drag.startX, e.clientY - drag.startY) > 5
        ) {
          drag.pan = true;
          view.panWorkshop(e.clientX - drag.x, e.clientY - drag.y);
          drag.x = e.clientX;
          drag.y = e.clientY;
        }
      } else if (workshop.ghost && !workshop.selectedId) {
        const picked = view.pickWorkshop(e.clientX, e.clientY);
        if (picked?.point) pointGhost(picked.point);
      }
      return;
    }
    if (look?.id === e.pointerId) {
      turnLook((e.clientX - look.x) * 0.006, (e.clientY - look.y) * 0.004);
      look.x = e.clientX;
      look.y = e.clientY;
    }
  });
  canvas.addEventListener("pointerdown", (e) => {
    canvas.setPointerCapture(e.pointerId);
    if (screen === "workshop") {
      drag = {
        id: e.pointerId,
        startX: e.clientX,
        startY: e.clientY,
        x: e.clientX,
        y: e.clientY,
        pan: e.button === 2 || e.altKey,
      };
      return;
    }
    if ((screen !== "game" && screen !== "tour") || paused || craftOpen) return;
    if (e.pointerType !== "mouse") {
      look = { id: e.pointerId, x: e.clientX, y: e.clientY };
      return;
    }
    if (!mouseLocked() && lockMouse()) return;
    if (screen !== "game") return;
    if (e.button === 2) input.block = true;
    else if (e.button === 0) input.attack = true;
  });
  canvas.addEventListener("pointerup", (e) => {
    if (screen !== "workshop" || !drag || drag.id !== e.pointerId) return;
    const moved = drag.pan;
    drag = null;
    if (moved) return;
    const picked = view.pickWorkshop(e.clientX, e.clientY);
    if (picked?.designId) selectDesignProp(picked.designId);
    else if (workshop.ghost && picked?.point) {
      pointGhost(picked.point);
      applyDesignGhost();
    } else selectDesignProp(null);
  });
  canvas.addEventListener("pointercancel", () => {
    drag = look = null;
  });
  canvas.addEventListener("lostpointercapture", (e) => {
    if (look?.id === e.pointerId) look = null;
  });
  document.addEventListener("mousemove", (e) => {
    if (paused || craftOpen || bagOpen) return;
    if (screen !== "game" && screen !== "tour") return;
    if (!mouseLocked() && e.target !== canvas) return;
    turnLook(e.movementX * MOUSE_SENSITIVITY, e.movementY * MOUSE_SENSITIVITY);
  });
  document.addEventListener("pointerlockchange", () => {
    document.body.classList.toggle("mouse-locked", mouseLocked());
    if (mouseLocked()) {
      lockRefusals = 0;
      lockPending = false;
    }
    if (
      !mouseLocked() &&
      screen === "game" &&
      game?.status === "playing" &&
      !paused &&
      !bagOpen &&
      !craftOpen
    )
      pause();
  });
  canvas.addEventListener(
    "wheel",
    (e) => {
      if (screen === "workshop") {
        e.preventDefault();
        view.zoomWorkshop(e.deltaY);
      }
    },
    { passive: false },
  );
  window.addEventListener("pointerup", (e) => {
    if (e.button === 2) input.block = false;
    else if (e.target === canvas) input.attack = false;
  });
}

function animate(now) {
  const dt = Math.min((now - last) / 1000, 0.25);
  last = now;
  if (game && view) {
    if ((screen === "game" || screen === "tour") && !paused) {
      if (!craftOpen) {
        const sx =
            touch.x +
            (keys.has("KeyD") || keys.has("ArrowRight") ? 1 : 0) -
            (keys.has("KeyA") || keys.has("ArrowLeft") ? 1 : 0),
          sy =
            touch.y +
            (keys.has("KeyW") || keys.has("ArrowUp") ? 1 : 0) -
            (keys.has("KeyS") || keys.has("ArrowDown") ? 1 : 0);
        const yaw = view.look.yaw,
          fx = Math.sin(yaw),
          fz = Math.cos(yaw);
        input.x = -fz * sx + fx * sy;
        input.z = fx * sx + fz * sy;
        input.yaw = screen === "game" ? yaw : undefined;
        input.attack ||= keys.has("KeyJ");
        input.block ||= keys.has("KeyK");
        input.interact ||= keys.has("KeyE");
        for (let remaining = dt; remaining > 0; remaining -= 0.025)
          game.tick(Math.min(0.025, remaining), input);
        if (keys.has("KeyJ")) input.attack = false;
        if (keys.has("KeyK")) input.block = false;
        if (keys.has("KeyE")) input.interact = false;
      } else
        for (let remaining = dt; remaining > 0; remaining -= 0.025)
          game.tick(Math.min(0.025, remaining), {});
    } else if (screen !== "game")
      for (let remaining = dt; remaining > 0; remaining -= 0.025)
        game.tick(Math.min(0.025, remaining), {});
    const events = game.events.splice(0);
    view.handleEvents(events);
    for (const e of events) {
      if (e.player === undefined || e.player === 0) sound(e.type);
      if (e.type === "warning") toast(`Movers incoming: leave ${e.room}!`);
      if (e.type === "keepsake") toast("A keepsake saved! Find the others.");
      if (e.type === "objective") toast(`${e.word} in its new home. Lovely!`);
      if (e.type === "refused" && e.player === 0) toast(e.message);
    }
    if (now - hudAt > 120) {
      updateHud();
      hudAt = now;
    }
    view.update(game, dt);
    placeLobbyTags();
  }
  requestAnimationFrame(animate);
}
window.addEventListener("resize", () => {
  if (game && view && ["home", "closet"].includes(screen)) stageLobby(lobbyTab);
});
async function boot() {
  ui.innerHTML =
    '<div class="loading"><div class="loading-tile">W</div><span class="eyebrow">UNPACKING A LITTLE MAYHEM</span><h1>Making room for imagination.</h1><p id="load-progress">Opening the house…</p></div>';
  try {
    const [rules, items, house, courtyard, wardrobe] = await Promise.all(
      ["rules", "items", "house_pinwheel", "house_courtyard", "wardrobe"].map(
        fetchData,
      ),
    );
    data = {
      rules,
      items,
      house,
      wardrobe,
      houses: { pinwheel: house, courtyard },
    };
    profile = structuredClone(wardrobe.default);
    try {
      const saved = JSON.parse(localStorage.getItem("wreckabulary.profile.v1"));
      if (saved?.wardrobe?.pieces && saved.wardrobe.colours) {
        const pieces = { ...saved.wardrobe.pieces };
        for (const slot of wardrobe.requiredSlots) pieces[slot] ??= profile.pieces[slot];
        if (pieces.Headwear === "Hood" && pieces.Top !== "Hoodie") pieces.Top = "Hoodie";
        profile = {
          pieces,
          colours: { ...profile.colours, ...saved.wardrobe.colours },
        };
      }
      if (["Classic", "Candy", "Arcade"].includes(saved?.skin))
        skin = saved.skin;
      muted = !!saved?.muted;
    } catch {}
    let savedProgress = null;
    try {
      savedProgress = JSON.parse(localStorage.getItem("wreckabulary.progress.v1"));
    } catch {}
    progress = loadProgress(savedProgress, { skin, colour: profile.colours?.Top });
    saveProgress();
    view = new WorldView(document.querySelector("#world"), data);
    await view.init(
      (n) =>
        (document.querySelector("#load-progress").textContent =
          `Unpacking real 3D objects… ${n}`),
    );
    for (const item of items.items.filter((i) => i.enabled))
      await view.icon(item.id);
    home();
    window.addEventListener("resize", () => {
      view.resize();
      if (screen === "workshop") refreshWorkshopView();
    });
    window.wreckabulary = {
      get game() {
        return game;
      },
      get data() {
        return data;
      },
      start: (options = {}) => {
        mode = options.mode ?? mode;
        map = options.map ?? map;
        start();
      },
      view,
      get workshop() {
        return workshop
          ? {
              layout: structuredClone(workshop.layout),
              selectedId: workshop.selectedId,
              ghost: structuredClone(workshop.ghost),
              at: workshop.at,
              dirty: designText(workshop.layout) !== workshop.savedText,
            }
          : null;
      },
      get screen() {
        return screen;
      },
    };
    requestAnimationFrame(animate);
  } catch (error) {
    ui.innerHTML = `<div class="loading"><h1>The house needs a hand.</h1><p>${esc(error.message)}</p><button class="primary" onclick="location.reload()">TRY AGAIN →</button></div>`;
    console.error(error);
  }
}
boot();
