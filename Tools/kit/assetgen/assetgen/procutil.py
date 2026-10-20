"""Child processes with argv lists only (never a shell string), a timeout that kills the whole tree by PID, and the
Windows .cmd/.bat rule: cmd.exe cuts an argument at a newline and re-parses quotes, so arguments with CR, LF or '"'
are refused for such targets (WoO: the Higgsfield .cmd shim cut multi-line prompts at the first newline)."""

import os
import subprocess

from .errors import InfraError

_IS_WIN = os.name == "nt"


def is_batch(path):
    return str(path).lower().endswith((".cmd", ".bat"))


# Python's list2cmdline quotes for the C runtime, not for cmd.exe, so cmd metacharacters stay live inside a .cmd call.
CMD_UNSAFE = set('"\r\n%!^&|<>()')


def check_args(argv):
    if argv and is_batch(argv[0]):
        for a in argv[1:]:
            if any(c in CMD_UNSAFE for c in str(a)):
                raise InfraError(
                    "EUNSAFEARG: refusing to pass a multi-line or quoted argument through a .cmd/.bat launcher; point "
                    "GSK_HIGGSFIELD_CLI at the package's bin/*.js so node receives the argv directly",
                    reason="unsafe_arg",
                )


def tree_kill(pid):
    try:
        if _IS_WIN:
            subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30)
        else:
            import signal

            os.killpg(os.getpgid(pid), signal.SIGKILL)
    except Exception:
        pass


def run_cmd(argv, timeout=120, cwd=None, input_text=None, env=None):
    """(exit_code, stdout, stderr, timed_out). exit_code is None when the program could not start."""
    argv = [str(a) for a in argv]
    check_args(argv)
    kwargs = {}
    if _IS_WIN:
        kwargs["creationflags"] = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    else:
        kwargs["start_new_session"] = True
    try:
        proc = subprocess.Popen(
            argv,
            stdin=subprocess.PIPE if input_text is not None else subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            cwd=cwd,
            env=env,
            **kwargs,
        )
    except OSError as e:
        return None, "", f"{type(e).__name__}: {e}", False
    try:
        out, err = proc.communicate(input=input_text.encode("utf-8") if input_text is not None else None, timeout=timeout)
        timed_out = False
    except subprocess.TimeoutExpired:
        tree_kill(proc.pid)
        try:
            out, err = proc.communicate(timeout=10)
        except Exception:
            out, err = b"", b""
        timed_out = True
    return proc.returncode, out.decode("utf-8", "replace"), err.decode("utf-8", "replace"), timed_out
