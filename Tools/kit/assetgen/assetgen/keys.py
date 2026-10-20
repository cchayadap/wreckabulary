"""Provider keys: Windows Credential Manager through Python keyring first, then the environment (OWNER_DEFAULTS C-19).

Only presence and the storage method ('keyring' | 'env' | 'missing') are ever reported; values are never printed,
logged or written. In test mode (GSK_ASSETGEN_URL_MAP set) keyring is skipped and only values starting with 'test-' are
used, so the test URL map can never carry a real key to a local listener.
"""

import os

from . import net

SERVICE = "game-studio-kit"


def _strip(v):
    v = (v or "").strip()
    for prefix in ("Bearer ", "Key ", "bearer ", "key "):
        if v.startswith(prefix):
            v = v[len(prefix):].strip()
    return v


def _from_keyring(service, username):
    if os.environ.get("GSK_ASSETGEN_NO_KEYRING") == "1":
        return None
    try:
        import keyring

        return keyring.get_password(service, username)
    except Exception:
        return None


def lookup(env_name, keyring_spec=None):
    """(value or None, storage)."""
    if not env_name:
        return None, "missing"
    test_mode = net.test_mode()
    if not test_mode:
        spec = keyring_spec or {}
        v = _from_keyring(spec.get("service", SERVICE), spec.get("username", env_name))
        if v and _strip(v):
            return _strip(v), "keyring"
    v = _strip(os.environ.get(env_name))
    if v:
        if test_mode and not v.startswith("test-"):
            return None, "missing"
        return v, "env"
    return None, "missing"


def storage_of(env_name, keyring_spec=None):
    return lookup(env_name, keyring_spec)[1]


def provider_key(prov_cfg):
    auth = prov_cfg.get("auth") or {}
    return lookup(auth.get("env"), auth.get("keyring"))


def human_step(env_name):
    return (
        f"Human step: store {env_name} in Windows Credential Manager (service '{SERVICE}', user '{env_name}'), e.g. "
        f"`uv run --project Tools/kit/assetgen python -c \"import keyring,getpass; keyring.set_password('{SERVICE}','{env_name}',getpass.getpass())\"`, "
        f"or `setx {env_name} <key>` in your own terminal. Never paste a key into the chat."
    )
