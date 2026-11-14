using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.EditorTools;

namespace Wreckabulary.Tests.Editor
{
    public sealed class WorkspaceCourseEntriesTests
    {
        SceneSetup[] previousScenes;
        SceneAsset previousEntry;
        bool previousOpenedScene;
        bool restore;

        [SetUp]
        public void SetUp()
        {
            restore = false;
            if (WorkspacePlayPreview.IsActive || EditorApplication.isPlayingOrWillChangePlaymode)
                Assert.Ignore("Stop the current preview before testing authoring entries.");
            for (int i = 0; !Application.isBatchMode && i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty || string.IsNullOrEmpty(scene.path) && scene.rootCount > 0)
                    Assert.Ignore("Save open authoring work before testing scene navigation.");
            }
            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            previousEntry = EditorSceneManager.playModeStartScene;
            previousOpenedScene = WorkspaceBootstrap.PlayOpenedScene;
            restore = true;
        }
        [TearDown]
        public void TearDown()
        {
            if (!restore) return;
            WorkspaceBootstrap.PlayOpenedScene = previousOpenedScene;
            EditorSceneManager.playModeStartScene = previousEntry;
            if (previousScenes.Length > 0 && previousScenes.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void ExplicitEntryOpensSavedSceneAndUsesItForPlayInsteadOfTheDefaultHub()
        {
            WorkspaceBootstrap.PlayOpenedScene = false;
            string path = "Assets/_Project/Scenes/WordboundLobby.unity";
            WorkspaceCourseEntries.OpenForPlay(path);
            Assert.IsTrue(WorkspaceBootstrap.PlayOpenedScene);
            Assert.AreEqual(path, SceneManager.GetActiveScene().path);
            Assert.IsNull(EditorSceneManager.playModeStartScene,
                "Opened-scene Play uses the current saved scene; a Hub start asset would redirect the test.");
            Assert.IsFalse(SceneManager.GetActiveScene().isDirty);
        }

        [Test]
        public void MissingEntryCannotChangeTheOpenSceneOrSavedPlayPreference()
        {
            string before = SceneManager.GetActiveScene().path;
            bool preference = WorkspaceBootstrap.PlayOpenedScene;
            var entry = EditorSceneManager.playModeStartScene;
            Assert.Throws<InvalidOperationException>(() => WorkspaceCourseEntries.OpenForPlay("Assets/_Project/Scenes/Authoring/Courses/Missing.unity"));
            Assert.AreEqual(before, SceneManager.GetActiveScene().path);
            Assert.AreEqual(preference, WorkspaceBootstrap.PlayOpenedScene);
            Assert.AreSame(entry, EditorSceneManager.playModeStartScene);
        }
    }
}
