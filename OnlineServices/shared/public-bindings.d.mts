export type PublicBinding = {
  kind: "BuiltIn"; courseId: string; courseHash: string; manifestHash: string; modeHash: string; catalogHash: string;
};
export const PUBLIC_BINDING_BYTES: number;
export const PUBLIC_BINDING_SHARDS: number;
export function canonical(value: unknown): string;
export function readPublicBindings(bindings: object): PublicBinding[];
export function publicBindingVars(rows: PublicBinding[]): Record<string, string>;
export function replacePublicBindingVars(existing: Record<string, unknown>, rows: PublicBinding[]): Record<string, unknown>;
export function validateDeploymentVars(vars: unknown, reservedSecrets?: number): void;
