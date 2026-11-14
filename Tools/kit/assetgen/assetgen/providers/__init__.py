"""Provider drivers for the REST and CLI paths. MCP-only providers (Scenario, Higgsfield MCP) have no driver: they use
reserve → MCP call → receipt → settle (CONTRACTS.md §4.6 MCP path protocol)."""

from ..errors import InfraError

DRIVERS = ("fal", "meshy", "higgsfield", "tripo")


def driver_for(layout, pid, prov_cfg, ep_cfg, endpoint):
    if pid == "fal":
        from .fal import FalDriver

        return FalDriver(layout, pid, prov_cfg, ep_cfg, endpoint)
    if pid == "meshy":
        from .meshy import MeshyDriver

        return MeshyDriver(layout, pid, prov_cfg, ep_cfg, endpoint)
    if pid == "higgsfield":
        from .higgsfield import HiggsfieldDriver

        return HiggsfieldDriver(layout, pid, prov_cfg, ep_cfg, endpoint)
    if pid == "tripo":
        from .tripo import TripoDriver

        return TripoDriver(layout, pid, prov_cfg, ep_cfg, endpoint)
    raise InfraError(
        f"assetgen has no REST or CLI driver for '{pid}'. Use the MCP path: assetgen reserve --tools <mcp tool> → the MCP "
        f"call → assetgen receipt → assetgen settle --result <file>",
        reason="no_driver",
    )


def has_driver(pid):
    return pid in DRIVERS
