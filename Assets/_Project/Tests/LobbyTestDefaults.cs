using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.Tests
{
    [SetUpFixture]
    public sealed class LobbyTestDefaults
    {
        bool hadTutorial;
        int tutorial;
        string homes, priorHomes;

        [OneTimeSetUp]
        public void UseAReturningPlayer()
        {
            hadTutorial = PlayerPrefs.HasKey(TutorialDirector.DoneKey);
            tutorial = PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0);
            PlayerPrefs.SetInt(TutorialDirector.DoneKey, 1);
            priorHomes = HomeStorage.DefaultFolder;
            homes = Path.Combine(Path.GetTempPath(), "wreckabulary-test-homes-" + Guid.NewGuid().ToString("N"));
            HomeStorage.DefaultFolder = homes;
        }

        [OneTimeTearDown]
        public void RestorePlayer()
        {
            if (hadTutorial) PlayerPrefs.SetInt(TutorialDirector.DoneKey, tutorial);
            else PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            PlayerPrefs.Save();
            HomeStorage.DefaultFolder = priorHomes;
            if (Directory.Exists(homes)) Directory.Delete(homes, true);
        }
    }
}
