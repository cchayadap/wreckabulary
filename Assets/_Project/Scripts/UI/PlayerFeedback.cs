using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class PlayerFeedback : MonoBehaviour
    {
        PlayerController player;
        int letters;
        bool linked;

        public void Initialize(PlayerController controller)
        {
            if (linked || !controller.Health || !controller.Inventory || !controller.Summoner) return;
            player = controller; linked = true; letters = player.Inventory.Count;
            player.Inventory.Changed += LettersChanged;
            player.Summoner.Summoned += Crafted;
            player.Health.Damaged += Damaged;
            player.Health.Eliminated += Wrecked;
            player.Jumped += Jumped; player.Dodged += Dodged;
        }

        void LettersChanged()
        {
            int count = player.Inventory.Count;
            // A disabling summoner still refunds its reservation, but cannot spawn scene effects.
            if (count > letters && isActiveAndEnabled && !player.Summoner.IsDisabling)
            {
                GameFeedback.Play(GameCue.Pickup);
                GameFeedback.Burst("Pickup_Ring", player.transform.position + Vector3.up * .35f, .28f, player.Color, .25f);
            }
            letters = count;
        }
        void Crafted(string word)
        {
            GameFeedback.Play(GameCue.Craft);
            GameFeedback.Burst("Craft_Ring", player.transform.position + Vector3.up * .3f, 1.1f, new Color(.38f, .79f, 1f), .65f);
        }
        void Damaged(PlayerHealth health, HitInfo hit, HitResult result)
        {
            GameFeedback.Play(result.Blocked || result.Absorbed > 0f ? GameCue.Block : GameCue.Hit);
            if (result.Blocked) GameFeedback.Burst("Impact_Star", player.transform.position + Vector3.up * .8f + player.Facing * .45f, .6f, GameFeedback.SkillColor("PLATE"), .25f);
            else if (result.Absorbed > 0f) GameFeedback.Burst("Foam_Cloud", player.transform.position + Vector3.up * .9f, .7f, GameFeedback.SkillColor("FOAM"), .3f);
            else if (result.Damage > 0) GameFeedback.Burst("Impact_Star", player.transform.position + Vector3.up * 1.1f, .5f, new Color(1f, .59f, .36f), .3f);
        }
        void Wrecked(PlayerHealth health) => GameFeedback.Play(GameCue.Out);
        void Jumped(PlayerController controller) { GameFeedback.Play(GameCue.Jump); GameFeedback.Burst("Jump_Arrow", player.transform.position, 0.45f); }
        void Dodged(PlayerController controller) { GameFeedback.Play(GameCue.Dodge); GameFeedback.Burst("Speed_Trail", player.transform.position, 0.55f); }

        void OnDestroy()
        {
            if (!linked || !player) return;
            if (player.Inventory) player.Inventory.Changed -= LettersChanged;
            if (player.Summoner) player.Summoner.Summoned -= Crafted;
            if (player.Health) { player.Health.Damaged -= Damaged; player.Health.Eliminated -= Wrecked; }
            player.Jumped -= Jumped; player.Dodged -= Dodged;
        }
    }
}
