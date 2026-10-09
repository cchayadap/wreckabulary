import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(REPO, "Assets", "_Project", "Data", "Config", "items.json")
WEB_OUT = os.path.join(REPO, "Web", "public", "data", "items.json")

CORE = {
    "BAT": dict(family="MeleeSwing", hands=1, durability=20,
                melee=dict(damage=14, reach=1.6, arc=100, windup=0.18, active=0.10, recovery=0.30, knockback=7, breakPower=2, hitStun=0.3),
                notes="Fast close-range swings and knockback."),
    "BLADE": dict(family="MeleeSwing", hands=1, durability=16,
                  melee=dict(damage=24, reach=1.4, arc=70, windup=0.28, active=0.08, recovery=0.40, knockback=3, breakPower=2, hitStun=0.25),
                  notes="Damage-focused melee."),
    "LAMP": dict(family="MeleeThrust", hands=1, durability=18,
                 melee=dict(damage=16, reach=2.3, arc=25, windup=0.30, active=0.10, recovery=0.35, knockback=4, breakPower=1.5, hitStun=0.2),
                 notes="Longer-reaching thrusts."),
    "BALL": dict(family="Thrown", hands=1, durability=30,
                 thrown=dict(damage=12, speed=16, lob=False, recoverable=True, knockback=4, breakPower=1),
                 notes="Recoverable thrown projectile: it lands as an item anyone can pick up."),
    "PLATE": dict(family="Shield", hands=1, durability=60,
                  shield={"frontArc": 120, "reduction": 1.0, "moveSpeed": 0.7, "raise": 0.12},
                  notes="Blocks from the front only. Blocked damage wears it down."),
    "TABLE": dict(family="DeployCover", hands=2, durability=120,
                  deploy=dict(effect="Cover", place=0.8),
                  melee=dict(damage=22, reach=1.5, arc=110, windup=0.45, active=0.12, recovery=0.55, knockback=8, breakPower=3, hitStun=0.35),
                  legacyWords=["WALL"],
                  notes="Deployable cover and a slow handheld strike."),
    "BED": dict(family="DeployPad", hands=2, durability=80,
                deploy=dict(effect="JumpPad", place=0.9, strength=11),
                legacyWords=["SPRING"],
                notes="Deployable jump pad. The mattress is above step height, so the launch trigger sits on its sides as well as its top."),
    "MAT": dict(family="DeploySpeed", hands=2, durability=40,
                deploy=dict(effect="SpeedStrip", place=0.5, strength=1.6),
                legacyWords=["SKATES"],
                notes="Directional speed strip: 1.6x speed along its arrow for 1.5 s after leaving it."),
    "SOFA": dict(family="DeployCover", hands=2, durability=180,
                 deploy=dict(effect="Cover", place=1.2),
                 notes="Wider, heavier cover."),
    "SOAP": dict(family="DeployZone", hands=1, durability=0, consumable=True,
                 deploy=dict(effect="SlipZone", place=0.4, radius=2.5, lifetime=8),
                 legacyWords=["FLOOD"],
                 notes="Temporary slippery area. Spends its letters."),
    "FOAM": dict(family="Buff", hands=1, durability=0, consumable=True,
                 use=dict(effect="Bubble", amount=35, seconds=10, channel=0.4),
                 legacyWords=["ARMOR"],
                 notes="Temporary protection that refreshes rather than stacks. Spends its letters."),
    "BOMB": dict(family="Thrown", hands=1, durability=0, consumable=True,
                 thrown=dict(damage=45, edgeDamage=15, radius=3, fuse=2.5, speed=11, lob=True, knockback=9, breakPower=4),
                 legacyWords=["CANNON"],
                 notes="Clearly telegraphed delayed explosion: a growing ring and beeps during the 2.5 s fuse. Spends its letters."),
}

EXPANDED = {
    "HAMMER": "MeleeSwing", "SHIELD": "Shield", "SPEAR": "MeleeThrust", "BOW": "Ranged", "ARROW": "Thrown",
    "PAN": "MeleeSwing", "BROOM": "MeleeSwing", "CHAIR": "DeployPad", "STOOL": "DeployPad", "DESK": "DeployCover",
    "SHELF": "DeployCover", "CABINET": "DeployCover", "CRATE": "DeployCover", "BOX": "Utility", "CHEST": "Utility",
    "BARREL": "Thrown", "MUG": "Heal", "VASE": "Thrown", "BOOK": "Utility", "CLOCK": "DeployZone", "PLANT": "DeployCover",
    "POT": "Thrown", "FAN": "DeployZone", "PIE": "Thrown", "CAKE": "Heal", "SODA": "Buff", "WATER": "Heal", "APPLE": "Heal",
}

EXPANSION_STATS = {
    "APPLE": dict(family="Heal", hands=1, durability=0, consumable=True,
                  use=dict(effect="Heal", amount=30, channel=.55), notes="Eat to restore 30 HP after a short channel."),
    "WATER": dict(family="Heal", hands=1, durability=0, consumable=True,
                  use=dict(effect="Heal", amount=18, channel=.25), notes="Drink quickly to restore 18 HP."),
    "CAKE": dict(family="Heal", hands=1, durability=0, consumable=True,
                 use=dict(effect="Heal", amount=50, channel=1.1), notes="Restore 50 HP. The longer eating channel can be interrupted."),
    "SODA": dict(family="Buff", hands=1, durability=0, consumable=True,
                 use=dict(effect="Speed", amount=1.35, seconds=6, channel=.35), notes="Drink for 1.35x movement speed for 6 seconds. Refreshes instead of stacking."),
    "SHIELD": dict(family="Shield", hands=1, durability=90,
                   shield={"frontArc": 360, "reduction": 1, "moveSpeed": .55, "raise": .22}, notes="Hold to block from every direction. Raising it slows movement; blocked damage wears it down."),
    "FAN": dict(family="DeployZone", hands=1, durability=45,
                deploy=dict(effect="WindField", place=.7, radius=3.2, strength=8, lifetime=12, arc=90), notes="Place a fan that pushes nearby players in a forward cone for 12 seconds. Walls block the wind."),
    "CLOCK": dict(family="DeployZone", hands=1, durability=40,
                  deploy=dict(effect="SlowField", place=.6, radius=2.75, strength=.6, lifetime=10, arc=360), notes="Place a clock that slows nearby players to 60% speed for 10 seconds. Walls block the field."),
    "BROOM": dict(family="MeleeSwing", hands=1, durability=28,
                  melee=dict(damage=10, reach=2.2, arc=145, windup=.28, active=.14, recovery=.38, knockback=9, breakPower=1, hitStun=.22), notes="Wide sweeps push a crowd back, with light damage."),
    "HAMMER": dict(family="MeleeSwing", hands=1, durability=30,
                   melee=dict(damage=28, reach=1.25, arc=75, windup=.5, active=.12, recovery=.65, knockback=8, breakPower=4.5, hitStun=.42), notes="A slow, heavy swing with strong furniture-breaking power."),
    "SPEAR": dict(family="MeleeThrust", hands=1, durability=20,
                  melee=dict(damage=18, reach=2.9, arc=18, windup=.32, active=.08, recovery=.42, knockback=3.5, breakPower=1.5, hitStun=.2), notes="A narrow thrust with the longest melee reach."),
    "PIE": dict(family="Thrown", hands=1, durability=0, consumable=True,
                thrown=dict(damage=8, speed=13, lob=False, recoverable=False, knockback=3, breakPower=.5), notes="A single-use pie splats on its first impact. Spends its letters when thrown."),
    "STOOL": dict(family="DeployPad", hands=2, durability=45,
                  deploy=dict(effect="JumpPad", place=.35, strength=8.5), notes="A compact jump pad that places quickly and launches lower than BED."),
}

LEGACY = {
    "AXE": "Fast and weak (team word).", "SWORD": "Team word.", "UMBRELLA": "Long and powerful block (team word, hidden).",
    "WINGS": "Short glide (team word).", "ROPE": "Grapple (team word).", "BEES": "Swarm chases the nearest player (team word).",
    "MAGNET": "Pulls loose letters (team word).", "DUCK": "Decoy (team word).",
    "QUAKE": "Legendary: shakes the whole room (team word, hidden).", "ZAP": "Legendary: stuns nearby players (team word, hidden).",
}


def z_up_to_y_up_point(p):
    x, y, z = p
    return [round(-x, 4) + 0.0, round(z, 4), round(-y, 4) + 0.0]


def z_up_to_y_up_size(d):
    x, y, z = d
    return [round(x, 4), round(z, 4), round(y, 4)]


REPORT = os.path.join(REPO, "Assets", "_Project", "Data", "Generated", "build_report.json")


def floor_snaps():
    with open(REPORT, encoding="utf-8") as f:
        report = json.load(f)
    return {e["name"]: e.get("floor_snap_m", 0.0) for e in report["files"] if e["kind"] == "item"}


def asset_facts(r, snaps):
    grip = z_up_to_y_up_point(r["world_grip_xyz_m"])
    grip[1] = round(grip[1] + snaps.get(r["canonical_word"], 0.0), 4)
    return {
        "heldScale": round(r["held_scale"], 4),
        "grip": grip,
        "size": z_up_to_y_up_size(r["world_dimensions_xyz_m"]),
        "skins": list(r["skins"]),
    }


def build(recipes, snaps):
    items = []
    for r in recipes["recipes"]:
        word = r["canonical_word"]
        letters = "".join(sorted(word))
        declared = "".join(sorted(k * v for k, v in r["letters"].items()))
        if letters != declared:
            raise SystemExit(f"{word}: pack letters {declared} don't spell the word")
        core = word in CORE
        entry = {
            "id": word,
            "category": r["category"],
            "tier": "Core" if core else "Expanded",
            "enabled": core or word in EXPANSION_STATS,
            "consumable": bool(r["consumed_on_use"]),
            "model": f"Items/{word}",
        }
        entry.update(asset_facts(r, snaps))
        if entry["enabled"]:
            d = dict(CORE[word] if core else EXPANSION_STATS[word])
            if d.pop("consumable", False) != entry["consumable"]:
                raise SystemExit(f"{word}: consumable flag disagrees with the pack")
            if "deploy" in d:
                d["deploy"] = dict(d["deploy"], footprint=[entry["size"][0], entry["size"][2]])
            entry.update(d)
        else:
            entry["family"] = EXPANDED[word]
            entry["hands"] = 2 if r["category"] == "Furniture" else 1
            entry["notes"] = "Modelled; behaviour not built yet, so it is disabled. Breaks into letters as original furniture."
        items.append(entry)
    for word, note in LEGACY.items():
        items.append({"id": word, "category": "Legacy", "tier": "Legacy", "enabled": False, "consumable": False,
                      "model": None, "notes": note + " No model yet, so it is disabled."})
    order = {"Core": 0, "Expanded": 1, "Legacy": 2}
    items.sort(key=lambda i: (order[i["tier"]], i["id"]))
    return {"schema": 1, "note": "Item catalogue. Only enabled items can be crafted. Asset facts come from the Vault v2 pack; check them with Tools/DataTools/seed_items.py --check.", "items": items}


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--recipes", help="Vault v2 recipe manifest; required to rebuild, optional for checking original asset facts")
    p.add_argument("--check", action="store_true")
    args = p.parse_args()
    recipes = None
    if args.recipes:
        with open(args.recipes, encoding="utf-8") as f:
            recipes = json.load(f)
    elif not args.check:
        p.error("--recipes is required to rebuild the catalogue")
    if args.check:
        with open(OUT, encoding="utf-8") as f:
            data = json.load(f)
        current = {i["id"]: i for i in data["items"]}
        problems = []
        expected = dict(CORE, **EXPANSION_STATS)
        enabled = {i["id"] for i in data["items"] if i.get("enabled")}
        if enabled != set(expected):
            problems.append(f"enabled recipes differ: missing={sorted(set(expected) - enabled)}, unexpected={sorted(enabled - set(expected))}")
        for word, stats in expected.items():
            have = current.get(word, {})
            for key, value in stats.items():
                if key == "notes":
                    continue
                if key == "deploy":
                    value = dict(value, footprint=[have.get("size", [0, 0, 0])[0], have.get("size", [0, 0, 0])[2]])
                if have.get(key, False if key == "consumable" else None) != value:
                    problems.append(f"{word}.{key}: differs from the seeded gameplay contract")
            icon = os.path.join(REPO, "Assets", "_Project", "Resources", "UI", "Items", word + ".png")
            if not os.path.isfile(icon):
                problems.append(f"{word}: imported recipe icon missing")
        with open(WEB_OUT, encoding="utf-8") as f:
            if json.load(f) != data:
                problems.append("Web items.json is not synchronized with Unity")
        if recipes:
            snaps = floor_snaps()
            for r in recipes["recipes"]:
                word = r["canonical_word"]
                have = current.get(word)
                if have is None:
                    problems.append(f"{word} is missing from items.json")
                    continue
                for k, v in asset_facts(r, snaps).items():
                    if have.get(k) != v:
                        problems.append(f"{word}.{k}: items.json has {have.get(k)}, the pack says {v}")
        print("CHECK_RESULT " + json.dumps({"items": len(current), "enabled": len(enabled), "vaultFactsChecked": recipes is not None, "problems": problems}))
        sys.exit(1 if problems else 0)
    data = build(recipes, floor_snaps())
    for destination in (OUT, WEB_OUT):
        os.makedirs(os.path.dirname(destination), exist_ok=True)
        with open(destination, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, indent=1)
            f.write("\n")
    print(f"SEED_RESULT wrote {len(data['items'])} items to Unity and Web")


if __name__ == "__main__":
    main()
