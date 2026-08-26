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
                Require(first.FeedbackConfig.deathClip != null && first.FeedbackConfig.deathClip.name == "CoreDestroyed" &&
                        second.FeedbackConfig.deathClip == first.FeedbackConfig.deathClip,
                    "The shared DUO core-destruction sound was not configured.");
                Require(first.Score != second.Score, "DUO arenas shared a ScoreManager.");
                Require(first.Difficulty != second.Difficulty, "DUO arenas shared a DifficultyManager.");
                Require(first.ProjectilePool != second.ProjectilePool, "DUO arenas shared a projectile pool.");
                Require(first.Shield.LogicalDirection == AttackDirection.Top && second.Shield.LogicalDirection == AttackDirection.Top,
                    "Both DUO shields did not start at Top.");
                Require(first.Score.CurrentScore == 0 && second.Score.CurrentScore == 0,
                    "DUO scores did not start independently at zero.");
                Require(first.Spawner.PendingProjectileType == ProjectileType.Yellow && second.Spawner.PendingProjectileType == ProjectileType.Yellow,
                    "Both DUO boards did not begin their independent Yellow warmup.");
                Require(InputManager.GetDuoArenaIndex(KeyCode.Space) == 0,
                    "Space was not mapped to the left/top DUO arena.");
                Require(InputManager.GetDuoArenaIndex(KeyCode.LeftArrow) == 1 &&
                        InputManager.GetDuoArenaIndex(KeyCode.RightArrow) == 1,
                    "Both arrow keys were not mapped to the right/bottom DUO arena.");

                ValidateResponsiveSplitRules();
                ValidateLayout(manager, first, second);
                ValidateDebugGreenProtection(manager, first, second);
                ValidateLocalEffectsAndInputs(manager, first, second);
                ValidateWhiteProjectile(manager, first, second);

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
                manager.TogglePause();
                Require(manager.State == GameState.Paused && first.State == GameState.Paused && second.State == GameState.Paused,
                    "The DUO pause toggle did not pause both arenas.");
                manager.TogglePause();
                Require(manager.State == GameState.Playing && first.State == GameState.Playing && second.State == GameState.Playing,
                    "The DUO pause toggle did not resume both arenas.");

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

        private static void ValidateDebugGreenProtection(
            DuoGameManager manager,
            DuoArenaSession first,
            DuoArenaSession second)
        {
            manager.GrantDebugGreenProtectionToAll();
            Require(first.GreenProtectionActive && second.GreenProtectionActive,
                "DUO debug Green did not protect every core.");
            Require(Mathf.Abs(first.GreenProtectionRemaining - 10f) < 0.001f &&
                    Mathf.Abs(second.GreenProtectionRemaining - 10f) < 0.001f,
                "DUO debug Green did not grant exactly ten seconds to both cores.");
            Require(first.Score.CurrentScore == 0 && second.Score.CurrentScore == 0,
                "DUO debug Green incorrectly awarded score.");

            manager.Tick(0.40f);
            Require(first.GreenProtectionRemaining < 10f && second.GreenProtectionRemaining < 10f,
                "DUO debug Green timers did not advance during play.");
            manager.GrantDebugGreenProtectionToAll();
            Require(Mathf.Abs(first.GreenProtectionRemaining - 10f) < 0.001f &&
                    Mathf.Abs(second.GreenProtectionRemaining - 10f) < 0.001f,
                "Pressing the DUO debug Green command again did not refresh both timers.");

            first.HandleProjectileMissed(ProjectileType.Red, AttackDirection.Top, first.Core.CenterPosition);
            Require(manager.State == GameState.GameOver,
                "DUO debug Green incorrectly protected a core from Red.");
            manager.BeginRun();
        }

        private static void ValidateWhiteProjectile(
            DuoGameManager manager,
            DuoArenaSession first,
            DuoArenaSession second)
        {
            DuoWhiteProjectileController white = manager.WhiteProjectile;
            Require(white != null, "DUO White projectile controller was not initialized.");
            Require(first.GameplayConfig.GetProjectileWeight(ProjectileType.White) == 0f,
                "White leaked into the ordinary projectile weight table used by SOLO.");
            Require(Mathf.Approximately(first.GameplayConfig.duoWhiteProjectileChance, 0.01f),
                "DUO White projectile chance was not tuned to one percent.");
            Require(Mathf.Approximately(first.GameplayConfig.duoWhiteCurveDuration, 0.5f),
                "DUO White source curve was not tuned to 0.5 seconds.");
            Require(!manager.WhiteWarmupComplete,
                "White became eligible before both eight-Yellow warmups completed.");

            PrepareIsolatedWhiteRun(manager, first, second);
            AttackDirection direction = manager.GetWhiteTravelDirection(0);
            DuoArenaSession destination = second;
            SetShieldDirection(destination.Shield, AttackDirectionUtility.Opposite(direction));
            Require(manager.TrySpawnWhiteProjectile(0, 1f), "Could not spawn the DUO White projectile.");
            Require(!manager.TrySpawnWhiteProjectile(1, 1f),
                "A second White projectile was allowed while one was already active.");
            Require(white.SourceArenaIndex == 0 && white.DestinationArenaIndex == 1,
                "White did not preserve its source and destination arena ownership.");
            Require(white.DestinationDirection == direction,
                "White destination attack direction did not match the locked DUO layout.");
            Require(ColorApproximately(white.VisualColor, Color.white), "White projectile was not rendered white.");
            Require(Mathf.Abs((white.CurveEndTime - white.CurveStartTime) - 0.5f) < 0.001f,
                "White's clockwise source curve did not use the configured duration.");
            ArenaLayout sourceLayout = manager.Layout.GetArenaLayout(0);
            Require(Vector3.Distance(white.transform.position, sourceLayout.GetSpawnPosition(direction)) < 0.001f,
                "White did not begin at the required outer edge of its source arena.");

            manager.Tick(white.CurveStartTime + 0.25f);
            float sourceDistance = Vector3.Distance(white.transform.position, sourceLayout.Center);
            float sourceShieldDistance = Vector3.Distance(sourceLayout.GetShieldImpactPosition(direction), sourceLayout.Center);
            Require(sourceDistance > sourceShieldDistance,
                "White's source curve entered the source shield instead of staying outside it.");
            Require(first.Score.CurrentScore == 0 && second.Score.CurrentScore == 0,
                "White incorrectly interacted with its source arena during the curve.");

            manager.Tick(Mathf.Max(0f, white.HandoffTime - white.BaseElapsed) + 0.01f);
            second.HandleProjectileBlocked(ProjectileType.Blue, second.Core.CenterPosition);
            float beforeSlow = white.BaseElapsed;
            manager.Tick(0.20f);
            Require(Mathf.Abs((white.BaseElapsed - beforeSlow) - 0.10f) < 0.015f,
                "Destination Blue did not slow White after it entered the destination phase.");

            PrepareIsolatedWhiteRun(manager, first, second);
            direction = manager.GetWhiteTravelDirection(1);
            SetShieldDirection(first.Shield, direction);
            Require(manager.TrySpawnWhiteProjectile(1, 1f), "Could not spawn reverse-route White for its block test.");
            float shieldContact = white.ShieldContactTime;
            manager.Tick(shieldContact + 0.01f);
            Require(!white.IsActive, "A destination shield block did not destroy White at shield contact.");
            Require(first.Score.CurrentScore == 1 && second.Score.CurrentScore == 0,
                "A White block did not award exactly one point to the destination arena.");
            Require(!first.GreenProtectionActive && !first.BlueSlowActive && !first.PurpleReverseActive,
                "Blocking White incorrectly activated a status effect.");

            manager.BeginRun();
            second.Spawner.StopRun();
            first.ProjectilePool.ReturnAll();
            second.ProjectilePool.ReturnAll();
            first.Spawner.ForceNextProjectileType(ProjectileType.White);
            first.HandleProjectileBlocked(ProjectileType.Blue, first.Core.CenterPosition);
            manager.Tick(0.50f);
            Require(!white.IsActive && first.Spawner.SpawningPaused,
                "Source Blue did not delay a pending White replacement spawn.");
            for (int i = 0; i < 25; i++)
            {
                manager.Tick(0.10f);
            }
            Require(!first.Spawner.SpawningPaused && white.IsActive,
                "A source-delayed White projectile did not resume spawning after Blue expired.");

            PrepareIsolatedWhiteRun(manager, first, second);
            direction = manager.GetWhiteTravelDirection(0);
            SetShieldDirection(second.Shield, AttackDirectionUtility.Opposite(direction));
            second.HandleProjectileBlocked(ProjectileType.Green, second.Core.CenterPosition);
            Require(manager.TrySpawnWhiteProjectile(0, 1f), "Could not spawn White for its Green absorb test.");
            float greenProtectedTravel = white.TotalDuration;
            manager.Tick(greenProtectedTravel + 0.01f);
            Require(manager.State == GameState.Playing && !white.IsActive,
                "Green protection did not safely absorb a White core hit.");
            Require(first.Score.CurrentScore == 0 && second.Score.CurrentScore == 1,
                "A Green-protected White miss incorrectly awarded score.");

            PrepareIsolatedWhiteRun(manager, first, second);
            direction = manager.GetWhiteTravelDirection(0);
            SetShieldDirection(second.Shield, AttackDirectionUtility.Opposite(direction));
            Require(manager.TrySpawnWhiteProjectile(0, 1f), "Could not spawn White for its lethal miss test.");
            float lethalTravel = white.TotalDuration;
            manager.Tick(lethalTravel + 0.01f);
            Require(manager.State == GameState.GameOver,
                "An unprotected White hit did not destroy the destination arena.");
            Require(second.Core.IsDestructionPlaying && !first.Core.IsDestructionPlaying,
                "A White miss did not animate only the destination core's destruction.");
        }

        private static void PrepareIsolatedWhiteRun(
            DuoGameManager manager,
            DuoArenaSession first,
            DuoArenaSession second)
        {
            manager.BeginRun();
            first.Spawner.StopRun();
            second.Spawner.StopRun();
            first.ProjectilePool.ReturnAll();
            second.ProjectilePool.ReturnAll();
            first.Shield.ResetShield();
            second.Shield.ResetShield();
        }

        private static void SetShieldDirection(ShieldController shield, AttackDirection direction)
        {
            shield.ResetShield();
            for (int i = 0; i < 4 && shield.LogicalDirection != direction; i++)
            {
                shield.RotateClockwise();
            }

            Require(shield.LogicalDirection == direction, "Could not set a shield direction for the White smoke test.");
        }

        private static bool ColorApproximately(Color first, Color second)
        {
            return Mathf.Abs(first.r - second.r) < 0.001f &&
                   Mathf.Abs(first.g - second.g) < 0.001f &&
                   Mathf.Abs(first.b - second.b) < 0.001f &&
                   Mathf.Abs(first.a - second.a) < 0.001f;
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
