# Wreckabulary browser edition

A standalone Three.js game for one human with AI housemates. It uses the Unity project's JSON rules, item catalogue, two maps and cosmetic wardrobe, and genuine supplied models exported to GLB. This is a separately implemented HTML game; it is not a Unity WebGL build.

Requires Node 22.12+ or 24 and the repository's hydrated Git LFS assets. From this directory:

```sh
npm ci
npm test
npm run build
npm run preview -- --port 4173
```

Open `http://localhost:4173`. `dist/` can be hosted by any static HTTP server, including under a subdirectory. Serve over HTTP; browsers do not load model/data assets reliably from `file://`. No CDN, account or API key is needed during play. For development use `npm run dev` (port 4173).

The build synchronizes canonical data from `Assets/_Project/Data/Config/`; edit those source files rather than the public copies. GLB export and licensing provenance are documented in `Tools/AssetPipeline/README.md` and `docs/art/provenance.md`. The generated action art is a raster button atlas; game objects are actual meshes.

For desktop: WASD/arrows move, mouse aims, left click/J attacks, right click/K blocks with PLATE, E picks up or holds a revive, Q/C opens spelling, Tab swaps hands, 1/2 selects a hand, F places/uses, R drops, G throws, Space jumps, Shift dodges and Esc pauses. Tap a bag letter to toss it. Touch uses a movement joystick, autoaim and illustrated action buttons. All outfits and Classic/Candy/Arcade gear styles are cosmetic.

Dibs is first to three rounds; Duos adds an AI buddy and revives. Moving Day delivers recipe parcels, checks the shared room checklist and replenishes shortages. Moving Out requires physically carrying marked original keepsakes, dropping them at the van, and gathering all surviving teammates there alive. Play & Learn has a training dummy and no time limit. Browser play is solo; online multiplayer and a second local human are not implemented in this edition.

Run functional browser checks against a running dev or preview server:

```sh
npm run test:browser
# Optional overrides:
CHROMIUM_PATH=/path/to/chromium WRECKABULARY_URL=http://localhost:4173 npm run test:browser
```

The test uses actual Chromium keyboard, pointer and touchscreen input, and saves screenshots/report to ignored `playwright-results/`. Mechanics tests cover letter conservation, duplicate letters, channel cancellation, health/shields, hazards, item ownership, physical objectives, replenishment and cooperative AI completion. Test results do not establish real device frame rates or verify the Unity build.
