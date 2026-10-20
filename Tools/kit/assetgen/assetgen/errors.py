"""Exceptions that carry the CLI exit code (CONTRACTS.md §1.4 and §4.6)."""

EXIT_OK = 0
EXIT_FAIL = 1
EXIT_USAGE = 2
EXIT_INFRA = 3
EXIT_BLOCKED = 75


class AssetgenError(Exception):
    exit_code = EXIT_FAIL

    def __init__(self, message, *, reason=None, result=None):
        super().__init__(message)
        self.message = message
        self.reason = reason
        self.result = result or {}


class Refused(AssetgenError):
    """A policy, cap, licence or check said no (exit 1)."""

    exit_code = EXIT_FAIL


class UsageError(AssetgenError):
    exit_code = EXIT_USAGE


class InfraError(AssetgenError):
    """Missing key, tool, file or network; provider down; submission state unknown (exit 3)."""

    exit_code = EXIT_INFRA


class Blocked(AssetgenError):
    """A lock or pool slot could not be taken in time (exit 75)."""

    exit_code = EXIT_BLOCKED
