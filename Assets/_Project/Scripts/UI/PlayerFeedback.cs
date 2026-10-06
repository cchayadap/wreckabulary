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
            if (count > letters) GameFeedback.Play(GameCue.Pickup);
            letters = count;
        }
        void Crafted(string word)
        {
            GameFeedback.Play(GameCue.Craft);
            GameFeedback.Burst("Pickup_Ring", player.OverheadPosition, 0.75f);
        }
        void Damaged(PlayerHealth health, HitInfo hit, HitResult result)
        {
            GameFeedback.Play(result.Blocked ? GameCue.Block : GameCue.Hit);
            if (result.Damage > 0) GameFeedback.Burst("Impact_Star", player.OverheadPosition, 0.5f);
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
