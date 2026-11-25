using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyPlayStepFit : MonoBehaviour
    {
        public RectTransform Preview, Words, Tiles;
        public float Reserve = 8, Minimum = 220, Maximum = 640, Pad = 12, WordsMinimum = 330, ArtShare = .5f, ArtMaximum = 130, PictureMinimum = 180;
        LayoutElement element;
        Vector4 last = -Vector4.one;

        void LateUpdate()
        {
            var scroll = GetComponentInParent<ScrollRect>();
            float viewport = scroll && scroll.viewport ? scroll.viewport.rect.height : 0f;
            if (viewport <= 0f || !Preview || !(Preview.parent is RectTransform detail)) return;
            float width = detail.rect.width;
            float above = 0f;
            if (transform.parent is RectTransform column)
            {
                var group = column.GetComponent<VerticalLayoutGroup>();
                for (int i = 0; i < transform.GetSiblingIndex(); i++)
                    if (column.GetChild(i) is RectTransform child && child.gameObject.activeSelf) above += child.rect.height + (group ? group.spacing : 0f);
                if (group) above += group.padding.top;
            }
            float tileHeight = Tiles && Tiles.childCount > 0 ? ((RectTransform)Tiles.GetChild(0)).rect.height : 0f;
            var now = new Vector4(viewport, width, above, tileHeight);
            if (now == last) return;
            last = now;

            float height = Mathf.Clamp(viewport - Reserve - above, Minimum, Maximum);
            if (!element && !TryGetComponent(out element)) element = gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;

            float pictureWidth = Mathf.Max(0f, Mathf.Min((height - Pad * 2) * LobbyPlayModePreview.Aspect, width - Pad * 3 - WordsMinimum));
            if (pictureWidth < PictureMinimum) pictureWidth = 0f;
            Preview.gameObject.SetActive(pictureWidth > 0f);
            float pictureHeight = pictureWidth / LobbyPlayModePreview.Aspect;
            Preview.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, -Pad - pictureHeight), new Vector2(Pad + pictureWidth, -Pad));
            if (Words) Words.offsetMin = new Vector2(pictureWidth > 0f ? Pad * 2 + pictureWidth : Pad, Words.offsetMin.y);

            if (!Tiles || Tiles.childCount == 0) return;
            float art = Mathf.Min(ArtMaximum, Mathf.Max(48f, tileHeight * ArtShare));
            foreach (Transform tile in Tiles)
                foreach (var image in tile.GetComponentsInChildren<RawImage>())
                    if (image.name == PlayPage.ModeEmblemName) image.rectTransform.sizeDelta = new Vector2(art, art);
        }
    }
}
