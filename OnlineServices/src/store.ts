import { DurableObject } from "cloudflare:workers";
import { type ServiceEnv } from "./model";

export class Store<T> extends DurableObject<ServiceEnv> {
  constructor(ctx: DurableObjectState, env: ServiceEnv) {
    super(ctx, env);
    ctx.storage.sql.exec("CREATE TABLE IF NOT EXISTS state (id INTEGER PRIMARY KEY, value TEXT NOT NULL)");
  }
  protected read(): T | undefined {
    const rows = this.ctx.storage.sql.exec<{ value: string }>("SELECT value FROM state WHERE id = 1").toArray();
    return rows.length ? JSON.parse(rows[0].value) as T : undefined;
  }
  protected write(value: T) {
    this.ctx.storage.sql.exec("INSERT INTO state(id,value) VALUES(1,?) ON CONFLICT(id) DO UPDATE SET value=excluded.value", JSON.stringify(value));
  }
  protected transaction<R>(fn: () => R): R { return this.ctx.storage.transactionSync(fn); }
}
