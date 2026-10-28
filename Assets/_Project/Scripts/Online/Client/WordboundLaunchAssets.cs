using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary.Online
{
    [CreateAssetMenu(menuName = "Wreckabulary/Wordbound/Launch assets")]
    public sealed class WordboundLaunchAssets : ScriptableObject
    {
        public const string ResourcePath = "Wordbound/LaunchAssets";
        public const string LobbyPath = "Assets/_Project/Scenes/WordboundLobby.unity";
        public const string GameplayPath = "Assets/_Project/Scenes/WordboundGameplay.unity";
        public TextAsset PublicTrust;
        public TextAsset ApprovedPublicCourses;
        public string MapPoolVersion = "1";
        public WordboundCameraProfile CameraProfile;
        public WordboundVisualProfile VisualProfile;
        public WordboundStoryProfile StoryProfile;
        public WordboundUiTheme UiTheme;
        public WordboundCourseAsset WorkshopCourse;
        public WordboundCourseAsset ParcelServiceCourse;
        public NetworkObject PlayerBody;
        public NetworkObject ItemBody;
        public NetworkObject PlatformBody;
        public NetworkPrefabRef SnapshotChunk;

        public WordboundGameplayPrefabs BodyPrefabs() =>
            new WordboundGameplayPrefabs(PlayerBody, ItemBody, PlatformBody);

        public SceneRef GameplayScene()
        {
            int index = SceneUtility.GetBuildIndexByScenePath(GameplayPath);
            if (index < 0)
                throw new InvalidOperationException("The Wordbound gameplay scene is missing from this build.");
            return SceneRef.FromIndex(index);
        }

        public string PublicCoursesJson => ApprovedPublicCourses ? ApprovedPublicCourses.text : "[]";
    }
}
