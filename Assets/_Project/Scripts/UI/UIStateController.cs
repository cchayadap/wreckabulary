using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    public enum UIState { MainMenu, GameplayHUD, PauseMenu }

    /// <summary>Fades presentation states independently of scene, session and simulation managers.</summary>
    [DisallowMultipleComponent]
    public sealed class UIStateController : MonoBehaviour
    {
        [Serializable] public sealed class StateEvent : UnityEvent<UIState> { }
        [Serializable]
        public sealed class View
        {
            public UIState state;
            public CanvasGroup group;
            public GameObject firstSelection;
            [NonSerialized] internal float fromAlpha;
        }

        [SerializeField] UIState initialState = UIState.GameplayHUD;
        [SerializeField, Min(0f)] float fadeDuration = 0.18f;
        [SerializeField] View[] views = Array.Empty<View>();
        [SerializeField] StateEvent onStateChanged = new();
        [SerializeField] StateEvent onTransitionCompleted = new();
        public event Action<UIState> StateChanged;
        public event Action<UIState> TransitionCompleted;
        public UIState CurrentState { get; private set; }
        public bool IsTransitioning { get; private set; }
        public float FadeDuration { get => fadeDuration; set => fadeDuration = Mathf.Max(0f, value); }
        public StateEvent OnStateChanged => onStateChanged;
        public StateEvent OnTransitionCompleted => onTransitionCompleted;
        float elapsed;

        void Awake()
        {
            CurrentState = initialState;
            ApplyCompletedState(false);
        }

        /// <summary>Supports views assembled at runtime without coupling them to game managers.</summary>
        public void Register(UIState state, CanvasGroup group, GameObject firstSelection = null)
        {
            if (!group) throw new ArgumentNullException(nameof(group));
            View view = Array.Find(views, entry => entry != null && entry.state == state);
            if (view == null)
            {
                view = new View { state = state };
                Array.Resize(ref views, views.Length + 1);
                views[views.Length - 1] = view;
            }
            else if (view.group && view.group != group)
            {
                view.group.alpha = 0f;
                view.group.interactable = view.group.blocksRaycasts = false;
            }
            view.group = group;
            view.firstSelection = firstSelection;
            group.alpha = state == CurrentState ? 1f : 0f;
            group.interactable = group.blocksRaycasts = state == CurrentState && !IsTransitioning;
            view.fromAlpha = group.alpha;
        }

        /// <summary>Interrupts an unfinished fade from its current opacity. Input resumes after the fade.</summary>
        public void TransitionToState(UIState state)
        {
            if (!Enum.IsDefined(typeof(UIState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            if (CurrentState == state) return;
            CurrentState = state;
            elapsed = 0f;
            IsTransitioning = true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            foreach (var view in views)
            {
                if (view?.group == null) continue;
                view.fromAlpha = view.group.alpha;
                view.group.interactable = view.group.blocksRaycasts = false;
            }
            StateChanged?.Invoke(state);
            onStateChanged.Invoke(state);
            if (fadeDuration <= 0f || !isActiveAndEnabled) ApplyCompletedState(true);
        }

        void Update()
        {
            if (!IsTransitioning) return;
            elapsed += Time.unscaledDeltaTime;
            float t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeDuration);
            float eased = t * t * (3f - 2f * t);
            foreach (var view in views)
                if (view?.group != null)
                    view.group.alpha = Mathf.Lerp(view.fromAlpha, view.state == CurrentState ? 1f : 0f, eased);
            if (t >= 1f) ApplyCompletedState(true);
        }

        void ApplyCompletedState(bool notify)
        {
            IsTransitioning = false;
            GameObject selection = null;
            foreach (var view in views)
            {
                if (view?.group == null) continue;
                bool visible = view.state == CurrentState;
                view.group.alpha = visible ? 1f : 0f;
                view.group.interactable = view.group.blocksRaycasts = visible;
                if (visible) selection = view.firstSelection;
            }
            if (EventSystem.current && selection) EventSystem.current.SetSelectedGameObject(selection);
            if (!notify) return;
            TransitionCompleted?.Invoke(CurrentState);
            onTransitionCompleted.Invoke(CurrentState);
        }

        void OnDisable()
        {
            if (IsTransitioning) ApplyCompletedState(false);
        }
    }
}
