"""progress_log.py: the progress-capture log (Docs/progress/log.jsonl): add with dedupe, check integrity and cadence,
and build before/after sheets for gates.

    uv run --project Tools/kit/capture Tools/kit/capture/progress_log.py add --feature F --camera C --file <png>
        [--gate G] [--dedupe 4] [--log Docs/progress/log.jsonl] [--json]
    uv run --project Tools/kit/capture Tools/kit/capture/progress_log.py check [--max-gap-min 5]
        [--session-break-min 60] [--dedupe 4] [--log …] [--json]
    uv run --project Tools/kit/capture Tools/kit/capture/progress_log.py before-after --feature F [--camera C]
        [--out Docs/progress/_out/<F>_before_after.png] [--log …] [--json]

Log line (CONTRACTS.md §7.7): {utc, feature, camera, path, gate} plus `dhash` (64-bit difference hash, hex).
The cadence rule it supports (OWNER_DEFAULTS C-17; extract_spec_and_gates.md §7): capture from a fixed story camera
after every significant change and at least every 5 minutes of work on the active feature; machine times are UTC;
an unchanged view is not logged again (dedupe); every gate gets a before/after pair per feature.
`check` fails on unreadable lines, non-UTC times and missing files; cadence gaps and near-duplicates are warnings.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import capture_common as cc  # noqa: E402

NAME_RE = re.compile(r"^[A-Za-z0-9_.-]{1,64}$")
DEFAULT_LOG = Path("Docs") / "progress" / "log.jsonl"


def base_dir() -> Path:
    return cc.find_workspace() or Path.cwd()


def read_log(log: Path) -> list[tuple[int, dict | None, str]]:
    """[(line number, parsed object or None, raw)] for every non-empty line."""
    if not log.is_file():
        return []
    out = []
    text = log.read_text(encoding="utf-8-sig")
    for i, raw in enumerate(text.splitlines(), 1):
        if not raw.strip():
            continue
        try:
            obj = json.loads(raw)
            out.append((i, obj if isinstance(obj, dict) else None, raw))
        except json.JSONDecodeError:
            out.append((i, None, raw))
    return out


def hash_file(p: Path) -> int | None:
    from PIL import Image  # noqa: PLC0415

    try:
        with Image.open(p) as im:
            return cc.dhash(im)
    except Exception:  # noqa: BLE001
        return None


def line_hash(obj: dict, base: Path) -> int | None:
    h = obj.get("dhash")
    if isinstance(h, str) and re.fullmatch(r"[0-9a-f]{16}", h):
        return int(h, 16)
    p = base / str(obj.get("path", ""))
    return hash_file(p) if p.is_file() else None


def cmd_add(args, base: Path, log: Path) -> dict:
    for label, v in (("--feature", args.feature), ("--camera", args.camera)):
        if not NAME_RE.match(v or ""):
            raise cc.UsageError(f"{label} must match {NAME_RE.pattern}")
    f = Path(args.file)
    if not f.is_file():
        raise cc.ToolError(f"capture not found: {args.file}", cc.EXIT_FAIL)
    if not cc.is_inside(f, base):
        raise cc.UsageError("captures must live inside the workspace (Docs/progress/… or Assets/_AgentCaptures/…)")
    h = hash_file(f)
    if h is None:
        raise cc.ToolError(f"not a readable image: {args.file}", cc.EXIT_FAIL)
    rel = f.resolve().relative_to(base.resolve()).as_posix()
    if args.dedupe >= 0:
        for _n, obj, _raw in reversed(read_log(log)):
            if obj and obj.get("feature") == args.feature and obj.get("camera") == args.camera:
                prev = line_hash(obj, base)
                if prev is not None and cc.hamming(prev, h) <= args.dedupe:
                    return {"ok": True, "logged": False, "duplicate_of": obj.get("path"),
                            "distance": cc.hamming(prev, h), "log": cc.rel_path(log, base)}
                break
    line = {"utc": cc.utc_iso(), "feature": args.feature, "camera": args.camera, "path": rel,
            "gate": args.gate or None, "dhash": f"{h:016x}"}
    log.parent.mkdir(parents=True, exist_ok=True)
    with open(log, "a", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(line, ensure_ascii=False) + "\n")
    return {"ok": True, "logged": True, "line": line, "log": cc.rel_path(log, base)}


def cmd_check(args, base: Path, log: Path) -> dict:
    bad, missing, dupes, gaps, order = [], [], [], [], []
    per_feature: dict[str, list] = {}
    last_hash: dict[tuple, tuple[int, str]] = {}
    prev_t = None
    entries = read_log(log)
    for n, obj, raw in entries:
        if obj is None:
            bad.append({"line": n, "reason": "not a JSON object"})
            continue
        miss = [k for k in ("utc", "feature", "camera", "path") if not obj.get(k)]
        if miss:
            bad.append({"line": n, "reason": f"missing {', '.join(miss)}"})
            continue
        t = cc.parse_utc(obj["utc"])
        if t is None:
            bad.append({"line": n, "reason": f"utc '{obj['utc']}' is not YYYY-MM-DDTHH:MM:SSZ"})
            continue
        if prev_t is not None and t < prev_t:
            order.append({"line": n, "utc": obj["utc"]})
        prev_t = t
        p = base / str(obj["path"])
        if not p.is_file():
            missing.append({"line": n, "path": obj["path"]})
        per_feature.setdefault(obj["feature"], []).append((t, obj))
        key = (obj["feature"], obj["camera"])
        h = line_hash(obj, base) if args.dedupe >= 0 else None
        if h is not None:
            if key in last_hash and cc.hamming(last_hash[key][0], h) <= args.dedupe:
                dupes.append({"line": n, "path": obj["path"], "of": last_hash[key][1]})
            last_hash[key] = (h, obj["path"])
    summary = {}
    for feat, items in per_feature.items():
        items.sort(key=lambda x: x[0])
        for (t0, o0), (t1, o1) in zip(items, items[1:]):
            mins = (t1 - t0).total_seconds() / 60.0
            if args.max_gap_min < mins < args.session_break_min:
                gaps.append({"feature": feat, "from": o0["utc"], "to": o1["utc"], "minutes": round(mins, 1)})
        summary[feat] = {"captures": len(items), "first": items[0][1]["utc"], "last": items[-1][1]["utc"],
                         "cameras": sorted({o["camera"] for _, o in items})}
    ok = not bad and not missing
    return {"ok": ok, "log": cc.rel_path(log, base), "lines": len(entries), "bad_lines": bad, "missing_files": missing,
            "out_of_order": order, "near_duplicates": dupes, "cadence_gaps": gaps, "features": summary}


def cmd_before_after(args, base: Path, log: Path) -> dict:
    import contact_sheet  # noqa: PLC0415

    rows = [o for _n, o, _r in read_log(log) if o and o.get("feature") == args.feature and cc.parse_utc(o.get("utc", ""))
            and (base / str(o.get("path", ""))).is_file()]
    if args.camera:
        rows = [o for o in rows if o.get("camera") == args.camera]
    elif rows:
        counts: dict[str, int] = {}
        for o in rows:
            counts[o["camera"]] = counts.get(o["camera"], 0) + 1
        cam = sorted(counts, key=lambda c: (-counts[c], c))[0]
        rows = [o for o in rows if o["camera"] == cam]
    rows.sort(key=lambda o: o["utc"])
    if len(rows) < 2:
        raise cc.ToolError(f"feature {args.feature}: need at least 2 captures from one camera, found {len(rows)}",
                           cc.EXIT_FAIL)
    first, last = rows[0], rows[-1]
    out = Path(args.out) if args.out else base / "Docs" / "progress" / "_out" / f"{args.feature}_before_after.png"
    items = [(base / first["path"], f"BEFORE · {first['utc']}"), (base / last["path"], f"AFTER · {last['utc']}")]
    sheet, _c, unreadable, _g = contact_sheet.build_sheet(items, 2, 800, True, None,
                                                          f"{args.feature} · camera {first['camera']}")
    cc.save_image_atomic(sheet, out, format="PNG", optimize=True)
    return {"ok": not unreadable, "out": cc.rel_path(out, base), "feature": args.feature, "camera": first["camera"],
            "before": first["path"], "after": last["path"], "span": [first["utc"], last["utc"]]}


def parse_args(argv):
    ap = argparse.ArgumentParser(prog="progress_log.py", description="progress capture log: add, check, before-after")
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("add", "check", "before-after"):
        p = sub.add_parser(name)
        p.add_argument("--log", help="log file (default Docs/progress/log.jsonl in the workspace)")
        p.add_argument("--json", action="store_true")
        if name == "add":
            p.add_argument("--feature", required=True)
            p.add_argument("--camera", required=True)
            p.add_argument("--file", required=True)
            p.add_argument("--gate")
            p.add_argument("--dedupe", type=int, default=4, help="skip when within N bits of the last capture (-1: off)")
        elif name == "check":
            p.add_argument("--max-gap-min", type=float, default=5.0)
            p.add_argument("--session-break-min", type=float, default=60.0)
            p.add_argument("--dedupe", type=int, default=4)
        else:
            p.add_argument("--feature", required=True)
            p.add_argument("--camera")
            p.add_argument("--out")
    return ap.parse_args(argv)


def main(argv):
    args = parse_args(argv)
    base = base_dir()
    log = Path(args.log) if args.log else base / DEFAULT_LOG
    fn = {"add": cmd_add, "check": cmd_check, "before-after": cmd_before_after}[args.cmd]
    res = fn(args, base, log)
    cc.emit(res, args.json)
    return cc.EXIT_OK if res["ok"] else cc.EXIT_FAIL


if __name__ == "__main__":
    sys.exit(cc.run_main(main))
