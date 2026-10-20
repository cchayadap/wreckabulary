"""File type from magic bytes, never from a URL or an assumed extension (fal_job.py defect 8, generate.py defect 5)."""


def sniff(head):
    """(kind, ext): kind is budget.schema.json ledgerLine.outputs[].kind."""
    b = bytes(head[:64])
    if b.startswith(b"\x89PNG\r\n\x1a\n"):
        return "png", "png"
    if b.startswith(b"\xff\xd8\xff"):
        return "jpeg", "jpg"
    if len(b) >= 12 and b[:4] == b"RIFF" and b[8:12] == b"WEBP":
        return "webp", "webp"
    if len(b) >= 12 and b[:4] == b"RIFF" and b[8:12] == b"WAVE":
        return "wav", "wav"
    if b.startswith(b"glTF"):
        return "glb", "glb"
    if b.startswith(b"Kaydara FBX Binary"):
        return "fbx", "fbx"
    if b.lstrip().startswith(b"; FBX"):
        return "fbx", "fbx"
    if b.startswith(b"OggS"):
        return "ogg", "ogg"
    if b.startswith(b"ID3") or (len(b) >= 2 and b[0] == 0xFF and (b[1] & 0xE0) == 0xE0 and (b[1] & 0x06) != 0):
        return "mp3", "mp3"
    if len(b) >= 12 and b[4:8] == b"ftyp":
        brand = b[8:12]
        if brand in (b"M4A ", b"M4B "):
            return "m4a", "m4a"
        return "mp4", "mp4"
    if b.startswith(b"\x76\x2f\x31\x01"):
        return "exr", "exr"
    if b.startswith(b"#?RADIANCE") or b.startswith(b"#?RGBE"):
        return "hdr", "hdr"
    if b.startswith(b"PK\x03\x04") or b.startswith(b"PK\x05\x06"):
        return "zip", "zip"
    return "other", "bin"


def sniff_file(path):
    with open(path, "rb") as f:
        return sniff(f.read(64))


EXT_ALIASES = {"jpg": {"jpg", "jpeg"}, "png": {"png"}, "webp": {"webp"}, "glb": {"glb"}, "fbx": {"fbx"}, "wav": {"wav"},
               "mp3": {"mp3"}, "ogg": {"ogg"}, "m4a": {"m4a"}, "mp4": {"mp4", "m4v"}, "exr": {"exr"}, "hdr": {"hdr"}, "zip": {"zip"}}


def ext_matches(path, ext):
    suffix = str(path).rsplit(".", 1)[-1].lower() if "." in str(path) else ""
    return suffix in EXT_ALIASES.get(ext, {ext})
