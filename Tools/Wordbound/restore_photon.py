"""Restore the owner's pinned Photon downloads before opening a clean checkout."""

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys
import tarfile


PACKAGES = {
    "fusion": "375e21bc771ce5f7cea942b5f804e7c28c8f4d858f7e9aab96725214564bc52e",
    "physics": "1fbfccdbab0e9b00a8c1317d02a489ba17e38a3badd6ae10a59e751c603b1aaa",
}
MAX_UNPACKED = 128 * 1024 * 1024


def inspect_package(path, expected):
    package = Path(path).resolve(strict=True)
    if hashlib.sha256(package.read_bytes()).hexdigest() != expected:
        raise ValueError(f"Downloaded package hash differs from the pinned version: {package.name}")
    files = {}
    directories = set()
    with tarfile.open(package, "r:gz") as archive:
        members = archive.getmembers()
        if sum(m.size for m in members) > MAX_UNPACKED:
            raise ValueError("Package exceeds the unpacked size bound.")
        entries = {}
        for member in members:
            if member.isdir():
                continue
            name = member.name.removeprefix("./")
            if not member.isfile() or not re.fullmatch(r"[0-9a-f]{32}/[A-Za-z0-9_.]+", name):
                raise ValueError("Package contains an unexpected archive entry.")
            if name in entries:
                raise ValueError("Package contains a duplicate archive entry.")
            entries[name] = member
        for name in entries:
            if not name.endswith("/pathname"):
                continue
            prefix = name.removesuffix("pathname")
            relative = archive.extractfile(entries[name]).read().decode("utf-8").strip()
            parts = PurePosixPath(relative).parts
            if (
                len(parts) < 2 or parts[:2] != ("Assets", "Photon")
                or "\\" in relative or ":" in relative or "\x00" in relative
                or any(part in (".", "..") for part in parts)
                or PurePosixPath(relative).as_posix() != relative
            ):
                raise ValueError("Package path escapes Assets/Photon.")
            metadata = entries.get(prefix + "asset.meta")
            if metadata is None:
                raise ValueError(f"Package has no original metadata for {relative}.")
            additions = {relative + ".meta": archive.extractfile(metadata).read()}
            asset = entries.get(prefix + "asset")
            if asset:
                additions[relative] = archive.extractfile(asset).read()
            else:
                directories.add(relative)
            for output, data in additions.items():
                if output in files:
                    raise ValueError("Package repeats an output path.")
                files[output] = data
    if not files:
        raise ValueError("Package contains no Photon files.")
    return files, directories


def destination(project, relative):
    target = project.joinpath(*PurePosixPath(relative).parts)
    resolved = target.resolve()
    if not resolved.is_relative_to(project / "Assets"):
        raise ValueError("Destination resolves outside this project's Assets directory.")
    parent = target
    while parent != project:
        if parent.is_symlink() or (hasattr(parent, "is_junction") and parent.is_junction()):
            raise ValueError("SDK destination must not use symlinks or junctions.")
        parent = parent.parent
    return target


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", required=True, help="Real Unity checkout, not its parent.")
    parser.add_argument("--fusion", required=True, help="Owner-downloaded Fusion 2.1.3 unitypackage.")
    parser.add_argument("--physics", required=True, help="Owner-downloaded Physics Addon 2.1.2 unitypackage.")
    parser.add_argument("--verify-only", action="store_true", help="Inspect downloads without writing files.")
    args = parser.parse_args()
    project = Path(args.project).resolve(strict=True)
    if not (project / "ProjectSettings/ProjectVersion.txt").is_file() or not (project / "Assets").is_dir():
        raise ValueError("Project must contain Assets and ProjectSettings/ProjectVersion.txt.")
    files = {}
    directories = set()
    for key, expected in PACKAGES.items():
        incoming, folders = inspect_package(getattr(args, key), expected)
        for relative, data in incoming.items():
            if relative in files and files[relative] != data:
                raise ValueError("The pinned packages disagree about a shared output.")
            files[relative] = data
        directories.update(folders)
    if args.verify_only:
        print(json.dumps({"result": "VERIFIED", "packages": 2, "files": len(files), "directories": len(directories)}))
        return
    if (project / "Temp/UnityLockfile").exists():
        raise ValueError("Close this project's Unity Editor before restoring its SDK.")
    pending = []
    for relative, data in files.items():
        target = destination(project, relative)
        if target.exists():
            if not target.is_file() or target.read_bytes() != data:
                raise ValueError(f"Existing file differs; preserved without overwrite: {relative}")
        else:
            pending.append((target, data))
    folders = [destination(project, relative) for relative in directories]
    if any(folder.exists() and not folder.is_dir() for folder in folders):
        raise ValueError("An SDK directory path is occupied by a file.")
    for folder in folders:
        folder.mkdir(parents=True, exist_ok=True)
    for target, data in pending:
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("xb") as stream:
            stream.write(data)
    print(json.dumps({
        "result": "RESTORED", "written": len(pending), "unchanged": len(files) - len(pending),
        "next": "Run the gate checks, then Unity compilation. Native import and runtime verification are still required.",
    }))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, tarfile.TarError) as error:
        print(f"Photon restore failed: {error}", file=sys.stderr)
        sys.exit(1)
