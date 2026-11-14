"""Stdlib checks for belt_geometry: python -I test_belt_geometry.py (run from this folder)."""
import math
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import belt_geometry as g  # noqa: E402


class BeltGeometryTests(unittest.TestCase):
    def test_section_extents_match_strap(self):
        pts = g.rounded_rect(g.STRAP_W, g.STRAP_T, g.STRAP_R)
        self.assertEqual(16, len(pts))
        self.assertAlmostEqual(g.STRAP_W / 2, max(p[0] for p in pts), places=9)
        self.assertAlmostEqual(-g.STRAP_T / 2, min(p[1] for p in pts), places=9)

    def test_section_u_is_continuous_and_in_strap_tile(self):
        pts = g.rounded_rect(g.STRAP_W, g.STRAP_T, g.STRAP_R)
        us = [g.section_u(x, a) for x, _, a in pts]
        for u in us:
            self.assertTrue(0.0 <= u <= 0.5)
        for a, b in zip(us, us[1:] + us[:1]):
            self.assertLessEqual(abs(a - b), 0.39 + 1e-9)
        self.assertAlmostEqual(0.44, g.section_u(pts[3][0], pts[3][2]))
        self.assertAlmostEqual(0.06, g.section_u(pts[11][0], pts[11][2]))

    def test_strap_section_adds_flat_face_strips(self):
        pts = g.strap_section()
        self.assertEqual(20, len(pts))
        us = [g.section_u(x, a) for x, _, a in pts]
        for a, b in zip(us, us[1:] + us[:1]):
            self.assertLessEqual(abs(a - b), 0.2 + 1e-9)
        front = sorted(x for x, y, _ in pts if y <= -g.STRAP_T / 2 + 1e-9)
        self.assertEqual(4, len(front))
        self.assertLess(front[1] + g.HOLE_D / 2, 0)

    def test_weights_sum_to_one_with_two_influences(self):
        z = 0.0
        while z <= g.SIZE[1]:
            w = g.weights_at(z)
            self.assertLessEqual(len(w), 2)
            self.assertAlmostEqual(1.0, sum(w.values()), places=9)
            z += 0.0025
        self.assertEqual({"Belt_Root": 1.0}, g.weights_at(0.1))
        self.assertEqual({"Belt_Tip": 1.0}, g.weights_at(1.15))

    def test_joints_are_far_enough_apart_for_their_blend(self):
        js = g.joints()
        for a, b in zip(js, js[1:]):
            self.assertGreater(b - a, 2 * g.JOINT_BLEND)

    def test_strap_path_is_continuous_and_folds_round_the_bar(self):
        pts = g.strap_path()
        for a, b in zip(pts, pts[1:]):
            self.assertLess(math.hypot(b[0] - a[0], b[1] - a[1]), 0.03)
        top = max(p[1] for p in pts)
        self.assertAlmostEqual(g.BAR_Z + g.FOLD_R, top, places=6)
        self.assertAlmostEqual(g.FOLD_R, pts[-1][0], places=9)
        self.assertLess(top + g.STRAP_T / 2, g.FRAME_Z1 - 2 * g.FRAME_TUBE, "the fold leaves part of the buckle window open")

    def test_depth_fits_catalogue_size(self):
        self.assertLessEqual(g.FOLD_R + g.STRAP_T / 2, g.SIZE[2] / 2 + 1e-9)

    def test_bend_share_is_tip_weighted_and_within_limits(self):
        a = g.bend_angles(g.TOTAL_LIMIT_DEG)
        self.assertAlmostEqual(g.TOTAL_LIMIT_DEG, sum(a), places=9)
        self.assertEqual(sorted(a), a)
        self.assertLessEqual(max(a), 2 * g.JOINT_LIMIT_DEG)


if __name__ == "__main__":
    unittest.main()
