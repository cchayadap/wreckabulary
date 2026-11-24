using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class LetterStash : MonoBehaviour
    {
        public const int Capacity = 24;
        readonly List<char> letters = new();
        PlayerController owner;
        int team = Teams.NoTeam;

        public IReadOnlyList<char> Letters => letters;
        public int Count => letters.Count;
        public bool IsEmpty => letters.Count == 0;

        public void Open(PlayerController placedBy)
        {
            owner = placedBy;
            team = placedBy ? placedBy.Team : Teams.NoTeam;
        }

        public bool CanOpen(PlayerController player) =>
            player && owner && (player == owner || Teams.AreTeammates(player.Team, team));

        public bool WantsExchange(PlayerController player)
        {
            if (!CanOpen(player) || !player.Inventory) return false;
            var bag = player.Inventory;
            return (!bag.IsEmpty && letters.Count < Capacity) || (bag.IsEmpty && letters.Count > 0);
        }

        public bool TryExchange(PlayerController player)
        {
            if (!WantsExchange(player)) return false;
            var bag = player.Inventory;
            var at = transform.position + Vector3.up * 1.1f;
            if (!bag.IsEmpty)
            {
                int room = Capacity - letters.Count;
                var moved = new List<char>();
                foreach (char c in bag.Letters)
                {
                    if (moved.Count >= room) break;
                    moved.Add(c);
                }
                if (!bag.TrySpend(new string(moved.ToArray()))) return false;
                letters.AddRange(moved);
                Popup.Show($"BANKED {moved.Count} · {letters.Count} INSIDE", at, Color.white, 3f);
            }
            else
            {
                int taken = 0;
                while (letters.Count > 0 && !bag.IsFull && bag.TryAdd(letters[letters.Count - 1]))
                {
                    letters.RemoveAt(letters.Count - 1);
                    taken++;
                }
                if (taken == 0) return false;
                Popup.Show($"TOOK {taken} · {letters.Count} LEFT", at, Color.white, 3f);
            }
            GameFeedback.Play(GameCue.Pickup);
            return true;
        }

        public void Spill()
        {
            if (letters.Count == 0) return;
            var word = new string(letters.ToArray());
            letters.Clear();
            if (TilePool.Instance) TilePool.Instance.Burst(word, transform.position + Vector3.up * 0.5f, 3f);
        }
    }
}
