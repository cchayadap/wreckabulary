using UnityEngine;

namespace Wreckabulary
{
    /// <summary>One cached native display follows the showroom without changing authored maps.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class LobbyCollectionDecor : MonoBehaviour
    {
        public const string ResourcePath = "Collections/WinterDecor";
        LobbyStage stage;
        GameObject instance;
        Transform wreathDisplay;
        Transform[] props;
        Vector3[] positions, scales;
        bool attemptedLoad;
        public GameObject Instance => instance;
        public bool Visible => instance && instance.activeSelf;

        void Awake() => stage = GetComponent<LobbyStage>();
        void OnEnable() => LobbyThemes.Changed += Refresh;
        void OnDisable()
        {
            LobbyThemes.Changed -= Refresh;
            if (instance) instance.SetActive(false);
        }

        void LateUpdate() => Refresh();

        public void Refresh()
        {
            bool show = stage && stage.IsPresenting && stage.ArtworkShown && LobbyThemes.Current.Id == "winter";
            if (show && !instance && !attemptedLoad)
            {
                attemptedLoad = true;
                var prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab)
                {
                    instance = Instantiate(prefab, transform, false);
                    instance.name = "Winter collection display";
                    wreathDisplay = instance.transform.Find("Wreath display");
                    props = new[] { instance.transform.Find("Winter tree"), instance.transform.Find("Winter gifts"), wreathDisplay };
                    positions = new Vector3[props.Length]; scales = new Vector3[props.Length];
                    for (int i = 0; i < props.Length; i++)
                        if (props[i]) { positions[i] = props[i].localPosition; scales[i] = props[i].localScale; }
                }
            }
            if (!instance) return;
            if (instance.activeSelf != show) instance.SetActive(show);
            if (!show || !stage.Camera) return;
            var away = stage.Camera.transform.forward;
            away.y = 0f;
            instance.transform.SetPositionAndRotation(stage.Spot,
                away.sqrMagnitude > .0001f ? Quaternion.LookRotation(away, Vector3.up) : Quaternion.identity);
            // The side-page viewport reserves the right side for cards and the native item preview.
            bool centered = stage.Camera.WorldToViewportPoint(stage.Spot).x > .4f;
            if (wreathDisplay && wreathDisplay.gameObject.activeSelf != centered) wreathDisplay.gameObject.SetActive(centered);
            bool group = stage.PresentedPartySize > 1;
            for (int i = 0; i < props.Length; i++)
            {
                if (!props[i]) continue;
                var position = positions[i];
                if (group)
                {
                    position.x = i == 2 ? 2.30f : i == 1 ? -1.82f : -2.18f;
                    position.z += .65f;
                }
                props[i].localPosition = position;
                props[i].localScale = scales[i] * (group ? .75f : 1f);
            }
        }
    }
}
