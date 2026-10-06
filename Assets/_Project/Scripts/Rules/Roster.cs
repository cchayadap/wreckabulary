using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum SeatState { Connected, Disconnected, Left }

    public sealed class Seat
    {
        public int PlayerId { get; internal set; }
        public string PlayerKey { get; internal set; }
        public int LocalSlot { get; internal set; }
        public ulong ClientId { get; internal set; }
        public string Name { get; set; }
        public int Team { get; set; }
        public bool Ready { get; set; }
        public SeatState State { get; internal set; }
        public double DisconnectedAt { get; internal set; }

        public override string ToString() => $"P{PlayerId} {Name} ({State})";
    }

    public enum JoinResult { Joined, Rejoined, AlreadyConnected, Full, Closed }

    public sealed class PlayerRoster
    {
        readonly List<Seat> seats = new List<Seat>();
        int nextPlayerId;

        public int MaxPlayers { get; }
        public double GraceSeconds { get; }
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

        public Seat Leave(int playerId)
        {
            var s = ById(playerId);
            if (s != null) s.State = SeatState.Left;
            return s;
        }

        public List<Seat> Expire(double now)
        {
            var gone = seats.Where(s => s.State == SeatState.Disconnected && now - s.DisconnectedAt >= GraceSeconds).ToList();
            foreach (var s in gone) s.State = SeatState.Left;
            return gone;
        }
    }
}
