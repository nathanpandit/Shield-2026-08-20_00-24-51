using System;
using UnityEngine;

namespace ShieldGame
{
    /// <summary>
    /// One self-contained SHIELD run inside DUO. Scores, difficulty, projectiles and
    /// status effects never cross this boundary; only run state and feedback are shared.
    /// </summary>
    public sealed class DuoArenaSession : MonoBehaviour, IGameplaySession
    {
        [SerializeField] private GameplayConfig gameplayConfig;
        [SerializeField] private FeedbackConfig feedbackConfig;
        [SerializeField] private CoreController coreController;
        [SerializeField] private ShieldController shieldController;
        [SerializeField] private ProjectilePool projectilePool;
        [SerializeField] private ProjectileSpawner projectileSpawner;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultyManager difficultyManager;
        [SerializeField] private AttackDirector attackDirector;
        [SerializeField] private BlockBurstVFX blockBurstVfx;

        public event Action<float> GameplayTick;
        public event Action StatusEffectsChanged;

        public int ArenaIndex { get; private set; }
        public GameState State => coordinator != null ? coordinator.State : GameState.Initializing;
        public CoreController Core => coreController;
        public ShieldController Shield => shieldController;
        public ProjectilePool ProjectilePool => projectilePool;
        public ProjectileSpawner Spawner => projectileSpawner;
        public ScoreManager Score => scoreManager;
        public DifficultyManager Difficulty => difficultyManager;
        public GameplayConfig GameplayConfig => gameplayConfig;
        public FeedbackConfig FeedbackConfig => feedbackConfig;
        public float GreenProtectionRemaining { get; private set; }
        public float BlueSlowRemaining { get; private set; }
        public float PurpleReverseRemaining { get; private set; }
        public bool GreenProtectionActive => GreenProtectionRemaining > 0f;
        public bool BlueSlowActive => BlueSlowRemaining > 0f;
        public bool PurpleReverseActive => PurpleReverseRemaining > 0f;
        public float ProjectileSpeedMultiplier => BlueSlowActive && gameplayConfig != null
            ? gameplayConfig.blueProjectileSpeedMultiplier
            : 1f;

        private DuoGameManager coordinator;

        public void Configure(
            GameplayConfig gameplay,
            FeedbackConfig feedback,
            CoreController core,
            ShieldController shield,
            ProjectilePool pool,
            ProjectileSpawner spawner,
            ScoreManager score,
            DifficultyManager difficulty,
            AttackDirector director,
            BlockBurstVFX blockVfx)
        {
            gameplayConfig = gameplay;
            feedbackConfig = feedback;
            coreController = core;
            shieldController = shield;
            projectilePool = pool;
            projectileSpawner = spawner;
            scoreManager = score;
            difficultyManager = difficulty;
            attackDirector = director;
            blockBurstVfx = blockVfx;
        }

        public void Initialize(DuoGameManager owner, int arenaIndex, IPersistenceService persistence)
        {
            coordinator = owner;
            ArenaIndex = arenaIndex;
            scoreManager.Initialize(persistence);
            difficultyManager.Initialize(scoreManager);
            projectilePool.Initialize(this);
            projectileSpawner.Initialize(this);
            projectileSpawner.ConfigureDuoFairness(owner, arenaIndex);
        }

        public void ResetRun()
        {
            projectileSpawner.StopRun();
            projectilePool.ReturnAll();
            ResetStatusEffects();
            coreController?.ResetVisual();
            shieldController?.ResetShield();
            scoreManager.ResetRun();
            difficultyManager.ResetRun();
        }

        public void StartRun()
        {
            projectileSpawner.StartRun();
        }

        public void StopRun()
        {
            projectileSpawner.StopRun();
            shieldController?.StopVisualQueue();
        }

        public void Tick(float deltaTime)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            UpdateStatusEffects(safeDeltaTime);
            GameplayTick?.Invoke(safeDeltaTime);
        }

        public void RotateShieldForTap()
        {
            if (State != GameState.Playing || shieldController == null)
            {
                return;
            }

            if (PurpleReverseActive)
            {
                shieldController.RotateCounterClockwise();
            }
            else
            {
                shieldController.RotateClockwise();
            }

            coordinator?.PlayShieldRotation();
        }

        public void HandleProjectileBlocked(ProjectileType projectileType, Vector3 impactPosition)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            scoreManager.Increment();
            if (projectileType == ProjectileType.Green)
            {
                ActivateGreenProtection();
            }
            else if (projectileType == ProjectileType.Blue)
            {
                ActivateBlueSlow();
            }

            Color color = feedbackConfig != null ? feedbackConfig.GetProjectileColor(projectileType) : Color.white;
            blockBurstVfx?.PlayAt(impactPosition, color);
            coordinator?.PlayBlock();
        }

        public void HandleProjectileMissed(ProjectileType projectileType, AttackDirection direction, Vector3 impactPosition)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            if (projectileType == ProjectileType.Blue || projectileType == ProjectileType.Green)
            {
                PlaySafeCoreAbsorb(impactPosition, projectileType);
                return;
            }

            if (projectileType == ProjectileType.Purple)
            {
                ActivatePurpleReverse();
                PlaySafeCoreAbsorb(impactPosition, projectileType);
                return;
            }

            if (GreenProtectionActive && projectileType != ProjectileType.Red)
            {
                PlaySafeCoreAbsorb(impactPosition, projectileType);
                return;
            }

            coordinator?.HandleArenaDestroyed(this, impactPosition);
        }

        public void PlayOrangeSwitchCue()
        {
            if (State == GameState.Playing)
            {
                coordinator?.PlayOrangeSwitch();
            }
        }

        public float PlayCoreDestroyed(Vector3 impactPosition)
        {
            if (coreController == null)
            {
                blockBurstVfx?.PlayCoreDeathAt(impactPosition);
                return 0f;
            }

            float duration = feedbackConfig != null ? feedbackConfig.coreDeathDuration : 0.32f;
            float punchScale = feedbackConfig != null ? feedbackConfig.coreDeathPunchScale : 1.18f;
            coreController.PlayDestroyed(duration, punchScale);
            blockBurstVfx?.PlayCoreDeathAt(coreController.CenterPosition, coreController.CurrentColor);
            return duration;
        }

        private void ActivateGreenProtection()
        {
            GreenProtectionRemaining = gameplayConfig != null ? gameplayConfig.greenProtectionDuration : 10f;
            RefreshCoreEffectColor();
            StatusEffectsChanged?.Invoke();
        }

        private void ActivateBlueSlow()
        {
            BlueSlowRemaining = gameplayConfig != null ? gameplayConfig.blueSlowDuration : 1.5f;
            RefreshCoreEffectColor();
            StatusEffectsChanged?.Invoke();
        }

        private void ActivatePurpleReverse()
        {
            PurpleReverseRemaining = gameplayConfig != null ? gameplayConfig.purpleReverseDuration : 5f;
            RefreshCoreEffectColor();
            StatusEffectsChanged?.Invoke();
        }

        private void UpdateStatusEffects(float deltaTime)
        {
            bool hadGreen = GreenProtectionActive;
            bool hadBlue = BlueSlowActive;
            bool hadPurple = PurpleReverseActive;
            if (!hadGreen && !hadBlue && !hadPurple)
            {
                return;
            }

            GreenProtectionRemaining = Mathf.Max(0f, GreenProtectionRemaining - deltaTime);
            BlueSlowRemaining = Mathf.Max(0f, BlueSlowRemaining - deltaTime);
            PurpleReverseRemaining = Mathf.Max(0f, PurpleReverseRemaining - deltaTime);
            if (hadGreen != GreenProtectionActive || hadBlue != BlueSlowActive || hadPurple != PurpleReverseActive)
            {
                RefreshCoreEffectColor();
            }

            StatusEffectsChanged?.Invoke();
        }

        private void ResetStatusEffects()
        {
            GreenProtectionRemaining = 0f;
            BlueSlowRemaining = 0f;
            PurpleReverseRemaining = 0f;
            coreController?.ClearEffectColor();
            StatusEffectsChanged?.Invoke();
        }

        private void RefreshCoreEffectColor()
        {
            if (coreController == null)
            {
                return;
            }

            if (feedbackConfig == null)
            {
                coreController.ClearEffectColor();
            }
            else if (GreenProtectionActive)
            {
                coreController.SetEffectColor(feedbackConfig.greenProjectileColor);
            }
            else if (PurpleReverseActive)
            {
                coreController.SetEffectColor(feedbackConfig.purpleProjectileColor);
            }
            else if (BlueSlowActive)
            {
                coreController.SetEffectColor(feedbackConfig.blueProjectileColor);
            }
            else
            {
                coreController.ClearEffectColor();
            }
        }

        private void PlaySafeCoreAbsorb(Vector3 impactPosition, ProjectileType projectileType)
        {
            Color color = feedbackConfig != null ? feedbackConfig.GetProjectileColor(projectileType) : Color.white;
            blockBurstVfx?.PlayCoreAbsorbAt(impactPosition, color);
            coordinator?.PlayBlock();
        }
    }
}
