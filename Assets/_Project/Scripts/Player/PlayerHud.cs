using System.Text;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Remote identity and short contextual prompts; health, gear and letters live in the screen HUD.</summary>
    public class PlayerHud : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] TextMeshPro lettersText;
        [SerializeField] TextMeshPro wheelText;
        [SerializeField] float height = 1.95f;

        readonly StringBuilder sb = new();
        string lastLetters, lastWheel, nameMarkup;
        WorldSpaceBillboard billboard;

        void Awake()
        {
            billboard = GetComponent<WorldSpaceBillboard>();
            if (!billboard) billboard = gameObject.AddComponent<WorldSpaceBillboard>();
        }

        void Start()
        {
            if (!player) return;
            nameMarkup = "<color=#" + ColorUtility.ToHtmlStringRGB(player.Color) + ">" + player.Name + "</color>";
            if (CameraRig.Instance) billboard.SetCamera(CameraRig.Instance.ViewCamera);
        }

        void LateUpdate()
        {
            if (!player) return;
            transform.position = player.transform.position + Vector3.up * height;
            bool followed = GameHud.Active && GameHud.Active.LocalPlayer == player;
            if (!followed) followed = CameraRig.Instance && CameraRig.Instance.IsFollowing(player);
            if (!followed && player.Binding is not BotBinding)
            {
                int humans = 0;
                foreach (var seat in World.Players)
                    if (seat && seat.Binding != null && seat.Binding is not BotBinding) humans++;
                followed = humans == 1;
            }
            SetIfChanged(lettersText, IdentityLine(followed), ref lastLetters);
            bool remoteCraft = !followed && player.Summoner && player.Summoner.IsSpelling;
            SetIfChanged(wheelText, remoteCraft ? WheelLines() : ContextLine(), ref lastWheel);
        }

        string IdentityLine(bool followed)
        {
            if (player.IsEliminated) return "";
            if (player.IsDowned)
                return $"<color=#FF967D>REVIVE · {Mathf.CeilToInt(player.Health.BleedOutLeft)}s</color>";
            return followed ? "" : nameMarkup ?? "";
        }

        /// <summary>Show only actions that need nearby world context.</summary>
        string ContextLine()
        {
            var combat = player.Combat;
            if (!combat) return "";
            if (combat.IsReviving) return "<color=#98F3CA>REVIVING</color>";
            if (player.CanAct && !(combat.IsHolding && !combat.Weapon) && combat.DownedTeammateNearby())
                return "<color=#F6D98B>Hold grab to revive</color>";
            return "";
        }

        static void SetIfChanged(TextMeshPro text, string value, ref string last)
        {
            if (!text || value == last) return;
            last = value;
            text.text = value;
        }

        string WheelLines()
        {
            var summon = player.Summoner;
            sb.Clear();
            if (summon.Ready.Count == 0) return "";
            int selected = Mathf.Clamp(summon.Selected, 0, summon.Ready.Count - 1);
            return sb.Append("<color=#F6D98B>").Append(summon.Ready[selected].word).Append("</color>").ToString();
        }
    }
}
