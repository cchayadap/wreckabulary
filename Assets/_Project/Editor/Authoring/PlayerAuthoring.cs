using System;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTools
{
    /// <summary>One-time prefab migration and visual-only scene previews; never runs player setup or reads player preferences.</summary>
    public static class PlayerAuthoring
    {
        public const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string MaterialFolder = "Assets/_Project/Materials/CharacterDefaults";
        const string PreviewName = "Player Preview (Editor Only)";

        [MenuItem("Wreckabulary/Authoring/Upgrade Player Prefab")]
        public static void UpgradePrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Upgrade the player prefab outside Play mode.");
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (UpgradePlayer(root.GetComponent<PlayerController>()))
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>Returns false without modifying an already authored avatar, including artist mesh, material and transform overrides.</summary>
        public static bool UpgradePlayer(PlayerController player)
        {
            if (!player || !player.visual) throw new InvalidOperationException("Player requires its authored Visual root.");
            var appearance = player.GetComponent<PlayerAppearance>();
            if (appearance && appearance.AuthoredAvatar) return false;

            var wrapper = player.visual.Find("ImportedAvatar");
            GameObject model = null;
            bool created = !wrapper;
            if (wrapper)
            {
                var animator = wrapper.GetComponentInChildren<Animator>(true);
                if (!animator) throw new InvalidOperationException("Existing ImportedAvatar has no Animator; repair its reference without replacing artist work.");
                model = animator.gameObject;
            }
            else
            {
                wrapper = new GameObject("ImportedAvatar").transform;
                wrapper.SetParent(player.visual, false);
                model = InstantiateAvatar(wrapper);
                ApplyDefaultWardrobe(model);
                foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = player.gameObject.layer;
            }

            foreach (var renderer in player.visual.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.IsChildOf(wrapper)) renderer.enabled = false;
            if (!appearance) appearance = player.gameObject.AddComponent<PlayerAppearance>();
            var serialized = new SerializedObject(appearance);
            serialized.FindProperty("authoredAvatar").objectReferenceValue = model;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (created) PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            return true;
        }

        /// <summary>Creates an editable visual only. EditorOnly strips it from builds; it contains no gameplay player or physics.</summary>
        public static GameObject CreatePreview(Transform parent)
        {
            if (parent)
            {
                var existing = parent.Find(PreviewName);
                if (existing) return existing.gameObject;
            }
            var preview = new GameObject(PreviewName) { tag = "EditorOnly" };
            preview.transform.SetParent(parent, false);
            var model = InstantiateAvatar(preview.transform);
            ApplyDefaultWardrobe(model);
            var idle = ModelLibrary.Load().FindClip("Avatar/Avatar", "Idle");
            if (idle) idle.SampleAnimation(model, 0f);
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            return preview;
        }

        /// <summary>Explicitly frames a preview in the same close, centered lens as runtime; existing cameras are never changed automatically.</summary>
        public static void FramePreview(Camera camera, Transform avatar)
        {
            if (!camera || !avatar) return;
            Undo.RecordObjects(new UnityEngine.Object[] { camera, camera.transform }, "Frame third-person player preview");
            camera.orthographic = false;
            camera.fieldOfView = 55f;
            camera.nearClipPlane = .08f;
            var rotation = Quaternion.Euler(10f, avatar.eulerAngles.y, 0f);
            camera.transform.SetPositionAndRotation(avatar.position + Vector3.up + rotation * Vector3.back * 3.15f, rotation);
        }

        static GameObject InstantiateAvatar(Transform parent)
        {
            var library = ModelLibrary.Load();
            var asset = library ? library.Find("Avatar/Avatar") : null;
            if (!asset) throw new InvalidOperationException("Run Set Up Imported Art before authoring the player.");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
            return model;
        }

        static void ApplyDefaultWardrobe(GameObject model)
        {
            var catalogue = GameConfig.Current.Wardrobe;
            var outfit = PlayerAppearance.DefaultPresentationOutfit();
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                WardrobePiece piece = null;
                foreach (var candidate in catalogue.Pieces)
                    if (candidate.Mesh == renderer.name) { piece = candidate; break; }
                renderer.enabled = piece == null || outfit.PieceIn(piece.Slot) == piece.Id;
                if (piece != null && renderer.enabled)
                {
                    var colour = catalogue.ColourFor(outfit, piece.Slot);
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var source = materials[i];
                        if (!source || colour == null) continue;
                        bool rib = source.name == "fabric_rib" && piece.TintMaterial == "fabric_main";
                        if (source.name != piece.TintMaterial && !rib) continue;
                        var tint = new Color(colour.R, colour.G, colour.B, 1f);
                        if (rib) { tint *= .75f; tint.a = 1f; }
                        materials[i] = DefaultMaterial(source, tint);
                    }
                    renderer.sharedMaterials = materials;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        static Material DefaultMaterial(Material source, Color tint)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "CharacterDefaults");
            string name = source.name + "__Default_" + ColorUtility.ToHtmlStringRGBA(tint);
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(source) { name = name };
            material.SetColor("_BaseColor", tint);
            material.SetColor("_Color", tint);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
