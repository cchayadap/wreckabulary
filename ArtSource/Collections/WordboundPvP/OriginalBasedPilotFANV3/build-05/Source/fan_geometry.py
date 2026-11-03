"""Small, engine-free conservative clearance calculations in Blender metres."""
import math


def segment_radius(a, b):
    dx, dz = b[0] - a[0], b[2] - a[2]
    length = dx * dx + dz * dz
    t = max(0.0, min(1.0, -(a[0] * dx + a[2] * dz) / length)) if length else 0.0
    return math.hypot(a[0] + t * dx, a[2] + t * dz)


def radial_bounds(triangle, center_z=0.56):
    points = [(p[0], p[1], p[2] - center_z) for p in triangle]
    cross = [a[0] * b[2] - a[2] * b[0] for a, b in zip(points, points[1:] + points[:1])]
    area = abs(sum(cross))
    contains_axis = area > 1e-15 and (min(cross) >= 0 or max(cross) <= 0)
    low = 0.0 if contains_axis else min(segment_radius(a, b) for a, b in zip(points, points[1:] + points[:1]))
    return low, max(math.hypot(p[0], p[2]) for p in points)


def interval_gap(a_low, a_high, b_low, b_high):
    return max(0.0, a_low - b_high, b_low - a_high)


def cylinder_triangle_lower_bound(radius, y_low, y_high, triangle):
    return cylinder_bounds_lower_bound(radius, y_low, y_high, triangle_cylindrical_bounds(triangle))


def triangle_cylindrical_bounds(triangle):
    radial_low, radial_high = radial_bounds(triangle)
    return radial_low, radial_high, min(p[1] for p in triangle), max(p[1] for p in triangle)


def cylinder_bounds_lower_bound(radius, y_low, y_high, triangle_bounds):
    radial_low, _, low, high = triangle_bounds
    return math.hypot(max(0.0, radial_low - radius), interval_gap(y_low, y_high, low, high))


def swept_axial_profile(triangles, slabs=128):
    low = min(p[1] for triangle in triangles for p in triangle)
    high = max(p[1] for triangle in triangles for p in triangle)
    if high <= low or not 2 <= slabs <= 256:
        raise ValueError("Invalid rotor profile extent/slab count")
    result = []
    for index in range(slabs):
        start, end = low + (high - low) * index / slabs, low + (high - low) * (index + 1) / slabs
        intersecting = [triangle for triangle in triangles
                        if min(p[1] for p in triangle) <= end and max(p[1] for p in triangle) >= start]
        if intersecting:
            result.append((max(radial_bounds(triangle)[1] for triangle in intersecting), start, end))
    return result
