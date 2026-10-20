using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class OnlineSeatPresence
    {
        public string PlayerId { get; }
        public long ReconnectDeadline { get; }
        public bool Returned { get; }
        public OnlineSeatPresence(string playerId, long reconnectDeadline, bool returned)
        {
            if (reconnectDeadline < 0) throw new ArgumentOutOfRangeException(nameof(reconnectDeadline));
            PlayerId = playerId; ReconnectDeadline = reconnectDeadline; Returned = returned;
        }
    }

    public sealed class OnlineSessionGate
    {
        sealed class SeatState
        {
            public OnlineSeat Seat;
            public string Peer;
            public bool Ready, Seen, Returned;
            public long Deadline;
        }
        readonly Dictionary<string, SeatState> seats;
        readonly Dictionary<string, string> peers = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly long initialDeadline;
        public VerifiedOnlineMatch Match { get; }
        public bool Started { get; private set; }
        public bool Frozen { get; private set; }
        public int ConnectedCount => peers.Count;
        public bool AllReady => seats.Values.All(s => s.Peer != null && s.Ready && !s.Returned);

        public OnlineSessionGate(VerifiedOnlineMatch match)
        {
            Match = match ?? throw new ArgumentNullException(nameof(match));
            initialDeadline = checked(match.StartedAtMilliseconds + 90000);
            seats = match.Roster.ToDictionary(s => s.PlayerId, s => new SeatState { Seat = s }, StringComparer.Ordinal);
        }

        public bool RecognizesSecret(string secret)
        {
            try
            {
                if (OnlineJson.DecodeBase64Url(secret, 32).Length != 32) return false;
                string hash = OnlineJson.Hash(secret);
                return seats.Values.Any(s => !s.Returned && s.Seat.ConnectionSecretHash == hash);
            }
            catch (FormatException) { return false; }
        }

        public void Admit(VerifiedOnlineAdmission admission, string transportPeer, long nowMilliseconds)
        {
            if (admission == null || string.IsNullOrEmpty(transportPeer) || transportPeer.Length > 96 ||
                nowMilliseconds < 0 || admission.ExpiresAtSeconds <= nowMilliseconds / 1000 ||
                !seats.TryGetValue(admission.PlayerId, out var state) ||
                OnlineJson.Hash(state.Seat.ToValue()) != OnlineJson.Hash(admission.Seat.ToValue()) || state.Returned)
                throw new InvalidOperationException("Seat admission is not valid.");
            if (peers.TryGetValue(transportPeer, out string current))
            {
                if (current != admission.PlayerId) throw new InvalidOperationException("Peer identity changed.");
                return;
            }
            if (state.Peer != null) throw new InvalidOperationException("Seat is already connected.");
            if ((!Started && nowMilliseconds >= initialDeadline) ||
                (Started && (!state.Seen || state.Deadline == 0 || nowMilliseconds >= state.Deadline)))
                throw new InvalidOperationException("Seat admission deadline expired.");
            state.Peer = transportPeer; state.Seen = true; state.Ready = false; state.Deadline = 0;
            peers.Add(transportPeer, admission.PlayerId);
        }

        public string Actor(string transportPeer) => transportPeer != null && peers.TryGetValue(transportPeer, out var actor) ? actor : null;
        public bool CanCommand(string transportPeer) => Started && !Frozen && Actor(transportPeer) is string actor &&
            seats[actor].Ready && !seats[actor].Returned;
        public void Ready(string transportPeer)
        {
            string actor = Actor(transportPeer) ?? throw new InvalidOperationException("Peer has no admitted seat.");
            seats[actor].Ready = true;
        }
        public bool TryStart()
        {
            if (Started || Frozen || !AllReady) return false;
            Started = true; return true;
        }
        public string Disconnect(string transportPeer, long nowMilliseconds)
        {
            if (nowMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
            string actor = Actor(transportPeer);
            if (actor == null) return null;
            var state = seats[actor];
            peers.Remove(transportPeer); state.Peer = null; state.Ready = false;
            if (Started && !state.Returned && state.Deadline == 0) state.Deadline = checked(nowMilliseconds + 120000);
            return actor;
        }
        public void Return(string playerId)
        {
            if (!seats.TryGetValue(playerId, out var state)) throw new ArgumentException("Unknown original seat.");
            if (state.Peer != null) peers.Remove(state.Peer);
            state.Peer = null; state.Ready = false; state.Returned = true;
        }
        public void Freeze() => Frozen = true;
        public IReadOnlyList<OnlineSeatPresence> Presence() => seats.Values.Select(s =>
            new OnlineSeatPresence(s.Seat.PlayerId, s.Deadline, s.Returned)).ToArray();
        public void RestoreForMigration(IEnumerable<OnlineSeatPresence> presence, long nowMilliseconds)
        {
            var states = presence?.Take(9).ToArray() ?? Array.Empty<OnlineSeatPresence>();
            if (nowMilliseconds < 0 || states.Length != seats.Count || states.Any(s => s == null || !seats.ContainsKey(s.PlayerId)) ||
                states.Select(s => s.PlayerId).Distinct(StringComparer.Ordinal).Count() != seats.Count)
                throw new ArgumentException("Migration presence must cover the exact original roster.");
            peers.Clear(); Started = true; Frozen = true;
            foreach (var source in states)
            {
                var state = seats[source.PlayerId];
                state.Peer = null; state.Ready = false; state.Seen = true; state.Returned = source.Returned;
                state.Deadline = source.ReconnectDeadline == 0 ? checked(nowMilliseconds + 120000) : source.ReconnectDeadline;
            }
        }
        public void ResumeAfterRestore()
        {
            if (!Started || !Frozen || !seats.Values.Any(s => s.Peer != null && s.Ready && !s.Returned))
                throw new InvalidOperationException("Restore requires an admitted ready original seat.");
            Frozen = false;
        }
    }
}
