using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class ReverseZone : MonoBehaviour
    {
        const int ZoneArrows = 3, ArcPoints = 10;
        const float RingLift = .05f, RingWidth = .055f, ZoneArrowSpan = 70f, ZoneSpinDegrees = 75f, ArrowReach = .7f, HeadShare = .2f, BodyLift = .5f;

        DeployedGear tool;
        ItemTrait trait;
        string word;
        int ownerIndex, ownerTeam;
        bool friendlyFire;
        float radius;
        Transform visual, spinner;

        public static Color Tint(ReverseMode mode)
        {
            int rgb = Reversal.Tint(mode);
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        }

        public static bool TryAttach(DeployedGear tool, HeldWeapon gear, PlayerController owner)
        {
            var stats = gear ? gear.Definition.Deploy : null;
            var trait = stats != null ? GameConfig.Current.Tuning?.Trait(gear.word, TraitKind.Reverse) : null;
            if (trait == null || trait.Delivery != TraitDelivery.InZone) return false;
            var zone = gear.gameObject.AddComponent<ReverseZone>();
            zone.tool = tool;
            zone.trait = trait;
            zone.word = gear.word;
            zone.radius = stats.Radius;
            zone.ownerIndex = owner ? owner.Index : -1;
            zone.ownerTeam = owner ? owner.Team : Teams.NoTeam;
            zone.friendlyFire = !owner || !owner.Health || owner.Health.Rules.FriendlyFire;
            zone.Build();
            GameFeedback.Play(GameCue.Protect);
            return true;
        }

        public static int Spread(ItemTrait trait, string word, Vector3 centre, float reach, int maker, int makerTeam, bool friendlyFire, bool makerToo)
        {
            int caught = 0;
            var players = World.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!p || p.IsKnockedOut || p.IsHeld || p.Frozen) continue;
                var at = p.transform.position;
                if (Mathf.Abs(at.y - centre.y) > Reversal.HeightReach || World.Flat(at - centre).sqrMagnitude > reach * reach) continue;
                if (!Reversal.Affects(maker, makerTeam, p.Index, p.Team, friendlyFire, makerToo)) continue;
                if (p.Index != maker && !ClearPath(centre, at)) continue;
                var before = p.Reversed;
                if (!p.Reverse(trait.Turn, trait.Seconds, word)) continue;
                caught++;
                if (p.Reversed != before) Popup.Effect(Reversal.Proc(p.Reversed), p.OverheadPosition + Vector3.up * .45f, Tint(p.Reversed));
            }
            return caught;
        }

        static bool ClearPath(Vector3 from, Vector3 to) =>
            !Physics.Linecast(from + Vector3.up * BodyLift, to + Vector3.up * BodyLift, out var hit, World.GroundMask, QueryTriggerInteraction.Ignore) || hit.rigidbody;

        public static LineRenderer Arrow(Transform parent, Color colour, float arcRadius, float startDegrees, float spanDegrees)
        {
            var arrow = new GameObject("Turning arrow").transform;
            arrow.SetParent(parent, false);
            var points = new Vector3[ArcPoints + 3];
            for (int i = 0; i < ArcPoints; i++)
            {
                float angle = (startDegrees + spanDegrees * i / (ArcPoints - 1)) * Mathf.Deg2Rad;
                points[i] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * arcRadius;
            }
            var tip = points[ArcPoints - 1];
            var along = (tip - points[ArcPoints - 2]).normalized;
            var outward = tip.normalized;
            float head = arcRadius * HeadShare;
            points[ArcPoints] = tip - along * head + outward * head * .6f;
            points[ArcPoints + 1] = tip;
            points[ArcPoints + 2] = tip - along * head - outward * head * .6f;
            var line = SummonEffects.Line(arrow, colour, points);
            line.sharedMaterial = PlayerHud.Mark(colour);
            return line;
        }

        void Build()
        {
            var colour = Tint(trait.Turn);
            visual = new GameObject("Reverse zone").transform;
            visual.SetParent(transform, false);
            visual.localPosition = Vector3.up * RingLift;
            var edge = SummonEffects.Ring(visual, "Reverse zone edge", colour, radius, Quaternion.Euler(90f, 0f, 0f));
            edge.startWidth = edge.endWidth = RingWidth;
            edge.sharedMaterial = PlayerHud.Mark(colour);
            spinner = new GameObject("Turning arrows").transform;
            spinner.SetParent(visual, false);
            for (int i = 0; i < ZoneArrows; i++)
            {
                var arrow = Arrow(spinner, colour, radius * ArrowReach, i * 360f / ZoneArrows, ZoneArrowSpan);
                arrow.startWidth = arrow.endWidth = RingWidth;
            }
        }

        void FixedUpdate()
        {
            if (!tool || trait == null)
            {
                Destroy(this);
                return;
            }
            Spread(trait, word, transform.position, radius, ownerIndex, ownerTeam, friendlyFire, false);
        }

        void Update()
        {
            if (spinner) spinner.localRotation = Quaternion.Euler(0f, -Time.time * ZoneSpinDegrees, 0f);
        }

        void OnDestroy()
        {
            if (visual) Destroy(visual.gameObject);
            visual = spinner = null;
        }
    }
}
