using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public class UIStateControllerTests
    {
        GameObject root;
        UIStateController controller;
        CanvasGroup menu, gameplay, pause;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("UI state test");
            controller = root.AddComponent<UIStateController>();
            menu = View("Menu"); gameplay = View("Gameplay"); pause = View("Pause");
            controller.Register(UIState.MainMenu, menu);
            controller.Register(UIState.GameplayHUD, gameplay);
            controller.Register(UIState.PauseMenu, pause);
        }

        CanvasGroup View(string name)
        {
            var child = new GameObject(name, typeof(CanvasGroup));
            child.transform.SetParent(root.transform);
            return child.GetComponent<CanvasGroup>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
        }

        [Test]
        public void OnlyRequestedViewReceivesInputAndGameObjectsRemainActive()
        {
            controller.FadeDuration = 0f;
            controller.TransitionToState(UIState.MainMenu);
            Assert.AreEqual(1f, menu.alpha);
            Assert.IsTrue(menu.interactable && menu.blocksRaycasts);
            Assert.AreEqual(0f, gameplay.alpha);
            Assert.IsFalse(gameplay.interactable || gameplay.blocksRaycasts);
            Assert.AreEqual(0f, pause.alpha);
            Assert.IsTrue(menu.gameObject.activeSelf && gameplay.gameObject.activeSelf && pause.gameObject.activeSelf);
        }

        [Test]
        public void ActionsAndUnityEventsNotifyOncePerDistinctState()
        {
            controller.FadeDuration = 0f;
            int actions = 0, events = 0, completions = 0;
            controller.StateChanged += state => { Assert.AreEqual(UIState.PauseMenu, state); actions++; };
            controller.OnStateChanged.AddListener(state => events++);
            controller.TransitionCompleted += state => completions++;
            controller.TransitionToState(UIState.PauseMenu);
            controller.TransitionToState(UIState.PauseMenu);
            Assert.AreEqual(1, actions);
            Assert.AreEqual(1, events);
            Assert.AreEqual(1, completions);
        }

        [UnityTest]
        public IEnumerator PausedTimeDoesNotPreventFadeAndInputIsBlockedUntilCompletion()
        {
            controller.FadeDuration = 0.08f;
            Time.timeScale = 0f;
            controller.TransitionToState(UIState.PauseMenu);
            Assert.IsTrue(controller.IsTransitioning);
            Assert.IsFalse(gameplay.blocksRaycasts || pause.blocksRaycasts);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsFalse(controller.IsTransitioning);
            Assert.AreEqual(1f, pause.alpha);
            Assert.IsTrue(pause.interactable && pause.blocksRaycasts);
            Assert.AreEqual(0f, gameplay.alpha);
        }

        [UnityTest]
        public IEnumerator RapidTransitionKeepsCurrentOpacityAndFinishesAtLatestState()
        {
            controller.FadeDuration = 0.2f;
            controller.TransitionToState(UIState.PauseMenu);
            yield return null;
            float partial = pause.alpha;
            controller.TransitionToState(UIState.MainMenu);
            Assert.AreEqual(partial, pause.alpha, 0.0001f, "An interrupted fade must not snap opacity.");
            yield return new WaitForSecondsRealtime(0.28f);
            Assert.AreEqual(1f, menu.alpha);
            Assert.AreEqual(0f, pause.alpha);
            Assert.IsTrue(menu.blocksRaycasts);
        }
    }
}
