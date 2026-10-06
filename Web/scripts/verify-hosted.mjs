import { createHash } from "node:crypto";
import { readFile, readdir, mkdir, writeFile } from "node:fs/promises";
import { chromium } from "playwright";

const base = "https://wreckabulary.pagna.workers.dev";
const expected = JSON.parse(await readFile("dist/BUILD-INFO.json", "utf8"));
const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
let ready = false;
for (let attempt = 0; attempt < 60; attempt++) {
  const response = await fetch(`${base}/BUILD-INFO.json?check=${Date.now()}`, {
    headers: { "User-Agent": "Mozilla/5.0", "Cache-Control": "no-cache" },
    signal: AbortSignal.timeout(45000),
  });
  if (response.ok && (await response.json()).sourceCommit === expected.sourceCommit) {
    ready = true;
    break;
  }
  await delay(5000);
}
if (!ready) throw new Error("Worker did not publish the expected source commit.");

async function* files(dir, prefix = "") {
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const name = prefix + entry.name;
    if (entry.isDirectory()) yield* files(`${dir}/${entry.name}`, `${name}/`);
    else if (entry.isFile()) yield name;
  }
}
const sha = (bytes) => createHash("sha256").update(bytes).digest("hex");
const records = [];
for await (const path of files("dist")) {
  const local = await readFile(`dist/${path}`);
  const localSha = sha(local);
  if (sha(await readFile(`dist/${path}`)) !== localSha) throw new Error(`Unstable build file ${path}`);
  const passes = [];
  for (let read = 0; read < 2; read++) {
    const response = await fetch(`${base}/${path}?check=${Date.now()}-${read}`, {
      headers: { "User-Agent": "Mozilla/5.0", "Cache-Control": "no-cache" },
      signal: AbortSignal.timeout(45000),
    });
    if (!response.ok) throw new Error(`${response.status} ${path}`);
    const downloaded = Buffer.from(await response.arrayBuffer());
    const hash = sha(downloaded);
    if (hash !== localSha) throw new Error(`Public build differs at ${path}: ${hash}`);
    passes.push({ sha256: hash, bytes: downloaded.length, contentType: response.headers.get("content-type") });
  }
  records.push({ path, sha256: localSha, passes });
}
await mkdir("playwright-results", { recursive: true });
await writeFile("playwright-results/hosted-files.json", JSON.stringify({
  base, sourceCommit: expected.sourceCommit, files: records,
}, null, 2) + "\n");
console.log(`HOSTED_FILES passed=${records.length} reads=2 source=${expected.sourceCommit}`);
process.env.WRECKABULARY_URL = base;
process.env.CHROMIUM_PATH = chromium.executablePath();
await import("../test/browser.mjs");
