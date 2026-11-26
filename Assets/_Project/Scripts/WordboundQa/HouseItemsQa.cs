#if UNITY_EDITOR || GSK_QA
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
    public sealed class HouseItemsQa : IQaScenario
    {
        public const string Scenario = "house_items";
        public const int MaxItems = 20, Parts = 4;
        const float Wound = 30f, ShotSeconds = 3f, ShotGap = 3f;
        const float StepSeconds = 30f, WallSlack = .75f, MoverGap = 1.2f, TrapGap = 2f, ProvokeDamage = 1f, ArmSlack = .25f, WatchBack = 2.6f, WatchSide = 1.8f, WatchTurn = 20f;
        const string RefusalHouse = "pinwheel", BagHandA = "BAT", BagHandB = "BLADE";
        public string Name => ScenarioFor(part);
        static readonly Vector3 Lane = new Vector3(-5.5f, 0f, -2.6f);
        readonly int part;
        bool opened;
        string openFailure = "house_open has not run";
        BeltRangeDirector director;
        PlayerController player;
        ScriptedBinding input;
        Vector3 dummyHome;
        int throws;
        float placedAt, armedAt, firstStrikeAt, endedAt;
        HelperAgent helper;
        HelperEnd ended;
        int strikes, makerBitten;
        bool pauseOnStrike;
        List<string> ids = new(), allIds = new();
        readonly List<string> errors = new();
        readonly List<Dictionary<string, object>> records = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            for (int i = 0; i < Parts; i++)
            {
                int index = i;
                QaScenarios.Register(ScenarioFor(index), () => new HouseItemsQa(index));
            }
        }

        public HouseItemsQa(int part = 0) => this.part = part;

        public static string ScenarioFor(int part) => part == 0 ? Scenario : $"{Scenario}_{part + 1}";

        public static List<string> PartOf(IReadOnlyList<string> all, int part) => all.Skip(part * MaxItems).Take(MaxItems).ToList();

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            allIds = data && data.houseItems ? ItemCatalogue.FromJson(data.houseItems.text, "items_house.json").All.Select(i => i.Id).ToList() : new List<string>();
            ids = PartOf(allIds, part);
            yield return QaStep.Named("house_open").Timeout(60).When(_ => Object.FindFirstObjectByType<LobbyMenu>() != null).Do(Open);
            foreach (string id in ids)
                yield return QaStep.Named("house_" + id.ToLowerInvariant()).Timeout(TimeoutFor(id)).Do(c => Exercise(c, id));
            yield return QaStep.Named("house_return").Timeout(40).Do(Return);
        }

        void Log(string condition, string stack, LogType type)
        {
            if (type is LogType.Exception or LogType.Error or LogType.Assert) errors.Add(condition);
        }

        void Counted(Rigidbody _) => throws++;

        IEnumerator Open(QaContext context)
        {
            if (allIds.Count == 0) Refuse("GameData lists no items_house.json recipes. Run Wreckabulary > Data > Set Up Game Data.");
            if (allIds.Count > MaxItems * Parts)
                Refuse($"{allIds.Count} House items need {(allIds.Count + MaxItems - 1) / MaxItems} parts of {MaxItems}; raise HouseItemsQa.Parts and run {ScenarioFor(Parts)} too.");
            if (ids.Count == 0) Refuse($"{Name} has no House items: items_house.json lists {allIds.Count}, which fit in {ScenarioFor(0)} alone.");
            Session.Load(BeltRangeDirector.SceneName);
            yield return context.WaitUntil(() => (director = Object.FindFirstObjectByType<BeltRangeDirector>()) != null, 30, "the BELT range scene");
            Application.logMessageReceived += Log;
            input = new ScriptedBinding();
            player = director.Joins.Join(input);
            player.Combat.Thrown += Counted;
            Object.FindFirstObjectByType<CameraRig>()?.Follow(player);
            GameHud.Active.Follow(player);
            yield return context.WaitSeconds(1f);
            yield return context.WaitUntil(() => player.CanAct && player.Combat.Weapon && director.Dummy, 10, "a player holding BELT beside the dummy");
            dummyHome = director.Dummy.transform.position;
            var shared = ItemCatalogue.FromJson(Resources.Load<GameData>(GameData.ResourcePath).items.text);
            context.Note("house_items", ids.ToArray());
            context.Note("house_items_part", $"{part + 1} of {(allIds.Count + MaxItems - 1) / MaxItems}");
            opened = true;
            context.Note("house_items_shared_enabled", ids.Where(id => shared.TryGet(id, out var s) && s.Enabled).ToArray());
            context.Note("house_items_scope", "items_house scope: House-mode recipes layered on items.json, exercised in the offline Windows BELT range " +
                "with scripted input. Wordbound and online read items.json only; no network, remote peer or human-feel claim.");
        }

        void Refuse(string why)
        {
            openFailure = why;
            throw new QaFailException(why);
        }

        void RequireOpened(string id)
        {
            if (!opened || !director || !player || input == null)
                throw new QaFailException($"{id} skipped: house_open did not finish ({openFailure}).");
        }

        IEnumerator Exercise(QaContext context, string id)
        {
            RequireOpened(id);
            errors.Clear();
            var item = GameConfig.Current.Items.Get(id);
            string name = "house_" + id.ToLowerInvariant();
            Stand(Lane, Vector3.right);
            if (GameConfig.Current.BagRows.Holds(item))
            {
                var packed = new Dictionary<string, object> { ["id"] = id, ["family"] = item.Family.ToString(), ["hands"] = item.Hands, ["bag"] = true };
                yield return Pack(context, item, name, packed);
                if (errors.Count > 0) throw new QaFailException($"{id} logged {errors.Count} error(s); first: {errors[0]}");
                records.Add(packed);
                yield break;
            }
            var gear = director.EquipCatalogue(player, id);
            yield return context.WaitSeconds(0.8f);
            if (!gear || player.Combat.Weapon != gear || !gear.IsHeld || gear.Definition != item || !item.Enabled)
                throw new QaFailException(id + " is not held with its House definition.");
            yield return Capture(context, name + "_held");
            var record = new Dictionary<string, object>
            {
                ["id"] = id, ["family"] = item.Family.ToString(), ["hands"] = item.Hands, ["held_scale"] = gear.transform.localScale.x,
            };
            switch (item.Family)
            {
                case HandlingFamily.MeleeSwing:
                case HandlingFamily.MeleeThrust:
                    yield return Swing(context, gear, item, name, record);
                    break;
                case HandlingFamily.Thrown:
                    yield return Throw(context, gear, item, name, record);
                    break;
                case HandlingFamily.Shield:
                    yield return Raise(context, item, name, record);
                    break;
                case HandlingFamily.DeployCover:
                case HandlingFamily.DeployPad:
                case HandlingFamily.DeploySpeed:
                case HandlingFamily.DeployZone:
                case HandlingFamily.Utility:
                    yield return Place(context, gear, item, name, record);
                    break;
                case HandlingFamily.Heal:
                case HandlingFamily.Buff:
                    yield return Consume(context, item, name, record);
                    break;
                case HandlingFamily.Ranged:
                    yield return Shoot(context, gear, item, name, record);
                    break;
                case HandlingFamily.Helper:
                    yield return Release(context, item, name, record);
                    break;
                default:
                    throw new QaFailException($"{id}: no House QA use for {item.Family}.");
            }
            yield return Reverses(context, item, name, record);
            if (errors.Count > 0) throw new QaFailException($"{id} logged {errors.Count} error(s); first: {errors[0]}");
            records.Add(record);
        }

        IEnumerator Swing(QaContext context, HeldWeapon gear, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            var dummy = director.Dummy;
            if (!dummy) throw new QaFailException("The range dummy is missing.");
            var melee = item.Melee;
            dummy.Respawn(dummyHome);
            dummy.Body.linearVelocity = Vector3.zero;
            dummy.FaceTowards(Vector3.back);
            Stand(dummyHome - Vector3.forward * Mathf.Clamp(melee.Reach * 0.6f, 0.7f, 1.1f), Vector3.forward);
            yield return context.WaitSeconds(0.5f);
            float before = dummy.Health.Current;
            input.Next.attack = true;
            yield return context.WaitUntil(() => gear.SwingStartedAt >= 0f, 3, item.Id + " swing to start");
            float start = gear.SwingStartedAt;
            yield return context.WaitUntil(() => Time.time - start >= melee.Windup + melee.Active * 0.4f, 3, item.Id + " contact");
            Time.timeScale = 0f;
            try
            {
                yield return null;
                yield return Capture(context, name + "_used");
            }
            finally { Time.timeScale = 1f; }
            yield return context.WaitUntil(() => !gear || gear.SwingStartedAt < 0f, 3, item.Id + " swing to finish");
            float dealt = before - dummy.Health.Current;
            record["dummy_damage"] = dealt;
            if (dealt <= 0f) throw new QaFailException(item.Id + " swung but did not hit the dummy in front.");
        }

        IEnumerator Throw(QaContext context, HeldWeapon gear, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            int before = throws;
            var from = gear.transform.position;
            input.Next.attack = true;
            yield return context.WaitUntil(() => throws > before && !player.Combat.Weapon, 3, item.Id + " to be thrown");
            yield return context.WaitSeconds(0.35f);
            yield return Capture(context, name + "_used");
            record["recoverable"] = item.Thrown.Recoverable;
            record["spent"] = !gear || gear.IsSpent;
            if (!gear) yield break;
            float flown = World.Flat(gear.transform.position - from).magnitude;
            record["flown_metres"] = flown;
            if (gear.IsHeld || flown < 0.5f) throw new QaFailException(item.Id + " left the hand but did not fly.");
            Object.Destroy(gear.gameObject);
        }

        IEnumerator Raise(QaContext context, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            input.Next.blockHeld = true;
            try
            {
                yield return context.WaitUntil(() => player.Combat.IsBlocking, item.Shield.RaiseSeconds + 2f, item.Id + " to rise");
                yield return context.WaitUntil(() => !player.Health.IsInvulnerable, 5, "spawn protection to end");
                bool blocked = false;
                System.Action<PlayerHealth, HitInfo, HitResult> saw = (_, _, r) => blocked |= r.Blocked;
                player.Health.Damaged += saw;
                player.Health.ApplyDamage(Hits.Of(null, -player.Facing, HitSource.Melee, 4f, 0f, hitStun: 0f));
                player.Health.Damaged -= saw;
                record["blocked_front_hit"] = blocked;
                yield return context.WaitFrames(2);
                var turn = Reversing(item);
                if (turn != null && turn.Delivery == TraitDelivery.WhileRaised)
                {
                    yield return context.WaitUntil(() => player.Reversed != ReverseMode.None, 2, item.Id + " to reverse its raiser");
                    record["holder_reversed"] = player.Reversed.ToString();
                }
                yield return Capture(context, name + "_used");
                if (!blocked) throw new QaFailException(item.Id + " was raised but did not block a frontal hit.");
            }
            finally { input.Next.blockHeld = false; }
        }

        IEnumerator Place(QaContext context, HeldWeapon gear, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            int before = DeployedGear.CountFor(player);
            input.Next.attack = true;
            yield return context.WaitUntil(() => DeployedGear.CountFor(player) > before, item.Deploy.PlaceSeconds + 3f, item.Id + " to be placed");
            placedAt = Time.time;
            yield return context.WaitFrames(2);
            if (item.Consumable)
            {
                yield return Spread(context, gear, item, name, record);
                yield break;
            }
            if (!gear || player.Combat.Weapon || !gear.GetComponent<DeployedGear>()) throw new QaFailException(item.Id + " did not leave the hand as placed gear.");
            var offset = World.Flat(gear.GetComponent<Rigidbody>().position - player.Body.position);
            var effect = item.Deploy.Effect;
            record["effect"] = effect.ToString();
            record["placed_metres"] = offset.magnitude;
            if (Vector3.Dot(offset.normalized, player.Facing) < 0.9f) throw new QaFailException(item.Id + " was not placed in front.");
            bool zone = gear.GetComponent<ReverseZone>();
            bool field = !zone && effect is DeployEffect.WindField or DeployEffect.SlowField, pad = effect is DeployEffect.JumpPad or DeployEffect.SpeedStrip;
            if ((field && !gear.GetComponent<DeployedPowerField>()) || (pad && !gear.GetComponentInChildren<GearTrigger>()))
                throw new QaFailException(item.Id + " was placed without its " + effect + " effect.");
            if (effect == DeployEffect.Stash)
            {
                var stash = gear.GetComponent<LetterStash>();
                if (!stash) throw new QaFailException(item.Id + " was placed without a letter stash.");
                player.Inventory.Set("QA");
                bool banked = stash.TryExchange(player) && player.Inventory.IsEmpty && stash.Count == 2;
                bool returned = banked && stash.TryExchange(player) && stash.IsEmpty && player.Inventory.Count == 2;
                record["stash_banked"] = banked;
                record["stash_returned"] = returned;
                player.Inventory.Set("");
                if (!returned) throw new QaFailException(item.Id + " did not bank and return its placer's letters.");
            }
            if (zone)
            {
                var dummy = director.Dummy;
                if (!dummy) throw new QaFailException("The range dummy is missing.");
                dummy.ClearReversal();
                dummy.Body.position = dummy.transform.position = gear.transform.position + Vector3.right * Mathf.Min(1f, item.Deploy.Radius * .6f);
                dummy.Body.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                yield return context.WaitUntil(() => dummy.Reversed != ReverseMode.None, 3, item.Id + " to reverse the dummy standing inside");
                record["dummy_reversed"] = dummy.Reversed.ToString();
                record["placer_reversed"] = player.Reversed.ToString();
                if (player.Reversed != ReverseMode.None) throw new QaFailException(item.Id + " reversed its own placer.");
            }
            yield return context.WaitSeconds(0.5f);
            yield return Capture(context, name + "_used");
            if (HelperBook.IsWall(item))
            {
                yield return Wall(context, gear, item, record);
                yield break;
            }
            Object.Destroy(gear.gameObject);
            if (zone && director.Dummy) director.Dummy.Respawn(dummyHome);
        }

        static ItemTrait Reversing(ItemDefinition item) => GameConfig.Current.Tuning?.Trait(item.Id, TraitKind.Reverse);

        IEnumerator Reverses(QaContext context, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            var trait = Reversing(item);
            if (trait == null) yield break;
            record["reverse_mode"] = trait.Turn.ToString();
            record["reverse_delivery"] = trait.Delivery.ToString();
            record["reverse_seconds"] = trait.Seconds;
            if (!trait.OnHit) yield break;
            var dummy = director.Dummy;
            if (!dummy) throw new QaFailException("The range dummy is missing.");
            dummy.Health.ResetForRound();
            dummy.Respawn(dummyHome);
            player.ClearReversal();
            yield return context.WaitUntil(() => !dummy.Health.IsInvulnerable, 5, "dummy spawn protection to end");
            var source = item.Thrown != null ? HitSource.Thrown : HitSource.Melee;
            var roll = PlayerHealth.TraitRoll;
            PlayerHealth.TraitRoll = () => 0f;
            try
            {
                for (int blow = 0; blow < trait.Every; blow++)
                    dummy.Health.ApplyDamage(Hits.Of(player, dummy.transform.position - player.transform.position, source, 1f, 0f, 0f, 0f, item.Id));
            }
            finally { PlayerHealth.TraitRoll = roll; }
            record["dummy_reversed"] = dummy.Reversed.ToString();
            record["thrower_reversed"] = player.Reversed.ToString();
            if (dummy.Reversed != trait.Turn) throw new QaFailException($"{item.Id} landed {trait.Every} blow(s) but the dummy is {dummy.Reversed}, not {trait.Turn}.");
            if (player.Reversed != ReverseMode.None) throw new QaFailException(item.Id + " reversed the player who used it.");
            yield return context.WaitSeconds(0.4f);
            yield return Capture(context, name + "_reversed");
            dummy.ClearReversal();
        }

        IEnumerator Spread(QaContext context, HeldWeapon gear, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            var effect = item.Deploy.Effect;
            var patch = Object.FindObjectsByType<SummonedThing>(FindObjectsSortMode.None).FirstOrDefault(s => s && s.Word == item.Id);
            record["effect"] = effect.ToString();
            if (gear || player.Combat.Weapon || !patch) throw new QaFailException(item.Id + " was not used up into its " + effect + " patch.");
            var offset = World.Flat(patch.transform.position - player.Body.position);
            record["placed_metres"] = offset.magnitude;
            record["patch_radius"] = item.Deploy.Radius;
            if (Vector3.Dot(offset.normalized, player.Facing) < 0.9f) throw new QaFailException(item.Id + " was not laid in front.");
            yield return context.WaitSeconds(0.5f);
            yield return Capture(context, name + "_used");
            patch.Finish(SummonEndReason.Expired);
        }

        IEnumerator Consume(QaContext context, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            yield return context.WaitUntil(() => !player.Health.IsInvulnerable, 5, "spawn protection to end");
            player.Health.ApplyDamage(Hits.Of(null, -player.Facing, HitSource.Melee, Wound, 0f, hitStun: 0f));
            float before = player.Health.Current;
            input.Next.attack = true;
            yield return context.WaitUntil(() => !player.Combat.Weapon, item.Use.ChannelSeconds + 3f, item.Id + " to be used");
            yield return context.WaitSeconds(0.3f);
            yield return Capture(context, name + "_used");
            float restored = player.Health.Current - before;
            record["restored"] = restored;
            if (item.Use.Effect == UseEffect.Heal && restored <= 0f) throw new QaFailException(item.Id + " was used but restored no health.");
            record["boost_seconds"] = player.BoostLeft;
            if (item.Use.Effect == UseEffect.Speed && player.BoostLeft <= 0f) throw new QaFailException(item.Id + " was used but gave no speed boost.");
        }

        IEnumerator Shoot(QaContext context, HeldWeapon gear, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            var dummy = director.Dummy;
            if (!dummy) throw new QaFailException("The range dummy is missing.");
            dummy.Respawn(dummyHome);
            dummy.Body.linearVelocity = Vector3.zero;
            dummy.FaceTowards(Vector3.back);
            Stand(dummyHome - Vector3.forward * ShotGap, Vector3.forward);
            yield return context.WaitSeconds(0.5f);
            float before = dummy.Health.Current;
            input.Next.attack = true;
            input.Next.attackHeld = true;
            try
            {
                yield return context.WaitUntil(() => gear.DrawPower >= 1f, item.Ranged.DrawSeconds + 3f, item.Id + " to reach a full draw");
                yield return Capture(context, name + "_used");
            }
            finally { input.Next.attackHeld = false; }
            yield return context.WaitUntil(() => dummy.Health.Current < before, ShotSeconds, item.Id + " shot to land");
            record["dummy_damage"] = before - dummy.Health.Current;
            record["durability_left"] = gear ? gear.DurabilityLeft : 0f;
        }

        IEnumerator Wall(QaContext context, HeldWeapon gear, ItemDefinition item, Dictionary<string, object> record)
        {
            float lifetime = item.Deploy.LifetimeSeconds;
            var refusal = DoorwayRefusal(item.Deploy.FootprintX, item.Deploy.FootprintZ);
            record["wall_lifetime"] = lifetime;
            record["wall_doorway_refusal"] = refusal.ToString();
            if (refusal == HelperRefusal.None) throw new QaFailException(item.Id + " would be allowed in a " + RefusalHouse + " doorway.");
            yield return context.WaitUntil(() => !gear, lifetime + WallSlack + 1f, item.Id + " to come down after " + lifetime + " s");
            float lived = Time.time - placedAt;
            record["wall_lived"] = lived;
            if (Mathf.Abs(lived - lifetime) > WallSlack) throw new QaFailException($"{item.Id} stood {lived:0.00} s, not its {lifetime} s.");
        }

        static HelperRefusal DoorwayRefusal(float sizeX, float sizeZ)
        {
            var layout = GameConfig.Current.HouseFor(RefusalHouse);
            if (layout == null || layout.Doors.Count == 0) throw new QaFailException(RefusalHouse + " has no doorway to test against.");
            var door = layout.Doors[0];
            var room = layout.Room(door.A);
            var footprint = HelperFootprint.Around(door.X, door.Z, sizeX, sizeZ, 0f);
            return HelperPlacement.Check(layout, HelperPlaceKind.Wall, door.X, room != null ? room.FloorY : 0f, door.Z, footprint, null, null);
        }

        static HelperRefusal SpawnRefusal(float size)
        {
            var layout = GameConfig.Current.HouseFor(RefusalHouse);
            if (layout == null || layout.Spawns.Count == 0) throw new QaFailException(RefusalHouse + " has no spawn point to test against.");
            var spawn = layout.Spawns[0];
            var room = layout.Room(spawn.Room);
            var footprint = HelperFootprint.Around(spawn.X, spawn.Z, size, size, 0f);
            return HelperPlacement.Check(layout, HelperPlaceKind.Stayer, spawn.X, room != null ? room.FloorY : 0f, spawn.Z, footprint, null, null);
        }

        float TimeoutFor(string id)
        {
            if (!GameConfig.Current.Items.TryGet(id, out var item)) return StepSeconds;
            if (HelperAgent.SpecFor(item, out var spec)) return StepSeconds + spec.Seconds + HelperFrameSheet.Seconds;
            return HelperBook.IsWall(item) ? StepSeconds + item.Deploy.LifetimeSeconds : StepSeconds;
        }

        void OnLaunched(HelperAgent agent)
        {
            if (agent && agent.Maker == player && !helper) helper = agent;
        }

        void OnArmed(HelperAgent agent)
        {
            if (agent == helper && armedAt < 0f) armedAt = Time.time - agent.LaunchedAt;
        }

        void OnStruck(HelperAgent agent, PlayerController victim)
        {
            if (agent != helper) return;
            strikes++;
            if (victim == player) makerBitten++;
            if (firstStrikeAt >= 0f) return;
            firstStrikeAt = Time.time - agent.LaunchedAt;
            if (pauseOnStrike) Time.timeScale = 0f;
        }

        void OnEnded(HelperAgent agent, HelperEnd end)
        {
            if (agent != helper || endedAt >= 0f) return;
            ended = end;
            endedAt = Time.time - agent.LaunchedAt;
        }

        IEnumerator Release(QaContext context, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            if (!HelperAgent.SpecFor(item, out var spec)) throw new QaFailException(item.Id + " is a Helper recipe without a helper block.");
            var dummy = director.Dummy;
            if (!dummy) throw new QaFailException("The range dummy is missing.");
            dummy.Respawn(dummyHome);
            dummy.Body.linearVelocity = Vector3.zero;
            dummy.FaceTowards(Vector3.back);
            float gap = spec.IsTrap ? TrapGap : spec.Moves ? MoverGap : Mathf.Clamp(Mathf.Min(spec.Range, spec.Radius) * .6f, .4f, 2f);
            Stand(dummyHome - Vector3.forward * (PlayerCombat.DeployReach + gap), Vector3.forward);
            yield return context.WaitSeconds(0.5f);
            yield return context.WaitUntil(() => !player.Health.IsInvulnerable, 5, "spawn protection to end");
            record["body"] = spec.Body.ToString();
            record["pick"] = spec.Pick.ToString();
            record["bites"] = spec.Bites.ToString();
            record["attack"] = spec.Attack.ToString();
            record["life"] = spec.Seconds;
            if (spec.IsTrap)
            {
                var refusal = SpawnRefusal(Mathf.Max(item.Size[0], item.Size[2]));
                record["spawn_refusal"] = refusal.ToString();
                if (refusal == HelperRefusal.None) throw new QaFailException(item.Id + " would be allowed on a " + RefusalHouse + " spawn point.");
            }
            helper = null;
            armedAt = firstStrikeAt = endedAt = -1f;
            strikes = makerBitten = 0;
            ended = HelperEnd.None;
            pauseOnStrike = false;
            string sheetFailure = null;
            bool sheetDone = false;
            int sheetFrames = 0;
            float sheetSpan = 0f;
            HelperAgent.Launched += OnLaunched;
            HelperAgent.Armed += OnArmed;
            HelperAgent.Struck += OnStruck;
            HelperAgent.Ended += OnEnded;
            try
            {
                input.Next.attack = true;
                yield return context.WaitUntil(() => helper, HelperAgent.PlaceSeconds + 3f, item.Id + " to hop out of the hand");
                if (player.Combat.Weapon) throw new QaFailException(item.Id + " was released but is still in the hand.");
                WatchFrom(helper.transform.position);
                director.StartCoroutine(HelperFrameSheet.Record(context, name + "_motion", f => { sheetFailure = f; sheetDone = true; },
                    (n, s) => { sheetFrames = n; sheetSpan = s; sheetDone = true; }));
                yield return context.WaitUntil(() => armedAt >= 0f || ended != HelperEnd.None, HelperBrain.ArmSeconds + 2f, item.Id + " to arm");
                if (armedAt < 0f) throw new QaFailException($"{item.Id} ended ({ended}) before it armed.");
                yield return Capture(context, name + "_armed");
                if (spec.Pick == HelperPick.Provoker) player.Health.ApplyDamage(Hits.Of(dummy, player.Facing, HitSource.Unarmed, ProvokeDamage, 0f));
                if (spec.IsTrap && helper)
                {
                    dummy.Respawn(helper.transform.position);
                    dummy.Body.linearVelocity = Vector3.zero;
                }
                float makerBefore = player.Health.Current;
                if (spec.Hurts)
                {
                    pauseOnStrike = true;
                    yield return context.WaitUntil(() => firstStrikeAt >= 0f || ended != HelperEnd.None, spec.Seconds + HelperAgent.LifeGrace + 1f, item.Id + " to strike");
                    if (firstStrikeAt < 0f) throw new QaFailException($"{item.Id} ended ({ended}) without a strike on the dummy.");
                    try
                    {
                        yield return null;
                        yield return Capture(context, name + "_strike");
                    }
                    finally
                    {
                        pauseOnStrike = false;
                        Time.timeScale = 1f;
                    }
                }
                yield return context.WaitUntil(() => sheetDone, HelperFrameSheet.Seconds + 4f, item.Id + " motion frame sheet");
                if (sheetFailure != null) throw new QaFailException(sheetFailure);
                yield return context.WaitUntil(() => ended != HelperEnd.None, spec.Seconds + HelperAgent.LifeGrace + HelperAgent.EndSeconds + 2f, item.Id + " to end");
                yield return Capture(context, name + "_end");
                record["armed_at"] = armedAt;
                record["first_strike_at"] = firstStrikeAt;
                record["strikes"] = strikes;
                record["ended_reason"] = ended.ToString();
                record["ended_at"] = endedAt;
                record["maker_bitten"] = makerBitten;
                record["maker_damage"] = makerBefore - player.Health.Current;
                record["sheet_frames"] = sheetFrames;
                record["sheet_seconds"] = sheetSpan;
                if (spec.Bites == HelperBite.Enemies && (makerBitten > 0 || player.Health.Current < makerBefore))
                    throw new QaFailException(item.Id + " bites foes only but hurt its maker.");
                int most = spec.Attack == HelperAttack.Burst ? HelperSpec.BurstTargets : spec.Strikes;
                if (strikes > most) throw new QaFailException($"{item.Id} landed {strikes} hits, more than its {most}.");
                if (Mathf.Abs(armedAt - HelperBrain.ArmSeconds) > ArmSlack) throw new QaFailException($"{item.Id} armed after {armedAt:0.00} s, not {HelperBrain.ArmSeconds} s.");
            }
            finally
            {
                HelperAgent.Launched -= OnLaunched;
                HelperAgent.Armed -= OnArmed;
                HelperAgent.Struck -= OnStruck;
                HelperAgent.Ended -= OnEnded;
                pauseOnStrike = false;
                Time.timeScale = 1f;
                if (helper && !helper.IsEnding) helper.Stop(HelperEnd.Life);
            }
        }

        IEnumerator Pack(QaContext context, ItemDefinition item, string name, Dictionary<string, object> record)
        {
            if (!GameConfig.Current.BagRows.TryGet(item.Id, out var spec)) throw new QaFailException(item.Id + " is a bag recipe without a bag block.");
            string modeBefore = Match.ModeOverride;
            Match.ModeOverride = HouseModes.Dibs;
            try
            {
                player.Inventory.ClearBonusRows();
                var first = director.EquipCatalogue(player, BagHandA);
                var second = CatalogGear.Create(GameConfig.Current.Items.Get(BagHandB));
                if (!first || !player.Combat.TryEquip(second) || player.Combat.HasFreeGearSlot)
                    throw new QaFailException(item.Id + " needs both hands full before it is spelled.");
                player.Inventory.Set(item.Id);
                yield return context.WaitSeconds(0.6f);
                int capacity = player.Inventory.Capacity, rows = player.Inventory.BonusRows, shown = player.Inventory.ShownRows;
                yield return Capture(context, name + "_before");
                if (!player.Summoner.Summon(player.Summoner.Recipe(item.Id))) throw new QaFailException(item.Id + " could not be spelled with both hands full.");
                yield return context.WaitUntil(() => !player.Summoner.IsCrafting, 5, item.Id + " to be spelled");
                yield return context.WaitSeconds(0.6f);
                yield return Capture(context, name + "_after");
                bool kept = player.Combat.GearIn(0) == first && player.Combat.GearIn(1) == second || player.Combat.GearIn(0) == second && player.Combat.GearIn(1) == first;
                record["rows_added"] = spec.Rows;
                record["capacity_before"] = capacity;
                record["capacity_after"] = player.Inventory.Capacity;
                record["bonus_rows_before"] = rows;
                record["bonus_rows_after"] = player.Inventory.BonusRows;
                record["rows_shown_before"] = shown;
                record["rows_shown_after"] = player.Inventory.ShownRows;
                record["letters_left"] = player.Inventory.Count;
                record["hands_unchanged"] = kept;
                if (player.Inventory.Count != 0) throw new QaFailException($"{item.Id} left {player.Inventory.Count} of its letters unspent.");
                if (player.Inventory.BonusRows != rows + spec.Rows) throw new QaFailException($"{item.Id} gave {player.Inventory.BonusRows - rows} rows, not {spec.Rows}.");
                if (player.Inventory.Capacity != capacity + spec.Rows * BagRows.SlotsPerRow)
                    throw new QaFailException($"{item.Id} took the bag from {capacity} to {player.Inventory.Capacity} slots, not {capacity + spec.Rows * BagRows.SlotsPerRow}.");
                if (!kept) throw new QaFailException(item.Id + " changed what the hands hold.");
            }
            finally
            {
                Match.ModeOverride = modeBefore;
                if (player) player.Inventory.ClearBonusRows();
            }
        }

        IEnumerator Return(QaContext context)
        {
            Time.timeScale = 1f;
            Application.logMessageReceived -= Log;
            if (player) player.Combat.Thrown -= Counted;
            context.Note("house_items_used", records.ToArray());
            Session.GoHome();
            yield return context.WaitUntil(() => SceneManager.GetActiveScene().name == Session.HubScene && Object.FindFirstObjectByType<LobbyMenu>(), 30, "the Hub");
        }

        void Stand(Vector3 at, Vector3 direction)
        {
            player.Respawn(at);
            player.Body.linearVelocity = Vector3.zero;
            Aim(direction);
        }

        void WatchFrom(Vector3 focus)
        {
            var at = focus - Vector3.forward * WatchBack + Vector3.right * WatchSide;
            at.y = player.transform.position.y;
            player.Body.position = at;
            player.transform.position = at;
            player.Body.linearVelocity = Vector3.zero;
            var look = focus - at;
            look.y = 0f;
            Aim(Quaternion.AngleAxis(WatchTurn, Vector3.up) * look.normalized);
        }

        void Aim(Vector3 direction)
        {
            player.FaceTowards(direction);
            player.LookYaw = Mathf.Atan2(direction.x, direction.z);
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
    }
}
#endif
