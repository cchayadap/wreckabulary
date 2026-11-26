#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameStudioKit.Capture;
using GameStudioKit.Qa;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class HelperRulesQa : IQaScenario
    {
        public const string Scenario = "house_helpers";
        const string Map = "pinwheel";
        const string AimedId = "QAHAIMED", NearestId = "QAHNEAREST", ProvokerId = "QAHPROVOKER", BiterId = "QAHBITER", GuardId = "QAHGUARD", TrapId = "QAHTRAP", WallId = "QAHWALL";
        const int SeatCount = 3;
        const float LoadSeconds = 30f, CheckSeconds = 35f, LongCheckSeconds = 50f, Quiet = 4f, BiteWait = 8f, Settle = .6f, RefusalWait = .5f, KeyStandOff = .6f;
        const float Side = 3f, Back = 1.5f, Ahead = 5.5f, Margin = .6f, Grid = .5f, StageClear = 2.2f, FurnitureClear = 7f;
        const float WallLife = 8f, WallSlack = .75f, ProvokeDamage = 2f, RoundEndWait = 15f, TrapRest = 1.6f;
        static readonly Vector3[] Axes = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        Seat[] seats;
        PlayerJoinManager joins;
        GameHud hud;
        HouseLayout layout;
        RoomBox room;
        Vector3 stage, ahead, right;
        PlayerController p1, p2, p3;
        InputBinding[] originalBindings;
        string originalMap;
        bool launched;
        int loads, furnitureParked;
        Watch watch;
        readonly Dictionary<string, ItemDefinition> items = new();
        readonly Dictionary<string, HelperSpec> specs = new();
        readonly List<string> errors = new();

        public string Name => Scenario;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register(Scenario, () => new HelperRulesQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("helpers_open").Timeout(LoadSeconds * 3f).When(_ => Object.FindAnyObjectByType<LobbyMenu>() != null).Do(Open);
            yield return Check("helpers_maker_safe", MakerSafe);
            yield return Check("helpers_anyone_bites_maker", AnyoneBitesMaker);
            yield return Check("helpers_aimed_target", AimedTarget);
            yield return Check("helpers_nearest_target", NearestTarget);
            yield return Check("helpers_provoker_target", ProvokerTarget);
            yield return Check("helpers_limits", Limits, LongCheckSeconds);
            yield return Check("helpers_trap_spawn_refused", TrapRefusedAtSpawn);
            yield return Check("helpers_trap_fires_once", TrapFiresOnce);
            yield return Check("helpers_wall_doorway_refused", WallRefusedInDoorway);
            yield return Check("helpers_wall_expires", WallExpires);
            yield return Check("helpers_freeze_ends", FreezeEnds);
            yield return Check("helpers_round_end", RoundEnds);
            yield return Check("helpers_duos_open", c => Load(c, HouseModes.Duos), LoadSeconds * 3f);
            yield return Check("helpers_team_safe", TeamSafe);
            yield return Check("helpers_duos_refuses_anyone", DuosRefusesAnyone);
            yield return Check("helpers_escape_open", c => Load(c, HouseModes.Escape), LoadSeconds * 3f);
            yield return Check("helpers_escape_exit_wall_refused", WallRefusedAtExit);
            yield return Check("helpers_escape_key_wall_refused", WallRefusedAtKey);
            yield return Check("helpers_escape_refuses_attackers", EscapeRefusesAttackers);
            yield return QaStep.Named("helpers_return").Timeout(60).Do(Return);
        }

        static QaStep Check(string name, Func<QaContext, IEnumerator> act, float seconds = CheckSeconds) => QaStep.Named(name).Timeout(seconds).Do(act);

        void Log(string condition, string stack, LogType type)
        {
            if (type is LogType.Exception or LogType.Error or LogType.Assert) errors.Add(condition);
        }

        void Loaded(Scene scene, LoadSceneMode mode) => loads++;

        IEnumerator Open(QaContext context)
        {
            originalBindings = Session.Bindings.ToArray();
            originalMap = Session.MapId;
            Application.logMessageReceived += Log;
            SceneManager.sceneLoaded += Loaded;
            AddSpecs();
            watch = new Watch();
            context.Note("helpers_scope", "House helpers in the offline Windows player: Dibs, Duos and Escape on " + Map + " through Session.LoadMode with three scripted local seats " +
                "and the game's own AI seats parked. Helpers are QA test specs (QAH*) added to the helper book for this run and removed after; real Helper recipes " +
                "are used only for the letters-kept check. Furniture near the stage is parked and the clear-out is stopped so the checks stay deterministic. No network or human-feel claim.");
            context.Note("helpers_real_helper_recipes", RealHelpers().Select(i => i.Id).ToArray());
            yield return Load(context, HouseModes.Dibs);
        }

        void AddSpecs()
        {
            Add(Chaser(AimedId, HelperPick.Aimed));
            Add(Chaser(NearestId, HelperPick.Nearest));
            Add(Chaser(ProvokerId, HelperPick.Provoker));
            Add(new HelperSpec
            {
                Id = BiterId, Body = HelperBody.Walks, Pick = HelperPick.Nearest, Bites = HelperBite.Anyone, Attack = HelperAttack.Swing,
                Damage = 10f, Range = 1f, Windup = .45f, Cooldown = .7f, Knockback = 4f,
                Speed = 3.4f, Sight = 8f, Leash = 10f, Seconds = 12f, Strikes = 5, Health = 30f,
            });
            Add(new HelperSpec
            {
                Id = GuardId, Body = HelperBody.Stays, Pick = HelperPick.Nearest, Bites = HelperBite.Enemies, Attack = HelperAttack.Shot,
                Damage = 6f, Range = 3.5f, Windup = .5f, Cooldown = .9f, Knockback = 2f, ShotSpeed = 11f,
                Radius = 3.5f, Seconds = 18f, Strikes = 5, Health = 30f,
            });
            Add(new HelperSpec
            {
                Id = TrapId, Body = HelperBody.Stays, Pick = HelperPick.Stepper, Bites = HelperBite.Enemies, Attack = HelperAttack.Touch,
                Damage = 8f, Range = .6f, Windup = .15f, Cooldown = 1.2f, Knockback = 2f, Slow = .6f, SlowSeconds = 1f,
                Radius = .6f, Seconds = 12f, Strikes = 1, Health = 12f,
            });
            items[WallId] = new ItemDefinition
            {
                Id = WallId, Category = ItemCategory.Combat, Tier = ItemTier.Expanded, Enabled = true, Consumable = false,
                Family = HandlingFamily.DeployCover, Hands = 1, Model = "Items/" + WallId, HeldScale = .8f, Durability = 30,
                Grip = new[] { 0f, .1f, 0f }, Size = new[] { 1.4f, .55f, .3f },
                Deploy = new DeployStats { Effect = DeployEffect.Cover, FootprintX = 1.4f, FootprintZ = .3f, PlaceSeconds = .25f, LifetimeSeconds = WallLife },
            };
        }

        static HelperSpec Chaser(string id, HelperPick pick) => new HelperSpec
        {
            Id = id, Body = HelperBody.Walks, Pick = pick, Bites = HelperBite.Enemies, Attack = HelperAttack.Swing,
            Damage = 8f, Range = 1.1f, Windup = .45f, Cooldown = .75f, Knockback = 3f,
            Speed = 3f, Sight = 8f, Leash = 10f, Seconds = 10f, Strikes = 3, Health = 22f,
        };

        void Add(HelperSpec spec)
        {
            var book = GameConfig.Current.Helpers;
            book.Remove(spec.Id);
            book.Add(spec);
            specs[spec.Id] = spec;
            items[spec.Id] = new ItemDefinition
            {
                Id = spec.Id, Category = ItemCategory.Combat, Tier = ItemTier.Expanded, Enabled = true, Consumable = true,
                Family = HandlingFamily.Helper, Hands = 1, Model = "Items/" + spec.Id, HeldScale = .8f,
                Grip = new[] { 0f, .1f, 0f }, Size = new[] { .32f, .42f, .28f },
            };
        }

        static IEnumerable<ItemDefinition> RealHelpers() => GameConfig.Current.Items.All.Where(i => i.Enabled && i.Family == HandlingFamily.Helper);

        ItemDefinition WallItem => GameConfig.Current.Items.All.FirstOrDefault(i => i.Enabled && HelperBook.IsWall(i)) ?? items[WallId];

        IEnumerator Load(QaContext context, string mode)
        {
            Time.timeScale = 1f;
            seats = Enumerable.Range(1, SeatCount).Select(i => new Seat("qa-helpers-" + i)).ToArray();
            Session.Bindings.Clear();
            foreach (var seat in seats) Session.Remember(seat);
            UnityEngine.Random.InitState(context.Seed);
            string scene = Session.SceneForMode(mode);
            if (!Session.CanLoad(scene)) throw new QaFailException(scene + " is not in the build.");
            launched = true;
            p1 = p2 = p3 = null;
            int before = loads;
            Session.LoadMode(mode, Map);
            yield return context.WaitUntil(() => loads > before && SceneManager.GetActiveScene().name == scene
                && (joins = Object.FindAnyObjectByType<PlayerJoinManager>()) && (hud = Object.FindAnyObjectByType<GameHud>()) && Object.FindAnyObjectByType<RoomBuilder>()
                && Seated(), LoadSeconds, HouseModes.Name(mode) + " on " + Map + " to load with three seats");
            yield return context.WaitUntil(() => Playing(mode), LoadSeconds, HouseModes.Name(mode) + " to start");
            layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            if (Match.Mode != mode) throw new QaFailException($"Expected {mode}, got {Match.Mode}.");
            Park();
            if (!ChooseStage()) throw new QaFailException(Map + " has no ground-floor room with a clear " + (Side * 2f) + " x " + (Back + Ahead) + " m stage away from spawns and doors.");
            ParkFurniture();
            Put(p1, 0f, 0f);
            Put(p2, Side, Ahead);
            Put(p3, -Side, Ahead);
            hud.Follow(p1);
            yield return context.WaitSeconds(Settle);
            context.Note("helpers_mode", Match.Mode);
            context.Note("helpers_stage", new Dictionary<string, object>
            {
                ["room"] = room.Name, ["x"] = stage.x, ["z"] = stage.z, ["floor"] = room.FloorY, ["ahead"] = AxisName(ahead), ["furniture_parked"] = furnitureParked,
            });
            context.Note("helpers_players", Players.Select(p => $"{p.Name} seat {p.Index} team {p.Team} {(p.Binding is BotBinding ? "bot parked" : "local")}").ToArray());
            yield return Capture(context, "helpers_" + mode.ToLowerInvariant() + "_stage");
        }

        bool Seated()
        {
            var players = joins.Players;
            p1 = players.FirstOrDefault(p => p && p.Binding == seats[0]);
            p2 = players.FirstOrDefault(p => p && p.Binding == seats[1]);
            p3 = players.FirstOrDefault(p => p && p.Binding == seats[2]);
            return p1 && p2 && p3;
        }

        static bool Playing(string mode) => mode == HouseModes.Escape
            ? Object.FindAnyObjectByType<EscapeDirector>() is { Current: EscapeDirector.State.Playing }
            : RoundManager.Instance && RoundManager.Instance.Phase == Phase.Playing;

        IReadOnlyList<PlayerController> Players => joins ? joins.Players.Where(p => p).ToList() : new List<PlayerController>();

        void Park()
        {
            foreach (var p in Players)
            {
                var rules = Match.Rules.Clone();
                rules.SpawnProtectionSeconds = 0f;
                rules.LettersDroppedPerHit = 0;
                p.Health.UseRules(rules);
                p.Inventory.Collects = false;
                if (p.Binding is not BotBinding bot) continue;
                bot.Commands = default;
                p.Frozen = true;
                if (p.TryGetComponent<BotController>(out var brain)) brain.enabled = false;
            }
            var clearOut = Object.FindAnyObjectByType<ClearOutController>();
            if (clearOut) clearOut.Running = false;
        }

        bool ChooseStage()
        {
            float lowest = layout.Rooms.Min(candidate => candidate.FloorY);
            foreach (var r in layout.Rooms.Where(candidate => !candidate.Outdoor && candidate.FloorY < lowest + RoomBox.StoreyTolerance).OrderByDescending(candidate => candidate.Area))
                for (float x = r.MinX + Margin; x <= r.MaxX - Margin; x += Grid)
                    for (float z = r.MinZ + Margin; z <= r.MaxZ - Margin; z += Grid)
                        foreach (var axis in Axes)
                            if (Fits(r, x, z, axis))
                            {
                                room = r;
                                stage = new Vector3(x, r.FloorY, z);
                                ahead = axis;
                                right = new Vector3(axis.z, 0f, -axis.x);
                                return true;
                            }
            return false;
        }

        bool Fits(RoomBox r, float x, float z, Vector3 axis)
        {
            var centre = new Vector3(x, r.FloorY, z);
            var side = new Vector3(axis.z, 0f, -axis.x);
            foreach (float s in new[] { -Side, Side })
                foreach (float a in new[] { -Back, Ahead })
                {
                    var corner = centre + side * s + axis * a;
                    if (corner.x < r.MinX + Margin || corner.x > r.MaxX - Margin || corner.z < r.MinZ + Margin || corner.z > r.MaxZ - Margin) return false;
                }
            foreach (var s in layout.Spawns)
                if (OnStorey(s.Room, r) && Outside(centre, side, axis, s.X, s.Z) < StageClear) return false;
            foreach (var d in layout.Doors)
                if ((OnStorey(d.A, r) || OnStorey(d.B, r)) && Outside(centre, side, axis, d.X, d.Z) < StageClear) return false;
            foreach (var stairs in layout.Stairs)
                if (stairs.MaxX > x - Side - 1f && stairs.MinX < x + Side + 1f && stairs.MaxZ > z - Ahead - 1f && stairs.MinZ < z + Ahead + 1f) return false;
            return true;
        }

        bool OnStorey(string name, RoomBox r) => layout.Room(name) is not { } other || other.SameStorey(r);

        static float Outside(Vector3 centre, Vector3 side, Vector3 axis, float x, float z)
        {
            var offset = new Vector3(x - centre.x, 0f, z - centre.z);
            float across = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(offset, side)) - Side);
            float along = Vector3.Dot(offset, axis);
            float beyond = Mathf.Max(0f, Mathf.Max(-Back - along, along - Ahead));
            return Mathf.Sqrt(across * across + beyond * beyond);
        }

        static string AxisName(Vector3 axis) => axis == Vector3.forward ? "+z" : axis == Vector3.back ? "-z" : axis == Vector3.right ? "+x" : "-x";

        void ParkFurniture()
        {
            furnitureParked = 0;
            var centre = At(0f, (Ahead - Back) * .5f);
            foreach (var s in Object.FindObjectsByType<Smashable>(FindObjectsSortMode.None))
            {
                if (!s || !s.gameObject.activeInHierarchy || World.Flat(s.transform.position - centre).magnitude > FurnitureClear) continue;
                s.gameObject.SetActive(false);
                furnitureParked++;
            }
        }

        Vector3 At(float sideways, float forwards) => stage + right * sideways + ahead * forwards;

        void Put(PlayerController p, float sideways, float forwards) => Put(p, At(sideways, forwards), ahead);

        static void Put(PlayerController p, Vector3 at, Vector3 facing)
        {
            if (p.IsKnockedOut || p.Health.Current < p.Health.Max) p.Health.ResetForRound();
            p.Respawn(at);
            p.Body.linearVelocity = Vector3.zero;
            Face(p, facing);
        }

        static void Face(PlayerController p, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;
            direction.Normalize();
            p.FaceTowards(direction);
            p.LookYaw = Mathf.Atan2(direction.x, direction.z);
        }

        void Freeze(PlayerController p, bool frozen) => p.Frozen = frozen;

        void Ready(params PlayerController[] frozen)
        {
            foreach (var p in new[] { p1, p2, p3 }) Freeze(p, frozen.Contains(p));
        }

        HelperAgent Place(string id, PlayerController maker, Vector3 at, int locked = -1)
        {
            var gear = CatalogGear.Create(items[id]);
            var root = gear.gameObject;
            root.transform.position = at;
            root.GetComponent<Rigidbody>().position = at;
            var agent = HelperAgent.Launch(root, items[id], specs[id], maker, locked);
            if (!agent) throw new QaFailException(id + " did not launch.");
            return agent;
        }

        HeldWeapon Hold(PlayerController p, ItemDefinition item)
        {
            Discard(p);
            var gear = CatalogGear.Create(item);
            gear.transform.position = p.transform.position + Vector3.up;
            if (!p.Combat.TryEquip(gear))
            {
                Object.Destroy(gear.gameObject);
                throw new QaFailException(p.Name + " could not hold " + item.Id + ".");
            }
            return gear;
        }

        HeldWeapon Hold(PlayerController p, string id) => Hold(p, items[id]);

        static void Discard(PlayerController p)
        {
            var held = p.Combat.Weapon;
            if (!held) return;
            held.MarkSpent();
            held.Break(p.Combat);
        }

        IEnumerator Clear(QaContext context)
        {
            Time.timeScale = 1f;
            foreach (var h in HelperAgent.All.ToArray()) if (h) h.Stop(HelperEnd.Life);
            foreach (var placed in DeployedGear.All.ToArray())
                if (placed && placed.Owner && !placed.GetComponent<HelperAgent>()) Object.Destroy(placed.gameObject);
            foreach (var p in new[] { p1, p2, p3 }) if (p) Discard(p);
            yield return context.WaitUntil(() => HelperAgent.All.Count == 0 && Object.FindObjectsByType<HelperAgent>(FindObjectsSortMode.None).Length == 0
                && Players.All(p => DeployedGear.CountFor(p) == 0), HelperAgent.EndSeconds + 3f, "earlier helpers and walls to clear");
        }

        Sheet Motion(QaContext context, string name)
        {
            var sheet = new Sheet();
            p1.StartCoroutine(HelperFrameSheet.Record(context, name, f => { sheet.Failure = f; sheet.Done = true; },
                (frames, seconds) => { sheet.Frames = frames; sheet.Seconds = seconds; sheet.Done = true; }));
            return sheet;
        }

        static IEnumerator Finish(QaContext context, Sheet sheet, string name)
        {
            yield return context.WaitUntil(() => sheet.Done, HelperFrameSheet.Seconds + 5f, name + " frame sheet");
            if (sheet.Failure != null) throw new QaFailException(sheet.Failure);
        }

        IEnumerator MakerSafe(QaContext context)
        {
            yield return Clear(context);
            Ready(p2, p3);
            Put(p1, 0f, 0f);
            Put(p2, Side * .8f, 2.5f);
            Put(p3, -Side, Ahead);
            float makerStart = p1.Health.Current;
            var helper = Place(NearestId, p1, At(0f, 1.5f));
            var sheet = Motion(context, "helpers_maker_safe_motion");
            int framesOnMaker = 0, frames = 0;
            float until = Time.time + Quiet;
            while (Time.time < until)
            {
                if (helper && helper.TargetId == p1.Index) framesOnMaker++;
                frames++;
                yield return null;
            }
            float aloneDamage = makerStart - p1.Health.Current;
            float foeStart = p2.Health.Current;
            Freeze(p2, false);
            yield return context.WaitUntil(() => watch.Bites(helper, p2) > 0 || !helper, BiteWait, "the chaser to bite the foe once the foe can be hit");
            yield return Capture(context, "helpers_maker_safe");
            yield return Finish(context, sheet, "helpers_maker_safe_motion");
            context.Note("helpers_maker_safe", new Dictionary<string, object>
            {
                ["alone_seconds"] = Quiet, ["frames_alone"] = frames, ["frames_targeting_maker"] = framesOnMaker, ["maker_damage_alone"] = aloneDamage,
                ["maker_damage"] = makerStart - p1.Health.Current, ["maker_bites"] = watch.Bites(helper, p1), ["foe_bites"] = watch.Bites(helper, p2),
                ["foe_damage"] = foeStart - p2.Health.Current, ["first_bite_at"] = watch.FirstBiteAt(helper),
            });
            if (framesOnMaker > 0 || watch.Bites(helper, p1) > 0 || p1.Health.Current < makerStart)
                throw new QaFailException("A foes-only chaser picked or hurt its own maker.");
            if (watch.Bites(helper, p2) == 0) throw new QaFailException("The chaser never bit the foe, so its maker's safety proves nothing.");
        }

        IEnumerator AnyoneBitesMaker(QaContext context)
        {
            yield return Clear(context);
            Ready(p2, p3);
            Put(p1, 0f, 0f);
            Put(p2, Side, Ahead);
            Put(p3, -Side, Ahead);
            float start = p1.Health.Current;
            int attacker = int.MaxValue;
            Action<PlayerHealth, HitInfo, HitResult> saw = (_, hit, result) => { if (result.Landed) attacker = hit.AttackerId; };
            p1.Health.Damaged += saw;
            HelperAgent helper = null;
            try
            {
                helper = Place(BiterId, p1, At(0f, 1.8f));
                var sheet = Motion(context, "helpers_anyone_motion");
                yield return context.WaitUntil(() => watch.Bites(helper, p1) > 0, BiteWait, "the bite-anyone helper to bite its maker");
                yield return Capture(context, "helpers_anyone_bites_maker");
                yield return Finish(context, sheet, "helpers_anyone_motion");
            }
            finally { p1.Health.Damaged -= saw; }
            context.Note("helpers_anyone_bites_maker", new Dictionary<string, object>
            {
                ["maker_bites"] = watch.Bites(helper, p1), ["maker_damage"] = start - p1.Health.Current, ["attacker_id"] = attacker, ["first_bite_at"] = watch.FirstBiteAt(helper),
            });
            if (attacker != -1) throw new QaFailException($"A bite-anyone hit must come from nobody (attacker -1), not {attacker}.");
        }

        IEnumerator AimedTarget(QaContext context)
        {
            yield return Clear(context);
            Ready();
            Put(p1, 0f, 0f);
            Put(p2, -Side * .8f, 1.2f);
            Put(p3, .3f, Ahead - .5f);
            yield return context.WaitFrames(3);
            var spec = specs[AimedId];
            int locked = HelperAgent.AimedLockFor(p1, spec);
            var from = At(0f, PlayerCombat.DeployReach);
            var helper = Place(AimedId, p1, from, locked);
            var sheet = Motion(context, "helpers_aimed_motion");
            yield return context.WaitUntil(() => watch.FirstVictim(helper) >= 0, BiteWait, "the aimed chaser to strike");
            yield return Capture(context, "helpers_aimed_target");
            yield return Finish(context, sheet, "helpers_aimed_motion");
            context.Note("helpers_aimed_target", new Dictionary<string, object>
            {
                ["locked"] = locked, ["aimed_at"] = p3.Index, ["first_victim"] = watch.FirstVictim(helper), ["near_bystander"] = p2.Index,
                ["bystander_metres"] = Flat(p2.transform.position, from), ["aimed_metres"] = Flat(p3.transform.position, from), ["bystander_bites"] = watch.Bites(helper, p2),
            });
            if (locked != p3.Index) throw new QaFailException($"The aim locked {locked}, not the player straight ahead ({p3.Index}).");
            if (watch.FirstVictim(helper) != p3.Index || watch.Bites(helper, p2) > 0) throw new QaFailException("The aimed chaser bit someone other than the one aimed at.");
        }

        IEnumerator NearestTarget(QaContext context)
        {
            yield return Clear(context);
            Ready();
            Put(p1, 0f, 0f);
            var from = At(0f, PlayerCombat.DeployReach);
            Put(p2, 1.5f, PlayerCombat.DeployReach + .2f);
            Put(p3, -Side * .85f, Ahead - 1.5f);
            yield return context.WaitFrames(3);
            var helper = Place(NearestId, p1, from);
            var sheet = Motion(context, "helpers_nearest_motion");
            yield return context.WaitUntil(() => watch.FirstVictim(helper) >= 0, BiteWait, "the nearest-foe chaser to strike");
            yield return Capture(context, "helpers_nearest_target");
            yield return Finish(context, sheet, "helpers_nearest_motion");
            float near = Flat(p2.transform.position, from), far = Flat(p3.transform.position, from);
            context.Note("helpers_nearest_target", new Dictionary<string, object>
            {
                ["near_player"] = p2.Index, ["near_metres_now"] = near, ["far_player"] = p3.Index, ["far_metres_now"] = far,
                ["first_victim"] = watch.FirstVictim(helper), ["maker_bites"] = watch.Bites(helper, p1),
            });
            if (watch.FirstVictim(helper) != p2.Index) throw new QaFailException("The nearest-foe chaser did not bite the nearest foe first.");
            if (watch.Bites(helper, p1) > 0) throw new QaFailException("The nearest-foe chaser bit its maker.");
        }

        IEnumerator ProvokerTarget(QaContext context)
        {
            yield return Clear(context);
            Ready();
            Put(p1, 0f, 0f);
            Put(p2, 1.6f, .6f);
            Put(p3, -1.6f, .6f);
            var helper = Place(ProvokerId, p1, At(0f, 1.2f));
            var sheet = Motion(context, "helpers_provoker_motion");
            yield return context.WaitSeconds(Quiet * .5f);
            int unprovoked = watch.Strikes(helper);
            p1.Health.ApplyDamage(Hits.Of(p3, p1.transform.position - p3.transform.position, HitSource.Unarmed, ProvokeDamage, 0f));
            float provokedAt = Time.time;
            yield return context.WaitUntil(() => watch.FirstVictim(helper) >= 0, BiteWait, "the provoked helper to bite back");
            float answered = Time.time - provokedAt;
            yield return Capture(context, "helpers_provoker_target");
            yield return Finish(context, sheet, "helpers_provoker_motion");
            context.Note("helpers_provoker_target", new Dictionary<string, object>
            {
                ["strikes_before_provoke"] = unprovoked, ["quiet_seconds"] = Quiet * .5f, ["provoker"] = p3.Index, ["first_victim"] = watch.FirstVictim(helper),
                ["bystander_bites"] = watch.Bites(helper, p2), ["maker_bites"] = watch.Bites(helper, p1), ["answered_after"] = answered,
            });
            if (unprovoked > 0) throw new QaFailException("A provoked-only helper struck before anyone hurt its maker.");
            if (watch.FirstVictim(helper) != p3.Index || watch.Bites(helper, p2) > 0) throw new QaFailException("The provoked helper bit someone other than whoever hurt its maker.");
        }

        IEnumerator Limits(QaContext context)
        {
            yield return Clear(context);
            Ready(p2, p3);
            Put(p1, 0f, 0f);
            Put(p2, Side, Ahead);
            Put(p3, -Side, Ahead);
            yield return context.WaitFrames(3);
            Hold(p1, NearestId);
            bool firstReleased = p1.Combat.ReleaseHelper();
            yield return context.WaitUntil(() => HelperAgent.MoversOf(p1) == 1, HelperAgent.PlaceSeconds + 3f, "the first chaser to hop out");
            var second = Hold(p1, NearestId);
            bool secondReleased = p1.Combat.ReleaseHelper();
            yield return context.WaitSeconds(HelperAgent.PlaceSeconds + RefusalWait);
            bool secondKept = p1.Combat.Weapon == second;
            int movers = HelperAgent.MoversOf(p1);
            int max = p1.Health.Rules.MaxDeployed;
            var turns = new[] { right, -right, (ahead + right).normalized, (ahead - right).normalized };
            int turn = 0;
            while (DeployedGear.CountFor(p1) < max && turn < turns.Length - 1)
            {
                Face(p1, turns[turn++]);
                Hold(p1, GuardId);
                int before = DeployedGear.CountFor(p1);
                if (!p1.Combat.ReleaseHelper()) throw new QaFailException($"A guard was refused with {before} of {max} placed.");
                yield return context.WaitUntil(() => DeployedGear.CountFor(p1) > before, HelperAgent.PlaceSeconds + 3f, "a guard below the placed limit");
            }
            Face(p1, turns[turns.Length - 1]);
            var over = Hold(p1, GuardId);
            bool overReleased = p1.Combat.ReleaseHelper();
            yield return context.WaitSeconds(HelperAgent.PlaceSeconds + RefusalWait);
            bool overKept = p1.Combat.Weapon == over;
            int placed = DeployedGear.CountFor(p1);
            Discard(p1);
            int column = 0;
            while (HelperAgent.InPlay < HelperCaps.MaxInPlay && column < HelperCaps.MaxInPlay * 2)
            {
                Place(GuardId, p1, At(-Side * .8f + column * .9f, Ahead - .3f));
                column++;
                yield return null;
            }
            HelperAgent oldest = null;
            foreach (var h in HelperAgent.All) if (h && (!oldest || h.LaunchedAt < oldest.LaunchedAt)) oldest = h;
            Place(GuardId, p1, At(0f, Ahead - 1.2f));
            yield return null;
            int inPlay = HelperAgent.InPlay;
            bool retired = !oldest || oldest.IsEnding;
            yield return Capture(context, "helpers_limits");
            context.Note("helpers_limits", new Dictionary<string, object>
            {
                ["first_released"] = firstReleased, ["second_mover_released"] = secondReleased, ["second_mover_kept_in_hand"] = secondKept, ["movers_out"] = movers,
                ["max_movers_per_player"] = HelperCaps.MaxMoversPerPlayer, ["max_deployed"] = max, ["placed"] = placed, ["over_limit_released"] = overReleased,
                ["over_limit_kept_in_hand"] = overKept, ["max_in_play"] = HelperCaps.MaxInPlay, ["in_play_after_one_more"] = inPlay, ["oldest_retired"] = retired,
            });
            if (!firstReleased) throw new QaFailException("The first chaser was refused on open floor.");
            if (secondReleased || !secondKept || movers != HelperCaps.MaxMoversPerPlayer) throw new QaFailException("A second moving helper left the hand.");
            if (overReleased || !overKept || placed != max) throw new QaFailException($"A helper over the placed limit ({max}) left the hand.");
            if (inPlay > HelperCaps.MaxInPlay || !retired) throw new QaFailException($"{inPlay} helpers in play; the oldest should retire at {HelperCaps.MaxInPlay}.");
        }

        IEnumerator TrapRefusedAtSpawn(QaContext context)
        {
            yield return Clear(context);
            Ready(p2, p3);
            var spawn = layout.Spawns.FirstOrDefault(s => layout.Room(s.Room) is { } r && r.SameStorey(room));
            if (spawn == null) throw new QaFailException("No spawn point on the stage's storey.");
            var spawnRoom = layout.Room(spawn.Room);
            var at = new Vector3(spawn.X, spawnRoom.FloorY, spawn.Z);
            var toward = new Vector3((spawnRoom.MinX + spawnRoom.MaxX) * .5f - spawn.X, 0f, (spawnRoom.MinZ + spawnRoom.MaxZ) * .5f - spawn.Z);
            Put(p1, at, toward.sqrMagnitude > 1e-4f ? toward : ahead);
            yield return context.WaitFrames(3);
            var trap = items[TrapId];
            var held = Hold(p1, trap);
            var reason = Refusal(p1, HelperPlaceKind.Stayer, trap.Size[0], trap.Size[2], null);
            bool released = p1.Combat.ReleaseHelper();
            yield return context.WaitSeconds(HelperAgent.PlaceSeconds + RefusalWait);
            bool kept = p1.Combat.Weapon == held;
            yield return Capture(context, "helpers_trap_spawn_refused");
            context.Note("helpers_trap_spawn_refused", new Dictionary<string, object>
            {
                ["spawn_room"] = spawn.Room, ["placement_to_spawn_metres"] = Flat(PlacementPoint(p1), at), ["spawn_clear"] = HelperPlacement.SpawnClear,
                ["reason"] = reason.ToString(), ["released"] = released, ["kept_in_hand"] = kept, ["helpers_in_play"] = HelperAgent.InPlay,
            });
            if (reason != HelperRefusal.Spawn || released || !kept || HelperAgent.InPlay > 0) throw new QaFailException($"A trap next to a spawn point was not refused (reason {reason}, released {released}).");
        }

        IEnumerator TrapFiresOnce(QaContext context)
        {
            yield return Clear(context);
            Ready(p3);
            Put(p1, 0f, 0f);
            Put(p2, Side, 3f);
            Put(p3, -Side, Ahead);
            yield return context.WaitFrames(3);
            Hold(p1, TrapId);
            if (!p1.Combat.ReleaseHelper()) throw new QaFailException("A trap was refused on open floor.");
            yield return context.WaitUntil(() => HelperAgent.All.Any(h => h && h.Word == TrapId && h.IsArmed), HelperAgent.PlaceSeconds + HelperBrain.ArmSeconds + 3f, "the trap to arm");
            var trap = HelperAgent.All.First(h => h && h.Word == TrapId);
            var spot = trap.transform.position;
            var sheet = Motion(context, "helpers_trap_motion");
            float start = p2.Health.Current;
            Put(p2, spot, -ahead);
            yield return context.WaitUntil(() => watch.Bites(trap, p2) > 0, 3f, "the trap to snap on the foe stepping on it");
            yield return Capture(context, "helpers_trap_fires_once");
            yield return context.WaitSeconds(TrapRest);
            Put(p2, At(Side, 3f), ahead);
            yield return context.WaitSeconds(RefusalWait);
            Put(p2, spot, -ahead);
            yield return context.WaitSeconds(TrapRest);
            yield return Finish(context, sheet, "helpers_trap_motion");
            context.Note("helpers_trap_fires_once", new Dictionary<string, object>
            {
                ["bites"] = watch.Bites(trap, p2), ["strikes_allowed"] = specs[TrapId].Strikes, ["foe_damage"] = start - p2.Health.Current,
                ["ended_reason"] = watch.End(trap).ToString(), ["gone"] = !trap || trap.IsEnding, ["maker_bites"] = watch.Bites(trap, p1),
            });
            if (watch.Bites(trap, p2) != 1) throw new QaFailException($"A one-snap trap bit {watch.Bites(trap, p2)} times.");
            if (watch.End(trap) != HelperEnd.Strikes) throw new QaFailException($"The trap ended with {watch.End(trap)}, not Strikes.");
        }

        IEnumerator WallRefusedInDoorway(QaContext context)
        {
            yield return Clear(context);
            Ready(p2, p3);
            var wall = WallItem;
            Doorway chosen = null;
            Vector3 stand = default, facing = default;
            foreach (var d in layout.Doors)
            {
                var a = layout.Room(d.A);
                if (a == null || !a.SameStorey(room)) continue;
                var toward = new Vector3(d.X - (a.MinX + a.MaxX) * .5f, 0f, d.Z - (a.MinZ + a.MaxZ) * .5f);
                if (toward.sqrMagnitude < 1e-4f) continue;
                toward.Normalize();
                var from = new Vector3(d.X, a.FloorY, d.Z) - toward * PlayerCombat.DeployReach;
                if (Pure(HelperPlaceKind.Wall, from, toward, wall.Deploy.FootprintX, wall.Deploy.FootprintZ, null) != HelperRefusal.Doorway) continue;
                chosen = d;
                stand = from;
                facing = toward;
                break;
            }
            if (chosen == null) throw new QaFailException("No doorway on the stage's storey where a wall would be refused for the doorway alone.");
            Put(p1, stand, facing);
            yield return context.WaitFrames(3);
            var held = Hold(p1, wall);
            var reason = Refusal(p1, HelperPlaceKind.Wall, wall.Deploy.FootprintX, wall.Deploy.FootprintZ, null);
            bool placed = p1.Combat.DeployHeld();
            yield return context.WaitSeconds(wall.Deploy.PlaceSeconds + RefusalWait);
            bool kept = p1.Combat.Weapon == held;
            yield return Capture(context, "helpers_wall_doorway_refused");
            context.Note("helpers_wall_doorway_refused", new Dictionary<string, object>
            {
                ["wall"] = wall.Id, ["door"] = chosen.A + "|" + chosen.B, ["placement_to_door_metres"] = Flat(PlacementPoint(p1), new Vector3(chosen.X, 0f, chosen.Z)),
                ["door_clear"] = HelperPlacement.DoorClear, ["reason"] = reason.ToString(), ["placed"] = placed, ["kept_in_hand"] = kept, ["placed_count"] = DeployedGear.CountFor(p1),
            });
            if (reason != HelperRefusal.Doorway || placed || !kept || DeployedGear.CountFor(p1) > 0) throw new QaFailException($"A wall in a doorway was not refused (reason {reason}, placed {placed}).");
        }

        IEnumerator WallExpires(QaContext context)
        {
            yield return Clear(context);
            Ready(p2, p3);
            Put(p1, 0f, 0f);
            yield return context.WaitFrames(3);
            var wall = WallItem;
            float life = wall.Deploy.LifetimeSeconds;
            var held = Hold(p1, wall);
            int before = DeployedGear.CountFor(p1);
            if (!p1.Combat.DeployHeld()) throw new QaFailException(wall.Id + " was refused on open floor.");
            yield return context.WaitUntil(() => DeployedGear.CountFor(p1) > before, wall.Deploy.PlaceSeconds + 3f, wall.Id + " to be placed");
            float placedAt = Time.time;
            yield return context.WaitSeconds(RefusalWait);
            yield return Capture(context, "helpers_wall_up");
            yield return context.WaitUntil(() => !held, life + WallSlack + 1f, wall.Id + " to come down");
            float lived = Time.time - placedAt;
            yield return Capture(context, "helpers_wall_expired");
            context.Note("helpers_wall_expires", new Dictionary<string, object>
            {
                ["wall"] = wall.Id, ["lifetime"] = life, ["lived"] = lived, ["slack"] = WallSlack, ["placed_count_after"] = DeployedGear.CountFor(p1),
            });
            if (Mathf.Abs(lived - life) > WallSlack) throw new QaFailException($"{wall.Id} stood {lived:0.00} s, not its {life} s.");
        }

        IEnumerator FreezeEnds(QaContext context)
        {
            yield return Clear(context);
            Ready(p2, p3);
            Put(p1, 0f, 0f);
            var mover = Place(NearestId, p1, At(1.5f, 3f));
            var guard = Place(GuardId, p1, At(-1.5f, 3f));
            yield return context.WaitSeconds(HelperBrain.ArmSeconds + .3f);
            yield return Capture(context, "helpers_before_freeze");
            int before = HelperAgent.InPlay;
            World.FreezeTransient();
            yield return null;
            yield return null;
            context.Note("helpers_freeze_ends", new Dictionary<string, object>
            {
                ["in_play_before"] = before, ["in_play_after"] = HelperAgent.InPlay, ["mover_gone"] = !mover, ["guard_gone"] = !guard,
                ["mover_end"] = watch.End(mover).ToString(), ["guard_end"] = watch.End(guard).ToString(),
            });
            if (before != 2 || HelperAgent.InPlay != 0 || mover || guard) throw new QaFailException("Helpers outlived the freeze.");
            if (watch.End(mover) != HelperEnd.RoundOver || watch.End(guard) != HelperEnd.RoundOver) throw new QaFailException("Frozen helpers must end with RoundOver.");
        }

        IEnumerator RoundEnds(QaContext context)
        {
            yield return Clear(context);
            Ready();
            Put(p1, 0f, 0f);
            Put(p2, Side, Ahead);
            Put(p3, -Side, Ahead);
            var guard = Place(GuardId, p1, At(0f, 2f));
            yield return context.WaitSeconds(HelperBrain.ArmSeconds + .3f);
            var rounds = RoundManager.Instance;
            foreach (var other in Players.Where(q => q != p1 && !q.IsEliminated).ToArray()) other.Health.Eliminate();
            yield return context.WaitUntil(() => rounds && rounds.Phase is Phase.RoundOver or Phase.MatchOver, RoundEndWait, "the round to end");
            float overAt = Time.time;
            yield return context.WaitUntil(() => watch.End(guard) != HelperEnd.None, 5f, "the helper to end with the round");
            float after = Time.time - overAt;
            yield return Capture(context, "helpers_round_end");
            context.Note("helpers_round_end", new Dictionary<string, object>
            {
                ["phase"] = rounds ? rounds.Phase.ToString() : "none", ["end"] = watch.End(guard).ToString(), ["seconds_after_round_over"] = after, ["in_play"] = HelperAgent.InPlay,
            });
            if (watch.End(guard) != HelperEnd.RoundOver) throw new QaFailException($"A helper ended with {watch.End(guard)} at round end, not RoundOver.");
        }

        IEnumerator TeamSafe(QaContext context)
        {
            yield return Clear(context);
            var mate = new[] { p2, p3 }.FirstOrDefault(p => Teams.AreTeammates(p1.Team, p.Team));
            var foe = new[] { p2, p3 }.FirstOrDefault(p => Teams.AreHostile(p1.Team, p.Team));
            context.Note("helpers_teams", new[] { p1, p2, p3 }.Select(p => $"{p.Name} team {p.Team}").ToArray());
            if (!mate || !foe) throw new QaFailException("Duos did not give P1 a local teammate and a local foe.");
            Ready();
            Put(p1, 0f, 0f);
            Put(mate, 0f, 2.8f);
            Put(foe, Side * .85f, 2.8f);
            yield return context.WaitFrames(3);
            int mateHits = 0;
            Action<PlayerHealth, HitInfo, HitResult> saw = (_, _, result) => { if (result.Landed) mateHits++; };
            mate.Health.Damaged += saw;
            float mateStart = mate.Health.Current;
            HelperAgent helper = null;
            try
            {
                helper = Place(NearestId, p1, At(0f, 1.8f));
                var sheet = Motion(context, "helpers_team_motion");
                yield return context.WaitUntil(() => watch.Bites(helper, foe) > 0, BiteWait, "the chaser to pass the teammate and bite the foe");
                yield return context.WaitSeconds(Quiet * .5f);
                yield return Capture(context, "helpers_team_safe");
                yield return Finish(context, sheet, "helpers_team_motion");
            }
            finally { mate.Health.Damaged -= saw; }
            context.Note("helpers_team_safe", new Dictionary<string, object>
            {
                ["mate"] = mate.Index, ["foe"] = foe.Index, ["mate_start_metres"] = 1f, ["foe_start_metres"] = Mathf.Sqrt(Side * .85f * Side * .85f + 1f),
                ["mate_bites"] = watch.Bites(helper, mate), ["mate_hits_any_source"] = mateHits, ["mate_damage"] = mateStart - mate.Health.Current,
                ["foe_bites"] = watch.Bites(helper, foe), ["maker_bites"] = watch.Bites(helper, p1),
            });
            if (watch.Bites(helper, mate) > 0 || mateHits > 0 || mate.Health.Current < mateStart) throw new QaFailException("A foes-only helper hurt a teammate in Duos.");
        }

        IEnumerator DuosRefusesAnyone(QaContext context)
        {
            yield return Clear(context);
            Ready();
            Put(p1, 0f, 0f);
            yield return context.WaitFrames(3);
            var held = Hold(p1, BiterId);
            bool allowed = HelperBook.AllowedIn(Match.Mode, specs[BiterId]);
            bool released = p1.Combat.ReleaseHelper();
            yield return context.WaitSeconds(HelperAgent.PlaceSeconds + RefusalWait);
            bool kept = p1.Combat.Weapon == held;
            Discard(p1);
            var letters = LettersKept(p1, RealHelpers().Where(i => HelperAgent.SpecFor(i, out var s) && s.Bites == HelperBite.Anyone));
            yield return Capture(context, "helpers_duos_refuses_anyone");
            context.Note("helpers_duos_refuses_anyone", new Dictionary<string, object>
            {
                ["mode"] = Match.Mode, ["allowed"] = allowed, ["released"] = released, ["kept_in_hand"] = kept, ["in_play"] = HelperAgent.InPlay, ["letters"] = letters.Rows,
            });
            if (allowed || released || !kept || HelperAgent.InPlay > 0) throw new QaFailException("A bite-anyone helper was let out in Duos.");
            if (letters.Lost.Count > 0) throw new QaFailException("Refused Helper recipes spent their letters: " + string.Join(", ", letters.Lost));
        }

        IEnumerator WallRefusedAtExit(QaContext context)
        {
            yield return Clear(context);
            Ready();
            var escape = Object.FindAnyObjectByType<EscapeDirector>();
            if (!escape) throw new QaFailException("Escape has no director.");
            var exit = escape.ExtractionPoint;
            var exitRoom = layout.Room(layout.ExtractionRoom) ?? HelperPlacement.RoomAt(layout, exit.x, exit.y + .1f, exit.z) ?? room;
            var toward = new Vector3(exit.x - (exitRoom.MinX + exitRoom.MaxX) * .5f, 0f, exit.z - (exitRoom.MinZ + exitRoom.MaxZ) * .5f);
            if (toward.sqrMagnitude < 1e-4f) toward = ahead;
            toward.Normalize();
            Put(p1, new Vector3(exit.x, exitRoom.FloorY, exit.z) - toward * PlayerCombat.DeployReach, toward);
            yield return context.WaitFrames(3);
            var wall = WallItem;
            var held = Hold(p1, wall);
            var guards = new List<HelperGuard> { new HelperGuard(exit.x, exit.z, HelperPlacement.ObjectiveClear, HelperRefusal.Exit) };
            var reason = Refusal(p1, HelperPlaceKind.Wall, wall.Deploy.FootprintX, wall.Deploy.FootprintZ, guards);
            bool placed = p1.Combat.DeployHeld();
            yield return context.WaitSeconds(wall.Deploy.PlaceSeconds + RefusalWait);
            bool kept = p1.Combat.Weapon == held;
            yield return Capture(context, "helpers_escape_exit_wall_refused");
            context.Note("helpers_escape_exit_wall_refused", new Dictionary<string, object>
            {
                ["wall"] = wall.Id, ["exit_room"] = exitRoom.Name, ["placement_to_exit_metres"] = Flat(PlacementPoint(p1), exit), ["exit_clear"] = HelperPlacement.ObjectiveClear,
                ["reason"] = reason.ToString(), ["placed"] = placed, ["kept_in_hand"] = kept, ["placed_count"] = DeployedGear.CountFor(p1),
            });
            if (reason != HelperRefusal.Exit || placed || !kept || DeployedGear.CountFor(p1) > 0) throw new QaFailException($"A wall at the Escape exit was not refused (reason {reason}, placed {placed}).");
        }

        IEnumerator WallRefusedAtKey(QaContext context)
        {
            yield return Clear(context);
            Ready();
            var escape = Object.FindAnyObjectByType<EscapeDirector>();
            if (!escape) throw new QaFailException("Escape has no director.");
            var spots = new List<Vector3>();
            escape.KeySpots(spots);
            if (spots.Count == 0) throw new QaFailException("Escape has no key left to guard.");
            var key = spots[0];
            var keyRoom = HelperPlacement.RoomAt(layout, key.x, key.y + .1f, key.z) ?? room;
            var toward = new Vector3(key.x - (keyRoom.MinX + keyRoom.MaxX) * .5f, 0f, key.z - (keyRoom.MinZ + keyRoom.MaxZ) * .5f);
            if (toward.sqrMagnitude < 1e-4f) toward = ahead;
            toward.Normalize();
            int foundBefore = escape.KeysFound;
            Put(p1, new Vector3(key.x, keyRoom.FloorY, key.z) - toward * (PlayerCombat.DeployReach + KeyStandOff), toward);
            yield return context.WaitFrames(3);
            var wall = WallItem;
            var held = Hold(p1, wall);
            var guards = new List<HelperGuard> { new HelperGuard(key.x, key.z, HelperPlacement.ObjectiveClear, HelperRefusal.Key) };
            var reason = Refusal(p1, HelperPlaceKind.Wall, wall.Deploy.FootprintX, wall.Deploy.FootprintZ, guards);
            var open = Refusal(p1, HelperPlaceKind.Wall, wall.Deploy.FootprintX, wall.Deploy.FootprintZ, null);
            bool placed = p1.Combat.DeployHeld();
            yield return context.WaitSeconds(wall.Deploy.PlaceSeconds + RefusalWait);
            bool kept = p1.Combat.Weapon == held;
            yield return Capture(context, "helpers_escape_key_wall_refused");
            context.Note("helpers_escape_key_wall_refused", new Dictionary<string, object>
            {
                ["wall"] = wall.Id, ["key_room"] = keyRoom.Name, ["keys_guarded"] = spots.Count, ["placement_to_key_metres"] = Flat(PlacementPoint(p1), key), ["key_clear"] = HelperPlacement.ObjectiveClear,
                ["reason"] = reason.ToString(), ["reason_without_the_key"] = open.ToString(), ["placed"] = placed, ["kept_in_hand"] = kept, ["placed_count"] = DeployedGear.CountFor(p1),
                ["keys_found_before"] = foundBefore, ["keys_found_after"] = escape.KeysFound,
            });
            if (escape.KeysFound != foundBefore) throw new QaFailException("The key was picked up during the check, so the guard was not tested.");
            if (reason != HelperRefusal.Key || placed || !kept || DeployedGear.CountFor(p1) > 0) throw new QaFailException($"A wall beside an Escape key was not refused (reason {reason}, placed {placed}).");
        }

        IEnumerator EscapeRefusesAttackers(QaContext context)
        {
            yield return Clear(context);
            Ready();
            Put(p1, 0f, 0f);
            yield return context.WaitFrames(3);
            var held = Hold(p1, NearestId);
            bool allowed = HelperBook.AllowedIn(Match.Mode, specs[NearestId]);
            bool released = p1.Combat.ReleaseHelper();
            yield return context.WaitSeconds(HelperAgent.PlaceSeconds + RefusalWait);
            bool kept = p1.Combat.Weapon == held;
            bool trapAllowed = HelperBook.AllowedIn(Match.Mode, specs[TrapId]);
            Discard(p1);
            var letters = LettersKept(p1, RealHelpers().Where(i => !HelperAgent.Offered(i, Match.Mode)));
            yield return Capture(context, "helpers_escape_refuses_attackers");
            context.Note("helpers_escape_refuses_attackers", new Dictionary<string, object>
            {
                ["mode"] = Match.Mode, ["attacker_allowed"] = allowed, ["released"] = released, ["kept_in_hand"] = kept, ["trap_allowed"] = trapAllowed,
                ["in_play"] = HelperAgent.InPlay, ["letters"] = letters.Rows,
            });
            if (allowed || released || !kept || HelperAgent.InPlay > 0) throw new QaFailException("An attacking helper was let out in Escape, where no foe is fair game.");
            if (!trapAllowed) throw new QaFailException("Traps should stay allowed in Escape.");
            if (letters.Lost.Count > 0) throw new QaFailException("Refused Helper recipes spent their letters: " + string.Join(", ", letters.Lost));
        }

        static Letters LettersKept(PlayerController p, IEnumerable<ItemDefinition> refused)
        {
            var result = new Letters();
            foreach (var item in refused)
            {
                p.Inventory.Set(item.Id);
                string before = new string(p.Inventory.Letters.ToArray());
                bool began = p.Summoner.BeginCraft(new WordEntry { word = item.Id });
                if (began) p.Summoner.CancelCraft();
                string after = new string(p.Inventory.Letters.ToArray());
                bool keptLetters = !began && before == after;
                if (!keptLetters) result.Lost.Add(item.Id);
                result.Rows.Add(new Dictionary<string, object> { ["id"] = item.Id, ["craft_began"] = began, ["letters_before"] = before, ["letters_after"] = after });
            }
            p.Inventory.Set("");
            if (result.Rows.Count == 0) result.Rows.Add(new Dictionary<string, object> { ["id"] = "none", ["note"] = "no refused Helper recipe in items_house.json yet; the release refusal above stands alone" });
            return result;
        }

        static Vector3 PlacementPoint(PlayerController p) => p.transform.position + p.Facing * PlayerCombat.DeployReach;

        HelperRefusal Refusal(PlayerController p, HelperPlaceKind kind, float sizeX, float sizeZ, IReadOnlyList<HelperGuard> guards) =>
            Pure(kind, p.transform.position, p.Facing, sizeX, sizeZ, guards);

        HelperRefusal Pure(HelperPlaceKind kind, Vector3 from, Vector3 facing, float sizeX, float sizeZ, IReadOnlyList<HelperGuard> guards)
        {
            var at = from + facing * PlayerCombat.DeployReach;
            float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            var footprint = HelperFootprint.Around(at.x, at.z, sizeX, sizeZ, yaw);
            return HelperPlacement.Check(layout, kind, at.x, from.y, at.z, footprint, guards, null);
        }

        static float Flat(Vector3 a, Vector3 b) => World.Flat(a - b).magnitude;

        IEnumerator Return(QaContext context)
        {
            Time.timeScale = 1f;
            watch?.Dispose();
            foreach (string id in specs.Keys) GameConfig.Current.Helpers.Remove(id);
            if (originalBindings != null)
            {
                Session.Bindings.Clear();
                Session.Bindings.AddRange(originalBindings);
            }
            if (launched)
            {
                Session.GoHome();
                yield return context.WaitUntil(() => SceneManager.GetActiveScene().name == Session.HubScene && Object.FindAnyObjectByType<LobbyMenu>() != null, 30, "the Hub");
            }
            if (originalMap != null && Session.MapId != originalMap) Session.SelectMap(originalMap);
            SceneManager.sceneLoaded -= Loaded;
            Application.logMessageReceived -= Log;
            context.Note("helpers_errors", errors.ToArray());
            if (errors.Count > 0) throw new QaFailException($"house_helpers logged {errors.Count} error(s); first: {errors[0]}");
        }

        static IEnumerator Capture(QaContext context, string name)
        {
            string path = context.PathInRun(name + ".png"), error = null;
            if (File.Exists(path)) throw new QaFailException("Capture never overwrites existing evidence: " + name);
            yield return StoryCamera.CaptureScreen(path, result => error = result);
            if (error != null) throw new QaFailException("Screen capture failed: " + error);
            var image = new Texture2D(2, 2);
            try
            {
                if (!image.LoadImage(File.ReadAllBytes(path))) throw new QaFailException("Capture is not a readable PNG: " + name);
                var pixels = image.GetPixels32();
                int distinct = pixels.Where((_, i) => i % 97 == 0).Select(p => (p.r >> 3, p.g >> 3, p.b >> 3)).Distinct().Count();
                if (distinct < 24) throw new QaFailException("Capture looks blank: " + name);
            }
            finally { Object.Destroy(image); }
            context.AddCapture(name, path, "png", "screen");
        }

        sealed class Sheet
        {
            public bool Done;
            public string Failure;
            public int Frames;
            public float Seconds;
        }

        sealed class Letters
        {
            public readonly List<Dictionary<string, object>> Rows = new();
            public readonly List<string> Lost = new();
        }

        sealed class Watch : IDisposable
        {
            readonly Dictionary<HelperAgent, List<int>> victims = new();
            readonly Dictionary<HelperAgent, float> firstBite = new();
            readonly Dictionary<HelperAgent, HelperEnd> ends = new();

            public Watch()
            {
                HelperAgent.Struck += OnStruck;
                HelperAgent.Ended += OnEnded;
            }

            public void Dispose()
            {
                HelperAgent.Struck -= OnStruck;
                HelperAgent.Ended -= OnEnded;
            }

            void OnStruck(HelperAgent agent, PlayerController victim)
            {
                if (ReferenceEquals(agent, null) || !victim) return;
                if (!victims.TryGetValue(agent, out var list)) victims[agent] = list = new List<int>();
                list.Add(victim.Index);
                if (!firstBite.ContainsKey(agent)) firstBite[agent] = Time.time - agent.LaunchedAt;
            }

            void OnEnded(HelperAgent agent, HelperEnd end)
            {
                if (!ReferenceEquals(agent, null) && !ends.ContainsKey(agent)) ends[agent] = end;
            }

            public int Bites(HelperAgent agent, PlayerController victim) =>
                !ReferenceEquals(agent, null) && victim && victims.TryGetValue(agent, out var list) ? list.Count(id => id == victim.Index) : 0;

            public int Strikes(HelperAgent agent) => !ReferenceEquals(agent, null) && victims.TryGetValue(agent, out var list) ? list.Count : 0;

            public int FirstVictim(HelperAgent agent) => !ReferenceEquals(agent, null) && victims.TryGetValue(agent, out var list) && list.Count > 0 ? list[0] : -1;

            public float FirstBiteAt(HelperAgent agent) => !ReferenceEquals(agent, null) && firstBite.TryGetValue(agent, out float at) ? at : -1f;

            public HelperEnd End(HelperAgent agent) => !ReferenceEquals(agent, null) && ends.TryGetValue(agent, out var end) ? end : HelperEnd.None;
        }

        sealed class Seat : InputBinding
        {
            readonly string id;
            public PlayerCommands Next;

            public Seat(string id) => this.id = id;

            public override string Id => id;

            public override void Read(ref PlayerCommands c)
            {
                c = Next;
                Next = default;
            }

            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }
    }
}
#endif
