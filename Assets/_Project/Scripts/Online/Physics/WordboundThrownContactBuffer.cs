using System;
using System.Collections.Generic;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundThrownContactBuffer
    {
        public const int MaximumContacts = 256;
        readonly Dictionary<string, HostThrownContact> contacts = new Dictionary<string, HostThrownContact>(StringComparer.Ordinal);
        long epoch = -1, tick = -1;
        bool complete;

        public void Begin(long currentEpoch, long nextTick)
        {
            if (currentEpoch < 0 || nextTick < 1) throw new ArgumentException("Invalid contact step.");
            epoch = currentEpoch; tick = nextTick; complete = false; contacts.Clear();
        }
        public void Add(long currentEpoch, HostThrownContact contact)
        {
            if (complete || contact == null || currentEpoch != epoch || contact.Tick != tick)
                throw new ArgumentException("Collision does not belong to the collecting host step.");
            string key = contact.ItemBodyId + "/" + contact.TargetBodyId;
            if (contacts.TryGetValue(key, out var previous))
            {
                if (previous.LaunchRevision != contact.LaunchRevision) throw new ArgumentException("One step cannot mix thrown flights.");
                if (previous.RelativeSpeed >= contact.RelativeSpeed) return;
            }
            else if (contacts.Count >= MaximumContacts) throw new InvalidOperationException("Thrown contact step exceeds its bound.");
            contacts[key] = contact;
        }
        public void Complete(long currentEpoch, long completedTick)
        {
            if (complete || currentEpoch != epoch || completedTick != tick) throw new ArgumentException("Contact completion changed host step.");
            complete = true;
        }
        public IReadOnlyList<HostThrownContact> Read(long currentEpoch, long completedTick)
        {
            if (!complete || currentEpoch != epoch || completedTick != tick) throw new InvalidOperationException("Contacts are not complete for this host step.");
            return Array.AsReadOnly(contacts.Values.OrderBy(c => c.ItemBodyId, StringComparer.Ordinal)
                .ThenBy(c => c.TargetBodyId, StringComparer.Ordinal).ToArray());
        }
    }
}
