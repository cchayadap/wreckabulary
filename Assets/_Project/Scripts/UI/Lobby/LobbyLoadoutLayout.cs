using System;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Lobby/Loadout layout", fileName = "LobbyLoadoutLayout")]
    public sealed class LobbyLoadoutLayout : ScriptableObject
    {
        public const string ResourcePath = "UI/LobbyLoadoutLayout";
        public LobbyLoadoutPageView Page;
        public LobbyRecipeTileView Recipe;

        public void Validate()
        {
            if (!Page || !Recipe) throw new InvalidOperationException("Assign saved Loadout page and recipe tile prefabs.");
            Page.Validate();
            Recipe.Validate();
        }
    }
}
