import { execFileSync } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const web = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const supplied = process.env.WORKERS_CI_COMMIT_SHA ?? process.env.CF_PAGES_COMMIT_SHA;
const sourceCommit = supplied && /^[a-f0-9]{40}$/i.test(supplied)
  ? supplied
  : execFileSync("git", ["-C", resolve(web, ".."), "rev-parse", "HEAD"], { encoding: "utf8" }).trim();
const info = {
  project: "Wreckabulary",
  edition: "Standalone Three.js browser game with Creative Workshop",
  sourceRepository: "https://github.com/SethyPagna/wreckabulary",
  sourceCommit,
  hosting: "Cloudflare Workers with static assets",
};
await mkdir(resolve(web, "public"), { recursive: true });
await writeFile(resolve(web, "public/BUILD-INFO.json"), JSON.stringify(info, null, 2) + "\n");
console.log(`Browser source: ${sourceCommit}`);
