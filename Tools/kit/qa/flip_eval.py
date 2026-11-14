"""NVIDIA FLIP perceptual score for hero shots (research_qa_harness §1.7). Started by flip.cmd:
    uv run --no-config --python 3.13 --with flip-evaluator==1.7 --with numpy==<pin> --with pillow==<pin> python flip_eval.py <ref> <test> [<out dir>]

flip-evaluator (BSD) has Windows wheels for CPython 3.8-3.13 only, hence --python 3.13. The API call follows
NVlabs/flip src/python/api_example.py: flip_evaluator.evaluate(reference, test, "LDR") returns
(error_map, mean_error, parameters).

Writes <out dir>/flip.json {schema, reference, test, mean_flip, error_map_png, tool, utc} and <out dir>/flip_<test name>.png
(the error map), then prints the same JSON as one line. <out dir> defaults to the test image's folder.
Exit 0 ok, 64 bad arguments, 65 tool error: codes uv never returns, so flip.cmd can tell them from uv's own failures
and map them to the kit's 2 and 3. FLIP is advisory: it never fails a gate by itself.
"""
import datetime
import json
import os
import sys

EXIT_USAGE = 64
EXIT_TOOL = 65


def emit(obj, code):
    if code:
        obj["exit"] = 2 if code == EXIT_USAGE else 3
    print(json.dumps(obj))
    return code


def main(argv):
    if len(argv) not in (2, 3):
        return emit({"ok": False, "error": "usage: flip.cmd <reference> <test> [<out dir>]"}, EXIT_USAGE)
    ref, test = argv[0], argv[1]
    for p in (ref, test):
        if not os.path.isfile(p):
            return emit({"ok": False, "error": f"image not found: {p}"}, EXIT_USAGE)
    out_dir = argv[2] if len(argv) == 3 else os.path.dirname(os.path.abspath(test))
    os.makedirs(out_dir, exist_ok=True)
    try:
        import numpy as np
        import flip_evaluator as flip
        from PIL import Image
    except ImportError as e:  # pragma: no cover - environment problem
        return emit({"ok": False, "error": f"missing dependency: {e}"}, EXIT_TOOL)
    try:
        error_map, mean_error, _params = flip.evaluate(ref, test, "LDR")
    except Exception as e:  # noqa: BLE001 - any evaluator failure is a tool error
        return emit({"ok": False, "error": f"flip_evaluator.evaluate failed: {e}"}, EXIT_TOOL)
    stem = os.path.splitext(os.path.basename(test))[0]
    map_path = os.path.join(out_dir, f"flip_{stem}.png")
    try:
        arr = np.asarray(error_map, dtype=np.float64)
        arr = (arr.clip(0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)
        Image.fromarray(arr).save(map_path)
    except Exception:  # noqa: BLE001 - the score is still useful without the map
        map_path = None
    result = {
        "ok": True,
        "schema": 1,
        "reference": ref.replace("\\", "/"),
        "test": test.replace("\\", "/"),
        "mean_flip": float(mean_error),
        "error_map_png": map_path.replace("\\", "/") if map_path else None,
        "tool": "flip",
        "utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    with open(os.path.join(out_dir, "flip.json"), "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(result, indent=2) + "\n")
    return emit(result, 0)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
