using UnityEngine;

namespace Wreckabulary.Art
{
    /// <summary>Identifies saved seasonal decoration; no runtime rebuild or lobby-theme subscription.</summary>
    [DisallowMultipleComponent]
    public sealed class SeasonalRoomDressing : MonoBehaviour
    {
        public const int CurrentEdition = 1;
        [SerializeField] string collectionId;
        [SerializeField] string roomId;
        [SerializeField] int ownerStorey;
        [SerializeField] int edition;

        public string CollectionId => collectionId;
        public string RoomId => roomId;
        public int OwnerStorey => ownerStorey;
        public int Edition => edition;

        public void Configure(string collection, string room, int storey)
        {
            collectionId = collection;
            roomId = room;
            ownerStorey = storey;
            edition = CurrentEdition;
        }
    }
}
