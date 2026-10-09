using UnityEngine;

namespace Wreckabulary.Art
{
    /// <summary>Saved presentation edition; runtime never rebuilds or repositions its editable children.</summary>
    public sealed class RoomDressing : MonoBehaviour
    {
        public const int CurrentEdition = 1;
        [SerializeField] string roomId;
        [SerializeField] int ownerStorey;
        [SerializeField] int edition;
        [SerializeField] int importedModelAnchorsVersion;
        [SerializeField] int doorLintelsVersion;
        public string RoomId => roomId;
        public int OwnerStorey => ownerStorey;
        public int Edition => edition;
        public bool HasImportedModelAnchors => importedModelAnchorsVersion >= 1;
        public bool HasDoorLintels => doorLintelsVersion >= 1;

        public void Configure(string id, int storey)
        {
            roomId = id;
            ownerStorey = storey;
            edition = CurrentEdition;
            importedModelAnchorsVersion = 1;
        }

        public void MarkImportedModelAnchors() => importedModelAnchorsVersion = 1;
        public void MarkDoorLintels() => doorLintelsVersion = 1;
    }
}
