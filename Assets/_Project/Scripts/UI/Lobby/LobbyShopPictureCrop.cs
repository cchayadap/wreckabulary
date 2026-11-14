using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    [RequireComponent(typeof(RawImage))]
    public sealed class LobbyShopPictureCrop : MonoBehaviour
    {
        void OnEnable() => Refresh();
        void OnRectTransformDimensionsChange() => Refresh();
        void Refresh()
        {
            var image = GetComponent<RawImage>();
            if (!image || !image.texture) return;
            var rect = ((RectTransform)transform).rect;
            if (rect.width <= 0 || rect.height <= 0 || image.texture.width <= 0 || image.texture.height <= 0) return;
            float ratio = rect.width / rect.height / ((float)image.texture.width / image.texture.height);
            image.uvRect = ratio < 1 ? new Rect((1 - ratio) * .5f, 0, ratio, 1)
                : new Rect(0, (1 - 1 / ratio) * .5f, 1, 1 / ratio);
        }
    }
}
