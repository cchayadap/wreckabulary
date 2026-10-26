using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyHomePageView : MonoBehaviour
    {
        public RectTransform TurnHint;
        public RectTransform Dock;
        public LobbyShopActionView Start;
        public LobbyShopActionView Mode;
        public LobbyShopActionView House;
        public LobbyShopActionView Wordbound;
        public TextMeshProUGUI WordboundNote;

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            foreach (var reference in new Component[] { TurnHint, Dock, Start, Mode, House, Wordbound, WordboundNote })
                LobbyShopLayout.Require(this, reference, "Home binding");
            var actions = new[] { Start, Mode, House, Wordbound };
            if (TurnHint.IsChildOf(Dock) || Dock.IsChildOf(TurnHint) || !WordboundNote.transform.IsChildOf(Dock) ||
                actions.Distinct().Count() != actions.Length)
                throw new InvalidOperationException("Home needs a separate turn hint and dock with four distinct actions.");
            foreach (var action in actions)
            {
                action.Validate();
                if (!action.transform.IsChildOf(Dock) || WordboundNote.transform.IsChildOf(action.transform) ||
                    action.transform.IsChildOf(WordboundNote.transform) ||
                    actions.Any(other => other != action && other.transform.IsChildOf(action.transform)))
                    throw new InvalidOperationException("Home actions and the Wordbound note must have independent visibility inside the dock.");
            }
        }

        public Selectable Bind(bool showTurnHint, bool starting, bool blocked, bool showHouse, bool canOpenWordbound,
            string mode, string house, Action start, Action chooseMode, Action chooseHouse, Action openWordbound)
        {
            Validate();
            TurnHint.name = "Turn hint"; TurnHint.gameObject.SetActive(showTurnHint);
            Dock.name = "Next match";
            Start.Bind("GO", Start.Label.text, starting || blocked ? null : start, true);
            Mode.Bind("Choose mode", mode, starting ? null : chooseMode, false);
            House.Bind("Change house", house, starting || !showHouse ? null : chooseHouse, false);
            Wordbound.Bind("Wordbound", Wordbound.Label.text, starting || !canOpenWordbound ? null : openWordbound, false);
            House.gameObject.SetActive(showHouse);
            Wordbound.gameObject.SetActive(canOpenWordbound);
            WordboundNote.gameObject.SetActive(canOpenWordbound);
            Dock.gameObject.SetActive(!starting);
            return starting ? null : blocked ? Mode.Button : Start.Button;
        }
    }
}
