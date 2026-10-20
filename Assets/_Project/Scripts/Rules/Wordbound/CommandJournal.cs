using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public enum WordboundAction
    {
        CollectLetter, BeginCraft, CancelCraft, PickUp, Drop, Throw, Deploy, GiveLetter,
        Transfer, TakeKeepsake, DropKeepsake, UsePuzzle, Equip, Attack, PracticePuzzle
    }

    public sealed class PlayerCommand
    {
        public long Sequence { get; }
        public long Epoch { get; }
        public WordboundAction Action { get; }
        public string Target { get; }
        public int Revision { get; }
        public int Slot { get; }
        public string Value { get; }
        public string PayloadHash { get; }

        public PlayerCommand(long sequence, long epoch, WordboundAction action, string target = "",
            int revision = 0, int slot = -1, string value = "")
        {
            if (sequence < 1 || sequence == long.MaxValue || epoch < 0 || epoch == long.MaxValue ||
                !Enum.IsDefined(typeof(WordboundAction), action) || revision < 0 || slot < -1 || slot > 7 ||
                target == null || (target != "" && !WordboundIdentity.IsId(target)) ||
                value == null || (value != "" && !WordboundIdentity.IsId(value)))
                throw new ArgumentException("Invalid bounded player command.");
            Sequence = sequence; Epoch = epoch; Action = action; Target = target;
            Revision = revision; Slot = slot; Value = value;
            PayloadHash = WordboundIdentity.Sha256(CourseJson.Encode(new Dictionary<string, object>
            {
                ["action"] = action.ToString(), ["target"] = target, ["revision"] = revision,
                ["slot"] = slot, ["value"] = value
            }));
        }
    }

    public sealed class AuthenticatedCommand
    {
        public string ActorId { get; }
        public PlayerCommand Command { get; }
        public AuthenticatedCommand(string transportActorId, PlayerCommand command)
        {
            ActorId = transportActorId;
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }
    }

    public sealed class CommandEffect
    {
        public string Code { get; }
        public string EntityId { get; }
        public CommandEffect(string code, string entityId = "")
        {
            if (!WordboundIdentity.IsId(code) || entityId == null ||
                (entityId != "" && !WordboundIdentity.IsId(entityId))) throw new ArgumentException("Invalid command result.");
            Code = code; EntityId = entityId;
        }
    }

    public sealed class CommandReceipt
    {
        public string ActorId { get; }
        public long Sequence { get; }
        public string PayloadHash { get; }
        public long Tick { get; }
        public CommandEffect Effect { get; }
        internal CommandReceipt(AuthenticatedCommand command, long tick, CommandEffect effect)
        {
            ActorId = command.ActorId; Sequence = command.Command.Sequence;
            PayloadHash = command.Command.PayloadHash; Tick = tick; Effect = effect;
        }

        internal CommandReceipt(string actorId, long sequence, string payloadHash, long tick, CommandEffect effect)
        {
            if (!WordboundIdentity.IsId(actorId) || sequence < 1 || sequence == long.MaxValue ||
                !WordboundIdentity.IsHash(payloadHash) || tick < 0 || effect == null)
                throw new ArgumentException("Invalid retained command receipt.");
            ActorId = actorId; Sequence = sequence; PayloadHash = payloadHash; Tick = tick; Effect = effect;
        }
    }

    public interface ITransactionalCommandTarget
    {
        object CaptureTransactionState();
        void RestoreTransactionState(object state);
        CommandEffect ApplyValidatedCommand(AuthenticatedCommand command);
    }

    public sealed class CommandJournalSnapshot
    {
        public string IdentityHash { get; }
        public long Epoch { get; }
        public IReadOnlyDictionary<string, long> Frontiers { get; }
        public IReadOnlyList<CommandReceipt> Receipts { get; }
        internal CommandJournalSnapshot(string identityHash, long epoch,
            IDictionary<string, long> frontiers, IEnumerable<CommandReceipt> receipts)
        {
            IdentityHash = identityHash; Epoch = epoch;
            Frontiers = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(
                new Dictionary<string, long>(frontiers, StringComparer.Ordinal));
            Receipts = Array.AsReadOnly(receipts.OrderBy(r => r.ActorId, StringComparer.Ordinal)
                .ThenBy(r => r.Sequence).ToArray());
        }
    }

    public sealed class CommandJournal
    {
        public const int RetainedReceiptsPerPlayer = 128;
        public const int MaximumBatch = 256;
        readonly string identityHash;
        readonly Dictionary<string, long> frontiers;
        readonly Dictionary<string, SortedDictionary<long, CommandReceipt>> receipts;
        public long Epoch { get; private set; }

        public CommandJournal(string matchIdentityHash, IEnumerable<string> playerIds, long epoch = 0)
        {
            var ids = playerIds?.ToArray() ?? Array.Empty<string>();
            if (!WordboundIdentity.IsHash(matchIdentityHash) || epoch < 0 || epoch == long.MaxValue ||
                ids.Length < 1 || ids.Length > 8 || ids.Any(id => !WordboundIdentity.IsId(id)) ||
                ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("Invalid journal identity, roster or epoch.");
            identityHash = matchIdentityHash; Epoch = epoch;
            frontiers = ids.ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
            receipts = ids.ToDictionary(id => id, _ => new SortedDictionary<long, CommandReceipt>(), StringComparer.Ordinal);
        }

        public IReadOnlyList<CommandReceipt> ExecuteBatch(long tick, IEnumerable<AuthenticatedCommand> requests,
            ITransactionalCommandTarget target)
        {
            if (tick < 0 || target == null) throw new ArgumentException("A valid tick and transactional target are required.");
            var batch = requests?.Take(MaximumBatch + 1).ToArray() ?? Array.Empty<AuthenticatedCommand>();
            if (batch.Length > MaximumBatch || batch.Any(c => c == null))
                throw new ArgumentException("Command batch exceeds bounds.");
            var ordered = batch.OrderBy(c => c.ActorId, StringComparer.Ordinal).ThenBy(c => c.Command.Sequence)
                .ThenBy(c => c.Command.PayloadHash, StringComparer.Ordinal).ToArray();
            var conflicts = ordered.Where(c => c.ActorId != null && frontiers.ContainsKey(c.ActorId) && c.Command.Epoch == Epoch)
                .GroupBy(c => (c.ActorId, c.Command.Sequence))
                .Where(g => g.Select(c => c.Command.PayloadHash).Distinct(StringComparer.Ordinal).Count() > 1)
                .Select(g => g.Key).ToHashSet();
            var before = Capture();
            object transactionState = target.CaptureTransactionState();
            var results = new List<CommandReceipt>(batch.Length);
            try
            {
                foreach (var command in ordered)
                {
                    var packet = command.Command;
                    string refusal = null;
                    if (command.ActorId == null || !frontiers.TryGetValue(command.ActorId, out long frontier))
                        refusal = "UnknownActor";
                    else if (packet.Epoch != Epoch) refusal = "StaleEpoch";
                    else if (conflicts.Contains((command.ActorId, packet.Sequence))) refusal = "PayloadConflict";
                    else if (receipts[command.ActorId].TryGetValue(packet.Sequence, out var previous))
                    {
                        results.Add(previous.PayloadHash == packet.PayloadHash ? previous :
                            Refused(command, tick, "PayloadConflict"));
                        continue;
                    }
                    else if (packet.Sequence <= frontier) refusal = "RetiredSequence";
                    else if (packet.Sequence != frontier + 1) refusal = "SequenceGap";
                    if (refusal != null) { results.Add(Refused(command, tick, refusal)); continue; }
                    var effect = target.ApplyValidatedCommand(command);
                    if (effect == null) throw new InvalidOperationException("Command target returned no result.");
                    var receipt = new CommandReceipt(command, tick, effect);
                    frontiers[command.ActorId] = packet.Sequence;
                    var history = receipts[command.ActorId];
                    history.Add(packet.Sequence, receipt);
                    while (history.Count > RetainedReceiptsPerPlayer) history.Remove(history.First().Key);
                    results.Add(receipt);
                }
            }
            catch
            {
                try { target.RestoreTransactionState(transactionState); }
                finally { Restore(before); }
                throw;
            }
            return results.AsReadOnly();
        }

        public void AdvanceEpoch(long nextEpoch)
        {
            if (nextEpoch != Epoch + 1 || nextEpoch == long.MaxValue) throw new ArgumentOutOfRangeException(nameof(nextEpoch));
            Epoch = nextEpoch;
        }

        public CommandJournalSnapshot Capture() => new CommandJournalSnapshot(identityHash, Epoch,
            frontiers, receipts.Values.SelectMany(history => history.Values));

        public void Restore(CommandJournalSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IdentityHash != identityHash || snapshot.Epoch < 0 ||
                snapshot.Epoch == long.MaxValue || snapshot.Frontiers.Count != frontiers.Count ||
                snapshot.Frontiers.Any(pair => !frontiers.ContainsKey(pair.Key) || pair.Value < 0 || pair.Value == long.MaxValue) ||
                snapshot.Receipts.Any(r => r == null || !frontiers.ContainsKey(r.ActorId) ||
                    !WordboundIdentity.IsHash(r.PayloadHash) || r.Tick < 0 || r.Effect == null))
                throw new ArgumentException("Journal snapshot identity or state is invalid.");
            var histories = frontiers.Keys.ToDictionary(id => id,
                id => snapshot.Receipts.Where(r => r.ActorId == id).OrderBy(r => r.Sequence).ToArray(), StringComparer.Ordinal);
            foreach (var pair in snapshot.Frontiers)
            {
                var history = histories[pair.Key];
                int expected = (int)Math.Min(pair.Value, RetainedReceiptsPerPlayer);
                if (history.Length != expected || history.Where((r, i) => r.Sequence != pair.Value - expected + 1 + i).Any())
                    throw new ArgumentException("Journal receipts do not match the processed frontier.");
            }
            foreach (var pair in snapshot.Frontiers)
            {
                frontiers[pair.Key] = pair.Value;
                receipts[pair.Key].Clear();
                foreach (var receipt in histories[pair.Key]) receipts[pair.Key].Add(receipt.Sequence, receipt);
            }
            Epoch = snapshot.Epoch;
        }

        static CommandReceipt Refused(AuthenticatedCommand command, long tick, string reason) =>
            new CommandReceipt(command, tick, new CommandEffect(reason));
    }
}
