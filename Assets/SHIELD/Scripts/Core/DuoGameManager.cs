using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShieldGame
{
    public sealed class DuoGameManager : MonoBehaviour
    {
        [SerializeField] private GameplayConfig gameplayConfig;
        [SerializeField] private FeedbackConfig feedbackConfig;
        [SerializeField] private DuoArenaSession[] arenas;
        [SerializeField] private DuoLayoutController layoutController;
        [SerializeField] private ShieldAudioManager audioManager;
        [SerializeField] private HapticManager hapticManager;

        public static DuoGameManager Instance { get; private set; }

        public event Action RunStarted;
        public event Action<bool> PauseChanged;
        public event Action<int, int, int, int, bool> GameOver;
        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Initializing;
        public DuoLayoutController Layout => layoutController;
        public IReadOnlyList<DuoArenaSession> Arenas => arenas;
        public float RunTime { get; private set; }
        public int DuoBestScore => persistence != null ? persistence.DuoBestScore : 0;

        private readonly List<float>[] reservedImpacts = { new List<float>(8), new List<float>(8) };
        private IPersistenceService persistence;
        private Tween gameOverUiDelay;

        public void Configure(
            GameplayConfig gameplay,
            FeedbackConfig feedback,
            DuoArenaSession firstArena,
            DuoArenaSession secondArena,
            DuoLayoutController layout,
            ShieldAudioManager audio,
            HapticManager haptics)
        {
            gameplayConfig = gameplay;
            feedbackConfig = feedback;
            arenas = new[] { firstArena, secondArena };
            layoutController = layout;
            audioManager = audio;
            hapticManager = haptics;
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
            layoutController.InitializeLockedLayout();
            LockMobileOrientation();

            persistence = new PlayerPrefsPersistenceService();
            audioManager.Initialize(persistence, feedbackConfig);
            hapticManager.Initialize(persistence);
            for (int i = 0; i < arenas.Length; i++)
            {
                arenas[i].Initialize(this, i, persistence);
            }
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
            RunTime += safeDeltaTime;
            layoutController.RefreshLayoutIfNeeded();
            for (int i = 0; i < arenas.Length; i++)
            {
                arenas[i].Tick(safeDeltaTime);
            }
        }

        public void BeginRun()
        {
            gameOverUiDelay?.Kill(false);
            gameOverUiDelay = null;
            Time.timeScale = 1f;
            SetState(GameState.Initializing);
            RunTime = 0f;
            reservedImpacts[0].Clear();
            reservedImpacts[1].Clear();
            layoutController.RefreshLayoutIfNeeded(true);
            for (int i = 0; i < arenas.Length; i++)
            {
                arenas[i].ResetRun();
            }

            SetState(GameState.Playing);
            // These calls are intentionally adjacent: both opening attacks share the
            // same initial delay and are the one permitted simultaneous impact pair.
            for (int i = 0; i < arenas.Length; i++)
            {
                arenas[i].StartRun();
            }

            RunStarted?.Invoke();
        }

        public void RotateArena(int arenaIndex)
        {
            if (State == GameState.Playing && arenaIndex >= 0 && arenaIndex < arenas.Length)
            {
                arenas[arenaIndex].RotateShieldForTap();
            }
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

        public void LoadHome()
        {
            Time.timeScale = 1f;
            EnableMobileAutorotation();
            SceneManager.LoadScene("Home");
        }

        public void HandleArenaDestroyed(DuoArenaSession destroyedArena, Vector3 impactPosition)
        {
            if (State != GameState.Playing || destroyedArena == null)
            {
                return;
            }

            SetState(GameState.GameOver);
            for (int i = 0; i < arenas.Length; i++)
            {
                arenas[i].StopRun();
            }

            float presentationDelay = destroyedArena.PlayCoreDestroyed(impactPosition);
            int first = arenas[0].Score.CurrentScore;
            int second = arenas[1].Score.CurrentScore;
            int total = first + second;
            bool newBest = total > persistence.DuoBestScore;
            if (newBest)
            {
                persistence.DuoBestScore = total;
                persistence.Save();
            }

            audioManager?.PlayDeath();
            hapticManager?.PlayDeathHaptic();
            int best = persistence.DuoBestScore;
            if (presentationDelay <= 0f)
            {
                GameOver?.Invoke(first, second, total, best, newBest);
                return;
            }

            gameOverUiDelay = DOVirtual.DelayedCall(
                    presentationDelay,
                    () =>
                    {
                        gameOverUiDelay = null;
                        if (State == GameState.GameOver)
                        {
                            GameOver?.Invoke(first, second, total, best, newBest);
                        }
                    },
                    true)
                .SetUpdate(true);
        }

        /// <summary>
        /// Reserves a planned impact for cross-arena readability. It delays only the
        /// later candidate and never alters either arena's internal difficulty curve.
        /// </summary>
        public bool TryReserveImpact(int arenaIndex, float travelDuration, bool openingAttack, out float delay)
        {
            delay = 0f;
            if (arenaIndex < 0 || arenaIndex > 1)
            {
                return true;
            }

            float gap = gameplayConfig != null ? Mathf.Max(0f, gameplayConfig.duoCrossArenaImpactGap) : 0.10f;
            float candidateImpact = RunTime + Mathf.Max(0f, travelDuration);
            PruneImpactReservations();
            if (!openingAttack && gap > 0f)
            {
                List<float> other = reservedImpacts[1 - arenaIndex];
                for (int i = 0; i < other.Count; i++)
                {
                    if (Mathf.Abs(candidateImpact - other[i]) < gap)
                    {
                        delay = Mathf.Max(delay, other[i] + gap - candidateImpact);
                    }
                }
            }

            if (delay > 0.0001f)
            {
                return false;
            }

            reservedImpacts[arenaIndex].Add(candidateImpact);
            return true;
        }

        public void PlayBlock() => audioManager?.PlayBlock();
        public void PlayOrangeSwitch() => audioManager?.PlayOrangeSwitch();
        public void PlayShieldRotation() => audioManager?.PlayShieldRotation();

        private void PruneImpactReservations()
        {
            for (int arenaIndex = 0; arenaIndex < reservedImpacts.Length; arenaIndex++)
            {
                List<float> reservations = reservedImpacts[arenaIndex];
                for (int i = reservations.Count - 1; i >= 0; i--)
                {
                    if (reservations[i] < RunTime - 0.01f)
                    {
                        reservations.RemoveAt(i);
                    }
                }
            }
        }

        private void LockMobileOrientation()
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }

            if (layoutController.LandscapeSplit)
            {
                Screen.orientation = Screen.orientation == ScreenOrientation.LandscapeRight
                    ? ScreenOrientation.LandscapeRight
                    : ScreenOrientation.LandscapeLeft;
            }
            else
            {
                Screen.orientation = Screen.orientation == ScreenOrientation.PortraitUpsideDown
                    ? ScreenOrientation.PortraitUpsideDown
                    : ScreenOrientation.Portrait;
            }
        }

        private static void EnableMobileAutorotation()
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }

            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        private void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
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
