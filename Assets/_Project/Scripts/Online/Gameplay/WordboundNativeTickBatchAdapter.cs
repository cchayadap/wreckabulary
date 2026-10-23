#if UNITY_EDITOR
using System;
using Fusion;
using UnityEngine;

namespace Wreckabulary.Online
{
    [AddComponentMenu("")]
    public sealed class WordboundNativeTickBatchAdapter : SimulationBehaviour, IBeforeAllTicks, IAfterAllTicks
    {
        public Action<bool, int, bool> BeforeTicks;
        public Action<bool, int> AfterTicks;
        public Action<int> Updated;
        public Action Destroyed;

        public void BeforeAllTicks(bool resimulation, int tickCount) => BeforeTicks?.Invoke(resimulation, tickCount, Runner.IsServer);
        public void AfterAllTicks(bool resimulation, int tickCount) => AfterTicks?.Invoke(resimulation, tickCount);
        void Update()
        {
            if (Runner != null && Runner.IsRunning) Updated?.Invoke(Runner.TicksExecuted);
        }
        void OnDestroy()
        {
            Destroyed?.Invoke();
            BeforeTicks = null; AfterTicks = null; Updated = null; Destroyed = null;
        }
    }
}
#endif
