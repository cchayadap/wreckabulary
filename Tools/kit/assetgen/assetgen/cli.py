"""assetgen command line (CONTRACTS.md §4.6). `--json` prints exactly one JSON line on stdout; progress and human text
go elsewhere. Exit codes: 0 ok; 1 refused or FAIL (cap, policy, check, licence); 2 usage; 3 INFRA; 75 a lock or pool
wait expired. Long inputs come from files (--prompt-file, --params-file), never from argv."""

import argparse
import json
import sys

from . import __version__, asset as assetmod, budget, cc0, checks, doctor, reconcile, report, runner, schemas
from .errors import EXIT_FAIL, EXIT_INFRA, EXIT_OK, EXIT_USAGE, AssetgenError, Refused, UsageError
from .layout import Layout, find_workspace, parse_attempt_id
from .util import parse_since, read_json, write_json_atomic


class _Parser(argparse.ArgumentParser):
    def error(self, message):
        raise UsageError(message)


def _common(suppress=False):
    # Sub-command copies use SUPPRESS defaults so a global flag given before the command is not reset by the sub-parser.
    p = argparse.ArgumentParser(add_help=False, argument_default=argparse.SUPPRESS if suppress else None)
    p.add_argument("--json", action="store_true", help="print one JSON line on stdout")
    p.add_argument("--workspace", help="workspace root W (default: CLAUDE_PROJECT_DIR, then the folder holding Tools/kit/kit.json)")
    p.add_argument("--providers", help="use this providers.json instead of Tools/kit/assetgen/providers.json")
    return p


def _wait_flags(p):
    p.add_argument("--timeout-min", type=float, default=30.0, help="poll deadline; a timeout keeps the request id for resume")
    p.add_argument("--poll-s", type=float, default=3.0)
    p.add_argument("--pool-wait-min", type=float, default=30.0, help="wait for a provider concurrency slot")
    p.add_argument("--lock-wait-s", type=float, default=30.0)
    p.add_argument("--offline-quote", action="store_true", help="price from providers.json/ceilings only")


def build_parser():
    ap = _Parser(prog="assetgen", description="Game Studio Kit generation CLI", parents=[_common()])
    common = _common(suppress=True)
    ap.add_argument("--version", action="version", version=f"assetgen {__version__}")
    sub = ap.add_subparsers(dest="cmd", parser_class=_Parser)

    p = sub.add_parser("doctor", parents=[common], help="keys (storage only), configs, zero-cost probes, price-sheet age")
    p.add_argument("--offline", action="store_true", help="no provider calls")

    p = sub.add_parser("quote", parents=[common])
    p.add_argument("--provider", required=True)
    p.add_argument("--endpoint", required=True)
    p.add_argument("--n", type=int, default=None, help="outputs per call (default: params num_images, else 1)")
    p.add_argument("--addons", default="")
    p.add_argument("--params-file")
    p.add_argument("--offline", action="store_true")

    p = sub.add_parser("reserve", parents=[common], help="reserve money before an MCP (or manual) call")
    p.add_argument("--provider", required=True)
    p.add_argument("--endpoint", required=True)
    p.add_argument("--attempt", required=True, help="<asset_id>/<stage>/aNN")
    p.add_argument("--n", type=int, default=None)
    p.add_argument("--params-file", help="the call's params (cost-driving ones are priced: resolution, web search, textures…)")
    p.add_argument("--tools", default="", help="comma list of the MCP tools this reservation covers")
    p.add_argument("--lane")
    p.add_argument("--refs", default="", help="path:role,… of files that will be uploaded")
    p.add_argument("--addons", default="")
    p.add_argument("--dry-run", action="store_true")
    p.add_argument("--offline-quote", action="store_true")
    p.add_argument("--lock-wait-s", type=float, default=30.0)

    p = sub.add_parser("receipt", parents=[common], help="record the request id from submit_job (before the first check_job)")
    p.add_argument("reservation_id")
    p.add_argument("--request-id", required=True)

    p = sub.add_parser("settle", parents=[common], help="download allowlisted outputs of an MCP job, check, settle")
    p.add_argument("reservation_id")
    p.add_argument("--result", help="saved get_job_result JSON")
    p.add_argument("--actual", type=float)
    p.add_argument("--actual-source", help="evidence for an --actual below the reserved estimate (e.g. billing events)")
    p.add_argument("--request", help="the submit_job input JSON (its URLs are never outputs)")
    p.add_argument("--output-keys", default="")
    p.add_argument("--mesh-format", default="glb")
    p.add_argument("--check-class")
    p.add_argument("--asset-class")
    p.add_argument("--lock-wait-s", type=float, default=30.0)

    p = sub.add_parser("release", parents=[common])
    p.add_argument("reservation_id")
    p.add_argument("--reason", required=True)
    p.add_argument("--provider-confirmed-no-charge", action="store_true",
                   help="needed when the call may have been billed: the provider shows no job and no charge (logged in the release line)")

    p = sub.add_parser("run", parents=[common], help="REST/CLI path: reserve → receipt → submit → poll → download → checks → settle")
    p.add_argument("--asset", required=True)
    p.add_argument("--stage", required=True)
    p.add_argument("--preset", required=True)
    p.add_argument("--provider")
    p.add_argument("--endpoint")
    p.add_argument("--prompt-file")
    p.add_argument("--refs", default="")
    p.add_argument("--n", type=int, default=None, help="outputs (default: params num_images, else 1)")
    p.add_argument("--params-file")
    p.add_argument("--attempt", help="aNN: resume or create this attempt number")
    p.add_argument("--new-attempt", action="store_true", help="pay for another sample of an input that already settled")
    p.add_argument("--addons", default="")
    p.add_argument("--ref-param")
    p.add_argument("--prompt-param")
    p.add_argument("--lane")
    p.add_argument("--check-class", choices=checks.CLASSES)
    p.add_argument("--asset-class")
    p.add_argument("--mesh-format", default="glb")
    p.add_argument("--dry-run", action="store_true")
    _wait_flags(p)

    p = sub.add_parser("resume", parents=[common], help="resume by request id; never resubmits")
    p.add_argument("attempt_id")
    _wait_flags(p)

    p = sub.add_parser("poll", parents=[common])
    p.add_argument("attempt_id", nargs="?")
    p.add_argument("--all", action="store_true")
    _wait_flags(p)

    p = sub.add_parser("reconcile", parents=[common])
    p.add_argument("--since", default="2d")
    p.add_argument("--usage", action="store_true", help="fal billing events (admin key; human-run only)")
    p.add_argument("--provider", default="")

    p = sub.add_parser("checks", parents=[common])
    p.add_argument("path")
    p.add_argument("--class", dest="cls", required=True, choices=checks.CLASSES)
    p.add_argument("--out")
    for flag in ("--size", "--min-size", "--aspect", "--max-de", "--max-reserved", "--seam-ratio", "--proof", "--min-width",
                 "--halo-max", "--class-budget", "--tris-max", "--tex-max", "--height-range", "--sample-rate", "--bg-std-max", "--pivot-tol-m"):
        p.add_argument(flag)
    for flag in ("--bg-uniform", "--allow-npot", "--allow-metallic", "--skip-pivot"):
        p.add_argument(flag, action="store_true")

    for name in ("accept", "reject"):
        p = sub.add_parser(name, parents=[common])
        p.add_argument("attempt_id")
        p.add_argument("--reason", required=True)
        p.add_argument("--by")
        if name == "accept":
            p.add_argument("--output", default="0", help="output index or 'all'")
            p.add_argument("--waive-checks", action="store_true")

    p = sub.add_parser("asset-set", parents=[common])
    p.add_argument("asset_id")
    p.add_argument("assignment", help="key=value")

    sub.add_parser("report", parents=[common])
    sub.add_parser("budget", parents=[common])

    p = sub.add_parser("fetch-cc0", parents=[common])
    p.add_argument("--source", required=True, choices=["polyhaven", "ambientcg", "kenney"])
    p.add_argument("--id", required=True)
    p.add_argument("--res")
    p.add_argument("--format")
    p.add_argument("--maps", default="")
    p.add_argument("--out")
    p.add_argument("--url")
    p.add_argument("--sha256")
    p.add_argument("--ships", action="store_true")
    p.add_argument("--replace", action="store_true", help="replace an existing file whose content differs (the old one goes to .harness/backups/cc0/)")
    p.add_argument("--dry-run", action="store_true")

    p = sub.add_parser("fetch-refs", parents=[common])
    p.add_argument("--manifest", required=True)
    p.add_argument("--dry-run", action="store_true")
    return ap


CHECK_OPT_KEYS = ("size", "min_size", "aspect", "max_de", "max_reserved", "seam_ratio", "proof", "min_width", "halo_max", "class_budget",
                  "tris_max", "tex_max", "height_range", "sample_rate", "bg_std_max", "pivot_tol_m", "bg_uniform", "allow_npot", "allow_metallic", "skip_pivot")


def dispatch(a, ctx):
    L = ctx.layout
    cmd = a.cmd
    if cmd == "doctor":
        return doctor.doctor(ctx, offline=a.offline)
    if cmd == "quote":
        params = {}
        if a.params_file:
            params = runner.read_input_json(L, a.params_file, "--params-file", runner.PARAMS_MAX)
            if not isinstance(params, dict):
                raise UsageError("--params-file must hold a JSON object")
        from .providers import driver_for, has_driver
        from . import config, rules

        n = rules.quantity(params, a.n)
        drv = None
        if not a.offline and has_driver(a.provider):
            prov = config.provider(ctx.providers, a.provider)
            drv = driver_for(L, a.provider, prov, config.find_endpoint(ctx.providers, a.provider, a.endpoint), a.endpoint)
        q = budget.quote(ctx, a.provider, a.endpoint, n, params, [s for s in a.addons.split(",") if s], offline=a.offline, driver=drv)
        return dict(q, ok=True)
    if cmd == "reserve":
        return runner.mcp_reserve(ctx, a)
    if cmd == "receipt":
        return runner.mcp_receipt(ctx, a.reservation_id, a.request_id)
    if cmd == "settle":
        return runner.mcp_settle(ctx, a, a.reservation_id)
    if cmd == "release":
        return runner.release_reservation(ctx, a.reservation_id, a.reason, provider_confirmed=a.provider_confirmed_no_charge)
    if cmd == "run":
        return runner.run(ctx, a)
    if cmd == "resume":
        parse_attempt_id(a.attempt_id)
        return runner.resume(ctx, a, a.attempt_id)
    if cmd == "poll":
        if a.all:
            results, worst = [], EXIT_OK
            for aid in runner.resumable_attempts(L):
                try:
                    r = runner.resume(ctx, a, aid)
                    results.append({"attempt_id": aid, "ok": r["ok"], "state": r["state"]})
                    worst = max(worst, EXIT_OK if r["ok"] else EXIT_FAIL)
                except AssetgenError as e:
                    results.append({"attempt_id": aid, "ok": False, "exit": e.exit_code, "error": e.message, "reason": e.reason})
                    worst = max(worst, e.exit_code if e.exit_code != 75 else EXIT_INFRA)
            return {"ok": worst == EXIT_OK, "attempts": results, "_exit": worst}
        if not a.attempt_id:
            raise UsageError("poll needs <attempt_id> or --all")
        return runner.resume(ctx, a, a.attempt_id)
    if cmd == "reconcile":
        only = {p for p in a.provider.split(",") if p}
        return reconcile.reconcile(ctx, parse_since(a.since), usage=a.usage, only=only or None)
    if cmd == "checks":
        opts = {k: getattr(a, k) for k in CHECK_OPT_KEYS if getattr(a, k, None) not in (None, False)}
        style = None
        try:
            style = ctx.style_lock
        except AssetgenError:
            style = None
        target = None
        if a.out:
            # Inside W, outside kit and harness files, and never over a file that is not an earlier checks result.
            target = L.output_target(a.out, "--out")
            if target.exists():
                try:
                    old = read_json(target) if target.is_file() else None
                except Exception:
                    old = None
                if not (isinstance(old, dict) and {"ok", "class", "failures"} <= set(old)):
                    raise Refused(f"--out {a.out} exists and is not an asset checks result; nothing was overwritten", reason="exists_differs")
        res = checks.run_checks(a.cls, a.path, opts, style)
        if target is not None:
            write_json_atomic(target, res)
        return res
    if cmd == "accept":
        parse_attempt_id(a.attempt_id)
        return runner.accept(ctx, a, a.attempt_id)
    if cmd == "reject":
        parse_attempt_id(a.attempt_id)
        return runner.reject(ctx, a, a.attempt_id)
    if cmd == "asset-set":
        if "=" not in a.assignment:
            raise UsageError("asset-set takes key=value")
        key, value = a.assignment.split("=", 1)
        with ctx.ledger.lock("assetgen asset-set"):
            old, new = assetmod.set_key(L, a.asset_id, key.strip(), value)
        return {"ok": True, "asset_id": a.asset_id, "key": key.strip(), "old": old, "new": new}
    if cmd == "report":
        return report.report(ctx)
    if cmd == "budget":
        return report.budget_view(ctx)
    if cmd == "fetch-cc0":
        return cc0.fetch_cc0(ctx, a)
    if cmd == "fetch-refs":
        return cc0.fetch_refs(ctx, a)
    raise UsageError("a command is required (doctor, quote, reserve, receipt, settle, release, run, resume, poll, reconcile, checks, accept, reject, asset-set, report, budget, fetch-cc0, fetch-refs)")


def _emit(obj, as_json, stream=None):
    stream = stream or sys.stdout
    if as_json:
        stream.write(json.dumps(obj, ensure_ascii=True, separators=(",", ":"), default=str) + "\n")
    else:
        stream.write(json.dumps(obj, ensure_ascii=False, indent=2, default=str) + "\n")
    stream.flush()


def main(argv=None):
    for s in (sys.stdout, sys.stderr):
        try:
            s.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    argv = list(sys.argv[1:] if argv is None else argv)
    as_json = "--json" in argv
    try:
        a = build_parser().parse_args(argv)
        if not a.cmd:
            raise UsageError("a command is required; see assetgen --help")
        layout = Layout(find_workspace(a.workspace), providers_override=a.providers)
        schemas.bind(layout)
        ctx = budget.Ctx(layout)
        result = dispatch(a, ctx)
        code = result.pop("_exit", None)
        if code is None:
            code = EXIT_OK if result.get("ok", True) else EXIT_FAIL
        _emit(result, as_json)
        return code
    except AssetgenError as e:
        out = {"ok": False, "exit": e.exit_code, "error": e.message}
        if e.reason:
            out["reason"] = e.reason
        out.update({k: v for k, v in (e.result or {}).items() if k not in out})
        if as_json:
            _emit(out, True)
        else:
            sys.stderr.write(f"assetgen: {e.message}\n")
        return e.exit_code
    except KeyboardInterrupt:
        return EXIT_INFRA
    except SystemExit as e:  # argparse --help / --version
        return int(e.code or 0)
    except Exception as e:  # never a traceback with secrets; report the type and message only
        out = {"ok": False, "exit": EXIT_INFRA, "error": f"internal error: {type(e).__name__}: {str(e)[:300]}"}
        if as_json:
            _emit(out, True)
        else:
            sys.stderr.write(f"assetgen: {out['error']}\n")
        return EXIT_INFRA


if __name__ == "__main__":
    sys.exit(main())
