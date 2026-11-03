import json
import math
import struct


GLTF_TO_BLENDER = [[1, 0, 0, 0], [0, 0, -1, 0], [0, 1, 0, 0], [0, 0, 0, 1]]


def multiply(a, b):
    return [[sum(a[row][k] * b[k][column] for k in range(4)) for column in range(4)] for row in range(4)]


def identity():
    return [[float(row == column) for column in range(4)] for row in range(4)]


def numbers(values, count):
    if not isinstance(values, (list, tuple)) or len(values) != count or any(
            isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) for value in values):
        raise ValueError("Expected finite numeric coordinates")
    return list(values)


def matrix(value):
    if not isinstance(value, (list, tuple)) or len(value) != 4:
        raise ValueError("Expected four matrix rows")
    rows = [numbers(row, 4) for row in value]
    if rows[3] != [0, 0, 0, 1]:
        raise ValueError("Expected an affine transform")
    return rows


def read_glb(data):
    if len(data) < 20 or data[:4] != b"glTF" or struct.unpack_from("<I", data, 4)[0] != 2:
        raise ValueError("Expected GLB version 2")
    if struct.unpack_from("<I", data, 8)[0] != len(data):
        raise ValueError("GLB declared byte length differs")
    length, kind = struct.unpack_from("<II", data, 12)
    if kind != 0x4E4F534A or length == 0 or 20 + length > len(data):
        raise ValueError("Expected the bounded first JSON chunk")
    return json.loads(data[20:20 + length].decode("utf-8"))


def local_matrix(node):
    if "matrix" in node:
        if any(key in node for key in ("translation", "rotation", "scale")):
            raise ValueError("Node cannot mix matrix and TRS")
        values = numbers(node["matrix"], 16)
        return matrix([[values[column * 4 + row] for column in range(4)] for row in range(4)])
    translation = numbers(node.get("translation", [0, 0, 0]), 3)
    scale = numbers(node.get("scale", [1, 1, 1]), 3)
    if min(scale) <= 0:
        raise ValueError("Grip ancestry requires positive scale")
    q = numbers(node.get("rotation", [0, 0, 0, 1]), 4)
    norm = math.sqrt(sum(value * value for value in q))
    if abs(norm - 1) > 0.000001:
        raise ValueError("Expected a unit source quaternion")
    x, y, z, w = [value / norm for value in q]
    rotation = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
    result = identity()
    for row in range(3):
        for column in range(3):
            result[row][column] = rotation[row][column] * scale[column]
        result[row][3] = translation[row]
    return result


def world_matrix(document, name):
    nodes = document.get("nodes", [])
    if not isinstance(nodes, list) or not 1 <= len(nodes) <= 256 or any(not isinstance(node, dict) for node in nodes):
        raise ValueError("Expected a bounded node graph")
    selected = [i for i, node in enumerate(nodes) if node.get("name") == name]
    if len(selected) != 1:
        raise ValueError("Expected one exact named grip")
    parents = {}
    for index, node in enumerate(nodes):
        for child in node.get("children", []):
            if type(child) is not int or not 0 <= child < len(nodes) or child in parents:
                raise ValueError("Invalid or multiply parented node")
            parents[child] = index
    chain, current = [], selected[0]
    while True:
        if current in chain:
            raise ValueError("Cyclic grip ancestry")
        chain.append(current)
        if current not in parents:
            break
        current = parents[current]
    scenes = document.get("scenes", [])
    scene = document.get("scene", 0)
    if type(scene) is not int or not 0 <= scene < len(scenes) or chain[-1] not in scenes[scene].get("nodes", []):
        raise ValueError("Grip is not reachable in the default scene")
    result = identity()
    for index in reversed(chain):
        result = multiply(result, local_matrix(nodes[index]))
    return matrix(result), [nodes[index].get("name") for index in reversed(chain)]


def to_blender(value):
    inverse = [list(row) for row in zip(*GLTF_TO_BLENDER)]
    return multiply(multiply(GLTF_TO_BLENDER, matrix(value)), inverse)


def float32_matrix(value):
    return [[struct.unpack("<f", struct.pack("<f", component))[0] for component in row] for row in matrix(value)]


def check_matrix(actual, expected, tolerance=0.0):
    actual, expected = matrix(actual), matrix(expected)
    if not math.isfinite(tolerance) or tolerance < 0:
        raise ValueError("Invalid comparison tolerance")
    translation = max(abs(actual[row][3] - expected[row][3]) for row in range(3))
    basis = max(abs(actual[row][column] - expected[row][column]) for row in range(3) for column in range(3))
    if max(translation, basis) > tolerance:
        raise ValueError("Grip differs from the true original matrix: translation=" + str(translation) + "; basis=" + str(basis))
    return {"translation_max_error_metres": translation, "basis_max_error": basis, "tolerance": tolerance,
            "actual_world_matrix": actual, "expected_world_matrix": expected}


def original_contract(glb_bytes, catalogue, v1_receipt):
    original, ancestry = world_matrix(read_glb(glb_bytes), "Grip_R")
    if ancestry != ["Wreckabulary_ExportRoot", "FAN", "Grip_R"]:
        raise ValueError("Original FAN grip ancestry changed")
    analytic = to_blender(original)
    converted = float32_matrix(v1_receipt["grip_world_matrix_blender"])
    check_matrix(converted, v1_receipt["grip_world_matrix_blender"])
    comparison = check_matrix(converted, analytic, 0.000000000001)
    items = [item for item in catalogue["items"] if item.get("id") == "FAN"]
    if len(items) != 1 or items[0].get("model") != "Items/FAN":
        raise ValueError("Expected one canonical FAN definition")
    item = items[0]
    contact = numbers(item["grip"], 3)
    error = max(abs(original[axis][3] - contact[axis]) for axis in range(3))
    if error > 0.000001 or item["heldScale"] != v1_receipt["held_scale_from_original_config"]:
        raise ValueError("Original grip/held scale differs from the canonical mount")
    return {"source_ancestry": ancestry, "gltf_world_matrix": original,
            "blender_world_matrix_float32": converted, "basis": "X,Y,Z -> X,-Z,Y; metres; C*M*C^-1",
            "v1_receipt_native_matrix_verified": True, "analytic_source_comparison": comparison,
            "catalogue_grip_unity_metres": contact,
            "catalogue_contact_max_error_metres": error, "held_scale": item["heldScale"],
            "size_unity_metres": numbers(item["size"], 3),
            "limit": "Source coordinate/mount proof; actual Unity import, socket orientation and held rendering remain unverified."}
