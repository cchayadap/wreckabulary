using UnityEngine;

namespace Wreckabulary
{
    public sealed class FeedbackBurst : MonoBehaviour
    {
        float began;
        Vector3 scale;
        void Start() { began = Time.time; scale = transform.localScale; }
        void Update()
        {
            float age = Time.time - began;
            if (age > 0.4f) { Destroy(gameObject); return; }
            transform.position += Vector3.up * Time.deltaTime * 0.65f;
            transform.localScale = scale * Mathf.Max(0.01f, 1f - age / 0.4f);
            Popup.Billboard(transform);
        }
    }

}
