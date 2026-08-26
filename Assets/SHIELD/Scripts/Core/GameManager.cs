using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShieldGame
{
    public enum GameState
    {
        Initializing,
        Playing,
        Paused,
        GameOver
    }

    public sealed class GameManager : MonoBehaviour, IGameplaySession
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
        [SerializeField] private ShieldAudioManager audioManager;
        [SerializeField] private HapticManager hapticManager;
        [SerializeField] private BlockBurstVFX blockBurstVfx;

        public static GameManager Instance { get; private set; }

        public event Action<float> GameplayTick;
        public event Action RunStarted;
        public event Action<bool> PauseChanged;
        public event Action<int, int, bool> GameOver;
        public event Action<GameState> StateChanged;
        public event Action<bool> DebugOverlayChanged;
        public event Action StatusEffectsChanged;

        public GameState State { get; private set; } = GameState.Initializing;
        public CoreController Core => coreController;
        public ShieldController Shield => shieldController;
        public ProjectileSpawner Spawner => projectileSpawner;
        public ProjectilePool ProjectilePool => projectilePool;
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
        public bool DebugInvincibility { get; private set; }
        public bool DebugOverlayVisible { get; private set; }
        public string LastDebugMiss { get; private set; } = "None";
        public bool DebugFeaturesAvailable =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            gameplayConfig != null && gameplayConfig.explicitDebugFeaturesEnabled;
#endif

        private readonly int[] debugScores = { 0, 50, 100, 200, 500, 1000 };
        private IPersistenceService persistence;
        private Tween gameOverUiDelay;

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
            ShieldAudioManager audio,
            HapticManager haptics,
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
            audioManager = audio;
            hapticManager = haptics;
            blockBurstVfx = blockVfx;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.Portrait;
            }
            Application.targetFrameRate = gameplayConfig != null ? gameplayConfig.targetFrameRate : 60;
            QualitySettings.vSyncCount = 0;
            Time.timeScale = 1f;

            persistence = new PlayerPrefsPersistenceService();
            scoreManager.Initialize(persistence);
            difficultyManager.Initialize(scoreManager);
            projectilePool.Initialize(this);
            projectileSpawner.Initialize(this);
            audioManager.Initialize(persistence, feedbackConfig, false);
            hapticManager.Initialize(persistence);
        }

        private void Start()
        {
            BeginRun();
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

        public void BeginRun()
        {
            gameOverUiDelay?.Kill(false);
            gameOverUiDelay = null;
            Time.timeScale = 1f;
            SetState(GameState.Initializing);
            projectileSpawner.StopRun();
            projectilePool.ReturnAll();
            ResetStatusEffects();
            coreController?.ResetVisual();
            shieldController.ResetShield();
            scoreManager.ResetRun();
            difficultyManager.ResetRun();
            DebugInvincibility = false;
            LastDebugMiss = "None";

            SetState(GameState.Playing);
            projectileSpawner.StartRun();
            RunStarted?.Invoke();
        }

        public void PauseGame()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            SetState(GameState.Paused);
            Time.timeScale = 0f;
            PauseChanged?.Invoke(true);
        }

        public void ResumeGame()
        {
            if (State != GameState.Paused)
            {
                return;
            }

            Time.timeScale = 1f;
            SetState(GameState.Playing);
            PauseChanged?.Invoke(false);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
            {
                PauseGame();
            }
            else if (State == GameState.Paused)
            {
                ResumeGame();
            }
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

            Color projectileColor = feedbackConfig != null
                ? feedbackConfig.GetProjectileColor(projectileType)
                : Color.white;
            blockBurstVfx?.PlayAt(impactPosition, projectileColor);
            audioManager?.PlayBlock();
        }

        public void HandleProjectileMissed(ProjectileType projectileType, AttackDirection direction, Vector3 impactPosition)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            if (DebugInvincibility && DebugFeaturesAvailable)
            {
                LastDebugMiss = projectileType + " " + direction + " at score " + scoreManager.CurrentScore;
                Debug.Log("SHIELD debug invincibility absorbed a " + projectileType + " miss from " + direction + ".", this);
                return;
            }

            if (projectileType == ProjectileType.Blue)
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

            if (projectileType == ProjectileType.Green)
            {
                PlaySafeCoreAbsorb(impactPosition, projectileType);
                return;
            }

            if (GreenProtectionActive && projectileType != ProjectileType.Red)
            {
                PlaySafeCoreAbsorb(impactPosition, projectileType);
                return;
            }

            EndRun(impactPosition);
        }

        public void ToggleDebugInvincibility()
        {
            if (DebugFeaturesAvailable)
            {
                DebugInvincibility = !DebugInvincibility;
                StateChanged?.Invoke(State);
            }
        }

        public void GrantDebugGreenProtection()
        {
            if (!DebugFeaturesAvailable || State != GameState.Playing)
            {
                return;
            }

            GreenProtectionRemaining = 10f;
            RefreshCoreEffectColor();
            StatusEffectsChanged?.Invoke();
        }

        public void ToggleDebugOverlay()
        {
            if (!DebugFeaturesAvailable)
            {
                return;
            }

            DebugOverlayVisible = !DebugOverlayVisible;
            DebugOverlayChanged?.Invoke(DebugOverlayVisible);
        }

        public void ForceNextAttack(AttackDirection direction)
        {
            if (DebugFeaturesAvailable && State == GameState.Playing)
            {
                projectileSpawner.ForceNextDirection(direction);
            }
        }

        public void ForceNextProjectileType(ProjectileType projectileType)
        {
            if (DebugFeaturesAvailable && State == GameState.Playing)
            {
                projectileSpawner.ForceNextProjectileType(projectileType);
            }
        }

        public void PlayOrangeSwitchCue()
        {
            if (State == GameState.Playing)
            {
                audioManager?.PlayOrangeSwitch();
            }
        }

        public void AdjustDebugScore(int offset)
        {
            if (!DebugFeaturesAvailable || State != GameState.Playing)
            {
                return;
            }

            int nearest = 0;
            for (int i = 0; i < debugScores.Length; i++)
            {
                if (debugScores[i] <= scoreManager.CurrentScore)
                {
                    nearest = i;
                }
            }

            int targetIndex = Mathf.Clamp(nearest + offset, 0, debugScores.Length - 1);
            if (offset > 0 && debugScores[targetIndex] <= scoreManager.CurrentScore && targetIndex < debugScores.Length - 1)
            {
                targetIndex++;
            }

            scoreManager.SetDebugScore(debugScores[targetIndex]);
        }

        public void LoadHome()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Home");
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
                return;
            }

            if (GreenProtectionActive)
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
            Color color = feedbackConfig != null
                ? feedbackConfig.GetProjectileColor(projectileType)
                : Color.white;
            blockBurstVfx?.PlayCoreAbsorbAt(impactPosition, color);
            audioManager?.PlayCoreAbsorb();
        }

        private void EndRun(Vector3 impactPosition)
        {
            SetState(GameState.GameOver);
            projectileSpawner.StopRun();
            shieldController.StopVisualQueue();
            float presentationDelay = 0f;
            if (coreController != null)
            {
                float duration = feedbackConfig != null ? feedbackConfig.coreDeathDuration : 0.32f;
                float punchScale = feedbackConfig != null ? feedbackConfig.coreDeathPunchScale : 1.18f;
                coreController.PlayDestroyed(duration, punchScale);
                blockBurstVfx?.PlayCoreDeathAt(coreController.CenterPosition, coreController.CurrentColor);
                presentationDelay = duration;
            }
            else
            {
                blockBurstVfx?.PlayCoreDeathAt(impactPosition);
            }

            bool newBest = scoreManager.FinalizeRun();
            audioManager?.PlayDeath();
            hapticManager?.PlayDeathHaptic();
            int finalScore = scoreManager.CurrentScore;
            int bestScore = scoreManager.BestScore;
            if (presentationDelay <= 0f)
            {
                GameOver?.Invoke(finalScore, bestScore, newBest);
                return;
            }

            // Gameplay is already frozen by GameOver state. Delay only the nearly opaque
            // overlay so the core destruction remains visible to the player.
            gameOverUiDelay = DOVirtual.DelayedCall(
                    presentationDelay,
                    () =>
                    {
                        gameOverUiDelay = null;
                        if (State == GameState.GameOver)
                        {
                            GameOver?.Invoke(finalScore, bestScore, newBest);
                        }
                    },
                    true)
                .SetUpdate(true);
        }

        private void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(State);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && State == GameState.Playing)
            {
                PauseGame();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && State == GameState.Playing)
            {
                PauseGame();
            }
        }

        private void OnDestroy()
        {
            gameOverUiDelay?.Kill(false);
            if (Instance == this)
            {
                Instance = null;
            }

            Time.timeScale = 1f;
        }
    }
}
