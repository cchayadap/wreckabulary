using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class LobbySocialRailTests
    {
        const float Ready = 3f, Wait = 2f;
        const string PlayerName = "Robin";
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, RecentPlayers.StorageKey };
        string[] saved;
        Func<IWordboundLobbyParty> previousFactory;
        Func<CancellationToken, Task> previousCheck;
        bool previousEnabled, registered;
        StubParty stub;
        Gamepad pad;
        Keyboard keys;
        InputSettings.EditorInputBehaviorInPlayMode? keysRoute;
        InputSettings.BackgroundBehavior keysFocus;

        sealed class StubParty : IWordboundLobbyParty
        {
            public bool Active { get; set; } = true;
            public bool Leader { get; set; } = true;
            public bool Searching { get; set; }
            public bool Found { get; set; }
            public string Heading { get; set; } = "Party · 4 of 4";
            public string Status { get; set; } = "Party of 4";
            public string Problem { get; set; }
            public string SearchTitle { get; set; } = "";
            public IReadOnlyList<string> Members { get; set; } =
                new[] { "You · leader · ready", "Ana · ready", "Bo · ready", "Cleo · not ready" };
            public bool Copied { get; set; }
            public bool CanCopy { get; set; } = true;
            public bool CanLeave { get; set; } = true;
            public bool CanFind { get; set; } = true;
            public bool CanCancel { get; set; }
            public bool CanRetry { get; set; }
            public int Copies, Leaves;
            public void Invite() { }
            public void Join(string code) { }
            public void Copy() => Copies++;
            public void Leave() => Leaves++;
            public void FindMatch() { }
            public void Cancel() { }
            public void Retry() { }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            previousEnabled = LobbyServiceCheck.Enabled;
            previousCheck = LobbyServiceCheck.Register(null);
            LobbyServiceCheck.Enabled = false;
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(MatchTally.CareerKey, new Career { Name = PlayerName }.Serialize());
            PlayerPrefs.SetString(LobbyMenu.TurnHintKey, "seen");
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                Ready, "lobby ready");
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (stub != null) WordboundLobbyParty.Closed(stub);
            if (registered) WordboundLobbyParty.Register(previousFactory);
            WordboundLobbyParty.TakeMessage();
            stub = null; registered = false;
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            pad = null;
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
            keys = null;
            if (keysRoute.HasValue)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = keysRoute.Value;
                InputSystem.settings.backgroundBehavior = keysFocus;
            }
            keysRoute = null;
            yield return TestScenes.Reset();
            LobbyServiceCheck.Register(previousCheck);
            LobbyServiceCheck.Enabled = previousEnabled;
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save();
        }

        static LobbyMenu Menu => LobbyMenu.Instance;
        static LobbySocialRail Social => Menu.Social;
        static RectTransform Panel => (RectTransform)Menu.transform.Find(LobbyPartyLineup.PartyPanel);
        static GameObject Selected => EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;

        static Button Find(string name)
        {
            var button = Menu.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"a '{name}' button is showing");
            return button;
        }

        static bool Shows(string name) => Menu.GetComponentsInChildren<Button>().Any(b => b.name == name);

        static void Click(string name)
        {
            var button = Find(name);
            Assert.IsTrue(button.interactable, $"'{name}' can be pressed");
            button.onClick.Invoke();
        }

        static string Text(Transform root, string name) => root.GetComponentsInChildren<TMP_Text>().Single(text => text.name == name).text;

        void UseStub()
        {
            stub = new StubParty();
            previousFactory = WordboundLobbyParty.Register(() => stub);
            registered = true;
            Assert.AreSame(stub, WordboundLobbyParty.Open());
        }

        static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(.35f);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator TheCollapsedRailShowsThePartyAPlusSlotAndRecentOnly()
        {
            yield return Settle();
            var rail = Social.Rail;
            CollectionAssert.AreEqual(new[] { SocialRail.Server, SocialRail.Party, "Portrait 1", SocialRail.Invite, SocialRail.Recent },
                Social.FocusNames, "top to bottom: server, party, you, a free seat, then recent");
            Assert.AreEqual("1/4", Text(rail, LobbySocialRail.CountName));
            Assert.AreEqual(SocialRail.Recent, Text(Find(SocialRail.Recent).transform, LobbySocialRail.ButtonLabelName), "Recent is an icon with its word");
            var word = Find(SocialRail.Recent).GetComponentsInChildren<TMP_Text>().Single(text => text.name == LobbySocialRail.ButtonLabelName);
            var needed = word.GetPreferredValues(word.text);
            Assert.GreaterOrEqual(word.rectTransform.rect.width, needed.x + 4f, "the Recent word has room across: " + needed.x);
            Assert.GreaterOrEqual(word.rectTransform.rect.height, needed.y, "the Recent word has a full line of height: " + needed.y);
            Assert.AreEqual(15f, word.fontSize, .01f);
            foreach (var text in rail.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, "fits without an ellipsis: " + text.text);
                Assert.GreaterOrEqual(text.fontSize, 15f, "readable: " + text.text);
            }
            foreach (string missing in new[] { SocialRail.Friends, SocialRail.Nearby, SocialRail.Requests, SocialRail.AddFriend })
                Assert.IsFalse(Shows(missing), missing + " waits for a service");
            var you = (RectTransform)Find("Portrait 1").transform;
            Assert.AreEqual(you.rect.width, you.rect.height, .5f, "square portraits");
            Assert.AreEqual("R", you.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LobbySocialRail.InitialName).text);
            if (GameAssets.I)
                Assert.AreEqual(GameAssets.I.PlayerColor(0), Find("Portrait 1").FaceOf().color, "your seat colour, as in matches");
            Assert.IsFalse(Find("Portrait 1").GetComponentsInChildren<Image>(true).Any(image => image.name == "Edge" && image.color.grayscale > .9f),
                "no white outline");
            Assert.IsFalse(Panel.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator CouchSeatsFillTheRailAndAFullPartyHasNoPlusSlot()
        {
            var guests = new List<InputBinding>();
            for (int seats = 2; seats <= LobbyMenu.PartyMax; seats++)
            {
                var guest = new ScriptedBinding();
                guests.Add(guest);
                Menu.Join(guest);
                yield return null;
                Assert.AreEqual(seats + "/4", Text(Social.Rail, LobbySocialRail.CountName));
                Assert.IsTrue(Shows("Portrait " + seats));
                Assert.AreEqual(seats < LobbyMenu.PartyMax, Shows(SocialRail.Invite), seats + " seats");
                if (GameAssets.I) Assert.AreEqual(GameAssets.I.PlayerColor(seats - 1), Find("Portrait " + seats).FaceOf().color);
            }
            Menu.Leave(guests[0]);
            yield return null;
            Assert.AreEqual("3/4", Text(Social.Rail, LobbySocialRail.CountName), "the count follows the couch");
        }

        [UnityTest]
        public IEnumerator TheCountFollowsTheOnlinePartyWhenThereIsOne()
        {
            UseStub();
            yield return Settle();
            Assert.AreEqual("4/4", Text(Social.Rail, LobbySocialRail.CountName), "four online, though only you sit on this couch");
            Assert.AreEqual(1, Menu.PartySize, "the couch itself is unchanged");
            CollectionAssert.AreEqual(new[] { "You", "Ana", "Bo", "Cleo" }, Social.Plates.Select(plate => plate.Name).ToArray());
            Assert.IsFalse(Shows(SocialRail.Invite), "a full online party has no plus slot");
            Click(SocialRail.Party);
            yield return Settle();
            StringAssert.Contains("4/4", Text(Panel, LobbySocialRail.HeadingName));
            StringAssert.Contains("ONLINE", Text(Panel, LobbySocialRail.HeadingName));
            Click(LobbySocialRail.CopyButton);
            Assert.AreEqual(1, stub.Copies, "Copy code calls the party");
            stub.Copied = true;
            yield return Settle();
            Assert.AreEqual(LobbyKit.Upper(LobbyPartyStrip.Copied), Find(LobbySocialRail.CopyButton).GetComponentInChildren<TMP_Text>().text);
            Click(LobbySocialRail.LeaveButton);
            Assert.AreEqual(1, stub.Leaves);
            Assert.IsFalse(Shows(LobbySocialRail.InviteButton), "invite and paste step aside in a party");
            stub.Active = false;
            yield return Settle();
            Assert.AreEqual("1/4", Text(Social.Rail, LobbySocialRail.CountName));
            if (Session.CanLoad(Session.WordboundScene))
                Assert.IsTrue(Shows(LobbySocialRail.InviteButton) && Shows(LobbySocialRail.PasteButton), "solo again: invite and paste code");
        }

        [UnityTest]
        public IEnumerator ThePanelOpensOnEveryPageAndEscOrBClosesIt()
        {
            foreach (string page in new[] { LobbyMenu.Home, LobbyMenu.Play, LobbyMenu.Loadout, LobbyMenu.Shop, LobbyMenu.CareerPage })
            {
                Menu.Open(page);
                yield return null;
                Click(SocialRail.Party);
                yield return null;
                Assert.IsTrue(Panel.gameObject.activeSelf, page);
                Assert.AreEqual(page, Menu.Current, "the panel lies over the page and leaves it open");
                Assert.IsTrue(Social.Focused, "focus moves into the panel");
                yield return Tap(Key.Escape);
                Assert.IsFalse(Panel.gameObject.activeSelf, "Esc closes the panel first");
                Assert.AreEqual(page, Menu.Current);
                Assert.AreSame(Find(SocialRail.Party).gameObject, Selected, "focus returns to what opened it");
            }
            Click(SocialRail.Party);
            yield return null;
            yield return Press(GamepadButton.East);
            Assert.IsFalse(Panel.gameObject.activeSelf, "B closes it too");
        }

        [UnityTest]
        public IEnumerator ShouldersDoNotTurnPagesWhileThePanelIsOpen()
        {
            Menu.Open(LobbyMenu.Play);
            Click(SocialRail.Party);
            yield return null;
            yield return Press(GamepadButton.RightShoulder);
            Assert.AreEqual(LobbyMenu.Play, Menu.Current);
            Assert.IsTrue(Panel.gameObject.activeSelf);
            yield return Press(GamepadButton.East);
            yield return Press(GamepadButton.RightShoulder);
            Assert.AreEqual(LobbyMenu.CareerPage, Menu.Current, "closed again, the shoulders turn pages as before");
        }

        [UnityTest]
        public IEnumerator FAndPadNorthJumpToTheRailAndBack()
        {
            Menu.Open(LobbyMenu.Play);
            yield return null;
            var before = Selected;
            Assert.IsNotNull(before);
            yield return Tap(Key.F);
            Assert.AreSame(Find(SocialRail.Party).gameObject, Selected, "F lands on the party");
            yield return Tap(Key.F);
            Assert.AreSame(before, Selected, "F again goes back to where you were");
            yield return Press(GamepadButton.North);
            Assert.AreSame(Find(SocialRail.Party).gameObject, Selected, "Y does the same on a pad");
            yield return Press(GamepadButton.East);
            Assert.AreSame(before, Selected, "B leaves the rail");
            Assert.AreEqual(LobbyMenu.Play, Menu.Current, "without closing the page");
        }

        [UnityTest]
        public IEnumerator FocusRunsDownTheRailAndLeftIntoTheOpenPanel()
        {
            Menu.Join(new ScriptedBinding());
            yield return null;
            var order = Social.FocusNames.Select(Find).ToArray();
            for (int i = 0; i < order.Length - 1; i++)
            {
                Assert.AreSame(order[i + 1], order[i].FindSelectableOnDown(), order[i].name + " down");
                Assert.AreSame(order[i], order[i + 1].FindSelectableOnUp(), order[i + 1].name + " up");
            }
            Click(SocialRail.Recent);
            yield return null;
            Assert.IsTrue(Panel.gameObject.activeSelf);
            Assert.AreEqual(SocialRail.Recent, Social.Section);
            var left = Find(SocialRail.Party).FindSelectableOnLeft();
            Assert.IsNotNull(left);
            Assert.IsTrue(left.transform.IsChildOf(Panel), "left from the rail goes into the open panel");
            var inside = Panel.GetComponentsInChildren<Selectable>().Where(selectable => selectable.IsInteractable()).ToArray();
            foreach (var selectable in inside)
                Assert.AreEqual(Navigation.Mode.Explicit, selectable.navigation.mode, selectable.name + " has explicit navigation");
            Assert.IsTrue(inside.Any(selectable => selectable.name == "Leave 2"), "couch seats can still be removed from the panel");
            Click("Leave 2");
            yield return null;
            Assert.AreEqual(1, Menu.PartySize);
        }

        [UnityTest]
        public IEnumerator ThePlusSlotOpensInviteAndTheJoinHelp()
        {
            Click(SocialRail.Invite);
            yield return null;
            Assert.IsTrue(Panel.gameObject.activeSelf);
            Assert.IsTrue(Panel.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Join"), "how to join on this couch");
            if (Session.CanLoad(Session.WordboundScene))
                Assert.AreSame(Find(LobbySocialRail.InviteButton).gameObject, Selected, "the plus lands on Invite friends");
            yield return Press(GamepadButton.Start);
            Assert.AreEqual(2, Menu.PartySize, "Start on a controller still takes a seat");
            yield return null;
            Assert.IsTrue(Shows("Portrait 2"));
            Assert.IsTrue(Panel.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Seat 2"));
        }

        [UnityTest]
        public IEnumerator RecentListsFinishedMatchesNewestFirstAndCanBeCleared()
        {
            var recent = new RecentPlayers();
            long now = LobbyRecentPlayers.Now;
            var house = GameConfig.Current.Houses.First();
            recent.Add(new RecentMatch(HouseModes.Dibs, house.Key, now - 7200, new[] { new RecentPlayer(PlayerName, 0, true), new RecentPlayer("Player 2", 1, false) }), now);
            recent.Add(new RecentMatch(HouseModes.Duos, "", now - 60, new[] { new RecentPlayer(PlayerName, 0, true) }), now);
            LobbyRecentPlayers.Save(recent);
            Click(SocialRail.Recent);
            yield return Settle();
            var rows = Panel.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(LobbySocialRail.RecentPrefix)).ToArray();
            Assert.AreEqual(2, rows.Length);
            StringAssert.Contains(LobbyMenu.ModeName(HouseModes.Duos), string.Join(" ", rows[0].GetComponentsInChildren<TMP_Text>().Select(text => text.text)), "newest first");
            StringAssert.Contains("You, Player 2", string.Join(" ", rows[1].GetComponentsInChildren<TMP_Text>().Select(text => text.text)));
            StringAssert.Contains("2 h ago", string.Join(" ", rows[1].GetComponentsInChildren<TMP_Text>().Select(text => text.text)));
            StringAssert.Contains(house.Value.Name, string.Join(" ", rows[1].GetComponentsInChildren<TMP_Text>().Select(text => text.text)), "the house it was played in");
            Canvas.ForceUpdateCanvases();
            foreach (var text in Panel.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, "fits: " + text.text);
            }
            Click(LobbySocialRail.ClearButton);
            yield return Settle();
            Assert.IsFalse(PlayerPrefs.HasKey(RecentPlayers.StorageKey), "cleared from this device");
            Assert.IsFalse(Panel.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith(LobbySocialRail.RecentPrefix)));
            Assert.AreEqual(RecentPlayers.Empty, Text(Panel, LobbySocialRail.EmptyName));
        }

        [UnityTest]
        public IEnumerator TheServerPillNeverInventsANumber()
        {
            yield return Settle();
            var pill = Find(SocialRail.Server);
            string text = Text(pill.transform, LobbySocialRail.ServerTextName);
            Assert.AreEqual(Session.CanLoad(Session.WordboundScene) ? ServiceText.NotChecked : ServiceText.Offline, text,
                "with no check registered it says so plainly");
            Assert.IsFalse(text.Contains("ms"));
        }

        [UnityTest]
        public IEnumerator TheServerPillTimesTheRegisteredCall()
        {
            if (!Session.CanLoad(Session.WordboundScene)) Assert.Ignore("Offline build: the pill says Offline and never checks.");
            var answer = new TaskCompletionSource<bool>();
            int calls = 0;
            LobbyServiceCheck.Register(_ => { calls++; return answer.Task; });
            LobbyServiceCheck.Enabled = true;
            yield return TestScenes.WaitUntil(() => calls == 1, Wait, "the lobby checks the service once");
            yield return Settle();
            Assert.AreEqual(ServiceText.Checking, Text(Find(SocialRail.Server).transform, LobbySocialRail.ServerTextName));
            answer.SetResult(true);
            yield return TestScenes.WaitUntil(() => Social.Service.State == ServiceState.Reachable, Wait, "the answer is timed");
            yield return Settle();
            StringAssert.EndsWith(" ms", Text(Find(SocialRail.Server).transform, LobbySocialRail.ServerTextName));
            Assert.GreaterOrEqual(Social.Service.Milliseconds, 0);
            yield return Settle();
            Assert.AreEqual(1, calls, "the next check waits " + ServiceCheck.Every + " seconds");
            Click(SocialRail.Server);
            yield return Settle();
            StringAssert.Contains("(by clock)", Text(Panel, LobbySocialRail.ServerLineText), "the region is labelled as a guess");
            StringAssert.Contains("Service ", Text(Panel, LobbySocialRail.ServerLineText), "the time is the service, not game ping");
        }

        [UnityTest]
        public IEnumerator AFailedCheckSaysNoReply()
        {
            if (!Session.CanLoad(Session.WordboundScene)) Assert.Ignore("Offline build: the pill says Offline and never checks.");
            LobbyServiceCheck.Register(_ => Task.FromException(new InvalidOperationException("unreachable")));
            LobbyServiceCheck.Enabled = true;
            yield return TestScenes.WaitUntil(() => Social.Service.State == ServiceState.NoReply, Wait, "the failed check lands");
            yield return Settle();
            Assert.AreEqual(ServiceText.NoReply, Text(Find(SocialRail.Server).transform, LobbySocialRail.ServerTextName));
        }

        [UnityTest]
        public IEnumerator TheRailLocksDuringTheStartCountdown()
        {
            float delay = LobbyMenu.StartDelay;
            LobbyMenu.StartDelay = 3600f;
            try
            {
                Menu.Choose(mode: HouseModes.Dibs);
                Click(SocialRail.Party);
                yield return null;
                Assert.IsNull(Menu.Blocked, "a plain house match can start");
                Menu.Go();
                yield return null;
                yield return null;
                Assert.IsTrue(Menu.Starting);
                Assert.IsFalse(Panel.gameObject.activeSelf, "the panel closes for the countdown");
                var group = Social.GetComponent<CanvasGroup>();
                Assert.IsFalse(group.interactable, "the rail waits until the countdown ends");
                yield return Tap(Key.F);
                Assert.IsFalse(Social.Focused, "F does nothing while starting");
                Menu.CancelStart();
                yield return null;
                Assert.IsTrue(group.interactable);
            }
            finally { LobbyMenu.StartDelay = delay; }
        }

        IEnumerator Press(GamepadButton button)
        {
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        IEnumerator Tap(Key key)
        {
            if (keys == null)
            {
                keysRoute = InputSystem.settings.editorInputBehaviorInPlayMode;
                keysFocus = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                keys = InputSystem.AddDevice<Keyboard>();
            }
            keys.MakeCurrent();
            InputSystem.QueueStateEvent(keys, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            yield return null;
        }
    }
}
