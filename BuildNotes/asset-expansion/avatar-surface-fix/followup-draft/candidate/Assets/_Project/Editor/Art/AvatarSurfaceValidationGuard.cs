using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    [InitializeOnLoad]
    public sealed class AvatarSurfaceValidationGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        static AvatarSurfaceValidationGuard()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                try { AvatarSurfaceCorrectionSetup.VerifyExistingProfile(); }
                catch (Exception error)
                {
                    EditorApplication.isPlaying = false;
                    Debug.LogError("Avatar surface validation stopped Play Mode. Preserve the changed source and review its correction. " + error.Message);
                }
            };
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            try { AvatarSurfaceCorrectionSetup.VerifyExistingProfile(); }
            catch (Exception error) { throw new BuildFailedException("Avatar surface validation stopped the build: " + error.Message); }
        }
    }
}
