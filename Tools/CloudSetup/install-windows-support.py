#!/usr/bin/env python3
import argparse
import base64
import gzip
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import stat
import struct
import urllib.request
import xml.etree.ElementTree as ET
import zlib


class Section(io.RawIOBase):
    def __init__(self, stream, length):
        self.stream, self.remaining = stream, length

    def readable(self):
        return True

    def read(self, size=-1):
        size = self.remaining if size < 0 else min(size, self.remaining)
        data = self.stream.read(size)
        self.remaining -= len(data)
        return data


def exact(stream, length):
    data = stream.read(length)
    if len(data) != length:
        raise RuntimeError("Truncated official module payload")
    return data


def verify(archive, module):
    digest = hashlib.md5()
    count = 0
    with archive.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
            count += len(chunk)
    expected = module["integrity"]
    if not expected.startswith("md5-"):
        raise RuntimeError("Unsupported official integrity algorithm")
    if count != module["downloadSize"]["value"] or base64.b64encode(digest.digest()).decode() != expected[4:]:
        raise RuntimeError("Module size or official integrity verification failed")
    return count


def extract(archive, destination):
    files, bytes_written = 0, 0
    with archive.open("rb") as stream:
        magic, header_size, version, compressed, uncompressed, _ = struct.unpack(">4sHHQQI", exact(stream, 28))
        if magic != b"xar!" or version != 1 or compressed > 16 * 1024 * 1024:
            raise RuntimeError("Unsupported Unity package header")
        stream.seek(header_size)
        toc = zlib.decompress(exact(stream, compressed))
        if len(toc) != uncompressed:
            raise RuntimeError("Incorrect Unity package table length")
        entries = ET.fromstring(toc).findall(".//file")
        payloads = [entry for entry in entries if entry.findtext("name") == "Payload"]
        if len(payloads) != 1:
            raise RuntimeError("Expected exactly one module payload")
        payload = payloads[0]
        stream.seek(header_size + compressed + int(payload.findtext("data/offset")))
        with gzip.GzipFile(fileobj=Section(stream, int(payload.findtext("data/length")))) as body:
            while True:
                header = exact(body, 76)
                if header[:6] != b"070707":
                    raise RuntimeError("Unsupported module cpio format")
                mode = int(header[18:24], 8)
                name_size = int(header[59:65], 8)
                size = int(header[65:76], 8)
                if name_size < 1 or name_size > 4096:
                    raise RuntimeError("Invalid payload path length")
                encoded = exact(body, name_size)
                if encoded[-1:] != b"\0":
                    raise RuntimeError("Unterminated payload path")
                name = encoded[:-1].decode("utf-8")
                if name == "TRAILER!!!":
                    while body.read(1024 * 1024):
                        pass
                    break
                relative = PurePosixPath(name)
                if relative.is_absolute() or ".." in relative.parts:
                    raise RuntimeError("Payload path escapes module directory")
                target = destination.joinpath(*relative.parts)
                if not target.resolve().is_relative_to(destination.resolve()):
                    raise RuntimeError("Payload traverses an external symlink")
                kind = stat.S_IFMT(mode)
                if kind == stat.S_IFDIR:
                    if size:
                        raise RuntimeError("Unexpected directory payload")
                    target.mkdir(parents=True, exist_ok=True)
                elif kind == stat.S_IFREG:
                    target.parent.mkdir(parents=True, exist_ok=True)
                    remaining = size
                    with target.open("wb") as output:
                        while remaining:
                            chunk = exact(body, min(remaining, 1024 * 1024))
                            output.write(chunk)
                            remaining -= len(chunk)
                    os.chmod(target, mode & 0o777)
                    files += 1
                    bytes_written += size
                elif kind == stat.S_IFLNK:
                    link = exact(body, size).decode("utf-8")
                    if not (target.parent / link).resolve().is_relative_to(destination.resolve()):
                        raise RuntimeError("Payload symlink escapes module directory")
                    target.parent.mkdir(parents=True, exist_ok=True)
                    if target.is_symlink():
                        target.unlink()
                    target.symlink_to(link)
                else:
                    raise RuntimeError("Unsupported payload file type")
    return {"regularFiles": files, "extractedBytes": bytes_written}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-root", type=Path, default=Path("/workspace/.cloud-setup/Unity6000.6.3f1"))
    parser.add_argument("--archive", type=Path, default=Path("/workspace/.cloud-setup/windows-mono.pkg"))
    args = parser.parse_args()
    manifest = json.loads((Path(__file__).with_name("unity-packages.json")).read_text())
    module = manifest["windowsMono"]
    if not (args.unity_root / "Editor/Unity").is_file():
        raise RuntimeError("The pinned Linux editor must be installed first")
    if not args.archive.is_file():
        args.archive.parent.mkdir(parents=True, exist_ok=True)
        print("Downloading official Windows Mono support", flush=True)
        with urllib.request.urlopen(module["url"], timeout=60) as source, args.archive.open("wb") as output:
            while chunk := source.read(1024 * 1024):
                output.write(chunk)
    count = verify(args.archive, module)
    destination = args.unity_root / "Editor/Data/PlaybackEngines/WindowsStandaloneSupport"
    destination.mkdir(parents=True, exist_ok=True)
    result = extract(args.archive, destination)
    result.update(version=manifest["version"], revision=manifest["revision"], source=module["url"],
                  verifiedBytes=count, verifiedIntegrity=module["integrity"],
                  destination=str(destination), unityBuild="NOT RUN")
    (args.unity_root.parent / "windows-mono-install.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
