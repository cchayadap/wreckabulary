using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum SeatState { Connected, Disconnected, Left }

    /// <summary>
    /// One player in the session. <see cref="PlayerId"/> is the id every rule uses (inventory,
    /// items, health). It never changes and is never reused, so a player who reconnects gets the
    /// same letters and items back.
    /// </summary>
    public sealed class Seat
    {
        public int PlayerId { get; internal set; }
        /// <summary>A random id saved on the player's machine, plus the couch seat on that machine.</summary>
        public string PlayerKey { get; internal set; }
        public int LocalSlot { get; internal set; }
        /// <summary>The network connection this seat currently arrives on. It changes after a reconnect.</summary>
        public ulong ClientId { get; internal set; }
        public string Name { get; set; }
        public int Team { get; set; }
        public bool Ready { get; set; }
        public SeatState State { get; internal set; }
        public double DisconnectedAt { get; internal set; }

        public override string ToString() => $"P{PlayerId} {Name} ({State})";
    }

    public enum JoinResult { Joined, Rejoined, AlreadyConnected, Full, Closed }

    /// <summary>
    /// Who is in the session (server only). Couch players on one machine share a client and are
    /// told apart by their local slot. A dropped player keeps their seat for the grace period.
    /// </summary>
    public sealed class PlayerRoster
    {
        readonly List<Seat> seats = new List<Seat>();
        int nextPlayerId;

        public int MaxPlayers { get; }
        public double GraceSeconds { get; }
        /// <summary>False once a round is running: new players spectate until the next one.</summary>
        public bool JoiningOpen { get; set; } = true;

        public PlayerRoster(int maxPlayers, double graceSeconds)
        {
            if (maxPlayers < 1) throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            MaxPlayers = maxPlayers;
            GraceSeconds = graceSeconds;
        }

        public IReadOnlyList<Seat> Seats => seats;
        public IEnumerable<Seat> Active => seats.Where(s => s.State != SeatState.Left);
        public IEnumerable<Seat> Connected => seats.Where(s => s.State == SeatState.Connected);

        public Seat ById(int playerId) => seats.FirstOrDefault(s => s.PlayerId == playerId);

        public Seat Find(string playerKey, int localSlot) =>
            seats.FirstOrDefault(s => s.State != SeatState.Left && s.PlayerKey == playerKey && s.LocalSlot == localSlot);

        public JoinResult Join(string playerKey, int localSlot, ulong clientId, string name, out Seat seat)
        {
            if (string.IsNullOrEmpty(playerKey)) throw new ArgumentException("A player key is required.", nameof(playerKey));
            seat = Find(playerKey, localSlot);
            if (seat != null)
            {
                if (seat.State == SeatState.Connected) return JoinResult.AlreadyConnected;
                seat.State = SeatState.Connected;
                seat.ClientId = clientId;
                return JoinResult.Rejoined;
            }
            if (!JoiningOpen) return JoinResult.Closed;
            if (Active.Count() >= MaxPlayers) return JoinResult.Full;
            seat = new Seat
            {
                PlayerId = nextPlayerId++, PlayerKey = playerKey, LocalSlot = localSlot, ClientId = clientId,
                Name = string.IsNullOrEmpty(name) ? $"P{nextPlayerId}" : name, State = SeatState.Connected,
            };
            seats.Add(seat);
            return JoinResult.Joined;
        }

        /// <summary>A connection dropped: every seat on it waits for a reconnect.</summary>
        public List<Seat> Disconnect(ulong clientId, double now)
        {
            var dropped = seats.Where(s => s.State == SeatState.Connected && s.ClientId == clientId).ToList();
            foreach (var s in dropped)
            {
                s.State = SeatState.Disconnected;
                s.DisconnectedAt = now;
            }
            return dropped;
        }

        /// <summary>A player chose to leave: the seat is gone at once.</summary>
        public Seat Leave(int playerId)
        {
            var s = ById(playerId);
            if (s != null) s.State = SeatState.Left;
            return s;
        }

        /// <summary>Seats whose grace period ran out on this tick. The match eliminates them.</summary>
        public List<Seat> Expire(double now)
        {
            var gone = seats.Where(s => s.State == SeatState.Disconnected && now - s.DisconnectedAt >= GraceSeconds).ToList();
            foreach (var s in gone) s.State = SeatState.Left;
            return gone;
        }
    }
}
