import "./style.css";
import { Game, MODES, MAPS, canSpell, distance } from "./engine.js";
import { WorldView } from "./renderer.js";
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
  paused = false,
  muted = false,
  last = performance.now(),
  hudAt = 0,
  profile,
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
  `<img class="item-image ${cls}" data-icon="${word}" alt="${word.toLowerCase()}" src="${view?.icons.get(word) ?? ""}">`;
const ACTION_CROPS = {
  smash: [34, 19, 391, 376],
  dodge: [453, 100, 369, 286],
  jump: [925, 28, 280, 373],
  craft: [39, 445, 365, 336],
  block: [499, 434, 313, 353],
  interact: [874, 439, 333, 346],
  deploy: [35, 833, 381, 375],
  drop: [487, 813, 281, 405],
  throw: [836, 870, 410, 314],
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
}
function start() {
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
  renderHud();
  toast(
    mode === "MovingOut"
      ? `Carry ${game.keepsakes.length} keepsakes to the van. Drop them inside the circle.`
      : "Break furniture → collect its letters → spell new gear.",
  );
  sound("craft");
}
function home() {
  resetInputs();
  screen = "home";
  craftOpen = false;
  paused = false;
  game = new Game(data, { preview: true, map, wardrobe: profile });
  view.rebuild(game);
  ui.innerHTML = `<main class="home"><div class="home-top"><span class="eyebrow">A LITTLE WORDPLAY. A LOT OF MAYHEM.</span><button class="quiet" data-action="sound" aria-label="Toggle sound">${muted ? "Sound off" : "Sound on"}</button></div><section class="home-copy"><div class="logo"><span>WRECK</span><span>ABULARY<span class="logo-dot">!</span></span></div><p class="lede">Make a mess.<br>Make a word.<br><em>Make your move.</em></p><div class="home-pills"><span>100 HP</span><span>18 letters</span><span>Endless mischief</span></div></section><section class="play-card"><div class="card-top"><span class="eyebrow">TODAY’S HOUSE PARTY</span><button class="link" data-action="closet">Your wardrobe +</button></div><div class="mode-grid">${Object.entries(
    MODES,
  )
    .map(
      ([id, m]) =>
        `<button class="mode-card ${id === mode ? "selected" : ""}" data-mode="${id}" aria-pressed="${id === mode}"><span class="mode-emoji">${actionArt({ Dibs: "smash", Duos: "throw", MovingOut: "dodge", MovingDay: "deploy", Tutorial: "craft" }[id], 25)}</span><span><strong>${m.title}</strong><small>${m.tag}</small></span></button>`,
    )
    .join(
      "",
    )}</div><p class="mode-description">${MODES[mode].description}</p><div class="map-row"><label for="map-choice">The playground</label><select id="map-choice">${MAPS.map((m) => `<option value="${m.id}" ${m.id === map ? "selected" : ""}>${m.name}</option>`).join("")}</select></div><button class="primary start" data-action="start">LET’S MAKE A MESS <span>→</span></button><p class="fineprint">Solo with clever little AI housemates · keyboard, mouse or touch</p></section><div class="home-footer"><span>BREAK IT. SPELL IT. BRING IT.</span><button class="link" data-action="how">How to play +</button></div></main>`;
  bindUi();
}
function renderHud() {
  const m = MODES[game.mode];
  ui.innerHTML = `<div class="hud"><header class="hud-top"><button class="brand-small" data-action="pause" aria-label="Pause game">W<span>!</span></button><div class="match-label"><span class="eyebrow">${m.tag}</span><strong>${m.title}</strong><span id="round-text"></span></div><div class="objective-top" id="objective-top"></div><div class="timer" id="timer">2:30</div><button class="quiet pause-button" data-action="pause" aria-label="Pause"><svg width="14" height="16" viewBox="0 0 14 16" aria-hidden="true"><path fill="currentColor" d="M1 1h4v14H1zm8 0h4v14H9z"/></svg></button></header><div class="room-pill" id="room-pill">Bedroom</div><div class="minimap" id="minimap" aria-label="House map"></div><div class="status-message" id="status-message"></div><section class="vitals"><div class="player-name">YOU <span id="shield-text"></span></div><div class="hp-label"><strong id="hp-value">100</strong><span>HP</span></div><div class="health-track"><div id="health-fill"></div></div><div class="equipment"><button data-action="slot0" class="gear-slot" id="slot0"><kbd>1</kbd><span>Empty hand</span></button><button data-action="slot1" class="gear-slot" id="slot1"><kbd>2</kbd><span>Empty hand</span></button><button class="drop-button" data-action="drop" title="Drop equipped item (R)">${actionArt("drop", 20)}<span>Drop</span></button></div></section><section class="letter-tray"><div class="tray-header"><span>YOUR LETTERS</span><strong id="bag-count">0 / 18</strong><button class="link" data-action="craft">Recipes <kbd>Q</kbd></button></div><div id="letters"><p>Smash furniture to find your first word.</p></div><div id="craft-progress"></div></section><section class="actions"><button class="action small" data-hold="block" id="block-action">${actionArt("block")}<small>BLOCK</small><kbd>RMB</kbd></button><button class="action small" data-hold="interact" id="interact-action">${actionArt("interact")}<small>INTERACT</small><kbd>E</kbd></button><button class="action attack" data-hold="attack" id="attack-action"><span id="attack-icon">${actionArt("smash", 42)}</span><small id="attack-label">SMASH</small><kbd>LMB</kbd></button><button class="action small" data-action="jump" id="jump-action">${actionArt("jump")}<small>JUMP</small><kbd>Space</kbd></button><button class="action small" data-action="dodge" id="dodge-action">${actionArt("dodge")}<small>DODGE</small><kbd>Shift</kbd></button><button class="action craft-button" data-action="craft">${actionArt("craft", 34)}<small>SPELL</small><kbd>Q</kbd></button><button class="action small deploy-action" data-action="deploy">${actionArt("deploy")}<small>PLACE / USE</small><kbd>F</kbd></button><button class="action small throw-action" data-action="throw">${actionArt("throw")}<small>THROW</small><kbd>G</kbd></button></section><div class="joystick" id="joystick" aria-label="Touch movement joystick"><div class="joystick-knob"></div></div><p class="control-hint"><kbd>WASD</kbd> move <span>·</span> mouse aim <span>·</span> <kbd>Q</kbd> spell <span>·</span> <kbd>E</kbd> interact <span>·</span> <kbd>Esc</kbd> pause</p></div><div id="drawer-root"></div><div id="modal-root"></div>`;
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
  document.querySelector("#health-fill").style.width =
    `${(p.hp / p.maxHp) * 100}%`;
  document.querySelector("#health-fill").classList.toggle("low", p.hp < 30);
  document.querySelector("#shield-text").textContent =
    p.bubble > 0 ? `+ ${Math.ceil(p.bubble)} BUBBLE` : "";
  document.querySelector("#bag-count").textContent =
    `${p.bag.length + (p.craft?.word.length ?? 0)} / ${game.rules.maxLetters}`;
  const letters = document.querySelector("#letters"),
    bag = [...p.bag].sort().join("");
  if (letters.dataset.bag !== bag) {
    letters.dataset.bag = bag;
    letters.innerHTML = bag
      ? [...bag]
          .map(
            (c) =>
              `<button class="letter" data-letter="${c}" title="Toss ${c} (free bag space)">${c}</button>`,
          )
          .join("")
      : "<p>Smash furniture to find your first word.</p>";
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
  document.querySelector("#attack-label").textContent =
    held?.origin === "map"
      ? "TOSS"
      : held?.definition?.use
        ? "USE"
        : held?.definition?.thrown
          ? "THROW"
          : held?.definition?.deploy && !held?.definition?.melee
            ? "PLACE"
            : "SMASH";
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
  else
    objective.innerHTML = `<span class="eyebrow">THE HOUSEMATES</span><strong>${game.players.filter((p) => p.state === "alive").length} / ${game.players.length} UP & ABOUT</strong>`;
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
    status.innerHTML = `<strong>Carrying ${held?.word}</strong> Drop with R inside the van circle · G to throw`;
  else if (game.mode === "Tutorial")
    status.innerHTML =
      game.stats.broken === 0
        ? "Step 1 · Walk to a piece of furniture. Aim and smash it."
        : game.stats.collected === 0
          ? "Step 2 · Walk over the letter tiles."
          : game.stats.crafted === 0
            ? "Step 3 · Open SPELL and craft an available word."
            : "Nice wordwork! Try placing, throwing, dodging and different recipes.";
  else status.innerHTML = "";
  updateMinimap();
  if (craftOpen) updateRecipes();
  if (
    ["roundOver", "finished"].includes(game.status) &&
    !document.querySelector(".result-card")
  )
    result();
}
function updateMinimap() {
  const root = document.querySelector("#minimap"),
    extent = game.extent;
  root.innerHTML = `${game.house.rooms
    .map((r) => {
      const b = r.bounds,
        state = game.roomStatus.get(r.name);
      return `<div class="map-room ${state?.closed ? "closed" : state?.warning ? "warning" : ""}" style="left:${((b[0] + extent) / extent) * 50}%;top:${((b[1] + extent) / extent) * 50}%;width:${((b[2] - b[0]) / extent) * 50}%;height:${((b[3] - b[1]) / extent) * 50}%"><span>${r.name[0]}</span></div>`;
    })
    .join("")}${game.players
    .filter((p) => p.state !== "eliminated")
    .map(
      (p) =>
        `<i class="map-player ${p.id === 0 ? "you" : ""}" style="left:${((p.x + extent) / extent) * 50}%;top:${((p.z + extent) / extent) * 50}%"></i>`,
    )
    .join("")}${game.keepsakes
    .filter((k) => !k.collected)
    .map(
      (k) =>
        `<i class="map-keepsake" style="left:${((k.x + extent) / extent) * 50}%;top:${((k.z + extent) / extent) * 50}%">◆</i>`,
    )
    .join("")}`;
}
function drawer() {
  craftOpen = !craftOpen;
  resetInputs();
  const root = document.querySelector("#drawer-root");
  if (!craftOpen) {
    root.innerHTML = "";
    return;
  }
  root.innerHTML = `<section class="craft-drawer"><div class="drawer-title"><div><span class="eyebrow">YOUR LITTLE SPELLBOOK</span><h2>Letters become possibilities.</h2></div><button class="close" data-action="craft" aria-label="Close crafting">×</button></div><form id="spell-form"><label for="spell-word" class="sr-only">Type a recipe word</label><input id="spell-word" maxlength="18" placeholder="Type a word… BAT, BOMB, FOAM" autocomplete="off" autocapitalize="characters" spellcheck="false"><button class="primary" type="submit">SPELL →</button></form><p class="recipe-note">Only your loose letters can be used. Recipes reserve letters until finished; cancel with Esc.</p><div class="recipe-grid" id="recipe-grid"></div></section>`;
  bindUi();
  document.querySelector("#spell-form").onsubmit = (e) => {
    e.preventDefault();
    craftWord(document.querySelector("#spell-word").value);
  };
  updateRecipes(true);
}
function updateRecipes(force = false) {
  const p = game.players[0],
    grid = document.querySelector("#recipe-grid");
  if (!grid) return;
  const signature = p.bag + !!p.craft + p.slots.join(",");
  if (!force && grid.dataset.signature === signature) return;
  grid.dataset.signature = signature;
  grid.innerHTML = data.items.items
    .filter((i) => i.enabled)
    .map((item) => {
      const available =
        canSpell(p.bag, item.id) && !p.craft && game.freeSlot(p) >= 0;
      const copy = [...p.bag];
      const missing = [...item.id]
        .map((c) => {
          const index = copy.indexOf(c);
          if (index >= 0) {
            copy.splice(index, 1);
            return `<span class="got">${c}</span>`;
          }
          return `<span>${c}</span>`;
        })
        .join("");
      return `<button class="recipe ${available ? "available" : ""}" data-recipe="${item.id}" aria-label="Spell ${item.id}, ${available ? "available" : "missing letters"}">${iconHtml(item.id)}<div class="recipe-text"><strong>${item.id}</strong><small>${{ MeleeSwing: "A satisfying swing", MeleeThrust: "A little extra reach", Thrown: item.id === "BOMB" ? "A 2.5 second surprise" : "Catch. Throw. Repeat.", Buff: "A protective bubble", Shield: "Frontal block · hold RMB", DeployPad: "A bouncy jump pad", DeploySpeed: "A speedy little shortcut", DeployZone: "A slippery surprise", DeployCover: "Make your own cover" }[item.family] ?? item.family}</small><div class="recipe-letters">${missing}</div></div><span class="recipe-arrow">${available ? "↗" : "+"}</span></button>`;
    })
    .join("");
  grid
    .querySelectorAll("[data-recipe]")
    .forEach((b) => (b.onclick = () => craftWord(b.dataset.recipe)));
}
function craftWord(word) {
  const error = game.craft(game.players[0], word.trim());
  if (error) {
    toast(error);
    return;
  }
  toast(`Spelling ${word.toUpperCase()}…`);
  drawer();
}
function result() {
  resetInputs();
  craftOpen = false;
  document.querySelector("#drawer-root").innerHTML = "";
  const win = game.winner === game.players[0].team;
  document.querySelector("#modal-root").innerHTML =
    `<div class="modal-shade"><section class="result-card"><span class="result-spark">${win ? "✦" : "→"}</span><span class="eyebrow">${game.status === "roundOver" ? `ROUND ${game.round} COMPLETE` : "HOUSE PARTY COMPLETE"}</span><h2>${game.result}</h2><p>${win ? "A little imagination goes a long way." : "Every good mess teaches you a new trick."}</p><div class="result-stats"><div><strong>${game.stats.broken}</strong><span>objects wrecked</span></div><div><strong>${game.stats.crafted}</strong><span>words crafted</span></div><div><strong>${Math.ceil(game.stats.damage)}</strong><span>damage dealt</span></div></div><button class="primary" data-action="${game.status === "roundOver" ? "next" : "start"}">${game.status === "roundOver" ? "NEXT ROUND" : "PLAY AGAIN"} →</button><button class="quiet" data-action="home">Back to the house party</button></section></div>`;
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
  document.querySelector("#modal-root").innerHTML = paused
    ? `<div class="modal-shade"><section class="result-card"><span class="eyebrow">TAKE A BREATHER</span><h2>The mess can wait.</h2><p>${MODES[game.mode].description}</p><button class="primary" data-action="pause">KEEP PLAYING →</button><button class="quiet" data-action="how">Controls & recipes</button><button class="quiet" data-action="home">Back to the house party</button></section></div>`
    : "";
  bindUi();
}
function how() {
  const previous = screen;
  const layer = document.createElement("div");
  layer.className = "modal-shade help-shade";
  layer.innerHTML = `<section class="help-card"><button class="close" id="help-close" aria-label="Close instructions">×</button><span class="eyebrow">A HOUSE FULL OF POSSIBILITIES</span><h2>Everything starts with a word.</h2><div class="how-steps"><div><b>01</b><strong>Break it.</strong><p>Smash original furniture. A TABLE breaks into T, A, B, L, E.</p></div><div><b>02</b><strong>Spell it.</strong><p>Walk over letters. Your bag holds 18. Use SPELL to craft any of the 12 enabled recipes.</p></div><div><b>03</b><strong>Bring it.</strong><p>Attack with gear, throw it, or place a tool. Reusable gear returns its letters when broken. Consumables spend them.</p></div></div><div class="key-table"><span><kbd>WASD / arrows</kbd> move</span><span><kbd>Mouse / touch</kbd> aim</span><span><kbd>LMB / J</kbd> smash / use</span><span><kbd>RMB / K</kbd> block with PLATE</span><span><kbd>Shift</kbd> dodge</span><span><kbd>Space</kbd> jump</span><span><kbd>Q / C</kbd> spell</span><span><kbd>Tab / 1 / 2</kbd> switch hand</span><span><kbd>E</kbd> pickup / hold to revive</span><span><kbd>F</kbd> place / use</span><span><kbd>G</kbd> throw gear</span><span><kbd>R</kbd> drop gear</span></div><p class="fineprint">Health is 100 HP. Hits never remove letters. Cosmetics change your look, never your stats. The Movers announce room closures before dealing hazard damage.</p><button class="primary" id="help-done">GOT IT. LET’S PLAY. →</button></section>`;
  ui.append(layer);
  const close = () => layer.remove();
  layer.querySelector("#help-close").onclick = close;
  layer.querySelector("#help-done").onclick = close;
}
function closet() {
  screen = "closet";
  ui.innerHTML = `<main class="closet"><button class="quiet" data-action="home">← Back to the party</button><section class="closet-card"><span class="eyebrow">LOOK GOOD. MAKE MISCHIEF.</span><h1>Your little housemate.</h1><p>Pick the pieces. Find your colour. All style, zero stat changes.</p><div class="closet-controls"><label>Top<select id="outfit-top">${data.wardrobe.pieces
    .filter((p) => p.slot === "Top")
    .map(
      (p) =>
        `<option ${profile.pieces.Top === p.id ? "selected" : ""}>${p.id}</option>`,
    )
    .join(
      "",
    )}</select></label><label>Headwear<select id="outfit-head"><option value="">Bare head</option>${data.wardrobe.pieces
    .filter((p) => p.slot === "Headwear")
    .map(
      (p) =>
        `<option ${profile.pieces.Headwear === p.id ? "selected" : ""}>${p.id}</option>`,
    )
    .join(
      "",
    )}</select></label></div><span class="eyebrow">YOUR SIGNATURE COLOUR</span><div class="swatches">${data.wardrobe.palettes.Top.map((c) => `<button data-color="${c.id}" class="swatch ${profile.colours.Top === c.id ? "selected" : ""}" style="background:rgb(${c.rgb.map((n) => n * 255).join(",")})" title="${c.name}" aria-label="${c.name}"></button>`).join("")}</div><div class="accessories">${[
    ["Glasses", "Face"],
    ["Satchel", "Back"],
    ["TBadge", "Badge"],
  ]
    .map(
      ([id, slot]) =>
        `<label><input type="checkbox" data-piece="${id}" data-slot="${slot}" ${profile.pieces[slot] === id ? "checked" : ""}>${id === "TBadge" ? "Letter badge" : id}</label>`,
    )
    .join(
      "",
    )}</div><label class="skin-select">Crafted gear style<select id="skin-choice">${["Classic", "Candy", "Arcade"].map((s) => `<option ${skin === s ? "selected" : ""}>${s}</option>`).join("")}</select></label><p class="fineprint">Original and deployed furniture keeps its Classic look. Gear wears your chosen style while held or flying.</p><button class="primary" data-action="home">THAT’S MY LOOK →</button></section></main>`;
  bindUi();
  const update = () => {
    if (profile.pieces.Headwear === "Hood" && profile.pieces.Top !== "Hoodie")
      profile.pieces.Top = "Hoodie";
    saveProfile();
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
  game.players[0].x = -5;
  game.players[0].z = -2;
  game.players[0].yaw = Math.PI;
  view.closet = true;
}
const actions = {
  start,
  home,
  closet,
  how,
  craft: drawer,
  pause,
  sound: () => {
    muted = !muted;
    saveProfile();
    home();
  },
  next: () => {
    game.nextRound();
    view.rebuild(game);
    renderHud();
  },
  dodge: () => game.dodge(game.players[0]),
  jump: () => game.jump(game.players[0]),
  drop: () => {
    if (!game.drop(game.players[0])) toast("Your hand is empty.");
  },
  throw: () => {
    if (!game.throw(game.players[0])) toast("Equip something to throw.");
  },
  deploy: () => {
    const p = game.players[0];
    if (p.carried === null && game.held(p)?.definition?.use) game.use(p);
    else if (p.carried !== null) game.drop(p);
    else {
      const error = game.deploy(p);
      if (error) toast(error);
    }
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
  ui.querySelectorAll("[data-mode]").forEach(
    (b) =>
      (b.onclick = () => {
        mode = b.dataset.mode;
        home();
      }),
  );
  ui.querySelector("#map-choice")?.addEventListener("change", (e) => {
    map = e.target.value;
    home();
  });
  ui.querySelectorAll("[data-hold]").forEach((b) => {
    b.onpointerdown = (e) => {
      e.preventDefault();
      b.setPointerCapture(e.pointerId);
      input[b.dataset.hold] = true;
      if (b.dataset.hold === "attack" && e.pointerType === "touch") autoAim();
    };
    b.onpointerup = b.onpointercancel = () => (input[b.dataset.hold] = false);
  });
}
function autoAim() {
  const p = game.players[0],
    target = [
      ...game.players.filter(
        (q) => q.id !== 0 && q.team !== p.team && q.state === "alive",
      ),
      ...game.items.filter((i) => i.state === "world"),
    ].sort((a, b) => distance(a, p) - distance(b, p))[0];
  if (target) input.aim = { x: target.x, z: target.z };
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
  if (screen !== "game" || e.target.matches("input,select")) return;
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
    drawer();
    return;
  }
  if (e.code === "Tab") {
    if (!craftOpen && !paused)
      game.selectSlot(game.players[0], 1 - game.players[0].slot);
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
    KeyF: "deploy",
    KeyG: "throw",
    Digit1: "slot0",
    Digit2: "slot1",
  };
  if (keyActions[e.code]) actions[keyActions[e.code]]();
});
window.addEventListener("keyup", (e) => keys.delete(e.code));
window.addEventListener("blur", () => {
  resetInputs();
  if (screen === "game" && game.status === "playing" && !paused) pause();
});
window.addEventListener("contextmenu", (e) => e.preventDefault());
viewCanvasEvents();
function viewCanvasEvents() {
  const canvas = document.querySelector("#world");
  canvas.addEventListener("pointermove", (e) => {
    if (e.pointerType === "mouse") pointerAim = { x: e.clientX, y: e.clientY };
  });
  canvas.addEventListener("pointerdown", (e) => {
    canvas.setPointerCapture(e.pointerId);
    if (screen !== "game" || paused || craftOpen) return;
    if (e.button === 2) input.block = true;
    else input.attack = true;
    pointerAim = { x: e.clientX, y: e.clientY };
  });
  window.addEventListener("pointerup", (e) => {
    if (e.button === 2) input.block = false;
    else if (e.target === canvas) input.attack = false;
  });
}
function animate(now) {
  const dt = Math.min((now - last) / 1000, 0.25);
  last = now;
  if (game && view) {
    if (screen === "game" && !paused) {
      if (!craftOpen) {
        const sx =
            touch.x +
            (keys.has("KeyD") || keys.has("ArrowRight") ? 1 : 0) -
            (keys.has("KeyA") || keys.has("ArrowLeft") ? 1 : 0),
          sy =
            touch.y +
            (keys.has("KeyW") || keys.has("ArrowUp") ? 1 : 0) -
            (keys.has("KeyS") || keys.has("ArrowDown") ? 1 : 0);
        input.x = sx * 0.8 - sy * 0.6;
        input.z = -sx * 0.6 - sy * 0.8;
        if (pointerAim) input.aim = view.aim(pointerAim.x, pointerAim.y);
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
    }
    if (now - hudAt > 120) {
      updateHud();
      hudAt = now;
    }
    view.update(game, dt);
  }
  requestAnimationFrame(animate);
}
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
        profile = {
          pieces: { ...profile.pieces, ...saved.wardrobe.pieces },
          colours: { ...profile.colours, ...saved.wardrobe.colours },
        };
      }
      if (["Classic", "Candy", "Arcade"].includes(saved?.skin))
        skin = saved.skin;
      muted = !!saved?.muted;
    } catch {}
    view = new WorldView(document.querySelector("#world"), data);
    await view.init(
      (n) =>
        (document.querySelector("#load-progress").textContent =
          `Unpacking real 3D objects… ${n}`),
    );
    for (const item of items.items.filter((i) => i.enabled))
      await view.icon(item.id);
    home();
    window.addEventListener("resize", () => view.resize());
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
