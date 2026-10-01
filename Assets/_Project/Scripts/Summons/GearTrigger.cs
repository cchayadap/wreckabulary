using UnityEngine;

namespace Wreckabulary
{
    public sealed class GearTrigger : MonoBehaviour
    {
        public DeployedGear Tool;
        void OnTriggerStay(Collider other) { if (Tool) Tool.Affect(other); }
    }
}
