using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public sealed class WinterAudioTests
    {
        string savedPack, savedTheme;
        bool hadMute, wasMuted, hadVolume, listenerPaused;
        float volume, savedVolume;
        readonly List<AudioClip> heard = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedPack = PlayerPrefs.HasKey(GameSoundPacks.PreferenceKey) ? PlayerPrefs.GetString(GameSoundPacks.PreferenceKey) : null;
            savedTheme = LobbyThemes.Current.Id;
            hadMute = PlayerPrefs.HasKey("wv.muted"); wasMuted = GameFeedback.Muted;
            hadVolume = PlayerPrefs.HasKey(LobbyMenu.VolumeKey); savedVolume = PlayerPrefs.GetFloat(LobbyMenu.VolumeKey, 1f);
            volume = AudioListener.volume;
            listenerPaused = AudioListener.pause;
            yield return TestScenes.Reset();
            GameFeedback.Muted = false;
            AudioListener.volume = 1f;
            AudioListener.pause = false;
            GameSoundPacks.Select("winter");
            heard.Clear(); GameFeedback.Played += Record;
            yield return new WaitForSecondsRealtime(.07f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameFeedback.Played -= Record;
            yield return TestScenes.Reset();
            if (savedPack == null) PlayerPrefs.DeleteKey(GameSoundPacks.PreferenceKey); else PlayerPrefs.SetString(GameSoundPacks.PreferenceKey, savedPack);
            GameFeedback.Muted = wasMuted;
            if (!hadMute) PlayerPrefs.DeleteKey("wv.muted");
            if (hadVolume) PlayerPrefs.SetFloat(LobbyMenu.VolumeKey, savedVolume); else PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
            AudioListener.volume = volume;
            AudioListener.pause = listenerPaused;
            LobbyThemes.Preview(savedTheme);
            PlayerPrefs.Save();
        }

        void Record(GameCue cue, AudioClip clip) => heard.Add(clip);

        [UnityTest]
        public IEnumerator VariantsAlternateOnlyAfterPlaybackAndRespectMuteMasterAndThrottle()
        {
            Assert.IsNotNull(Resources.Load<GameSoundBank>("AudioPacks/Winter"));
            AudioListener.volume = .37f;
            GameFeedback.Play(GameCue.Pickup);
            Assert.AreEqual(1, heard.Count);
            StringAssert.StartsWith("bell-", heard[0].name);
            GameFeedback.Play(GameCue.Pickup);
            Assert.AreEqual(1, heard.Count, "Same-frame calls are throttled without consuming a variant.");
            GameFeedback.Muted = true;
            yield return new WaitForSecondsRealtime(.07f);
            GameFeedback.Play(GameCue.Pickup);
            Assert.AreEqual(1, heard.Count);
            GameFeedback.Muted = false;
            GameFeedback.Play(GameCue.Pickup);
            Assert.AreEqual(2, heard.Count);
            Assert.AreNotSame(heard[0], heard[1]);
            yield return new WaitForSecondsRealtime(.07f);
            GameFeedback.Play(GameCue.Pickup);
            Assert.AreSame(heard[0], heard[2]);
            Assert.AreEqual(.37f, AudioListener.volume);
            var speaker = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Single(source => source.name == "House sound cues");
            Assert.AreEqual(.13f, speaker.volume);
            Assert.AreEqual(0f, speaker.spatialBlend);
        }

        [UnityTest]
        public IEnumerator SilentMasterAndPausedListenerDoNotConsumeVariants()
        {
            GameFeedback.Play(GameCue.Dodge);
            Assert.AreEqual(1, heard.Count);
            var first = heard[0];
            yield return new WaitForSecondsRealtime(.07f);
            AudioListener.volume = 0f;
            GameFeedback.Play(GameCue.Dodge);
            Assert.AreEqual(1, heard.Count, "A silent master produces no accepted playback.");
            AudioListener.volume = .37f;
            GameFeedback.Play(GameCue.Dodge);
            Assert.AreEqual(2, heard.Count);
            Assert.AreNotSame(first, heard[1], "Silent master does not advance the variant or throttle.");
            yield return new WaitForSecondsRealtime(.07f);
            AudioListener.pause = true;
            GameFeedback.Play(GameCue.Dodge);
            Assert.AreEqual(2, heard.Count);
            AudioListener.pause = false;
            GameFeedback.Play(GameCue.Dodge);
            Assert.AreEqual(3, heard.Count);
            Assert.AreSame(first, heard[2], "Paused listener does not advance the variant or throttle.");
        }

        [UnityTest]
        public IEnumerator PreviewAndVisualThemesDoNotEquipOrAdvanceGameplayVariants()
        {
            GameFeedback.Play(GameCue.Craft);
            var first = heard.Last();
            GameSoundPacks.Select("default");
            yield return new WaitForSecondsRealtime(.07f);
            Assert.IsTrue(GameSoundPacks.Preview("winter", GameCue.Craft));
            Assert.AreEqual("default", GameSoundPacks.Selected);
            Assert.IsFalse(PlayerPrefs.HasKey(GameSoundPacks.PreferenceKey));
            Assert.IsTrue(LobbyThemes.Preview("candy"));
            Assert.AreEqual("default", GameSoundPacks.Selected);
            GameSoundPacks.Select("winter");
            yield return new WaitForSecondsRealtime(.07f);
            GameFeedback.Play(GameCue.Craft);
            Assert.AreNotSame(first, heard.Last(), "Preview has a separate cursor from gameplay.");
            string selected = GameSoundPacks.Selected;
            Assert.IsFalse(GameSoundPacks.Select("missing-pack"));
            Assert.AreEqual(selected, GameSoundPacks.Selected);
        }

        [UnityTest]
        public IEnumerator UnsupportedCuesAndClassicPackKeepTheExistingSyntheticSounds()
        {
            GameFeedback.Play(GameCue.Jump);
            Assert.AreEqual("House Jump", heard.Last().name);
            Assert.AreEqual(16000, heard.Last().frequency);
            GameSoundPacks.Select("default");
            GameFeedback.Play(GameCue.Dodge);
            Assert.AreEqual("House Dodge", heard.Last().name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseSoundPackSelectorPreviewsAndResetRestoresClassic()
        {
            GameSoundPacks.Select("default");
            yield return TestScenes.Load(Session.DibsScene);
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.StateController.FadeDuration = 0f;
            hud.TogglePause();
            void Click(string name) => hud.GetComponentsInChildren<Button>().Single(button => button.name == name).onClick.Invoke();
            Click("Pause settings");
            Click("Sound pack more");
            Assert.AreEqual("winter", GameSoundPacks.Selected);
            heard.Clear();
            yield return new WaitForSecondsRealtime(.07f);
            Click("Preview sound pack");
            Assert.AreEqual(1, heard.Count);
            StringAssert.StartsWith("bell-", heard[0].name);
            Click("Reset audio");
            Assert.AreEqual("default", GameSoundPacks.Selected);
            Assert.IsFalse(PlayerPrefs.HasKey(GameSoundPacks.PreferenceKey));
            Assert.IsTrue(hud.Paused);
        }
    }
}
