using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyRecipeTileView : MonoBehaviour
    {
        public Button Button;
        public LobbyPress Press;
        public Image Selection;
        public RawImage Icon;
        public RectTransform Letters;
        public Color SelectedInk = LobbyKit.Sun;
        [Range(8, 64)] public float MaximumLetterSize = 24;
        [Range(40, 800)] public float LetterWidthBudget = 112;
        UnityAction click;
        Action<bool> hover;

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            foreach (var reference in new Component[] { Button, Press, Selection, Icon, Letters })
                LobbyShopLayout.Require(this, reference, "Recipe tile binding");
            LobbyShopLayout.Require(this, Press.Body, "recipe motion container");
            LobbyShopLayout.Require(this, Press.Ring, "recipe focus ring");
            foreach (var reference in new Component[] { Button, Press, Selection, Icon, Press.Body, Press.Ring })
                if (reference.transform.IsChildOf(Letters))
                    throw new InvalidOperationException("The dynamic letter slot cannot contain the recipe controls or visuals.");
            if (Button.gameObject != gameObject || Press.gameObject != gameObject || !Letters.GetComponent<HorizontalLayoutGroup>() ||
                Button.onClick.GetPersistentEventCount() != 0 ||
                !LobbyShopLayout.Finite(new Vector3(MaximumLetterSize, LetterWidthBudget, 0)) ||
                MaximumLetterSize < 8 || MaximumLetterSize > 64 || LetterWidthBudget < 40 || LetterWidthBudget > 800)
                throw new InvalidOperationException("Recipe tiles need one runtime-bound button and a bounded dynamic letter slot.");
        }

        public void Bind(string id, bool selected, Action pin, Action<bool> preview)
        {
            Validate();
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Recipe ID is required.", nameof(id));
            Unbind();
            gameObject.name = "Recipe " + id;
            click = () => pin(); hover = preview;
            Button.onClick.AddListener(click); Press.Hot += hover;
            Selection.color = SelectedInk; Selection.enabled = selected;
            Icon.enabled = UI.ItemArt.TryGet(id, out var texture, out var uv);
            Icon.texture = texture; Icon.uvRect = uv;
            for (int i = Letters.childCount - 1; i >= 0; i--)
            {
                var child = Letters.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            float gap = Letters.GetComponent<HorizontalLayoutGroup>().spacing;
            float size = Mathf.Min(MaximumLetterSize, Mathf.Max(1, (LetterWidthBudget - gap * (id.Length - 1)) / id.Length));
            foreach (char letter in id) LobbyKit.LetterTile(Letters, letter, size);
        }

        void Unbind()
        {
            if (Button && click != null) Button.onClick.RemoveListener(click);
            if (Press && hover != null) Press.Hot -= hover;
            click = null; hover = null;
        }

        void OnDestroy() => Unbind();
    }
}
