"""Fixed-support repair in Blender metres; no Blender dependency or material edits."""
import itertools
import math

OLD_Y = 0.072
NEW_Y = 0.108
BOTTOM_Z = 0.463
TOP_Z = 0.560
PIVOT = (0.0, OLD_Y, TOP_Z)


def shift_crossbar(point):
    x, y, z = point
    return x, y + NEW_Y - OLD_Y, z


def aim_support(point):
    x, y, z = point
    height = TOP_Z - BOTTOM_Z
    offset = NEW_Y - OLD_Y
    length = math.hypot(height, offset)
    t = (z - BOTTOM_Z) / height
    radial_y = y - OLD_Y
    return (x, NEW_Y - offset * t + radial_y * height / length,
            z + radial_y * offset / length)


def affine_rows(operation):
    origin = operation((0, 0, 0))
    basis = [operation(tuple(1 if i == j else 0 for i in range(3))) for j in range(3)]
    return tuple(tuple(basis[j][i] - origin[i] for j in range(3)) + (origin[i],) for i in range(3)) + ((0, 0, 0, 1),)


def sinusoid_bounds(a, b, lo, hi):
    candidates = [lo, hi]
    stationary = math.atan2(b, a)
    first = math.ceil((lo - stationary) / math.pi)
    last = math.floor((hi - stationary) / math.pi)
    candidates.extend(stationary + k * math.pi for k in range(first, last + 1))
    values = [a * math.cos(angle) + b * math.sin(angle) for angle in candidates]
    return min(values), max(values)


def swept_box(bounds, minimum_degrees=-10, maximum_degrees=10):
    if minimum_degrees > maximum_degrees:
        raise ValueError("Reversed tilt range")
    low, high = bounds
    if any(low[i] > high[i] for i in range(3)):
        raise ValueError("Reversed source bounds")
    lo, hi = math.radians(minimum_degrees), math.radians(maximum_degrees)
    y_values, z_values = [], []
    for y, z in itertools.product((low[1], high[1]), (low[2], high[2])):
        dy, dz = y - PIVOT[1], z - PIVOT[2]
        y_values.extend(v + PIVOT[1] for v in sinusoid_bounds(dy, -dz, lo, hi))
        z_values.extend(v + PIVOT[2] for v in sinusoid_bounds(dz, dy, lo, hi))
    return ((low[0], min(y_values), min(z_values)), (high[0], max(y_values), max(z_values)))
