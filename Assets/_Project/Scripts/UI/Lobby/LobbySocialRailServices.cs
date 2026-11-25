using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class LobbyServiceCheck
    {
        const string QaScenarioArgument = "-qa-scenario";
        static Func<CancellationToken, Task> call;

        public static bool Enabled { get; set; }

        public static Func<CancellationToken, Task> Call => Enabled ? call : null;

        public static Func<CancellationToken, Task> Register(Func<CancellationToken, Task> check)
        {
            var previous = call;
            call = check;
            return previous;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetOnLaunch() =>
            Enabled = !Application.isEditor && Array.IndexOf(Environment.GetCommandLineArgs(), QaScenarioArgument) < 0;
    }

    public static class LobbyRecentPlayers
    {
        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static RecentPlayers Load() => RecentPlayers.Load(PlayerPrefs.GetString(RecentPlayers.StorageKey, ""), Now);

        public static void Save(RecentPlayers recent)
        {
            PlayerPrefs.SetString(RecentPlayers.StorageKey, recent.Save());
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(RecentPlayers.StorageKey);
            PlayerPrefs.Save();
        }

        public static bool Note(MatchRecord record, IEnumerable<PlayerController> players, PlayerController you)
        {
            if (record == null || players == null) return false;
            var seats = new List<RecentPlayer>();
            foreach (var player in players)
                if (player && player.Binding is not BotBinding) seats.Add(new RecentPlayer(player.Name, player.Index, player == you));
            var recent = Load();
            if (!recent.Add(new RecentMatch(record.Mode, record.Map, record.EndedAt, seats), Now)) return false;
            Save(recent);
            return true;
        }
    }
}
