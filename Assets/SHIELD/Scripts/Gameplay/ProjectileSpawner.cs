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

        private GameManager gameManager;
        private PendingAttack pendingAttack;
        private bool hasPendingAttack;
        private bool hasPreviousScheduledImpact;
        private bool running;
        private float runTime;
        private float lastSpawnTime;
        private float lastScheduledImpactTime;
        private AttackDirection lastScheduledImpactDirection;
        private int spawnedAttackCount;

        public float RunTime => runTime;
        public float TimeUntilNextSpawn => hasPendingAttack ? Mathf.Max(0f, pendingAttack.spawnTime - runTime) : 0f;
        public float CurrentActualSpawnInterval => hasPendingAttack ? pendingAttack.spawnTime - lastSpawnTime : 0f;
        public float LastScheduledImpactTime => lastScheduledImpactTime;
        public AttackDirection LastScheduledImpactDirection => lastScheduledImpactDirection;
        public bool HasPreviousScheduledImpact => hasPreviousScheduledImpact;
        public bool LastFairnessCushionApplied { get; private set; }
        public AttackDirection? PendingDirection => hasPendingAttack ? pendingAttack.direction : (AttackDirection?)null;

        public void Configure(GameplayConfig gameplay, DifficultyManager difficulty, AttackDirector director, ProjectilePool pool, ArenaLayout arena)
        {
            gameplayConfig = gameplay;
            difficultyManager = difficulty;
            attackDirector = director;
            projectilePool = pool;
            arenaLayout = arena;
        }

        public void Initialize(GameManager manager)
        {
            gameManager = manager;
            gameManager.GameplayTick += HandleGameplayTick;
            difficultyManager.MilestoneChanged += HandleMilestoneChanged;
        }

        public void StartRun()
        {
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

        private void HandleGameplayTick(float deltaTime)
        {
            if (!running || !hasPendingAttack || gameManager.State != GameState.Playing)
            {
                return;
            }

            runTime += deltaTime;
            if (runTime >= pendingAttack.spawnTime)
            {
                SpawnPendingAttack();
            }
        }

        private void PrepareFirstAttack()
        {
            DifficultyMilestone milestone = difficultyManager.CurrentMilestone;
            pendingAttack = new PendingAttack
            {
                direction = gameplayConfig.firstAttackDirection,
                jitterMultiplier = 1f,
                fairnessRoll = 1f,
                baseCandidateTime = gameplayConfig.initialSpawnDelay,
                speed = ApplyWarmupSpeed(milestone.normalizedProjectileSpeed),
                travelDuration = 1f / ApplyWarmupSpeed(milestone.normalizedProjectileSpeed),
                spawnTime = gameplayConfig.initialSpawnDelay
            };
            hasPendingAttack = true;
            attackDirector.RecordDirection(pendingAttack.direction);
        }

        private void SpawnPendingAttack()
        {
            PendingAttack attack = pendingAttack;
            hasPendingAttack = false;

            projectilePool.Acquire(
                attack.direction,
                arenaLayout.GetSpawnPosition(attack.direction),
                arenaLayout.GetShieldImpactPosition(attack.direction),
                arenaLayout.GetCoreImpactPosition(attack.direction),
                attack.travelDuration,
                arenaLayout.ProjectileWorldSize);

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
                jitterMultiplier = jitterMultiplier,
                fairnessRoll = Random.value,
                baseCandidateTime = runTime + interval,
                speed = speed,
                travelDuration = 1f / speed
            };
            hasPendingAttack = true;
            RecalculatePendingTiming(true);
        }

        private void RecalculatePendingTiming(bool preserveBaseCandidate)
        {
            DifficultyMilestone milestone = difficultyManager.CurrentMilestone;
            float speed = ApplyWarmupSpeed(milestone.normalizedProjectileSpeed);
            pendingAttack.speed = speed;
            pendingAttack.travelDuration = 1f / speed;

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
            if (gameManager != null)
            {
                gameManager.GameplayTick -= HandleGameplayTick;
            }

            if (difficultyManager != null)
            {
                difficultyManager.MilestoneChanged -= HandleMilestoneChanged;
            }
        }

        private struct PendingAttack
        {
            public AttackDirection direction;
            public float speed;
            public float travelDuration;
            public float jitterMultiplier;
            public float fairnessRoll;
            public float baseCandidateTime;
            public float spawnTime;
        }
    }
}
