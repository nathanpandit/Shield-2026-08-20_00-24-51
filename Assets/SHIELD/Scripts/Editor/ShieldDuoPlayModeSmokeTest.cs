#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShieldGame.Editor
{
    [InitializeOnLoad]
    public static class ShieldDuoPlayModeSmokeTest
    {
        private const string PendingKey = "SHIELD_DuoPlayModeSmokeTest_Pending";
        private const string DuoScenePath = "Assets/SHIELD/Scenes/DuoGame.unity";

        static ShieldDuoPlayModeSmokeTest()
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
            EditorSceneManager.OpenScene(DuoScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void PollForPlayMode()
        {
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying || DuoGameManager.Instance == null)
            {
                return;
            }

            EditorApplication.update -= PollForPlayMode;
            RunAssertions();
        }

        private static void RunAssertions()
        {
            try
            {
                DuoGameManager manager = DuoGameManager.Instance;
                InputManager input = Object.FindAnyObjectByType<InputManager>();
                if (input != null)
                {
                    input.enabled = false;
                }

                manager.BeginRun();
                Require(manager.State == GameState.Playing, "DUO run did not start in Playing state.");
                Require(manager.Arenas.Count == 2, "DUO did not initialize exactly two arenas.");
                DuoArenaSession first = manager.Arenas[0];
                DuoArenaSession second = manager.Arenas[1];
                Require(first != second, "DUO arenas were not separate sessions.");
                Require(first.Score != second.Score, "DUO arenas shared a ScoreManager.");
                Require(first.Difficulty != second.Difficulty, "DUO arenas shared a DifficultyManager.");
                Require(first.ProjectilePool != second.ProjectilePool, "DUO arenas shared a projectile pool.");
                Require(first.Shield.LogicalDirection == AttackDirection.Top && second.Shield.LogicalDirection == AttackDirection.Top,
                    "Both DUO shields did not start at Top.");
                Require(first.Score.CurrentScore == 0 && second.Score.CurrentScore == 0,
                    "DUO scores did not start independently at zero.");
                Require(first.Spawner.PendingProjectileType == ProjectileType.Yellow && second.Spawner.PendingProjectileType == ProjectileType.Yellow,
                    "Both DUO boards did not begin their independent Yellow warmup.");

                ValidateResponsiveSplitRules();
                ValidateLayout(manager, first, second);
                ValidateLocalEffectsAndInputs(manager, first, second);

                manager.BeginRun();
                manager.Tick(0.31f);
                Require(first.ProjectilePool.ActiveCount == 1 && second.ProjectilePool.ActiveCount == 1,
                    "The simultaneous DUO opening did not spawn on both boards.");
                Require(Mathf.Abs(first.Spawner.LastScheduledImpactTime - second.Spawner.LastScheduledImpactTime) < 0.001f,
                    "The deliberately simultaneous opening attacks did not share an impact time.");

                manager.BeginRun();
                Require(manager.TryReserveImpact(0, 1f, false, out float firstDelay) && firstDelay <= 0.0001f,
                    "The first cross-arena impact reservation was unexpectedly delayed.");
                Require(!manager.TryReserveImpact(1, 1f, false, out float secondDelay) && secondDelay >= 0.099f,
                    "DUO did not enforce its configurable cross-arena impact gap.");

                manager.BeginRun();
                manager.PauseGame();
                Require(manager.State == GameState.Paused && first.State == GameState.Paused && second.State == GameState.Paused,
                    "Pause did not apply to both DUO arenas.");
                manager.ResumeGame();
                Require(manager.State == GameState.Playing && first.State == GameState.Playing && second.State == GameState.Playing,
                    "Resume did not apply to both DUO arenas.");

                first.HandleProjectileBlocked(ProjectileType.Yellow, first.Core.CenterPosition);
                second.HandleProjectileBlocked(ProjectileType.Yellow, second.Core.CenterPosition);
                Require(first.Score.CurrentScore == 1 && second.Score.CurrentScore == 1,
                    "DUO scores did not remain independently writable.");
                manager.BeginRun();
                first.HandleProjectileMissed(ProjectileType.Red, AttackDirection.Top, first.Core.CenterPosition);
                Require(manager.State == GameState.GameOver, "A destroyed DUO core did not end the combined run.");
                Require(first.Core.IsDestructionPlaying, "The struck DUO core did not begin its destruction animation.");
                Require(second.Core.IsVisible && !second.Core.IsDestructionPlaying,
                    "The surviving DUO core incorrectly played the destruction animation.");

                Require(Object.FindAnyObjectByType<DuoGameUIController>() != null, "DUO UI controller was not present.");
                TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int liveScoreTexts = 0;
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i].name == "ScoreText" && texts[i].text == "0") liveScoreTexts++;
                }
                Require(liveScoreTexts >= 2, "DUO did not render a score inside each core.");

                Debug.Log("[SHIELD_DUO_PLAYMODE_SMOKE] PASS");
                Finish(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void ValidateLayout(DuoGameManager manager, DuoArenaSession first, DuoArenaSession second)
        {
            Rect firstRegion = manager.Layout.FirstPixelRegion;
            Rect secondRegion = manager.Layout.SecondPixelRegion;
            Require(firstRegion.width > 0f && firstRegion.height > 0f && secondRegion.width > 0f && secondRegion.height > 0f,
                "DUO pixel regions were not initialized.");
            Camera camera = Camera.main;
            Vector3 firstScreen = camera.WorldToScreenPoint(first.Core.CenterPosition);
            Vector3 secondScreen = camera.WorldToScreenPoint(second.Core.CenterPosition);
            Require(firstRegion.Contains(firstScreen), "First core was not centered inside its DUO region.");
            Require(secondRegion.Contains(secondScreen), "Second core was not centered inside its DUO region.");
            if (manager.Layout.LandscapeSplit)
            {
                Require(first.Core.CenterPosition.x < second.Core.CenterPosition.x, "Wide DUO layout was not left/right.");
            }
            else
            {
                Require(first.Core.CenterPosition.y > second.Core.CenterPosition.y, "Tall DUO layout was not top/bottom.");
            }
        }

        private static void ValidateResponsiveSplitRules()
        {
            Require(DuoLayoutController.ShouldUseLandscapeSplit(800f, 800f),
                "A square screen did not select the required left/right DUO layout.");
            DuoLayoutController.CalculateRegions(new Rect(0f, 0f, 600f, 1000f), false, out Rect top, out Rect bottom);
            Require(top.yMin >= bottom.yMax - 0.001f && Mathf.Approximately(top.width, bottom.width),
                "Tall-screen DUO regions were not arranged top/bottom.");
            DuoLayoutController.CalculateRegions(new Rect(0f, 0f, 1000f, 600f), true, out Rect left, out Rect right);
            Require(left.xMax <= right.xMin + 0.001f && Mathf.Approximately(left.height, right.height),
                "Wide-screen DUO regions were not arranged left/right.");
        }

        private static void ValidateLocalEffectsAndInputs(DuoGameManager manager, DuoArenaSession first, DuoArenaSession second)
        {
            first.HandleProjectileBlocked(ProjectileType.Green, first.Core.CenterPosition);
            Require(first.Score.CurrentScore == 1 && second.Score.CurrentScore == 0,
                "A block on one board changed the other board's score.");
            Require(first.GreenProtectionActive && !second.GreenProtectionActive,
                "Green protection leaked across DUO arenas.");

            second.HandleProjectileBlocked(ProjectileType.Blue, second.Core.CenterPosition);
            Require(second.Score.CurrentScore == 1, "A blocked Blue did not score on its own board.");
            Require(second.BlueSlowActive && !first.BlueSlowActive,
                "Blue slowdown leaked across DUO arenas.");
            Require(second.Spawner.SpawningPaused && !first.Spawner.SpawningPaused,
                "Blue spawn suppression was not local to its DUO arena.");

            first.HandleProjectileMissed(ProjectileType.Purple, AttackDirection.Top, first.Core.CenterPosition);
            Require(first.PurpleReverseActive && !second.PurpleReverseActive,
                "Purple reversal leaked across DUO arenas.");
            manager.RotateArena(0);
            manager.RotateArena(1);
            Require(first.Shield.LogicalDirection == AttackDirection.Left,
                "The upper/left arena did not apply its local Purple reversal.");
            Require(second.Shield.LogicalDirection == AttackDirection.Right,
                "The lower/right arena did not retain regular rotation.");
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
