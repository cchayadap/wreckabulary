using TMPro;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary
{
    /// <summary>A physical letter tile that can be picked up, carried and dropped.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class LetterTile : MonoBehaviour
    {
        [SerializeField] char letter = 'A';
        [SerializeField] TextMeshPro[] labels;
        [SerializeField] MeshRenderer body;

        public char Letter => letter;
        public LetterRarity Rarity => LetterScores.RarityOf(letter);
        public Rigidbody Body { get; private set; }

        GameObject importedVisual;
        char visualLetter;

        float readyAt;
        LetterInventory droppedBy;
        float ownerLockUntil;

        void Awake() => Body = GetComponent<Rigidbody>();

        public void SetLetter(char c)
        {
            letter = char.ToUpperInvariant(c);
            if (labels != null) foreach (var label in labels) if (label) label.text = letter.ToString();
            if (body) body.sharedMaterial = GameAssets.I.TileMaterial(Rarity);
            UpdateImportedVisual();
        }

        void UpdateImportedVisual()
        {
            var library = ModelLibrary.Load();
            string key = "Letters/Tile_" + letter;
            if (!library || !library.Find(key)) return;
            if (importedVisual && visualLetter == letter) return;
            if (importedVisual)
            {
                importedVisual.SetActive(false);
                Destroy(importedVisual);
            }
            importedVisual = new GameObject("ImportedTile");
            importedVisual.transform.SetParent(transform, false);
            // Source tiles are authored upright; rest on their broad wooden back
            // so the actual raised glyph faces the overhead gameplay camera.
            importedVisual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            importedVisual.transform.localScale = Vector3.one * 1.6f;
            var model = ModelVisual.Spawn(key, importedVisual.transform);
            var bounds = ModelVisual.BoundsIn(transform, model);
            importedVisual.transform.localPosition -= bounds.center;
            visualLetter = letter;
            if (body) body.enabled = false;
            if (TryGetComponent(out BoxCollider collision))
            {
                collision.center = Vector3.zero;
                collision.size = bounds.size;
            }
            if (labels != null)
                for (int i = 0; i < labels.Length; i++)
                {
                    if (!labels[i]) continue;
                    // One underside label keeps a tumbled tile readable. The top
                    // is the supplied model's mesh glyph, with no duplicate TMP.
                    bool underside = i == 5;
                    labels[i].gameObject.SetActive(underside);
                    if (!underside) continue;
                    labels[i].transform.localPosition = Vector3.down * (bounds.size.y*.5f+.003f);
                    labels[i].rectTransform.sizeDelta = new Vector2(bounds.size.x,bounds.size.z)*.85f;
                }
            Color tint = Rarity == LetterRarity.Legendary ? new Color(.95f,.77f,.30f)
                : Rarity == LetterRarity.Rare ? new Color(.49f,.77f,.71f) : Color.white;
            if (Rarity == LetterRarity.Common) return;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i] || !materials[i].name.StartsWith("wood_light")) continue;
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", tint);
                    block.SetColor("_Color", tint);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }

        /// <summary>Freshly launched tiles can't be grabbed straight away, and never instantly by whoever dropped them.</summary>
        public bool CanBeCollectedBy(LetterInventory who) =>
            Time.time >= readyAt && (who != droppedBy || Time.time >= ownerLockUntil);

        /// <summary>Called when a player collects this tile.</summary>
        public void Collect() => TilePool.Instance.Release(this);

        /// <summary>Launches the tile out into the world.</summary>
        public void Launch(Vector3 position, Vector3 velocity, LetterInventory from = null)
        {
            transform.SetPositionAndRotation(position, Random.rotation);
            Body.position = position;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Random.insideUnitSphere * 8f;
            readyAt = Time.time + 0.3f;
            droppedBy = from;
            ownerLockUntil = Time.time + 1.2f;
        }
    }
}
