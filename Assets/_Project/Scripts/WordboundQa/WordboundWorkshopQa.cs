#if UNITY_EDITOR || GSK_QA
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GameStudioKit.Qa;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.Qa
{
    public sealed class WordboundWorkshopQa : IQaScenario
    {
        public string Name => "wordbound_workshop_flow";
        WordboundLauncher launcher;
        WordboundWorkshop workshop;
        WordboundLocalMatchClient client;
        WorkshopHistory history;
        string initial, edited, manifest;
        Camera ownedCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => QaScenarios.Register("wordbound_workshop_flow", () => new WordboundWorkshopQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("open_wordbound").Timeout(60)
                .When(_ => Object.FindFirstObjectByType<LobbyMenu>() != null)
                .Do(OpenWordbound);
            yield return QaStep.Named("edit_workshop").Timeout(60).Do(EditWorkshop);
            yield return QaStep.Named("exact_playtest").Timeout(90).Do(Playtest)
                .Measure(_ => new Dictionary<string, object> {
                    ["exact_manifest"] = client && client.Gameplay.CourseWorld.ManifestHash == manifest,
                    ["draft_unchanged"] = history.Current.RecoveryJson == edited,
                    ["markers"] = client ? client.GetComponent<WordboundPuzzleGuide>().MarkerCount : 0
                })
                .Expect(QaAssert.IsTrue("exact_manifest")).Expect(QaAssert.IsTrue("draft_unchanged"))
                .Expect(QaAssert.AtLeast("markers", 1));
            yield return QaStep.Named("return_to_workshop").Timeout(45).Do(Return)
                .Measure(_ => new Dictionary<string, object> {
                    ["same_history"] = workshop && ReferenceEquals(workshop.History, history),
                    ["draft_unchanged"] = history.Current.RecoveryJson == edited,
                    ["client_destroyed"] = client == null,
                    ["camera_destroyed"] = ownedCamera == null,
                    ["editor_visible"] = workshop && workshop.Visible
                })
                .Expect(QaAssert.IsTrue("same_history")).Expect(QaAssert.IsTrue("draft_unchanged"))
                .Expect(QaAssert.IsTrue("client_destroyed")).Expect(QaAssert.IsTrue("camera_destroyed"))
                .Expect(QaAssert.IsTrue("editor_visible"));
            yield return QaStep.Named("undo_redo_preserved").Timeout(15).Do(UndoRedo);
        }

        IEnumerator OpenWordbound(QaContext context)
        {
            if (Application.isBatchMode) throw new QaFailException("This visual scenario requires a presented player window.");
            Object.FindFirstObjectByType<LobbyMenu>().OpenWordbound();
            yield return context.WaitUntil(() => {
                launcher = Object.FindFirstObjectByType<WordboundLauncher>();
                return launcher && launcher.GetComponent<WordboundPartyMenu>()?.Coordinator != null;
            }, 45, "the Wordbound party menu");
            yield return context.WaitFrames(3);
            yield return context.CaptureScreen("wordbound_party");
        }

        IEnumerator EditWorkshop(QaContext context)
        {
            launcher.SendMessage("OpenWorkshop", SendMessageOptions.RequireReceiver);
            workshop = launcher.GetComponent<WordboundWorkshop>();
            if (!workshop) throw new QaFailException("The launcher did not open its workshop.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            workshop.OpenFlagship(WordboundFlagshipContent.Create(ItemCatalogue.FromJson(data.items.text)),
                Path.Combine(context.OutDir, "workshop-draft"));
            history = workshop.History;
            initial = history.Current.RecoveryJson;
            history.Execute("QA draft edit", draft => {
                draft.Course.Title = "Wordbound playtest draft";
                draft.Seed = "0314159265358979";
                draft.SeedLocked = true;
            });
            edited = history.Current.RecoveryJson;
            workshop.ResumeEditing();
            var preview = workshop.Preview();
            if (!preview.Success) throw new QaFailException("The exact edited flagship did not validate.");
            manifest = preview.Manifest.Hash;
            context.Note("manifest_hash", manifest);
            context.Note("seed", history.Current.Seed);
            yield return context.WaitFrames(3);
            yield return context.CaptureScreen("wordbound_workshop");
        }

        IEnumerator Playtest(QaContext context)
        {
            workshop.RequestPlaytest();
            yield return context.WaitUntil(() => {
                client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
                if (!client || !client.Gameplay) return false;
                if (client.Gameplay.Fault != "") throw new QaFailException("The local playtest faulted.");
                var guide = client.GetComponent<WordboundPuzzleGuide>();
                if (guide && guide.Fault != "") throw new QaFailException("The puzzle guide faulted.");
                return client.Gameplay.CourseWorld && client.Gameplay.Frame != null &&
                    !client.Gameplay.IsSimulationPaused && guide && guide.MarkerCount > 0;
            }, 60, "the local flagship and puzzle markers");
            ownedCamera = client.GetComponent<WordboundPlayerView>().Camera;
            yield return context.WaitFrames(90);
            yield return context.CaptureScreen("wordbound_local_world");
            client.GetComponent<WordboundPlayerView>().SetMenuOpen(true);
            yield return context.WaitFrames(3);
            yield return context.CaptureScreen("wordbound_equipment_menu");
        }

        IEnumerator Return(QaContext context)
        {
            yield return Complete(context, client.ReturnToWorkshop());
            yield return context.WaitFrames(3);
            yield return context.CaptureScreen("wordbound_returned_workshop");
        }

        IEnumerator UndoRedo(QaContext context)
        {
            if (!history.Undo() || history.Current.RecoveryJson != initial)
                throw new QaFailException("The original draft was not restored by Undo after playtest.");
            if (!history.Redo() || history.Current.RecoveryJson != edited)
                throw new QaFailException("Redo did not recover the edited draft after playtest.");
            context.Note("undo_redo_exact", true);
            yield return context.WaitFrames(2);
        }

        static IEnumerator Complete(QaContext context, Task task)
        {
            yield return context.WaitUntil(() => task.IsCompleted, 30, "confirmed local shutdown");
            if (task.IsCanceled || task.IsFaulted) throw new QaFailException("Local shutdown did not complete successfully.");
        }
    }
}
#endif
