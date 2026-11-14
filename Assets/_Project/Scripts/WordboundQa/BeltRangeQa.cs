#if UNITY_EDITOR || GSK_QA
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameStudioKit.Capture;
using GameStudioKit.Qa;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Audio;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class BeltRangeQa : IQaScenario
    {
        public string Name => "belt_range";
        BeltRangeDirector director;
        PlayerController player;
        ScriptedBinding input;
        readonly List<ImpactPlayback> played = new();
        readonly List<Dictionary<string, object>> swings = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register("belt_range", () => new BeltRangeQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("belt_open").Timeout(60).When(_ => Object.FindFirstObjectByType<LobbyMenu>() != null).Do(Open);
            yield return QaStep.Named("belt_held").Timeout(30).Do(Held);
            yield return QaStep.Named("belt_strap_glass").Timeout(30).Do(StrapGlass);
            yield return QaStep.Named("belt_buckle_crate").Timeout(30).Do(BuckleCrate);
            yield return QaStep.Named("belt_miss").Timeout(30).Do(Miss);
            yield return QaStep.Named("belt_skins").Timeout(30).Do(Skins);
            yield return QaStep.Named("belt_throw").Timeout(30).Do(Throw);
            yield return QaStep.Named("belt_bat_break").Timeout(40).Do(BatBreak);
            yield return QaStep.Named("belt_return").Timeout(40).Do(Return);
        }

        void Record(ImpactPlayback p) => played.Add(p);

        IEnumerator Open(QaContext context)
        {
            if (ImpactAudioPlayer.Silent) throw new QaFailException("Sound is muted in settings; unmute to verify impact sounds.");
            Session.Load(BeltRangeDirector.SceneName);
            yield return context.WaitUntil(() => (director = Object.FindFirstObjectByType<BeltRangeDirector>()) != null, 30, "the BELT range scene");
            ImpactAudioPlayer.Played += Record;
            input = new ScriptedBinding();
            player = director.Joins.Join(input);
            Object.FindFirstObjectByType<CameraRig>()?.Follow(player);
            var hud = GameHud.Active;
            hud.Follow(player);
            yield return context.WaitSeconds(1f);
            yield return context.WaitUntil(() => player.CanAct && player.Combat.Weapon, 10, "a player holding BELT");
            var minimap = hud.transform.Find("Safe HUD/Side column/Minimap");
            if (hud.LocalPlayer != player || hud.LocalPlayer == director.Dummy) throw new QaFailException("The HUD shows someone other than the player.");
            if (!minimap || !minimap.gameObject.activeInHierarchy) throw new QaFailException("The range has no minimap.");
            context.Note("hud_minimap", true);
            var belt = director.Belt.Definition();
            context.Note("belt_identity", belt.Identity());
            context.Note("belt_catalogue_absent", !GameConfig.Current.Items.TryGet("BELT", out _));
            context.Note("belt_scope", "Offline Windows BELT range; scripted input; no remote peers, online or human-feel claim.");
        }

        IEnumerator Held(QaContext context)
        {
            var weapon = player.Combat.Weapon;
            var flex = weapon.GetComponent<BeltFlexDriver>();
            if (weapon.word != "BELT" || !flex.Ready || weapon.Definition.Enabled) throw new QaFailException("BELT is not held as an isolated rigged prototype.");
            Aim(Vector3.forward);
            yield return context.WaitSeconds(0.8f);
            yield return Capture(context, "belt_held");
            context.Note("belt_held_droop_degrees", flex.Angles.Sum());
            float drop = flex.Root.position.y - flex.Tip.position.y;
            context.Note("belt_held_tip_below_grip_metres", drop);
            if (drop < 0.15f) throw new QaFailException($"The held strap should hang from the hand; its tip is only {drop:0.00} m below the grip.");
        }

        IEnumerator StrapGlass(QaContext context)
        {
            var glass = Target("GLASS");
            Stand(glass, 0.6f);
            yield return context.WaitFrames(3);
            played.Clear();
            var melee = director.Belt.Definition().Melee;
            yield return Swing(context, "belt_strap_glass", melee.Windup * 0.6f, melee.Windup + melee.Active * 0.4f);
            Expect(context, "belt_strap_glass", "swing/BELT", "contact/BELT/Strap/Glass", "damage/Glass");
            if (!glass || glass.IsBroken || glass.Remaining >= 1f) throw new QaFailException("The strap should crack the glass without breaking it.");
        }

        IEnumerator BuckleCrate(QaContext context)
        {
            var crate = Target("CRATE");
            var shaper = player.Combat.Weapon.GetComponent<BeltWeapon>();
            Stand(crate, shaper.Definition.Melee.Reach - 0.06f);
            yield return context.WaitFrames(3);
            float before = crate.Health;
            played.Clear();
            var melee = shaper.Definition.Melee;
            yield return Swing(context, "belt_buckle_crate", -1f, melee.Windup + melee.Active * 0.4f);
            Expect(context, "belt_buckle_crate", "swing/BELT", "contact/BELT/Buckle/Wood", "damage/Wood");
            if (shaper.LastPart != StrikePart.Buckle || Mathf.Abs(before - crate.Health - shaper.Definition.BuckleMelee.BreakPower * Smashable.HealthPerBreakPower) > 0.01f)
                throw new QaFailException("The outer end did not land as a buckle hit.");
        }

        IEnumerator Miss(QaContext context)
        {
            player.Respawn(new Vector3(0f, 0f, -4.5f));
            Aim(Vector3.back);
            yield return context.WaitFrames(3);
            played.Clear();
            yield return Swing(context, "belt_miss", -1f, -1f);
            Expect(context, "belt_miss", "swing/BELT");
        }

        IEnumerator Skins(QaContext context)
        {
            Stand(Target("PAN"), 1.2f);
            Aim(Vector3.forward);
            foreach (string skin in new[] { "Candy", "Arcade" })
            {
                if (director.CycleSkin(player) != skin) throw new QaFailException("Skin order changed.");
                yield return context.WaitFrames(2);
                var leather = player.Combat.Weapon.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials[0];
                if (leather != director.Belt.LeatherFor(skin)) throw new QaFailException(skin + " leather did not apply.");
                yield return Capture(context, "belt_skin_" + skin.ToLowerInvariant());
            }
            director.CycleSkin(player);
        }

        IEnumerator Throw(QaContext context)
        {
            var weapon = player.Combat.Weapon;
            director.Throw(player);
            yield return context.WaitSeconds(0.6f);
            if (player.Combat.Weapon || weapon.IsHeld) throw new QaFailException("Thrown BELT stayed in the hand.");
            yield return Capture(context, "belt_thrown");
            if (!director.EquipBelt(player)) throw new QaFailException("BELT could not be equipped again.");
        }

        IEnumerator BatBreak(QaContext context)
        {
            if (!director.EquipCatalogue(player, "BAT")) throw new QaFailException("BAT could not be equipped.");
            var vase = Target("VASE");
            Stand(vase, 0.6f);
            yield return context.WaitFrames(3);
            played.Clear();
            yield return Swing(context, "bat_vase", -1f, -1f);
            Expect(context, "bat_vase", "swing/BAT", "contact/BAT/Any/Ceramic", "break/Ceramic");
            yield return Capture(context, "bat_vase_broken");
            yield return context.WaitUntil(() => Target("VASE") && !Target("VASE").IsBroken, 6, "the vase to return");
        }

        IEnumerator Return(QaContext context)
        {
            ImpactAudioPlayer.Played -= Record;
            context.Note("belt_swings", swings.ToArray());
            Session.GoHome();
            yield return context.WaitUntil(() => SceneManager.GetActiveScene().name == Session.HubScene && Object.FindFirstObjectByType<LobbyMenu>(), 30, "the Hub");
        }

        Smashable Target(string word) => director.LiveTarget(director.Targets.Single(t => t.word == word));

        void Aim(Vector3 direction)
        {
            player.FaceTowards(direction);
            player.LookYaw = Mathf.Atan2(direction.x, direction.z);
        }

        void Stand(Smashable target, float distance)
        {
            if (!target) throw new QaFailException("Range target missing.");
            var bounds = target.GetComponentsInChildren<Collider>().Select(c => c.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            player.Respawn(new Vector3(bounds.center.x, 0f, bounds.min.z - distance));
            player.Body.linearVelocity = Vector3.zero;
            Aim(Vector3.forward);
        }

        IEnumerator Swing(QaContext context, string name, float windupAt, float contactAt)
        {
            var weapon = player.Combat.Weapon;
            var flex = weapon.GetComponent<BeltFlexDriver>();
            input.Next.attack = true;
            yield return context.WaitUntil(() => weapon.SwingStartedAt >= 0f, 3, "the swing to start");
            float start = weapon.SwingStartedAt;
            var record = new Dictionary<string, object> { ["name"] = name, ["weapon"] = weapon.word };
            foreach (var (label, at) in new[] { ("windup", windupAt), ("contact", contactAt) })
            {
                if (at < 0f) continue;
                yield return context.WaitUntil(() => Time.time - start >= at, 3, name + " " + label);
                Time.timeScale = 0f;
                yield return null;
                if (flex) record[label + "_degrees"] = flex.Angles.ToArray();
                if (flex) record[label + "_phase"] = flex.Phase.ToString();
                yield return Capture(context, name + "_" + label);
                Time.timeScale = 1f;
            }
            yield return context.WaitUntil(() => weapon.SwingStartedAt < 0f, 3, "the swing to finish");
            yield return context.WaitSeconds(0.15f);
            record["played"] = played.Select(p => p.Key + " " + p.Clip + $" v{p.Volume:0.00} p{p.Pitch:0.00}").ToArray();
            swings.Add(record);
        }

        void Expect(QaContext context, string name, params string[] keys)
        {
            var got = played.Select(p => p.Key).ToArray();
            context.Note(name + "_sounds", got);
            bool sameSwing = got.Length == keys.Length && got[0] == keys[0];
            if (!sameSwing || !got.Skip(1).OrderBy(k => k).SequenceEqual(keys.Skip(1).OrderBy(k => k))) throw new QaFailException($"{name}: expected sounds [{string.Join(", ", keys)}], played [{string.Join(", ", got)}].");
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
