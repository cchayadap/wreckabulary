using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundPartyMenuInputTests
    {
        const string Player = "10000000-0000-4000-8000-000000000001", PartyId = "20000000-0000-4000-8000-000000000001";
        const string Seed = "0123456789abcdef", Code = "1234567890abcdef";
        GameObject root;
        WordboundPartyMenu menu;
        WordboundPartyCoordinator coordinator;
        MenuWindow window;
        FakeHttp http;
        RSA rsa;
        Gamepad pad, previousPad;
        bool previousTyping;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode routing;
        OnlineSettings settings;
        bool ready;
        string channel;
        int revision, closed;

        [SetUp]
        public void SetUp()
        {
            previousPad = Gamepad.current; previousTyping = DesktopBinding.Typing;
            background = InputSystem.settings.backgroundBehavior; routing = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            DesktopBinding.Typing = false;
            pad = InputSystem.AddDevice<Gamepad>();
            string items = File.ReadAllText("Assets/_Project/Data/Config/items.json");
            string rules = File.ReadAllText("Assets/_Project/Data/Config/rules.json");
            var content = new WordboundClientContent(items, rules, "Freestyle", "Solo", Seed);
            var catalogue = WordboundPublicCourseCatalog.FromTestedContent("pad-tests", new[] { content });
            rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            var key = rsa.ExportParameters(false);
            var verifier = new OnlineAdmissionVerifier("wreckabulary-wordbound", OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["keys"] = new[] { new Dictionary<string, object> { ["kty"] = "RSA", ["kid"] = "pad-test", ["use"] = "sig",
                    ["alg"] = "RS256", ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } }
            })));
            http = new FakeHttp { Handler = Respond };
            coordinator = new WordboundPartyCoordinator(new OnlineCoordinatorClient(http), verifier, items, rules,
                "pad-tests", new WordboundPublicCourseRegistry(catalogue.Json), () => 1000000);
            root = new GameObject("Party input native test"); menu = root.AddComponent<WordboundPartyMenu>(); menu.enabled = false;
            Set(menu, "coordinator", coordinator); Set(menu, "publicCourses", catalogue);
            Set(menu, "itemSource", items); Set(menu, "ruleSource", rules);
            menu.CloseRequested += () => closed++;
            window = ScriptableObject.CreateInstance<MenuWindow>(); window.Menu = menu;
            window.position = new Rect(100, 100, 700, 900); window.ShowUtility();
            Sample(new GamepadState { leftStick = new Vector2(.8f, 0) });
            Sample(default); Sample(default);
        }

        [TearDown]
        public void TearDown()
        {
            if (window) window.Close();
            if (root) Object.DestroyImmediate(root);
            coordinator?.Dispose(); rsa?.Dispose();
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
            DesktopBinding.Typing = previousTyping;
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = routing;
        }

        [Test]
        public void RealPadButtonsConnectSelectCreateReadyAndQuickPlayOnlyAfterRepaint()
        {
            Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
            Activate("mode:4"); Assert.AreEqual(4, Get<int>(menu, "mode"));
            Activate("region:1"); Assert.AreEqual(1, Get<int>(menu, "region"));
            Activate("fill");
            Activate("create-party"); Assert.IsNotNull(coordinator.Party);
            Assert.AreEqual("Freestyle", coordinator.Party.Settings.Mode); Assert.AreEqual("eu", coordinator.Party.Settings.Region);
            Activate("ready"); Assert.IsTrue(coordinator.Party.Members.Single().Ready);
            Activate("quick-play"); Assert.AreEqual("QuickPlay", coordinator.Party.Channel);
            Assert.IsTrue(http.Requests.Any(r => r.Path == "/v1/guests"));
            Assert.IsTrue(http.Requests.Any(r => r.Path.EndsWith("/queue") && OnlineJson.Parse(r.JsonBody)["channel"].String() == "QuickPlay"));
            Assert.Greater(window.Layouts, 0); Assert.Greater(window.Repaints, 0); Assert.IsNull(window.Failure);
        }

        [Test]
        public void RetryReusesPendingCreateAndPrivateQueueThenBackLeavesParty()
        {
            Activate("connect"); Activate("mode:4");
            http.FailNext = true; Activate("create-party");
            Assert.IsTrue(coordinator.RetryRequired);
            var first = http.Requests.Last();
            Activate("retry");
            Assert.IsFalse(coordinator.RetryRequired); Assert.IsNotNull(coordinator.Party);
            Assert.AreEqual(first.IdempotencyKey, http.Requests.Last().IdempotencyKey);
            Assert.AreEqual(first.JsonBody, http.Requests.Last().JsonBody);
            Activate("ready"); Activate("create-private");
            Assert.AreEqual("PrivateCreate", coordinator.Party.Channel);
            Sample(default); Sample(default); Sample(new GamepadState().WithButton(GamepadButton.East));
            Assert.IsNull(coordinator.Party); Assert.IsTrue(http.Requests.Last().Path.EndsWith("/leave"));
            Sample(default); Sample(new GamepadState().WithButton(GamepadButton.East));
            Assert.AreEqual(1, closed);
        }

        [Test]
        public void FocusLossDiscardsActualQueuedConnectAndHeldConfirmNeedsNeutral()
        {
            QueueControl("connect");
            Invoke(menu, "ProcessPartyInput", false);
            Sample(new GamepadState().WithButton(GamepadButton.South));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.IsEmpty(http.Requests); Assert.IsFalse(coordinator.Authenticated);
            Sample(default); Sample(default);
            Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
        }

        [Test]
        public void ExplicitMouseActionSurvivesChangingFromPadToDesktop()
        {
            Mouse previousMouse = Mouse.current, mouse = null;
            try
            {
                mouse = InputSystem.AddDevice<Mouse>();
                int actions = 0;
                window.BeforeRepaint = () => Invoke(menu, "QueuePartyAction", (Action)(() => actions++), false);
                Draw(EventType.Layout); Draw(EventType.Repaint);
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); InputSystem.Update();
                Invoke(menu, "ProcessPartyInput", true);
                Assert.AreEqual(1, actions);
                Assert.IsFalse(Get<bool>(menu, "partyUsesPad"));
                Invoke(menu, "ProcessPartyInput", true);
                Assert.AreEqual(1, actions);
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NewInputBlockerDiscardsActualQueuedPadConnect(bool gameplayLease)
        {
            IDisposable lease = null;
            try
            {
                QueueControl("connect");
                if (gameplayLease) lease = KeyBindings.BlockGameplay();
                else DesktopBinding.Typing = true;
                Invoke(menu, "ProcessPartyInput", true);
                Assert.IsNull(Get<Action>(menu, "pendingPartyAction"));
                Assert.IsEmpty(http.Requests);
                Assert.IsFalse(coordinator.Authenticated);
            }
            finally
            {
                lease?.Dispose();
                DesktopBinding.Typing = false;
            }
            Sample(new GamepadState().WithButton(GamepadButton.South));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.IsEmpty(http.Requests);
            Sample(default); Sample(default);
            Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
        }

        void Activate(string id)
        {
            int count = http.Requests.Count;
            QueueControl(id);
            Assert.AreEqual(count, http.Requests.Count, "The real button handler must not execute during Repaint.");
            Sample(default);
            Assert.IsNull(window.Failure);
        }

        void QueueControl(string id)
        {
            Sample(default); Sample(default);
            Get<WordboundMenuFocus>(menu, "partyFocus").Reset(id);
            Draw(EventType.Layout);
            Assert.AreEqual(id, Get<WordboundMenuFocus>(menu, "partyFocus").Selected, "The intended enabled control must exist.");
            Sample(new GamepadState().WithButton(GamepadButton.South));
            Draw(EventType.Repaint);
            Assert.IsNotNull(Get<Action>(menu, "pendingPartyAction"), "Confirmation must use the real drawn control.");
        }

        void Sample(GamepadState state)
        {
            InputSystem.QueueStateEvent(pad, state); InputSystem.Update();
            Invoke(menu, "ProcessPartyInput", true);
        }
        void Draw(EventType type)
        {
            window.SendEvent(new Event { type = type });
            Assert.IsNull(window.Failure);
        }
        OnlineResponse Respond(OnlineRequest request)
        {
            if (request.Path == "/v1/guests") return Json(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "test.token", ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900
            });
            var body = string.IsNullOrEmpty(request.JsonBody) ? null : OnlineJson.Parse(request.JsonBody);
            if (request.Path == "/v1/parties") settings = OnlineSettings.Read(body["settings"]);
            if (request.Path.EndsWith("/ready")) ready = body["ready"].Bool();
            if (request.Path.EndsWith("/queue")) channel = body["channel"].String();
            if (request.Path.EndsWith("/leave")) return Json(new Dictionary<string, object>());
            var response = new Dictionary<string, object> {
                ["id"] = PartyId, ["revision"] = (++revision).ToString(), ["leaderId"] = Player, ["invite"] = Code,
                ["members"] = new[] { new Dictionary<string, object> { ["playerId"] = Player, ["ready"] = ready } },
                ["settings"] = settings.ToValue(), ["state"] = channel == null ? "OPEN" : "QUEUED"
            };
            if (channel != null) response["activeAttempt"] = new Dictionary<string, object> { ["channel"] = channel };
            return Json(response);
        }
        static OnlineResponse Json(object value) => new OnlineResponse(200, OnlineJson.Encode(value));
        sealed class FakeHttp : IOnlineHttpTransport
        {
            public readonly List<OnlineRequest> Requests = new List<OnlineRequest>();
            public Func<OnlineRequest, OnlineResponse> Handler;
            public bool FailNext;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellation)
            {
                Requests.Add(request);
                if (FailNext) { FailNext = false; return Task.FromException<OnlineResponse>(new IOException("Synthetic lost response")); }
                return Task.FromResult(Handler(request));
            }
        }
        static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Invoke(object target, string name, params object[] args)
        {
            try { target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        sealed class MenuWindow : EditorWindow
        {
            public WordboundPartyMenu Menu;
            public Exception Failure;
            public Action BeforeRepaint;
            public int Layouts, Repaints;
            void OnGUI()
            {
                if (!Menu) return;
                bool enabled = GUI.enabled;
                try
                {
                    if (Event.current.type == EventType.Layout) Layouts++;
                    if (Event.current.type == EventType.Repaint) Repaints++;
                    Invoke(Menu, "BeginPartyGui", true);
                    if (Event.current.type == EventType.Repaint && BeforeRepaint != null)
                    { var action = BeforeRepaint; BeforeRepaint = null; action(); }
                    try { Invoke(Menu, "DrawPartyGui"); }
                    finally { Invoke(Menu, "EndPartyGui"); }
                }
                catch (Exception error) { Failure = error; }
                finally { GUI.enabled = enabled; }
            }
        }
    }
}
