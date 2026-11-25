using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class MatchTally : MonoBehaviour
    {
        public const string CareerKey = "wv.career";
        static MatchTally current;
        readonly HashSet<PlayerController> watched = new HashSet<PlayerController>();
        readonly KnockoutWatch knockouts = new KnockoutWatch();
        readonly Dictionary<PlayerController, Action<string>> spellers = new Dictionary<PlayerController, Action<string>>();
        readonly Dictionary<Smashable, int> lastToHit = new Dictionary<Smashable, int>();
        readonly List<Smashable> justBroken = new List<Smashable>();
        PlayerController local;
        RoomBuilder room;
        float damage, roundDamage, matchStarted;
        int crafted, broken, roundCrafted, roundBroken;
        bool live;

        public static MatchRecord LastResult { get; set; }
        public static bool LastWasBest { get; private set; }
        public static int RoundBroken => current ? current.roundBroken : 0;
        public static int RoundCrafted => current ? current.roundCrafted : 0;
        public static int RoundDamage => current ? Mathf.CeilToInt(current.roundDamage) : 0;
        public static bool StatsLive => current && current.live;
        public static bool StatsRecording => current && current.Recording;

        static bool WarmingUp => RoundManager.Instance && RoundManager.Instance.Phase == Phase.Warmup;

        bool Recording
        {
            get
            {
                if (!live) return false;
                var rounds = RoundManager.Instance;
                return !rounds || !rounds.isActiveAndEnabled || rounds.Phase == Phase.Playing;
            }
        }

        public const string CorruptKey = CareerKey + ".corrupt";

        public static string SetAside { get; private set; }

        public static Career LoadCareer()
        {
            string text = PlayerPrefs.GetString(CareerKey, "");
            if (Career.TryDeserialize(text, out var career)) return career;
            string stamp = CorruptKey + "." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string key = stamp;
            for (int n = 2; PlayerPrefs.HasKey(key); n++) key = stamp + "-" + n;
            PlayerPrefs.SetString(key, text);
            PlayerPrefs.SetString(CorruptKey, key);
            PlayerPrefs.DeleteKey(CareerKey);
            PlayerPrefs.Save();
            SetAside = key;
            Debug.LogWarning("[Wreckabulary] The career save could not be read. It was kept as " + key + " and a new career started.");
            return career;
        }

        public static string TakeSetAside()
        {
            string key = SetAside;
            SetAside = null;
            return key;
        }

        public static void SaveCareer(Career career)
        {
            PlayerPrefs.SetString(CareerKey, career.Serialize());
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == Session.HubScene || scene.name == Session.TutorialScene) return;
            if (!FindAnyObjectByType<PlayerJoinManager>()) return;
            MatchStats.Begin();
            current = new GameObject("Match tally").AddComponent<MatchTally>();
        }

        void Update()
        {
            bool joined = false;
            foreach (var player in World.Players)
            {
                if (!player || watched.Contains(player)) continue;
                watched.Add(player);
                joined = true;
                if (player.Health)
                {
                    player.Health.Damaged += OnDamaged;
                    player.Health.Revived += OnRevived;
                }
                if (player.Summoner) HearSpells(player);
                if (!local && player.Binding is not BotBinding && player.Binding is not ScriptedBinding)
                {
                    local = player;
                    if (player.Summoner) player.Summoner.Summoned += OnSummoned;
                }
            }
            if (joined) knockouts.Watch(World.Players);
            if (!live) return;
            var stats = MatchStats.Current;
            if (stats.Lines.Count != World.Players.Count) EnterSeats();
            var rounds = RoundManager.Instance;
            if (rounds) rounds.FeedScores(stats);
        }

        void LateUpdate()
        {
            if (justBroken.Count == 0) return;
            foreach (var piece in justBroken)
            {
                if (lastToHit.TryGetValue(piece, out int seat) || (seat = piece ? piece.LastAttacker : -1) >= 0) MatchStats.Current.Break(seat);
                lastToHit.Remove(piece);
            }
            justBroken.Clear();
        }

        void HearSpells(PlayerController player)
        {
            int seat = player.Index;
            Action<string> heard = _ => { if (Recording) MatchStats.Current.Craft(seat); };
            player.Summoner.Summoned += heard;
            spellers[player] = heard;
        }

        void OnEnable()
        {
            Smashable.AnyBroken += OnBroken;
            PlayerCombat.Contacted += OnContact;
            knockouts.KnockedOut += OnKnockedOut;
        }

        void OnDisable()
        {
            Smashable.AnyBroken -= OnBroken;
            PlayerCombat.Contacted -= OnContact;
            knockouts.KnockedOut -= OnKnockedOut;
        }

        void OnBroken(Smashable smashable)
        {
            if (!room) room = FindAnyObjectByType<RoomBuilder>();
            if (!room) return;
            foreach (var original in room.Originals)
                if (original == smashable)
                {
                    broken++; roundBroken++;
                    if (Recording) justBroken.Add(smashable);
                    return;
                }
        }

        void OnContact(MeleeContact contact)
        {
            if (!live || !contact.Attacker || !contact.Target || !contact.Target.TryGetComponent(out Smashable piece)) return;
            if (justBroken.Remove(piece))
            {
                MatchStats.Current.Break(contact.Attacker.Index);
                lastToHit.Remove(piece);
            }
            else if (!piece.IsBroken) lastToHit[piece] = contact.Attacker.Index;
        }

        void OnKnockedOut(PlayerController victim, PlayerController attacker)
        {
            if (Recording && victim) MatchStats.Current.Knockout(victim.Index, attacker ? attacker.Index : -1);
        }

        void OnRevived(PlayerHealth saved)
        {
            if (!Recording || !saved) return;
            foreach (var player in World.Players)
            {
                if (!player || !player.Combat) continue;
                var helped = player.Combat.Reviving;
                if (!helped || helped.Health != saved) continue;
                MatchStats.Current.Revive(player.Index);
                return;
            }
        }

        void OnDestroy()
        {
            foreach (var player in watched)
            {
                if (!player || !player.Health) continue;
                player.Health.Damaged -= OnDamaged;
                player.Health.Revived -= OnRevived;
            }
            foreach (var entry in spellers)
                if (entry.Key && entry.Key.Summoner) entry.Key.Summoner.Summoned -= entry.Value;
            spellers.Clear();
            knockouts.Clear();
            if (local && local.Summoner) local.Summoner.Summoned -= OnSummoned;
            if (current == this) current = null;
        }

        void OnDamaged(PlayerHealth victim, HitInfo hit, HitResult result)
        {
            if (result.Damage > 0f && hit.AttackerId >= 0 && Recording && !(victim.TryGetComponent(out PlayerController hurt) && hurt.Index == hit.AttackerId))
                MatchStats.Current.Hurt(hit.AttackerId, result.Damage);
            if (!local || hit.AttackerId != local.Index || victim == local.Health || result.Damage <= 0f || WarmingUp) return;
            int before = Mathf.RoundToInt(damage);
            damage += result.Damage;
            roundDamage += result.Damage;
            var player = victim.GetComponent<PlayerController>();
            Popup.Points(Mathf.RoundToInt(damage) - before, player ? player.OverheadPosition : victim.transform.position + Vector3.up * 2f);
        }

        void OnSummoned(string word) { crafted++; roundCrafted++; }

        public static void BeginRound()
        {
            EnterSeats();
            if (current) current.ResetRound();
        }

        public static void BeginMatch()
        {
            MatchStats.Begin();
            if (!current) return;
            current.ResetRound();
            current.broken = current.crafted = 0;
            current.damage = 0f;
            current.matchStarted = Time.realtimeSinceStartup;
            current.live = true;
        }

        public static void Idle()
        {
            MatchStats.Begin();
            if (current) current.live = false;
        }

        public static void EnterSeats()
        {
            var stats = MatchStats.Current;
            foreach (var player in World.Players)
                if (player) stats.Enter(player.Index, player.Name, player.Team, player.Binding is BotBinding);
        }

        void ResetRound()
        {
            roundBroken = roundCrafted = 0;
            roundDamage = 0f;
            justBroken.Clear();
            lastToHit.Clear();
            knockouts.ForgetAll();
        }

        public static MatchRecord Finish(bool won)
        {
            if (!current) return null;
            current.live = false;
            return current.Record(won);
        }

        public static MatchRecord FinishFor(PlayerController winner)
        {
            if (!current) return null;
            current.live = false;
            return current.Record(winner && current.local && winner.Team == current.local.Team);
        }

        int SecondsSinceStart()
        {
            if (matchStarted <= 0f) return 0;
            int seconds = Mathf.Max(0, Mathf.FloorToInt(Time.realtimeSinceStartup - matchStarted));
            matchStarted = 0f;
            return seconds;
        }

        MatchRecord Record(bool won)
        {
            if (!local) return null;
            var career = LoadCareer();
            var record = Career.Reward(Match.Mode, Session.MapId, true, won, broken, crafted, Mathf.RoundToInt(damage),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            LastWasBest = career.Record(record);
            career.SecondsPlayed += SecondsSinceStart();
            LastResult = record;
            SaveCareer(career);
            LobbyRecentPlayers.Note(record, World.Players, local);
            damage = 0; crafted = 0; broken = 0;
            return record;
        }
    }
}
