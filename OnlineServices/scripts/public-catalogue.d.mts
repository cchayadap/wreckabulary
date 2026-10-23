export type PublicContent = {
  kind: "BuiltIn"; courseId: string; courseHash: string; manifestHash: string; modeHash: string; catalogHash: string;
};
export type PublicCourse = {
  mode: "TimeRun" | "TeamRace" | "BrawlRace" | "FreeForAll" | "Freestyle";
  format: "Solo" | "Duo" | "Squad"; seed: string; content: PublicContent;
};
export type PublicCatalogueProjection = {
  catalogue: { schema: 1; protocolVersion: string; mapPoolVersion: string; courses: PublicCourse[] };
  sourceSha256: string; builtinContentJson: string; bindingsSha256: string;
};
export function canonical(value: unknown): string;
export function parsePublicCatalogue(source: string): PublicCatalogueProjection;
export function readPublicCatalogue(path: string): PublicCatalogueProjection;
export function requireConfigParity(projected: PublicCatalogueProjection, config: unknown): void;
export function configurePublicCatalogue(projected: PublicCatalogueProjection, config: object): object;
export function activationProjection(projected: PublicCatalogueProjection): {
  schema: number; catalogueSha256: string; protocolVersion: string; mapPoolVersion: string;
  courseCount: number; bindingsSha256: string; builtinContentJson: string; builtinContentVars: Record<string, string>;
};
