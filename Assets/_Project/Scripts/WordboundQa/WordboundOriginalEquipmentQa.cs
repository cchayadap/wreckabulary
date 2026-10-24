#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameStudioKit.Capture;
using GameStudioKit.Qa;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class WordboundOriginalEquipmentQa : IQaScenario
    {
        public string Name => "wordbound_original_equipment";
        const string Actor = WordboundLocalMatchClient.LocalActor;
        internal static readonly string[] Props = { "BAT", "HAMMER", "FAN", "SOFA" };
        WordboundLauncher launcher;
        WordboundWorkshop workshop;
        WordboundLocalMatchClient client;
        WordboundOriginalEquipmentView observation;
        WorkshopHistory history;
        ItemCatalogue items;
        string draft, manifest;
        readonly List<object> receipts = new List<object>();
        bool worldCaptured, batCaptured, hammerCaptured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register("wordbound_original_equipment", () => new WordboundOriginalEquipmentQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("equipment_open").Timeout(75)
                .When(_ => Object.FindFirstObjectByType<LobbyMenu>() != null).Do(Open);
            yield return QaStep.Named("equipment_world").Timeout(90).Do(World);
            yield return QaStep.Named("equipment_bat").Timeout(90).Do(Bat);
            yield return QaStep.Named("equipment_hammer").Timeout(120).Do(Hammer);
            yield return QaStep.Named("equipment_return").Timeout(45).Do(Return);
        }

        IEnumerator Open(QaContext context)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer || Application.isBatchMode)
                throw new QaFailException("Original equipment capture requires a normal Windows QA player window.");
            Object.FindFirstObjectByType<LobbyMenu>().OpenWordbound();
            yield return context.WaitUntil(() => {
                launcher = Object.FindFirstObjectByType<WordboundLauncher>();
                return launcher && launcher.GetComponent<WordboundPartyMenu>()?.Coordinator != null;
            }, 45, "the actual Wordbound launcher");
            string folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wbqa", Guid.NewGuid().ToString("N")));
            launcher.ConfigureQaWorkshopStorage(folder);
            launcher.SendMessage("OpenWorkshop", SendMessageOptions.RequireReceiver);
            workshop = launcher.GetComponent<WordboundWorkshop>();
            if (!workshop || workshop.RecoveryChoicePending) throw new QaFailException("The isolated workshop did not open cleanly.");
            items = ItemCatalogue.FromJson(Resources.Load<GameData>(GameData.ResourcePath).items.text);
            var geometry = WordboundFlagshipContent.Create(items).Geometry;
            var mode = ModeDefinition.For("Freestyle");
            var document = Course(geometry);
            workshop.Open(document, geometry, MatchStateValidation.Requirements(mode), folder);
            history = workshop.History;
            history.Execute("QA original equipment draft", state => {
                state.Course.Title = "Original equipment inspection";
                state.Seed = "0000000000000001"; state.SeedLocked = true;
            });
            workshop.ResumeEditing();
            var preview = workshop.Preview();
            if (!preview.Success || preview.Manifest.Placements.Count != 4 || preview.Manifest.Supplies.Count != 9)
                throw new QaFailException("The exact four-prop, nine-letter Freestyle inspection course did not validate.");
            manifest = preview.Manifest.Hash; draft = history.Current.RecoveryJson;
            var primary = Path.Combine(folder, document.Id + ".autosave.json");
            yield return context.WaitUntil(() => File.Exists(primary) && File.ReadAllText(primary) == draft,
                15, "the isolated exact equipment draft autosave");
            var recovered = new WorkshopStorage(folder).Recover(document.Id, out bool backup);
            if (backup || recovered.RecoveryJson != draft) throw new QaFailException("Equipment draft recovery differs from the saved primary.");
            File.WriteAllText(context.PathInRun("equipment-course.json"), history.Current.CourseJsonText);
            File.WriteAllText(context.PathInRun("equipment-manifest.json"), CourseJson.ExportManifest(preview.Manifest));
            context.Note("equipment_manifest", manifest); context.Note("equipment_seed", history.Current.Seed);
            context.Note("equipment_catalogue", geometry.Hash); context.Note("equipment_isolated_storage", folder);
            context.Note("equipment_original_definitions", Props.Select(id => {
                var item = items.Get(id);
                return new Dictionary<string, object> { ["id"] = id, ["model"] = item.Model,
                    ["canonical_size_metres"] = item.Size, ["held_scale"] = item.HeldScale, ["authored_grip"] = item.Grip };
            }).ToArray());
            context.Note("equipment_scope", "Actual local Windows Freestyle; QA inspection camera; no remote peers or human-input claim.");
            observation = launcher.gameObject.AddComponent<WordboundOriginalEquipmentView>();
            observation.Begin(context, launcher);
        }

        static CourseDocument Course(CourseCatalogue geometry)
        {
            var course = new CourseDocument { Id = "qa-original-equipment", Title = "Original equipment draft",
                SupportedModes = new List<string> { "Freestyle" } };
            course.Layers.Add(new CourseLayer { Id = "Ground" });
            course.Modules.Add(new CourseModule { Id = "Room", DefinitionId = "FlagshipRoom", LayerId = "Ground" });
            course.Spawns.Add(new CourseSpawn { Id = "Start0", ModuleId = "Room", Position = new CoursePoint(0, 0, -1200) });
            for (int i = 1; i < 8; i++) course.Spawns.Add(new CourseSpawn { Id = "Start" + i,
                ModuleId = "Room", Position = new CoursePoint(-9000 + (i - 1) * 3000, 0, -8000) });
            course.RecoveryAnchors.Add(new CourseRecovery { Id = "Safe", ModuleId = "Room", Position = new CoursePoint(0, 0, -5000), RadiusMm = 500 });
            var positions = new[] { new CoursePoint(-1500, 0, 500), new CoursePoint(1500, 0, 500),
                new CoursePoint(3800, 0, 2200), new CoursePoint(-2000, 0, 3300) };
            for (int i = 0; i < Props.Length; i++)
            {
                var item = geometry.Assets[Props[i]]; var at = positions[i];
                course.Zones.Add(new CourseZone { Id = "Original" + Props[i], ModuleId = "Room", Placement = "Fixed",
                    FixedPosition = at, FixedYaw = 0, MinCount = 1, MaxCount = 1, Yaws = new List<int> { 0 },
                    LayerIds = new List<string> { "Ground" }, Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = Props[i] } },
                    Bounds = new CourseBox(new CoursePoint(at.X - item.WidthMm, 0, at.Z - item.DepthMm),
                        new CoursePoint(at.X + item.WidthMm, item.HeightMm, at.Z + item.DepthMm)) });
            }
            const string letters = "BATHAMMER";
            for (int i = 0; i < letters.Length; i++) course.Supplies.Add(new CourseSupply { Id = "Letter" + i,
                ModuleId = "Room", Letters = letters[i].ToString(), Position = new CoursePoint(-1800 + i * 450, 0, -3000),
                Renewable = false, Protected = false });
            return course;
        }

        IEnumerator World(QaContext context)
        {
            RequireDraft();
            observation.ExpectClient();
            workshop.RequestPlaytest();
            yield return context.WaitUntil(() => {
                client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
                if (!client || !client.Gameplay) return false;
                RequireHealthy();
                return client.Gameplay.Frame != null && client.Gameplay.CourseWorld && !client.Gameplay.IsSimulationPaused &&
                    client.GetComponent<WordboundPlayerView>()?.Player != null;
            }, 60, "the actual local equipment course and player");
            if (client.Gameplay.CourseWorld.ManifestHash != manifest) throw new QaFailException("The running equipment course differs from the authored preview.");
            observation.Prepare(client);
            observation.FrameWorld();
            yield return context.WaitUntil(() => { RequireHealthy(); return observation.Settled; }, 30,
                "30 fresh grounded player/keepsake observation frames spanning at least 30 ticks; initial launch remains diagnostic evidence");
            yield return Capture(context, "wordbound_original_world", null);
            worldCaptured = true;
        }

        IEnumerator Bat(QaContext context)
        {
            if (!worldCaptured) throw new QaFailException("The verified world capture must precede equipment commands.");
            yield return Craft(context, "BAT", 0);
            yield return Capture(context, "wordbound_original_held_bat", "BAT");
            batCaptured = true;
        }

        IEnumerator Hammer(QaContext context)
        {
            if (!batCaptured) throw new QaFailException("The actual crafted BAT capture must complete first.");
            yield return Craft(context, "HAMMER", 3);
            yield return Capture(context, "wordbound_original_held_hammer", "HAMMER");
            hammerCaptured = true;
        }

        MatchSnapshot Frame { get { RequireHealthy(); return client.Gameplay.Frame; } }
        PlayerInventory Inventory => Frame.Economy.Players.Single(player => player.PlayerId == 0);

        IEnumerator Craft(QaContext context, string recipe, int firstLetter)
        {
            for (int i = 0; i < recipe.Length; i++)
            {
                var source = client.Gameplay.CourseWorld.Course.Supplies.Single(value => value.SourceId == "Letter" + (firstLetter + i));
                string tile = Frame.Bodies.Single(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id).Id;
                yield return Command(context, WordboundAction.CollectLetter, tile);
            }
            if (!Inventory.Letters.Equals(LetterBag.FromWord(recipe))) throw new QaFailException("The actual collected letter bag does not match " + recipe + ".");
            yield return Command(context, WordboundAction.BeginCraft, value: recipe);
            yield return context.WaitUntil(() => Frame.Economy.Items.Any(item => item.Word == recipe &&
                item.Origin == ItemOrigin.Crafted && item.CrafterId == 0 && item.HolderId == 0 && item.State == ItemState.Held),
                20, "the real " + recipe + " craft timer");
            var crafted = Frame.Economy.Items.Single(item => item.Word == recipe && item.Origin == ItemOrigin.Crafted);
            int slot = Array.IndexOf(Inventory.Slots, crafted.Id);
            if (slot < 0 || !Inventory.Letters.IsEmpty || !Inventory.Reserved.IsEmpty)
                throw new QaFailException("Crafted equipment was not placed in a real cleared inventory slot.");
            yield return Command(context, WordboundAction.Equip, slot: slot);
            if (Frame.Actors.Single(actor => actor.PlayerId == Actor).EquippedSlot != slot ||
                WordboundCombatPolicy.EquippedItemId(Frame, Actor) != "item:" + crafted.Id)
                throw new QaFailException("The real equipment selection did not select " + recipe + ".");
            context.Note("equipment_" + recipe.ToLowerInvariant() + "_entity", "item:" + crafted.Id);
            context.Note("equipment_" + recipe.ToLowerInvariant() + "_slot", slot);
            observation.FrameHeld(recipe);
        }

        IEnumerator Command(QaContext context, WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "")
        {
            RequireHealthy();
            if (action == WordboundAction.CollectLetter) observation.RecordPhysics("before_collect_" + (receipts.Count + 1), target);
            var command = client.Gameplay.Submit(action, target, revision, slot, value);
            yield return context.WaitUntil(() => Frame.Commands.Frontiers[Actor] >= command.Sequence, 15, "actual " + action + " receipt");
            var receipt = Frame.Commands.Receipts.Single(entry => entry.ActorId == Actor && entry.Sequence == command.Sequence);
            receipts.Add(new Dictionary<string, object> { ["sequence"] = command.Sequence, ["epoch"] = Frame.Epoch,
                ["tick"] = Frame.Progress.Tick, ["action"] = action.ToString(), ["target"] = target,
                ["code"] = receipt.Effect.Code, ["payload_matches"] = receipt.PayloadHash == command.PayloadHash });
            File.WriteAllText(context.PathInRun("equipment-command-receipts.json"), OnlineJson.Encode(receipts));
            if (action == WordboundAction.CollectLetter) observation.RecordPhysics("after_collect_" + command.Sequence, target);
            if (receipt.PayloadHash != command.PayloadHash || receipt.Effect.Code != "Ok")
                throw new QaFailException("Actual " + action + " failed without retry: " + receipt.Effect.Code);
        }

        IEnumerator Capture(QaContext context, string name, string held)
        {
            yield return context.WaitUntil(() => { RequireHealthy(); return observation.Ready(held); }, 15,
                "fresh assigned-camera frames and actual original artwork");
            observation.Record(context, name, held);
            string path = context.PathInRun(name + ".png"), error = null;
            if (File.Exists(path)) throw new QaFailException("Equipment capture never overwrites existing evidence.");
            yield return StoryCamera.CaptureScreen(path, result => error = result);
            if (error != null || !observation.Ready(held)) throw new QaFailException("Equipment screen capture failed or presentation lost readiness: " + error);
            context.AddCapture(name, path, "png", "screen");
            WordboundOriginalEquipmentView.VerifyPixels(context, name, path);
        }

        IEnumerator Return(QaContext context)
        {
            if (observation) observation.RecordBeforeCleanup();
            if (observation) observation.RestoreView();
            if (!client) client = launcher ? launcher.GetComponentInChildren<WordboundLocalMatchClient>() : null;
            if (client)
            {
                var shutdown = client.ReturnToWorkshop();
                yield return context.WaitUntil(() => shutdown.IsCompleted, 30, "confirmed equipment playtest shutdown");
                if (shutdown.IsFaulted || shutdown.IsCanceled) throw new QaFailException("Equipment playtest shutdown failed.");
            }
            if (observation) observation.Finish();
            yield return context.WaitFrames(2);
            RequireDraft();
            if (client || !workshop.Visible || !worldCaptured || !batCaptured || !hammerCaptured)
                throw new QaFailException("All three actual captures and clean return to the unchanged inspection draft are required.");
            context.Note("equipment_actual_captures_and_return", true);
        }

        void RequireDraft()
        {
            if (!workshop || history == null || !ReferenceEquals(workshop.History, history) || history.Current.RecoveryJson != draft)
                throw new QaFailException("The isolated equipment draft changed or was not prepared.");
        }

        void RequireHealthy()
        {
            if (!client || !client.Gameplay || client.Gameplay.Fault != "") throw new QaFailException("The actual equipment client is unavailable or faulted.");
            var presentation = client.GetComponent<WordboundGameplayPresentation>();
            if (presentation && presentation.Fault != "") throw new QaFailException("The actual equipment presentation faulted.");
        }
    }
}
#endif
