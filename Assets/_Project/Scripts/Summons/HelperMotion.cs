using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class HelperMotion : MonoBehaviour
    {
        public const float BobMetres = .02f, BobHertz = 1.5f, LookYaw = 15f, LookHertz = .4f;
        public const float WalkRoll = 6f, WalkHertz = 4f, WalkBob = .03f;
        public const float HopHeight = .3f, HopSeconds = .45f;
        public const float WindLean = 12f, WindShake = 2f, WindShakeHertz = 18f, WindSquash = .9f;
        public const float LungeMetres = .25f, LungeSeconds = .1f;
        public const float HurtSeconds = .25f, HurtWobble = 10f;
        public const float ArmBounceHeight = .06f;
        public const float TrapRest = .4f, TrapRiseSeconds = .1f;
        public const float RingHeight = .03f;
        public const float FlagGap = .04f, FlagPole = .36f, FlagTall = .2f, FlagLong = .28f, FlagPoleWidth = .025f;
        public const string FlagName = "Helper flag", PennantName = "Helper pennant";

        HelperAgent agent;
        Transform pose, flag;
        Vector3 flagRest, flagScale;
        Mesh pennant;
        Vector3 restPosition, restScale;
        Quaternion restRotation;
        float struckAt = float.NegativeInfinity, hurtAt = float.NegativeInfinity, endedAt = -1f, seed;

        public Transform Pose => pose;
        public Transform Flag => flag;

        public void Configure(HelperAgent owner, Color ringColor, float ringRadius, bool flagged = false)
        {
            agent = owner;
            seed = Random.value * 10f;
            pose = new GameObject("Helper pose").transform;
            pose.SetParent(transform, false);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != pose) child.SetParent(pose, false);
            }
            restPosition = pose.localPosition;
            restRotation = pose.localRotation;
            restScale = pose.localScale;
            if (ringRadius > 0f)
            {
                var ring = SummonEffects.Ring(transform, "Helper ring", ringColor, ringRadius, Quaternion.Euler(90f, 0f, 0f));
                ring.transform.localPosition = Vector3.up * RingHeight;
                ring.sharedMaterial = PlayerHud.Mark(ringColor);
            }
            if (flagged) RaiseFlag(ringColor);
        }

        void RaiseFlag(Color colour)
        {
            float scale = Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.y)), top = 0f;
            foreach (var renderer in pose.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, renderer.bounds.max.y - transform.position.y);
            flag = new GameObject(FlagName).transform;
            flag.SetParent(transform, false);
            flag.localPosition = flagRest = Vector3.up * ((top + FlagGap) / scale);
            flag.localScale = flagScale = Vector3.one / scale;
            var pole = SummonEffects.Line(flag, Color.white, new[] { Vector3.zero, Vector3.up * FlagPole });
            pole.startWidth = pole.endWidth = FlagPoleWidth;
            pole.sharedMaterial = PlayerHud.Mark(Color.white);
            pennant = new Mesh { name = PennantName };
            Vector3 high = new Vector3(0f, FlagPole, 0f), low = new Vector3(0f, FlagPole - FlagTall, 0f), tip = new Vector3(FlagLong, FlagPole - FlagTall * .5f, 0f);
            pennant.vertices = new[] { high, low, tip, high, low, tip };
            pennant.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            pennant.RecalculateNormals();
            pennant.RecalculateBounds();
            var cloth = new GameObject(PennantName);
            cloth.transform.SetParent(flag, false);
            cloth.AddComponent<MeshFilter>().sharedMesh = pennant;
            var drawn = cloth.AddComponent<MeshRenderer>();
            drawn.sharedMaterial = PlayerHud.Mark(colour);
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            drawn.receiveShadows = false;
        }

        void OnDestroy()
        {
            if (pennant) Destroy(pennant);
        }

        public void Strike() => struckAt = Time.time;

        public void Hurt() => hurtAt = Time.time;

        public void End() => endedAt = Time.time;

        void LateUpdate()
        {
            if (!agent || !pose || agent.Spec == null) return;
            float t = Time.time + seed;
            var spec = agent.Spec;
            var position = restPosition;
            var rotation = restRotation;
            var scale = restScale;
            if (endedAt >= 0f)
            {
                float k = Mathf.Clamp01((Time.time - endedAt) / HelperAgent.EndSeconds);
                pose.localPosition = position;
                pose.localRotation = rotation * Quaternion.Euler(0f, k * 360f, 0f);
                pose.localScale = scale * (1f - k);
                PlaceFlag(0f, 1f - k);
                return;
            }
            var phase = agent.Phase;
            if (spec.IsTrap)
            {
                bool snapping = phase == HelperPhase.WindUp || Time.time - struckAt < TrapRiseSeconds * 3f;
                float y = snapping ? 1f : TrapRest;
                scale = new Vector3(scale.x, scale.y * y, scale.z);
            }
            else if (phase == HelperPhase.Arming)
            {
                position.y += Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f / (HelperBrain.ArmSeconds * .5f))) * ArmBounceHeight;
            }
            else if (phase == HelperPhase.WindUp)
            {
                rotation *= Quaternion.Euler(-WindLean, Mathf.Sin(t * WindShakeHertz * Mathf.PI * 2f) * WindShake, 0f);
                scale = new Vector3(scale.x / Mathf.Sqrt(WindSquash), scale.y * WindSquash, scale.z / Mathf.Sqrt(WindSquash));
            }
            else if (spec.Moves && (agent.LastOrder == HelperOrder.Approach || agent.LastOrder == HelperOrder.ReturnHome || agent.LastOrder == HelperOrder.SideStep))
            {
                if (spec.Body == HelperBody.Hops)
                {
                    float hop = Mathf.Repeat(t, HopSeconds) / HopSeconds;
                    position.y += 4f * HopHeight * hop * (1f - hop);
                    rotation *= Quaternion.Euler(Mathf.Lerp(-8f, 8f, hop), 0f, 0f);
                }
                else
                {
                    rotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(t * WalkHertz * Mathf.PI * 2f) * WalkRoll);
                    position.y += Mathf.Abs(Mathf.Sin(t * WalkHertz * Mathf.PI)) * WalkBob;
                }
            }
            else
            {
                position.y += Mathf.Sin(t * BobHertz * Mathf.PI * 2f) * BobMetres;
                rotation *= Quaternion.Euler(0f, Mathf.Sin(t * LookHertz * Mathf.PI * 2f) * LookYaw, 0f);
            }
            float lunge = Time.time - struckAt;
            if (lunge < LungeSeconds * 2f && !spec.IsTrap)
                position += Vector3.forward * (LungeMetres * Mathf.Sin(Mathf.Clamp01(lunge / (LungeSeconds * 2f)) * Mathf.PI));
            float hurt = Time.time - hurtAt;
            if (hurt < HurtSeconds) rotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(hurt / HurtSeconds * Mathf.PI * 4f) * HurtWobble);
            pose.localPosition = position;
            pose.localRotation = rotation;
            pose.localScale = scale;
            PlaceFlag(position.y - restPosition.y, 1f);
        }

        void PlaceFlag(float lift, float size)
        {
            if (!flag) return;
            flag.localPosition = flagRest + Vector3.up * lift;
            flag.localScale = flagScale * size;
            if (Camera.main) flag.rotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
        }
    }
}
