export async function rpcValue<T extends PromiseLike<unknown>>(pending: T): Promise<Awaited<T>> {
  const value = await pending;
  try { return structuredClone(value); }
  finally {
    const disposable = value as { [Symbol.dispose]?: () => void } | undefined | null;
    disposable?.[Symbol.dispose]?.();
  }
}
