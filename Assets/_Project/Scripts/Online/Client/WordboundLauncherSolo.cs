using System;
using System.Linq;
using UnityEngine;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundLauncher
    {
        const string SoloStartupFailure = "Solo could not start. Return to the lobby and try again.";

        public static IWordboundPuzzleContent CreateSoloContent(string items, string rules, string mode, string seed)
        {
            if (!WordboundSoloEntry.Supports(mode)) throw new ArgumentException("Choose Time Run or Freestyle for solo play.", nameof(mode));
            return new WordboundClientContent(items, rules, mode, "Solo", seed).PhysicalPuzzles;
        }

        async void StartSolo(string mode)
        {
            if (destroying || localClient || activeClient || activeLaunch != null) return;
            int generation = ++localGeneration;
            WordboundLocalMatchClient client = null;
            try
            {
                var content = CreateSoloContent(itemJson, ruleJson, mode, Guid.NewGuid().ToString("N").Substring(0, 16));
                var root = new GameObject("Wordbound solo game");
                root.transform.SetParent(transform, false);
                client = root.AddComponent<WordboundLocalMatchClient>();
                localClient = client;
                client.Returned += () => ReturnedFromSolo(client);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                await client.StartLocal(content, Assets.BodyPrefabs(), Assets.GameplayScene(),
                    controls: Session.Bindings.FirstOrDefault(), cameraProfile: Assets.CameraProfile,
                    visualProfile: Assets.VisualProfile, uiTheme: Assets.UiTheme, storyProfile: Assets.StoryProfile,
                    returnLabel: "Return to lobby");
                if (!destroying && generation == localGeneration && localClient == client && LobbyCamera)
                    LobbyCamera.enabled = false;
            }
            catch (Exception)
            {
                if (destroying || generation != localGeneration) return;
                if (client && localClient == client && !client.ShutdownConfirmed)
                {
                    client.ShowStartupFailure();
                    return;
                }
                if (client && localClient == client)
                {
                    localClient = null;
                    Destroy(client.gameObject);
                }
                if (LobbyCamera) LobbyCamera.enabled = true;
                failure = SoloStartupFailure;
            }
        }

        void ReturnedFromSolo(WordboundLocalMatchClient client)
        {
            if (destroying || localClient != client) return;
            if (!client.ShutdownConfirmed) throw new InvalidOperationException("Stop the local game before returning.");
            localGeneration++;
            localClient = null;
            Destroy(client.gameObject);
            if (LobbyCamera) LobbyCamera.enabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            WordboundSoloEntry.MarkReturned();
            if (!Session.GoHome()) failure = "The main lobby is unavailable in this build.";
        }
    }
}
