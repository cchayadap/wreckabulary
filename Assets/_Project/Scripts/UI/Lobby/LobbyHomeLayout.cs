using System;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Lobby/Home layout", fileName = "LobbyHomeLayout")]
    public sealed class LobbyHomeLayout : ScriptableObject
    {
        public const string ResourcePath = "UI/LobbyHomeLayout";
        public LobbyHomePageView Page;

        public void Validate()
        {
            if (!Page) throw new InvalidOperationException("Assign the saved Home page prefab.");
            Page.Validate();
        }
    }
}
