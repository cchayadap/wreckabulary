using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public sealed class LobbyPlayPresentationTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active &&
                !GameHud.Active.StateController.IsTransitioning, 2f, "play presentation ready");
        }

        [UnityTearDown]
        public IEnumerator TearDown() { yield return TestScenes.Reset(); }

        [UnityTest]
        public IEnumerator PlayHidesCharactersWithoutLosingPartyAndRestoresTheSameAvatarOnReturn()
        {
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Home);
            var second = new ScriptedBinding(); menu.Join(second);
            yield return null;
            var avatar = menu.Stage.Avatar;
            var bindings = Session.Bindings.ToArray();
            var outfit = menu.Outfit.Serialize();
            var page = menu.Page<PlayPage>();
            try
            {
                foreach (bool maps in new[] { false, true })
                {
                    foreach (bool wordbound in new[] { false, true })
                    {
                        menu.Choose(queue: maps ? LobbyMenu.Workshop : LobbyMenu.Practice);
                        page.WordboundSolo = wordbound; page.WordboundMaps = wordbound;
                        menu.Open(LobbyMenu.Play);
                        yield return null;
                        Assert.IsFalse(menu.Stage.CharactersVisible);
                        Assert.AreSame(avatar, menu.Stage.Avatar);
                        Assert.IsFalse(avatar.gameObject.activeSelf);
                        foreach (Transform child in menu.Stage.transform)
                            if (child.name.StartsWith("Lobby guest ")) Assert.IsFalse(child.gameObject.activeSelf);
                        foreach (var shadow in menu.Stage.GetComponentsInChildren<RawImage>(true))
                            if (shadow.name.Contains("contact shadow")) Assert.IsFalse(shadow.enabled);
                        Assert.AreEqual(2, menu.PartySize); Assert.AreSame(second, menu.Party.Single());
                        CollectionAssert.AreEqual(bindings, Session.Bindings);
                        Assert.AreEqual(outfit, menu.Outfit.Serialize());
                    }
                }
                foreach (string destination in new[] { LobbyMenu.Loadout, LobbyMenu.Home })
                {
                    menu.Open(destination); yield return null;
                    Assert.IsTrue(menu.Stage.CharactersVisible);
                    Assert.AreSame(avatar, menu.Stage.Avatar); Assert.IsTrue(avatar.gameObject.activeSelf);
                    Assert.AreEqual(2, menu.PartySize); Assert.AreSame(second, menu.Party.Single());
                }
            }
            finally { if (menu && menu.Party.Contains(second)) menu.Leave(second); }
        }
    }
}
