using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.Tests
{
    public class CareerSaveTests
    {
        bool hadCareer, hadPointer;
        string career, pointer, aside;

        [SetUp]
        public void SetUp()
        {
            hadCareer = PlayerPrefs.HasKey(MatchTally.CareerKey); career = PlayerPrefs.GetString(MatchTally.CareerKey, "");
            hadPointer = PlayerPrefs.HasKey(MatchTally.CorruptKey); pointer = PlayerPrefs.GetString(MatchTally.CorruptKey, "");
            MatchTally.TakeSetAside();
            aside = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (aside != null) PlayerPrefs.DeleteKey(aside);
            if (hadCareer) PlayerPrefs.SetString(MatchTally.CareerKey, career); else PlayerPrefs.DeleteKey(MatchTally.CareerKey);
            if (hadPointer) PlayerPrefs.SetString(MatchTally.CorruptKey, pointer); else PlayerPrefs.DeleteKey(MatchTally.CorruptKey);
            PlayerPrefs.Save();
            MatchTally.TakeSetAside();
        }

        [Test]
        public void AnUnreadableCareerIsKeptAsideBeforeANewOneIsSaved()
        {
            const string damaged = "{\"coins\":1200,\"xp\":";
            PlayerPrefs.SetString(MatchTally.CareerKey, damaged);
            var fresh = MatchTally.LoadCareer();
            aside = MatchTally.TakeSetAside();
            Assert.IsNotNull(aside, "the damaged save is reported");
            StringAssert.StartsWith(MatchTally.CorruptKey + ".", aside);
            Assert.AreEqual(damaged, PlayerPrefs.GetString(aside), "the damaged text is kept byte for byte");
            Assert.AreEqual(aside, PlayerPrefs.GetString(MatchTally.CorruptKey), "the newest copy is easy to find");
            Assert.IsFalse(PlayerPrefs.HasKey(MatchTally.CareerKey));
            Assert.AreEqual(0, fresh.Coins);

            fresh.Coins = 40;
            MatchTally.SaveCareer(fresh);
            Assert.AreEqual(damaged, PlayerPrefs.GetString(aside), "saving the new career leaves the old save untouched");
            Assert.AreEqual(40, MatchTally.LoadCareer().Coins);
            Assert.IsNull(MatchTally.TakeSetAside(), "a readable save is never set aside");
        }

        [Test]
        public void AFirstRunHasNothingToKeepAside()
        {
            PlayerPrefs.DeleteKey(MatchTally.CareerKey);
            Assert.AreEqual(0, MatchTally.LoadCareer().Coins);
            Assert.IsNull(MatchTally.TakeSetAside());
        }
    }
}
