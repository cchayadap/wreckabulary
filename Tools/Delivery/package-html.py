#!/usr/bin/env python3
import argparse
import hashlib
from pathlib import Path
import zipfile

PLAY = """WRECKABULARY — HTML EDITION

Extract this ZIP. From the extracted folder run:
    python3 -m http.server 8080
Then open http://localhost:8080 in your browser.
You can also upload this folder to a static HTTP host, including a subdirectory.
Serve over HTTP rather than opening index.html directly from the filesystem.

Choose a mode and house from the front door. Break objects, collect letters and
spell the twelve enabled recipes. See How to play for keyboard/touch controls.

Creative Workshop: type furniture words, arrange the actual 3D models, save a
home and tour it. Unlimited Workshop decor is separate from match crafting.
Your wardrobe and home saves stay in this browser. Use Export to keep a portable
home layout and Import to restore it on another browser or in the native project.

This is the standalone HTML/Three.js edition, separately implemented from Unity.
All runtime models, images, game data and JavaScript dependencies are included.
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dist", type=Path, default=Path(__file__).resolve().parents[2] / "Web/dist")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    required = ["index.html", "art/manifest.json", "UI/ActionIcons.png", "data/items.json",
                "data/house_pinwheel.json", "data/house_courtyard.json", "art/avatar.glb"]
    if any(not (args.dist / item).is_file() for item in required):
        raise SystemExit("Build the HTML edition before packaging it.")
    if args.output.exists():
        raise SystemExit("Choose a new delivery filename; existing packages are preserved.")
    files = sorted(p for p in args.dist.rglob("*") if p.is_file())
    if any(p.suffix in (".env", ".log") or "node_modules" in p.parts or ".harness" in p.parts for p in files):
        raise SystemExit("Unexpected private/development content in build output.")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.output, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as package:
        for path in files:
            package.write(path, path.relative_to(args.dist).as_posix())
        package.writestr("PLAY-README.txt", PLAY)
    with zipfile.ZipFile(args.output) as package:
        bad = package.testzip()
        if bad:
            raise SystemExit("ZIP CRC check failed: " + bad)
    digest = hashlib.sha256(args.output.read_bytes()).hexdigest()
    args.output.with_suffix(args.output.suffix + ".sha256").write_text(digest + "  " + args.output.name + "\n")
    print(str(args.output))
    print("bytes=" + str(args.output.stat().st_size) + " sha256=" + digest)


if __name__ == "__main__":
    main()
