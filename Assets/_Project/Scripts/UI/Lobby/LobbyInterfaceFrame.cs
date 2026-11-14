using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyInterfaceFrame : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public Graphic Target;
            public LobbyColourRole Role;
            public LobbyGradient Gradient;
        }

        public LobbyInterfaceTheme Theme;
        public Binding[] Bindings = Array.Empty<Binding>();

        public void Validate()
        {
            if (!Theme || !Theme.TryValidate(out string reason))
                throw new InvalidOperationException("A valid saved interface theme is required.");
            if (Bindings == null || Bindings.Length == 0) throw new InvalidOperationException("A frame needs explicit colour bindings.");
            var targets = new HashSet<Graphic>();
            foreach (var binding in Bindings)
                if (binding == null || !binding.Target || binding.Target is RawImage ||
                    !binding.Target.transform.IsChildOf(transform) && binding.Target.transform != transform ||
                    !Enum.IsDefined(typeof(LobbyColourRole), binding.Role) || !targets.Add(binding.Target) ||
                    binding.Gradient && binding.Gradient.gameObject != binding.Target.gameObject)
                    throw new InvalidOperationException("Frame colour bindings must be unique, local UI graphics; never artwork textures.");
        }

        [ContextMenu("Apply shared interface colours")]
        public void Apply()
        {
            Validate();
            var p = Theme.Colours;
            foreach (var binding in Bindings)
            {
                var colour = p.Get(binding.Role);
                if (binding.Gradient)
                {
                    binding.Target.color = Color.white;
                    binding.Gradient.Set(colour, colour);
                }
                else binding.Target.color = colour;
            }
            foreach (var action in GetComponentsInChildren<LobbyShopActionView>(true))
            { action.IdleInk = p.Text; action.ActiveInk = p.Selection; }
            foreach (var page in GetComponentsInChildren<LobbyShopPageView>(true))
            { page.CloseIdleInk = p.Text; page.CloseActiveInk = p.Selection; }
            foreach (var page in GetComponentsInChildren<LobbyLoadoutPageView>(true))
            { page.CloseIdleInk = p.Text; page.CloseActiveInk = p.Selection; }
            foreach (var card in GetComponentsInChildren<LobbyShopOfferView>(true))
            { card.PreviewColour = p.Selection; card.EquippedColour = p.Selection; }
            foreach (var tile in GetComponentsInChildren<LobbyRecipeTileView>(true)) tile.SelectedInk = p.Selection;
        }

        void Awake()
        {
            if (Theme) Apply();
        }
    }
}
