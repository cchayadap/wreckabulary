using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary.UI
{
    public static class ItemArt
    {
        static readonly Dictionary<string, Texture2D> Imported = new(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => Imported.Clear();

        public static bool TryGet(string id, out Texture2D texture, out Rect uv)
        {
            texture = null;
            uv = new Rect(0f, 0f, 1f, 1f);
            if (string.IsNullOrEmpty(id)) return false;
            if (GeneratedItemArt.TryGet(id, out var sprite))
            {
                texture = sprite.texture;
                var region = sprite.textureRect;
                uv = new Rect(region.x / texture.width, region.y / texture.height,
                    region.width / texture.width, region.height / texture.height);
                return true;
            }
            if (!Imported.TryGetValue(id, out texture))
                Imported[id] = texture = Resources.Load<Texture2D>("UI/Items/" + id);
            return texture;
        }

        public static bool Apply(RawImage image, string id)
        {
            bool found = TryGet(id, out var texture, out var uv);
            image.texture = texture;
            image.uvRect = uv;
            image.enabled = found;
            return found;
        }
    }
}
