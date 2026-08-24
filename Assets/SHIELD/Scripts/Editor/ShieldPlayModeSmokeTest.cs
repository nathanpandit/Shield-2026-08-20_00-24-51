#if UNITY_EDITOR
using TMPro;
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
                ValidateCenteredScoreHud(manager);

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

                ValidateProjectileTypes(manager);

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

        private static void ValidateProjectileTypes(GameManager manager)
        {
            GameplayConfig gameplay = manager.GameplayConfig;
            FeedbackConfig feedback = manager.FeedbackConfig;
            Require(gameplay.specialProjectileWarmupCount == 8, "Special projectile warmup was not eight Yellow attacks.");
            Require(Mathf.Approximately(gameplay.yellowProjectileWeight, 0.92f), "Yellow projectile weight was not 92%.");
            Require(Mathf.Approximately(gameplay.greenProjectileWeight, 0.02f), "Green projectile weight was not 2%.");
            Require(Mathf.Approximately(gameplay.blueProjectileWeight, 0.02f), "Blue projectile weight was not 2%.");
            Require(Mathf.Approximately(gameplay.purpleProjectileWeight, 0.02f), "Purple projectile weight was not 2%.");
            Require(Mathf.Approximately(gameplay.redProjectileWeight, 0.01f), "Red projectile weight was not 1%.");
            Require(Mathf.Approximately(gameplay.orangeProjectileWeight, 0.01f), "Orange projectile weight was not 1%.");
            Require(Mathf.Approximately(gameplay.orangeSwitchDuration, 0.5f), "Orange switch duration was not 0.5 seconds.");
            Require(Mathf.Approximately(gameplay.orangeSwitchRadiusNormalized, 0.25f), "Orange switch radius was not 25% of the arena side.");
            Require(Mathf.Approximately(gameplay.blueSlowDuration, 1.5f), "Blue slowdown duration was not 1.5 seconds.");
            Require(ColorsApproximately(feedback.coreColor, Color.white), "The base core color was not white.");
            Require(ColorsApproximately(feedback.GetProjectileColor(ProjectileType.Orange), feedback.orangeProjectileColor), "Orange did not use its configured color.");

            manager.BeginRun();
            float yellowTravelDuration = manager.Spawner.PendingTravelDuration;
            manager.ForceNextProjectileType(ProjectileType.Green);
            Require(manager.Spawner.PendingProjectileType == ProjectileType.Green, "Debug forcing did not select Green.");
            Require(
                Mathf.Abs(manager.Spawner.PendingTravelDuration * 2f - yellowTravelDuration) < 0.001f,
                "Green was not scheduled at twice Yellow speed.");
            manager.ForceNextProjectileType(ProjectileType.Blue);
            Require(manager.Spawner.PendingProjectileType == ProjectileType.Blue, "Debug forcing did not select Blue.");
            Require(
                Mathf.Abs(manager.Spawner.PendingTravelDuration * 2f - yellowTravelDuration) < 0.001f,
                "Blue was not scheduled at the same twice-Yellow speed as Green.");
            manager.ForceNextProjectileType(ProjectileType.Orange);
            Require(manager.Spawner.PendingProjectileType == ProjectileType.Orange, "Debug forcing did not select Orange.");
            Require(
                Mathf.Abs(manager.Spawner.PendingTravelDuration - yellowTravelDuration - gameplay.orangeSwitchDuration) < 0.001f,
                "Orange did not add only its switch time to Yellow's travel duration.");

            ValidateOrangeProjectile(manager, gameplay, feedback);
            ValidateBlockBurstColors(feedback);

            PrepareIsolatedRun(manager);
            ProjectileController blockedBlue = AcquireManual(manager, ProjectileType.Blue, AttackDirection.Top, 0.5f);
            manager.Tick(0.11f);
            Require(!blockedBlue.IsActive, "Blocked Blue projectile was not destroyed.");
            Require(manager.Score.CurrentScore == 1, "Blocking Blue did not award one point.");
            Require(manager.BlueSlowActive, "Blocking Blue did not activate slowdown.");
            Require(Mathf.Approximately(manager.ProjectileSpeedMultiplier, 0.5f), "Blue slowdown was not 50% speed.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.blueProjectileColor), "Blue effect did not color the core Blue.");

            ProjectileController slowedYellow = AcquireManual(manager, ProjectileType.Yellow, AttackDirection.Right, 1f);
            manager.Tick(0.25f);
            Require(Mathf.Abs(slowedYellow.TravelProgress - 0.125f) < 0.001f, "Blue did not slow an active projectile to 50% speed.");
            ProjectileController slowedOrange = AcquireManual(manager, ProjectileType.Orange, AttackDirection.Right, 1f);
            Vector3 slowedOrangeStart = slowedOrange.transform.position;
            manager.Tick(0.10f);
            Require(
                Mathf.Abs(Vector3.Distance(slowedOrangeStart, slowedOrange.transform.position) - 0.25f) < 0.001f,
                "Blue did not slow Orange's Yellow-speed straight approach to 50% speed.");
            manager.ProjectilePool.ReturnAll();

            float pausedBlueTime = manager.BlueSlowRemaining;
            manager.PauseGame();
            manager.Tick(1f);
            Require(Mathf.Approximately(manager.BlueSlowRemaining, pausedBlueTime), "Effect timer advanced while paused.");
            manager.ResumeGame();

            manager.BeginRun();
            float pendingSpawnDelay = manager.Spawner.TimeUntilNextSpawn;
            float spawnClockBeforeBlue = manager.Spawner.RunTime;
            manager.HandleProjectileBlocked(ProjectileType.Blue, Vector3.zero);
            Require(manager.Score.CurrentScore == 1, "A direct Blue block did not increase score.");
            Require(manager.Spawner.SpawningPaused, "Blue did not pause projectile spawning.");
            manager.Tick(1f);
            Require(manager.ProjectilePool.ActiveCount == 0, "A projectile spawned while Blue was active.");
            Require(Mathf.Approximately(manager.Spawner.RunTime, spawnClockBeforeBlue), "The spawn clock advanced while Blue was active.");
            Require(
                Mathf.Approximately(manager.Spawner.TimeUntilNextSpawn, pendingSpawnDelay),
                "Blue did not preserve the pending projectile's remaining spawn delay.");
            manager.Tick(gameplay.blueSlowDuration - 1.01f);
            Require(manager.BlueSlowActive && manager.ProjectilePool.ActiveCount == 0, "Blue stopped suppressing spawns before 1.5 seconds.");
            manager.Tick(0.02f);
            Require(!manager.BlueSlowActive && !manager.Spawner.SpawningPaused, "Projectile spawning did not resume after Blue expired.");
            Require(manager.ProjectilePool.ActiveCount == 0, "Blue expiration released a projectile immediately instead of resuming its delay.");
            float resumedSpawnDelay = manager.Spawner.TimeUntilNextSpawn;
            Require(resumedSpawnDelay > 0f, "The pending spawn delay was not retained after Blue expired.");
            manager.Tick(resumedSpawnDelay + 0.01f);
            Require(manager.ProjectilePool.ActiveCount == 1, "Projectile spawning did not continue after Blue expired.");
            Require(Mathf.Approximately(manager.ProjectileSpeedMultiplier, 1f), "Post-Blue projectiles did not return to regular speed.");
            manager.ProjectilePool.ReturnAll();

            PrepareIsolatedRun(manager);
            ProjectileController absorbedBlue = AcquireManual(manager, ProjectileType.Blue, AttackDirection.Right, 0.5f);
            manager.Tick(0.51f);
            Require(!absorbedBlue.IsActive, "Absorbed Blue projectile remained active.");
            Require(manager.State == GameState.Playing, "Absorbing Blue ended the run.");
            Require(manager.Score.CurrentScore == 0, "Absorbing Blue incorrectly awarded score.");
            Require(!manager.BlueSlowActive, "Absorbing Blue incorrectly activated slowdown.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.coreColor), "Absorbing Blue incorrectly recolored the core.");

            PrepareIsolatedRun(manager);
            ProjectileController absorbedGreenWithoutProtection = AcquireManual(manager, ProjectileType.Green, AttackDirection.Right, 0.5f);
            manager.Tick(0.51f);
            Require(!absorbedGreenWithoutProtection.IsActive, "Absorbed Green projectile remained active.");
            Require(manager.State == GameState.Playing, "Absorbing Green without protection ended the run.");
            Require(manager.Score.CurrentScore == 0, "Absorbing Green incorrectly awarded score.");
            Require(!manager.GreenProtectionActive, "Absorbing Green incorrectly granted protection.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.coreColor), "Absorbing Green incorrectly recolored the core.");

            PrepareIsolatedRun(manager);
            ProjectileController blockedGreen = AcquireManual(manager, ProjectileType.Green, AttackDirection.Top, 0.5f);
            manager.Tick(0.11f);
            Require(!blockedGreen.IsActive, "Blocked Green projectile was not destroyed.");
            Require(manager.Score.CurrentScore == 1, "Blocking Green did not award one point.");
            Require(manager.GreenProtectionActive, "Blocking Green did not activate protection.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.greenProjectileColor), "Green effect did not color the core Green.");

            ProjectileController protectedYellow = AcquireManual(manager, ProjectileType.Yellow, AttackDirection.Right, 1f);
            manager.Tick(1f);
            Require(!protectedYellow.IsActive && manager.State == GameState.Playing, "Green protection did not absorb Yellow.");
            Require(manager.Score.CurrentScore == 1, "A Green-protected miss incorrectly awarded score.");

            ProjectileController protectedOrange = AcquireManual(manager, ProjectileType.Orange, AttackDirection.Right, 1f);
            manager.Tick(protectedOrange.TravelDuration + 0.01f);
            Require(!protectedOrange.IsActive && manager.State == GameState.Playing, "Green protection did not absorb Orange.");
            Require(manager.Score.CurrentScore == 1, "A Green-protected Orange miss incorrectly awarded score.");

            float greenBeforeGreenMiss = manager.GreenProtectionRemaining;
            ProjectileController protectedGreen = AcquireManual(manager, ProjectileType.Green, AttackDirection.Right, 0.5f);
            manager.Tick(0.51f);
            Require(!protectedGreen.IsActive && manager.State == GameState.Playing, "Green protection did not absorb Green.");
            Require(
                manager.GreenProtectionRemaining < greenBeforeGreenMiss,
                "A missed Green projectile incorrectly refreshed Green protection.");

            manager.Shield.ResetShield();
            ProjectileController blockedRed = AcquireManual(manager, ProjectileType.Red, AttackDirection.Top, 1f);
            manager.Tick(0.21f);
            Require(!blockedRed.IsActive && manager.Score.CurrentScore == 2, "Blocking Red did not behave like a scoring projectile.");

            ProjectileController blueDuringGreen = AcquireManual(manager, ProjectileType.Blue, AttackDirection.Right, 0.5f);
            manager.Tick(0.51f);
            Require(!blueDuringGreen.IsActive && manager.Score.CurrentScore == 2, "Blue absorption during Green incorrectly awarded a point.");
            Require(!manager.BlueSlowActive, "Blue absorption during Green incorrectly activated slowdown.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.greenProjectileColor), "Green did not retain core-color priority over Blue.");

            manager.Shield.ResetShield();
            ProjectileController blockedBlueDuringGreen = AcquireManual(manager, ProjectileType.Blue, AttackDirection.Top, 0.5f);
            manager.Tick(0.11f);
            Require(!blockedBlueDuringGreen.IsActive && manager.Score.CurrentScore == 3, "Blocking Blue during Green did not award one point.");
            Require(manager.BlueSlowActive, "Blocking Blue during Green did not activate slowdown.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.greenProjectileColor), "Green did not retain core-color priority after blocking Blue.");

            manager.Shield.ResetShield();
            ProjectileController blockedPurple = AcquireManual(manager, ProjectileType.Purple, AttackDirection.Top, 1f);
            manager.Tick(0.41f);
            Require(!blockedPurple.IsActive && manager.Score.CurrentScore == 4, "Blocking Purple did not award one point.");
            Require(!manager.PurpleReverseActive, "Blocking Purple incorrectly activated reverse movement.");

            ProjectileController absorbedPurple = AcquireManual(manager, ProjectileType.Purple, AttackDirection.Right, 0.25f);
            manager.Tick(0.51f);
            Require(!absorbedPurple.IsActive && manager.State == GameState.Playing, "Absorbing Purple ended the run.");
            Require(manager.Score.CurrentScore == 4, "Absorbing Purple incorrectly awarded score.");
            Require(manager.PurpleReverseActive, "Absorbing Purple did not activate reverse movement.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.greenProjectileColor), "Green did not retain core-color priority over Purple.");

            Transform effectTimers = FindEffectTimers();
            Require(effectTimers != null && effectTimers.gameObject.activeSelf, "Effect timer HUD was not visible for active effects.");
            Require(effectTimers.Find("GreenTimer").gameObject.activeSelf, "Green timer was not displayed.");
            Require(effectTimers.Find("BlueTimer").gameObject.activeSelf, "Blue timer was not displayed.");
            Require(effectTimers.Find("PurpleTimer").gameObject.activeSelf, "Purple timer was not displayed.");

            manager.Shield.ResetShield();
            manager.RotateShieldForTap();
            Require(manager.Shield.LogicalDirection == AttackDirection.Left, "Purple did not reverse a gameplay tap.");

            float pausedGreenTime = manager.GreenProtectionRemaining;
            float pausedPurpleTime = manager.PurpleReverseRemaining;
            manager.PauseGame();
            manager.Tick(1f);
            Require(Mathf.Approximately(manager.GreenProtectionRemaining, pausedGreenTime), "Green timer advanced while paused.");
            Require(Mathf.Approximately(manager.PurpleReverseRemaining, pausedPurpleTime), "Purple timer advanced while paused.");
            manager.ResumeGame();

            ProjectileController lethalRed = AcquireManual(manager, ProjectileType.Red, AttackDirection.Right, 1f);
            manager.Tick(2.01f);
            Require(!lethalRed.IsActive, "Red projectile remained active after reaching the core.");
            Require(manager.State == GameState.GameOver, "Red did not bypass active Green protection.");

            PrepareIsolatedRun(manager);
            ProjectileController isolatedPurple = AcquireManual(manager, ProjectileType.Purple, AttackDirection.Right, 1f);
            manager.Tick(1f);
            Require(!isolatedPurple.IsActive && manager.State == GameState.Playing, "An isolated Purple absorption ended the run.");
            Require(manager.Score.CurrentScore == 0, "An isolated Purple absorption incorrectly awarded score.");
            Require(manager.PurpleReverseActive, "An isolated Purple absorption did not activate reverse movement.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.purpleProjectileColor), "Purple effect did not color the core Purple.");
            manager.RotateShieldForTap();
            Require(manager.Shield.LogicalDirection == AttackDirection.Left, "Purple absorption did not reverse the next tap.");
            manager.Tick(gameplay.purpleReverseDuration + 0.01f);
            Require(!manager.PurpleReverseActive, "Purple reverse did not expire.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.coreColor), "Core did not return to white after Purple expired.");
            manager.RotateShieldForTap();
            Require(manager.Shield.LogicalDirection == AttackDirection.Top, "Clockwise movement did not return after Purple expired.");

            manager.BeginRun();
            Require(!manager.GreenProtectionActive && !manager.BlueSlowActive && !manager.PurpleReverseActive, "Restart did not clear effects.");
            Require(ColorsApproximately(manager.Core.CurrentColor, feedback.coreColor), "Restart did not restore the white core.");
            effectTimers = FindEffectTimers();
            Require(effectTimers != null && !effectTimers.gameObject.activeSelf, "Restart did not hide effect timers.");
        }

        private static void ValidateCenteredScoreHud(GameManager manager)
        {
            GameObject scoreObject = GameObject.Find("Canvas/SafeArea/ScoreText");
            Require(scoreObject != null, "The active score HUD was not found.");
            TMP_Text score = scoreObject.GetComponent<TMP_Text>();
            RectTransform rect = scoreObject.GetComponent<RectTransform>();
            RectTransform safeArea = rect.parent as RectTransform;
            Require(score != null && safeArea != null, "The active score HUD was not configured as safe-area UI text.");
            Require(Vector2.Distance(rect.anchorMin, new Vector2(0.5f, 0.5f)) < 0.001f, "The score was not horizontally and vertically centered.");
            Require(Vector2.Distance(rect.anchorMax, new Vector2(0.5f, 0.5f)) < 0.001f, "The score anchors were not fixed to the core center.");
            Require(rect.anchoredPosition.sqrMagnitude < 0.001f, "The score was offset from the core center.");
            Require(ColorsApproximately(score.color, Color.black), "The in-core score was not black.");
            Require(score.enableAutoSizing, "The score did not enable auto-sizing for longer values.");
            Require(score.canvas != null && score.canvas.renderMode == RenderMode.ScreenSpaceOverlay, "The score was not rendered above the world-space core.");

            float coreDiameter = Mathf.Min(safeArea.rect.width, safeArea.rect.height)
                * manager.GameplayConfig.coreRadiusNormalized
                * 2f;
            Require(rect.rect.width < coreDiameter && rect.rect.height < coreDiameter, "The score bounds exceeded the core diameter.");

            manager.Score.SetDebugScore(999);
            Canvas.ForceUpdateCanvases();
            score.ForceMeshUpdate();
            Require(score.text == "999", "A three-digit score was not displayed in the core.");
            Require(score.renderedWidth * 1.25f < coreDiameter, "A pulsing three-digit score could extend beyond the core horizontally.");
            Require(score.renderedHeight * 1.25f < coreDiameter, "A pulsing three-digit score could extend beyond the core vertically.");
            manager.BeginRun();
        }

        private static void ValidateBlockBurstColors(FeedbackConfig feedback)
        {
            BlockBurstVFX burst = Object.FindAnyObjectByType<BlockBurstVFX>();
            Require(burst != null, "Block burst VFX was not available for color validation.");
            ParticleSystem particles = burst.GetComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = burst.GetComponent<ParticleSystemRenderer>();
            Require(particles != null, "Block burst VFX had no particle system.");
            Require(renderer != null && renderer.sharedMaterial != null, "Block burst VFX had no tint-capable material.");

            var emittedParticles = new ParticleSystem.Particle[16];
            for (int typeIndex = 0; typeIndex <= (int)ProjectileType.Orange; typeIndex++)
            {
                ProjectileType projectileType = (ProjectileType)typeIndex;
                Color expectedColor = feedback.GetProjectileColor(projectileType);
                Color32 expectedColor32 = expectedColor;
                particles.Clear(true);
                burst.PlayAt(Vector3.zero, expectedColor);
                int emittedCount = particles.GetParticles(emittedParticles);
                Require(emittedCount == 10, projectileType + " block burst did not emit the expected particles.");
                Require(ColorsApproximately(burst.LastEmissionColor, expectedColor), projectileType + " block burst did not receive its projectile color.");
                for (int particleIndex = 0; particleIndex < emittedCount; particleIndex++)
                {
                    Color32 actualColor = emittedParticles[particleIndex].startColor;
                    Require(
                        actualColor.r == expectedColor32.r &&
                        actualColor.g == expectedColor32.g &&
                        actualColor.b == expectedColor32.b &&
                        actualColor.a == expectedColor32.a,
                        projectileType + " block burst particle did not preserve its projectile color.");
                }
            }

            particles.Clear(true);
        }

        private static void ValidateOrangeProjectile(GameManager manager, GameplayConfig gameplay, FeedbackConfig feedback)
        {
            PrepareIsolatedRun(manager);
            manager.Shield.RotateClockwise();
            manager.Shield.RotateClockwise();
            manager.Shield.RotateClockwise();
            ProjectileController orange = AcquireManual(manager, ProjectileType.Orange, AttackDirection.Right, 1f);
            Require(orange.AttackDirection == AttackDirection.Right, "Orange did not preserve its true Right attack direction.");
            Require(orange.VisualSpawnDirection == AttackDirection.Left, "Orange did not visually spawn from the opposite Left side.");
            Require(orange.transform.position.x < 0f, "Orange was not initially shown on its fake Left side.");
            Require(ColorsApproximately(orange.VisualColor, feedback.orangeProjectileColor), "Orange projectile color was not applied.");
            Require(orange.OrangeOrbitRadius >= 4.5f, "Orange did not begin switching far enough outside the fake-side shield.");
            Require(
                Mathf.Abs(orange.OrangeSwitchDuration - gameplay.orangeSwitchDuration) < 0.001f,
                "Orange switch duration did not match its configured duration.");
            Require(
                Mathf.Abs(orange.OrangeRadialSpeed - 5f) < 0.001f,
                "Orange's straight approaches did not move at the same five-units-per-second speed as Yellow.");
            Require(
                Mathf.Abs(orange.TravelDuration - 1f - gameplay.orangeSwitchDuration) < 0.001f,
                "Orange total travel time was not Yellow travel time plus its quick switch.");
            float postSwitchReactionDuration =
                (orange.ShieldContactProgress - orange.OrangeSwitchEndProgress) * orange.TravelDuration;
            Require(
                postSwitchReactionDuration >= gameplay.shieldRotationDuration - 0.001f,
                "Orange did not leave enough true-side approach time for one shield rotation.");

            manager.Tick((orange.OrangeSwitchStartProgress - 0.01f) * orange.TravelDuration);
            Require(orange.IsActive && !orange.OrangeSwitchStarted, "Orange switched before reaching its configured fake approach point.");
            Require(orange.transform.position.x < 0f && orange.transform.position.magnitude > 4f, "Orange reached the fake shield before switching.");

            float switchMidpoint = (orange.OrangeSwitchStartProgress + orange.OrangeSwitchEndProgress) * 0.5f;
            manager.Tick((switchMidpoint - orange.TravelProgress) * orange.TravelDuration);
            Require(orange.OrangeSwitchStarted && !orange.OrangeSwitchCompleted, "Orange did not enter its switch phase.");
            Require(orange.HasOrangeSwitchTrail && orange.OrangeSwitchTrailEmitting, "Orange switch afterimage was not active.");
            Require(orange.transform.position.y > 0f && Mathf.Abs(orange.transform.position.x) < 0.01f, "Orange did not curve clockwise from Left through Top toward Right.");
            Require(Mathf.Abs(orange.transform.position.magnitude - orange.OrangeOrbitRadius) < 0.01f, "Orange left its outside-shield orbit while switching.");

            float beforeTrueContact = Mathf.Lerp(orange.OrangeSwitchEndProgress, orange.ShieldContactProgress, 0.5f);
            manager.Tick((beforeTrueContact - orange.TravelProgress) * orange.TravelDuration);
            Require(orange.IsActive && orange.OrangeSwitchCompleted, "The fake-side shield incorrectly blocked Orange.");
            Require(manager.Score.CurrentScore == 0, "Orange awarded score before reaching its true-side shield.");

            manager.Shield.RotateClockwise();
            manager.Shield.RotateClockwise();
            manager.Tick((orange.ShieldContactProgress + 0.01f - orange.TravelProgress) * orange.TravelDuration);
            Require(!orange.IsActive, "Orange was not destroyed at its true-side shield.");
            Require(manager.Score.CurrentScore == 1, "Blocking Orange on its true side did not award one point.");
            Require(!manager.GreenProtectionActive && !manager.BlueSlowActive && !manager.PurpleReverseActive, "Blocking Orange activated an unrelated status effect.");

            PrepareIsolatedRun(manager);
            ProjectileController missedOrange = AcquireManual(manager, ProjectileType.Orange, AttackDirection.Right, 1f);
            manager.Tick(missedOrange.TravelDuration + 0.01f);
            Require(!missedOrange.IsActive, "Missed Orange remained active after reaching the core.");
            Require(manager.State == GameState.GameOver, "Missing Orange was not lethal without Green protection.");
        }

        private static void PrepareIsolatedRun(GameManager manager)
        {
            manager.BeginRun();
            manager.Spawner.StopRun();
            manager.ProjectilePool.ReturnAll();
            manager.Shield.ResetShield();
        }

        private static ProjectileController AcquireManual(
            GameManager manager,
            ProjectileType projectileType,
            AttackDirection direction,
            float travelDuration)
        {
            Vector3 directionVector;
            switch (direction)
            {
                case AttackDirection.Top: directionVector = Vector3.up; break;
                case AttackDirection.Right: directionVector = Vector3.right; break;
                case AttackDirection.Bottom: directionVector = Vector3.down; break;
                default: directionVector = Vector3.left; break;
            }

            bool isOrange = projectileType == ProjectileType.Orange;
            Vector3 spawnDirection = isOrange ? -directionVector : directionVector;
            float totalTravelDuration = isOrange
                ? travelDuration + manager.GameplayConfig.orangeSwitchDuration
                : travelDuration;
            return manager.ProjectilePool.Acquire(
                projectileType,
                direction,
                spawnDirection * 5f,
                directionVector * 4f,
                Vector3.zero,
                totalTravelDuration,
                0.1f,
                Vector3.zero,
                isOrange ? 4.5f : 0f);
        }

        private static Transform FindEffectTimers()
        {
            GameObject canvas = GameObject.Find("Canvas");
            return canvas != null ? canvas.transform.Find("SafeArea/EffectTimers") : null;
        }

        private static bool ColorsApproximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.001f &&
                   Mathf.Abs(a.g - b.g) < 0.001f &&
                   Mathf.Abs(a.b - b.b) < 0.001f &&
                   Mathf.Abs(a.a - b.a) < 0.001f;
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
