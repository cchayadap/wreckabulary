import * as THREE from "three";

const UPPER = new Set([
  "spine",
  "chest",
  "neck",
  "head",
  "clavicle_L",
  "upper_arm_L",
  "forearm_L",
  "hand_L",
  "grip_L",
  "clavicle_R",
  "upper_arm_R",
  "forearm_R",
  "hand_R",
  "grip_R",
]);

export const SHOTS = {
  swing: { clip: "Swing_OneHand", rate: 1.6 },
  thrust: { clip: "Thrust_OneHand", rate: 1.5 },
  throw: { clip: "Throw_OneHand", rate: 1.5 },
  pickup: { clip: "Pickup", rate: 1.35, full: true },
  place: { clip: "Place", rate: 1.15, full: true },
  drink: { clip: "Drink_Consumable", rate: 1.2 },
  hit: { clip: "Hit_Reaction", rate: 1.15, full: true },
  present: { clip: "Present_Item", rate: 1.25 },
  celebrate: { clip: "Celebrate", rate: 1, full: true },
};

const POSES = [
  "Hold_OneHand",
  "Carry_TwoHand",
  "Block_Plate",
  "Inspect_OneHand",
  "Place",
];

const WALK_FULL = 1.2,
  RUN_FULL = 3.6,
  TURN_SPEED = 12,
  STRETCH = { walk: 1.7, run: 2.0 },
  LEGACY_STRIDE = { walk: 1.5, run: 3.0 },
  HIP_TURN_MAX = 1.05,
  BACKPEDAL_ON = 1.92,
  BACKPEDAL_OFF = 1.57;

const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
const wrap = (a) => Math.atan2(Math.sin(a), Math.cos(a));
const ease = (rate, dt) => 1 - Math.exp(-rate * dt);
const _q = new THREE.Quaternion(),
  _q2 = new THREE.Quaternion(),
  _v = new THREE.Vector3(),
  _axis = new THREE.Vector3();

export class AvatarAnimator {
  constructor(avatar, clips, { locomotion = null, scale = 1 } = {}) {
    this.avatar = avatar;
    const stride = (clip, kind) =>
      locomotion?.[clip]?.stride
        ? locomotion[clip].stride * scale * STRETCH[kind]
        : LEGACY_STRIDE[kind];
    this.walkStride = stride("Walk_InPlace", "walk");
    this.runStride = stride("Run_InPlace", "run");
    this.mixer = new THREE.AnimationMixer(avatar);
    this.clips = new Map(clips.map((c) => [c.name, c]));
    this.actions = new Map();
    for (const clip of clips)
      for (const half of ["U", "L"]) {
        const tracks = clip.tracks.filter(
          (t) => UPPER.has(t.name.split(".")[0]) === (half === "U"),
        );
        const action = this.mixer.clipAction(
          new THREE.AnimationClip(`${clip.name}|${half}`, clip.duration, tracks),
        );
        action.setLoop(THREE.LoopRepeat, Infinity);
        action.timeScale = 0;
        action.setEffectiveWeight(0);
        action.play();
        this.actions.set(`${clip.name}|${half}`, action);
      }
    this.bones = new Map();
    avatar.traverse((o) => {
      if (o.isBone || /^(root|pelvis|spine|chest|neck|head|grip_[LR]|hand_[LR])$/.test(o.name))
        this.bones.set(o.name, o);
    });
    this.baseY = avatar.position.y;
    avatar.rotation.order = "YXZ";
    this.time = 0;
    this.phase = 0;
    this.idleTime = 0;
    this.poseTime = new Map(POSES.map((p) => [p, 0]));
    this.poseWeights = new Map(POSES.map((p) => [p, 0]));
    this.air = 0;
    this.down = 0;
    this.speed = 0;
    this.accel = 0;
    this.yaw = null;
    this.vx = 0;
    this.vz = 0;
    this.heading = null;
    this.pathTurnRate = 0;
    this.hipTurn = 0;
    this.backpedal = false;
    this.runW = 0;
    this.travel = 1;
    this.flinch = 0;
    this.flinchVelocity = 0;
    this.shot = null;
    this.last = null;
    this.current = "Idle";
    this.holdReference = this.sampleReference("Hold_OneHand", "grip_R");
  }

  has(clip) {
    return this.clips.has(clip);
  }

  sampleReference(pose, boneName) {
    const bone = this.bones.get(boneName);
    if (!bone || !this.clips.has(pose) || !this.clips.has("Idle")) return null;
    this.apply(
      new Map([[`${pose}|U`, 1]]),
      new Map([["Idle|L", 1]]),
      new Map([[pose, 0]]),
    );
    this.mixer.update(0);
    this.avatar.updateMatrixWorld(true);
    const avatarQ = this.avatar.getWorldQuaternion(new THREE.Quaternion());
    const gripQ = bone.getWorldQuaternion(new THREE.Quaternion());
    return avatarQ.invert().multiply(gripQ);
  }

  trigger(name) {
    const spec = SHOTS[name];
    if (!spec || !this.clips.has(spec.clip)) return;
    if (this.shot?.spec === SHOTS.celebrate && name !== "celebrate") return;
    this.shot = { spec, time: 0, duration: this.clips.get(spec.clip).duration };
    if (name === "hit") this.flinchVelocity += 7;
  }

  flinchOnly(amount = 4) {
    this.flinchVelocity += amount;
  }

  update(dt, player, game, held) {
    dt = Math.min(dt, 0.1);
    this.time += dt;
    const avatar = this.avatar;

    if (this.yaw === null) this.yaw = player.yaw;
    const limit = this.instantTurn ? Infinity : TURN_SPEED * dt;
    this.yaw = wrap(this.yaw + clamp(wrap(player.yaw - this.yaw), -limit, limit));
    avatar.rotation.y = this.yaw;

    let mx = 0,
      mz = 0;
    if (this.last && dt > 0) {
      mx = (player.x - this.last.x) / dt;
      mz = (player.z - this.last.z) / dt;
      const m = Math.hypot(mx, mz);
      if (m > 9) (mx *= 9 / m), (mz *= 9 / m);
    }
    this.last = { x: player.x, z: player.z };
    this.vx += (mx - this.vx) * ease(12, dt);
    this.vz += (mz - this.vz) * ease(12, dt);
    const before = this.speed;
    this.speed = Math.hypot(this.vx, this.vz);
    this.accel += ((this.speed - before) / Math.max(dt, 1e-3) - this.accel) * ease(8, dt);

    let legTarget = 0;
    if (this.speed > 0.3) {
      const heading = Math.atan2(this.vx, this.vz);
      const rate = this.heading === null || dt <= 0 ? 0 : wrap(heading - this.heading) / dt;
      this.pathTurnRate += (clamp(rate, -8, 8) - this.pathTurnRate) * ease(6, dt);
      this.heading = heading;
      const relative = wrap(heading - this.yaw);
      if (Math.abs(relative) > BACKPEDAL_ON) this.backpedal = true;
      else if (Math.abs(relative) < BACKPEDAL_OFF) this.backpedal = false;
      legTarget = clamp(this.backpedal ? wrap(relative - Math.PI) : relative, -HIP_TURN_MAX, HIP_TURN_MAX);
      this.travel = Math.cos(relative);
    } else {
      this.pathTurnRate -= this.pathTurnRate * ease(6, dt);
      this.heading = null;
      this.backpedal = false;
      this.travel = 1;
    }

    const alive = player.state === "alive";
    const downed = player.state === "downed";
    const dodging = game.time < (player.dodgingUntil ?? 0);
    const s = alive ? this.speed : 0;

    let idleW = clamp(1 - s / WALK_FULL, 0, 1),
      walkW = 0,
      runW = 0;
    if (s <= WALK_FULL) walkW = s / WALK_FULL;
    else if (s < RUN_FULL) {
      runW = (s - WALK_FULL) / (RUN_FULL - WALK_FULL);
      walkW = 1 - runW;
    } else runW = 1;
    const moving = walkW + runW;
    const cycles = moving
      ? (walkW * (s / this.walkStride) + runW * (s / this.runStride)) / moving
      : 0;
    const step = dt * Math.max(cycles, moving ? 0.6 : 0) * (dodging ? 1.3 : 1);
    this.phase = (((this.phase + (this.backpedal ? -step : step)) % 1) + 1) % 1;
    this.hipTurn += (legTarget * Math.min(1, moving) - this.hipTurn) * ease(10, dt);
    this.runW = runW;
    this.idleTime += dt;

    let pose = null;
    if (alive) {
      if (player.reviveTarget !== null && player.reviveTarget !== undefined) pose = "Place";
      else if (player.craft) pose = "Inspect_OneHand";
      else if (player.carried !== null && player.carried !== undefined) pose = "Carry_TwoHand";
      else if (game.isGuardRaised(player)) pose = "Block_Plate";
      else if (held) pose = "Hold_OneHand";
    }
    for (const p of POSES) {
      const target = p === pose ? 1 : 0;
      this.poseWeights.set(p, this.poseWeights.get(p) + (target - this.poseWeights.get(p)) * ease(12, dt));
      const d = this.clips.get(p)?.duration ?? 1;
      this.poseTime.set(p, p === "Place" ? d * 0.45 : (this.poseTime.get(p) + dt) % d);
    }

    const inAir = alive && game.time < (player.jumpUntil ?? 0);
    this.air += ((inAir ? 1 : 0) - this.air) * ease(inAir ? 18 : 8, dt);
    const jumpDur = this.clips.get("Jump_Preview")?.duration ?? 1;
    const progress = inAir
      ? clamp((game.time - (player.jumpStarted ?? game.time)) / (player.jumpDuration || 1), 0, 1)
      : 1;
    const jumpTime = (0.15 + 0.7 * progress) * jumpDur;

    let shotW = 0,
      shotKey = null,
      shotTime = 0;
    if (this.shot) {
      this.shot.time += dt * this.shot.spec.rate;
      const { time, duration } = this.shot;
      if (time >= duration || !(alive || this.shot.spec === SHOTS.hit)) this.shot = null;
      else {
        shotW = Math.min(time / 0.08, 1, (duration - time) / 0.18);
        shotKey = this.shot.spec.clip;
        shotTime = time;
      }
    }

    this.down += ((downed ? 1 : 0) - this.down) * ease(downed ? 5 : 7, dt);

    const upper = new Map(),
      lower = new Map();
    const loco = [
      ["Idle", idleW],
      ["Walk_InPlace", walkW],
      ["Run_InPlace", runW],
    ];
    const layer = (stack, entries) => {
      const total = Math.min(1, entries.reduce((sum, [, w]) => sum + w, 0));
      if (total <= 0) return;
      for (const [k, w] of stack) stack.set(k, w * (1 - total));
      for (const [k, w] of entries) if (w > 0) stack.set(k, (stack.get(k) ?? 0) + w);
    };
    for (const [clip, w] of loco) {
      if (w <= 0) continue;
      upper.set(`${clip}|U`, w);
      lower.set(`${clip}|L`, w);
    }
    layer(
      upper,
      [...this.poseWeights].map(([p, w]) => [`${p}|U`, w]),
    );
    layer(upper, [["Jump_Preview|U", this.air]]);
    layer(lower, [["Jump_Preview|L", this.air]]);
    if (shotKey) {
      layer(upper, [[`${shotKey}|U`, shotW]]);
      if (this.shot?.spec.full) layer(lower, [[`${shotKey}|L`, shotW * (1 - clamp(moving, 0, 1)) * (1 - this.air)]]);
    }
    const hitDur = this.clips.get("Hit_Reaction")?.duration ?? 0.6;
    layer(upper, [["Hit_Reaction|U", this.down]]);
    layer(lower, [["Hit_Reaction|L", this.down]]);

    const times = new Map([
      ["Idle", this.idleTime % (this.clips.get("Idle")?.duration ?? 1)],
      ["Walk_InPlace", this.phase * (this.clips.get("Walk_InPlace")?.duration ?? 1)],
      ["Run_InPlace", this.phase * (this.clips.get("Run_InPlace")?.duration ?? 1)],
      ["Jump_Preview", jumpTime],
      ...this.poseTime,
    ]);
    if (shotKey) times.set(shotKey, shotTime);
    if (this.down > 0.001) times.set("Hit_Reaction", hitDur - 0.02);
    this.apply(upper, lower, times);
    this.mixer.update(0);
    this.current = this.dominant(upper);

    avatar.rotation.x = -this.down * 1.3;
    avatar.position.y = this.baseY + this.down * 0.16;

    this.posture(dt, player, s, idleW, dodging);
  }

  apply(upper, lower, times) {
    for (const [key, action] of this.actions) {
      const w = (key.endsWith("|U") ? upper : lower).get(key) ?? 0;
      action.setEffectiveWeight(w);
      if (w > 0) {
        const clip = key.slice(0, -2);
        if (times.has(clip)) action.time = Math.min(times.get(clip), action.getClip().duration - 1e-4);
      }
    }
  }

  dominant(stack) {
    let best = "Idle",
      weight = -1;
    for (const [k, w] of stack)
      if (w > weight) {
        best = k.slice(0, -2);
        weight = w;
      }
    return best;
  }

  rotateWorld(name, axis, angle) {
    const bone = this.bones.get(name);
    if (!bone || !angle) return;
    bone.parent.getWorldQuaternion(_q);
    _axis.copy(axis).applyQuaternion(_q.invert()).normalize();
    bone.quaternion.premultiply(_q2.setFromAxisAngle(_axis, angle));
    bone.updateMatrixWorld(true);
  }

  posture(dt, player, speed, idleW, dodging) {
    this.flinchVelocity += (-120 * this.flinch - 14 * this.flinchVelocity) * dt;
    this.flinch += this.flinchVelocity * dt;
    if (this.down > 0.98) return;
    const live = 1 - this.down;
    this.avatar.updateMatrixWorld(true);
    const bodyQ = this.avatar.getWorldQuaternion(_q2.clone());
    const side = new THREE.Vector3(1, 0, 0).applyQuaternion(bodyQ);
    const forward = new THREE.Vector3(0, 0, 1).applyQuaternion(bodyQ);
    const up = new THREE.Vector3(0, 1, 0);

    const lean =
      clamp((0.012 * speed + 0.03 * this.accel) * this.travel, -0.1, 0.15) +
      (this.backpedal ? -0.12 * this.runW : 0) +
      (dodging ? 0.22 : 0);
    const bank = clamp(-this.pathTurnRate * Math.min(speed, 4.5) * 0.02, -0.12, 0.12);
    const breathe = Math.sin(this.time * 2.1) * 0.012 * idleW;
    const twist = clamp(wrap(player.yaw - this.yaw), -0.8, 0.8);
    const flinch = clamp(this.flinch, -0.6, 0.6);
    const legs = live * this.hipTurn;

    this.rotateWorld("pelvis", up, legs);
    this.rotateWorld("spine", up, -legs * 0.5);
    this.rotateWorld("chest", up, -legs * 0.35);
    this.rotateWorld("head", up, -legs * 0.15);
    this.rotateWorld("spine", side, live * (lean * 0.6 - flinch * 0.15));
    this.rotateWorld("chest", side, live * (lean * 0.4 + breathe - flinch * 0.3));
    this.rotateWorld("head", side, live * (-lean * 0.35 - flinch * 0.2));
    this.rotateWorld("spine", forward, live * bank * 0.6);
    this.rotateWorld("chest", forward, live * bank * 0.4);
    this.rotateWorld("chest", up, live * twist * 0.5);
    this.rotateWorld("head", up, live * twist * 0.4);
  }

  holdTransform(item, gripLocal, scale, base) {
    const grip = this.bones.get("grip_R");
    if (!grip || !this.holdReference) return false;
    grip.getWorldQuaternion(_q);
    item.quaternion.copy(_q).multiply(_q2.copy(this.holdReference).invert()).multiply(base);
    grip.getWorldPosition(_v);
    const offset = new THREE.Vector3(...(gripLocal ?? [0, 0, 0]))
      .multiplyScalar(scale)
      .applyQuaternion(item.quaternion);
    item.position.copy(_v).sub(offset);
    return true;
  }

  carryPoint(target) {
    const l = this.bones.get("grip_L"),
      r = this.bones.get("grip_R");
    if (!l || !r) return false;
    l.getWorldPosition(target);
    r.getWorldPosition(_v);
    target.add(_v).multiplyScalar(0.5);
    return true;
  }

  dispose() {
    this.mixer.stopAllAction();
    this.mixer.uncacheRoot(this.avatar);
  }
}
