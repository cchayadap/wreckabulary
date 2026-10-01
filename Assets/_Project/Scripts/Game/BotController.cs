using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>A match-only seat. The brain uses the same commands and crafting entry point as a human.</summary>
    public sealed class BotBinding : InputBinding
    {
        static int nextId;
        readonly string id = $"bot-{nextId++}";
        public PlayerCommands Commands;
        public override string Id => id;
        public override void Read(ref PlayerCommands commands) => commands = Commands;
        public override bool JoinPressed() => false;
        public override bool StartPressed() => false;
    }

    [DefaultExecutionOrder(-100)]
    public sealed class BotController : MonoBehaviour
    {
        PlayerController player;
        BotBinding binding;
        Transform target;
        float nextThink, nextAttack, nextCraft, nextEscape;
        Vector3 lastPosition;
        float stuckTime;
        ClearOutController clearOut;
        HouseLayout layout;
        RoomGraph graph;

        public void Configure(PlayerController owner, BotBinding input)
        {
            player = owner;
            binding = input;
            lastPosition = transform.position;
            clearOut = FindAnyObjectByType<ClearOutController>();
            layout = clearOut ? clearOut.Layout : GameConfig.Current.HouseFor(Session.MapId);
            graph = layout.Graph();
        }

        void Update()
        {
            if (binding == null || !player) return;
            var c = new PlayerCommands();
            if (!player.CanAct) { binding.Commands = c; return; }

            var at = player.transform.position;
            string here = layout.RoomAt(at.x, at.z);
            Vector3 destination = at;

            var downed = World.Players.Where(p => p && p.IsDowned && Teams.AreTeammates(player.Team, p.Team))
                .OrderBy(p => (p.transform.position - at).sqrMagnitude).FirstOrDefault();
            bool evacuating = clearOut && clearOut.Running && here != null &&
                clearOut.Schedule.PhaseOf(here, clearOut.Elapsed) >= RoomPhase.Warning;

            if (evacuating)
            {
                var safe = layout.Rooms.Where(r => clearOut.Schedule.PhaseOf(r.Name, clearOut.Elapsed) == RoomPhase.Safe)
                    .OrderBy(r => (Centre(r) - at).sqrMagnitude).FirstOrDefault();
                if (safe != null) destination = Centre(safe);
            }
            else if (downed)
            {
                destination = downed.transform.position;
                if (World.Flat(destination - at).magnitude <= player.Health.Rules.ReviveRange * .9f)
                {
                    c.grabHeld = true;
                    c.drop = player.Combat.IsHolding;
                    c.grab = !player.Combat.IsReviving && !player.Combat.IsHolding;
                    destination = at;
                }
            }
            else
            {
                if (Time.time >= nextThink)
                {
                    nextThink = Time.time + .3f;
                    var opponent = World.NearestOpponent(player, at);
                    // Gather letters and build equipment before closing in; never conjure free gear.
                    var tile = TilePool.Instance && player.Inventory.Count < player.Inventory.Capacity
                        ? TilePool.Instance.Active.Where(t => t).OrderBy(t => (t.transform.position - at).sqrMagnitude).FirstOrDefault() : null;
                    if (opponent && (player.Combat.Weapon || player.Inventory.TotalCount >= 8 || Vector3.Distance(opponent.transform.position, at) < 3f)) target = opponent.transform;
                    else if (tile && !player.Combat.Weapon) target = tile.transform;
                    else target = FindObjectsByType<Smashable>().Where(s => s && !s.IsBroken && !s.Invulnerable)
                        .OrderBy(s => (s.transform.position - at).sqrMagnitude).FirstOrDefault()?.transform ?? opponent?.transform;
                }
                if (target)
                {
                    destination = target.position;
                    float distance = World.Flat(destination - at).magnitude;
                    if (distance < 1.35f && !target.GetComponent<LetterTile>())
                    {
                        if (Time.time >= nextAttack) { c.attack = true; nextAttack = Time.time + .6f; }
                        destination = at;
                    }
                }
                if (!player.Combat.Weapon && Time.time >= nextCraft)
                {
                    nextCraft = Time.time + 1f;
                    var ready = WordSolver.Spellable(GameAssets.I.words.Words, player.Inventory.Letters)
                        .FirstOrDefault(w => w.category == WordCategory.Weapon);
                    if (ready != null) player.Summoner.BeginCraft(ready);
                }
            }

            var waypoint = Waypoint(layout, graph, here, destination, clearOut);
            var delta = World.Flat(waypoint - at);
            var direction = delta.sqrMagnitude > .16f ? delta.normalized : Vector3.zero;
            if (target && !evacuating && !downed) c.look = new Vector2(target.position.x - at.x, target.position.z - at.z);
            else c.look = new Vector2(direction.x, direction.z);
            if (direction.sqrMagnitude > 0f)
            {
                // Room graph handles the walls; local steering skirts furniture between doorways.
                if (Physics.SphereCast(at + Vector3.up * .4f, .25f, direction, out var hit, .85f, World.GroundMask,
                    QueryTriggerInteraction.Ignore) && hit.rigidbody != player.Body && (!target || hit.transform.root != target.root))
                    direction = Quaternion.Euler(0f, 55f + player.Index * 12f, 0f) * direction;
                c.move = new Vector2(direction.x, direction.z);
            }
            stuckTime = c.move.sqrMagnitude > .2f && World.Flat(at - lastPosition).sqrMagnitude < .0004f
                ? stuckTime + Time.deltaTime : 0f;
            if (stuckTime > .7f && Time.time >= nextEscape)
            {
                c.jump = true;
                c.dodge = player.Health.CanDodge;
                nextEscape = Time.time + 1.5f;
                stuckTime = 0f;
            }
            lastPosition = at;
            binding.Commands = c;
        }

        static Vector3 Centre(RoomBox room) => new((room.MinX + room.MaxX) * .5f, room.FloorY, (room.MinZ + room.MaxZ) * .5f);

        static Vector3 Waypoint(HouseLayout layout, RoomGraph graph, string start, Vector3 goal, ClearOutController hazard)
        {
            string end = layout.RoomAt(goal.x, goal.z);
            if (start == null || end == null || start == end) return goal;
            var queue = new Queue<string>();
            var from = new Dictionary<string, string> { [start] = null };
            queue.Enqueue(start);
            while (queue.Count > 0 && !from.ContainsKey(end))
            {
                string room = queue.Dequeue();
                foreach (var next in graph.Neighbours(room))
                {
                    if (from.ContainsKey(next) || (hazard && hazard.Running && hazard.Schedule.PhaseOf(next, hazard.Elapsed) == RoomPhase.Closed)) continue;
                    from[next] = room;
                    queue.Enqueue(next);
                }
            }
            if (!from.ContainsKey(end)) return Centre(layout.Room(start));
            string step = end;
            while (from[step] != start) step = from[step];
            var door = layout.Doors.First(d => (d.A == start && d.B == step) || (d.B == start && d.A == step));
            // Aim just beyond the threshold so RoomAt switches rooms instead of stopping on the wall.
            var toward = World.Flat(Centre(layout.Room(step)) - new Vector3(door.X, 0f, door.Z)).normalized;
            return new Vector3(door.X, layout.Room(step).FloorY, door.Z) + toward * .7f;
        }
    }
}
