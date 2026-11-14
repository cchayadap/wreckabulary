// Game Studio Kit (MIT). Marks a placed unit for the floating and sunk prop audits: the object and everything under it
// is judged as one thing (the union of its renderers), so a table top between its legs or a lamp head above its pole is
// not reported on its own. Without a marker the audit uses the prefab instance root (editor) or the largest ancestor
// that is still prop-sized (players).
using UnityEngine;

namespace GameStudioKit.Qa.Probes
{
    [AddComponentMenu("Game Studio Kit/Audit Unit")]
    [DisallowMultipleComponent]
    public sealed class AuditUnit : MonoBehaviour
    {
    }
}
