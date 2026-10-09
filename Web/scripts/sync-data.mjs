import { mkdir, readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { resolve, dirname } from "node:path";
const root = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
await mkdir(resolve(root, "Web/public/data"), { recursive: true });
for (const name of [
  "rules",
  "items",
  "house_pinwheel",
  "house_courtyard",
  "wardrobe",
]) {
  const source = await readFile(
    resolve(root, `Assets/_Project/Data/Config/${name}.json`),
    "utf8",
  );
  JSON.parse(source);
  await writeFile(resolve(root, `Web/public/data/${name}.json`), source);
}
console.log("Synced five canonical Unity data contracts.");
