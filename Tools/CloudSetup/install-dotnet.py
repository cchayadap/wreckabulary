#!/usr/bin/env python3
"""Install the pinned rules-harness SDK without root, preserving artifact checksums.

Package hashes were verified against Microsoft's signed Debian repository InRelease
on 2026-10-01. This installer changes no system packages or shell profiles.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def install(destination: Path):
    manifest = json.loads(Path(__file__).with_name("dotnet-packages.json").read_text())
    archive = destination / "downloads"
    archive.mkdir(parents=True, exist_ok=True)
    for package in manifest["packages"]:
        file = archive / Path(package["path"]).name
        if not file.exists():
            subprocess.run(["curl", "--fail", "--location", "--retry", "2",
                            "--output", str(file), manifest["source"] + package["path"]], check=True)
        actual = hashlib.sha256(file.read_bytes()).hexdigest()
        if actual != package["sha256"]:
            raise RuntimeError(f"Checksum mismatch for {file}; remove it and retry.")
        subprocess.run(["dpkg-deb", "-x", str(file), str(destination)], check=True)
    subprocess.run([str(destination / "usr/share/dotnet/dotnet"), "--version"], check=True)
    print(f"Use DOTNET_ROOT={destination / 'usr/share/dotnet'} and its dotnet executable.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--destination", type=Path, default=Path("/workspace/.cloud-setup/dotnet-root"))
    install(parser.parse_args().destination.resolve())
