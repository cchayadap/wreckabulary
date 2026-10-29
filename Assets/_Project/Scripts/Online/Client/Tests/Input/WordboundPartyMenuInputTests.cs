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
        int revision, closed, humans;

        [SetUp]
        public void SetUp()
        {
            previousPad = Gamepad.current; previousTyping = DesktopBinding.Typing;
            try
            {
                settings = null; ready = false; channel = null; revision = closed = 0; humans = 1;
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
        public IEnumerator PublicCoopDefaultsFindHumansWhilePrivateAndFfaChoicesRemainExplicit()
        {
            yield return Activate("mode:4");
            Assert.IsTrue(Get<bool>(menu, "fill"));
            Assert.IsFalse(coordinator.Authenticated); Assert.IsEmpty(http.Requests);
            Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("fill", controls); Assert.IsFalse(controls.Contains("public-map:0"));
            yield return Activate("mode:3"); Draw(EventType.Layout);
            Assert.IsFalse(Get<bool>(menu, "fill")); Assert.IsFalse(controls.Contains("fill"));
            yield return Activate("mode:4"); Assert.IsTrue(Get<bool>(menu, "fill"));
            yield return Activate("create-settings");
            Draw(EventType.Layout);
            Assert.Contains("public-map:0", controls);
            yield return Activate("map-source:1"); Assert.IsFalse(Get<bool>(menu, "fill"));
            Assert.IsEmpty(http.Requests, "Browsing modes and maps cannot create a party or enqueue a match.");
        }

        [UnityTest]
        public IEnumerator PublicCoopPreflightBlocksOneHumanWithoutFillAndReenablesForFillOrTwoHumans()
        {
            yield return Activate("connect"); yield return Activate("mode:4");
            yield return Activate("fill"); yield return Activate("create-party");
            Assert.AreEqual(1, coordinator.Party.Members.Count); Assert.IsFalse(coordinator.Party.Settings.Fill);
            Draw(EventType.Layout); Draw(EventType.Repaint);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.IsFalse(controls.Contains("quick-play"), "The real Find match control must be disabled before a blocked request.");
            Assert.Contains("private-actions", controls, "Solo private play remains reachable.");
            int requests = http.Requests.Count;
            Assert.Throws<InvalidOperationException>(() => coordinator.Queue("QuickPlay"));
            Assert.AreEqual(requests, http.Requests.Count, "The coordinator's independent human guard remains intact.");

            yield return Activate("party-settings"); yield return Activate("fill"); yield return Activate("apply-settings");
            yield return Activate("party-settings"); Draw(EventType.Layout);
            Assert.IsTrue(coordinator.Party.Settings.Fill); Assert.Contains("quick-play", controls);

            yield return Activate("party-settings"); yield return Activate("fill"); yield return Activate("apply-settings");
            yield return Activate("party-settings"); Draw(EventType.Layout);
            Assert.IsFalse(coordinator.Party.Settings.Fill); Assert.IsFalse(controls.Contains("quick-play"));
            humans = 2; coordinator.Poll().GetAwaiter().GetResult();
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.AreEqual(2, coordinator.Party.Members.Count); Assert.IsFalse(coordinator.Party.Settings.Fill);
            Assert.Contains("quick-play", controls, "Exactly two human players must be admitted without changing their Fill setting.");
            yield return Activate("ready"); yield return Activate("quick-play");
            Assert.AreEqual("QuickPlay", coordinator.Party.Channel); Assert.IsFalse(coordinator.Party.Settings.Fill);
        }

        [UnityTest]
        public IEnumerator RealPadButtonsConnectSelectCreateReadyAndQuickPlayOnlyAfterRepaint()
        {
            yield return Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
            yield return Activate("mode:4"); Assert.AreEqual(4, Get<int>(menu, "mode"));
            yield return Activate("create-settings");
            yield return Activate("region:1"); Assert.AreEqual(1, Get<int>(menu, "region"));
            Assert.IsTrue(Get<bool>(menu, "fill"), "A new public co-op choice finds human teammates by default.");
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
            yield return Activate("ready"); yield return Activate("private-actions"); yield return Activate("create-private");
            Assert.AreEqual("PrivateCreate", coordinator.Party.Channel);
            Sample(default); Sample(default); Sample(new GamepadState().WithButton(GamepadButton.East));
            Assert.IsNull(coordinator.Party); Assert.IsTrue(http.Requests.Last().Path.EndsWith("/leave"));
            Sample(default); Sample(new GamepadState().WithButton(GamepadButton.East));
            Assert.AreEqual(1, closed);
        }

        [UnityTest]
        public IEnumerator EntryTabsRegisterOnlyVisibleActionsAndRoomSettingsReturnToCreate()
        {
            yield return Activate("connect"); yield return Activate("mode:4");
            Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("create-party", controls); Assert.IsFalse(controls.Contains("join-party"));
            yield return Activate("entry:1"); Draw(EventType.Layout);
            Assert.Contains("join-party", controls); Assert.Contains("use-room-settings", controls);
            Assert.IsFalse(controls.Contains("create-party")); Assert.IsFalse(controls.Contains("mode:4"));
            Set(menu, "scroll", new Vector2(0, 900)); Set(menu, "revealPartySelection", false);
            Invoke(menu, "PartyBack");
            Assert.AreEqual(0, Get<int>(menu, "entryPage")); Assert.AreEqual(Vector2.zero, Get<Vector2>(menu, "scroll"));
            Assert.IsTrue(Get<bool>(menu, "revealPartySelection"));
            Assert.AreEqual("entry:0", Get<WordboundMenuFocus>(menu, "partyFocus").Selected);
            yield return Activate("entry:1");
            var requested = coordinator.Settings("Freestyle", "Solo", false, "eu");
            Set(menu, "privateCode", WordboundRoomInvite.Encode(Code, requested, Seed));
            int count = http.Requests.Count;
            yield return Activate("use-room-settings");
            Assert.AreEqual(count, http.Requests.Count, "Using invite settings must not create or queue a party.");
            Assert.AreEqual(0, Get<int>(menu, "entryPage"));
            Assert.AreEqual(4, Get<int>(menu, "mode")); Assert.AreEqual(1, Get<int>(menu, "region"));
            Assert.IsTrue(Get<bool>(menu, "customSeedMode")); Assert.AreEqual(Seed, Get<string>(menu, "seed"));
            Draw(EventType.Layout); Assert.Contains("create-party", controls);
            Assert.IsFalse(controls.Contains("join-party"));
        }

        [UnityTest]
        public IEnumerator CompactCreateResolvesPublicDefaultsAndPreservesFoldedPrivateSettings()
        {
            yield return Activate("connect"); Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("create-settings", controls); Assert.IsFalse(controls.Contains("create-party"));
            yield return Activate("mode:4"); Set(menu, "seed", "ffffffffffffffff");
            int requests = http.Requests.Count;
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.AreEqual(Seed, Get<string>(menu, "seed"), "The closed form must resolve the same approved default map.");
            Assert.AreEqual(requests, http.Requests.Count); Assert.Contains("create-party", controls);
            Assert.Contains("mode:4", controls); Assert.IsFalse(controls.Contains("region:1"));
            Assert.Contains("fill", controls); Assert.IsFalse(controls.Contains("public-map:0"));
            yield return Activate("create-settings"); yield return Activate("region:1");
            yield return Activate("map-source:1"); yield return Activate("fill");
            const string privateSeed = "fedcba9876543210";
            Set(menu, "privateSeed", privateSeed); Draw(EventType.Layout); Draw(EventType.Repaint);
            Set(menu, "scroll", new Vector2(0, 900)); Set(menu, "revealPartySelection", false);
            Invoke(menu, "PartyBack");
            Assert.IsFalse(Get<bool>(menu, "showCreateSettings")); Assert.AreEqual(Vector2.zero, Get<Vector2>(menu, "scroll"));
            Assert.IsTrue(Get<bool>(menu, "revealPartySelection"));
            Assert.AreEqual("create-settings", Get<WordboundMenuFocus>(menu, "partyFocus").Selected);
            Draw(EventType.Layout); Assert.IsFalse(controls.Contains("region:1")); Assert.IsFalse(controls.Contains("new-seed"));
            Assert.AreEqual(privateSeed, Get<string>(menu, "seed")); Assert.AreEqual(1, Get<int>(menu, "region"));
            Assert.IsTrue(Get<bool>(menu, "fill"));
            yield return Activate("create-party");
            Assert.AreEqual(privateSeed, coordinator.Seed); Assert.AreEqual("eu", coordinator.Party.Settings.Region);
            Assert.IsTrue(coordinator.Party.Settings.Fill); Assert.AreEqual("Freestyle", coordinator.Party.Settings.Mode);
        }

        [UnityTest]
        public IEnumerator HiddenCreateSettingCannotDispatchAfterItsSectionCloses()
        {
            yield return Activate("connect"); yield return Activate("mode:4"); yield return Activate("create-settings");
            int requests = http.Requests.Count;
            yield return QueueControl("region:1");
            Assert.AreEqual(0, Get<int>(menu, "region"));
            Set(menu, "showCreateSettings", false); Sample(default); Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.AreEqual(0, Get<int>(menu, "region")); Assert.AreEqual(requests, http.Requests.Count);
            Assert.IsNull(Get<Action>(menu, "pendingPartyAction"));
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.IsFalse(controls.Contains("region:1")); Assert.Contains("create-settings", controls);
        }

        [UnityTest]
        public IEnumerator HiddenPrivateActionCannotDispatchAfterItsPanelContextChanges()
        {
            yield return Activate("connect"); yield return Activate("mode:4");
            yield return Activate("create-party"); yield return Activate("ready");
            Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("ready", controls); Assert.Contains("quick-play", controls);
            Assert.IsFalse(controls.Contains("create-private")); Assert.IsFalse(controls.Contains("mode:4"));
            yield return Activate("private-actions");
            int count = http.Requests.Count;
            yield return QueueControl("create-private");
            Assert.AreEqual(count, http.Requests.Count);
            Set(menu, "showPrivateActions", false);
            Sample(default); Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.AreEqual(count, http.Requests.Count);
            Assert.IsFalse(coordinator.Party.HasAttempt);
            Assert.IsNull(Get<Action>(menu, "pendingPartyAction"));
            Assert.IsFalse(controls.Contains("create-private"));
        }

        [UnityTest]
        public IEnumerator BackClosesOptionalPanelsBeforeLeavingIdlePartyAndCancelRemainsVisible()
        {
            yield return Activate("connect"); yield return Activate("mode:4");
            yield return Activate("create-settings"); Assert.IsTrue(Get<bool>(menu, "fill"));
            yield return Activate("create-party");
            string party = coordinator.Party.Id;
            yield return Activate("party-settings");
            Set(menu, "scroll", new Vector2(0, 900)); Set(menu, "revealPartySelection", false);
            Invoke(menu, "PartyBack");
            Assert.IsTrue(Get<bool>(menu, "revealPartySelection"));
            Assert.AreEqual("party-settings", Get<WordboundMenuFocus>(menu, "partyFocus").Selected);
            Assert.IsFalse(Get<bool>(menu, "showPartySettings")); Assert.AreEqual(party, coordinator.Party.Id);
            yield return Activate("private-actions");
            Set(menu, "scroll", new Vector2(0, 900)); Set(menu, "revealPartySelection", false);
            Invoke(menu, "PartyBack");
            Assert.IsTrue(Get<bool>(menu, "revealPartySelection"));
            Assert.AreEqual("private-actions", Get<WordboundMenuFocus>(menu, "partyFocus").Selected);
            Assert.IsFalse(Get<bool>(menu, "showPrivateActions")); Assert.AreEqual(party, coordinator.Party.Id);
            yield return Activate("ready"); yield return Activate("quick-play");
            Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("cancel-queue", controls); Assert.IsFalse(controls.Contains("party-settings"));
            Assert.IsFalse(controls.Contains("private-actions"));
            yield return Activate("cancel-queue");
            Assert.IsFalse(coordinator.Party.HasAttempt);
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/cancel"));
            yield return Activate("leave-party");
            Assert.IsNull(coordinator.Party); Assert.IsTrue(http.Requests.Last().Path.EndsWith("/leave"));
        }

        [UnityTest]
        public IEnumerator FailedQueueKeepsRetryVisibleWithoutReopeningPrivateForms()
        {
            yield return Activate("connect"); yield return Activate("mode:4");
            yield return Activate("create-settings"); Assert.IsTrue(Get<bool>(menu, "fill"));
            yield return Activate("create-party"); yield return Activate("ready");
            http.FailNext = true; yield return Activate("quick-play");
            Assert.IsTrue(coordinator.RetryRequired);
            var failed = http.Requests.Last();
            Draw(EventType.Layout); Draw(EventType.Repaint);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("retry", controls); Assert.IsFalse(controls.Contains("create-private"));
            yield return Activate("retry");
            Assert.AreEqual(failed.IdempotencyKey, http.Requests.Last().IdempotencyKey);
            Assert.AreEqual(failed.JsonBody, http.Requests.Last().JsonBody);
            Assert.AreEqual("QuickPlay", coordinator.Party.Channel);
            Assert.IsFalse(coordinator.RetryRequired);
        }

        [Test]
        public void EditableBackdropIsBorrowedAndDoesNotMutateItsThemeOrGuiState()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            var backdrop = new Texture2D(2, 2);
            try
            {
                theme.OnlineUseLobbyBackground = false; theme.OnlineBackground = backdrop;
                theme.OnlineBackgroundShade = .32f; menu.Theme = theme;
                string before = JsonUtility.ToJson(theme);
                Color colour = new Color(.2f, .6f, .8f, .9f);
                window.BeforeRepaint = () => GUI.color = colour;
                bool checkedState = false;
                window.AfterRepaint = () => { Assert.AreEqual(colour, GUI.color); checkedState = true; window.AfterRepaint = null; };
                Draw(EventType.Layout); Draw(EventType.Repaint);
                Assert.IsTrue(checkedState); Assert.AreEqual(before, JsonUtility.ToJson(theme));
                Assert.IsTrue(window.SkinRestored); Assert.IsEmpty(http.Requests);
                window.Menu = null; Invoke(menu, "OnDestroy");
                Assert.IsTrue(backdrop); Assert.IsTrue(theme);
            }
            finally { Object.DestroyImmediate(backdrop); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void HumanCompositionCopyAgreesWithActualWordboundModeCapacities()
        {
            var describe = typeof(WordboundPartyMenu).GetMethod("Composition", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (string id in GetStatic<string[]>(typeof(WordboundPartyMenu), "Modes"))
                foreach (var format in id == "TeamRace" || id == "BrawlRace" ?
                    new[] { RaceFormat.Solo, RaceFormat.Duo, RaceFormat.Squad } : new[] { RaceFormat.Solo })
                {
                    var mode = ModeDefinition.For(id, format);
                    string copy = (string)describe.Invoke(null, new object[] { id, format.ToString() });
                    StringAssert.DoesNotContain("bot", copy.ToLowerInvariant());
                    if (mode.HasRaceFormat)
                    {
                        StringAssert.Contains(mode.TeamCount + " teams", copy);
                        StringAssert.Contains(mode.TeamCapacity + " per team", copy);
                    }
                    else StringAssert.Contains(mode.MaximumPlayers + " humans", copy);
                }
        }

        static T GetStatic<T>(Type type, string name) => (T)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-.01f)]
        [TestCase(1.01f)]
        public void InvalidBackdropShadeIsRejected(float opacity)
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            try { theme.OnlineBackgroundShade = opacity; Assert.Throws<ArgumentException>(theme.Validate); }
            finally { Object.DestroyImmediate(theme); }
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
            Rect before = StablePartyCard(width);
            float measured = Get<float>(menu, "guestHeight");
            Assert.AreEqual(width, window.position.width, 1, "The native window must use the requested test width.");
            var styles = Get<WordboundUiStyles>(menu, "ui");
            Assert.AreEqual(styles.MenuRect(window.position.width, window.position.height).width, before.width, 1,
                "The actual drawn card must use this window's viewport, not a wider shared Screen size.");
            Assert.Greater(measured, 0);
            Assert.IsTrue(Get<bool>(menu, "compactPartyLayout"));
            Assert.GreaterOrEqual(before.height, measured, "The actual drawn content must fit without hiding controls.");
            Assert.Less(before.height, window.position.height - 48, "The disconnected card should release its unused vertical space.");
            Rect repaintStart = default;
            int repaintSerial = -1;
            bool checkedRepaint = false;
            window.BeforeRepaint = () => {
                repaintStart = Get<Rect>(menu, "partyRect");
                repaintSerial = window.Repaints;
                Set(menu, "guestHeight", repaintStart.height + 20);
            };
            window.AfterRepaint = () => {
                Assert.AreEqual(repaintSerial, window.Repaints, "Compare within the same actual Repaint event.");
                Assert.AreEqual(repaintStart, Get<Rect>(menu, "partyRect"), "Measured height is applied only during Layout.");
                checkedRepaint = true;
                window.AfterRepaint = null;
            };
            Draw(EventType.Repaint);
            Assert.IsTrue(checkedRepaint, "The same-event guard must actually execute.");
            Assert.IsEmpty(http.Requests);
        }

        [Test]
        public void DisconnectedCardReflowsAfterResizeAndThemedErrorScrollThenShrinksWithoutClippingActions()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            try
            {
                window.position = new Rect(100, 100, 420, 600);
                Rect narrow = StablePartyCard(420);
                window.position = new Rect(100, 100, 700, 600);
                Rect wide = StablePartyCard(700);
                Assert.LessOrEqual(wide.height, narrow.height);
                theme.BodySize = 18; theme.Padding = 20; theme.RowHeight = 42;
                menu.Theme = theme;
                window.position = new Rect(100, 100, 420, 600);
                Set(menu, "localError", string.Join(" ", Enumerable.Repeat("Connection could not finish. Please try again.", 30)));
                Rect overflow = StablePartyCard(420, true);
                window.SendEvent(new Event { type = EventType.ScrollWheel, mousePosition = overflow.center,
                    delta = new Vector2(0, 100) });
                Draw(EventType.Layout); Draw(EventType.Repaint);
                Assert.Greater(Get<Vector2>(menu, "scroll").y, 0, "Overflow must remain scrollable through the actual GUI event.");
                Set(menu, "localError", null);
                Rect smaller = StablePartyCard(420);
                Assert.Less(smaller.height, overflow.height, "Clearing content must release height rather than preserve a maximum.");
                Assert.AreEqual(Vector2.zero, Get<Vector2>(menu, "scroll"));
                Assert.IsEmpty(http.Requests);
                Assert.IsTrue(theme, "The assigned theme remains borrowed.");
            }
            finally { menu.Theme = null; Object.DestroyImmediate(theme); }
        }

        Rect StablePartyCard(int width, bool overflow = false)
        {
            var observations = new List<(int Layout, Vector4 Metrics)>();
            window.AfterRepaint = () => {
                Rect drawn = Get<Rect>(menu, "partyRect");
                if (observations.Count > 0 && observations[observations.Count - 1].Layout == window.Layouts)
                    observations.RemoveAt(observations.Count - 1);
                if (observations.Count == 16) observations.RemoveAt(0);
                observations.Add((window.Layouts, new Vector4(drawn.width, drawn.height, Get<float>(menu, "guestHeight"),
                    Get<float>(menu, "partyViewportHeight"))));
            };
            try { for (int i = 0; i < 12; i++) { Draw(EventType.Layout); Draw(EventType.Repaint); } }
            finally { window.AfterRepaint = null; }
            string trace = string.Join(" | ", observations.Select(value =>
                $"layout={value.Layout},width={value.Metrics.x},card={value.Metrics.y},measured={value.Metrics.z},scroll={value.Metrics.w}"));
            TestContext.WriteLine("WORDBOUND_PARTY_CARD_LAYOUT: requestedWidth=" + width + "; overflow=" + overflow + "; " + trace);
            Assert.GreaterOrEqual(observations.Count, 3, "Observe actual rendered layout convergence: " + trace);
            var styles = Get<WordboundUiStyles>(menu, "ui");
            Rect available = styles.MenuRect(window.position.width, window.position.height);
            Assert.AreEqual(width, window.position.width, 1);
            var settled = observations.Skip(observations.Count - 3).Select(value => value.Metrics).ToArray();
            foreach (var value in settled)
            {
                Assert.AreEqual(available.width, value.x, 1, "Draw the actual window viewport: " + trace);
                Assert.AreEqual(settled[0].y, value.y, .5f, "The card must stop changing height: " + trace);
                if (overflow)
                {
                    Assert.AreEqual(available.height, value.y, .5f, "Overflow stays inside the available viewport: " + trace);
                    Assert.Greater(value.z, value.y, "The control must exercise actual overflow: " + trace);
                }
                else Assert.AreEqual(value.z, value.y, .5f, "The settled card must fit its current measured content: " + trace);
            }
            Rect card = Get<Rect>(menu, "partyRect");
            if (!overflow)
            {
                foreach (string field in new[] { "guestFirstContentRect", "guestLastContentRect" })
                {
                    Rect child = Get<Rect>(menu, field);
                    Assert.Greater(child.height, 0, field);
                    Assert.GreaterOrEqual(child.xMin, styles.Panel.padding.left - .5f, field + " left edge");
                    Assert.GreaterOrEqual(child.yMin, styles.Panel.padding.top - .5f, field + " top edge");
                    Assert.LessOrEqual(child.xMax, card.width - styles.Panel.padding.right + .5f, field + " right edge");
                    Assert.LessOrEqual(child.yMax, card.height - styles.Panel.padding.bottom + .5f, field + " bottom edge");
                }
            }
            return card;
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
            if (request.Path == "/v1/parties" || request.Method == "PATCH") settings = OnlineSettings.Read(body["settings"]);
            if (request.Path.EndsWith("/ready")) ready = body["ready"].Bool();
            if (request.Path.EndsWith("/queue")) channel = body["channel"].String();
            if (request.Path.EndsWith("/cancel")) channel = null;
            if (request.Path.EndsWith("/leave")) return Json(new Dictionary<string, object>());
            var response = new Dictionary<string, object> {
                ["id"] = PartyId, ["revision"] = (++revision).ToString(), ["leaderId"] = Player, ["invite"] = Code,
                ["members"] = Enumerable.Range(0, humans).Select(index => new Dictionary<string, object> {
                    ["playerId"] = index == 0 ? Player : "10000000-0000-4000-8000-" + (index + 1).ToString("D12"),
                    ["ready"] = ready }).ToArray(),
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
            public Action BeforeRepaint, AfterRepaint;
            public int Layouts, Repaints;
            public string FocusedControl;
            public bool SkinRestored = true;
            void OnGUI()
            {
                if (!Menu) return;
                bool enabled = GUI.enabled;
                var borrowedSkin = GUI.skin;
                var borrowedColor = GUI.color;
                try
                {
                    if (Event.current.type == EventType.Layout) Layouts++;
                    if (Event.current.type == EventType.Repaint) Repaints++;
                    Invoke(Menu, "BeginPartyGui", true);
                    if (Event.current.type == EventType.Repaint && BeforeRepaint != null)
                    { var action = BeforeRepaint; BeforeRepaint = null; action(); }
                    try { Invoke(Menu, "DrawPartyGui", position.size); }
                    finally { Invoke(Menu, "EndPartyGui"); FocusedControl = GUI.GetNameOfFocusedControl(); }
                    if (Event.current.type == EventType.Repaint) AfterRepaint?.Invoke();
                }
                catch (Exception error) { Failure = error; }
                finally { SkinRestored &= ReferenceEquals(borrowedSkin, GUI.skin); GUI.enabled = enabled; GUI.color = borrowedColor; }
            }
        }
    }
}
