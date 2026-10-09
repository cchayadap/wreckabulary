import assert from "node:assert/strict";
import { configureSoftwareRendering } from "./browser-ui.mjs";
const layout = (page) =>
  page.evaluate(() => window.wreckabulary.workshop.layout);
async function coords(page, x, z) {
  await page.locator("#prop-x").fill(String(x));
  await page.locator("#prop-z").fill(String(z));
  await page.locator("#prop-z").press("Tab");
}
async function objectPoint(page, id) {
  await page.waitForFunction(
    (id) => window.wreckabulary.view.designEntities.has(id),
    id,
  );
  return page.evaluate((id) => {
    const v = window.wreckabulary.view,
      node = v.designEntities.get(id),
      item = window.wreckabulary.game.items.find((i) => i.designId === id);
    const position = node.position.clone();
    position.y += (item.definition.size[1] ?? 0.4) * 0.55;
    position.project(v.camera);
    const cx = ((position.x + 1) * innerWidth) / 2,
      cy = ((1 - position.y) * innerHeight) / 2;
    for (const radius of [0, 4, 8, 12, 18, 25])
      for (let a = 0; a < 12; a++) {
        const x = cx + Math.cos((a * Math.PI) / 6) * radius,
          y = cy + Math.sin((a * Math.PI) / 6) * radius;
        if (
          x <= 0 ||
          y <= 0 ||
          x >= innerWidth ||
          y >= innerHeight ||
          document.elementFromPoint(x, y)?.id !== "world"
        )
          continue;
        if (v.pickWorkshop(x, y).designId === id) return { x, y };
      }
    throw Error(`No visible clickable supplied model for ${id}`);
  }, id);
}
async function groundPoint(page, prop) {
  return page.evaluate((prop) => {
    const v = window.wreckabulary.view,
      p = v.camera.position.clone().set(prop.x, 0, prop.z).project(v.camera),
      x = ((p.x + 1) * innerWidth) / 2,
      y = ((1 - p.y) * innerHeight) / 2;
    if (
      x <= 0 ||
      y <= 0 ||
      x >= innerWidth ||
      y >= innerHeight ||
      document.elementFromPoint(x, y)?.id !== "world"
    )
      throw Error("Placement point is hidden by the Workshop UI");
    const hit = v.pickWorkshop(x, y);
    if (
      !hit.point ||
      Math.hypot(hit.point.x - prop.x, hit.point.z - prop.z) > 0.1
    )
      throw Error(
        "Projected point does not roundtrip through real floor raycast",
      );
    return { x, y };
  }, prop);
}
export async function workshopDesktop(page, { click, screenshot, record }) {
  await click(page, "[data-action=workshop]");
  await page.waitForFunction(() => window.wreckabulary.screen === "workshop");
  assert.equal(await page.locator("[data-design-word]").count(), 40);
  const original = await layout(page);
  await page.locator("#design-name").fill("Sunbeam House");
  await page.locator("#design-name").press("Tab");
  assert.equal(await page.locator("#design-undo").isDisabled(), false);
  await click(page, "#design-undo");
  assert.deepEqual(await layout(page), original);
  const shell = await page.evaluate(() => {
    const view = window.wreckabulary.view,
      sources = new Set();
    for (const name of [
      "Kitchen_Fridge",
      "Kitchen_Counter",
      "Kitchen_Sink",
      "Kitchen_Stove",
    ]) {
      const model = view.models.get(`env:${name}`);
      if (!model) throw Error(`Missing supplied environment model ${name}`);
      model.scene.traverse((node) => {
        if (node.isMesh) sources.add(node.geometry);
      });
    }
    let appliances = 0;
    view.world.traverse((node) => {
      if (node.isMesh && sources.has(node.geometry)) appliances++;
    });
    view.raycaster.set(
      view.camera.position.clone().set(3, 20, 2),
      view.camera.position.clone().set(0, -1, 0),
    );
    const meshes = [];
    view.world.traverse((node) => {
      if (node.isMesh) meshes.push(node);
    });
    const floor = view.raycaster
      .intersectObjects(meshes, false)
      .find((hit) => hit.object.isMesh);
    return { appliances, floorY: floor?.point.y ?? null };
  });
  assert.equal(shell.appliances, 0);
  assert.notEqual(shell.floorY, null);
  assert.ok(Math.abs(shell.floorY) < 0.02);
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.floorAt(3, 2)),
    0,
  );
  record(
    "The real Workshop scene has a flat floor and no unsaved indoor appliance geometry.",
  );
  await click(page, "#design-word-form button[type=submit]");
  assert.match(await page.textContent("#workshop-message"), /furniture words/);
  await click(page, "[data-design-idea=reading]");
  assert.equal((await layout(page)).props.length, 0);
  assert.equal(await page.inputValue("#design-room"), "LivingRoom");
  assert.equal(
    await page.inputValue("#design-words"),
    "SOFA TABLE LAMP BOOK PLANT",
  );
  await page.locator("#design-words").fill("SOFA TABLE PLANT");
  await click(page, "#design-word-form button[type=submit]");
  assert.deepEqual(
    (await layout(page)).props.map((p) => p.word),
    ["SOFA", "TABLE", "PLANT"],
  );
  record(
    "Workshop lists 40 actual models; room ideas only fill text and real word submission furnishes the chosen room.",
  );
  const plant = (await layout(page)).props.find((p) => p.word === "PLANT");
  await click(page, "#design-focus-room");
  const pixel = await objectPoint(page, plant.id);
  await page.mouse.click(pixel.x, pixel.y);
  await page.waitForFunction(
    (id) => window.wreckabulary.workshop.selectedId === id,
    plant.id,
  );
  await click(page, "#design-rotate");
  assert.equal(
    (await layout(page)).props.find((p) => p.id === plant.id).yaw,
    90,
  );
  await page.selectOption("#prop-skin", "Candy");
  await click(page, "#design-place");
  assert.equal(
    (await layout(page)).props.find((p) => p.id === plant.id).skin,
    "Candy",
  );
  record(
    "A real 3D model can be selected with the pointer, quarter-turned and given a finish.",
  );
  await page.locator("#design-words").fill("BOOK");
  await click(page, "#design-preview");
  const pending = await page.evaluate(() => window.wreckabulary.workshop.ghost);
  await page.locator("#design-name").fill("A Cozy House in Progress");
  await page.locator("#design-name").press("Tab");
  assert.deepEqual(
    await page.evaluate(() => window.wreckabulary.workshop.ghost),
    pending,
  );
  const before = await layout(page),
    preview = await page.evaluate(() => window.wreckabulary.workshop.ghost);
  await page.locator("#prop-x").fill("");
  await click(page, "#design-place");
  assert.deepEqual(await layout(page), before);
  assert.match(await page.textContent("#workshop-message"), /number/);
  const door = await page.evaluate(
    () => window.wreckabulary.data.houses.pinwheel.doors[0].at,
  );
  await coords(page, ...door);
  await click(page, "#design-place");
  assert.deepEqual(await layout(page), before);
  assert.match(await page.textContent("#workshop-message"), /door|room|spawn/);
  await coords(page, preview.x, preview.z);
  await click(page, "#design-focus-room");
  const floor = await groundPoint(page, preview);
  await page.mouse.click(floor.x, floor.y);
  await page.waitForFunction(
    () => window.wreckabulary.workshop.layout.props.length === 4,
  );
  record(
    "Blank coordinates and blocked doors reject placement atomically; a real floor click places the valid model preview.",
  );
  await click(page, "#design-delete");
  assert.equal((await layout(page)).props.length, 3);
  await click(page, "#design-undo");
  assert.equal((await layout(page)).props.length, 4);
  await click(page, "#design-redo");
  assert.equal((await layout(page)).props.length, 3);
  await click(page, "#design-undo");
  const book = (await layout(page)).props.find((p) => p.word === "BOOK");
  await page.selectOption("#design-selection", book.id);
  await click(page, "#design-rotate");
  assert.equal(await page.locator("#design-redo").isDisabled(), true);
  record(
    "Remove, Undo and Redo change the real layout, and a new edit branches history.",
  );
  await page.locator("#design-name").fill("Sunday Reading House");
  await click(page, "#design-save");
  const saved = await layout(page);
  assert.deepEqual(
    await page.evaluate(() =>
      JSON.parse(localStorage.getItem("wreckabulary.workshop.v1.pinwheel")),
    ),
    saved,
  );
  await Promise.all([
    page.waitForEvent("download", { timeout: 90000 }),
    click(page, "#design-export"),
  ]);
  assert.deepEqual(JSON.parse(await page.inputValue("#design-json")), saved);
  await click(page, "#transfer-close");
  await click(page, "#design-import-open");
  await page.locator("#design-json").fill('{"schema":1}');
  await click(page, "#design-import");
  assert.deepEqual(await layout(page), saved);
  assert.ok((await page.textContent("#transfer-error")).length > 0);
  const overlap = structuredClone(saved);
  overlap.props.push({ ...overlap.props[0], id: "overlap" });
  await page.locator("#design-json").fill(JSON.stringify(overlap));
  await click(page, "#design-import");
  assert.deepEqual(await layout(page), saved);
  assert.match(await page.textContent("#transfer-error"), /overlap|between/);
  const copy = { ...saved, name: "Sunday House Copy" };
  await page.locator("#design-json").fill(JSON.stringify(copy));
  await click(page, "#design-import");
  assert.deepEqual(await layout(page), copy);
  record(
    "Explicit Save and JSON export roundtrip the house; malformed and overlapping imports preserve it atomically.",
  );
  await click(page, "#design-tour");
  await page.waitForFunction(() => window.wreckabulary.screen === "tour");
  assert.deepEqual(
    await page.evaluate(() =>
      window.wreckabulary.game.items.map((i) => ({
        id: i.designId,
        word: i.word,
        x: i.x,
        z: i.z,
        yaw: (i.rotation * 180) / Math.PI,
        skin: i.skin,
      })),
    ),
    copy.props,
  );
  const pos = await page.evaluate(() => {
    const p = window.wreckabulary.game.players[0];
    return { x: p.x, z: p.z };
  });
  await page.keyboard.down("KeyD");
  await page.waitForTimeout(900);
  await page.keyboard.up("KeyD");
  assert.ok(
    await page.evaluate((old) => {
      const p = window.wreckabulary.game.players[0];
      return Math.hypot(p.x - old.x, p.z - old.z) > 0.1;
    }, pos),
  );
  await page.keyboard.press("KeyJ");
  await page.keyboard.press("KeyQ");
  await page.keyboard.press("KeyE");
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.players[0].bag),
    "",
  );
  assert.equal(
    await page.evaluate(() => window.wreckabulary.game.players[0].hp),
    100,
  );
  await screenshot(page, "workshop-tour-desktop");
  await click(page, "[data-action=workshop-return]");
  assert.deepEqual(await layout(page), copy);
  record(
    "Save & explore walks through exactly the saved decor with no combat or letter gain, and return preserves the house.",
  );
  await page.reload();
  await page.waitForFunction(() => window.wreckabulary?.screen === "home");
  await configureSoftwareRendering(page);
  await click(page, "[data-action=workshop]");
  assert.deepEqual(await layout(page), copy);
  await page.selectOption("#design-map", "courtyard");
  await click(page, "[data-design-idea=party]");
  await click(page, "#design-word-form button[type=submit]");
  await page.locator("#design-name").fill("Sunny Garden");
  await click(page, "#design-save");
  const garden = await layout(page);
  assert.ok(garden.props.length > 0);
  await page.reload();
  await page.waitForFunction(() => window.wreckabulary?.screen === "home");
  await configureSoftwareRendering(page);
  await click(page, "[data-action=workshop]");
  assert.equal((await layout(page)).map, "pinwheel");
  await click(page, "#design-import-open");
  await page
    .locator("#design-json")
    .fill(JSON.stringify({ ...garden, name: "Imported Garden" }));
  await click(page, "#design-import");
  assert.equal((await layout(page)).name, "Imported Garden");
  await click(page, "#design-undo");
  assert.deepEqual(await layout(page), garden);
  await page.selectOption("#design-map", "pinwheel");
  assert.deepEqual(await layout(page), copy);
  record(
    "Reload preserves names, positions, turns and finishes; cross-map import Undo restores that map's saved house independently.",
  );
  const persisted = await page.evaluate(() =>
    localStorage.getItem("wreckabulary.workshop.v1.pinwheel"),
  );
  await page.evaluate(() => {
    window.__originalStorageSet = Storage.prototype.setItem;
    Storage.prototype.setItem = function () {
      throw new DOMException("Quota exceeded", "QuotaExceededError");
    };
  });
  await click(page, "#design-save");
  assert.match(await page.textContent("#workshop-message"), /could not save/);
  assert.equal(
    await page.evaluate(() =>
      localStorage.getItem("wreckabulary.workshop.v1.pinwheel"),
    ),
    persisted,
  );
  await page.evaluate(() => {
    Storage.prototype.setItem = window.__originalStorageSet;
    delete window.__originalStorageSet;
  });
  await page.locator("#design-name").fill("");
  await click(page, "#design-save");
  assert.equal(
    await page.evaluate(() =>
      localStorage.getItem("wreckabulary.workshop.v1.pinwheel"),
    ),
    persisted,
  );
  assert.match(await page.textContent("#workshop-message"), /name|characters/);
  record(
    "Storage failures and invalid house names report errors without pretending to save.",
  );
  await page.locator("#design-name").fill(copy.name);
  await click(page, "#design-save");
  await screenshot(page, "workshop-desktop");
  await click(page, "[data-action=home]");
}
async function fit(page) {
  const result = await page.evaluate(() => {
    const r = document.querySelector(".workshop-panel").getBoundingClientRect(),
      h = document.querySelector(".workshop-head").getBoundingClientRect(),
      c = document.querySelector(".workshop-camera").getBoundingClientRect();
    const outside = [r, h, c].some(
      (b) =>
        b.left < 0 ||
        b.top < 0 ||
        b.right > innerWidth + 1 ||
        b.bottom > innerHeight + 1,
    );
    const overlaps = (a, b) =>
      a.left < b.right &&
      a.right > b.left &&
      a.top < b.bottom &&
      a.bottom > b.top;
    const buttons = [
      ...document.querySelectorAll(
        ".workshop-head button,.workshop-camera button",
      ),
    ];
    const blocked = buttons
      .filter((b) => {
        const q = b.getBoundingClientRect();
        return !b.contains(
          document.elementFromPoint(q.x + q.width / 2, q.y + q.height / 2),
        );
      })
      .map((b) => b.id || b.dataset.action);
    return {
      outside,
      overlap: overlaps(r, h) || overlaps(r, c),
      blocked,
      scroll: document.documentElement.scrollWidth > innerWidth,
    };
  });
  assert.deepEqual(result, {
    outside: false,
    overlap: false,
    blocked: [],
    scroll: false,
  });
}
export async function workshopMobile(phone, { click, screenshot, record }) {
  await phone.setViewportSize({ width: 844, height: 390 });
  await click(phone, "[data-action=pause]");
  await click(phone, ".result-card [data-action=home]");
  await click(phone, "[data-action=workshop]");
  await phone.selectOption("#design-room", "LivingRoom");
  await phone.locator("#design-words").fill("PLANT");
  await click(phone, "#design-preview");
  const preview = await phone.evaluate(
      () => window.wreckabulary.workshop.ghost,
    ),
    p = await groundPoint(phone, preview);
  await phone.touchscreen.tap(p.x, p.y);
  await phone.waitForFunction(
    () => window.wreckabulary.workshop.layout.props.length === 1,
  );
  await fit(phone);
  await screenshot(phone, "workshop-mobile-landscape");
  record(
    "Workshop on a landscape phone keeps primary buttons, house view and scrollable tools separate; native touch places a supplied model.",
  );
  await click(phone, "#design-tour");
  const client = await phone.context().newCDPSession(phone),
    joy = await phone.locator("#joystick").boundingBox(),
    cx = joy.x + joy.width / 2,
    cy = joy.y + joy.height / 2;
  const before = await phone.evaluate(() => {
    const p = window.wreckabulary.game.players[0];
    return { x: p.x, z: p.z };
  });
  await client.send("Input.dispatchTouchEvent", {
    type: "touchStart",
    touchPoints: [{ x: cx, y: cy, id: 1 }],
  });
  await client.send("Input.dispatchTouchEvent", {
    type: "touchMove",
    touchPoints: [{ x: cx + 25, y: cy - 15, id: 1 }],
  });
  await phone.waitForTimeout(900);
  await client.send("Input.dispatchTouchEvent", {
    type: "touchEnd",
    touchPoints: [],
  });
  assert.ok(
    await phone.evaluate((old) => {
      const p = window.wreckabulary.game.players[0];
      return Math.hypot(p.x - old.x, p.z - old.z) > 0.1;
    }, before),
  );
  await screenshot(phone, "workshop-tour-mobile");
  await click(phone, "[data-action=workshop-return]");
  assert.equal((await layout(phone)).props.length, 1);
  record(
    "Native touch joystick explores the peaceful saved house and the return button restores its design.",
  );
}
