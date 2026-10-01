// Browser simulation uses the canonical Unity catalogue and rule values. Rendering never mutates rules.
export const MODES = {
  Dibs: {
    title: "Dibs!",
    tag: "House brawl",
    description: "Smash, spell, survive. First to three rounds wins.",
  },
  Duos: {
    title: "Double trouble",
    tag: "2v2 · AI partner",
    description:
      "Watch each other’s backs. Hold interact to revive your buddy.",
  },
  MovingOut: {
    title: "The great escape",
    tag: "Co-op · AI partner",
    description:
      "Find every keepsake, then get the whole crew to the van before the house is packed.",
  },
  MovingDay: {
    title: "Moving day",
    tag: "Co-op · AI partner",
    description:
      "Spell the checklist furniture and place it in its marked room.",
  },
  Tutorial: {
    title: "Play & learn",
    tag: "Practice",
    description: "A friendly room to try smashing, spelling and every skill.",
  },
};
export const MAPS = [
  {
    id: "pinwheel",
    name: "Pinwheel House",
    subtitle: "Five cosy rooms. Eight sneaky shortcuts.",
    scale: 1,
    open: false,
  },
  {
    id: "courtyard",
    name: "Courtyard House",
    subtitle: "A big garden, broad paths, and four cosy wings.",
    scale: 1,
    open: false,
  },
];
export const distance = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);
const normalize = (x, z) => {
  const l = Math.hypot(x, z);
  return l ? { x: x / l, z: z / l } : { x: 0, z: 1 };
};
export function rulesFor(data, mode) {
  const d = data.rules.defaults,
    m = data.rules.modes[mode] ?? {};
  return {
    ...d,
    ...m,
    unarmed: { ...d.unarmed, ...m.unarmed },
    clearOut: { ...d.clearOut, ...m.clearOut },
  };
}
export function inArc(facing, to, arc) {
  const f = normalize(facing.x, facing.z),
    t = normalize(to.x, to.z);
  return (
    Math.hypot(to.x, to.z) > 0 &&
    f.x * t.x + f.z * t.z >= Math.cos((arc * Math.PI) / 360) - 1e-5
  );
}
export function canSpell(bag, word) {
  const letters = [...bag];
  for (const c of word) {
    const i = letters.indexOf(c);
    if (i < 0) return false;
    letters.splice(i, 1);
  }
  return true;
}
export function takeLetters(bag, word) {
  if (!canSpell(bag, word)) return null;
  const letters = [...bag];
  for (const c of word) letters.splice(letters.indexOf(c), 1);
  return letters.join("");
}
export class Game {
  constructor(data, options = {}) {
    this.data = data;
    this.catalogue = new Map(data.items.items.map((i) => [i.id, i]));
    this.mode = options.mode ?? "Dibs";
    this.map = MAPS.find((m) => m.id === options.map) ?? MAPS[0];
    this.rules = rulesFor(data, this.mode);
    this.wardrobe = options.wardrobe ?? structuredClone(data.wardrobe.default);
    this.skin = options.skin ?? "Classic";
    this.time = 0;
    this.status = options.preview ? "preview" : "playing";
    this.round = 1;
    this.wins = [0, 0, 0, 0];
    this.events = [];
    this.seed = options.seed ?? 8127;
    this.ids = 0;
    this.setup();
  }
  random() {
    this.seed = (Math.imul(this.seed, 1664525) + 1013904223) >>> 0;
    return this.seed / 4294967296;
  }
  emit(type, entity = {}, extra = {}) {
    this.events.push({ type, x: entity.x ?? 0, z: entity.z ?? 0, ...extra });
  }
  setup() {
    const s = this.map.scale;
    this.house = structuredClone(
      this.data.houses?.[this.map.id] ?? this.data.house,
    );
    this.extent = Math.max(
      ...this.house.rooms.flatMap((r) => r.bounds.map(Math.abs)),
    );
    this.house.rooms.forEach((r) => (r.bounds = r.bounds.map((n) => n * s)));
    this.house.doors.forEach((d) => {
      d.at = d.at.map((n) => n * s);
      d.width *= s;
    });
    this.items = [];
    this.tiles = [];
    this.zones = [];
    this.projectiles = [];
    this.keepsakes = [];
    this.objectives = [];
    this.minted = 0;
    this.spent = 0;
    this.roomStatus = new Map();
    this.tutorial = 0;
    this.stats = { broken: 0, crafted: 0, damage: 0, collected: 0 };
    this.roundStart = this.time;
    if (this.mode !== "MovingDay")
      this.house.furniture.forEach((f) => {
        const item = this.addItem(
          f.word,
          f.at[0] * s,
          f.at[1] * s,
          "map",
          f.yaw,
        );
        item.y = f.y ?? 0;
      });
    const count = this.mode === "Tutorial" ? 2 : 4;
    this.players = Array.from({ length: count }, (_, id) => {
      const spawn = this.house.spawns[id];
      return {
        id,
        name: ["You", "Pip", "Miso", "Clover"][id],
        team:
          this.mode === "Dibs" || this.mode === "Tutorial"
            ? id
            : this.mode === "Duos"
              ? Math.floor(id / 2)
              : 0,
        x: spawn.at[0] * s,
        z: spawn.at[1] * s,
        y: 0,
        yaw: (spawn.yaw * Math.PI) / 180,
        facing: {
          x: Math.sin((spawn.yaw * Math.PI) / 180),
          z: Math.cos((spawn.yaw * Math.PI) / 180),
        },
        hp: this.rules.maxHealth,
        maxHp: this.rules.maxHealth,
        state: "alive",
        bag: this.rules.starterLetters,
        carried: null,
        action: null,
        slots: Array(this.rules.maxCarried).fill(null),
        slot: 0,
        craft: null,
        bubble: 0,
        bubbleUntil: 0,
        invulnerableUntil: this.time + this.rules.spawnProtection,
        staggerUntil: 0,
        stunUntil: 0,
        lastDodge: -Infinity,
        dodgingUntil: 0,
        jumpUntil: 0,
        attackAt: 0,
        attackingUntil: 0,
        block: false,
        downs: 0,
        wardrobe:
          id === 0
            ? this.wardrobe
            : {
                ...this.wardrobe,
                colours: {
                  ...this.wardrobe.colours,
                  Top: ["pool", "tomato", "sunflower", "grape"][id],
                },
              },
        ai: id !== 0,
        aiTimer: 0,
        reviveTarget: null,
      };
    });
    this.players.forEach((p) => (this.minted += p.bag.length));
    if (this.mode === "MovingOut") {
      (
        this.house.keepsakes ??
        ["Kitchen", "Bedroom", "Study"].map((room) => ({ room }))
      ).forEach((k, id) => {
        const b = this.house.rooms.find((r) => r.name === k.room).bounds;
        this.keepsakes.push({
          id,
          room: k.room,
          x: k.at?.[0] ?? (b[0] + b[2]) / 2,
          z: k.at?.[1] ?? (b[1] + b[3]) / 2,
          collected: false,
        });
      });
      const extraction = this.house.extractionAt ?? [-7, -7];
      this.extraction = { x: extraction[0], z: extraction[1] };
    }
    if (this.mode === "MovingDay") {
      (
        this.house.movingDay ??
        [
          ["BED", "Bedroom"],
          ["TABLE", "Kitchen"],
          ["SOFA", "LivingRoom"],
          ["LAMP", "Study"],
        ].map(([word, room]) => ({ word, room }))
      ).forEach(({ word, room }) => {
        const b = this.house.rooms.find((r) => r.name === room).bounds;
        this.objectives.push({
          word,
          room,
          x: (b[0] + b[2]) / 2,
          z: (b[1] + b[3]) / 2,
          done: false,
        });
      });
    }
    if (this.mode === "MovingOut") {
      for (const k of this.keepsakes) {
        const item = this.items
          .filter((i) => !i.keepsake && this.roomAt(i)?.name === k.room)
          .sort((a, b) => distance(a, k) - distance(b, k))[0];
        if (item) {
          item.invulnerable = true;
          item.keepsake = k.id;
          k.itemId = item.id;
          k.word = item.word;
          k.x = item.x;
          k.z = item.z;
        }
      }
    }
    if (this.mode === "MovingDay") {
      this.objectives.forEach((o) => this.deliver(o.word));
      this.nextResupply = this.time + 2;
      this.nextSpill = this.time + 15;
    }
    this.walls = this.makeWalls();
    this.emit("round", {}, { round: this.round });
  }
  makeWalls() {
    const grouped = new Map();
    for (const room of this.house.rooms) {
      const [x1, z1, x2, z2] = room.bounds;
      for (const [axis, fixed, min, max] of [
        ["H", z1, x1, x2],
        ["H", z2, x1, x2],
        ["V", x1, z1, z2],
        ["V", x2, z1, z2],
      ]) {
        const key = `${axis}:${fixed}`;
        if (!grouped.has(key)) grouped.set(key, []);
        grouped.get(key).push([min, max]);
      }
    }
    const walls = [];
    for (const [key, intervals] of grouped) {
      const [axis, value] = key.split(":"),
        fixed = Number(value),
        horizontal = axis === "H";
      intervals.sort((a, b) => a[0] - b[0]);
      const merged = [];
      for (const interval of intervals) {
        const previous = merged.at(-1);
        if (previous && interval[0] <= previous[1])
          previous[1] = Math.max(previous[1], interval[1]);
        else merged.push([...interval]);
      }
      const holes = this.house.doors
        .filter(
          (d) => Math.abs((horizontal ? d.at[1] : d.at[0]) - fixed) < 0.01,
        )
        .map((d) => [
          (horizontal ? d.at[0] : d.at[1]) - d.width / 2,
          (horizontal ? d.at[0] : d.at[1]) + d.width / 2,
        ])
        .sort((a, b) => a[0] - b[0]);
      for (const [min, max] of merged) {
        let start = min;
        for (const [a, b] of holes) {
          if (b <= min || a >= max) continue;
          if (a > start)
            walls.push(
              horizontal
                ? { x1: start, z1: fixed, x2: a, z2: fixed }
                : { x1: fixed, z1: start, x2: fixed, z2: a },
            );
          start = Math.max(start, b);
        }
        if (start < max)
          walls.push(
            horizontal
              ? { x1: start, z1: fixed, x2: max, z2: fixed }
              : { x1: fixed, z1: start, x2: fixed, z2: max },
          );
      }
    }
    return walls;
  }

  addItem(word, x, z, origin = "crafted", rotation = 0) {
    const definition = this.catalogue.get(word);
    const toughness =
      this.rules.furnitureToughness[0] +
      this.rules.furnitureToughness[1] * word.length;
    const item = {
      id: ++this.ids,
      word,
      definition,
      origin,
      state: "world",
      x,
      z,
      y: 0,
      rotation: (rotation * Math.PI) / 180,
      durability: origin === "map" ? toughness : (definition?.durability ?? 20),
      maxDurability:
        origin === "map" ? toughness : (definition?.durability ?? 20),
      owner: null,
      deployer: null,
      spent: false,
    };
    this.items.push(item);
    if (origin === "map") this.minted += word.length;
    return item;
  }
  item(id) {
    return this.items.find((i) => i.id === id && i.state !== "gone");
  }
  held(p) {
    return this.item(p.carried) ?? this.item(p.slots[p.slot]);
  }
  canAct(p) {
    return (
      this.status === "playing" &&
      p.state === "alive" &&
      this.time >= p.stunUntil &&
      this.time >= p.dodgingUntil
    );
  }
  freeSlot(p) {
    return p.slots.findIndex((id, i) => id === null && p.craft?.slot !== i);
  }
  roomAt(p) {
    return this.house.rooms.find(
      (r) =>
        p.x >= r.bounds[0] &&
        p.x <= r.bounds[2] &&
        p.z >= r.bounds[1] &&
        p.z <= r.bounds[3],
    );
  }
  mintTiles(word, x, z, mint = true) {
    if (mint) this.minted += word.length;
    for (const char of word) {
      const a = this.random() * Math.PI * 2,
        r = 0.3 + this.random() * 0.65;
      this.tiles.push({
        id: ++this.ids,
        char,
        x: x + Math.cos(a) * r,
        z: z + Math.sin(a) * r,
        born: this.time,
      });
    }
  }
  collect(p, tile) {
    if (
      p.state !== "alive" ||
      p.bag.length + (p.craft?.word.length ?? 0) >= this.rules.maxLetters
    )
      return false;
    const index = this.tiles.indexOf(tile);
    if (index < 0) return false;
    this.tiles.splice(index, 1);
    p.bag += tile.char;
    if (p.id === 0) this.stats.collected++;
    this.emit("pickup", tile, { char: tile.char, player: p.id });
    return true;
  }
  craft(p, word) {
    word = word.toUpperCase();
    const def = this.catalogue.get(word);
    if (!this.canAct(p)) return "Wait until you can act again.";
    if (!def?.enabled) return "That recipe is not available.";
    if (p.carried !== null) return "Set down the furniture first.";
    if (p.action || p.pendingAttack || this.time < (p.meleeUntil ?? 0))
      return "Finish your current action first.";
    if (p.craft) return "Finish your current word first.";
    const slot = this.freeSlot(p);
    if (slot < 0) return "Both hands are full. Drop some gear first.";
    const remaining = takeLetters(p.bag, word);
    if (remaining === null) return "You still need some letters.";
    p.bag = remaining;
    p.craft = {
      word,
      slot,
      start: this.time,
      readyAt:
        this.time +
        this.rules.craftBase +
        this.rules.craftPerLetter * word.length,
    };
    this.emit("craftStart", p, { word, player: p.id });
    return null;
  }
  cancelCraft(p) {
    if (!p.craft) return false;
    p.bag += p.craft.word;
    p.craft = null;
    this.emit("craftCancel", p);
    return true;
  }
  completeCraft(p) {
    if (!p.craft || this.time < p.craft.readyAt) return;
    if (!this.canAct(p)) {
      this.cancelCraft(p);
      return;
    }
    const job = p.craft,
      item = this.addItem(job.word, p.x, p.z);
    item.state = "held";
    item.owner = p.id;
    p.slots[job.slot] = item.id;
    p.slot = job.slot;
    p.craft = null;
    if (p.id === 0) this.stats.crafted++;
    this.emit("craft", p, { word: item.word, player: p.id });
  }
  selectSlot(p, slot) {
    if (slot < 0 || slot >= p.slots.length || slot === p.slot) return false;
    p.slot = slot;
    p.action = null;
    p.pendingAttack = null;
    p.meleeUntil = 0;
    return true;
  }
  drop(p) {
    const item = this.held(p);
    if (!item) return false;
    p.action = null;
    p.pendingAttack = null;
    p.meleeUntil = 0;
    if (p.carried === item.id) p.carried = null;
    else p.slots[p.slot] = null;
    item.state = "world";
    item.owner = null;
    item.x = p.x + p.facing.x * 0.9;
    item.z = p.z + p.facing.z * 0.9;
    item.y = 0;
    this.emit("drop", item, { word: item.word });
    return true;
  }
  giveLetter(from, to, char) {
    if (
      from.id === to.id ||
      from.team !== to.team ||
      !this.canAct(from) ||
      to.state !== "alive" ||
      to.bag.length + (to.craft?.word.length ?? 0) >= this.rules.maxLetters
    )
      return false;
    const index = from.bag.indexOf(char);
    if (index < 0) return false;
    from.bag = from.bag.slice(0, index) + from.bag.slice(index + 1);
    to.bag += char;
    this.emit("gift", to, { char, player: to.id });
    return true;
  }
  tossLetter(p, char) {
    const i = p.bag.indexOf(char);
    if (i < 0) return false;
    p.bag = p.bag.slice(0, i) + p.bag.slice(i + 1);
    this.mintTiles(char, p.x + p.facing.x, p.z + p.facing.z, false);
    return true;
  }
  breakItem(item) {
    if (!item || item.invulnerable || item.state === "gone") return;
    this.zones = this.zones.filter((z) => z.id !== item.id);
    const holder = this.players[item.owner];
    if (holder)
      holder.slots = holder.slots.map((id) => (id === item.id ? null : id));
    item.state = "gone";
    if (!item.spent) this.mintTiles(item.word, item.x, item.z, false);
    if (item.origin === "map") this.stats.broken++;
    this.emit("break", item, { word: item.word });
  }
  damageItem(item, amount) {
    if (!item || item.invulnerable || item.state === "gone" || amount <= 0)
      return;
    item.durability = Math.max(0, item.durability - amount);
    this.emit("furnitureHit", item, { amount });
    if (item.durability === 0) this.breakItem(item);
  }
  spend(item) {
    if (item.spent) return;
    item.spent = true;
    this.spent += item.word.length;
  }
  dodge(p) {
    if (!this.canAct(p) || this.time - p.lastDodge < this.rules.dodgeCooldown)
      return false;
    p.action = null;
    p.pendingAttack = null;
    p.meleeUntil = 0;
    this.cancelCraft(p);
    p.dodgeDirection = { ...p.facing };
    p.lastDodge = this.time;
    p.dodgingUntil = this.time + this.rules.dodgeSeconds;
    p.invulnerableUntil = Math.max(
      p.invulnerableUntil,
      this.time + this.rules.dodgeInvulnerable,
    );
    this.emit("dodge", p);
    return true;
  }
  launch(p, velocity) {
    p.jumpStarted = this.time;
    p.jumpVelocity = velocity;
    p.jumpDuration = (2 * velocity) / 23.81;
    p.jumpUntil = this.time + p.jumpDuration;
  }
  jump(p) {
    if (!this.canAct(p) || this.time < p.jumpUntil) return false;
    this.launch(p, Math.sqrt(2 * 23.81 * this.rules.jumpHeight));
    this.emit("jump", p);
    return true;
  }
  hit(p, hit) {
    if (p.state !== "alive" || this.time < p.invulnerableUntil)
      return { ignored: true };
    if (
      hit.attacker !== p.id &&
      hit.attacker >= 0 &&
      !this.rules.friendlyFire &&
      this.players[hit.attacker]?.team === p.team
    )
      return { ignored: true };
    let damage = Math.max(0, hit.damage),
      blocked = 0;
    const shield = this.held(p)?.definition?.shield;
    if (
      p.block &&
      shield &&
      hit.blockable !== false &&
      inArc(p.facing, { x: -hit.dx, z: -hit.dz }, shield.frontArc)
    ) {
      blocked = damage * shield.reduction;
      damage -= blocked;
      this.damageItem(this.held(p), blocked);
      this.emit("block", p);
    }
    const absorbed = this.time < p.bubbleUntil ? Math.min(damage, p.bubble) : 0;
    p.bubble -= absorbed;
    damage -= absorbed;
    damage = Math.min(damage, p.hp);
    p.hp -= damage;
    if (damage > 0) {
      if (this.time >= p.staggerUntil) {
        p.stunUntil =
          this.time + Math.min(hit.hitStun ?? 0, this.rules.hitStunMax);
        p.staggerUntil = this.time + this.rules.staggerImmunity;
      }
      if (hit.attacker === 0) this.stats.damage += damage;
      this.emit("hit", p, { amount: damage, player: p.id });
      p.reviveTarget = null;
      p.pendingAttack = null;
      p.meleeUntil = 0;
      p.action = null;
      const force = (hit.knockback ?? 0) * 0.07 * (blocked ? 0.3 : 1);
      this.move(p, (hit.dx ?? 0) * force, (hit.dz ?? 0) * force);
      if (p.craft) this.cancelCraft(p);
    }
    if (p.hp <= 0) {
      p.hp = 0;
      p.block = false;
      p.action = null;
      if (p.carried !== null) this.drop(p);
      if (this.rules.downed) {
        p.state = "downed";
        p.downs++;
        p.bleedAt =
          this.time +
          this.rules.bleedOut[
            Math.min(p.downs - 1, this.rules.bleedOut.length - 1)
          ];
        this.emit("downed", p);
      } else this.eliminate(p);
    }
    return { damage, blocked, absorbed, ignored: false };
  }
  eliminate(p) {
    if (p.state === "eliminated") return;
    p.action = null;
    this.cancelCraft(p);
    if (p.carried !== null) this.drop(p);
    this.mintTiles(p.bag, p.x, p.z, false);
    p.bag = "";
    for (let slot = 0; slot < p.slots.length; slot++) {
      const previous = p.slot;
      p.slot = slot;
      this.drop(p);
      p.slot = previous;
    }
    p.state = "eliminated";
    p.hp = 0;
    p.respawnAt =
      this.rules.respawn >= 0 ? this.time + this.rules.respawn : Infinity;
    this.emit("eliminated", p, { player: p.id });
  }
  attack(p) {
    if (
      !this.canAct(p) ||
      p.craft ||
      p.action ||
      this.time < p.attackAt ||
      this.time < p.stunUntil
    )
      return false;
    const item = this.held(p),
      def = item?.origin === "map" ? null : item?.definition;
    if (item?.origin === "map") {
      this.throw(p);
      return true;
    }
    if (def?.use) {
      this.use(p);
      return true;
    }
    if (def?.thrown) {
      this.throw(p);
      return true;
    }
    if (def?.deploy && !def?.melee) {
      this.deploy(p);
      return true;
    }
    const stats = def?.melee ?? this.rules.unarmed;
    p.attackAt = this.time + stats.windup + stats.active + stats.recovery;
    p.meleeUntil = p.attackAt;
    p.attackingUntil = this.time + 0.22;
    p.pendingAttack = {
      at: this.time + stats.windup,
      stats,
      itemId: item?.id,
      slot: p.slot,
      seenPlayers: new Set(),
      seenItems: new Set(),
      dx: p.facing.x,
      dz: p.facing.z,
    };
    this.emit("swing", p, { word: item?.word ?? "PUNCH", player: p.id });
    return true;
  }
  resolveAttack(p, a) {
    a.seenPlayers ??= new Set();
    a.seenItems ??= new Set();
    const center = { x: p.x, z: p.z };
    for (const q of this.players) {
      if (
        a.seenPlayers.has(q.id) ||
        q.id === p.id ||
        q.state !== "alive" ||
        distance(q, center) > a.stats.reach + 0.35
      )
        continue;
      if (
        !this.segmentBlocked(p, q) &&
        inArc({ x: a.dx, z: a.dz }, { x: q.x - p.x, z: q.z - p.z }, a.stats.arc)
      ) {
        a.seenPlayers.add(q.id);
        this.hit(q, {
          ...a.stats,
          attacker: p.id,
          dx: a.dx,
          dz: a.dz,
          blockable: true,
        });
      }
    }
    for (const i of this.items) {
      if (
        a.seenItems.has(i.id) ||
        !["world", "deployed"].includes(i.state) ||
        i.owner !== null
      )
        continue;
      const reach =
        a.stats.reach + Math.min(0.5, (i.definition?.size?.[0] ?? 0.4) / 2);
      if (
        !this.segmentBlocked(p, i) &&
        distance(i, p) < reach &&
        inArc(
          { x: a.dx, z: a.dz },
          { x: i.x - p.x, z: i.z - p.z },
          Math.max(a.stats.arc, 105),
        )
      ) {
        a.seenItems.add(i.id);
        this.damageItem(i, a.stats.breakPower);
      }
    }
    if (a.itemId && !a.worn) {
      a.worn = true;
      this.damageItem(this.item(a.itemId), 1);
    }
  }
  use(p) {
    const item = this.held(p),
      use = item?.definition?.use;
    if (
      !this.canAct(p) ||
      item?.origin !== "crafted" ||
      !use ||
      p.action ||
      p.craft
    )
      return false;
    p.action = {
      kind: "use",
      itemId: item.id,
      start: this.time,
      readyAt: this.time + (use.channel ?? 0),
    };
    p.attackAt = p.action.readyAt;
    this.emit("useStart", p, { word: item.word });
    if (!use.channel) this.completeAction(p);
    return true;
  }
  applyUse(p) {
    const item = this.held(p),
      use = item?.definition?.use;
    if (p.state !== "alive" || !use) return false;
    this.spend(item);
    p.slots[p.slot] = null;
    item.state = "gone";
    if (use.effect === "Bubble") {
      p.bubble = use.amount;
      p.bubbleUntil = this.time + use.seconds;
    } else if (use.effect === "Heal")
      p.hp = Math.min(p.maxHp, p.hp + use.amount);
    else if (use.effect === "Speed") {
      p.speedUntil = this.time + use.seconds;
      p.speedStrength = use.amount;
    }
    this.emit("buff", p, { word: item.word });
    return true;
  }
  throw(p) {
    const item = this.held(p);
    if (!this.canAct(p) || p.action || p.craft || !item) return false;
    const stats = item.definition?.thrown ?? {
      damage: 18,
      speed: 9,
      knockback: 5,
      breakPower: 2,
      recoverable: true,
    };
    p.action = null;
    p.pendingAttack = null;
    p.meleeUntil = 0;
    if (p.carried === item.id) p.carried = null;
    else p.slots[p.slot] = null;
    item.state = "thrown";
    item.owner = null;
    item.thrower = p.id;
    item.skin = p.id === 0 ? this.skin : "Classic";
    if (item.definition?.consumable && stats.fuse) this.spend(item);
    const pr = {
      id: item.id,
      item,
      word: item.word,
      x: p.x + p.facing.x * 0.6,
      z: p.z + p.facing.z * 0.6,
      y: 0.85,
      vx: p.facing.x * stats.speed,
      vz: p.facing.z * stats.speed,
      born: this.time,
      fuseAt: stats.fuse ? this.time + stats.fuse : null,
      owner: p.id,
      stats,
    };
    this.projectiles.push(pr);
    p.attackAt = this.time + 0.4;
    this.emit("throw", p, { word: item.word });
    return true;
  }
  deploy(p) {
    const item = this.held(p),
      def = item?.definition;
    if (!this.canAct(p) || !item) return "Nothing equipped.";
    if (p.action || p.craft || this.time < (p.meleeUntil ?? 0))
      return "Finish your current action first.";
    if (
      !def?.deploy &&
      !def?.thrown?.fuse &&
      !(
        this.mode === "MovingDay" &&
        this.objectives.some((o) => o.word === item.word && !o.done)
      )
    )
      return "This is handheld gear. Attack or throw it.";
    if (
      this.items.filter(
        (i) => i.deployer === p.id && ["deployed", "armed"].includes(i.state),
      ).length >= this.rules.maxDeployed
    )
      return "Two tools are already placed. Break one to make room.";
    p.action = {
      kind: "deploy",
      itemId: item.id,
      start: this.time,
      readyAt: this.time + (def.deploy?.place ?? 0),
    };
    this.emit("placeStart", p, { word: item.word });
    if (!def.deploy?.place) this.completeAction(p);
    return null;
  }
  completeAction(p) {
    if (!p.action || this.time < p.action.readyAt) return;
    const action = p.action;
    p.action = null;
    if (p.state !== "alive" || this.held(p)?.id !== action.itemId) return;
    if (action.kind === "use") this.applyUse(p);
    else this.applyDeploy(p);
  }
  applyDeploy(p) {
    const item = this.held(p),
      def = item?.definition;
    if (!this.canAct(p) || !item) return "Nothing equipped.";
    if (
      !def?.deploy &&
      !def?.thrown?.fuse &&
      !(
        this.mode === "MovingDay" &&
        this.objectives.some((o) => o.word === item.word && !o.done)
      )
    )
      return "This is handheld gear. Attack or throw it.";
    const count = this.items.filter(
      (i) => i.deployer === p.id && ["deployed", "armed"].includes(i.state),
    ).length;
    if (count >= this.rules.maxDeployed)
      return "Two tools are already placed. Break one to make room.";
    item.x = p.x + p.facing.x * 1.6;
    item.z = p.z + p.facing.z * 1.6;
    item.rotation = p.yaw;
    item.owner = null;
    item.deployer = p.id;
    p.slots[p.slot] = null;
    if (this.mode === "MovingDay") {
      const objective = this.objectives.find(
        (o) =>
          !o.done && o.word === item.word && this.roomAt(item)?.name === o.room,
      );
      if (objective) {
        objective.done = true;
        item.state = "deployed";
        item.checklist = true;
        item.deployer = null;
        this.emit("objective", item, { word: item.word });
        return null;
      }
    }
    item.state = def.consumable ? "armed" : "deployed";
    if (def.consumable) this.spend(item);
    if (def.thrown?.fuse) {
      this.projectiles.push({
        id: item.id,
        item,
        word: item.word,
        x: item.x,
        z: item.z,
        y: 0,
        vx: 0,
        vz: 0,
        born: this.time,
        fuseAt: this.time + def.thrown.fuse,
        owner: p.id,
        stats: def.thrown,
      });
    } else if (!def.deploy) {
      item.state = "world";
      return null;
    } else if (def.deploy.effect !== "Cover") {
      this.zones.push({
        id: item.id,
        word: item.word,
        x: item.x,
        z: item.z,
        radius: def.deploy.radius ?? 1,
        expires: def.deploy.lifetime
          ? this.time + def.deploy.lifetime
          : Infinity,
        strength: def.deploy.strength,
        effect: def.deploy.effect,
        owner: p.id,
        rotation: item.rotation,
        footprint: def.deploy.footprint,
        lastLaunch: new Map(),
      });
    }
    this.emit("deploy", item, { word: item.word });
    return null;
  }
  interact(p, dt = 0) {
    if (
      !this.canAct(p) ||
      p.action ||
      p.craft ||
      this.time < (p.meleeUntil ?? 0)
    ) {
      p.reviveTarget = null;
      return false;
    }
    const friend = this.players.find(
      (q) =>
        q.state === "downed" &&
        q.team === p.team &&
        distance(q, p) <= this.rules.reviveRange,
    );
    if (friend) {
      if (p.reviveTarget !== friend.id) {
        p.reviveTarget = friend.id;
        p.reviveStart = this.time;
      }
      if (this.time - p.reviveStart >= this.rules.reviveSeconds) {
        friend.state = "alive";
        friend.hp = this.rules.reviveHealth;
        friend.invulnerableUntil = this.time + 1;
        p.reviveTarget = null;
        this.emit("revive", friend);
      }
      return true;
    }
    if (this.mode === "MovingOut") {
      const k = this.keepsakes.find(
        (k) =>
          !k.collected &&
          this.item(k.itemId)?.state === "world" &&
          distance(k, p) < 1.6,
      );
      if (k && p.carried === null && !p.craft && !p.action) {
        const item = this.item(k.itemId);
        item.state = "carried";
        item.owner = p.id;
        p.carried = item.id;
        this.emit("carry", p, { word: item.word });
        return true;
      }
    }
    const item = this.items
        .filter(
          (i) =>
            i.origin === "crafted" &&
            ["world", "deployed"].includes(i.state) &&
            !i.checklist &&
            distance(i, p) < 1.6,
        )
        .sort((a, b) => distance(a, p) - distance(b, p))[0],
      slot = this.freeSlot(p);
    p.reviveTarget = null;
    if (item && slot >= 0) {
      p.slots[slot] = item.id;
      p.slot = slot;
      this.zones = this.zones.filter((z) => z.id !== item.id);
      item.deployer = null;
      item.state = "held";
      item.owner = p.id;
      this.emit("equip", p, { word: item.word });
      return true;
    }
    const original = this.items
      .filter(
        (i) =>
          i.origin === "map" &&
          i.state === "world" &&
          !i.packed &&
          distance(i, p) < 1.6 &&
          !this.segmentBlocked(p, i),
      )
      .sort((a, b) => distance(a, p) - distance(b, p))[0];
    if (original && p.carried === null) {
      original.state = "carried";
      original.owner = p.id;
      p.carried = original.id;
      this.emit("carry", p, { word: original.word });
      return true;
    }
    return false;
  }
  floorAt(x, z) {
    if (this.map.id !== "pinwheel" || x < 2.5 || x > 4) return 0;
    if (z >= 1 && z <= 4) return 1.7;
    if (z >= -1 && z < 1) return (z + 1) * 0.85;
    return 0;
  }
  canStand(p, x, z) {
    const radius = 0.3;
    if (
      Math.abs(x) > this.extent - radius ||
      Math.abs(z) > this.extent - radius
    )
      return false;
    for (const wall of this.walls) {
      if (
        wall.x1 === wall.x2 &&
        Math.abs(x - wall.x1) < radius &&
        z > wall.z1 - radius &&
        z < wall.z2 + radius
      )
        return false;
      if (
        wall.z1 === wall.z2 &&
        Math.abs(z - wall.z1) < radius &&
        x > wall.x1 - radius &&
        x < wall.x2 + radius
      )
        return false;
    }
    for (const i of this.items) {
      if (
        !["world", "deployed"].includes(i.state) ||
        i.definition?.deploy?.effect === "JumpPad" ||
        i.definition?.deploy?.effect === "SpeedStrip" ||
        i.definition?.category === "Consumables"
      )
        continue;
      const size = i.delivery
          ? [0.65, 0.55, 0.55]
          : (i.definition?.size ?? [0.5, 0.5, 0.5]),
        rx = Math.min(size[0] / 2, 0.9),
        rz = Math.min(size[2] / 2, 0.9),
        c = Math.cos(i.rotation),
        s = Math.sin(i.rotation),
        localX = (x - i.x) * c - (z - i.z) * s,
        localZ = (x - i.x) * s + (z - i.z) * c;
      if (
        Math.abs(localX) < rx + radius &&
        Math.abs(localZ) < rz + radius &&
        p.y + 1.2 > (i.y ?? 0) &&
        p.y < (i.y ?? 0) + (size[1] ?? 0.5)
      )
        return false;
    }
    return true;
  }
  move(p, dx, dz) {
    const steps = Math.max(1, Math.ceil(Math.hypot(dx, dz) / 0.12));
    for (let n = 0; n < steps; n++) {
      const x = p.x + dx / steps,
        z = p.z + dz / steps;
      if (this.canStand(p, x, p.z)) p.x = x;
      if (this.canStand(p, p.x, z)) p.z = z;
    }
  }

  segmentHitsItem(a, b, item, pad = 0.15) {
    const size = item.delivery
        ? [0.65, 0.55, 0.55]
        : (item.definition?.size ?? [0.5, 0.5, 0.5]),
      c = Math.cos(item.rotation),
      s = Math.sin(item.rotation),
      local = (q) => ({
        x: (q.x - item.x) * c - (q.z - item.z) * s,
        z: (q.x - item.x) * s + (q.z - item.z) * c,
      }),
      from = local(a),
      to = local(b),
      r = { x: size[0] / 2 + pad, z: size[2] / 2 + pad };
    let near = 0,
      far = 1;
    for (const axis of ["x", "z"]) {
      const d = to[axis] - from[axis];
      if (Math.abs(d) < 1e-6) {
        if (Math.abs(from[axis]) > r[axis]) return false;
        continue;
      }
      let t1 = (-r[axis] - from[axis]) / d,
        t2 = (r[axis] - from[axis]) / d;
      if (t1 > t2) [t1, t2] = [t2, t1];
      near = Math.max(near, t1);
      far = Math.min(far, t2);
      if (near > far) return false;
    }
    return true;
  }

  segmentBlocked(a, b) {
    for (const wall of this.walls) {
      if (wall.x1 === wall.x2) {
        const dx = b.x - a.x;
        if (Math.abs(dx) < 1e-6) continue;
        const t = (wall.x1 - a.x) / dx,
          z = a.z + (b.z - a.z) * t;
        if (t > 1e-5 && t < 1 - 1e-5 && z >= wall.z1 && z <= wall.z2)
          return true;
      } else {
        const dz = b.z - a.z;
        if (Math.abs(dz) < 1e-6) continue;
        const t = (wall.z1 - a.z) / dz,
          x = a.x + (b.x - a.x) * t;
        if (t > 1e-5 && t < 1 - 1e-5 && x >= wall.x1 && x <= wall.x2)
          return true;
      }
    }
    return false;
  }

  aim(p, x, z) {
    if (Math.hypot(x - p.x, z - p.z) < 0.05) return;
    p.facing = normalize(x - p.x, z - p.z);
    p.yaw = Math.atan2(p.facing.x, p.facing.z);
  }
  ai(p, dt) {
    if (!this.canAct(p) || this.mode === "Tutorial") return;
    p.aiTimer -= dt;
    const ally = this.players.find(
      (q) => q.state === "downed" && q.team === p.team,
    );
    let target;
    if (ally) {
      target = ally;
      if (distance(p, ally) < this.rules.reviveRange) {
        this.interact(p);
        return;
      }
    } else if (this.mode === "MovingOut") {
      if (p.carried !== null) {
        target = this.extraction;
        if (distance(p, target) < 1.2) this.drop(p);
      } else {
        target =
          this.keepsakes
            .filter(
              (k) => !k.collected && this.item(k.itemId)?.state === "world",
            )
            .sort((a, b) => distance(a, p) - distance(b, p))[0] ??
          this.extraction;
        if (target.itemId !== undefined && distance(p, target) < 1.5)
          this.interact(p);
      }
    } else if (this.mode === "MovingDay") {
      p.desiredWord = this.objectives[p.id % this.objectives.length].word;
      const assigned = this.objectives.find(
        (o) => !o.done && o.word === p.desiredWord,
      );
      if (!assigned) p.desiredWord = this.objectives.find((o) => !o.done)?.word;
      const held = this.held(p);
      const goal =
        this.objectives.find((o) => !o.done && held?.word === o.word) ??
        this.objectives.find((o) => !o.done && o.word === p.desiredWord);
      if (!goal) return;
      const leader =
        this.players[this.objectives.indexOf(goal) % this.players.length];
      p.supporting = leader.id !== p.id;
      if (p.supporting) {
        const need = [...goal.word];
        for (const c of leader.bag) {
          const index = need.indexOf(c);
          if (index >= 0) need.splice(index, 1);
        }
        const gift = need.find((c) => p.bag.includes(c));
        if (gift) {
          target = leader;
          if (distance(p, leader) < 1.8) this.giveLetter(p, leader, gift);
        }
      }
      if (held && held.word !== goal.word) {
        this.drop(p);
        return;
      }
      if (!target && !p.craft && !held) {
        const parcel = this.items
          .filter(
            (i) => i.delivery && i.state === "world" && i.word === goal.word,
          )
          .sort((a, b) => distance(a, p) - distance(b, p))[0];
        if (parcel) target = parcel;
      }
      if (held?.word === goal.word) {
        target = goal;
        if (distance(p, goal) < 1.5) this.deploy(p);
      } else if (
        !p.supporting &&
        canSpell(p.bag, goal.word) &&
        !p.craft &&
        this.freeSlot(p) >= 0
      )
        this.craft(p, goal.word);
    }
    const enemies = this.players.filter(
      (q) => q.team !== p.team && q.state === "alive",
    );
    const enemy = enemies.sort((a, b) => distance(a, p) - distance(b, p))[0];
    if (!target && this.mode === "Tutorial") {
      target = { x: 0, z: 0 };
    }
    if (!target && enemy && this.time - this.roundStart > 10 && this.held(p)) {
      target = enemy;
      if (
        distance(p, enemy) <
        (this.held(p)?.definition?.melee?.reach ?? 1.1) + 0.25
      ) {
        this.aim(p, enemy.x, enemy.z);
        if (p.aiTimer <= 0) {
          this.attack(p);
          p.aiTimer = 0.45 + this.random() * 0.5;
        }
        return;
      }
    }
    if (!target) {
      const tile = this.tiles
        .filter(
          (t) =>
            p.bag.length < this.rules.maxLetters &&
            (!p.desiredWord ||
              [...p.desiredWord].filter((c) => c === t.char).length >
                [...p.bag].filter((c) => c === t.char).length),
        )
        .sort((a, b) => distance(a, p) - distance(b, p))[0];
      if (tile) target = tile;
      else
        target =
          this.items
            .filter((i) => i.origin === "map" && i.state === "world")
            .sort((a, b) => distance(a, p) - distance(b, p))[0] ?? enemy;
    }
    if (!p.craft && !p.action && !this.held(p) && this.mode !== "MovingDay") {
      const recipe = ["BAT", "BLADE", "LAMP", "BALL", "BOMB", "FOAM"].find(
        (w) => this.catalogue.get(w)?.enabled && canSpell(p.bag, w),
      );
      if (recipe) this.craft(p, recipe);
    }
    if (target) {
      const d = distance(target, p);
      this.aim(p, target.x, target.z);
      if (
        target.origin === "map" &&
        d <
          (this.held(p)?.definition?.melee?.reach ?? this.rules.unarmed.reach) +
            Math.min(
              0.5,
              (target.delivery ? 0.65 : (target.definition?.size?.[0] ?? 0.4)) /
                2,
            ) -
            0.08
      ) {
        if (p.aiTimer <= 0) {
          this.attack(p);
          p.aiTimer = 0.6;
        }
        return;
      }
      const waypoint = this.waypoint(p, target);
      let dir = normalize(waypoint.x - p.x, waypoint.z - p.z);
      const before = { x: p.x, z: p.z };
      p.motion = { ...dir };
      this.move(p, dir.x * 3.3 * dt, dir.z * 3.3 * dt);
      if (distance(before, p) < 0.01 && d > 1) {
        const options = [
          { x: -dir.z, z: dir.x },
          { x: dir.z, z: -dir.x },
        ];
        for (const alternative of options) {
          this.move(p, alternative.x * 3.3 * dt, alternative.z * 3.3 * dt);
          if (distance(before, p) > 0.02) break;
        }
      }

      if (
        enemy &&
        distance(p, enemy) < 1.35 &&
        this.time - this.roundStart > 10 &&
        p.aiTimer <= 0 &&
        this.mode !== "Tutorial"
      ) {
        this.aim(p, enemy.x, enemy.z);
        this.attack(p);
        p.aiTimer = 0.85;
      }
    }
  }
  waypoint(p, target) {
    const cached = p.navigation;
    if (
      cached &&
      this.time < cached.until &&
      distance(cached.goal, target) < 1.2
    ) {
      while (cached.path.length > 1 && distance(p, cached.path[0]) < 0.3)
        cached.path.shift();
      if (cached.path.length) return cached.path[0];
    }
    const step = 0.45,
      min = -this.extent + 0.4,
      n = Math.ceil((this.extent * 2 - 0.8) / step),
      index = (x, z) =>
        Math.round((z - min) / step) * n + Math.round((x - min) / step),
      point = (id) => ({
        x: min + (id % n) * step,
        z: min + Math.floor(id / n) * step,
      }),
      start = index(p.x, p.z);
    let goal = index(target.x, target.z);
    if (!this.canStand(p, point(goal).x, point(goal).z)) {
      let best = Infinity;
      for (let j = -3; j <= 3; j++)
        for (let i = -3; i <= 3; i++) {
          const candidate = goal + j * n + i,
            q = point(candidate);
          if (
            candidate < 0 ||
            candidate >= n * n ||
            !this.canStand(p, q.x, q.z)
          )
            continue;
          const score = distance(q, target) + distance(q, p) * 0.03;
          if (score < best) {
            best = score;
            goal = candidate;
          }
        }
    }
    const open = [start],
      came = new Map(),
      scores = new Map([[start, 0]]),
      closed = new Set();
    let found = false,
      iterations = 0;
    while (open.length && iterations++ < n * n) {
      let bestIndex = 0,
        bestScore = Infinity;
      for (let i = 0; i < open.length; i++) {
        const id = open[i],
          score = scores.get(id) + distance(point(id), point(goal));
        if (score < bestScore) {
          bestScore = score;
          bestIndex = i;
        }
      }
      const current = open.splice(bestIndex, 1)[0];
      if (current === goal) {
        found = true;
        break;
      }
      closed.add(current);
      const q = point(current);
      for (const [dx, dz] of [
        [-1, 0],
        [1, 0],
        [0, -1],
        [0, 1],
        [-1, -1],
        [-1, 1],
        [1, -1],
        [1, 1],
      ]) {
        const next = current + dx + dz * n;
        if (
          next < 0 ||
          next >= n * n ||
          closed.has(next) ||
          Math.abs((next % n) - (current % n)) > 1
        )
          continue;
        const pos = point(next);
        if (
          !this.canStand(p, pos.x, pos.z) ||
          (dx &&
            dz &&
            (!this.canStand(p, q.x + dx * step, q.z) ||
              !this.canStand(p, q.x, q.z + dz * step)))
        )
          continue;
        const score = scores.get(current) + step * Math.hypot(dx, dz);
        if (score >= (scores.get(next) ?? Infinity)) continue;
        scores.set(next, score);
        came.set(next, current);
        if (!open.includes(next)) open.push(next);
      }
    }
    const path = [];
    if (found) {
      let current = goal;
      while (current !== start && came.has(current)) {
        path.unshift(point(current));
        current = came.get(current);
      }
    }
    if (!path.length) path.push(target);
    p.navigation = {
      goal: { x: target.x, z: target.z },
      path,
      until: this.time + 0.7,
    };
    return path[0];
  }

  clearOut(dt) {
    const c = this.rules.clearOut;
    if (!c.enabled) return;
    const order =
      this.house.clearOutOrders[this.mode] ?? this.house.clearOutOrders.Dibs;
    order.forEach((name, index) => {
      const starts = this.roundStart + c.firstAt + c.interval * index;
      const state = {
        warning: this.time >= starts - c.warn && this.time < starts,
        filling: this.time >= starts && this.time < starts + c.fill,
        closed: this.time >= starts + c.fill,
        damage:
          this.time >= starts
            ? c.damagePerSecond + c.damageGrowth * (this.time - starts)
            : 0,
        progress: Math.min(1, Math.max(0, (this.time - starts) / c.fill)),
      };
      const old = this.roomStatus.get(name);
      if (state.warning && !old?.warning)
        this.emit("warning", {}, { room: name });
      if (state.closed && !old?.closed) this.emit("closed", {}, { room: name });
      this.roomStatus.set(name, state);
    });
    for (const p of this.players) {
      const state = this.roomStatus.get(this.roomAt(p)?.name);
      if (state?.filling || state?.closed)
        this.hit(p, {
          damage: state.damage * dt,
          attacker: -1,
          blockable: false,
        });
    }
  }
  tick(dt, input = {}) {
    if (this.status !== "playing" && this.status !== "preview") return;
    dt = Math.min(dt, 0.05);
    this.time += dt;
    if (this.status === "preview") return;
    const p = this.players[0];
    p.motion = { x: input.x ?? 0, z: input.z ?? 0 };
    if (p.state === "alive") {
      if (input.aim) this.aim(p, input.aim.x, input.aim.z);
      else if (input.x || input.z) {
        p.facing = normalize(input.x, input.z);
        p.yaw = Math.atan2(p.facing.x, p.facing.z);
      }
      p.block = !!input.block && !!this.held(p)?.definition?.shield;
      if (input.attack) this.attack(p);
      if (input.dodge) this.dodge(p);
      if (input.jump) this.jump(p);
      if (input.interact) this.interact(p, dt);
      else p.reviveTarget = null;
      let speed = p.craft || p.action ? 4.5 * this.rules.craftMoveSpeed : 4.5;
      if (p.carried !== null) speed *= 0.7;
      if (p.block) speed *= this.held(p).definition.shield.moveSpeed;
      if (this.time < p.speedUntil) speed *= p.speedStrength;
      if (this.time < p.stunUntil) speed = 0;
      const dir = normalize(input.x ?? 0, input.z ?? 0),
        length = Math.min(1, Math.hypot(input.x ?? 0, input.z ?? 0));
      if (this.time < p.dodgingUntil)
        this.move(
          p,
          ((p.dodgeDirection.x * this.rules.dodgeDistance) /
            this.rules.dodgeSeconds) *
            dt,
          ((p.dodgeDirection.z * this.rules.dodgeDistance) /
            this.rules.dodgeSeconds) *
            dt,
        );
      else
        this.move(p, dir.x * speed * dt * length, dir.z * speed * dt * length);
    }
    for (const q of this.players) {
      if (q.ai) this.ai(q, dt);
      const flight = this.time - (q.jumpStarted ?? this.time);
      q.y =
        this.floorAt(q.x, q.z) +
        (this.time < q.jumpUntil
          ? Math.max(0, q.jumpVelocity * flight - 23.81 * 0.5 * flight * flight)
          : 0);
      if (q.state === "downed" && this.time >= q.bleedAt) this.eliminate(q);
      if (q.state === "eliminated" && this.time >= q.respawnAt) {
        this.respawn(q);
        this.emit("respawn", q);
      }
      if (this.time >= q.bubbleUntil) q.bubble = 0;
      this.completeCraft(q);
      this.completeAction(q);
      const carried = this.item(q.carried);
      if (carried) {
        carried.x = q.x + q.facing.x * 0.65;
        carried.z = q.z + q.facing.z * 0.65;
        carried.y = q.y + 0.5;
      }
      if (q.pendingAttack) {
        const a = q.pendingAttack;
        if (
          !this.canAct(q) ||
          q.action ||
          q.craft ||
          this.held(q)?.id !== a.itemId ||
          q.slot !== a.slot ||
          this.time > a.at + a.stats.active
        ) {
          q.pendingAttack = null;
        } else if (this.time >= a.at) this.resolveAttack(q, a);
      }
      if (q.state === "alive")
        for (const tile of [...this.tiles])
          if (
            distance(q, tile) < 0.8 &&
            (!q.ai ||
              !q.desiredWord ||
              [...q.desiredWord].filter((c) => c === tile.char).length >
                [...q.bag].filter((c) => c === tile.char).length)
          )
            this.collect(q, tile);
    }
    this.tickProjectiles(dt);
    for (const zone of [...this.zones]) {
      const backing = this.item(zone.id);
      if (!backing || !["deployed", "armed"].includes(backing.state)) {
        this.zones.splice(this.zones.indexOf(zone), 1);
        continue;
      }
      if (this.time >= zone.expires) {
        this.zones.splice(this.zones.indexOf(zone), 1);
        const item = this.item(zone.id);
        if (item) item.state = "gone";
        continue;
      }
      for (const q of this.players) {
        if (q.state !== "alive") continue;
        const c = Math.cos(zone.rotation ?? 0),
          s = Math.sin(zone.rotation ?? 0),
          dx = q.x - zone.x,
          dz = q.z - zone.z,
          localX = dx * c - dz * s,
          localZ = dx * s + dz * c;
        const inside = zone.footprint
          ? Math.abs(localX) < zone.footprint[0] / 2 + 0.3 &&
            Math.abs(localZ) < zone.footprint[1] / 2 + 0.3
          : distance(q, zone) <= zone.radius;
        if (!inside) continue;
        if (zone.effect === "SpeedStrip") {
          const motion = q.motion ?? { x: 0, z: 0 };
          if (
            motion.x * Math.sin(zone.rotation) +
              motion.z * Math.cos(zone.rotation) >
            0.1
          ) {
            q.speedUntil = this.time + 1.5;
            q.speedStrength = zone.strength;
          }
        } else if (
          zone.effect === "JumpPad" &&
          this.time >= q.jumpUntil &&
          this.time - (zone.lastLaunch.get(q.id) ?? -Infinity) >= 1
        ) {
          zone.lastLaunch.set(q.id, this.time);
          this.launch(q, zone.strength);
        } else if (zone.effect === "SlipZone") {
          q.slipUntil = this.time + 0.2;
          this.move(q, q.facing.x * dt * 1.1, q.facing.z * dt * 1.1);
        }
      }
    }
    if (this.mode === "MovingOut")
      for (const k of this.keepsakes) {
        const item = this.item(k.itemId);
        if (item) {
          k.x = item.x;
          k.z = item.z;
          if (
            !k.collected &&
            item.state === "world" &&
            distance(item, this.extraction) <= 2.4
          ) {
            k.collected = true;
            item.state = "packed";
            this.emit("keepsake", item, { word: item.word });
          }
        }
      }
    if (this.mode === "MovingDay") {
      this.checkPlacements();
      this.resupply();
    }
    this.clearOut(dt);
    this.checkWin();
  }
  tickProjectiles(dt) {
    for (const pr of [...this.projectiles]) {
      const next = { x: pr.x + pr.vx * dt, z: pr.z + pr.vz * dt };
      const obstacle = this.items.find(
        (i) =>
          i.id !== pr.id &&
          ["world", "deployed"].includes(i.state) &&
          pr.y + 0.2 > (i.y ?? 0) &&
          pr.y - 0.2 < (i.y ?? 0) + (i.definition?.size?.[1] ?? 0.5) &&
          this.segmentHitsItem(pr, next, i),
      );
      if (this.segmentBlocked(pr, next) || obstacle) {
        if (obstacle) this.damageItem(obstacle, pr.stats.breakPower ?? 1);
        pr.vx = pr.vz = 0;
        pr.y = 0;
      } else {
        pr.x = next.x;
        pr.z = next.z;
      }
      const item = pr.item;
      item.x = pr.x;
      item.z = pr.z;
      item.y = pr.y;
      const age = this.time - pr.born;
      if (pr.stats.lob) pr.y = Math.max(0, 0.85 + 3 * age - 5 * age * age);
      else pr.y = Math.max(0, 0.85 - age * 0.9);
      if (
        age > 0.8 ||
        Math.abs(pr.x) > this.extent - 0.5 ||
        Math.abs(pr.z) > this.extent - 0.5
      ) {
        pr.vx = pr.vz = 0;
        pr.y = 0;
        if (pr.fuseAt) item.state = "armed";
      }
      if (pr.fuseAt) {
        if (this.time >= pr.fuseAt) {
          for (const q of this.players) {
            const d = distance(q, pr);
            if (d > pr.stats.radius) continue;
            const direction = normalize(q.x - pr.x, q.z - pr.z),
              damage =
                pr.stats.damage +
                ((pr.stats.edgeDamage - pr.stats.damage) * d) / pr.stats.radius;
            this.hit(q, {
              damage,
              attacker: pr.owner,
              dx: direction.x,
              dz: direction.z,
              knockback: pr.stats.knockback,
              blockable: true,
            });
          }
          for (const i of this.items)
            if (
              i.id !== pr.id &&
              ["world", "deployed"].includes(i.state) &&
              distance(i, pr) <= pr.stats.radius
            )
              this.damageItem(i, pr.stats.breakPower);
          item.state = "gone";
          this.projectiles.splice(this.projectiles.indexOf(pr), 1);
          this.emit("explosion", pr, { radius: pr.stats.radius });
        }
        continue;
      }
      const victim = this.players.find(
        (q) =>
          q.id !== pr.owner && q.state === "alive" && distance(q, pr) < 0.55,
      );
      if (victim) {
        const dir = normalize(pr.vx, pr.vz);
        this.hit(victim, {
          ...pr.stats,
          attacker: pr.owner,
          dx: dir.x,
          dz: dir.z,
          blockable: true,
        });
        pr.vx = pr.vz = 0;
        pr.y = 0;
      }
      if ((pr.vx === 0 && pr.vz === 0) || age > 1.4) {
        item.state = "world";
        item.y = 0;
        this.projectiles.splice(this.projectiles.indexOf(pr), 1);
      }
    }
  }
  respawn(p) {
    p.state = "alive";
    p.hp = p.maxHp;
    p.bubble = 0;
    p.bubbleUntil = 0;
    p.block = false;
    p.stunUntil = 0;
    p.staggerUntil = 0;
    p.lastDodge = -Infinity;
    p.dodgingUntil = 0;
    p.jumpUntil = 0;
    p.y = 0;
    p.reviveTarget = null;
    p.bleedAt = 0;
    p.action = null;
    p.pendingAttack = null;
    p.meleeUntil = 0;
    p.attackAt = 0;
    p.invulnerableUntil = this.time + this.rules.spawnProtection;
    this.emit("respawn", p);
  }
  deliver(word) {
    const center =
        this.house.rooms.find((r) => this.house.neverClose.includes(r.name)) ??
        this.house.rooms[0],
      b = center.bounds;
    const item = this.addItem(
      word,
      (b[0] + b[2]) / 2 + (this.random() - 0.5) * 3,
      (b[1] + b[3]) / 2 + (this.random() - 0.5) * 3,
      "map",
    );
    item.delivery = true;
    this.emit("delivery", item, { word });
    return item;
  }
  resupply() {
    if (this.time < this.nextResupply) return;
    this.nextResupply = this.time + 2;
    let loose =
      this.tiles.map((t) => t.char).join("") +
      this.players.map((p) => p.bag).join("");
    for (const goal of this.objectives.filter((o) => !o.done)) {
      if (
        this.players.some((p) => p.craft?.word === goal.word) ||
        this.items.some(
          (i) => i.word === goal.word && i.state !== "gone" && !i.checklist,
        )
      )
        continue;
      const remaining = takeLetters(loose, goal.word);
      if (remaining === null) this.deliver(goal.word);
      else loose = remaining;
    }
  }
  checkPlacements() {
    for (const item of this.items) {
      if (
        item.origin !== "crafted" ||
        !["world", "deployed"].includes(item.state) ||
        item.checklist
      )
        continue;
      const goal = this.objectives.find(
        (o) =>
          !o.done && o.word === item.word && this.roomAt(item)?.name === o.room,
      );
      if (goal) {
        goal.done = true;
        item.checklist = true;
        item.invulnerable = true;
        item.deployer = null;
        item.state = "deployed";
        this.emit("objective", item, { word: item.word });
      }
    }
  }

  checkWin() {
    if (this.mode === "Tutorial") return;
    const elapsed = this.time - this.roundStart;
    if (this.mode === "MovingDay") {
      if (this.objectives.every((o) => o.done))
        this.finish(true, "Home, sweet home!");
      else if (elapsed >= this.rules.roundTimeLimit)
        this.finish(false, "The truck is leaving. Try a faster route!");
      return;
    }
    if (this.mode === "MovingOut") {
      const team = this.players.filter((p) => p.team === 0),
        alive = team.some((p) => p.state === "alive");
      if (!alive) this.finish(false, "The Movers got your crew.");
      else if (
        this.keepsakes.every((k) => k.collected) &&
        team
          .filter((p) => p.state !== "eliminated")
          .every(
            (p) => p.state === "alive" && distance(p, this.extraction) < 2.4,
          )
      )
        this.finish(true, "All packed. Everybody aboard!");
      else if (elapsed >= this.rules.roundTimeLimit)
        this.finish(false, "The van had to leave.");
      return;
    }
    const teams = [
      ...new Set(
        this.players.filter((p) => p.state === "alive").map((p) => p.team),
      ),
    ];
    if (this.rules.downed) {
      for (const team of [...new Set(this.players.map((p) => p.team))])
        if (!this.players.some((p) => p.team === team && p.state === "alive"))
          this.players
            .filter((p) => p.team === team && p.state === "downed")
            .forEach((p) => this.eliminate(p));
    }
    if (teams.length <= 1 && elapsed > 2) {
      const winner = teams[0] ?? -1;
      if (winner >= 0) this.wins[winner]++;
      this.winner = winner;
      this.result =
        winner === this.players[0].team
          ? "You called dibs!"
          : "One more word. One more chance.";
      this.status =
        this.wins[winner] >= this.rules.roundsToWin ? "finished" : "roundOver";
      this.emit("result", {}, { win: winner === this.players[0].team });
    } else if (
      this.rules.roundTimeLimit > 0 &&
      elapsed >= this.rules.roundTimeLimit
    ) {
      this.winner = -1;
      this.result = "Time’s up. A perfectly messy draw.";
      this.status = "roundOver";
      this.emit("result", {}, { win: false });
    }
  }
  finish(win, result) {
    this.status = "finished";
    this.result = result;
    this.winner = win ? 0 : 1;
    this.emit("result", {}, { win });
  }
  nextRound() {
    if (this.status !== "roundOver") return false;
    this.round++;
    this.status = "playing";
    this.setup();
    return true;
  }
  audit() {
    const tiles = this.tiles.length,
      bags = this.players.reduce((n, p) => n + p.bag.length, 0),
      reserved = this.players.reduce(
        (n, p) => n + (p.craft?.word.length ?? 0),
        0,
      ),
      items = this.items.reduce(
        (n, i) => n + (i.state === "gone" || i.spent ? 0 : i.word.length),
        0,
      );
    return {
      tiles,
      bags,
      reserved,
      items,
      minted: this.minted,
      spent: this.spent,
      balanced: tiles + bags + reserved + items === this.minted - this.spent,
    };
  }
}
