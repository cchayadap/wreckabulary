import type { PublicBinding, PublicContent } from "../../shared/public-bindings.mjs";

const variants = ["BrawlRace/Duo", "BrawlRace/Solo", "BrawlRace/Squad", "FreeForAll/Solo", "Freestyle/Solo",
  "TeamRace/Duo", "TeamRace/Solo", "TeamRace/Squad", "TimeRun/Solo"];

export function publicContent(mode: string, format: string): PublicContent {
  const index = variants.indexOf(`${mode}/${format}`);
  if (index < 0) throw new Error("Unknown fixture variant");
  return { kind: "BuiltIn", courseId: "test-circuit", courseHash: "a".repeat(64), manifestHash: "b".repeat(64),
    modeHash: (index + 1).toString(16).repeat(64), catalogHash: "d".repeat(64) };
}

export const publicRows: PublicBinding[] = variants.map(variant => {
  const [mode, format] = variant.split("/") as [PublicBinding["mode"], PublicBinding["format"]];
  return { mode, format, content: publicContent(mode, format) };
});
