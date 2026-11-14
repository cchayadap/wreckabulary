"""Finite world-vertex envelope and boundary-owner diagnostics; no Blender import."""
import math

BOUND_TOLERANCE = 0.00001
OWNER_EPSILON = 0.0000001
CONTAINMENT_TOLERANCE = 0.000001


def checked_points(points):
    result = [tuple(float(value) for value in point) for point in points]
    if not result or any(len(point) != 3 or not all(math.isfinite(v) for v in point) for point in result):
        raise ValueError("Nonempty finite three-dimensional vertices required")
    return result


def bounds(points):
    vertices = checked_points(points)
    return [[min(point[axis] for point in vertices) for axis in range(3)],
            [max(point[axis] for point in vertices) for axis in range(3)]]


def analyze_parts(parts):
    if not parts:
        raise ValueError("At least one named mesh is required")
    vertices = {name: checked_points(points) for name, points in parts.items()}
    if any(not isinstance(name, str) or not name for name in vertices):
        raise ValueError("Distinct nonempty mesh names required")
    envelope = bounds(point for points in vertices.values() for point in points)
    witnesses = []
    protected = set()
    for side in range(2):
        for axis in range(3):
            owners = []
            for name, points in vertices.items():
                index = (min if side == 0 else max)(range(len(points)), key=lambda i: points[i][axis])
                value = points[index][axis]
                if abs(value - envelope[side][axis]) <= OWNER_EPSILON:
                    owners.append({"mesh": name, "vertex_index": index, "point_world_metres": list(points[index]),
                                   "axis_value": value})
                    protected.add(name)
            if not owners:
                raise ValueError("An envelope face has no vertex witness")
            witnesses.append({"side": "min" if side == 0 else "max", "axis": "xyz"[axis],
                              "value": envelope[side][axis], "owners": owners})
    return {"bounds": envelope, "protected_boundary_meshes": sorted(protected), "witnesses": witnesses,
            "owner_epsilon_metres": OWNER_EPSILON}


def first_outside(points, envelope):
    checked = checked_points(envelope)
    if len(checked) != 2 or any(checked[0][axis] > checked[1][axis] for axis in range(3)):
        raise ValueError("Ordered minimum and maximum required")
    for index, point in enumerate(checked_points(points)):
        for axis in range(3):
            if point[axis] < checked[0][axis] - CONTAINMENT_TOLERANCE or point[axis] > checked[1][axis] + CONTAINMENT_TOLERANCE:
                return {"vertex_index": index, "axis": "xyz"[axis], "point_world_metres": list(point)}
    return None


def comparison(actual, expected):
    rows = [{"side": "min" if side == 0 else "max", "axis": "xyz"[axis],
             "expected": expected[side][axis], "actual": actual[side][axis],
             "delta": actual[side][axis] - expected[side][axis]}
            for side in range(2) for axis in range(3)]
    return {"axes": rows, "maximum_error_metres": max(abs(row["delta"]) for row in rows),
            "tolerance_metres": BOUND_TOLERANCE}
