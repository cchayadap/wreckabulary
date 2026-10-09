using System;
using System.Collections.Generic;
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
        PlayerController local;
        RoomBuilder room;
        float damage, roundDamage;
        int crafted, broken, roundCrafted, roundBroken;

        public static MatchRecord LastResult { get; set; }
        public static bool LastWasBest { get; private set; }
        public static int RoundBroken => current ? current.roundBroken : 0;
        public static int RoundCrafted => current ? current.roundCrafted : 0;
        public static int RoundDamage => current ? Mathf.CeilToInt(current.roundDamage) : 0;

        public static Career LoadCareer() => Career.Deserialize(PlayerPrefs.GetString(CareerKey, ""));

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
            current = new GameObject("Match tally").AddComponent<MatchTally>();
        }

        void Update()
        {
            foreach (var player in World.Players)
            {
                if (!player || watched.Contains(player)) continue;
                watched.Add(player);
                if (player.Health) player.Health.Damaged += OnDamaged;
                if (!local && player.Binding is not BotBinding && player.Binding is not ScriptedBinding)
                {
                    local = player;
                    if (player.Summoner) player.Summoner.Summoned += OnSummoned;
                }
            }
        }

        void OnEnable() => Smashable.AnyBroken += OnBroken;
        void OnDisable() => Smashable.AnyBroken -= OnBroken;

        void OnBroken(Smashable smashable)
        {
            if (!room) room = FindAnyObjectByType<RoomBuilder>();
            if (!room) return;
            foreach (var original in room.Originals)
                if (original == smashable) { broken++; roundBroken++; return; }
        }

        void OnDestroy()
        {
            foreach (var player in watched)
                if (player && player.Health) player.Health.Damaged -= OnDamaged;
            if (local && local.Summoner) local.Summoner.Summoned -= OnSummoned;
            if (current == this) current = null;
        }

        void OnDamaged(PlayerHealth victim, HitInfo hit, HitResult result)
        {
            if (local && hit.AttackerId == local.Index && victim != local.Health) { damage += result.Damage; roundDamage += result.Damage; }
        }

        void OnSummoned(string word) { crafted++; roundCrafted++; }

        public static void BeginRound()
        {
            if (!current) return;
            current.roundBroken = current.roundCrafted = 0;
            current.roundDamage = 0f;
        }

        public static void BeginMatch()
        {
            if (!current) return;
            BeginRound();
            current.broken = current.crafted = 0;
            current.damage = 0f;
        }

        public static MatchRecord Finish(bool won) => current ? current.Record(won) : null;

        public static MatchRecord FinishFor(PlayerController winner) =>
            current ? current.Record(winner && current.local && winner.Team == current.local.Team) : null;

        MatchRecord Record(bool won)
        {
            if (!local) return null;
            var career = LoadCareer();
            var record = Career.Reward(Match.Mode, Session.MapId, true, won, broken, crafted, Mathf.RoundToInt(damage),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            LastWasBest = career.Record(record);
            LastResult = record;
            SaveCareer(career);
            damage = 0; crafted = 0; broken = 0;
            return record;
        }
    }
}
