using System;
using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.EditorTests
{
    public class ControlTipsTests
    {
        int? savedEnabled, savedLearned;

        [SetUp]
        public void Save()
        {
            savedEnabled = PlayerPrefs.HasKey(ControlTips.EnabledKey) ? PlayerPrefs.GetInt(ControlTips.EnabledKey) : null;
            savedLearned = PlayerPrefs.HasKey(ControlTips.LearnedKey) ? PlayerPrefs.GetInt(ControlTips.LearnedKey) : null;
            ControlTips.Reset();
        }

        [TearDown]
        public void Restore()
        {
            ControlTips.Reset();
            foreach (ControlTips.Tip tip in Enum.GetValues(typeof(ControlTips.Tip)))
                if (savedLearned.HasValue && (savedLearned.Value & (1 << (int)tip)) != 0) ControlTips.Learn(tip);
            if (savedEnabled.HasValue) PlayerPrefs.SetInt(ControlTips.EnabledKey, savedEnabled.Value);
            PlayerPrefs.Save();
        }

        [Test]
        public void TipsComeOneAtATimeUntilEachControlIsUsed()
        {
            Assert.AreEqual(ControlTips.Tip.Move, ControlTips.Next(true));
            ControlTips.Learn(ControlTips.Tip.Move);
            Assert.AreEqual(ControlTips.Tip.Look, ControlTips.Next(true));
            Assert.AreEqual(ControlTips.Tip.Smash, ControlTips.Next(false), "Looking and aiming only apply over the shoulder.");
            foreach (ControlTips.Tip tip in Enum.GetValues(typeof(ControlTips.Tip))) ControlTips.Learn(tip);
            Assert.IsNull(ControlTips.Next(true), "Nothing left to teach: the view stays clear.");
        }

        [Test]
        public void SettingsTurnTipsOffAndReplayThem()
        {
            ControlTips.Learn(ControlTips.Tip.Move);
            ControlTips.Enabled = false;
            Assert.IsNull(ControlTips.Next(true));
            ControlTips.Enabled = true;
            Assert.AreEqual(ControlTips.Tip.Move, ControlTips.Next(true), "Turning tips back on replays them from the start.");
        }

        [Test]
        public void EveryTipNamesItsKeyAndTheLastPointsToSettings()
        {
            foreach (ControlTips.Tip tip in Enum.GetValues(typeof(ControlTips.Tip)))
                StringAssert.Contains("</color></b>", ControlTips.Text(tip), tip + " shows a key");
            StringAssert.Contains("Settings › Controls", ControlTips.Text(ControlTips.Tip.Menu), "The full list lives in Settings.");
        }
    }
}
