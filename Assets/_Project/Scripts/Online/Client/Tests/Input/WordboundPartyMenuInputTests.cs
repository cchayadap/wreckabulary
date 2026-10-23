using System;
using System.Collections;
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
using UnityEngine.TestTools;
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
        WordboundNativeInputScope input;
        OnlineSettings settings;
        bool ready;
        string channel;
        int revision, closed;

        [SetUp]
        public void SetUp()
        {
            previousPad = Gamepad.current; previousTyping = DesktopBinding.Typing;
            try
            {
                settings = null; ready = false; channel = null; revision = closed = 0;
                input = new WordboundNativeInputScope();
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
            catch (Exception setupError)
            {
                try { TearDown(); }
                catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (window) window.Close();
                if (menu) Get<WordboundUiStyles>(menu, "ui").Dispose();
                if (root) Object.DestroyImmediate(root);
                coordinator?.Dispose(); rsa?.Dispose();
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
            }
            finally
            {
                try { input?.Dispose(); }
                finally { input = null; DesktopBinding.Typing = previousTyping; }
            }
        }

        [UnityTest]
        public IEnumerator RealPadButtonsConnectSelectCreateReadyAndQuickPlayOnlyAfterRepaint()
        {
            yield return Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
            yield return Activate("mode:4"); Assert.AreEqual(4, Get<int>(menu, "mode"));
            yield return Activate("region:1"); Assert.AreEqual(1, Get<int>(menu, "region"));
            yield return Activate("fill");
            yield return Activate("create-party"); Assert.IsNotNull(coordinator.Party);
            Assert.AreEqual("Freestyle", coordinator.Party.Settings.Mode); Assert.AreEqual("eu", coordinator.Party.Settings.Region);
            yield return Activate("ready"); Assert.IsTrue(coordinator.Party.Members.Single().Ready);
            yield return Activate("quick-play"); Assert.AreEqual("QuickPlay", coordinator.Party.Channel);
            Assert.IsTrue(http.Requests.Any(r => r.Path == "/v1/guests"));
            Assert.IsTrue(http.Requests.Any(r => r.Path.EndsWith("/queue") && OnlineJson.Parse(r.JsonBody)["channel"].String() == "QuickPlay"));
            Assert.Greater(window.Layouts, 0); Assert.Greater(window.Repaints, 0); Assert.IsNull(window.Failure);
        }

        [UnityTest]
        public IEnumerator RetryReusesPendingCreateAndPrivateQueueThenBackLeavesParty()
        {
            yield return Activate("connect"); yield return Activate("mode:4");
            http.FailNext = true; yield return Activate("create-party");
            Assert.IsTrue(coordinator.RetryRequired);
            var first = http.Requests.Last();
            yield return Activate("retry");
            Assert.IsFalse(coordinator.RetryRequired); Assert.IsNotNull(coordinator.Party);
            Assert.AreEqual(first.IdempotencyKey, http.Requests.Last().IdempotencyKey);
            Assert.AreEqual(first.JsonBody, http.Requests.Last().JsonBody);
            yield return Activate("ready"); yield return Activate("create-private");
            Assert.AreEqual("PrivateCreate", coordinator.Party.Channel);
            Sample(default); Sample(default); Sample(new GamepadState().WithButton(GamepadButton.East));
            Assert.IsNull(coordinator.Party); Assert.IsTrue(http.Requests.Last().Path.EndsWith("/leave"));
            Sample(default); Sample(new GamepadState().WithButton(GamepadButton.East));
            Assert.AreEqual(1, closed);
        }

        [Test]
        public void ActualPartyDrawUsesAssignedThemeAndRestoresTheBorrowedSkin()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            try
            {
                theme.BodySize = 17; theme.RowHeight = 40; theme.ModuleHeight = 64;
                menu.Theme = theme;
                Draw(EventType.Layout); Draw(EventType.Repaint);
                var styles = Get<WordboundUiStyles>(menu, "ui");
                var owned = Get<GUISkin>(styles, "skin");
                var textures = new List<Texture2D>(Get<List<Texture2D>>(styles, "textures"));
                Assert.AreEqual(17, owned.label.fontSize);
                Assert.GreaterOrEqual(styles.RowHeight, 40);
                Assert.IsTrue(window.SkinRestored);
                Assert.IsEmpty(http.Requests, "Styling must not dispatch a party action.");
                window.Menu = null;
                Invoke(menu, "OnDestroy");
                Assert.IsFalse(owned);
                foreach (var texture in textures) Assert.IsFalse(texture);
                Assert.IsTrue(theme, "The designer's assigned asset is borrowed.");
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [UnityTest]
        public IEnumerator FocusLossDiscardsActualQueuedConnectAndHeldConfirmNeedsNeutral()
        {
            yield return QueueControl("connect");
            Invoke(menu, "ProcessPartyInput", false);
            Sample(new GamepadState().WithButton(GamepadButton.South));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.IsEmpty(http.Requests); Assert.IsFalse(coordinator.Authenticated);
            Sample(default); Sample(default);
            yield return Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
        }

        [TestCase(700)]
        [TestCase(420)]
        public void DisconnectedPartyCardFitsActualMeasuredContentWithoutChangingItDuringRepaint(int width)
        {
            window.position = new Rect(100, 100, width, 800);
            for (int i = 0; i < 3; i++) { Draw(EventType.Layout); Draw(EventType.Repaint); }
            Rect before = Get<Rect>(menu, "partyRect");
            float measured = Get<float>(menu, "guestHeight");
            Assert.AreEqual(width, window.position.width, 1, "The native window must use the requested test width.");
            var styles = Get<WordboundUiStyles>(menu, "ui");
            Assert.AreEqual(styles.MenuRect(window.position.width, window.position.height).width, before.width, 1,
                "The actual drawn card must use this window's viewport, not a wider shared Screen size.");
            Assert.Greater(measured, 0);
            Assert.IsTrue(Get<bool>(menu, "compactPartyLayout"));
            Assert.GreaterOrEqual(before.height, measured, "The actual drawn content must fit without hiding controls.");
            Assert.Less(before.height, window.position.height - 48, "The disconnected card should release its unused vertical space.");
            window.BeforeRepaint = () => {
                Rect layout = Get<Rect>(menu, "partyRect");
                Set(menu, "guestHeight", measured + 20);
                Assert.AreEqual(before, layout);
            };
            Draw(EventType.Repaint);
            Assert.AreEqual(before, Get<Rect>(menu, "partyRect"), "Measured height is applied only during Layout.");
            Assert.IsEmpty(http.Requests);
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
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); WordboundNativeInputScope.Pump();
                Assert.IsTrue(mouse.leftButton.isPressed, "The pointer state must reach the input system before device handoff.");
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

        [UnityTest]
        public IEnumerator NewInputBlockerDiscardsActualQueuedPadConnectWithTyping() => BlockQueuedConnect(false);

        [UnityTest]
        public IEnumerator NewInputBlockerDiscardsActualQueuedPadConnectWithGameplayLease() => BlockQueuedConnect(true);

        IEnumerator BlockQueuedConnect(bool gameplayLease)
        {
            IDisposable lease = null;
            try
            {
                yield return QueueControl("connect");
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
            if (gameplayLease) Assert.IsTrue(KeyBindings.GameplayBlocked, "Lease release intentionally suppresses input through the next engine frame.");
            Sample(new GamepadState().WithButton(GamepadButton.South));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.IsEmpty(http.Requests);
            Sample(default); Sample(default);
            yield return Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
        }

        IEnumerator Activate(string id)
        {
            int count = http.Requests.Count;
            yield return QueueControl(id);
            Assert.AreEqual(count, http.Requests.Count, "The real button handler must not execute during Repaint.");
            Sample(default);
            Assert.IsNull(window.Failure);
        }

        IEnumerator QueueControl(string id)
        {
            double deadline = EditorApplication.timeSinceStartup + 5;
            do
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
                Assert.Less(EditorApplication.timeSinceStartup, deadline, "Input capture did not release: " + InputState());
            } while (KeyBindings.GameplayBlocked);
            if (Get<WordboundInputBinding>(menu, "partyInput")?.ActiveGamepad != pad)
                Sample(new GamepadState { leftStick = new Vector2(.8f, 0) });
            Sample(default); Sample(default);
            Get<WordboundMenuFocus>(menu, "partyFocus").Reset(id);
            Draw(EventType.Layout);
            Assert.AreEqual(id, Get<WordboundMenuFocus>(menu, "partyFocus").Selected, "The intended enabled control must exist.");
            Draw(EventType.Repaint);
            Assert.AreEqual("party:" + id, window.FocusedControl, "The selected real control must receive rendered focus before confirmation: " + InputState());
            Assert.IsFalse(Get<bool>(menu, "partyTyping"), "A rendered button must not remain in text-entry mode.");
            Sample(default); Sample(default);
            Sample(new GamepadState().WithButton(GamepadButton.South));
            Draw(EventType.Repaint);
            Assert.IsNotNull(Get<Action>(menu, "pendingPartyAction"), "Confirmation must use the real drawn control: " + InputState());
        }

        string InputState() => "frame=" + Time.frameCount + ", quietUntil=" +
            typeof(KeyBindings).GetField("quietUntil", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) +
            ", blocked=" + KeyBindings.GameplayBlocked +
            ", typing=" + DesktopBinding.Typing + ", menuTyping=" + Get<bool>(menu, "partyTyping") +
            ", pad=" + Get<bool>(menu, "partyUsesPad") + ", selected=" + Get<WordboundMenuFocus>(menu, "partyFocus").Selected +
            ", focused=" + window.FocusedControl + ", layouts=" + window.Layouts + ", repaints=" + window.Repaints;

        void Sample(GamepadState state)
        {
            WordboundNativeInputScope.Sample(pad, state);
            Invoke(menu, "ProcessPartyInput", true);
        }
        void Draw(EventType type)
        {
            int before = type == EventType.Layout ? window.Layouts : window.Repaints;
            window.SendEvent(new Event { type = type });
            Assert.IsNull(window.Failure);
            Assert.Greater(type == EventType.Layout ? window.Layouts : window.Repaints, before,
                "EditorWindow did not process the requested " + type + ": " + InputState());
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
            public string FocusedControl;
            public bool SkinRestored = true;
            void OnGUI()
            {
                if (!Menu) return;
                bool enabled = GUI.enabled;
                var borrowedSkin = GUI.skin;
                try
                {
                    if (Event.current.type == EventType.Layout) Layouts++;
                    if (Event.current.type == EventType.Repaint) Repaints++;
                    Invoke(Menu, "BeginPartyGui", true);
                    if (Event.current.type == EventType.Repaint && BeforeRepaint != null)
                    { var action = BeforeRepaint; BeforeRepaint = null; action(); }
                    try { Invoke(Menu, "DrawPartyGui", position.size); }
                    finally { Invoke(Menu, "EndPartyGui"); FocusedControl = GUI.GetNameOfFocusedControl(); }
                }
                catch (Exception error) { Failure = error; }
                finally { SkinRestored &= ReferenceEquals(borrowedSkin, GUI.skin); GUI.enabled = enabled; }
            }
        }
    }
}
