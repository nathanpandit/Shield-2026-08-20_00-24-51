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
        [SerializeField] private DuoWhiteProjectileController whiteProjectile;
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
        public DuoWhiteProjectileController WhiteProjectile => whiteProjectile;
        public float RunTime { get; private set; }
        public int DuoBestScore => persistence != null ? persistence.DuoBestScore : 0;
        public bool DebugFeaturesAvailable =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            gameplayConfig != null && gameplayConfig.explicitDebugFeaturesEnabled;
#endif

        private readonly List<float>[] reservedImpacts = { new List<float>(8), new List<float>(8) };
        private IPersistenceService persistence;
        private Tween gameOverUiDelay;
        private int whiteReservedSourceArena = -1;
        private bool tickingArenas;
        private bool whiteSpawnedDuringTick;

        public void Configure(
            GameplayConfig gameplay,
            FeedbackConfig feedback,
            DuoArenaSession firstArena,
            DuoArenaSession secondArena,
            DuoLayoutController layout,
            DuoWhiteProjectileController duoWhiteProjectile,
            ShieldAudioManager audio,
            HapticManager haptics)
        {
            gameplayConfig = gameplay;
            feedbackConfig = feedback;
            arenas = new[] { firstArena, secondArena };
            layoutController = layout;
            whiteProjectile = duoWhiteProjectile;
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
            audioManager.Initialize(persistence, feedbackConfig, false);
            hapticManager.Initialize(persistence);
            for (int i = 0; i < arenas.Length; i++)
            {
                arenas[i].Initialize(this, i, persistence);
            }

            whiteProjectile?.Initialize(this, gameplayConfig, feedbackConfig, arenas, layoutController);
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
            whiteSpawnedDuringTick = false;
            tickingArenas = true;
            for (int i = 0; i < arenas.Length; i++)
            {
                arenas[i].Tick(safeDeltaTime);
            }
            tickingArenas = false;

            // A White projectile spawned by one of the arena ticks begins moving on
            // the next frame, just like a newly acquired pooled projectile.
            if (!whiteSpawnedDuringTick)
            {
                whiteProjectile?.Tick(safeDeltaTime);
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
            whiteReservedSourceArena = -1;
            whiteProjectile?.Deactivate();
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

        public void GrantDebugGreenProtectionToAll()
        {
            if (!DebugFeaturesAvailable || State != GameState.Playing || arenas == null)
            {
                return;
            }

            for (int i = 0; i < arenas.Length; i++)
            {
                arenas[i]?.GrantDebugGreenProtection(10f);
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

        public bool TryReserveWhiteImpact(float travelDuration, out float delay)
        {
            delay = 0f;
            float gap = gameplayConfig != null ? Mathf.Max(0f, gameplayConfig.duoCrossArenaImpactGap) : 0.10f;
            float candidateImpact = RunTime + Mathf.Max(0f, travelDuration);
            PruneImpactReservations();
            if (gap > 0f)
            {
                for (int arenaIndex = 0; arenaIndex < reservedImpacts.Length; arenaIndex++)
                {
                    List<float> reservations = reservedImpacts[arenaIndex];
                    for (int i = 0; i < reservations.Count; i++)
                    {
                        if (Mathf.Abs(candidateImpact - reservations[i]) < gap)
                        {
                            delay = Mathf.Max(delay, reservations[i] + gap - candidateImpact);
                        }
                    }
                }
            }

            if (delay > 0.0001f)
            {
                return false;
            }

            // White crosses the board boundary, so both future source-board and
            // destination-board attacks must observe this arrival reservation.
            reservedImpacts[0].Add(candidateImpact);
            reservedImpacts[1].Add(candidateImpact);
            return true;
        }

        public bool TrySelectWhiteProjectile(int sourceArenaIndex)
        {
            if (!WhiteWarmupComplete || whiteProjectile == null || whiteProjectile.IsActive ||
                whiteReservedSourceArena >= 0 || sourceArenaIndex < 0 || sourceArenaIndex > 1)
            {
                return false;
            }

            float chance = gameplayConfig != null ? Mathf.Clamp01(gameplayConfig.duoWhiteProjectileChance) : 0f;
            if (chance <= 0f || UnityEngine.Random.value >= chance)
            {
                return false;
            }

            whiteReservedSourceArena = sourceArenaIndex;
            return true;
        }

        public bool CanSpawnWhiteProjectile(int sourceArenaIndex)
        {
            return State == GameState.Playing && whiteProjectile != null && !whiteProjectile.IsActive &&
                   (whiteReservedSourceArena < 0 || whiteReservedSourceArena == sourceArenaIndex);
        }

        public bool TrySpawnWhiteProjectile(int sourceArenaIndex, float normalizedYellowSpeed)
        {
            if (!CanSpawnWhiteProjectile(sourceArenaIndex) ||
                !whiteProjectile.Activate(sourceArenaIndex, normalizedYellowSpeed))
            {
                return false;
            }

            whiteReservedSourceArena = sourceArenaIndex;
            if (tickingArenas)
            {
                whiteSpawnedDuringTick = true;
            }

            return true;
        }

        public float GetWhiteTravelDuration(int sourceArenaIndex, float normalizedYellowSpeed)
        {
            return whiteProjectile != null
                ? whiteProjectile.CalculateTravelDuration(sourceArenaIndex, normalizedYellowSpeed)
                : 1f / Mathf.Max(0.01f, normalizedYellowSpeed);
        }

        public AttackDirection GetWhiteTravelDirection(int sourceArenaIndex)
        {
            if (layoutController != null && layoutController.LandscapeSplit)
            {
                return sourceArenaIndex == 0 ? AttackDirection.Left : AttackDirection.Right;
            }

            return sourceArenaIndex == 0 ? AttackDirection.Top : AttackDirection.Bottom;
        }

        public bool WhiteWarmupComplete
        {
            get
            {
                if (arenas == null || arenas.Length != 2 || gameplayConfig == null)
                {
                    return false;
                }

                int required = Mathf.Max(0, gameplayConfig.specialProjectileWarmupCount);
                return arenas[0].Spawner.SpawnedAttackCount >= required &&
                       arenas[1].Spawner.SpawnedAttackCount >= required;
            }
        }

        public void NotifyWhiteProjectileResolved(DuoWhiteProjectileController resolvedProjectile)
        {
            if (resolvedProjectile == whiteProjectile)
            {
                whiteReservedSourceArena = -1;
            }
        }

        public void PlayBlock() => audioManager?.PlayBlock();
        public void PlayCoreAbsorb() => audioManager?.PlayCoreAbsorb();
        public void PlayOrangeSwitch() => audioManager?.PlayOrangeSwitch();
        public void PlayWhiteCrossing() => audioManager?.PlayWhiteCrossing();
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
