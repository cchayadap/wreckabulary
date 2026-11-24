using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
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

        [Header("Knockouts")]
        [SerializeField] bool respawnKnockedOut = true;
        [Tooltip("Used when the mode's rules don't set a respawn time.")]
        [SerializeField] float respawnDelay = 2f;

        readonly List<PlayerController> players = new();
        readonly KeyboardBinding keyboardLeft = new(KeyboardBinding.Side.Left);
        readonly KeyboardBinding keyboardRight = new(KeyboardBinding.Side.Right);
        readonly Dictionary<Gamepad, GamepadBinding> waitingPads = new();

        public IReadOnlyList<PlayerController> Players => players;
        public bool AllowJoining { get; set; } = true;
        public bool RespawnKnockedOut { get => respawnKnockedOut; set => respawnKnockedOut = value; }
        public bool RestoredFromSession { get; private set; }
        public event Action<PlayerController> Joined;
        Vector3[] layoutSpawns;
        string[] spawnRooms;
        Rules.HouseLayout spawnLayout;

        public int HumanCount => players.Count(p => p.Binding is not BotBinding);
        public int MaxPlayers { get => maxPlayers; set => maxPlayers = Mathf.Clamp(value, 1, 8); }
        public int SpawnCount => layoutSpawns?.Length ?? (spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints.Length : 4);
        public Rules.Spot SpawnSpot(int spawn)
        {
            var at = layoutSpawns != null && layoutSpawns.Length > 0 ? layoutSpawns[spawn % layoutSpawns.Length] : SpawnPoint(spawn);
            return new Rules.Spot(at.x, at.y, at.z);
        }

        public void ConfigureLayout(Rules.HouseLayout layout)
        {
            layoutSpawns = layout.Spawns.Select(s => new Vector3(s.X, layout.Room(s.Room).FloorY, s.Z)).ToArray();
            spawnRooms = layout.Spawns.Select(s => s.Room).ToArray();
            spawnLayout = layout;
            walkIn = Vector3.zero;
        }

        public void AssignTeams()
        {
            int sides = Rules.HouseModes.Find(Match.Mode)?.Sides ?? 0;
            var human = players.Select(p => p.Binding is not BotBinding).ToArray();
            var teams = Rules.Teams.For(players.Count, Match.Rules.TeamSize, sides, human);
            teamPlaces = Rules.Teams.Places(players.Count, Match.Rules.TeamSize, sides, human);
            for (int i = 0; i < players.Count; i++) players[i].Team = teams[i];
        }

        int[] teamPlaces;

        public int SpawnSeat(PlayerController p) => teamPlaces != null && p.Index < teamPlaces.Length ? teamPlaces[p.Index] : p.Index;

        public void EnsureOpponents()
        {
            if (HumanCount == 0) return;
            int target = Rules.HouseModes.BotSeats(Match.Mode);
            if (target == 0) { AssignTeams(); return; }
            if (target > maxPlayers) MaxPlayers = target;
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
            foreach (var b in Session.Bindings.ToArray())
            {
                if (b is GamepadBinding g && !g.Pad.added) continue;
                if (b is BotBinding && Rules.HouseModes.BotSeats(Match.Mode) == 0) continue;
                Join(b, arriving: false);
                RestoredFromSession = true;
            }
        }

        void Update()
        {
            if (Time.timeScale <= 0f || !AllowJoining || players.Count >= maxPlayers) return;

            if (!HasJoined(keyboardLeft.Id)) TryJoin(DesktopBinding.Shared);
            if (!HasJoined(DesktopBinding.Shared.Id)) TryJoin(keyboardLeft);
            TryJoin(keyboardRight);
            foreach (var pad in Gamepad.all)
            {
                if (PadJoined(pad)) continue;
                if (!waitingPads.TryGetValue(pad, out var binding)) waitingPads[pad] = binding = new GamepadBinding(pad);
                if (!binding.JoinPressed()) continue;
                waitingPads.Remove(pad);
                Join(binding);
            }
        }

        void TryJoin(InputBinding binding)
        {
            if (HasJoined(binding.Id)) return;
            if (binding.JoinPressed()) Join(binding);
        }

        bool HasJoined(string bindingId)
        {
            foreach (var p in players)
                if (p.Binding.Id == bindingId) return true;
            return false;
        }

        bool PadJoined(Gamepad pad)
        {
            foreach (var p in players)
                if (p.Binding is GamepadBinding g && g.Pad == pad) return true;
            return false;
        }

        public PlayerController Join(InputBinding binding) => Join(binding, arriving: true);

        PlayerController Join(InputBinding binding, bool arriving)
        {
            if (players.Count >= maxPlayers) return null;
            int index = players.Count;
            var prefab = playerPrefab ? playerPrefab : GameAssets.I.playerPrefab;
            var p = Instantiate(prefab, SpawnPoint(index), Quaternion.identity, playersRoot);
            p.Setup(index, binding);
            players.Add(p);
            Session.Remember(binding);
            p.Health.Eliminated += _ => { if (respawnKnockedOut) StartCoroutine(RespawnLater(p)); };
            Place(p);
            if (arriving) Popup.Show($"{p.Name} joined!", p.OverheadPosition, p.Color, 4f);
            Joined?.Invoke(p);
            return p;
        }

        public void Place(PlayerController p) => Place(p, SpawnSeat(p));

        public void Place(PlayerController p, int spawn)
        {
            p.Combat.ResetForRound();
            p.Summoner.CancelCraft();
            p.Summoner.Close();
            p.Health.ResetForRound();
            p.Respawn(SpawnPoint(spawn));
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
            ? RoomySpawn(index % layoutSpawns.Length)
            : spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[index % spawnPoints.Length].position : new Vector3(index * 2f - 3f, 0f, -2f);

        Vector3 RoomySpawn(int i)
        {
            Physics.SyncTransforms();
            string room = spawnRooms[i];
            return ShoulderView.RoomySpot(layoutSpawns[i], at => spawnLayout.RoomAt(at.x, at.y + .1f, at.z) == room);
        }

        public bool AnyStartPressed() => players.Any(p => p.Commands.start || (p.Binding != null && p.Binding.StartPressed()));

        public void GiveStarterLetters(PlayerController p)
        {
            p.Inventory.Set(Match.Rules.StarterLetters ?? "");
        }
    }
}
