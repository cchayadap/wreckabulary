using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    [RequireComponent(typeof(Graphic))]
    public sealed class LobbySkew : BaseMeshEffect
    {
        public float Degrees = 10f;

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            float middle = graphic.rectTransform.rect.center.y;
            float slope = Mathf.Tan(Degrees * Mathf.Deg2Rad);
            var vertex = new UIVertex();
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.position.x += (vertex.position.y - middle) * slope;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
