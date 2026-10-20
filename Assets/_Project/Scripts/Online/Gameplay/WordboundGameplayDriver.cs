using Fusion;
using UnityEngine;

namespace Wreckabulary.Online
{
    [DefaultExecutionOrder(900)]
    public sealed class WordboundGameplayDriver : SimulationBehaviour
    {
        public WordboundGameplayController Controller { private get; set; }
        public override void FixedUpdateNetwork() => Controller?.PrepareHostPhysics(Runner);
    }
}
