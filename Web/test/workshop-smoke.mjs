import { chromium } from "playwright";
import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";
import { click, screenshot } from "./browser-ui.mjs";
import { workshopDesktop, workshopMobile } from "./workshop-browser.mjs";
const base = process.env.WRECKABULARY_URL ?? "http://127.0.0.1:4173",
  checks = [],
  errors = [];
const browser = await chromium.launch({
  executablePath: process.env.CHROMIUM_PATH ?? "/usr/bin/chromium",
  headless: true,
  args: ["--no-sandbox", "--enable-unsafe-swiftshader"],
});
await mkdir("playwright-results", { recursive: true });
const record = (message) => {
  checks.push(message);
  console.log(`✓ ${message}`);
};
async function boot(context) {
  const page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("response", (r) => {
    if (r.status() >= 400) errors.push(`${r.status()} ${r.url()}`);
  });
  await page.goto(base);
  await page.waitForFunction(
    () => window.wreckabulary?.screen === "home",
    null,
    { timeout: 90000 },
  );
  return page;
}
try {
  const desktop = await browser.newContext({
      viewport: { width: 1440, height: 900 },
      reducedMotion: "reduce",
    }),
    page = await boot(desktop);
  await workshopDesktop(page, { click, screenshot, record });
  await desktop.close();
  const mobile = await browser.newContext({
      viewport: { width: 844, height: 390 },
      isMobile: true,
      hasTouch: true,
      deviceScaleFactor: 1,
      reducedMotion: "reduce",
    }),
    phone = await boot(mobile);
  await click(phone, "[data-action=modes]");
  await click(phone, "[data-mode=Tutorial]");
  await click(phone, "[data-action=start]");
  await workshopMobile(phone, { click, screenshot, record });
  await mobile.close();
  assert.deepEqual(errors, []);
  console.log(`WORKSHOP_BROWSER passed=${checks.length} errors=0`);
} catch (error) {
  errors.push(error.message);
  const page = browser
    .contexts()
    .flatMap((c) => c.pages())
    .at(-1);
  if (page) await screenshot(page, "workshop-failure");
  throw error;
} finally {
  await writeFile(
    "playwright-results/workshop-browser-report.json",
    JSON.stringify({ base, checks, errors, passed: checks.length }, null, 2),
  );
  await browser.close();
}
