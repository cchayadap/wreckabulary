using UnityEngine;

namespace Wreckabulary.Audio
{
    [DisallowMultipleComponent]
    public sealed class ImpactAudioScope : MonoBehaviour
    {
        void OnEnable() => ImpactAudioBridge.Enter();
        void OnDisable() => ImpactAudioBridge.Exit();
    }
}
