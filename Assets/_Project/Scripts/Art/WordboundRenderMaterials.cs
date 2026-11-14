using System;
using UnityEngine;

namespace Wreckabulary.Art
{
    public static class WordboundRenderMaterials
    {
        public const string OpaquePath = "Wordbound/Render/Surface";
        public const string TransparentPath = "Wordbound/Render/TransparentSurface";

        public static Material Create(bool transparent, string name)
        {
            string path = transparent ? TransparentPath : OpaquePath;
            var template = Resources.Load<Material>(path);
            if (!template || !template.shader)
                throw new InvalidOperationException("Wordbound render material is missing: " + path + ". Create the render assets in Unity before building.");
            return new Material(template) { name = name, hideFlags = HideFlags.DontSave };
        }
    }
}
