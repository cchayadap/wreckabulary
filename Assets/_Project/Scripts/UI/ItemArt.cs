using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary.UI
{
    public static class ItemArt
    {
        public static bool TryGet(string id, out Texture2D texture, out Rect uv)
        {
            texture = OriginalItemArt.Find(id);
            uv = new Rect(0f, 0f, 1f, 1f);
            if (texture) return true;
            if (string.IsNullOrEmpty(id)) return false;
            if (GeneratedItemArt.TryGet(id, out var sprite))
            {
                texture = sprite.texture;
                var region = sprite.textureRect;
                uv = new Rect(region.x / texture.width, region.y / texture.height,
                    region.width / texture.width, region.height / texture.height);
                return true;
            }
            return false;
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
