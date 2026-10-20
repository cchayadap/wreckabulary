using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        [UnityTest, Timeout(600000), Category("ExternalPhotonGameplay")]
        public IEnumerator HostAndRemoteHudRefreshDefenseAndReleaseForKeySpellMenuFocusAndSelection()
        {
            yield return Run(2, false, hudInteractions: true);
            yield return HudDefenseInteractions(peers.Single(p => p.Runner.IsServer));
        }

        IEnumerator HudDefenseInteractions(Peer host)
        {
            for (int lane = 0; lane < peers.Count; lane++)
            {
                var peer = peers[lane];
                int seat = WordboundGameplayPlan.Seat(host.Controller.Frame.Binding, peer.Actor);
                foreach (string recipe in new[] { "SHIELD", "PLATE" })
                {
                    string source = live.Course.Supplies.Single(s => s.SourceId == "Hud" + recipe + lane).Id;
                    var tiles = host.Controller.Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == source)
                        .Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                    Assert.AreEqual(recipe.Length, tiles.Length);
                    foreach (string tile in tiles)
                    {
                        yield return WalkTo(host, peer, () => Pose(host, tile).Position, 1.1f,
                            "Walk to real " + recipe + " preparation letters");
                        yield return Command(host, peer, WordboundAction.CollectLetter, tile);
                    }
                    yield return Command(host, peer, WordboundAction.BeginCraft, value: recipe);
                    yield return Until(() => host.Controller.Frame.Economy.Items.Any(i => i.Word == recipe &&
                        i.CrafterId == seat && i.Origin == ItemOrigin.Crafted && i.State == ItemState.Held),
                        "Complete the actual " + recipe + " craft timer", 20);
                }
                var shield = host.Controller.Frame.Economy.Items.Single(i => i.Word == "SHIELD" && i.CrafterId == seat);
                var plate = host.Controller.Frame.Economy.Items.Single(i => i.Word == "PLATE" && i.CrafterId == seat);
                int shieldSlot = Array.IndexOf(Inventory(host, peer).Slots, shield.Id);
                int plateSlot = Array.IndexOf(Inventory(host, peer).Slots, plate.Id);
                Assert.GreaterOrEqual(shieldSlot, 0);
                Assert.GreaterOrEqual(plateSlot, 0);
                Assert.AreNotEqual(shieldSlot, plateSlot);
                yield return CommittedState(host, frame => frame.Economy.Items.Any(i => i.Id == shield.Id && i.State == ItemState.Held) &&
                    frame.Economy.Items.Any(i => i.Id == plate.Id && i.State == ItemState.Held),
                    "Replicate the actual held shield and plate before HUD selection");

                var ui = new GameObject(Prefix + "DefenseHUD-" + lane);
                ui.transform.SetParent(peer.Controller.transform, false);
                var cameraRoot = new GameObject("Native defense input camera");
                cameraRoot.transform.SetParent(ui.transform, false);
                var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
                var view = ui.AddComponent<WordboundPlayerView>();
                view.Initialize(peer.Controller, camera, new HudIdleBinding());
                view.LocalPlayerChanged(Bodies(peer).Single(b => b.ActorId.ToString() == peer.Actor));
                view.enabled = false;
                var hud = ui.AddComponent<WordboundMatchHud>();
                hud.Initialize(peer.Controller, view, live.Items, live.Mode, live.Course, peer.Actor);
                hud.enabled = false;
                try
                {
                    foreach (string releasePath in new[] { "Key", "Spell", "Menu", "Focus", "Selection" })
                    {
                        view.SetMenuOpen(false);
                        InvokeHud(hud, "MenuRequested");
                        long beforeEquip = host.Controller.Frame.Commands.Frontiers[peer.Actor];
                        InvokeHud(hud, "ReadCommands", new PlayerCommands { slot = shieldSlot + 1 });
                        string equipHash = HudPayload(peer, WordboundAction.Equip, slot: shieldSlot);
                        yield return Until(() => HudReceipt(host, peer, beforeEquip, equipHash) != null,
                            "Apply actual HUD shield slot selection", 15);
                        Assert.AreEqual("Ok", HudReceipt(host, peer, beforeEquip, equipHash).Effect.Code);
                        yield return CommittedState(host, frame => frame.Actors.Single(a => a.PlayerId == peer.Actor).EquippedSlot == shieldSlot,
                            "Replicate HUD shield selection");
                        yield return HoldHudDefense(host, peer, hud, "item:" + shield.Id);

                        long beforeRelease = host.Controller.Frame.Commands.Frontiers[peer.Actor];
                        long leaseEnd = host.Controller.Frame.Actors.Single(a => a.PlayerId == peer.Actor).Defense.UntilTick;
                        switch (releasePath)
                        {
                            case "Key": InvokeHud(hud, "ReadCommands", new PlayerCommands()); break;
                            case "Spell": InvokeHud(hud, "ReadCommands", new PlayerCommands { spellDown = true }); break;
                            case "Menu": view.SetMenuOpen(true); InvokeHud(hud, "MenuRequested"); break;
                            case "Focus":
                                view.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver);
                                InvokeHud(hud, "Update");
                                break;
                            case "Selection": InvokeHud(hud, "ReadCommands", new PlayerCommands { slot = plateSlot + 1 }); break;
                        }
                        if (releasePath == "Spell" || releasePath == "Menu" || releasePath == "Focus")
                            Assert.IsTrue(view.MenuOpen, releasePath + " must enter the actual player-view menu state.");
                        string releaseHash = HudPayload(peer, WordboundAction.Block, value: "Release");
                        yield return Until(() => HudReceipt(host, peer, beforeRelease, releaseHash) != null,
                            releasePath + " emits an authenticated HUD Block/Release", 15);
                        var receipt = HudReceipt(host, peer, beforeRelease, releaseHash);
                        Assert.AreEqual("Ok", receipt.Effect.Code, releasePath);
                        Assert.Less(receipt.Tick, leaseEnd, releasePath + " must clear defense before its prior lease expires.");
                        if (releasePath == "Selection")
                        {
                            string plateHash = HudPayload(peer, WordboundAction.Equip, slot: plateSlot);
                            yield return Until(() => HudReceipt(host, peer, beforeRelease, plateHash) != null,
                                "Apply the HUD's new selected equipment", 15);
                            var equip = HudReceipt(host, peer, beforeRelease, plateHash);
                            Assert.AreEqual("Ok", equip.Effect.Code);
                            Assert.Greater(equip.Sequence, receipt.Sequence, "HUD must release the old shield before selecting the plate.");
                        }
                        yield return CommittedState(host, frame => frame.Actors.Single(a => a.PlayerId == peer.Actor).Defense == null &&
                            (releasePath != "Selection" || frame.Actors.Single(a => a.PlayerId == peer.Actor).EquippedSlot == plateSlot),
                            "Commit " + releasePath + " release to both authenticated peers");
                        yield return Until(() => host.Controller.Frame.Progress.Tick > receipt.Tick + WordboundDefensePolicy.HoldTicks,
                            "Observe no stale defense refresh after " + releasePath, 10);
                        Assert.IsNull(host.Controller.Frame.Actors.Single(a => a.PlayerId == peer.Actor).Defense);
                        string holdHash = HudPayload(peer, WordboundAction.Block, value: "Hold");
                        Assert.IsFalse(host.Controller.Frame.Commands.Receipts.Any(r => r.ActorId == peer.Actor &&
                            r.Sequence > receipt.Sequence && r.PayloadHash == holdHash), releasePath + " must stop held-input refreshes.");
                    }
                }
                finally { UnityEngine.Object.Destroy(ui); }
                yield return null;
            }
            Debug.Log("WORDBOUND_HUD_DEFENSE_OBSERVED: host and remote crafted real SHIELD/PLATE; actual HUD held input renewed canonical defense beyond 30 ticks and key/spell/menu/focus/selection emitted successful Release before expiry. Focus is an explicit Unity callback, not an OS focus test; cleanup pending.");
        }

        IEnumerator HoldHudDefense(Peer host, Peer peer, WordboundMatchHud hud, string shieldId)
        {
            long before = host.Controller.Frame.Commands.Frontiers[peer.Actor], started = -1;
            int observedTicks = 0;
            string failure = null;
            string hash = HudPayload(peer, WordboundAction.Block, value: "Hold");
            void Observe(MatchSnapshot frame)
            {
                var defense = frame.Actors.Single(a => a.PlayerId == peer.Actor).Defense;
                if (defense == null)
                {
                    if (started >= 0) failure = failure ?? "Held defense expired between actual HUD refreshes.";
                    return;
                }
                if (started < 0) started = defense.StartedTick;
                observedTicks++;
                if (defense.ItemEntityId != shieldId || defense.StartedTick != started || defense.UntilTick <= frame.Progress.Tick)
                    failure = failure ?? "HUD refresh changed the shield/raise start or left an expired lease.";
            }
            host.Controller.FrameChanged += Observe;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 15;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    Healthy();
                    InvokeHud(hud, "ReadCommands", new PlayerCommands { blockHeld = true });
                    Assert.IsNull(failure);
                    var frame = host.Controller.Frame;
                    var defense = frame.Actors.Single(a => a.PlayerId == peer.Actor).Defense;
                    var receipts = frame.Commands.Receipts.Where(r => r.ActorId == peer.Actor && r.Sequence > before && r.PayloadHash == hash).ToArray();
                    foreach (var receipt in receipts) Assert.AreEqual("Ok", receipt.Effect.Code, "HUD Block/Hold must be accepted.");
                    if (started >= 0 && frame.Progress.Tick - started > WordboundDefensePolicy.HoldTicks &&
                        defense != null && defense.UntilTick - frame.Progress.Tick >= WordboundDefensePolicy.HoldTicks - WordboundDefensePolicy.RefreshTicks &&
                        receipts.Select(r => r.Tick).Distinct().Count() >= 3) break;
                    yield return null;
                }
                Assert.IsNull(failure);
                var final = host.Controller.Frame;
                var active = final.Actors.Single(a => a.PlayerId == peer.Actor).Defense;
                Assert.IsNotNull(active, "HUD hold must leave an active canonical defense state.");
                Assert.Greater(final.Progress.Tick - started, WordboundDefensePolicy.HoldTicks);
                Assert.Greater(observedTicks, WordboundDefensePolicy.HoldTicks);
                Assert.IsTrue(WordboundDefensePolicy.Raised(active, live.Items.Get("SHIELD").Shield, final.Progress.Tick));
                Assert.GreaterOrEqual(final.Commands.Receipts.Where(r => r.ActorId == peer.Actor && r.Sequence > before && r.PayloadHash == hash)
                    .Select(r => r.Tick).Distinct().Count(), 3, "At least three actual successful refresh commands are required.");
                Assert.GreaterOrEqual(active.UntilTick - final.Progress.Tick,
                    WordboundDefensePolicy.HoldTicks - WordboundDefensePolicy.RefreshTicks, "Release needs a fresh, still-live lease.");
            }
            finally { host.Controller.FrameChanged -= Observe; }
        }

        static string HudPayload(Peer peer, WordboundAction action, int slot = -1, string value = "") =>
            new PlayerCommand(1, peer.Controller.Frame.Epoch, action, slot: slot, value: value).PayloadHash;

        static CommandReceipt HudReceipt(Peer host, Peer peer, long after, string hash) =>
            host.Controller.Frame.Commands.Receipts.Where(r => r.ActorId == peer.Actor && r.Sequence > after && r.PayloadHash == hash)
                .OrderBy(r => r.Sequence).FirstOrDefault();
    }
}
