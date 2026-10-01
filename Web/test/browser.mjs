// Real Chromium interactions exercise the bundled game, not a mocked DOM.
import { chromium } from "playwright";
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
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("response", (r) => {
    if (r.status() >= 400) errors.push(`${r.status()} ${r.url()}`);
  });
  await page.goto(base);
  await page.waitForFunction(() => window.wreckabulary?.screen === "home", {
    timeout: 90000,
  });
  return page;
}
async function click(page, selector) {
  const target = page.locator(selector).first();
  await target.scrollIntoViewIfNeeded();
  const accessible = await target.evaluate((button) => {
    const r = button.getBoundingClientRect(),
      hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
    return (
      r.width > 0 &&
      r.height > 0 &&
      r.left >= 0 &&
      r.top >= 0 &&
      r.right <= innerWidth + 1 &&
      r.bottom <= innerHeight + 1 &&
      (hit === button || button.contains(hit))
    );
  });
  assert.equal(accessible, true, `unreachable UI: ${selector}`);
  await target.click({ timeout: 30000 });
}
async function screenshot(page, name) {
  await page.screenshot({ path: `playwright-results/${name}.png` });
}
try {
  const desktop = await browser.newContext({
      viewport: { width: 1440, height: 900 },
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
  await page.selectOption("#map-choice", "courtyard");
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.house.name),
    await page.evaluate(() => window.wreckabulary.data.houses.courtyard.name),
  );
  await click(page, "[data-action=closet]");
  await page.selectOption("#outfit-top", "Hoodie");
  await click(page, "[data-color=tomato]");
  await page.locator("[data-piece=Glasses]").check();
  await click(page, "[data-action=home]");
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.wardrobe.pieces.Top),
    "Hoodie",
  );
  record(
    "Wardrobe controls change real modular meshes and persist the selection.",
  );
  await page.selectOption("#map-choice", "pinwheel");
  await click(page, "[data-mode=Tutorial]");
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
  // Place the test player beside real map furniture; damage still comes through the public controls.
  await page.evaluate(() => {
    const g = window.wreckabulary.game,
      p = g.players[0],
      lamp = g.items.find((i) => i.word === "LAMP");
    p.x = lamp.x + 0.9;
    p.z = lamp.z;
    p.facing = { x: -1, z: 0 };
    p.yaw = -Math.PI / 2;
    window.testFurniture = lamp.id;
  });
  await page.keyboard.down("KeyJ");
  await page.waitForFunction(
    () => window.wreckabulary.game.item(window.testFurniture) === undefined,
    { timeout: 25000 },
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
  await page.waitForSelector("[data-recipe=LAMP].available");
  await click(page, "[data-recipe=LAMP]");
  await page.waitForFunction(
    () =>
      window.wreckabulary.game.held(window.wreckabulary.game.players[0])
        ?.word === "LAMP",
    { timeout: 15000 },
  );
  assert.match(await page.locator("#slot0").textContent(), /LAMP/);
  record(
    "Smash input releases exact tiles; live pickup, recipe UI, craft channel and equipment all connect.",
  );
  await click(page, "[data-action=drop]");
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
    { timeout: 10000 },
  );
  await page.keyboard.up("KeyE");
  record("Dropping and picking up gear preserves the physical item.");
  await page.keyboard.press("ShiftLeft");
  assert.ok(
    await page.evaluate(
      () => window.wreckabulary.game.players[0].lastDodge >= 0,
    ),
  );
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
    { timeout: 15000 },
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
    g.mintTiles("LAMP", p.x, p.z);
    for (const t of [...g.tiles]) g.collect(p, t);
  });
  await page.keyboard.press("KeyQ");
  await page.waitForSelector("[data-recipe=LAMP].available");
  await click(page, "[data-recipe=LAMP]");
  await page.waitForFunction(
    () =>
      window.wreckabulary.game.held(window.wreckabulary.game.players[0])
        ?.word === "LAMP",
    { timeout: 15000 },
  );
  await page.keyboard.press("KeyF");
  await page.waitForSelector(".result-card");
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
    { timeout: 10000 },
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
    { timeout: 15000 },
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
      viewport: { width: 390, height: 844 },
      isMobile: true,
      hasTouch: true,
      deviceScaleFactor: 1,
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
  await click(phone, "[data-mode=Tutorial]");
  await click(phone, "[data-action=start]");
  await phone.waitForFunction(() => window.wreckabulary.screen === "game");
  const joy = await phone.locator("#joystick").boundingBox();
  assert.ok(joy && joy.width > 90);
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
  await phone.waitForSelector(".craft-drawer");
  assert.equal(await phone.locator(".recipe").count(), 12);
  await screenshot(phone, "recipes-mobile");
  record(
    "Mobile HUD has separate touch targets, real item thumbnails and all 12 recipes.",
  );
  assert.equal(
    await phone.evaluate(() => window.wreckabulary.game.audit().balanced),
    true,
  );
  await click(phone, ".craft-drawer .close");
  await phone.setViewportSize({ width: 844, height: 390 });
  await phone.waitForTimeout(500);
  await phone.evaluate(() => {
    const g = window.wreckabulary.game,
      p = g.players[0];
    g.mintTiles("A".repeat(18 - p.bag.length), p.x, p.z);
    for (const tile of [...g.tiles]) g.collect(p, tile);
  });
  await phone.waitForFunction(
    () => document.querySelectorAll("#letters .letter").length === 18,
  );
  const landscape = await phone.evaluate(() => {
    const selectors = [
      ".letter-tray",
      ".vitals",
      "#joystick",
      ".actions button",
      ".gear-slot",
      ".drop-button",
      ".pause-button",
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
    "Landscape touch controls, full 18-letter bag and health remain visible, reachable and separate.",
  );
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
  const pages = browser.contexts().flatMap((context) => context.pages());
  const lastPage = pages.at(-1);
  if (lastPage) {
    await screenshot(lastPage, "failure");
    console.log(
      await lastPage.evaluate(() => {
        const g = window.wreckabulary?.game;
        return g
          ? {
              mode: g.mode,
              status: g.status,
              time: g.time,
              players: g.players.map((p) => ({
                name: p.name,
                x: p.x,
                z: p.z,
                hp: p.hp,
                state: p.state,
                carried: p.carried,
                bag: p.bag,
              })),
              keepsakes: g.keepsakes.map((k) => ({
                word: k.word,
                collected: k.collected,
                x: k.x,
                z: k.z,
              })),
            }
          : null;
      }),
    );
  }
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
  throw error;
} finally {
  await browser.close();
}
