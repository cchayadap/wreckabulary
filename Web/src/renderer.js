import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { RoundedBoxGeometry } from "three/addons/geometries/RoundedBoxGeometry.js";
import { clone as cloneSkeleton } from "three/addons/utils/SkeletonUtils.js";
import { RoomEnvironment } from "three/addons/environments/RoomEnvironment.js";
import { MaterialPalette } from "./materials.js";
import { AvatarAnimator } from "./animator.js";
export const AVATAR_SCALE = 1.23;
const ROOM_COLORS = {
  Garden: 0x92b78b,
  Playroom: 0xe1b970,
  Bedroom: 0xc8b3bd,
  Kitchen: 0xb8c9be,
  Study: 0xa8b7c8,
  LivingRoom: 0xd4b397,
};
const material = (color, roughness = 0.8) => {
  const value = new THREE.MeshStandardMaterial({ color, roughness });
  value.userData.viewOwned = true;
  return value;
};
const mesh = (geometry, mat) => {
  geometry.userData.viewOwned = true;
  for (const value of Array.isArray(mat) ? mat : [mat])
    if (!value.userData.paletteOwned && !value.userData.viewShared)
      value.userData.viewOwned = true;
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
    this.renderer.toneMappingExposure = 1.14;
    this.materials = new MaterialPalette(this.renderer);
    this.assetGeometries = new Set();
    this.assetMaterials = new Set();
    this.assetTextures = new Set();
    this.visualMaterials = new Set();
    this.visualTextures = new Set();
    this.skinMaterials = new Map();
    this.wardrobeMaterials = new Map();
    this.floorMaterials = new Map();
    const pmrem = new THREE.PMREMGenerator(this.renderer);
    const studio = new RoomEnvironment();
    this.environmentTarget = pmrem.fromScene(studio, 0.04);
    this.scene.environment = this.environmentTarget.texture;
    this.scene.environmentIntensity = 0.35;
    studio.dispose();
    pmrem.dispose();
    this.camera = new THREE.PerspectiveCamera(43, 1, 0.1, 120);
    this.camera.position.set(16, 22, 23);
    this.target = new THREE.Vector3();
    this.camera.lookAt(this.target);
    this.look = { yaw: 0, pitch: 0.16, distance: 2.6 };
    this.shake = 0;
    this.raycaster = new THREE.Raycaster();
    this.plane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
    this.models = new Map();
    this.entities = new Map();
    this.avatarModels = new Map();
    this.effects = [];
    this.vfx = [];
    this.icons = new Map();
    this.loader = new GLTFLoader();
    this.world = new THREE.Group();
    this.dynamic = new THREE.Group();
    this.workshopOverlay = new THREE.Group();
    this.scene.add(this.world, this.dynamic, this.workshopOverlay);
    this.designEntities = new Map();
    this.workshop = {
      enabled: false,
      selectedId: null,
      ghost: null,
      insets: {},
      zoom: 1,
      center: new THREE.Vector3(),
      distance: 40,
    };
    this.scene.add(new THREE.HemisphereLight(0xfff1dc, 0x465e60, 1.65));
    const sun = new THREE.DirectionalLight(0xffead1, 2.85);
    sun.position.set(-8, 19, 8);
    sun.castShadow = true;
    const shadowSize = matchMedia("(pointer: coarse)").matches ? 1024 : 2048;
    sun.shadow.mapSize.set(shadowSize, shadowSize);
    Object.assign(sun.shadow.camera, {
      left: -25,
      right: 25,
      top: 25,
      bottom: -25,
      near: 0.1,
      far: 70,
    });
    sun.shadow.bias = -0.0004;
    sun.shadow.normalBias = 0.025;
    this.scene.add(sun);
    const fill = new THREE.DirectionalLight(0xd7ebf2, 1.05);
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
    const entries = [
      ...Object.entries(this.manifest.items ?? {}).map(([id, a]) => [id, a]),
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
      ...Object.entries(this.manifest.vfx ?? {}).map(([id, a]) => [
        `vfx:${id}`,
        a,
      ]),
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
    const total = entries.length;
    const worker = async () => {
      while (entries.length) {
        const [id, a] = entries.shift();
        if (!a?.path) continue;
        const gltf = await this.loader.loadAsync(url(a.path));
        gltf.scene.traverse((o) => {
          if (o.isMesh) {
            o.castShadow = true;
            o.receiveShadow = true;
            this.assetGeometries.add(o.geometry);
            const originals = Array.isArray(o.material)
              ? o.material
              : [o.material];
            for (const source of originals) {
              this.assetMaterials.add(source);
              for (const texture of Object.values(source))
                if (texture?.isTexture) this.assetTextures.add(texture);
            }
            if (id !== "avatar" && !id.startsWith("letter:")) {
              const values = originals.map((source) =>
                this.materials.enrichMaterial(source, id),
              );
              o.material = Array.isArray(o.material) ? values : values[0];
            }
          }
        });
        this.models.set(id, gltf);
        progress(++done, total);
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
    texture.userData = { viewOwned: true };
    texture.colorSpace = THREE.SRGBColorSpace;
    const sprite = new THREE.Sprite(
      new THREE.SpriteMaterial({ map: texture, depthTest: false }),
    );
    sprite.material.userData.viewOwned = true;
    sprite.scale.set(2.3, 0.57, 1);
    return sprite;
  }
  floorMaterial(room) {
    const b = room.bounds,
      w = b[2] - b[0],
      d = b[3] - b[1];
    const key = `${room.name}:${w}:${d}`;
    if (this.floorMaterials.has(key)) return this.floorMaterials.get(key);
    let value;
    if (room.name === "Garden")
      value = this.materials.surface("lawn", 0x86a17a, {
        repeatX: w / 4,
        repeatY: d / 4,
      });
    else if (room.name === "Kitchen") {
      const canvas = document.createElement("canvas");
      canvas.width = canvas.height = 128;
      const ctx = canvas.getContext("2d");
      ctx.fillStyle = "#f1e7ce";
      ctx.fillRect(0, 0, 128, 128);
      ctx.fillStyle = "#bcc9ba";
      ctx.fillRect(0, 0, 64, 64);
      ctx.fillRect(64, 64, 64, 64);
      ctx.strokeStyle = "#c7c2b1";
      ctx.lineWidth = 1.5;
      for (const x of [0, 64, 128]) {
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x, 128);
        ctx.stroke();
      }
      for (const y of [0, 64, 128]) {
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(128, y);
        ctx.stroke();
      }
      const texture = new THREE.CanvasTexture(canvas);
      texture.colorSpace = THREE.SRGBColorSpace;
      texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
      texture.repeat.set(w / 1.6, d / 1.6);
      texture.anisotropy = Math.min(
        8,
        this.renderer.capabilities.getMaxAnisotropy(),
      );
      value = this.materials
        .surface("ceramic", 0xffffff, { repeatX: w / 1.6, repeatY: d / 1.6 })
        .clone();
      value.map = texture;
      value.userData.viewShared = true;
      this.visualTextures.add(texture);
      this.visualMaterials.add(value);
    } else
      value = this.materials.surface(
        "planks",
        room.name === "Study" ? 0xb68e6b : 0xcba37b,
        { repeatX: w / 4, repeatY: d / 1.2 },
      );
    this.floorMaterials.set(key, value);
    return value;
  }
  rebuild(game) {
    this.tallWalls = game.status !== "preview";
    if (game.status !== "preview" || game.mode === "Tour") this.setLobby(null);
    const worldKey = `${game.map.id}:${game.house.name}:${game.mode === "MovingOut"}:${this.tallWalls}`;
    const reuse =
      game.mode === "Tour" &&
      this.game?.mode === "Tour" &&
      this.worldKey === worldKey;
    this.game = game;
    if (reuse) {
      for (const fx of this.effects) this.removeObject(fx.node);
      this.effects = [];
      this.update(game, 0);
      return;
    }
    this.worldKey = worldKey;
    for (const animation of this.avatarModels.values()) {
      animation.mixer.stopAllAction();
      animation.mixer.uncacheRoot(animation.avatar);
    }
    this.clear(this.world);
    this.clear(this.dynamic);
    this.entities.clear();
    this.avatarModels.clear();
    this.vfx = [];
    this.designEntities.clear();
    this.effects = [];
    const s = game.map.scale;
    const foundation = this.box(
      2 * game.extent + 2,
      0.6,
      2 * game.extent + 2,
      0xc4aa86,
    );
    foundation.position.y = -0.45;
    this.world.add(foundation);
    const lawn = this.box(80, 0.4, 80, 0x577960);
    lawn.material.dispose();
    lawn.material = this.materials.surface("lawn", 0x789775, {
      repeatX: 18,
      repeatY: 18,
    });
    lawn.position.y = -0.85;
    this.world.add(lawn);
    for (const room of game.house.rooms) {
      const b = room.bounds,
        w = b[2] - b[0],
        d = b[3] - b[1];
      const floor = mesh(
        new THREE.BoxGeometry(w, 0.15, d),
        this.floorMaterial(room),
      );
      floor.position.set((b[0] + b[2]) / 2, -0.075, (b[1] + b[3]) / 2);
      this.world.add(floor);
      const rugW = Math.min(w - 1, w * 0.62),
        rugD = Math.min(d - 1, d * 0.55);
      const rug = mesh(
        new RoundedBoxGeometry(rugW, 0.008, rugD, 2, 0.003),
        this.materials.rug(ROOM_COLORS[room.name] ?? 0xe4c695, rugW / rugD),
      );
      rug.position.set(floor.position.x, 0.004, floor.position.z);
      this.world.add(rug);
      const text = this.label(
        room.name.replace(/([a-z])([A-Z])/g, "$1 $2").toUpperCase(),
        "#37575b",
        "#f3e6cf",
      );
      text.position.set(floor.position.x, 0.4, floor.position.z - d * 0.35);
      text.material.opacity = 0.8;
      text.visible = !this.tallWalls && !this.lobbyView;
      text.userData.roomLabel = true;
      this.world.add(text);
    }
    for (const wall of game.walls) {
      const horizontal = wall.z1 === wall.z2,
        length = horizontal ? wall.x2 - wall.x1 : wall.z2 - wall.z1;
      const exterior =
        Math.abs(horizontal ? wall.z1 : wall.x1) >= game.extent - 0.01;
      const height = this.tallWalls ? (exterior ? 2.7 : 2.4) : exterior ? 0.8 : 1.2;
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
      m.material.dispose();
      m.material = this.materials.surface("plaster", 0xeddfc4, {
        repeatX: Math.max(1, length / 3),
        repeatY: 1,
      });
      this.world.add(m);
      const trim = this.box(
        horizontal ? length : 0.22,
        0.085,
        horizontal ? 0.22 : length,
        0xb58760,
      );
      trim.material.dispose();
      trim.material = this.materials.surface("wood", 0xb58760, {
        repeatX: Math.max(1, length / 2),
        repeatY: 1,
      });
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
    if (game.map.id === "pinwheel" && game.mode !== "Tour") {
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
    if (kitchen && game.mode !== "Tour") {
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
        plant.userData.yardPlant = true;
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
        const box = new THREE.Box3().setFromObject(avatar);
        avatar.scale.setScalar(AVATAR_SCALE);
        avatar.position.y = -box.min.y * AVATAR_SCALE;
        group.add(avatar);
        this.applyWardrobe(avatar, p.wardrobe);
        const source = this.models.get("avatar");
        const animator = new AvatarAnimator(avatar, source.animations ?? [], {
          locomotion: this.manifest.avatar?.locomotion,
          scale: AVATAR_SCALE,
        });
        this.avatarModels.set(p.id, {
          avatar,
          animator,
          mixer: animator.mixer,
          get current() {
            return animator.current;
          },
        });
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
      name.visible = p.id !== 0 && !this.lobbyView;
      name.userData.nameTag = p.id !== 0;
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
  applyWardrobe(avatar, wardrobe = {}) {
    const config = this.data.wardrobe;
    const colourFor = (piece) => {
      const from = piece.colourFrom ?? piece.slot;
      const id = wardrobe.colours?.[from] ?? config.default.colours[from];
      return config.palettes[from]?.find((c) => c.id === id);
    };
    avatar.traverse((o) => {
      if (!o.isMesh) return;
      const piece = config.pieces.find(
        (p) => o.name === p.mesh || o.name.startsWith(`${p.mesh}_`),
      );
      const visible = !piece || wardrobe.pieces?.[piece.slot] === piece.id;
      if (piece) o.visible = visible;
      const colour = piece?.tint && visible ? colourFor(piece) : null;
      const tint = piece?.tint?.toLowerCase();
      const originals = Array.isArray(o.material) ? o.material : [o.material];
      const values = originals.map((source) => {
        const name = source.name.toLowerCase();
        const rib = tint === "fabric_main" && name.includes("fabric_rib");
        const tinted = colour && (name.includes(tint) || rib);
        const key = `${source.uuid}:${tinted ? `${colour.id}${rib ? ":rib" : ""}` : "-"}`;
        if (this.wardrobeMaterials.has(key))
          return this.wardrobeMaterials.get(key);
        const copy = source.clone();
        if (tinted)
          copy.color.setRGB(
            ...colour.rgb.map((v) => (rib ? v * 0.75 : v)),
            THREE.SRGBColorSpace,
          );
        copy.userData.viewShared = true;
        this.visualMaterials.add(copy);
        this.wardrobeMaterials.set(key, copy);
        return copy;
      });
      o.material = Array.isArray(o.material) ? values : values[0];
    });
  }
  removeObject(root) {
    const geometries = new Set(),
      materials = new Set(),
      textures = new Set(),
      skeletons = new Set();
    root.traverse((o) => {
      if (
        o.geometry?.userData.viewOwned &&
        !this.assetGeometries.has(o.geometry)
      )
        geometries.add(o.geometry);
      for (const value of Array.isArray(o.material)
        ? o.material
        : [o.material]) {
        if (
          !value ||
          value.userData.paletteOwned ||
          value.userData.viewShared ||
          this.assetMaterials.has(value)
        )
          continue;
        if (value.userData.viewOwned) {
          materials.add(value);
          for (const texture of Object.values(value))
            if (
              texture?.isTexture &&
              texture.userData.viewOwned &&
              !this.assetTextures.has(texture) &&
              !this.visualTextures.has(texture)
            )
              textures.add(texture);
        }
      }
      if (o.isSkinnedMesh && o.skeleton) skeletons.add(o.skeleton);
    });
    root.removeFromParent();
    for (const geometry of geometries) geometry.dispose();
    for (const value of materials) value.dispose();
    for (const texture of textures) texture.dispose();
    for (const skeleton of skeletons) skeleton.dispose();
  }
  clear(group) {
    for (const child of [...group.children]) this.removeObject(child);
  }
  itemModel(item) {
    const model = this.clone(item.delivery ? "BOX" : item.word);
    if (!model) return null;
    const group = new THREE.Group(),
      sources = new WeakMap();
    group.add(model);
    model.traverse((o) => {
      if (o.isMesh)
        sources.set(
          o,
          Array.isArray(o.material) ? [...o.material] : [o.material],
        );
    });
    group.userData = {
      model,
      word: item.word,
      designId: item.designId ?? null,
      sources,
    };
    this.dynamic.add(group);
    return group;
  }
  skinMaterial(source, skin, flash = false) {
    const key = `${source.uuid}:${skin}:${flash}`;
    if (this.skinMaterials.has(key)) return this.skinMaterials.get(key);
    const name = source.name.replace(/_(Classic|Candy|Arcade)$/, "");
    const info =
      this.manifest.materialSkins?.[name]?.[skin] ??
      this.manifest.materialSkins?.[name]?.Classic;
    if (!info && !flash) return source;
    const value = source.clone();
    if (info) {
      value.color.setRGB(...info.baseColor.slice(0, 3), THREE.SRGBColorSpace);
      value.roughness = info.roughness;
      value.metalness = info.metallic;
      value.emissive.setRGB(...info.emissive);
    }
    if (flash && value.emissive) value.emissive.add(new THREE.Color(0x6a3022));
    value.userData.viewShared = true;
    this.visualMaterials.add(value);
    this.skinMaterials.set(key, value);
    return value;
  }
  applySkin(node, skin, flash = false) {
    const key = `${skin}:${flash}`;
    if (node.userData.skinKey === key) return;
    node.userData.skin = skin;
    node.userData.skinKey = key;
    node.userData.model.traverse((o) => {
      if (!o.isMesh) return;
      const sources = node.userData.sources.get(o);
      const values = sources.map((source) =>
        this.skinMaterial(source, skin, flash),
      );
      o.material = Array.isArray(o.material) ? values : values[0];
    });
  }
  update(game, dt) {
    if (!this.ready) return;
    if (this.workshop.enabled) {
      this.setFov(43);
      this.updateWorkshopCamera();
    }
    else {
      this.camera.clearViewOffset();
      const p = game.players[0],
        preview = game.status === "preview";
      if (this.lobbyView) {
        this.setFov(40);
        const { camera, target } = this.lobbyView,
          k = dt === 0 ? 1 : 1 - Math.exp(-dt * 4);
        this.camera.position.lerp(new THREE.Vector3(camera.x, camera.y, camera.z), k);
        this.target.lerp(new THREE.Vector3(target.x, target.y, target.z), k);
        this.camera.lookAt(this.target);
        this.animateLobby(performance.now() / 1000);
        return this.updateEntities(game, dt);
      }
      this.setFov(!this.closet && !preview ? 64 : 43);
      if (!this.closet && !preview) {
        this.shoulderCamera(p, dt);
        return this.updateEntities(game, dt);
      }
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
    }
    this.updateEntities(game, dt);
  }
  shoulderCamera(p, dt) {
    const look = this.look,
      yaw = look.yaw,
      pitch = look.pitch,
      forward = new THREE.Vector3(
        Math.sin(yaw) * Math.cos(pitch),
        -Math.sin(pitch),
        Math.cos(yaw) * Math.cos(pitch),
      );
    this.shoulderY = THREE.MathUtils.lerp(
      this.shoulderY ?? p.y,
      p.y,
      1 - Math.exp(-dt * 12),
    );
    const pivot = new THREE.Vector3(p.x, this.shoulderY + 1.3, p.z);
    let distance = look.distance;
    const game = this.lastGame ?? this.game;
    if (this.tallWalls && game?.segmentBlocked) {
      const at = (d) => ({
        x: pivot.x - forward.x * d * 1.15,
        z: pivot.z - forward.z * d * 1.15,
      });
      if (game.segmentBlocked(pivot, at(distance))) {
        let low = 0,
          high = distance;
        for (let i = 0; i < 8; i++) {
          const mid = (low + high) / 2;
          if (game.segmentBlocked(pivot, at(mid))) high = mid;
          else low = mid;
        }
        distance = Math.max(0.35, low);
      }
    }
    this.cameraDistance = THREE.MathUtils.lerp(
      this.cameraDistance ?? distance,
      distance,
      distance < (this.cameraDistance ?? distance) ? 1 : 1 - Math.exp(-dt * 6),
    );
    const position = pivot.clone().addScaledVector(forward, -this.cameraDistance);
    position.y = Math.max(0.35, position.y + 0.55);
    this.camera.position.copy(position);
    this.target.copy(pivot).addScaledVector(forward, 12);
    this.camera.lookAt(this.target);
    this.camera.position.add(
      new THREE.Vector3(
        (Math.random() - 0.5) * this.shake,
        (Math.random() - 0.5) * this.shake,
        0,
      ),
    );
    this.shake = Math.max(0, (this.shake ?? 0) - dt * 1.2);
  }
  setLobby(view) {
    this.lobbyView = view;
    if (!view) {
      if (this.lobbyStage?.parent) {
        this.lobbyStage.removeFromParent();
        this.world.visible = true;
        this.scene.background = this.lobbySaved.background;
        this.scene.fog.color.copy(this.lobbySaved.fog);
        this.scene.fog.near = this.lobbySaved.near;
        this.scene.fog.far = this.lobbySaved.far;
        for (const [light, intensity] of this.lobbySaved.lights) light.intensity = intensity;
      }
      for (const node of this.entities.values()) node.visible = true;
      this.dynamic.traverse((o) => {
        if (o.userData.nameTag) o.visible = true;
      });
      return;
    }
    if (!this.lobbyStage) this.lobbyStage = this.buildLobbyStage(view.spots);
    if (!this.lobbyStage.parent) {
      this.lobbySaved = {
        background: this.scene.background,
        fog: this.scene.fog.color.clone(),
        near: this.scene.fog.near,
        far: this.scene.fog.far,
        lights: this.scene.children.filter((o) => o.isLight).map((l) => [l, l.intensity]),
      };
      for (const [light, intensity] of this.lobbySaved.lights) light.intensity = intensity * 0.42;
      this.scene.add(this.lobbyStage);
      this.scene.background = this.lobbyStage.userData.sky;
      this.scene.fog.color.set(0x1a2fb8);
      this.scene.fog.near = 16;
      this.scene.fog.far = 42;
    }
    this.world.visible = false;
    this.lobbyStage.position.set(view.centre.x, view.floor, view.centre.z);
    this.lobbyStage.rotation.y = view.yaw;
    this.dynamic.traverse((o) => {
      if (o.userData.nameTag) o.visible = false;
    });
  }
  screenOf(x, y, z) {
    const v = new THREE.Vector3(x, y, z).project(this.camera),
      rect = this.renderer.domElement.getBoundingClientRect();
    return {
      x: rect.left + ((v.x + 1) / 2) * rect.width,
      y: rect.top + ((1 - v.y) / 2) * rect.height,
      visible: v.z < 1 && Math.abs(v.x) < 1.1 && Math.abs(v.y) < 1.1,
    };
  }
  animateLobby(time) {
    const stage = this.lobbyStage;
    if (!stage) return;
    for (const f of stage.userData.floaters) {
      f.node.position.y = f.y + Math.sin(time * 0.9 + f.phase) * 0.12;
      f.node.rotation.y = f.turn + Math.sin(time * 0.5 + f.phase) * 0.5;
    }
    if (stage.userData.beam)
      stage.userData.beam.material.opacity = 0.4 + Math.sin(time * 2.2) * 0.1;
  }
  buildLobbyStage(spots) {
    const stage = new THREE.Group(),
      canvasTexture = (w, h, paint) => {
        const canvas = document.createElement("canvas");
        canvas.width = w;
        canvas.height = h;
        paint(canvas.getContext("2d"), w, h);
        const texture = new THREE.CanvasTexture(canvas);
        texture.colorSpace = THREE.SRGBColorSpace;
        return texture;
      };
    stage.userData.sky = canvasTexture(4, 512, (ctx, w, h) => {
      const g = ctx.createLinearGradient(0, 0, 0, h);
      g.addColorStop(0, "#0b0c44");
      g.addColorStop(0.45, "#1d2fc4");
      g.addColorStop(0.72, "#2f6dff");
      g.addColorStop(1, "#46c4ff");
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
    });
    const floorMap = canvasTexture(1024, 1024, (ctx, w, h) => {
      const g = ctx.createRadialGradient(w / 2, h / 2, 0, w / 2, h / 2, w / 2);
      g.addColorStop(0, "#3a63ff");
      g.addColorStop(0.35, "#2440d8");
      g.addColorStop(1, "#141f86");
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
      ctx.strokeStyle = "#7fb2ff55";
      ctx.lineWidth = 2;
      for (let i = 0; i <= w; i += 32) {
        ctx.beginPath();
        ctx.moveTo(i, 0);
        ctx.lineTo(i, h);
        ctx.moveTo(0, i);
        ctx.lineTo(w, i);
        ctx.stroke();
      }
    });
    const floor = new THREE.Mesh(
      new THREE.CircleGeometry(32, 96),
      new THREE.MeshBasicMaterial({ map: floorMap, toneMapped: false }),
    );
    floor.rotation.x = -Math.PI / 2;
    floor.receiveShadow = true;
    stage.add(floor);
    const glowMap = canvasTexture(256, 256, (ctx, w) => {
      const g = ctx.createRadialGradient(w / 2, w / 2, 0, w / 2, w / 2, w / 2);
      g.addColorStop(0, "#ffffff");
      g.addColorStop(0.35, "#ffffff88");
      g.addColorStop(1, "#ffffff00");
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, w);
    });
    const glow = (radius, color, opacity) => {
      const disc = new THREE.Mesh(
        new THREE.PlaneGeometry(radius * 2, radius * 2),
        new THREE.MeshBasicMaterial({
          map: glowMap,
          color,
          transparent: true,
          opacity,
          blending: THREE.AdditiveBlending,
          depthWrite: false,
        }),
      );
      disc.rotation.x = -Math.PI / 2;
      return disc;
    };
    const centreGlow = glow(5.5, 0x4fd8ff, 0.3);
    centreGlow.position.y = 0.01;
    stage.add(centreGlow);
    const padTop = material(0x10155a),
      beamMap = canvasTexture(4, 256, (ctx, w, h) => {
        const g = ctx.createLinearGradient(0, 0, 0, h);
        g.addColorStop(0, "#ffffff00");
        g.addColorStop(0.7, "#ffffff33");
        g.addColorStop(1, "#ffffff99");
        ctx.fillStyle = g;
        ctx.fillRect(0, 0, w, h);
      });
    spots.forEach(([side, back], i) => {
      const you = i === 0,
        colour = you ? 0x3ff0ff : 0x9ec5ff,
        pad = new THREE.Group(),
        base = mesh(new THREE.CylinderGeometry(0.66, 0.72, 0.12, 48), padTop),
        ring = new THREE.Mesh(
          new THREE.TorusGeometry(0.67, 0.035, 8, 64),
          new THREE.MeshBasicMaterial({ color: colour }),
        );
      base.position.y = 0.06;
      ring.rotation.x = Math.PI / 2;
      ring.position.y = 0.12;
      const shine = glow(you ? 1.5 : 1.1, colour, you ? 0.9 : 0.45);
      shine.position.y = 0.125;
      pad.add(base, ring, shine);
      if (you) {
        const beam = new THREE.Mesh(
          new THREE.CylinderGeometry(0.62, 0.68, 2.8, 48, 1, true),
          new THREE.MeshBasicMaterial({
            map: beamMap,
            color: colour,
            transparent: true,
            opacity: 0.6,
            side: THREE.DoubleSide,
            blending: THREE.AdditiveBlending,
            depthWrite: false,
          }),
        );
        beam.position.y = 1.52;
        pad.add(beam);
        stage.userData.beam = beam;
      }
      pad.position.set(-side, 0, back);
      stage.add(pad);
    });
    stage.add(this.lobbyTiles());
    stage.userData.floaters = [];
    [
      ["W", -4.6, 2.6, -2.6],
      ["O", 4.4, 2.2, -2.2],
      ["R", -3.4, 3.4, -3.6],
      ["D", 3.6, 3.3, -3.4],
      ["S", 5.6, 1.4, -1.0],
      ["A", -5.8, 1.2, -1.4],
    ].forEach(([char, x, y, z], i) => {
      const node = this.clone(`letter:${char}`);
      if (!node) return;
      node.scale.setScalar(2.1);
      node.position.set(x, y, z);
      stage.add(node);
      stage.userData.floaters.push({ node, y, phase: i * 1.7, turn: x < 0 ? 0.35 : -0.35 });
    });
    const key = new THREE.SpotLight(0xffd9a0, 28, 22, 0.55, 0.6, 1.4);
    key.position.set(0, 7, 3);
    key.target.position.set(0, 2.4, -5);
    const rim = new THREE.DirectionalLight(0x6fd4ff, 1.6);
    rim.position.set(0, 4, -6);
    rim.target.position.set(0, 1, 0);
    stage.add(key, key.target, rim, rim.target);
    return stage;
  }
  lobbyTiles() {
    const group = new THREE.Group(),
      tile = (char, scale) => {
        const node = this.clone(`letter:${char}`);
        if (!node) return null;
        node.scale.setScalar(scale);
        group.add(node);
        return node;
      },
      wobble = (i, amount) => (((i * 37) % 7) - 3) * amount,
      size = 4.4,
      step = 0.21 * size + 0.05,
      back = -5,
      boxHeight = 0.74,
      lift = boxHeight * 2;
    const cardboard = material(0xb06f32),
      tape = material(0xe9d6ac);
    for (let row = 0; row < 2; row++)
      for (let i = 0; i < 8 - row; i++) {
        const w = 0.98 + wobble(i + row, 0.025),
          box = new THREE.Group(),
          body = mesh(new THREE.BoxGeometry(w, boxHeight, 0.9), cardboard),
          strip = mesh(new THREE.BoxGeometry(0.2, 0.012, 0.92), tape),
          band = mesh(new THREE.BoxGeometry(0.2, boxHeight * 0.45, 0.012), tape);
        strip.position.y = boxHeight / 2 + 0.006;
        band.position.set(0, boxHeight * 0.27, 0.456);
        box.add(body, strip, band);
        box.position.set(
          (i - (7 - row) / 2) * 1.0 + wobble(i, 0.02),
          boxHeight * (row + 0.5),
          back - 0.05 + wobble(i * 2 + row, 0.03),
        );
        box.rotation.y = wobble(i * 5 + row, 0.025);
        group.add(box);
      }
    [
      ["ABULARY", 0],
      ["WRECK", 1],
    ].forEach(([word, row]) =>
      [...word].forEach((char, i) => {
        const node = tile(char, size);
        if (!node) return;
        node.position.set(
          (i - (word.length - 1) / 2) * step + (row ? 0.08 : 0),
          lift + row * 0.21 * size + Math.abs(wobble(i + row, 0.01)),
          back + wobble(i * 3 + row, 0.03),
        );
        node.rotation.set(0, wobble(i + 2 * row, 0.035), wobble(i + 5 * row, 0.018));
      }),
    );
    [
      ["B", -2.35, 1.55, 0.5],
      ["A", 2.15, 1.85, -0.35],
      ["M", 2.75, 0.9, 0.9],
    ].forEach(([char, x, z, turn]) => {
      const node = tile(char, 2.2);
      if (!node) return;
      node.rotation.set(-Math.PI / 2, 0, turn);
      node.position.set(x, 0.0285 * 2.2, z);
    });
    return group;
  }
  setFov(fov) {
    if (this.camera.fov === fov) return;
    this.camera.fov = fov;
    this.camera.updateProjectionMatrix();
  }
  updateEntities(game, dt) {
    for (const player of game.players) {
      const group = this.entities.get(`p${player.id}`);
      if (!group) continue;
      group.position.set(player.x, player.y, player.z);
      const avatar = group.userData.avatar;
      group.userData.bubble.visible = player.bubble > 0;
      group.userData.ring.visible = player.state === "alive";
      group.userData.name.material.opacity =
        player.state === "eliminated" ? 0.3 : 1;
      avatar.visible = player.state !== "eliminated";
      const anim = this.avatarModels.get(player.id);
      if (anim) {
        const item =
          game.items.find(
            (i) => i.id === player.slots?.[player.slot] && i.state === "held",
          ) ?? null;
        anim.animator.instantTurn = player.id === 0 && game.status === "playing";
        anim.animator.update(dt, player, game, item);
      }
      group.userData.last = { x: player.x, z: player.z };
    }
    const active = new Set();
    this.designEntities.clear();
    for (const item of game.items) {
      if (item.state === "gone" || item.state === "packed") continue;
      const key = `i${item.id}`;
      active.add(key);
      let node = this.entities.get(key);
      if (
        node &&
        (node.userData.word !== item.word ||
          node.userData.designId !== (item.designId ?? null))
      ) {
        this.removeObject(node);
        this.entities.delete(key);
        node = null;
      }
      if (!node) {
        node = this.itemModel(item);
        if (!node) continue;
        this.entities.set(key, node);
      }
      if (item.designId) this.designEntities.set(item.designId, node);
      node.visible = true;
      this.applySkin(
        node,
        item.origin === "decor"
          ? (item.skin ?? "Classic")
          : item.state === "held"
            ? item.owner === 0
              ? game.skin
              : "Classic"
            : item.state === "thrown"
              ? (item.skin ?? "Classic")
              : "Classic",
        item.origin === "map" && item.flashUntil > game.time,
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
        if (this.attachToHands(node, item, owner, nodeScale, game)) continue;
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
        this.removeObject(node);
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
        this.removeObject(node);
        this.entities.delete(key);
      }
    const zones = new Set(game.zones.map((z) => `zone${z.id}`));
    for (const [key, node] of this.entities)
      if (key.startsWith("zone") && !zones.has(key)) {
        this.removeObject(node);
        this.entities.delete(key);
      }
    for (const zone of game.zones) {
      const key = `zone${zone.id}`;
      let node = this.entities.get(key);
      if (!node) {
        node = mesh(
          new THREE.CircleGeometry(zone.radius, 48,
            zone.effect === "WindField" ? (zone.rotation ?? 0) - Math.PI / 2 - (zone.arc ?? 360) * Math.PI / 360 : 0,
            zone.effect === "WindField" ? (zone.arc ?? 360) * Math.PI / 180 : Math.PI * 2),
          new THREE.MeshBasicMaterial({
            color:
              zone.effect === "WindField" ? 0x78e6cb : zone.effect === "SlowField" ? 0xa18af2 : zone.effect === "SlipZone"
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
        node.position.set(zone.x, (zone.y ?? 0) + 0.045, zone.z);
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
    this.lastGame = game;
    this.updateVfx(dt);
    for (const fx of [...this.effects]) {
      fx.life -= dt;
      if (fx.life <= 0) {
        this.removeObject(fx.node);
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
    this.updateWorkshopSelection();
    if (this.lobbyView)
      for (const [key, node] of this.entities) node.visible = /^p\d+$/.test(key);
    this.renderer.render(this.scene, this.camera);
  }
  attachToHands(node, item, owner, scale, game = this.lastGame) {
    const animator = this.avatarModels.get(owner.id)?.animator;
    if (!animator || owner.state === "eliminated") return false;
    const asset = this.manifest.items?.[item.word];
    if (item.state === "carried") {
      const mid = new THREE.Vector3();
      if (!animator.carryPoint(mid)) return false;
      const size = asset?.size ?? [0.6, 0.6, 0.6];
      node.quaternion.setFromAxisAngle(new THREE.Vector3(0, 1, 0), animator.yaw);
      const forward = new THREE.Vector3(
        Math.sin(animator.yaw),
        0,
        Math.cos(animator.yaw),
      );
      node.position.copy(mid).addScaledVector(forward, size[2] * scale * 0.5 + 0.05);
      node.position.y = Math.max(owner.y ?? 0, mid.y - size[1] * scale * 0.9);
      return true;
    }
    const shield =
      item.definition?.family === "Shield" || !!item.definition?.shield;
    if (shield && game?.isGuardRaised(owner)) {
      const yaw = new THREE.Quaternion().setFromAxisAngle(
        new THREE.Vector3(0, 1, 0),
        animator.yaw,
      );
      node.quaternion
        .copy(yaw)
        .multiply(
          new THREE.Quaternion().setFromEuler(new THREE.Euler(-Math.PI / 2, 0, 0)),
        );
      const forward = new THREE.Vector3(0, 0, 1).applyQuaternion(yaw);
      const offset = new THREE.Vector3(...(asset?.grip ?? [0, 0, 0]))
        .multiplyScalar(scale)
        .applyQuaternion(node.quaternion);
      node.position
        .set(owner.x, (owner.y ?? 0) + 0.55, owner.z)
        .addScaledVector(forward, 0.32)
        .sub(offset);
      return true;
    }
    const base = new THREE.Quaternion().setFromEuler(
      shield ? new THREE.Euler(-Math.PI / 2, 0, 0) : new THREE.Euler(0.35, 0, 0),
    );
    return animator.holdTransform(node, asset?.grip, scale, base);
  }
  spawnVfx(id, x, y, z, options = {}) {
    const { life = 0.6, scale = 1, billboard = false, rise = 0 } = options;
    const node = this.clone(`vfx:${id}`);
    if (!node) return;
    node.position.set(x, y, z);
    node.scale.setScalar(0.01);
    this.dynamic.add(node);
    this.vfx.push({ node, life, max: life, scale, billboard, rise });
  }
  updateVfx(dt) {
    for (const fx of [...this.vfx]) {
      fx.life -= dt;
      if (fx.life <= 0) {
        this.removeObject(fx.node);
        this.vfx.splice(this.vfx.indexOf(fx), 1);
        continue;
      }
      const t = 1 - fx.life / fx.max;
      const envelope =
        Math.min(t / 0.15, 1) * Math.min(fx.life / (fx.max * 0.35), 1);
      fx.node.scale.setScalar(Math.max(0.01, fx.scale * envelope));
      fx.node.position.y += fx.rise * dt;
      if (fx.billboard) fx.node.quaternion.copy(this.camera.quaternion);
      else fx.node.rotation.y += dt * 1.5;
    }
  }
  animateEvent(e) {
    const anim = this.avatarModels.get(e.player)?.animator;
    if (!anim) return;
    const p = this.lastGame?.players?.[e.player];
    switch (e.type) {
      case "swing": {
        const item = p ? this.lastGame.held?.(p) : null;
        const thrust =
          e.word === "PUNCH" || item?.definition?.family === "MeleeThrust";
        anim.trigger(thrust ? "thrust" : "swing");
        break;
      }
      case "throw":
        anim.trigger("throw");
        break;
      case "pickup":
      case "equip":
      case "carry":
        anim.trigger("pickup");
        break;
      case "placeStart":
        anim.trigger("place");
        break;
      case "useStart":
        anim.trigger("drink");
        break;
      case "hit":
        anim.trigger("hit");
        break;
      case "block":
        anim.flinchOnly(3);
        break;
      case "craft":
        anim.trigger("present");
        break;
    }
  }
  handleEvents(events) {
    for (const e of events) {
      if (e.player !== undefined) this.animateEvent(e);
      if (e.type === "result" && e.win)
        this.avatarModels.get(0)?.animator.trigger("celebrate");
      const p =
        e.player !== undefined ? this.lastGame?.players?.[e.player] : null;
      if (e.type === "hit" && p)
        this.spawnVfx("Impact_Star", p.x, (p.y ?? 0) + 1.0, p.z, {
          life: 0.4,
          scale: 1.4,
          billboard: true,
        });
      else if (e.type === "craft" && p)
        this.spawnVfx("Craft_Ring", p.x, (p.y ?? 0) + 0.05, p.z, {
          life: 0.8,
          scale: 1.6,
        });
      else if (e.type === "pickup")
        this.spawnVfx("Pickup_Ring", e.x, 0.05, e.z, { life: 0.45 });
      else if (e.type === "jump" && p)
        this.spawnVfx("Jump_Arrow", p.x, 0.1, p.z, {
          life: 0.4,
          scale: 1.2,
          billboard: true,
          rise: 1.5,
        });
      else if (e.type === "buff" && p && e.word === "FOAM")
        this.spawnVfx("Foam_Cloud", p.x, (p.y ?? 0) + 0.4, p.z, {
          life: 1.2,
          scale: 2.2,
        });
      else if (e.type === "buff" && p && e.word === "SODA")
        this.spawnVfx("Speed_Trail", p.x, (p.y ?? 0) + 0.2, p.z, { life: 0.8, scale: 1.5 });
      else if (e.type === "buff" && p)
        this.spawnVfx("Pickup_Ring", p.x, (p.y ?? 0) + 0.05, p.z, { life: 0.7, scale: 1.5, rise: 0.5 });
      else if (e.type === "splat")
        this.spawnVfx("Foam_Cloud", e.x, (e.y ?? 0) + 0.4, e.z, { life: 0.5, scale: 1.3 });
      else if (e.type === "break" || e.type === "furnitureHit")
        this.spawnVfx("Wood_Splinter", e.x, 0.5, e.z, {
          life: 0.5,
          scale: 1.6,
          rise: 1,
        });
      else if (e.type === "explosion")
        this.spawnVfx("Bomb_Warning", e.x, 0.05, e.z, {
          life: 0.5,
          scale: (e.radius ?? 2) / 0.44,
        });
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
    if (this.workshop.enabled)
      this.fitWorkshop(this.workshop.focus ?? null, false);
  }
  setWorkshop(options = {}) {
    const wasEnabled = this.workshop.enabled;
    const oldInsets = JSON.stringify(this.workshop.insets);
    for (const key of ["enabled", "selectedId", "ghost", "insets"])
      if (Object.hasOwn(options, key)) this.workshop[key] = options[key];
    this.workshop.insets ??= {};
    if (this.workshop.enabled) {
      this.camera.far = 320;
      this.scene.fog.near = 85;
      this.scene.fog.far = 180;
      const key = `${this.game?.map.id}:${this.game?.house.name}`;
      if (!wasEnabled || this.workshop.mapKey !== key) {
        this.workshop.mapKey = key;
        this.workshop.zoom = 1;
        this.fitWorkshop(null, true);
      } else if (oldInsets !== JSON.stringify(this.workshop.insets))
        this.fitWorkshop(this.workshop.focus ?? null, false);
      this.refreshWorkshopGhost();
      this.updateWorkshopCamera();
      this.updateWorkshopSelection();
    } else {
      this.camera.far = 120;
      this.scene.fog.near = 48;
      this.scene.fog.far = 100;
      this.camera.clearViewOffset();
      this.camera.updateProjectionMatrix();
      this.clear(this.workshopOverlay);
      this.ghostNode = this.selectionNode = null;
      this.ghostKey = null;
    }
  }
  workshopBounds(roomName = null) {
    const rooms = roomName
      ? this.game?.house.rooms.filter((r) => r.name === roomName)
      : this.game?.house.rooms;
    if (!rooms?.length) return null;
    const box = new THREE.Box3();
    for (const room of rooms) {
      const b = room.bounds;
      box.expandByPoint(new THREE.Vector3(b[0] - 0.6, 0, b[1] - 0.6));
      box.expandByPoint(new THREE.Vector3(b[2] + 0.6, 3, b[3] + 0.6));
    }
    return box;
  }
  workshopViewport() {
    const rect = this.canvas.getBoundingClientRect(),
      insets = this.workshop.insets;
    const width = Math.max(1, rect.width),
      height = Math.max(1, rect.height);
    const left = Math.max(0, Number(insets.left) || 0),
      right = Math.max(0, Number(insets.right) || 0);
    const top = Math.max(0, Number(insets.top) || 0),
      bottom = Math.max(0, Number(insets.bottom) || 0);
    return {
      width,
      height,
      left,
      right,
      top,
      bottom,
      availableW: Math.max(80, width - left - right),
      availableH: Math.max(80, height - top - bottom),
    };
  }
  fitWorkshop(roomName = null, recenter = true) {
    const bounds = this.workshopBounds(roomName);
    if (!bounds) return;
    this.workshop.focus = roomName;
    if (recenter) bounds.getCenter(this.workshop.center).setY(0.7);
    const viewport = this.workshopViewport();
    const effectiveFov = THREE.MathUtils.radToDeg(
      2 *
        Math.atan(
          (Math.tan(THREE.MathUtils.degToRad(this.camera.fov / 2)) *
            viewport.availableH) /
            viewport.height,
        ),
    );
    const camera = new THREE.PerspectiveCamera(
      effectiveFov,
      viewport.availableW / viewport.availableH,
      0.1,
      320,
    );
    const direction = new THREE.Vector3(0.62, 1.05, 0.83).normalize();
    const corners = [];
    for (const x of [bounds.min.x, bounds.max.x])
      for (const y of [bounds.min.y, bounds.max.y])
        for (const z of [bounds.min.z, bounds.max.z])
          corners.push(new THREE.Vector3(x, y, z));
    let low = 5,
      high = 280;
    const center = bounds.getCenter(new THREE.Vector3()).setY(0.7);
    for (let n = 0; n < 22; n++) {
      const distance = (low + high) / 2;
      camera.position.copy(center).addScaledVector(direction, distance);
      camera.lookAt(center);
      camera.updateMatrixWorld();
      const fits = corners.every((p) => {
        const q = p.clone().project(camera);
        return (
          Math.abs(q.x) <= 0.94 &&
          Math.abs(q.y) <= 0.94 &&
          q.z >= -1 &&
          q.z <= 1
        );
      });
      if (fits) high = distance;
      else low = distance;
    }
    this.workshop.distance = high;
    this.updateWorkshopCamera();
  }
  updateWorkshopCamera() {
    if (!this.workshop.enabled) return;
    const v = this.workshopViewport();
    const distance = this.workshop.distance * this.workshop.zoom;
    this.camera.far = Math.max(320, distance * 2);
    this.scene.fog.near = Math.max(85, distance * 1.4);
    this.scene.fog.far = Math.max(180, distance * 2.8);
    this.camera.setViewOffset(
      v.width,
      v.height,
      (v.right - v.left) / 2,
      (v.bottom - v.top) / 2,
      v.width,
      v.height,
    );
    this.camera.position
      .copy(this.workshop.center)
      .addScaledVector(
        new THREE.Vector3(0.62, 1.05, 0.83).normalize(),
        this.workshop.distance * this.workshop.zoom,
      );
    this.target.copy(this.workshop.center);
    this.camera.lookAt(this.target);
    this.camera.updateMatrixWorld();
  }
  panWorkshop(dx, dy) {
    if (!this.workshop.enabled || !Number.isFinite(dx) || !Number.isFinite(dy))
      return;
    const v = this.workshopViewport();
    const metresPerPixel =
      (2 *
        this.workshop.distance *
        this.workshop.zoom *
        Math.tan(THREE.MathUtils.degToRad(this.camera.fov / 2))) /
      v.height;
    const right = new THREE.Vector3().setFromMatrixColumn(
      this.camera.matrixWorld,
      0,
    );
    const forward = this.camera
      .getWorldDirection(new THREE.Vector3())
      .setY(0)
      .normalize();
    this.workshop.center
      .addScaledVector(right, -dx * metresPerPixel)
      .addScaledVector(forward, dy * metresPerPixel * 1.4);
    const bounds = this.workshopBounds();
    if (bounds) {
      this.workshop.center.x = THREE.MathUtils.clamp(
        this.workshop.center.x,
        bounds.min.x,
        bounds.max.x,
      );
      this.workshop.center.z = THREE.MathUtils.clamp(
        this.workshop.center.z,
        bounds.min.z,
        bounds.max.z,
      );
    }
    this.updateWorkshopCamera();
  }
  zoomWorkshop(delta) {
    if (!this.workshop.enabled || !Number.isFinite(delta)) return;
    this.workshop.zoom = THREE.MathUtils.clamp(
      this.workshop.zoom * Math.exp(delta * 0.0015),
      0.35,
      2.3,
    );
    this.updateWorkshopCamera();
  }
  focusWorkshop(roomName = null) {
    if (!this.workshop.enabled) return;
    this.workshop.zoom = 1;
    this.fitWorkshop(roomName, true);
  }
  refreshWorkshopGhost() {
    const ghost = this.workshop.ghost;
    if (
      !ghost ||
      !this.models.has(ghost.word) ||
      !Number.isFinite(ghost.x) ||
      !Number.isFinite(ghost.z)
    ) {
      if (this.ghostNode) this.removeObject(this.ghostNode);
      this.ghostNode = null;
      this.ghostKey = null;
      return;
    }
    const key = `${ghost.word}:${ghost.skin}:${Boolean(ghost.valid)}`;
    if (this.ghostKey !== key) {
      if (this.ghostNode) this.removeObject(this.ghostNode);
      const node = this.itemModel({ word: ghost.word });
      this.applySkin(node, ghost.skin ?? "Classic");
      const tint = ghost.valid ? 0x83e7bb : 0xf17865;
      const copies = new Map();
      node.traverse((o) => {
        if (!o.isMesh) return;
        const originals = Array.isArray(o.material) ? o.material : [o.material];
        const values = originals.map((source) => {
          if (copies.has(source)) return copies.get(source);
          const value = source.clone();
          delete value.userData.paletteOwned;
          delete value.userData.viewShared;
          value.userData.viewOwned = true;
          value.transparent = true;
          value.opacity = 0.48;
          value.depthWrite = false;
          value.color.lerp(new THREE.Color(tint), 0.22);
          copies.set(source, value);
          return value;
        });
        o.material = Array.isArray(o.material) ? values : values[0];
        o.castShadow = false;
      });
      const item = this.data.items.items.find((i) => i.id === ghost.word);
      const width = Math.max(0.4, item?.size?.[0] ?? 1),
        depth = Math.max(0.4, item?.size?.[2] ?? 1);
      const line = new THREE.LineLoop(
        new THREE.BufferGeometry().setFromPoints([
          new THREE.Vector3(-width / 2, 0.025, -depth / 2),
          new THREE.Vector3(width / 2, 0.025, -depth / 2),
          new THREE.Vector3(width / 2, 0.025, depth / 2),
          new THREE.Vector3(-width / 2, 0.025, depth / 2),
        ]),
        new THREE.LineBasicMaterial({
          color: tint,
          depthTest: false,
          transparent: true,
          opacity: 0.95,
        }),
      );
      line.geometry.userData.viewOwned = true;
      line.material.userData.viewOwned = true;
      line.renderOrder = 5;
      node.add(line);
      this.workshopOverlay.add(node);
      this.ghostNode = node;
      this.ghostKey = key;
    }
    this.ghostNode.position.set(
      ghost.x,
      this.game?.floorAt(ghost.x, ghost.z) ?? 0,
      ghost.z,
    );
    this.ghostNode.rotation.set(0, THREE.MathUtils.degToRad(ghost.yaw ?? 0), 0);
    this.ghostNode.updateMatrixWorld(true);
  }
  updateWorkshopSelection() {
    const selected =
      this.workshop.enabled &&
      this.designEntities.get(this.workshop.selectedId);
    if (!selected) {
      if (this.selectionNode) this.removeObject(this.selectionNode);
      this.selectionNode = null;
      return;
    }
    selected.updateMatrixWorld(true);
    if (!this.selectionNode) {
      this.selectionNode = new THREE.Box3Helper(new THREE.Box3(), 0xffdb89);
      this.selectionNode.geometry.userData.viewOwned = true;
      this.selectionNode.material.userData.viewOwned = true;
      this.selectionNode.material.depthTest = false;
      this.selectionNode.renderOrder = 6;
      this.workshopOverlay.add(this.selectionNode);
    }
    this.selectionNode.box.setFromObject(selected).expandByScalar(0.04);
    this.selectionNode.updateMatrixWorld(true);
  }
  pickWorkshop(clientX, clientY) {
    if (!this.workshop.enabled) return { point: null, designId: null };
    this.camera.updateMatrixWorld();
    const point = this.aim(clientX, clientY);
    this.dynamic.updateMatrixWorld(true);
    const hits = this.raycaster
      .intersectObjects([...this.designEntities.values()], true)
      .filter((hit) => hit.object.visible && hit.object.isMesh);
    let designId = null;
    if (hits.length) {
      let node = hits[0].object;
      while (node && !node.userData.designId) node = node.parent;
      designId = node?.userData.designId ?? null;
    }
    return { point, designId };
  }
  diagnostics() {
    return {
      calls: this.renderer.info.render.calls,
      triangles: this.renderer.info.render.triangles,
      geometries: this.renderer.info.memory.geometries,
      textures: this.renderer.info.memory.textures,
      programs: this.renderer.info.programs?.length ?? 0,
      entities: this.entities.size,
      loadedModels: this.models.size,
      skinVariants: this.skinMaterials.size,
      wardrobeVariants: this.wardrobeMaterials.size,
      materials: this.materials.info(),
    };
  }
  dispose() {
    if (this.disposed) return;
    this.disposed = true;
    for (const animation of this.avatarModels.values()) {
      animation.mixer.stopAllAction();
      animation.mixer.uncacheRoot(animation.avatar);
    }
    this.clear(this.world);
    this.clear(this.dynamic);
    this.clear(this.workshopOverlay);
    for (const value of this.visualMaterials) value.dispose();
    for (const texture of this.visualTextures) texture.dispose();
    this.materials.dispose();
    for (const geometry of this.assetGeometries) geometry.dispose();
    for (const value of this.assetMaterials) value.dispose();
    for (const texture of this.assetTextures) texture.dispose();
    this.scene.traverse((node) => {
      if (node.isLight && node.shadow) node.shadow.dispose();
    });
    this.scene.environment = null;
    this.environmentTarget.dispose();
    this.renderer.dispose();
    this.assetGeometries.clear();
    this.assetMaterials.clear();
    this.assetTextures.clear();
    this.effects = [];
    this.icons.clear();
    this.models.clear();
    this.entities.clear();
    this.designEntities.clear();
    this.avatarModels.clear();
    this.skinMaterials.clear();
    this.wardrobeMaterials.clear();
    this.floorMaterials.clear();
    this.visualMaterials.clear();
    this.visualTextures.clear();
  }
  async icon(word) {
    if (this.icons.has(word)) return this.icons.get(word);
    const model = this.clone(word);
    if (!model) return "";
    const scene = new THREE.Scene();
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
    const target = new THREE.WebGLRenderTarget(160, 160),
      clearColor = this.renderer.getClearColor(new THREE.Color()),
      clearAlpha = this.renderer.getClearAlpha();
    this.renderer.setRenderTarget(target);
    this.renderer.setClearColor(0x000000, 0);
    this.renderer.clear();
    this.renderer.render(scene, cam);
    this.renderer.setClearColor(clearColor, clearAlpha);
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
    this.removeObject(model);
    const result = c.toDataURL();
    this.icons.set(word, result);
    return result;
  }
}
