"""assetgen: the Game Studio Kit generation CLI (CONTRACTS.md §4.6, ARCHITECTURE §3.7).

Every paid call goes through one append-only ledger with a reservation taken under a lock before submit, a receipt
written before the first poll, allowlisted outputs named by magic bytes, and a settle line. Nothing here ever prints a
key value.
"""

__version__ = "1.0.0"
USER_AGENT = f"game-studio-kit-assetgen/{__version__}"
