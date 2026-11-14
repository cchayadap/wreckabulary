using System;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Lobby/Settings layout", fileName = "LobbySettingsLayout")]
    public sealed class LobbySettingsLayout : ScriptableObject
    {
        public const string ResourcePath = "UI/LobbySettingsLayout";
        public LobbySettingsPageView Page;

        public void Validate()
        {
            if (!Page) throw new InvalidOperationException("Assign the saved Settings frame prefab.");
            Page.Validate();
        }
    }
}
