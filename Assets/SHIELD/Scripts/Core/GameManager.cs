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

    public sealed class GameManager : MonoBehaviour
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

        public GameState State { get; private set; } = GameState.Initializing;
        public CoreController Core => coreController;
        public ShieldController Shield => shieldController;
        public ProjectileSpawner Spawner => projectileSpawner;
        public ProjectilePool ProjectilePool => projectilePool;
        public ScoreManager Score => scoreManager;
        public DifficultyManager Difficulty => difficultyManager;
        public GameplayConfig GameplayConfig => gameplayConfig;
        public FeedbackConfig FeedbackConfig => feedbackConfig;
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
            Application.targetFrameRate = gameplayConfig != null ? gameplayConfig.targetFrameRate : 60;
            QualitySettings.vSyncCount = 0;
            Time.timeScale = 1f;

            persistence = new PlayerPrefsPersistenceService();
            scoreManager.Initialize(persistence);
            difficultyManager.Initialize(scoreManager);
            projectilePool.Initialize(this);
            projectileSpawner.Initialize(this);
            audioManager.Initialize(persistence, feedbackConfig);
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

            GameplayTick?.Invoke(Mathf.Max(0f, deltaTime));
        }

        public void BeginRun()
        {
            gameOverUiDelay?.Kill(false);
            gameOverUiDelay = null;
            Time.timeScale = 1f;
            SetState(GameState.Initializing);
            projectileSpawner.StopRun();
            projectilePool.ReturnAll();
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

        public void HandleProjectileBlocked(Vector3 impactPosition)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            scoreManager.Increment();
            blockBurstVfx?.PlayAt(impactPosition);
            audioManager?.PlayBlock();
        }

        public void HandleProjectileMissed(AttackDirection direction, Vector3 impactPosition)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            if (DebugInvincibility && DebugFeaturesAvailable)
            {
                LastDebugMiss = direction + " at score " + scoreManager.CurrentScore;
                Debug.Log("SHIELD debug invincibility absorbed a miss from " + direction + ".", this);
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
                blockBurstVfx?.PlayCoreDeathAt(coreController.CenterPosition);
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
