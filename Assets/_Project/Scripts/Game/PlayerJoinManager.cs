using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    /// <summary>
    /// Drop-in join: press a button on keyboard and mouse, either keyboard half or any gamepad to spawn a roommate.
    /// Roommates who joined in an earlier scene (see <see cref="Session"/>) are brought back automatically.
    /// </summary>
    public class PlayerJoinManager : MonoBehaviour
    {
        [SerializeField] PlayerController playerPrefab;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] Transform playersRoot;
        [SerializeField] int maxPlayers = 4;

        [Header("Arrival")]
        [Tooltip("If set, new roommates walk this way on arrival (through the front door in the house).")]
        [SerializeField] Vector3 walkIn;
        [SerializeField] float walkInTime = 0.5f;
        [SerializeField] int starterLetters = 3;

        [Header("Knockouts")]
        [SerializeField] bool respawnKnockedOut = true;
        [Tooltip("Used when the mode's rules don't set a respawn time.")]
        [SerializeField] float respawnDelay = 2f;

        readonly List<PlayerController> players = new();
        readonly KeyboardBinding keyboardLeft = new(KeyboardBinding.Side.Left);
        readonly KeyboardBinding keyboardRight = new(KeyboardBinding.Side.Right);

        public IReadOnlyList<PlayerController> Players => players;
        public bool AllowJoining { get; set; } = true;
        /// <summary>When true, wrecked roommates get back up after a short delay (house, tutorial, lobby).</summary>
        public bool RespawnKnockedOut { get => respawnKnockedOut; set => respawnKnockedOut = value; }
        /// <summary>True if the players were carried over from another scene.</summary>
        public bool RestoredFromSession { get; private set; }
        public int StarterLetters { get => starterLetters; set => starterLetters = Mathf.Clamp(value, 0, 6); }
        public event Action<PlayerController> Joined;
        Vector3[] layoutSpawns;

        public int HumanCount => players.Count(p => p.Binding is not BotBinding);

        public void ConfigureLayout(Rules.HouseLayout layout)
        {
            layoutSpawns = layout.Spawns.Select(s => new Vector3(s.X, layout.Room(s.Room).FloorY, s.Z)).ToArray();
            walkIn = Vector3.zero;
        }

        public void AssignTeams()
        {
            var teams = Rules.Teams.Assign(players.Count, Match.Rules.TeamSize);
            for (int i = 0; i < players.Count; i++) players[i].Team = teams[i];
        }

        public void EnsureOpponents()
        {
            if (HumanCount == 0) return;
            int target = 4;
            while (players.Count < target && players.Count < maxPlayers)
            {
                var binding = new BotBinding();
                var p = Join(binding, arriving: false);
                p.Name = $"AI {p.Index + 1}";
                p.gameObject.AddComponent<BotController>().Configure(p, binding);
            }
            AssignTeams();
        }

        void Awake()
        {
            // Restore in Awake so every other script's Start already sees the players.
            foreach (var b in Session.Bindings.ToArray())
            {
                if (b is GamepadBinding g && !g.Pad.added) continue;
                Join(b, arriving: false);
                RestoredFromSession = true;
            }
        }

        void Update()
        {
            if (!AllowJoining || players.Count >= maxPlayers) return;

            // Keyboard and mouse and the left keyboard half both use WASD, so only one of them can play.
            if (!HasJoined(keyboardLeft.Id)) TryJoin(DesktopBinding.Shared);
            if (!HasJoined(DesktopBinding.Shared.Id)) TryJoin(keyboardLeft);
            TryJoin(keyboardRight);
            foreach (var pad in Gamepad.all)
                if (players.All(p => p.Binding is not GamepadBinding g || g.Pad != pad))
                {
                    var binding = new GamepadBinding(pad);
                    if (binding.JoinPressed()) Join(binding);
                }
        }

        void TryJoin(InputBinding binding)
        {
            if (HasJoined(binding.Id)) return;
            if (binding.JoinPressed()) Join(binding);
        }

        // Compare by id: bindings restored from the session are different objects for the same keys.
        bool HasJoined(string bindingId) => players.Any(p => p.Binding.Id == bindingId);

        public PlayerController Join(InputBinding binding) => Join(binding, arriving: true);

        PlayerController Join(InputBinding binding, bool arriving)
        {
            if (players.Count >= maxPlayers) return null;
            int index = players.Count;
            var prefab = playerPrefab ? playerPrefab : GameAssets.I.playerPrefab;
            var p = Instantiate(prefab, SpawnPoint(index), Quaternion.identity, playersRoot);
            p.Setup(index, binding);
            // Everyone arrives dressed the way they left the wardrobe.
            Wreckabulary.Looks.Apply(p, Session.Looks.TryGetValue(index, out var look) ? look : Wreckabulary.Looks.Default(index));
            players.Add(p);
            Session.Remember(binding);
            p.Health.Eliminated += _ => { if (respawnKnockedOut) StartCoroutine(RespawnLater(p)); };
            Place(p);
            if (arriving)
            {
                Popup.Show($"{p.Name} joined!", p.OverheadPosition, p.Color, 4f);
                Sfx.Play(Sound.Join, p.transform.position);
            }
            Joined?.Invoke(p);
            return p;
        }

        /// <summary>Puts a roommate at their spawn point with starter letters, walking in if set up to.</summary>
        public void Place(PlayerController p)
        {
            p.Combat.ResetForRound();
            p.Summoner.CancelCraft();
            p.Summoner.Close();
            p.Health.ResetForRound();
            p.Respawn(SpawnPoint(p.Index));
            GiveStarterLetters(p);
            if (walkIn.sqrMagnitude > 0.01f)
            {
                p.FaceTowards(walkIn);
                p.AutoWalk(walkIn.normalized, walkInTime);
            }
        }

        IEnumerator RespawnLater(PlayerController p)
        {
            float rulesDelay = p.Health.Rules.RespawnSeconds;
            yield return new WaitForSeconds(rulesDelay > 0f ? rulesDelay : respawnDelay);
            if (p && p.IsEliminated && respawnKnockedOut) Place(p);
        }

        public Vector3 SpawnPoint(int index) => layoutSpawns != null && layoutSpawns.Length > 0
            ? layoutSpawns[index % layoutSpawns.Length]
            : spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[index % spawnPoints.Length].position : new Vector3(index * 2f - 3f, 0f, -2f);

        public bool AnyStartPressed() => players.Any(p => p.Commands.start || (p.Binding != null && p.Binding.StartPressed()));

        /// <summary>The mode's configured starter letters; the normal arena starts empty.</summary>
        public void GiveStarterLetters(PlayerController p)
        {
            p.Inventory.Set(Match.Rules.StarterLetters ?? "");
        }
    }
}
