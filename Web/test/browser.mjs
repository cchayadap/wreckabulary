import { chromium } from "playwright";
import { workshopDesktop, workshopMobile } from "./workshop-browser.mjs";
import { click, screenshot, softwareGpu, deviceScaleFactor, configureSoftwareRendering } from "./browser-ui.mjs";
import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";
const base = process.env.WRECKABULARY_URL ?? "http://127.0.0.1:4173";
const browser = await chromium.launch({
  executablePath: process.env.CHROMIUM_PATH ?? "/usr/bin/chromium",
  headless: true,
  args: ["--no-sandbox", "--enable-unsafe-swiftshader"],
});
await mkdir("playwright-results", { recursive: true });
const errors = [],
  checks = [],
  metrics = {};
function record(message) {
  checks.push(message);
  console.log(`✓ ${message}`);
}
async function boot(context) {
  const page = await context.newPage();
  if (softwareGpu) page.setDefaultTimeout(120000);
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("response", (r) => {
    const offlineBoard = r.status() === 404 && new URL(r.url()).pathname === "/api/scores";
    if (r.status() >= 400 && !offlineBoard) errors.push(`${r.status()} ${r.url()}`);
  });
  await page.goto(base);
  await page.waitForFunction(() => window.wreckabulary?.screen === "home", undefined, {
    timeout: 90000,
    polling: 100,
  });
  const profile = await configureSoftwareRendering(page);
  if (profile) (metrics.softwareRendering ??= []).push(profile);
  return page;
}

try {
  const desktop = await browser.newContext({
      viewport: { width: 1440, height: 900 },
      deviceScaleFactor,
      reducedMotion: "reduce",
    }),
    page = await boot(desktop);
  metrics.desktop = await page.evaluate(() => {
    const v = window.wreckabulary.view;
    v.camera.position.set(13, 20, 20);
    v.target.set(0, 0, 0);
    v.update(window.wreckabulary.game, 0);
    return {
      triangles: v.renderer.info.render.triangles,
      avatar: v.manifest.avatar.defaultTriangles,
      aspect: v.camera.aspect,
    };
  });
  await screenshot(page, "home-desktop");
  record("Real supplied GLBs load and render the home scene.");
  await workshopDesktop(page, { click, screenshot, record });
  await click(page, "[data-action=maps]");
  await click(page, "[data-map=courtyard]");
  assert.equal(await page.locator(".picker").count(), 0, "Picking a house closes the picker.");
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.house.name),
    await page.evaluate(() => window.wreckabulary.data.houses.courtyard.name),
  );
  await click(page, "[data-action=closet]");
  await page.selectOption("#outfit-top", "Hoodie");
  await click(page, "[data-color=tomato]");
  await page.locator("[data-piece=Glasses]").check();
  assert.equal(await page.locator("[data-piece=Glasses]").isChecked(), true);
  await click(page, "[data-action=home]");
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.wardrobe.pieces.Top),
    "Hoodie",
  );
  const glasses = await page.evaluate(() => {
    const player = window.wreckabulary.game.players[0];
    const avatar = window.wreckabulary.view.entities.get("p0").userData.avatar;
    const visible = [];
    avatar.traverse((node) => {
      if (node.isMesh && node.name.includes("Glasses")) visible.push(node.visible);
    });
    return { selected: player.wardrobe.pieces.Face,
      saved: JSON.parse(localStorage.getItem("wreckabulary.profile.v1")).wardrobe.pieces.Face,
      visible };
  });
  assert.equal(glasses.selected, "Glasses");
  assert.equal(glasses.saved, "Glasses");
  assert.ok(glasses.visible.length > 0 && glasses.visible.every(Boolean));
  record(
    "Wardrobe controls change real modular meshes and persist the selection.",
  );
  await click(page, "[data-action=maps]");
  await click(page, "[data-map=pinwheel]");
  await click(page, "[data-action=modes]");
  await page.keyboard.press("Escape");
  assert.equal(await page.locator(".picker").count(), 0, "Escape closes the mode picker.");
  await click(page, "[data-action=modes]");
  await click(page, "[data-mode=Tutorial]");
  assert.match(await page.locator(".dock-mode strong").innerText(), /learn/i);
  record("The lobby dock shows the chosen mode and house; each opens a picker that Escape or a choice closes.");
  await click(page, "[data-action=start]");
  await page.waitForFunction(() => window.wreckabulary.screen === "game");
  await click(page, "[data-action=pause]");
  const pausedSnapshot = await page.evaluate(() => ({
    time: window.wreckabulary.game.time,
    players: window.wreckabulary.game.players.map((p) => ({
      x: p.x,
      z: p.z,
      hp: p.hp,
    })),
  }));
  await page.waitForTimeout(1000);
  assert.deepEqual(
    await page.evaluate(() => ({
      time: window.wreckabulary.game.time,
      players: window.wreckabulary.game.players.map((p) => ({
        x: p.x,
        z: p.z,
        hp: p.hp,
      })),
    })),
    pausedSnapshot,
  );
  await click(page, ".result-card [data-action=pause]");
  record("Pause freezes simulation time, health and every AI position.");
  const before = await page.evaluate(() => ({
    x: window.wreckabulary.game.players[0].x,
    z: window.wreckabulary.game.players[0].z,
  }));
  await page.keyboard.down("KeyD");
  await page.waitForTimeout(1800);
  await page.keyboard.up("KeyD");
  const moved = await page.evaluate(() => ({
    x: window.wreckabulary.game.players[0].x,
    z: window.wreckabulary.game.players[0].z,
  }));
  assert.ok(Math.hypot(moved.x - before.x, moved.z - before.z) > 0.1);
  record("Keyboard movement advances a live player through the house.");
  const hud = await page.evaluate(() => {
    const box = (s) => document.querySelector(s).getBoundingClientRect(),
      cells = [...document.querySelectorAll("#letters > *")].map(
        (c) => c.getBoundingClientRect().top,
      ),
      side = box(".hud-side");
    return {
      actions: getComputedStyle(document.querySelector(".actions")).display,
      cells: cells.length,
      rows: new Set(cells.map(Math.round)).size,
      timerUnderMap: box("#timer").top >= box("#minimap").bottom,
      sideRight: innerWidth - side.right < 40,
    };
  });
  assert.deepEqual(hud, {
    actions: "none",
    cells: 10,
    rows: 2,
    timerUnderMap: true,
    sideRight: true,
  });
  await page.keyboard.down("Tab");
  await page.waitForSelector(".bag-panel #bag-recipes .recipe");
  const bag = await page.evaluate(() => {
    const r = document.querySelector(".bag-panel").getBoundingClientRect(),
      map = document.querySelector("#minimap").getBoundingClientRect();
    return {
      width: r.width / innerWidth,
      height: r.height / innerHeight,
      letters: document.querySelectorAll("#bag-letters > *").length,
      hands: document.querySelectorAll("#bag-hands .bag-hand").length,
      wearing: document.querySelectorAll("#bag-wear .wear-chip svg").length > 0,
      recipes: document.querySelectorAll("#bag-recipes .recipe").length,
      maps: document.querySelectorAll(".map-room").length / document.querySelectorAll("#minimap .map-room").length,
      zoomed: map.width > 240 && Math.abs(map.left + map.width / 2 - innerWidth / 2) < 40,
      you: !!document.querySelector("#minimap .map-player.you"),
    };
  });
  assert.ok(bag.width >= 0.75 && bag.height >= 0.75, JSON.stringify(bag));
  assert.deepEqual(
    { letters: bag.letters, hands: bag.hands, wearing: bag.wearing, recipes: bag.recipes, maps: bag.maps, zoomed: bag.zoomed, you: bag.you },
    { letters: 10, hands: 2, wearing: true, recipes: 12, maps: 1, zoomed: true, you: true },
  );
  await page.keyboard.up("Tab");
  assert.equal(await page.locator(".bag-panel").count(), 0);
  record(
    "Desktop HUD hides touch buttons, stacks timer under the map, shows a 5 x 2 bag, and Tab peeks an icon bag, the zoomed minimap and the recipe book.",
  );
  await page.evaluate(() => {
    const g = window.wreckabulary.game,
      p = g.players[0],
      lamp = g.items.find((i) => i.word === "LAMP");
    p.x = lamp.x + 0.9;
    p.z = lamp.z;
    p.facing = { x: -1, z: 0 };
    p.yaw = -Math.PI / 2;
    window.wreckabulary.view.look.yaw = p.yaw;
    window.testFurniture = lamp.id;
  });
  await page.keyboard.down("KeyJ");
  await page.waitForFunction(
    () => window.wreckabulary.game.item(window.testFurniture) === undefined,
    undefined,
    { timeout: 25000, polling: 100 },
  );
  await page.keyboard.up("KeyJ");
  for (let n = 0; n < 4; n++) {
    await page.evaluate(() => {
      const g = window.wreckabulary.game,
        t = g.tiles[0];
      if (t) {
        g.players[0].x = t.x;
        g.players[0].z = t.z;
      }
    });
    await page.waitForTimeout(250);
  }
  await page.waitForFunction(
    () => window.wreckabulary.game.players[0].bag.length >= 4,
  );
  await click(page, "[data-action=craft]");
  await page.waitForSelector(".spell-composer");
  await page.keyboard.type("lamp");
  await page.waitForFunction(
    () => document.querySelector(".spell-composer").dataset.state === "ready",
  );
  await page.keyboard.press("Enter");
  await page.waitForFunction(
    () =>
      window.wreckabulary.game.held(window.wreckabulary.game.players[0])
        ?.word === "LAMP",
    undefined,
    { timeout: 15000, polling: 100 },
  );
  await page.waitForFunction(() =>
    document.querySelector("#slot0").textContent.includes("LAMP"),
  );
  assert.match(await page.locator("#slot0").textContent(), /LAMP/);
  record(
    "Smash input releases exact tiles; live pickup, recipe UI, craft channel and equipment all connect.",
  );
  await page.keyboard.press("KeyR");
  assert.equal(
    await page.evaluate(
      () =>
        window.wreckabulary.game.held(window.wreckabulary.game.players[0]) ??
        null,
    ),
    null,
  );
  await page.keyboard.down("KeyE");
  await page.waitForFunction(
    () =>
      window.wreckabulary.game.held(window.wreckabulary.game.players[0])
        ?.word === "LAMP",
    undefined,
    { timeout: 10000, polling: 100 },
  );
  await page.keyboard.up("KeyE");
  record("Dropping and picking up gear preserves the physical item.");
  await page.keyboard.press("ShiftLeft");
  assert.ok(
    await page.evaluate(
      () => window.wreckabulary.game.players[0].lastDodge >= 0,
    ),
  );
  await page.waitForFunction(() => {
    const game = window.wreckabulary.game;
    return game.canAct(game.players[0]);
  });
  await page.keyboard.press("Space");
  assert.ok(
    await page.evaluate(
      () => window.wreckabulary.game.players[0].jumpUntil > 0,
    ),
  );
  await screenshot(page, "game-desktop");
  await page.evaluate(() =>
    window.wreckabulary.start({ mode: "Dibs", map: "courtyard" }),
  );
  await page.evaluate(() => {
    const g = window.wreckabulary.game;
    g.time = 3;
    g.players.slice(1).forEach((p) => g.eliminate(p));
    g.checkWin();
  });
  await page.waitForSelector(".result-card");
  await screenshot(page, "round-result");
  await click(page, "[data-action=next]");
  assert.equal(await page.evaluate(() => window.wreckabulary.game.round), 2);
  record("Round result and next-round controls reset the live arena.");
  for (const mode of ["Duos", "MovingDay", "MovingOut"]) {
    await page.evaluate(
      (mode) => window.wreckabulary.start({ mode, map: "courtyard" }),
      mode,
    );
    assert.equal(
      await page.evaluate(() => window.wreckabulary.game.mode),
      mode,
    );
    assert.equal(
      await page.evaluate(() => window.wreckabulary.game.audit().balanced),
      true,
    );
  }
  await screenshot(page, "courtyard-escape");
  await page.evaluate(() => {
    window.wreckabulary.start({ mode: "Duos", map: "pinwheel" });
    const g = window.wreckabulary.game,
      p = g.players[0],
      buddy = g.players[1];
    g.players.slice(1).forEach((q) => (q.ai = false));
    g.time = 3;
    buddy.invulnerableUntil = 0;
    g.hit(buddy, { damage: 200, attacker: 2 });
    p.x = buddy.x - 0.8;
    p.z = buddy.z;
  });
  await page.keyboard.down("KeyE");
  await page.waitForFunction(
    () => window.wreckabulary.game.players[1].state === "alive",
    undefined,
    { timeout: 15000, polling: 100 },
  );
  await page.keyboard.up("KeyE");
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.players[1].hp),
    30,
  );
  record(
    "Duos holds E through a real revive channel and restores the buddy to 30 HP.",
  );
  await page.evaluate(() => {
    window.wreckabulary.start({ mode: "MovingDay", map: "pinwheel" });
    const g = window.wreckabulary.game,
      p = g.players[0];
    g.players.slice(1).forEach((q) => (q.ai = false));
    for (const o of g.objectives) if (o.word !== "LAMP") o.done = true;
    const goal = g.objectives.find((o) => o.word === "LAMP");
    p.x = goal.x;
    p.z = goal.z;
    p.facing = { x: 0, z: 1 };
    p.yaw = 0;
    window.wreckabulary.view.look.yaw = 0;
    g.mintTiles("LAMP", p.x, p.z);
    for (const t of [...g.tiles]) g.collect(p, t);
  });
  await page.keyboard.press("KeyQ");
  await page.waitForSelector(".spell-composer");
  await page.keyboard.type("LAMP");
  await page.keyboard.press("Enter");
  await page.waitForFunction(
    () =>
      window.wreckabulary.game.held(window.wreckabulary.game.players[0])
        ?.word === "LAMP",
    undefined,
    { timeout: 15000, polling: 100 },
  );
  await page.keyboard.press("KeyF");
  await page.keyboard.press("KeyG");
  assert.equal(
    await page.evaluate(
      () =>
        window.wreckabulary.game.held(window.wreckabulary.game.players[0])
          ?.word,
    ),
    "LAMP",
  );
  await page.waitForFunction(
    () => document.querySelector("#attack-label").textContent === "PLACE",
    undefined,
    { timeout: 5000, polling: 100 },
  );
  await page.keyboard.down("KeyJ");
  await page.waitForSelector(".result-card");
  await page.keyboard.up("KeyJ");
  record("Moving Day: F and G do nothing; the attack button places the checklist LAMP.");
  assert.equal(await page.evaluate(() => window.wreckabulary.game.winner), 0);
  await screenshot(page, "moving-day-complete");
  await click(page, ".result-card [data-action=start]");
  assert.equal(
    await page.evaluate(() =>
      window.wreckabulary.game.objectives.some((o) => o.done),
    ),
    false,
  );
  record(
    "Moving Day spells and places its final LAMP through UI, shows a win and retries with a fresh checklist.",
  );
  await page.evaluate(() => {
    window.wreckabulary.start({ mode: "MovingOut", map: "pinwheel" });
    const g = window.wreckabulary.game,
      p = g.players[0],
      k = g.keepsakes[0];
    g.players.slice(1).forEach((q) => {
      q.ai = false;
      q.x = g.extraction.x;
      q.z = g.extraction.z;
    });
    for (const other of g.keepsakes.slice(1)) {
      other.collected = true;
      g.item(other.itemId).state = "packed";
    }
    p.x = k.x - 0.9;
    p.z = k.z;
    const buddy = g.players[1];
    buddy.state = "downed";
    buddy.hp = 0;
    buddy.bleedAt = g.time + 100;
    buddy.x = g.extraction.x + 0.6;
    buddy.z = g.extraction.z + 3.2;
  });
  await page.keyboard.down("KeyE");
  await page.waitForFunction(
    () => window.wreckabulary.game.players[0].carried !== null,
    undefined,
    { timeout: 10000, polling: 100 },
  );
  await page.keyboard.up("KeyE");
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.keepsakes[0].collected),
    false,
  );
  await page.evaluate(() => {
    const g = window.wreckabulary.game,
      p = g.players[0];
    p.x = g.extraction.x;
    p.z = g.extraction.z;
    p.facing = { x: 0, z: 1 };
    p.yaw = 0;
    window.wreckabulary.view.look.yaw = 0;
  });
  await page.keyboard.press("KeyR");
  await page.waitForFunction(
    () => window.wreckabulary.game.keepsakes[0].collected,
  );
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.status),
    "playing",
  );
  await page.evaluate(() => {
    const g = window.wreckabulary.game,
      p = g.players[0],
      buddy = g.players[1];
    p.x = buddy.x - 0.8;
    p.z = buddy.z;
  });
  await page.keyboard.down("KeyE");
  await page.waitForFunction(
    () => window.wreckabulary.game.players[1].state === "alive",
    undefined,
    { timeout: 15000, polling: 100 },
  );
  await page.keyboard.up("KeyE");
  await page.evaluate(() => {
    const g = window.wreckabulary.game;
    g.players[1].ai = true;
    g.players[0].x = g.extraction.x;
    g.players[0].z = g.extraction.z;
  });
  await page.waitForSelector(".result-card", { timeout: 30000 });
  assert.equal(await page.evaluate(() => window.wreckabulary.game.winner), 0);
  await screenshot(page, "moving-out-complete");
  await click(page, ".result-card [data-action=start]");
  assert.equal(
    await page.evaluate(() =>
      window.wreckabulary.game.keepsakes.some((k) => k.collected),
    ),
    false,
  );
  record(
    "Moving Out physically carries and drops a keepsake; a downed crew member blocks victory until revived and gathered, then retry resets the rescue.",
  );
  await desktop.close();
  const mobile = await browser.newContext({
      viewport: { width: 844, height: 390 },
      isMobile: true,
      hasTouch: true,
      deviceScaleFactor,
      reducedMotion: "reduce",
    }),
    phone = await boot(mobile);
  metrics.mobile = await phone.evaluate((aspect) => {
    const v = window.wreckabulary.view,
      original = v.camera.aspect;
    v.camera.aspect = aspect;
    v.camera.updateProjectionMatrix();
    v.camera.position.set(13, 20, 20);
    v.target.set(0, 0, 0);
    v.update(window.wreckabulary.game, 0);
    const result = {
      triangles: v.renderer.info.render.triangles,
      avatar: v.manifest.avatar.mobileDefaultTriangles,
      aspect: v.camera.aspect,
    };
    v.camera.aspect = original;
    v.camera.updateProjectionMatrix();
    return result;
  }, metrics.desktop.aspect);
  assert.ok(metrics.mobile.triangles < metrics.desktop.triangles);
  await screenshot(phone, "home-mobile");
  const sideways = await phone.evaluate(() => {
    const hint = getComputedStyle(document.querySelector(".rotate-hint")).display,
      dock = document.querySelector(".match-dock").getBoundingClientRect(),
      you = window.wreckabulary.game.players[0],
      head = window.wreckabulary.view.screenOf(you.x, you.y + 1.2, you.z);
    return {
      hint,
      dockInside: dock.right <= innerWidth + 1 && dock.bottom <= innerHeight + 1,
      centred: Math.abs(head.x - innerWidth / 2) < innerWidth * 0.08,
      scroll: document.documentElement.scrollWidth > innerWidth,
    };
  });
  assert.deepEqual(sideways, { hint: "none", dockInside: true, centred: true, scroll: false });
  await phone.setViewportSize({ width: 390, height: 844 });
  await phone.waitForFunction(
    () => getComputedStyle(document.querySelector(".rotate-hint")).display === "flex",
  );
  await screenshot(phone, "rotate-hint-mobile");
  await phone.setViewportSize({ width: 844, height: 390 });
  await phone.waitForFunction(
    () => getComputedStyle(document.querySelector(".rotate-hint")).display === "none",
  );
  record("Phones play sideways: the lobby fits a landscape phone with you centred, and an upright phone gets the rotate hint.");
  await click(phone, "[data-action=modes]");
  await click(phone, "[data-mode=Tutorial]");
  await click(phone, "[data-action=start]");
  await phone.waitForFunction(() => window.wreckabulary.screen === "game");
  const joy = await phone.locator("#joystick").boundingBox();
  assert.ok(joy && joy.width >= 88);
  const initial = await phone.evaluate(() => ({
    x: window.wreckabulary.game.players[0].x,
    z: window.wreckabulary.game.players[0].z,
  }));
  const client = await phone.context().newCDPSession(phone),
    cx = joy.x + joy.width / 2,
    cy = joy.y + joy.height / 2;
  await client.send("Input.dispatchTouchEvent", {
    type: "touchStart",
    touchPoints: [{ x: cx, y: cy, id: 1 }],
  });
  await client.send("Input.dispatchTouchEvent", {
    type: "touchMove",
    touchPoints: [{ x: cx + 30, y: cy - 18, id: 1 }],
  });
  await phone.waitForTimeout(1300);
  await client.send("Input.dispatchTouchEvent", {
    type: "touchEnd",
    touchPoints: [],
  });
  const after = await phone.evaluate(() => ({
    x: window.wreckabulary.game.players[0].x,
    z: window.wreckabulary.game.players[0].z,
  }));
  assert.ok(Math.hypot(after.x - initial.x, after.z - initial.z) > 0.1);
  record("A native touchscreen joystick gesture moves the player.");
  const attack = await phone.locator("#attack-action").boundingBox();
  const oldAttack = await phone.evaluate(
    () => window.wreckabulary.game.players[0].attackAt,
  );
  await client.send("Input.dispatchTouchEvent", {
    type: "touchStart",
    touchPoints: [
      { x: cx, y: cy, id: 1 },
      {
        x: attack.x + attack.width / 2,
        y: attack.y + attack.height / 2,
        id: 2,
      },
    ],
  });
  await client.send("Input.dispatchTouchEvent", {
    type: "touchMove",
    touchPoints: [
      { x: cx + 25, y: cy - 10, id: 1 },
      {
        x: attack.x + attack.width / 2,
        y: attack.y + attack.height / 2,
        id: 2,
      },
    ],
  });
  await phone.waitForTimeout(600);
  await client.send("Input.dispatchTouchEvent", {
    type: "touchEnd",
    touchPoints: [],
  });
  assert.ok(
    await phone.evaluate(
      (old) => window.wreckabulary.game.players[0].attackAt > old,
      oldAttack,
    ),
  );
  record(
    "Simultaneous native joystick and attack touches are tracked separately.",
  );
  const bounds = await phone.evaluate(() => {
    const a = document.querySelector(".letter-tray").getBoundingClientRect(),
      b = document.querySelector(".vitals").getBoundingClientRect();
    return {
      overlap:
        a.left < b.right &&
        a.right > b.left &&
        a.top < b.bottom &&
        a.bottom > b.top,
      scroll: document.documentElement.scrollWidth > innerWidth,
      art: getComputedStyle(document.querySelector(".action-art"))
        .backgroundImage,
    };
  });
  assert.equal(bounds.overlap, false);
  assert.equal(bounds.scroll, false);
  assert.match(bounds.art, /ActionIcons/);
  await screenshot(phone, "game-mobile");
  await click(phone, "[data-action=craft]");
  await phone.waitForSelector(".spell-composer #spell-word");
  assert.equal(await phone.locator(".spell-composer .recipe").count(), 0, "In play you type the word; no list to pick from.");
  await click(phone, ".spell-composer [data-action=recipes]");
  await phone.waitForSelector(".bag-panel #bag-recipes .recipe");
  assert.equal(await phone.locator("#bag-recipes .recipe").count(), 12);
  await screenshot(phone, "recipes-mobile");
  record(
    "Mobile HUD has separate touch targets, a typed spell composer and all 12 recipes in the bag's recipe book.",
  );
  await click(phone, ".bag-panel [data-action=bag]");
  assert.equal(
    await phone.evaluate(() => window.wreckabulary.game.audit().balanced),
    true,
  );
  await click(phone, ".spell-composer .close");
  await phone.setViewportSize({ width: 844, height: 390 });
  await phone.waitForTimeout(500);
  await phone.evaluate(() => {
    const g = window.wreckabulary.game,
      p = g.players[0];
    g.mintTiles("A".repeat(g.rules.maxLetters - p.bag.length), p.x, p.z);
    for (const tile of [...g.tiles]) g.collect(p, tile);
  });
  await phone.waitForFunction(
    () => document.querySelectorAll("#letters .letter").length === window.wreckabulary.game.rules.maxLetters,
  );
  const landscape = await phone.evaluate(() => {
    const selectors = [
      ".letter-tray",
      ".vitals",
      "#joystick",
      ".actions button",
      ".gear-slot",
      ".brand-small",
      "#letters button",
      ".tray-header button",
    ];
    const targets = selectors.flatMap((selector) => [
      ...document.querySelectorAll(selector),
    ]);
    const unreachable = targets
      .filter((target) => {
        const r = target.getBoundingClientRect(),
          hit = document.elementFromPoint(
            r.x + r.width / 2,
            r.y + r.height / 2,
          );
        return (
          r.width <= 0 ||
          r.height <= 0 ||
          r.left < 0 ||
          r.top < 0 ||
          r.right > innerWidth + 1 ||
          r.bottom > innerHeight + 1 ||
          (target.matches("button,input,#joystick") && !target.contains(hit))
        );
      })
      .map((target) => target.id || target.className);
    const regions = [".letter-tray", ".vitals", "#joystick", ".actions"].map(
      (selector) => ({
        selector,
        r: document.querySelector(selector).getBoundingClientRect(),
      }),
    );
    const overlaps = [];
    for (let i = 0; i < regions.length; i++)
      for (let j = i + 1; j < regions.length; j++) {
        const a = regions[i].r,
          b = regions[j].r;
        if (
          a.left < b.right &&
          a.right > b.left &&
          a.top < b.bottom &&
          a.bottom > b.top
        )
          overlaps.push([regions[i].selector, regions[j].selector]);
      }
    return {
      unreachable,
      overlaps,
      scroll: document.documentElement.scrollWidth > innerWidth,
    };
  });
  assert.deepEqual(landscape.unreachable, []);
  assert.deepEqual(landscape.overlaps, []);
  assert.equal(landscape.scroll, false);
  await screenshot(phone, "game-mobile-landscape");
  record(
    "Landscape touch controls, full 10-letter bag and health remain visible, reachable and separate.",
  );
  await workshopMobile(phone, { click, screenshot, record });
  await mobile.close();
  assert.deepEqual(errors, []);
  await writeFile(
    "playwright-results/browser-report.json",
    JSON.stringify(
      { checks, errors, metrics, passed: checks.length, base },
      null,
      2,
    ),
  );
  console.log(`BROWSER_SMOKE passed=${checks.length} errors=0`);
  checks.forEach((c) => console.log(`✓ ${c}`));
} catch (error) {
  await writeFile(
    "playwright-results/browser-report.json",
    JSON.stringify(
      {
        checks,
        errors: [...errors, error.message],
        metrics,
        passed: checks.length,
        base,
      },
      null,
      2,
    ),
  );
  const pages = browser.contexts().flatMap((context) => context.pages());
  const lastPage = pages.at(-1);
  if (lastPage) {
    await lastPage.screenshot({ path: "playwright-results/failure.png", timeout: 15000 }).catch(() => {});
  }
  throw error;
} finally {
  await browser.close();
}
