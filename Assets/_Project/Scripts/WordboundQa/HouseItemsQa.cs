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
        public string Name => "house_items";
        const int MaxItems = 20;
        static readonly Vector3 Lane = new Vector3(-5.5f, 0f, -2.6f);
        BeltRangeDirector director;
        PlayerController player;
        ScriptedBinding input;
        Vector3 dummyHome;
        int throws;
        List<string> ids = new();
        readonly List<string> errors = new();
        readonly List<Dictionary<string, object>> records = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register("house_items", () => new HouseItemsQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            ids = data && data.houseItems ? ItemCatalogue.FromJson(data.houseItems.text, "items_house.json").All.Select(i => i.Id).ToList() : new List<string>();
            yield return QaStep.Named("house_open").Timeout(60).When(_ => Object.FindFirstObjectByType<LobbyMenu>() != null).Do(Open);
            foreach (string id in ids)
                yield return QaStep.Named("house_" + id.ToLowerInvariant()).Timeout(30).Do(c => Exercise(c, id));
            yield return QaStep.Named("house_return").Timeout(40).Do(Return);
        }

        void Log(string condition, string stack, LogType type)
        {
            if (type is LogType.Exception or LogType.Error or LogType.Assert) errors.Add(condition);
        }

        void Counted(Rigidbody _) => throws++;

        IEnumerator Open(QaContext context)
        {
            if (ids.Count == 0) throw new QaFailException("GameData lists no items_house.json recipes. Run Wreckabulary > Data > Set Up Game Data.");
            if (ids.Count > MaxItems) throw new QaFailException($"{ids.Count} House items would take {ids.Count * 2} captures; split the scenario.");
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
            context.Note("house_items_shared_enabled", ids.Where(id => shared.TryGet(id, out var s) && s.Enabled).ToArray());
            context.Note("house_items_scope", "items_house scope: House-mode recipes layered on items.json, exercised in the offline Windows BELT range " +
                "with scripted input. Wordbound and online read items.json only; no network, remote peer or human-feel claim.");
        }

        IEnumerator Exercise(QaContext context, string id)
        {
            errors.Clear();
            var item = GameConfig.Current.Items.Get(id);
            string name = "house_" + id.ToLowerInvariant();
            Stand(Lane, Vector3.right);
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
                    yield return Place(context, gear, item, name, record);
                    break;
                default:
                    throw new QaFailException($"{id}: no House QA use for {item.Family}.");
            }
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
            yield return context.WaitFrames(2);
            if (!gear || player.Combat.Weapon || !gear.GetComponent<DeployedGear>()) throw new QaFailException(item.Id + " did not leave the hand as placed gear.");
            var offset = World.Flat(gear.GetComponent<Rigidbody>().position - player.Body.position);
            var effect = item.Deploy.Effect;
            record["effect"] = effect.ToString();
            record["placed_metres"] = offset.magnitude;
            if (Vector3.Dot(offset.normalized, player.Facing) < 0.9f) throw new QaFailException(item.Id + " was not placed in front.");
            bool field = effect is DeployEffect.WindField or DeployEffect.SlowField, pad = effect is DeployEffect.JumpPad or DeployEffect.SpeedStrip;
            if ((field && !gear.GetComponent<DeployedPowerField>()) || (pad && !gear.GetComponentInChildren<GearTrigger>()))
                throw new QaFailException(item.Id + " was placed without its " + effect + " effect.");
            yield return context.WaitSeconds(0.5f);
            yield return Capture(context, name + "_used");
            Object.Destroy(gear.gameObject);
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
