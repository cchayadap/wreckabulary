using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class StairsTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            Match.ModeOverride = "Dibs";
            Session.SelectMap("pinwheel");
            var room = new GameObject("Stairs test house").AddComponent<RoomBuilder>();
            foreach (var prop in room.Originals) prop.gameObject.SetActive(false);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static PlayerController Player(ScriptedBinding input, Vector3 at)
        {
            var p = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            p.Setup(0, input);
            var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
            p.Health.UseRules(rules);
            p.Inventory.Collects = false;
            p.Respawn(at);
            return p;
        }

        [UnityTest]
        public IEnumerator APlayerWalksUpAndDownThePinwheelRamp()
        {
            var input = new ScriptedBinding();
            var p = Player(input, new Vector3(3.25f, 0f, -3f));
            yield return new WaitForSeconds(.3f);

            float peak = float.MinValue;
            input.Next.move = Vector2.up;
            float until = Time.time + 4f;
            while (p.transform.position.z < 2.4f && Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, p.transform.position.y);
            }
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(.4f);
            var top = p.transform.position;
            Debug.Log($"RAMP_UP z {top.z:F2} y {top.y:F3} peak {peak:F3}");

            int onRamp = 0, airborne = 0;
            input.Next.move = Vector2.down;
            until = Time.time + 4f;
            while (p.transform.position.z > -2.5f && Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                float z = p.transform.position.z;
                if (z > -.9f && z < .9f) { onRamp++; if (!p.Grounded) airborne++; }
            }
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(.4f);
            var bottom = p.transform.position;
            Debug.Log($"RAMP_DOWN z {bottom.z:F2} y {bottom.y:F3} ramp steps {onRamp} airborne {airborne}");
            Assert.Greater(top.z, 2.3f, "walked up onto the balcony");
            Assert.AreEqual(1.7f, top.y, .2f, "stands on the balcony");
            Assert.Less(peak, 1.9f, "no launch off the top of the ramp (it reached 2.29 before walking stuck to slopes)");
            Assert.Less(bottom.z, -2.4f, "walked back down to the floor");
            Assert.AreEqual(0f, bottom.y, .2f, "stands on the floor");
            Assert.Greater(onRamp, 0, "came down the ramp, not off the balcony's edge");
            Assert.LessOrEqual(airborne, onRamp / 4, "the feet stay on the ramp going down");
        }
    }
}
