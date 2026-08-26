#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShieldGame.Editor
{
    [InitializeOnLoad]
    public static class ShieldHomePlayModeSmokeTest
    {
        private const string PendingKey = "SHIELD_HomePlayModeSmokeTest_Pending";
        private const string HomeScenePath = "Assets/SHIELD/Scenes/Home.unity";

        static ShieldHomePlayModeSmokeTest()
        {
            if (SessionState.GetBool(PendingKey, false))
            {
                EditorApplication.update -= PollForPlayMode;
                EditorApplication.update += PollForPlayMode;
            }
        }

        public static void RunBatch()
        {
            SessionState.SetBool(PendingKey, true);
            EditorApplication.update -= PollForPlayMode;
            EditorApplication.update += PollForPlayMode;
            EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void PollForPlayMode()
        {
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying)
            {
                return;
            }

            HomeUIController home = Object.FindAnyObjectByType<HomeUIController>();
            ShieldAudioManager audio = Object.FindAnyObjectByType<ShieldAudioManager>();
            if (home == null || audio == null)
            {
                return;
            }

            EditorApplication.update -= PollForPlayMode;
            try
            {
                AudioSource source = audio.GetComponent<AudioSource>();
                Require(source != null, "Home did not create its menu AudioSource.");
                Require(audio.ActiveMusicClip != null && audio.ActiveMusicClip.name == "MainMenuTheme",
                    "Home did not bind the supplied main-menu theme.");
                Require(source.loop && !source.playOnAwake,
                    "Home menu music was not configured as an explicit looping track.");
                Require(Mathf.Abs(source.volume - 0.35f) < 0.001f,
                    "Home menu music did not use the configured music volume.");
                Debug.Log("[SHIELD_HOME_PLAYMODE_SMOKE] PASS");
                Finish(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new System.InvalidOperationException(message);
            }
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(PendingKey, false);
            EditorApplication.update -= PollForPlayMode;
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
