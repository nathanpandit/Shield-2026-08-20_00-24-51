#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShieldGame.Editor
{
    [InitializeOnLoad]
    public static class ShieldPlayModeSmokeTest
    {
        private const string PendingKey = "SHIELD_PlayModeSmokeTest_Pending";
        private const string GameScenePath = "Assets/SHIELD/Scenes/Game.unity";

        static ShieldPlayModeSmokeTest()
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
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void PollForPlayMode()
        {
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying || GameManager.Instance == null)
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
                GameManager manager = GameManager.Instance;
                Require(manager != null, "GameManager did not initialize.");

                // Real editor frames may pass while the domain reload completes. Disable the
                // normal driver and restart so every assertion begins at deterministic time zero.
                InputManager inputManager = Object.FindAnyObjectByType<InputManager>();
                if (inputManager != null)
                {
                    inputManager.enabled = false;
                }

                manager.BeginRun();
                Require(manager.State == GameState.Playing, "Run did not start in Playing state.");
                Require(manager.Core != null, "CoreController was not wired to GameManager.");
                Require(manager.Shield.LogicalDirection == AttackDirection.Top, "Shield did not start at Top.");
                Require(manager.Score.CurrentScore == 0, "Score did not start at zero.");
                Require(manager.ProjectilePool.ActiveCount == 0, "Pool was not empty before the initial delay.");

                manager.Shield.RotateClockwise();
                manager.Shield.RotateClockwise();
                manager.Shield.RotateClockwise();
                Require(manager.Shield.LogicalDirection == AttackDirection.Left, "Three rapid taps did not preserve three logical steps.");
                Require(manager.Shield.PendingVisualClockwiseSteps == 3, "Three rapid taps were not preserved in the visual queue.");
                manager.Shield.ResetShield();

                manager.Tick(0.31f);
                Require(manager.ProjectilePool.ActiveCount == 1, "First projectile did not spawn after the initial delay.");
                Require(manager.Spawner.LastScheduledImpactDirection == AttackDirection.Bottom, "First projectile was not Bottom.");

                // The Bottom projectile reaches the shield before its full edge-to-core travel
                // duration. A correct logical shield must remove it at that first checkpoint.
                manager.Shield.RotateClockwise();
                manager.Shield.RotateClockwise();
                manager.Tick(1.58f);
                Require(manager.State == GameState.Playing, "Correct Bottom shield did not block the first projectile.");
                Require(manager.Score.CurrentScore == 1, "The first projectile was not blocked before reaching the core.");

                // Isolate the two-stage projectile behavior from the endless spawner.
                manager.Spawner.StopRun();
                manager.ProjectilePool.ReturnAll();
                manager.Shield.ResetShield();
                manager.Shield.RotateClockwise();
                manager.Shield.RotateClockwise();
                manager.Shield.RotateClockwise();
                manager.ProjectilePool.Acquire(
                    AttackDirection.Top,
                    Vector3.up * 5f,
                    Vector3.up,
                    Vector3.zero,
                    1f,
                    0.1f);

                manager.Tick(0.79f);
                Require(manager.Score.CurrentScore == 1, "Projectile blocked before reaching the shield radius.");
                Require(manager.ProjectilePool.ActiveCount == 1, "Projectile disappeared before shield contact.");

                // Rotate Left -> Top immediately before the contact tick. This checks that input
                // applied before the tick can still save the player at the shield boundary.
                manager.Shield.RotateClockwise();
                manager.Tick(0.02f);
                Require(manager.State == GameState.Playing, "Same-frame logical shield contact did not block.");
                Require(manager.Score.CurrentScore == 2, "A shield-radius block did not add exactly one point.");
                Require(manager.ProjectilePool.ActiveCount == 0, "Blocked projectile was not returned at the shield.");

                // A projectile that passes a mismatched side stays committed to the core perimeter.
                // Moving the shield into place afterward must not retroactively catch it.
                manager.BeginRun();
                manager.Spawner.StopRun();
                manager.ProjectilePool.ReturnAll();
                ArenaLayout arena = Object.FindAnyObjectByType<ArenaLayout>();
                Require(arena != null, "ArenaLayout was not available for core-contact validation.");
                Require(
                    Vector3.Distance(arena.GetCoreImpactPosition(AttackDirection.Right), arena.Center) > 0.001f,
                    "Arena core impact point was still the core center.");

                Vector3 expectedCoreContact = Vector3.right * 0.5f;
                ProjectileController missedProjectile = manager.ProjectilePool.Acquire(
                    AttackDirection.Right,
                    Vector3.right * 5f,
                    Vector3.right,
                    expectedCoreContact,
                    1f,
                    0.1f);

                manager.Tick(0.90f);
                Require(manager.State == GameState.Playing, "A missed shield contact ended the run before reaching the core perimeter.");
                Require(manager.ProjectilePool.ActiveCount == 1, "Unblocked projectile did not continue toward the core perimeter.");
                manager.Shield.RotateClockwise();
                manager.Tick(0.11f);

                Require(manager.State == GameState.GameOver, "A projectile was caught after already passing the shield.");
                Require(!missedProjectile.IsActive, "Projectile remained active after touching the core perimeter.");
                Require(
                    Vector3.Distance(missedProjectile.transform.position, expectedCoreContact) < 0.001f,
                    "Missed projectile traveled past the configured core perimeter.");
                Require(manager.Core.IsDestructionPlaying, "Core destruction animation did not start on a miss.");

                manager.BeginRun();
                Require(manager.State == GameState.Playing, "Restart did not return to Playing.");
                Require(manager.Score.CurrentScore == 0, "Restart did not clear the score.");
                Require(manager.Shield.LogicalDirection == AttackDirection.Top, "Restart did not reset shield to Top.");
                Require(manager.Core.IsVisible && !manager.Core.IsDestructionPlaying, "Restart did not restore the core visual.");
                Require(manager.ProjectilePool.ActiveCount == 0, "Restart did not return active projectiles to the pool.");
                Require(manager.Spawner.PendingDirection == AttackDirection.Bottom, "Restart did not schedule Bottom first.");

                Debug.Log("[SHIELD_PLAYMODE_SMOKE] PASS");
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
