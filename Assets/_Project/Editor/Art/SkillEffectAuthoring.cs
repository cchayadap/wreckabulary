using System;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public static class SkillEffectAuthoring
    {
        public static void EnsureFoamAssets()
        {
            const string folder = "Assets/_Project/Resources/VFX";
            MaterialLibraryBuilder.EnsureFolder(folder);
            if (!AssetDatabase.LoadAssetAtPath<Material>(folder + "/FoamBubble.mat"))
            {
                var shader = Shader.Find("Wreckabulary/Soap Bubble");
                if (!shader) throw new InvalidOperationException("Import the Soap Bubble shader before creating VFX assets.");
                var material = new Material(shader) { name = "Foam Bubble" };
                AssetDatabase.CreateAsset(material, folder + "/FoamBubble.mat");
            }
            if (!AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/BubbleSphere.asset"))
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                try
                {
                    var mesh = UnityEngine.Object.Instantiate(sphere.GetComponent<MeshFilter>().sharedMesh);
                    mesh.name = "Bubble Sphere";
                    AssetDatabase.CreateAsset(mesh, folder + "/BubbleSphere.asset");
                }
                finally { UnityEngine.Object.DestroyImmediate(sphere); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
