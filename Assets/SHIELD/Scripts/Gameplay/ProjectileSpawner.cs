using UnityEngine;

namespace ShieldGame
{
    public sealed class ProjectileSpawner : MonoBehaviour
    {
        [SerializeField] private GameplayConfig gameplayConfig;
        [SerializeField] private DifficultyManager difficultyManager;
        [SerializeField] private AttackDirector attackDirector;
        [SerializeField] private ProjectilePool projectilePool;
        [SerializeField] private ArenaLayout arenaLayout;

        private IGameplaySession gameSession;
        private PendingAttack pendingAttack;
        private bool hasPendingAttack;
        private bool hasPreviousScheduledImpact;
        private bool running;
        private float runTime;
        private float lastSpawnTime;
        private float lastScheduledImpactTime;
        private AttackDirection lastScheduledImpactDirection;
        private int spawnedAttackCount;
        private int appliedLayoutRevision = -1;
        private DuoGameManager duoGameManager;
        private int duoArenaIndex = -1;

        public float RunTime => runTime;
        public float TimeUntilNextSpawn => hasPendingAttack ? Mathf.Max(0f, pendingAttack.spawnTime - runTime) : 0f;
        public float CurrentActualSpawnInterval => hasPendingAttack ? pendingAttack.spawnTime - lastSpawnTime : 0f;
        public float LastScheduledImpactTime => lastScheduledImpactTime;
        public AttackDirection LastScheduledImpactDirection => lastScheduledImpactDirection;
        public bool HasPreviousScheduledImpact => hasPreviousScheduledImpact;
        public bool LastFairnessCushionApplied { get; private set; }
        public AttackDirection? PendingDirection => hasPendingAttack ? pendingAttack.direction : (AttackDirection?)null;
        public ProjectileType? PendingProjectileType => hasPendingAttack ? pendingAttack.projectileType : (ProjectileType?)null;
        public float PendingTravelDuration => hasPendingAttack ? pendingAttack.travelDuration : 0f;
        public bool SpawningPaused => running && gameSession != null && gameSession.BlueSlowActive;

        public void Configure(GameplayConfig gameplay, DifficultyManager difficulty, AttackDirector director, ProjectilePool pool, ArenaLayout arena)
        {
            gameplayConfig = gameplay;
            difficultyManager = difficulty;
            attackDirector = director;
            projectilePool = pool;
            arenaLayout = arena;
        }

        public void Initialize(IGameplaySession session)
        {
            gameSession = session;
            gameSession.GameplayTick += HandleGameplayTick;
            difficultyManager.MilestoneChanged += HandleMilestoneChanged;
            appliedLayoutRevision = arenaLayout != null ? arenaLayout.LayoutRevision : -1;
        }

        public void ConfigureDuoFairness(DuoGameManager manager, int arenaIndex)
        {
            duoGameManager = manager;
            duoArenaIndex = arenaIndex;
        }

        public void StartRun()
        {
            RefreshLayoutIfNeeded();
            runTime = 0f;
            lastSpawnTime = 0f;
            lastScheduledImpactTime = 0f;
            lastScheduledImpactDirection = AttackDirection.Top;
            spawnedAttackCount = 0;
            hasPreviousScheduledImpact = false;
            hasPendingAttack = false;
            LastFairnessCushionApplied = false;
            running = true;

            attackDirector.ResetRun();
            PrepareFirstAttack();
        }

        public void StopRun()
        {
            running = false;
            hasPendingAttack = false;
        }

        public void ForceNextDirection(AttackDirection direction)
        {
            if (!running || !hasPendingAttack)
            {
                return;
            }

            pendingAttack.direction = direction;
            RecalculatePendingTiming(false);
        }

        public void ForceNextProjectileType(ProjectileType projectileType)
        {
            if (!running || !hasPendingAttack)
            {
                return;
            }

            pendingAttack.projectileType = projectileType;
            RecalculatePendingTiming(false);
        }

        private void HandleGameplayTick(float deltaTime)
        {
            RefreshLayoutIfNeeded();
            if (!running || !hasPendingAttack || gameSession.State != GameState.Playing)
            {
                return;
            }

            // Blue freezes the spawn clock itself. The pending attack retains its
            // remaining delay, so expiration cannot release a catch-up burst.
            if (SpawningPaused)
            {
                return;
            }

            runTime += deltaTime;
            if (runTime >= pendingAttack.spawnTime)
            {
                SpawnPendingAttack();
            }
        }

        private void RefreshLayoutIfNeeded()
        {
            if (arenaLayout == null)
            {
                return;
            }

            arenaLayout.RefreshIfScreenChanged();
            if (appliedLayoutRevision == arenaLayout.LayoutRevision)
            {
                return;
            }

            projectilePool?.ApplyLayoutToActive(arenaLayout);
            appliedLayoutRevision = arenaLayout.LayoutRevision;
        }

        private void PrepareFirstAttack()
        {
            DifficultyMilestone milestone = difficultyManager.CurrentMilestone;
            pendingAttack = new PendingAttack
            {
                direction = gameplayConfig.firstAttackDirection,
                projectileType = ProjectileType.Yellow,
                jitterMultiplier = 1f,
                fairnessRoll = 1f,
                baseCandidateTime = gameplayConfig.initialSpawnDelay,
                speed = ApplyWarmupSpeed(milestone.normalizedProjectileSpeed),
                travelDuration = GetTravelDuration(ApplyWarmupSpeed(milestone.normalizedProjectileSpeed), ProjectileType.Yellow),
                spawnTime = gameplayConfig.initialSpawnDelay
            };
            hasPendingAttack = true;
            attackDirector.RecordDirection(pendingAttack.direction);
        }

        private void SpawnPendingAttack()
        {
            PendingAttack attack = pendingAttack;
            if (duoGameManager != null &&
                !duoGameManager.TryReserveImpact(duoArenaIndex, attack.travelDuration, spawnedAttackCount == 0, out float delay))
            {
                pendingAttack.spawnTime += Mathf.Max(0f, delay);
                return;
            }

            hasPendingAttack = false;
            AttackDirection visualSpawnDirection = attack.projectileType == ProjectileType.Orange
                ? AttackDirectionUtility.Opposite(attack.direction)
                : attack.direction;

            projectilePool.Acquire(
                attack.projectileType,
                attack.direction,
                arenaLayout.GetSpawnPosition(visualSpawnDirection),
                arenaLayout.GetShieldImpactPosition(attack.direction),
                arenaLayout.GetCoreImpactPosition(attack.direction),
                attack.travelDuration,
                arenaLayout.ProjectileWorldSize,
                arenaLayout.Center,
                arenaLayout.OrangeOrbitRadius);

            lastSpawnTime = runTime;
            lastScheduledImpactTime = runTime + attack.travelDuration;
            lastScheduledImpactDirection = attack.direction;
            hasPreviousScheduledImpact = true;
            spawnedAttackCount++;

            ScheduleNextAttack();
        }

        private void ScheduleNextAttack()
        {
            DifficultyMilestone milestone = difficultyManager.CurrentMilestone;
            float jitter = difficultyManager.Config.SpawnIntervalJitterPercent;
            float jitterMultiplier = Random.Range(1f - jitter, 1f + jitter);
            float interval = ApplyWarmupInterval(milestone.baseSpawnInterval) * jitterMultiplier;
            float speed = ApplyWarmupSpeed(milestone.normalizedProjectileSpeed);

            pendingAttack = new PendingAttack
            {
                direction = attackDirector.GetNextDirection(milestone.patternFragmentChance),
                projectileType = SelectNextProjectileType(),
                jitterMultiplier = jitterMultiplier,
                fairnessRoll = Random.value,
                baseCandidateTime = runTime + interval,
                speed = speed
            };
            pendingAttack.travelDuration = GetTravelDuration(speed, pendingAttack.projectileType);
            hasPendingAttack = true;
            RecalculatePendingTiming(true);
        }

        private void RecalculatePendingTiming(bool preserveBaseCandidate)
        {
            DifficultyMilestone milestone = difficultyManager.CurrentMilestone;
            float speed = ApplyWarmupSpeed(milestone.normalizedProjectileSpeed);
            pendingAttack.speed = speed;
            pendingAttack.travelDuration = GetTravelDuration(speed, pendingAttack.projectileType);

            if (!preserveBaseCandidate)
            {
                float interval = ApplyWarmupInterval(milestone.baseSpawnInterval) * pendingAttack.jitterMultiplier;
                pendingAttack.baseCandidateTime = Mathf.Max(runTime, lastSpawnTime + interval);
            }

            float candidateSpawnTime = Mathf.Max(runTime, pendingAttack.baseCandidateTime);
            LastFairnessCushionApplied = false;
            if (hasPreviousScheduledImpact)
            {
                int steps = AttackDirectionUtility.ClockwiseSteps(lastScheduledImpactDirection, pendingAttack.direction);
                float requiredGap = milestone.hardMinimumImpactGap;
                if (pendingAttack.fairnessRoll < milestone.fairnessChance)
                {
                    requiredGap += steps * milestone.fairnessExtraGapPerClockwiseStep;
                    LastFairnessCushionApplied = steps > 0;
                }

                float earliestImpact = lastScheduledImpactTime + requiredGap;
                float earliestSpawn = earliestImpact - pendingAttack.travelDuration;
                candidateSpawnTime = Mathf.Max(candidateSpawnTime, earliestSpawn);
            }

            pendingAttack.spawnTime = candidateSpawnTime;
        }

        private ProjectileType SelectNextProjectileType()
        {
            if (spawnedAttackCount < gameplayConfig.specialProjectileWarmupCount)
            {
                return ProjectileType.Yellow;
            }

            float totalWeight = 0f;
            for (int i = 0; i <= (int)ProjectileType.Orange; i++)
            {
                totalWeight += gameplayConfig.GetProjectileWeight((ProjectileType)i);
            }

            if (totalWeight <= Mathf.Epsilon)
            {
                return ProjectileType.Yellow;
            }

            float roll = Random.value * totalWeight;
            for (int i = 0; i <= (int)ProjectileType.Orange; i++)
            {
                ProjectileType candidate = (ProjectileType)i;
                roll -= gameplayConfig.GetProjectileWeight(candidate);
                if (roll <= 0f)
                {
                    return candidate;
                }
            }

            return ProjectileType.Orange;
        }

        private float GetTravelDuration(float normalizedSpeed, ProjectileType projectileType)
        {
            float typeSpeedMultiplier = gameplayConfig.GetProjectileSpeedMultiplier(projectileType);
            float radialTravelDuration = 1f / Mathf.Max(0.01f, normalizedSpeed * typeSpeedMultiplier);
            return projectileType == ProjectileType.Orange
                ? radialTravelDuration + Mathf.Max(0.01f, gameplayConfig.orangeSwitchDuration)
                : radialTravelDuration;
        }

        private void HandleMilestoneChanged(DifficultyMilestone milestone)
        {
            if (running && hasPendingAttack && spawnedAttackCount > 0)
            {
                RecalculatePendingTiming(false);
            }
        }

        private float ApplyWarmupSpeed(float speed)
        {
            return spawnedAttackCount < difficultyManager.Config.WarmupAttackCount
                ? speed * difficultyManager.Config.WarmupSpeedMultiplier
                : speed;
        }

        private float ApplyWarmupInterval(float interval)
        {
            return spawnedAttackCount < difficultyManager.Config.WarmupAttackCount
                ? interval * difficultyManager.Config.WarmupSpawnIntervalMultiplier
                : interval;
        }

        private void OnDestroy()
        {
            if (gameSession != null)
            {
                gameSession.GameplayTick -= HandleGameplayTick;
            }

            if (difficultyManager != null)
            {
                difficultyManager.MilestoneChanged -= HandleMilestoneChanged;
            }
        }

        private struct PendingAttack
        {
            public AttackDirection direction;
            public ProjectileType projectileType;
            public float speed;
            public float travelDuration;
            public float jitterMultiplier;
            public float fairnessRoll;
            public float baseCandidateTime;
            public float spawnTime;
        }
    }
}
