using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class ControlWordsTests
    {
        static readonly ControlDevice[] FixedKeys = { ControlDevice.KeysLeft, ControlDevice.KeysRight, ControlDevice.Gamepad, ControlDevice.Touch };
        static ControlDevice[] Devices => (ControlDevice[])Enum.GetValues(typeof(ControlDevice));
        static PlayerControl[] Controls => (PlayerControl[])Enum.GetValues(typeof(PlayerControl));

        static IEnumerable<string> Sentences(ControlDevice d) => new[]
        {
            ControlWords.MoveHow(d, "WASD"),
            ControlWords.JumpDodgeHow(d, "SPACE", "L-SHIFT"),
            ControlWords.SmashHow(d, "LMB"),
            ControlWords.BookHow(d, "TAB"),
            ControlWords.SpellHow(d, "Q", "BAT"),
            ControlWords.SwingHow(d, "LMB", "BAT"),
            ControlWords.ThrowHow(d, "E", "LMB"),
            ControlWords.HandsHow(d, "1 / 2"),
            ControlWords.JoinFirst("SPACE", "join"),
            ControlWords.JoinMore,
        };

        [Test]
        public void EveryDeviceNamesOneKeyPerControl()
        {
            foreach (var device in FixedKeys)
                foreach (var control in Controls)
                {
                    string key = ControlWords.Key(device, control);
                    Assert.IsNotNull(key);
                    StringAssert.DoesNotContain(", ", key, $"{device} {control} lists more than one key");
                    StringAssert.DoesNotContain(" or ", key, $"{device} {control} lists more than one key");
                }
            foreach (var control in Controls)
                Assert.AreEqual("", ControlWords.Key(ControlDevice.MouseKeyboard, control), "mouse and keyboard keys come from the live bindings");
        }

        [Test]
        public void KeyboardHalvesHaveNoBookAndPadsPauseWithStart()
        {
            Assert.AreEqual("", ControlWords.Key(ControlDevice.KeysLeft, PlayerControl.Book));
            Assert.AreEqual("", ControlWords.Key(ControlDevice.KeysRight, PlayerControl.Book));
            Assert.AreEqual("RB", ControlWords.Key(ControlDevice.Gamepad, PlayerControl.Book));
            Assert.IsNotEmpty(ControlWords.Key(ControlDevice.Touch, PlayerControl.Book));
            Assert.AreEqual("Y", ControlWords.Key(ControlDevice.Gamepad, PlayerControl.Spell));
            Assert.AreEqual("START", ControlWords.Key(ControlDevice.Gamepad, PlayerControl.Pause));
            Assert.AreEqual("K", ControlWords.Key(ControlDevice.KeysLeft, PlayerControl.Spell));
            Assert.AreEqual("R-SHIFT", ControlWords.Key(ControlDevice.KeysRight, PlayerControl.Spell));
            Assert.AreEqual("", ControlWords.BookHow(ControlDevice.KeysLeft, ""), "no book, no book step");
        }

        [Test]
        public void EachDeviceSpellsWithItsOwnGesture()
        {
            Assert.AreEqual("Press RMB, type BAT, then press RMB again", ControlWords.SpellHow(ControlDevice.MouseKeyboard, "RMB", "BAT"),
                "the spell key opens a typing box on mouse and keyboard and pressing it again spells, so letting go does nothing");
            Assert.AreEqual("Press E to grab it, then hold LMB to aim and let go to throw", ControlWords.ThrowHow(ControlDevice.MouseKeyboard, "E", "LMB"),
                "the mouse aims a throw by holding the attack button");
            Assert.AreEqual("Press RT to grab it, then X to throw it", ControlWords.ThrowHow(ControlDevice.Gamepad, "RT", "X"), "pads keep their throw line");
            string pad = ControlWords.SpellHow(ControlDevice.Gamepad, "Y", "BAT");
            StringAssert.StartsWith("Hold Y", pad);
            StringAssert.EndsWith("let go", pad);
            StringAssert.StartsWith("Hold K", ControlWords.SpellHow(ControlDevice.KeysLeft, "K", "BAT"));
            StringAssert.Contains("W and S", ControlWords.SpellHow(ControlDevice.KeysLeft, "K", "BAT"));
            StringAssert.StartsWith("Hold R-SHIFT", ControlWords.SpellHow(ControlDevice.KeysRight, "R-SHIFT", "BAT"));
            StringAssert.StartsWith("Tap SPELL", ControlWords.SpellHow(ControlDevice.Touch, "SPELL", "BAT"));
            foreach (var device in Devices) StringAssert.Contains("BAT", ControlWords.SpellHow(device, "?", "BAT"));
        }

        [Test]
        public void TheCopySaysSpellAndOnlyUsesPlayerWords()
        {
            foreach (var device in Devices)
                foreach (string line in Sentences(device))
                    foreach (string banned in new[] { "summon", "craft", "build", "make", "wordbound", "gear" })
                        StringAssert.DoesNotContain(banned, line.ToLowerInvariant(), $"{device}: {line}");
        }

        [Test]
        public void HintsNameSeatsOnlyWhenTheirDevicesDiffer()
        {
            Func<ControlDevice, string> spell = d => ControlWords.SpellHow(d, d == ControlDevice.Gamepad ? "Y" : "Q", "BAT");
            Assert.AreEqual(spell(ControlDevice.MouseKeyboard), ControlWords.PerDevice(Array.Empty<(int, ControlDevice)>(), spell));
            Assert.AreEqual(spell(ControlDevice.Gamepad), ControlWords.PerDevice(new[] { (1, ControlDevice.Gamepad), (2, ControlDevice.Gamepad) }, spell),
                "two pads share one line");
            string mixed = ControlWords.PerDevice(new[] { (1, ControlDevice.MouseKeyboard), (2, ControlDevice.Gamepad), (3, ControlDevice.Gamepad) }, spell);
            Assert.AreEqual($"P1: {spell(ControlDevice.MouseKeyboard)}\nP2: {spell(ControlDevice.Gamepad)}", mixed);
            string book = ControlWords.PerDevice(new[] { (1, ControlDevice.KeysLeft), (2, ControlDevice.Gamepad) },
                d => ControlWords.BookHow(d, ControlWords.Key(d, PlayerControl.Book)));
            Assert.AreEqual("P2: Hold RB to see every recipe and what it does", book, "a seat without a book gets no line");
        }

        [Test]
        public void JoiningNamesOneKeyPerDevice()
        {
            Assert.AreEqual("Press SPACE to join, or Start on a controller", ControlWords.JoinFirst("SPACE", "join"));
            StringAssert.Contains("Start on a controller", ControlWords.JoinMore);
            StringAssert.Contains(". on the keyboard's right half", ControlWords.JoinMore);
        }

        [Test]
        public void OnlyPadsAndKeyboardHalvesHoldToSpell()
        {
            CollectionAssert.AreEquivalent(new[] { ControlDevice.KeysLeft, ControlDevice.KeysRight, ControlDevice.Gamepad },
                Devices.Where(ControlWords.HoldsToSpell));
        }

        [Test]
        public void TheTutorialEffectWordCarriesAnEffectAndTheFirstWordIsPlain()
        {
            var tuning = HouseTuning.FromJson(TestData.Read("items_house.json"));
            Assert.IsNotNull(tuning.Trait("LAMP", TraitKind.Heavy), "the tutorial says LAMP hits are Heavy");
            CollectionAssert.IsEmpty(tuning.TraitsOf("BAT"), "the tutorial's first word is a plain one");
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            Assert.AreEqual(1, items.Get("BAT").Hands, "BAT and LAMP fit one hand each, so the tutorial can switch hands");
            Assert.AreEqual(1, items.Get("LAMP").Hands);
            Assert.IsTrue(items.Get("BAT").Enabled && items.Get("LAMP").Enabled);
        }
    }
}
