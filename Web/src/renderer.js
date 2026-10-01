import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { RoundedBoxGeometry } from "three/addons/geometries/RoundedBoxGeometry.js";
import { clone as cloneSkeleton } from "three/addons/utils/SkeletonUtils.js";
const ROOM_COLORS = {
  Garden: 0x92b78b,
  Playroom: 0xe1b970,
  Bedroom: 0xc8b3bd,
  Kitchen: 0xb8c9be,
  Study: 0xa8b7c8,
  LivingRoom: 0xd4b397,
};
const material = (color, roughness = 0.8) =>
  new THREE.MeshStandardMaterial({ color, roughness });
const mesh = (geometry, mat) => {
  const m = new THREE.Mesh(geometry, mat);
  m.castShadow = true;
  m.receiveShadow = true;
  return m;
};
export class WorldView {
  constructor(canvas, data) {
    this.canvas = canvas;
    this.data = data;
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x173a3d);
    this.scene.fog = new THREE.Fog(0x173a3d, 48, 100);
    this.renderer = new THREE.WebGLRenderer({
      canvas,
      antialias: true,
      alpha: false,
      powerPreference: "high-performance",
    });
    this.renderer.setPixelRatio(
      Math.min(
        devicePixelRatio,
        matchMedia("(pointer: coarse)").matches ? 1.25 : 1.5,
      ),
    );
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.3;
    this.camera = new THREE.PerspectiveCamera(43, 1, 0.1, 120);
    this.camera.position.set(16, 22, 23);
    this.target = new THREE.Vector3();
    this.camera.lookAt(this.target);
    this.raycaster = new THREE.Raycaster();
    this.plane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
    this.models = new Map();
    this.entities = new Map();
    this.avatarModels = new Map();
    this.effects = [];
    this.icons = new Map();
    this.loader = new GLTFLoader();
    this.world = new THREE.Group();
    this.dynamic = new THREE.Group();
    this.scene.add(this.world, this.dynamic);
    this.scene.add(new THREE.HemisphereLight(0xffefd3, 0x465e60, 2));
    const sun = new THREE.DirectionalLight(0xffe6c3, 3.4);
    sun.position.set(-8, 19, 8);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    Object.assign(sun.shadow.camera, {
      left: -25,
      right: 25,
      top: 25,
      bottom: -25,
      near: 0.1,
      far: 70,
    });
    sun.shadow.bias = -0.0008;
    sun.shadow.normalBias = 0.025;
    this.scene.add(sun);
    const fill = new THREE.DirectionalLight(0xb9e9ff, 1.1);
    fill.position.set(8, 6, -9);
    this.scene.add(fill);
    this.resize();
  }
  async init(progress = () => {}) {
    const url = (path) => new URL(path, document.baseURI).href;
    this.manifest = await fetch(url("art/manifest.json")).then((r) => {
      if (!r.ok)
        throw new Error(
          "The 3D art pack is missing. Run the documented asset export.",
        );
      return r.json();
    });
    for (const [id, a] of Object.entries(this.manifest.items ?? {}))
      if (a.icon) this.icons.set(id, url(a.icon));
    const neededItems = new Set([
      ...this.data.items.items.filter((i) => i.enabled).map((i) => i.id),
      ...Object.values(
        this.data.houses ?? { pinwheel: this.data.house },
      ).flatMap((h) => h.furniture.map((f) => f.word)),
      "PLANT",
      "CHEST",
    ]);
    const entries = [
      ...Object.entries(this.manifest.items ?? {})
        .filter(([id]) => neededItems.has(id))
        .map(([id, a]) => [id, a]),
      ...Object.entries(this.manifest.letters ?? {}).map(([id, a]) => [
        `letter:${id}`,
        a,
      ]),
      ...Object.entries(this.manifest.environment ?? {})
        .filter(([id]) =>
          [
            "Doorstep",
            "Kitchen_Counter",
            "Kitchen_Fridge",
            "Kitchen_Sink",
            "Kitchen_Stove",
            "Wall_Shelf",
            "Wall_Window",
            "Wall_Sconce",
          ].includes(id),
        )
        .map(([id, a]) => [`env:${id}`, a]),
      [
        "avatar",
        {
          ...this.manifest.avatar,
          path:
            matchMedia("(pointer: coarse)").matches &&
            this.manifest.avatar.mobilePath
              ? this.manifest.avatar.mobilePath
              : this.manifest.avatar.path,
        },
      ],
    ];
    let done = 0;
    const worker = async () => {
      while (entries.length) {
        const [id, a] = entries.shift();
        if (!a?.path) continue;
        const gltf = await this.loader.loadAsync(url(a.path));
        gltf.scene.traverse((o) => {
          if (o.isMesh) {
            o.castShadow = true;
            o.receiveShadow = true;
            if (o.material) {
              for (const m of Array.isArray(o.material)
                ? o.material
                : [o.material]) {
                m.roughness = Math.max(0.3, m.roughness);
              }
            }
          }
        });
        this.models.set(id, gltf);
        progress(++done, 40);
      }
    };
    await Promise.all([worker(), worker(), worker()]);
    this.ready = true;
  }
  clone(id) {
    const source = this.models.get(id);
    return source ? cloneSkeleton(source.scene) : null;
  }
  box(w, h, d, color) {
    return mesh(new THREE.BoxGeometry(w, h, d), material(color));
  }
  label(text, color = "#173a3d", background = "#faeed6") {
    const canvas = document.createElement("canvas");
    canvas.width = 512;
    canvas.height = 128;
    const ctx = canvas.getContext("2d");
    ctx.fillStyle = background;
    ctx.beginPath();
    ctx.roundRect(3, 3, 506, 122, 28);
    ctx.fill();
    ctx.font = "bold 50px sans-serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillStyle = color;
    ctx.fillText(text, 256, 65);
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    const sprite = new THREE.Sprite(
      new THREE.SpriteMaterial({ map: texture, depthTest: false }),
    );
    sprite.scale.set(2.3, 0.57, 1);
    return sprite;
  }
  floorTexture(mapId) {
    const c = document.createElement("canvas");
    c.width = c.height = 512;
    const ctx = c.getContext("2d");
    ctx.fillStyle = mapId === "courtyard" ? "#789c76" : "#bd9161";
    ctx.fillRect(0, 0, 512, 512);
    for (let y = 0; y < 512; y += 64) {
      ctx.fillStyle =
        mapId === "courtyard"
          ? y % 128
            ? "#789b76"
            : "#7da17b"
          : y % 128
            ? "#c29563"
            : "#b98b59";
      ctx.fillRect(0, y, 512, 62);
      ctx.strokeStyle = mapId === "courtyard" ? "#82a57c" : "#956c45";
      ctx.lineWidth = 2;
      ctx.strokeRect(0, y, 512, 64);
      for (let x = y % 128 ? 64 : 0; x < 512; x += 128) {
        ctx.beginPath();
        ctx.moveTo(x, y);
        ctx.lineTo(x, y + 64);
        ctx.stroke();
      }
      if (mapId !== "courtyard") {
        ctx.globalAlpha = 0.13;
        for (let n = 0; n < 12; n++) {
          ctx.beginPath();
          ctx.moveTo(0, y + n * 5);
          ctx.bezierCurveTo(
            160,
            y + n * 5 + 5,
            320,
            y + n * 5 - 4,
            512,
            y + n * 5,
          );
          ctx.stroke();
        }
        ctx.globalAlpha = 1;
      }
    }
    const tex = new THREE.CanvasTexture(c);
    tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
    tex.repeat.set(7, 7);
    tex.colorSpace = THREE.SRGBColorSpace;
    tex.anisotropy = Math.min(8, this.renderer.capabilities.getMaxAnisotropy());
    return tex;
  }
  rebuild(game) {
    this.game = game;
    this.clear(this.world);
    this.clear(this.dynamic);
    this.entities.clear();
    this.avatarModels.clear();
    this.effects = [];
    const s = game.map.scale;
    const foundation = this.box(
      2 * game.extent + 2,
      0.6,
      2 * game.extent + 2,
      0xc4aa86,
    );
    foundation.position.y = -0.5;
    this.world.add(foundation);
    const lawn = this.box(80, 0.4, 80, 0x577960);
    lawn.position.y = -0.85;
    this.world.add(lawn);
    const floorMat = material(0xffffff);
    floorMat.map = this.floorTexture("pinwheel");
    for (const room of game.house.rooms) {
      const b = room.bounds,
        w = b[2] - b[0],
        d = b[3] - b[1];
      const floor = mesh(new THREE.BoxGeometry(w, 0.15, d), floorMat);
      floor.position.set((b[0] + b[2]) / 2, -0.12, (b[1] + b[3]) / 2);
      if (room.name === "Garden") {
        floor.material = material(0xa6bf91);
        floor.material.map = this.floorTexture("courtyard");
      } else if (room.name === "Kitchen") {
        const c = document.createElement("canvas");
        c.width = c.height = 128;
        const ctx = c.getContext("2d");
        for (let y = 0; y < 8; y++)
          for (let x = 0; x < 8; x++) {
            ctx.fillStyle = (x + y) % 2 ? "#dce1cf" : "#acc5b6";
            ctx.fillRect(x * 16, y * 16, 15, 15);
          }
        const tile = new THREE.CanvasTexture(c);
        tile.colorSpace = THREE.SRGBColorSpace;
        tile.wrapS = tile.wrapT = THREE.RepeatWrapping;
        tile.repeat.set(3, 3);
        floor.material = material(0xffffff);
        floor.material.map = tile;
      } else {
        floor.material = floorMat.clone();
        floor.material.color.setHex(
          room.name === "Study"
            ? 0xd1bd9e
            : room.name === "Bedroom"
              ? 0xffe8d3
              : 0xffedcc,
        );
      }
      this.world.add(floor);
      const rug = mesh(
        new THREE.BoxGeometry(
          Math.min(w - 1, w * 0.62),
          0.045,
          Math.min(d - 1, d * 0.55),
        ),
        material(ROOM_COLORS[room.name] ?? 0xe4c695),
      );
      rug.position.set(floor.position.x, 0.006, floor.position.z);
      this.world.add(rug);
      const edges = new THREE.LineSegments(
        new THREE.EdgesGeometry(rug.geometry),
        new THREE.LineBasicMaterial({ color: 0xffead4 }),
      );
      edges.position.copy(rug.position).y += 0.025;
      this.world.add(edges);
      const text = this.label(
        room.name.replace(/([a-z])([A-Z])/g, "$1 $2").toUpperCase(),
        "#37575b",
        "#f3e6cf",
      );
      text.position.set(floor.position.x, 0.4, floor.position.z - d * 0.35);
      text.material.opacity = 0.8;
      this.world.add(text);
    }
    for (const wall of game.walls) {
      const horizontal = wall.z1 === wall.z2,
        length = horizontal ? wall.x2 - wall.x1 : wall.z2 - wall.z1;
      const exterior =
        Math.abs(horizontal ? wall.z1 : wall.x1) >= game.extent - 0.01;
      const height = exterior ? 0.8 : 1.2;
      const m = this.box(
        horizontal ? length : 0.18,
        height,
        horizontal ? 0.18 : length,
        0xeddfc4,
      );
      m.position.set(
        (wall.x1 + wall.x2) / 2,
        height / 2,
        (wall.z1 + wall.z2) / 2,
      );
      this.world.add(m);
      const trim = this.box(
        horizontal ? length : 0.22,
        0.085,
        horizontal ? 0.22 : length,
        0xb58760,
      );
      trim.position.set(m.position.x, height + 0.04, m.position.z);
      this.world.add(trim);
    }
    for (const door of game.house.doors) {
      const m = this.clone("env:Doorstep");
      if (m) {
        m.position.set(door.at[0], 0, door.at[1]);
        m.rotation.y = door.between.includes("Kitchen") ? Math.PI / 2 : 0;
        this.world.add(m);
      }
      const mat = mesh(
        new THREE.BoxGeometry(door.width, 0.045, 0.75),
        material(0xd5aa6e),
      );
      mat.position.set(door.at[0], 0.014, door.at[1]);
      this.world.add(mat);
    }
    if (game.map.id === "pinwheel") {
      const balcony = this.box(1.5, 0.15, 3, 0xb18b60);
      balcony.position.set(3.25, 1.625, 2.5);
      this.world.add(balcony);
      for (let n = 0; n < 7; n++) {
        const h = ((n + 1) / 7) * 1.7,
          step = this.box(1.5, h, 2 / 7, 0xc09b70);
        step.position.set(3.25, h / 2, -1 + ((n + 0.5) * 2) / 7);
        this.world.add(step);
      }
      for (const z of [1.3, 2, 2.7, 3.4, 4]) {
        const rail = this.box(0.06, 0.55, 0.06, 0x7a644c);
        rail.position.set(2.5, 1.97, z);
        this.world.add(rail);
      }
      const top = this.box(0.1, 0.08, 3, 0x8b7054);
      top.position.set(2.5, 2.27, 2.5);
      this.world.add(top);
    }
    const kitchen = game.house.rooms.find((r) => r.name === "Kitchen");
    if (kitchen) {
      const b = kitchen.bounds;
      [
        "Kitchen_Fridge",
        "Kitchen_Counter",
        "Kitchen_Sink",
        "Kitchen_Stove",
      ].forEach((id, n) => {
        const appliance = this.clone(`env:${id}`);
        if (appliance) {
          appliance.position.set(b[2] - 0.5, 0, b[1] + 1 + n * 1.7);
          appliance.rotation.y = -Math.PI / 2;
          this.world.add(appliance);
        }
      });
    }
    // Real foliage models, picket details and warm lamp pools make the house feel inhabited.
    for (let n = 0; n < 16; n++) {
      const angle = (n / 16) * Math.PI * 2,
        radius = game.extent + 4 + (n % 3) * 1.5;
      const plant = this.clone("PLANT");
      if (plant) {
        plant.position.set(
          Math.cos(angle) * radius,
          0,
          Math.sin(angle) * radius,
        );
        plant.scale.setScalar(1.8 + (n % 3) * 0.35);
        this.world.add(plant);
      }
    }
    if (game.mode === "MovingOut") {
      const van = new THREE.Group();
      const body = mesh(
        new RoundedBoxGeometry(1.4, 0.85, 2.3, 3, 0.12),
        material(0x5c9f9b),
      );
      body.position.y = 0.8;
      van.add(body);
      const cab = mesh(
        new RoundedBoxGeometry(1.4, 0.9, 0.9, 3, 0.15),
        material(0xf1dfb6),
      );
      cab.position.set(0, 1.15, -0.85);
      van.add(cab);
      const window = this.box(1.16, 0.42, 0.035, 0x315a64);
      window.position.set(0, 1.3, -1.32);
      van.add(window);
      for (const side of [-1, 1]) {
        for (const z of [-0.78, 0.8]) {
          const tire = mesh(
            new THREE.CylinderGeometry(0.25, 0.25, 0.18, 24),
            material(0x303938),
          );
          tire.rotation.z = Math.PI / 2;
          tire.position.set(side * 0.73, 0.35, z);
          van.add(tire);
          const hub = mesh(
            new THREE.CylinderGeometry(0.13, 0.13, 0.19, 16),
            material(0xc8bc9a, 0.3),
          );
          hub.rotation.z = Math.PI / 2;
          hub.position.copy(tire.position);
          van.add(hub);
        }
        const sideWindow = this.box(0.03, 0.38, 0.55, 0x315a64);
        sideWindow.position.set(side * 0.71, 1.3, -0.86);
        van.add(sideWindow);
        const headlight = this.box(0.28, 0.18, 0.04, 0xffe9a9);
        headlight.position.set(side * 0.45, 0.65, -1.34);
        van.add(headlight);
      }
      const bumper = this.box(1.48, 0.16, 0.18, 0xb0a992);
      bumper.position.set(0, 0.45, -1.4);
      van.add(bumper);
      const crate = this.clone("CRATE");
      if (crate) {
        crate.position.set(0, 1.25, 0.25);
        crate.scale.setScalar(0.65);
        van.add(crate);
      }
      van.position.set(game.extraction.x, 0, game.extraction.z);
      this.world.add(van);
      const label = this.label("THE GETAWAY");
      label.position.set(van.position.x, 2, van.position.z);
      this.world.add(label);
    }

    for (const objective of game.objectives) {
      const marker = mesh(
        new THREE.RingGeometry(1.1, 1.3, 48),
        new THREE.MeshBasicMaterial({
          color: 0xffdc70,
          side: THREE.DoubleSide,
          transparent: true,
          opacity: 0.75,
        }),
      );
      marker.rotation.x = -Math.PI / 2;
      marker.position.set(objective.x, 0.08, objective.z);
      this.world.add(marker);
      const text = this.label(objective.word);
      text.position.set(objective.x, 0.6, objective.z);
      this.world.add(text);
      objective.marker = marker;
      objective.label = text;
    }
    for (const p of game.players) {
      const group = new THREE.Group(),
        avatar = this.clone("avatar");
      if (avatar) {
        const box = new THREE.Box3().setFromObject(avatar),
          height = box.getSize(new THREE.Vector3()).y;
        avatar.scale.setScalar(1.42 / height);
        avatar.position.y = -box.min.y * avatar.scale.x;
        group.add(avatar);
        this.applyWardrobe(avatar, p.wardrobe);
        const source = this.models.get("avatar");
        const mixer = new THREE.AnimationMixer(avatar);
        const clips = source.animations ?? [];
        const actions = new Map(
          clips.map((clip) => [clip.name, mixer.clipAction(clip)]),
        );
        this.avatarModels.set(p.id, { avatar, mixer, actions, current: null });
      } else throw new Error("Avatar asset unavailable.");
      const ring = mesh(
        new THREE.RingGeometry(0.33, 0.4, 36),
        new THREE.MeshBasicMaterial({
          color: p.id === 0 ? 0x8ee2ce : 0xffd078,
          side: THREE.DoubleSide,
        }),
      );
      ring.rotation.x = -Math.PI / 2;
      ring.position.y = 0.025;
      group.add(ring);
      const shadow = mesh(
        new THREE.CircleGeometry(0.33, 24),
        new THREE.MeshBasicMaterial({
          color: 0x102d2e,
          transparent: true,
          opacity: 0.15,
        }),
      );
      shadow.rotation.x = -Math.PI / 2;
      shadow.position.y = 0.01;
      group.add(shadow);
      const name = this.label(
        p.name,
        p.id === 0 ? "#153e3c" : "#77512d",
        p.id === 0 ? "#b4efd7" : "#ffe2a4",
      );
      name.position.y = 1.8;
      name.scale.set(1.1, 0.28, 1);
      group.add(name);
      const bubble = mesh(
        new THREE.SphereGeometry(0.78, 24, 16),
        new THREE.MeshPhysicalMaterial({
          color: 0x8bdcca,
          transparent: true,
          opacity: 0.24,
          roughness: 0.15,
          metalness: 0.1,
          side: THREE.DoubleSide,
          depthWrite: false,
        }),
      );
      bubble.position.y = 0.8;
      bubble.visible = false;
      group.add(bubble);
      group.userData = { name, bubble, avatar, ring };
      this.dynamic.add(group);
      this.entities.set(`p${p.id}`, group);
    }
  }
  applyWardrobe(avatar, wardrobe) {
    const selected = new Set(Object.values(wardrobe.pieces ?? {}));
    const config = this.data.wardrobe;
    avatar.traverse((o) => {
      if (!o.isMesh) return;
      const piece = config.pieces.find(
        (p) => o.name === p.mesh || o.name.startsWith(`${p.mesh}_`),
      );
      if (piece) o.visible = selected.has(piece.id);
      const mats = Array.isArray(o.material) ? o.material : [o.material];
      o.material = mats.map((m) => {
        const copy = m.clone();
        for (const [slot, palette] of Object.entries(config.palettes)) {
          const chosen = palette.find((c) => c.id === wardrobe.colours?.[slot]);
          const def = config.pieces.find(
            (p) => p.slot === slot && selected.has(p.id),
          );
          if (
            chosen &&
            def?.tint &&
            m.name.toLowerCase().includes(def.tint.toLowerCase())
          )
            copy.color.setRGB(...chosen.rgb, THREE.SRGBColorSpace);
        }
        return copy;
      });
      if (o.material.length === 1) o.material = o.material[0];
    });
  }
  clear(group) {
    for (const child of [...group.children]) group.remove(child);
  }
  itemModel(item) {
    const model = this.clone(item.delivery ? "BOX" : item.word);
    if (!model) return null;
    const group = new THREE.Group();
    group.add(model);
    model.traverse((o) => {
      if (o.isMesh)
        o.material = Array.isArray(o.material)
          ? o.material.map((m) => m.clone())
          : o.material.clone();
    });
    group.userData = { model, word: item.word };
    this.dynamic.add(group);
    return group;
  }
  applySkin(node, skin) {
    if (node.userData.skin === skin) return;
    node.userData.skin = skin;
    node.userData.model.traverse((o) => {
      if (!o.isMesh) return;
      for (const m of Array.isArray(o.material) ? o.material : [o.material]) {
        const key = m.name.replace(/_(Classic|Candy|Arcade)$/, "");
        const info =
          this.manifest.materialSkins?.[key]?.[skin] ??
          this.manifest.materialSkins?.[key]?.Classic;
        if (!info) continue;
        m.color.setRGB(...info.baseColor.slice(0, 3), THREE.SRGBColorSpace);
        m.roughness = info.roughness;
        m.metalness = info.metallic;
        m.emissive.setRGB(...info.emissive);
      }
    });
  }
  update(game, dt) {
    if (!this.ready) return;
    const p = game.players[0],
      preview = game.status === "preview";
    const desired = this.closet
      ? new THREE.Vector3(p.x + 3.5, 2.4, p.z + 4.4)
      : preview
        ? new THREE.Vector3(
            13 * game.map.scale,
            20 * game.map.scale,
            20 * game.map.scale,
          )
        : new THREE.Vector3(p.x + 10, p.y + 16, p.z + 13);
    const target = this.closet
      ? new THREE.Vector3(p.x + 0.6, 0.8, p.z)
      : preview
        ? new THREE.Vector3(0, 0, 0)
        : new THREE.Vector3(p.x, 0, p.z - 0.8);
    this.camera.position.lerp(desired, 1 - Math.exp(-dt * 3));
    this.target.lerp(target, 1 - Math.exp(-dt * 5));
    this.camera.lookAt(this.target);
    for (const player of game.players) {
      const group = this.entities.get(`p${player.id}`);
      if (!group) continue;
      group.position.set(player.x, player.y, player.z);
      const avatar = group.userData.avatar;
      avatar.rotation.y = player.yaw;
      group.userData.bubble.visible = player.bubble > 0;
      group.userData.ring.visible = player.state === "alive";
      group.userData.name.material.opacity =
        player.state === "eliminated" ? 0.3 : 1;
      avatar.visible = player.state !== "eliminated";
      if (player.state === "downed") avatar.rotation.z = 0.95;
      else avatar.rotation.z = 0;
      const anim = this.avatarModels.get(player.id);
      if (anim) {
        const moving =
          group.userData.last &&
          Math.hypot(
            player.x - group.userData.last.x,
            player.z - group.userData.last.z,
          ) > 0.002;
        const pattern =
          player.state === "downed"
            ? /down|ko|fall/i
            : game.time < player.attackingUntil
              ? /attack|punch|swing/i
              : game.time < player.jumpUntil
                ? /jump/i
                : moving
                  ? /run|walk|move/i
                  : /idle/i;
        const name =
          [...anim.actions.keys()].find((n) => pattern.test(n)) ??
          [...anim.actions.keys()][0];
        if (name && anim.current !== name) {
          anim.actions.get(anim.current)?.fadeOut(0.12);
          anim.actions.get(name).reset().fadeIn(0.12).play();
          anim.current = name;
        }
        anim.mixer.update(dt);
      }
      group.userData.last = { x: player.x, z: player.z };
    }
    const active = new Set();
    for (const item of game.items) {
      if (item.state === "gone" || item.state === "packed") continue;
      const key = `i${item.id}`;
      active.add(key);
      let node = this.entities.get(key);
      if (!node) {
        node = this.itemModel(item);
        if (!node) continue;
        this.entities.set(key, node);
      }
      node.visible = true;
      this.applySkin(
        node,
        item.state === "held"
          ? item.owner === 0
            ? game.skin
            : "Classic"
          : item.state === "thrown"
            ? (item.skin ?? "Classic")
            : "Classic",
      );
      if (item.state === "held" || item.state === "carried") {
        const owner = game.players[item.owner];
        if (!owner) {
          node.visible = false;
          continue;
        }
        const a = owner.yaw,
          nodeScale =
            item.state === "carried" ? 1 : (item.definition?.heldScale ?? 0.5);
        node.scale.setScalar(nodeScale);
        node.position.set(
          owner.x +
            (item.state === "carried"
              ? Math.sin(a) * 0.8
              : Math.cos(a) * 0.45 + Math.sin(a) * 0.18),
          owner.y + (item.state === "carried" ? 0.55 : 0.8),
          owner.z +
            (item.state === "carried"
              ? Math.cos(a) * 0.8
              : -Math.sin(a) * 0.45 + Math.cos(a) * 0.18),
        );
        node.rotation.set(0, a, game.time < owner.attackingUntil ? -0.8 : 0);
      } else {
        node.scale.setScalar(1);
        node.position.set(item.x, item.y ?? 0, item.z);
        node.rotation.set(0, item.rotation ?? 0, 0);
      }
      if (item.origin === "map") {
        const flash = Math.max(0, item.flashUntil - game.time);
        node.userData.model.traverse((m) => {
          if (m.isMesh && m.material?.emissive)
            m.material.emissive.setHex(flash > 0 ? 0x6a3022 : 0);
        });
      }
    }
    for (const tile of game.tiles) {
      const key = `t${tile.id}`;
      active.add(key);
      let node = this.entities.get(key);
      if (!node) {
        node = this.clone(`letter:${tile.char}`) ?? new THREE.Group();
        node.scale.setScalar(0.85);
        this.dynamic.add(node);
        this.entities.set(key, node);
      }
      node.position.set(
        tile.x,
        0.13 + Math.sin(game.time * 3 + tile.id) * 0.035,
        tile.z,
      );
      node.rotation.y = Math.sin(tile.id) * 0.2;
    }
    for (const [key, node] of this.entities)
      if (/^[it]\d/.test(key) && !active.has(key)) {
        this.dynamic.remove(node);
        this.entities.delete(key);
      }
    for (const k of game.keepsakes) {
      const key = `k${k.id}`;
      let node = this.entities.get(key);
      if (!node && !k.collected) {
        node = this.label(`SAVE ${k.word}`, "#6f4e2d", "#ffdf9d");
        node.scale.set(1.15, 0.3, 1);
        this.dynamic.add(node);
        this.entities.set(key, node);
      }
      if (node) {
        node.visible = !k.collected;
        node.position.set(k.x, 1.5 + Math.sin(game.time * 3) * 0.04, k.z);
      }
    }

    for (const room of game.house.rooms) {
      const state = game.roomStatus.get(room.name),
        key = `hazard${room.name}`;
      let node = this.entities.get(key);
      if ((state?.warning || state?.filling || state?.closed) && !node) {
        const b = room.bounds;
        node = mesh(
          new THREE.PlaneGeometry(b[2] - b[0] - 0.1, b[3] - b[1] - 0.1),
          new THREE.MeshBasicMaterial({
            color: 0xe96244,
            transparent: true,
            opacity: 0.2,
            side: THREE.DoubleSide,
            depthWrite: false,
          }),
        );
        node.rotation.x = -Math.PI / 2;
        node.position.set((b[0] + b[2]) / 2, 0.04, (b[1] + b[3]) / 2);
        this.dynamic.add(node);
        this.entities.set(key, node);
      }
      if (node)
        node.material.opacity =
          state?.closed || state?.filling
            ? 0.22
            : 0.1 + 0.1 * Math.sin(game.time * 5);
    }
    const bombs = new Set(
      game.projectiles.filter((pr) => pr.fuseAt).map((pr) => `bomb${pr.id}`),
    );
    for (const [key, node] of this.entities)
      if (key.startsWith("bomb") && !bombs.has(key)) {
        this.dynamic.remove(node);
        this.entities.delete(key);
      }
    const zones = new Set(game.zones.map((z) => `zone${z.id}`));
    for (const [key, node] of this.entities)
      if (key.startsWith("zone") && !zones.has(key)) {
        this.dynamic.remove(node);
        this.entities.delete(key);
      }
    for (const zone of game.zones) {
      const key = `zone${zone.id}`;
      let node = this.entities.get(key);
      if (!node) {
        node = mesh(
          new THREE.CircleGeometry(zone.radius, 48),
          new THREE.MeshBasicMaterial({
            color:
              zone.effect === "SlipZone"
                ? 0x79cbd5
                : zone.effect === "JumpPad"
                  ? 0xe7b877
                  : 0xa6d8b9,
            transparent: true,
            opacity: 0.32,
            side: THREE.DoubleSide,
            depthWrite: false,
          }),
        );
        node.rotation.x = -Math.PI / 2;
        node.position.set(zone.x, 0.045, zone.z);
        this.dynamic.add(node);
        this.entities.set(key, node);
      }
    }
    for (const pr of game.projectiles.filter((pr) => pr.fuseAt)) {
      const key = `bomb${pr.id}`;
      let node = this.entities.get(key);
      if (!node) {
        node = mesh(
          new THREE.RingGeometry(pr.stats.radius - 0.05, pr.stats.radius, 48),
          new THREE.MeshBasicMaterial({
            color: 0xff7158,
            transparent: true,
            opacity: 0.7,
            side: THREE.DoubleSide,
            depthWrite: false,
          }),
        );
        node.rotation.x = -Math.PI / 2;
        this.dynamic.add(node);
        this.entities.set(key, node);
      }
      node.position.set(pr.x, 0.07, pr.z);
      node.material.opacity = 0.4 + 0.4 * Math.sin(game.time * 12);
    }
    for (const fx of [...this.effects]) {
      fx.life -= dt;
      if (fx.life <= 0) {
        this.dynamic.remove(fx.node);
        this.effects.splice(this.effects.indexOf(fx), 1);
        continue;
      }
      fx.node.position.addScaledVector(fx.velocity, dt);
      fx.velocity.y -= 4 * dt;
      fx.node.material.opacity = fx.life / fx.max;
      fx.node.scale.multiplyScalar(1 - dt * 0.5);
    }
    for (const objective of game.objectives) {
      if (objective.marker)
        objective.marker.material.color.setHex(
          objective.done ? 0x8ce2b4 : 0xffda73,
        );
      if (objective.label) objective.label.visible = !objective.done;
    }
    this.renderer.render(this.scene, this.camera);
  }
  handleEvents(events) {
    for (const e of events) {
      if (
        [
          "hit",
          "break",
          "explosion",
          "craft",
          "pickup",
          "buff",
          "dodge",
          "objective",
          "keepsake",
        ].includes(e.type)
      ) {
        const colors = {
          hit: 0xff9b83,
          break: 0xd7ae7e,
          explosion: 0xffc25e,
          craft: 0x9be7cf,
          pickup: 0xffdf8b,
          buff: 0x93dbea,
          dodge: 0xc8f6e2,
          objective: 0x9fe4b7,
          keepsake: 0xffe0a0,
        };
        const count =
          e.type === "explosion"
            ? 28
            : e.type === "break"
              ? 12
              : e.type === "pickup"
                ? 3
                : 9;
        for (let n = 0; n < count; n++) {
          const node = mesh(
            new THREE.BoxGeometry(0.07, 0.07, 0.07),
            new THREE.MeshBasicMaterial({
              color: colors[e.type],
              transparent: true,
            }),
          );
          node.position.set(e.x, 0.3, e.z);
          this.dynamic.add(node);
          this.effects.push({
            node,
            velocity: new THREE.Vector3(
              (Math.random() - 0.5) * 3,
              1 + Math.random() * 3,
              (Math.random() - 0.5) * 3,
            ),
            life: 0.6 + Math.random() * 0.5,
            max: 1.1,
          });
        }
      }
    }
  }
  aim(clientX, clientY) {
    const rect = this.canvas.getBoundingClientRect();
    this.raycaster.setFromCamera(
      new THREE.Vector2(
        ((clientX - rect.left) / rect.width) * 2 - 1,
        (-(clientY - rect.top) / rect.height) * 2 + 1,
      ),
      this.camera,
    );
    const point = new THREE.Vector3();
    return this.raycaster.ray.intersectPlane(this.plane, point)
      ? { x: point.x, z: point.z }
      : null;
  }
  resize() {
    const w = innerWidth,
      h = innerHeight;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }
  async icon(word) {
    if (this.icons.has(word)) return this.icons.get(word);
    const model = this.clone(word);
    if (!model) return "";
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0xe8d6b5);
    scene.add(new THREE.HemisphereLight(0xfff7e2, 0x596966, 3));
    const light = new THREE.DirectionalLight(0xffffff, 4);
    light.position.set(-3, 5, 4);
    scene.add(light);
    scene.add(model);
    const box = new THREE.Box3().setFromObject(model),
      size = box.getSize(new THREE.Vector3()),
      center = box.getCenter(new THREE.Vector3());
    model.position.sub(center);
    const r = Math.max(size.x, size.y, size.z) * 1.5;
    const cam = new THREE.PerspectiveCamera(32, 1, 0.01, 100);
    cam.position.set(r * 0.8, r * 0.6, r);
    cam.lookAt(0, 0, 0);
    const target = new THREE.WebGLRenderTarget(160, 160);
    this.renderer.setRenderTarget(target);
    this.renderer.render(scene, cam);
    const pixels = new Uint8Array(160 * 160 * 4);
    this.renderer.readRenderTargetPixels(target, 0, 0, 160, 160, pixels);
    this.renderer.setRenderTarget(null);
    const c = document.createElement("canvas");
    c.width = c.height = 160;
    const ctx = c.getContext("2d"),
      image = ctx.createImageData(160, 160);
    for (let y = 0; y < 160; y++)
      image.data.set(
        pixels.subarray((159 - y) * 160 * 4, (160 - y) * 160 * 4),
        y * 160 * 4,
      );
    ctx.putImageData(image, 0, 0);
    target.dispose();
    const result = c.toDataURL();
    this.icons.set(word, result);
    return result;
  }
}
