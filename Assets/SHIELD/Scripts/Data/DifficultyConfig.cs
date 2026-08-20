using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShieldGame
{
    [Serializable]
    public sealed class DifficultyMilestone
    {
        [Min(0)] public int scoreThreshold;
        [Min(0.01f)] public float normalizedProjectileSpeed = 0.75f;
        [Min(0.01f)] public float baseSpawnInterval = 0.62f;
        [Min(0.01f)] public float hardMinimumImpactGap = 0.38f;
        [Range(0f, 1f)] public float fairnessChance = 0.85f;
        [Min(0f)] public float fairnessExtraGapPerClockwiseStep = 0.06f;
        [Range(0f, 1f)] public float patternFragmentChance;
    }

    [Serializable]
    public sealed class AttackPattern
    {
        public string name;
        public AttackDirection[] directions;

        public AttackPattern(string name, params AttackDirection[] directions)
        {
            this.name = name;
            this.directions = directions;
        }
    }

    [CreateAssetMenu(menuName = "SHIELD/Difficulty Config", fileName = "DifficultyConfig")]
    public sealed class DifficultyConfig : ScriptableObject
    {
        [SerializeField] private List<DifficultyMilestone> milestones = new List<DifficultyMilestone>();
        [SerializeField, Range(0f, 0.35f)] private float spawnIntervalJitterPercent = 0.08f;
        [SerializeField, Min(0)] private int warmupAttackCount = 5;
        [SerializeField, Range(0.1f, 1f)] private float warmupSpeedMultiplier = 0.80f;
        [SerializeField, Min(1f)] private float warmupSpawnIntervalMultiplier = 1.25f;
        [SerializeField, Range(2, 32)] private int recentDirectionHistoryLength = 8;
        [SerializeField] private bool historyQualityFilterEnabled = true;
        [SerializeField] private List<AttackPattern> authoredPatterns = new List<AttackPattern>();

        public IReadOnlyList<DifficultyMilestone> Milestones => milestones;
        public float SpawnIntervalJitterPercent => spawnIntervalJitterPercent;
        public int WarmupAttackCount => warmupAttackCount;
        public float WarmupSpeedMultiplier => warmupSpeedMultiplier;
        public float WarmupSpawnIntervalMultiplier => warmupSpawnIntervalMultiplier;
        public int RecentDirectionHistoryLength => recentDirectionHistoryLength;
        public bool HistoryQualityFilterEnabled => historyQualityFilterEnabled;
        public IReadOnlyList<AttackPattern> AuthoredPatterns => authoredPatterns;

        public void ApplyRecommendedDefaults()
        {
            milestones = new List<DifficultyMilestone>
            {
                Milestone(0, 0.75f, 0.62f, 0.38f, 0.85f, 0.060f, 0.00f),
                Milestone(10, 0.82f, 0.58f, 0.36f, 0.80f, 0.055f, 0.00f),
                Milestone(25, 0.90f, 0.54f, 0.34f, 0.75f, 0.050f, 0.00f),
                Milestone(50, 1.00f, 0.50f, 0.32f, 0.70f, 0.045f, 0.00f),
                Milestone(100, 1.12f, 0.46f, 0.30f, 0.65f, 0.040f, 0.02f),
                Milestone(200, 1.28f, 0.42f, 0.28f, 0.55f, 0.035f, 0.05f),
                Milestone(350, 1.45f, 0.39f, 0.26f, 0.45f, 0.030f, 0.08f),
                Milestone(500, 1.65f, 0.36f, 0.24f, 0.35f, 0.025f, 0.12f),
                Milestone(750, 1.85f, 0.34f, 0.22f, 0.25f, 0.020f, 0.15f),
                Milestone(1000, 2.00f, 0.32f, 0.20f, 0.20f, 0.015f, 0.20f)
            };

            authoredPatterns = new List<AttackPattern>
            {
                new AttackPattern("Clockwise Sweep", AttackDirection.Top, AttackDirection.Right, AttackDirection.Bottom, AttackDirection.Left),
                new AttackPattern("Counter Sweep", AttackDirection.Top, AttackDirection.Left, AttackDirection.Bottom, AttackDirection.Right),
                new AttackPattern("Opposite Alternation", AttackDirection.Top, AttackDirection.Bottom, AttackDirection.Right, AttackDirection.Left),
                new AttackPattern("Three-Step Turn Pressure", AttackDirection.Top, AttackDirection.Left, AttackDirection.Right)
            };
        }

        public DifficultyMilestone GetMilestoneForScore(int score)
        {
            if (milestones == null || milestones.Count == 0)
            {
                return null;
            }

            DifficultyMilestone result = milestones[0];
            for (int i = 0; i < milestones.Count; i++)
            {
                DifficultyMilestone candidate = milestones[i];
                if (candidate.scoreThreshold <= score && candidate.scoreThreshold >= result.scoreThreshold)
                {
                    result = candidate;
                }
            }

            return result;
        }

        public bool ValidateConfiguration(out string message)
        {
            if (milestones == null || milestones.Count == 0)
            {
                message = "DifficultyConfig needs at least one milestone.";
                return false;
            }

            var thresholds = new HashSet<int>();
            bool hasZero = false;
            for (int i = 0; i < milestones.Count; i++)
            {
                DifficultyMilestone milestone = milestones[i];
                hasZero |= milestone.scoreThreshold == 0;
                if (milestone.scoreThreshold < 0 || !thresholds.Add(milestone.scoreThreshold))
                {
                    message = "Milestone thresholds must be non-negative and unique.";
                    return false;
                }

                if (milestone.normalizedProjectileSpeed <= 0f || milestone.baseSpawnInterval <= 0f || milestone.hardMinimumImpactGap <= 0f)
                {
                    message = "Milestone speed, spawn interval, and hard impact gap must be greater than zero.";
                    return false;
                }
            }

            if (!hasZero)
            {
                message = "The first difficulty milestone must begin at score 0.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            spawnIntervalJitterPercent = Mathf.Max(0f, spawnIntervalJitterPercent);
            warmupAttackCount = Mathf.Max(0, warmupAttackCount);
            warmupSpeedMultiplier = Mathf.Max(0.1f, warmupSpeedMultiplier);
            warmupSpawnIntervalMultiplier = Mathf.Max(1f, warmupSpawnIntervalMultiplier);

            if (milestones == null)
            {
                return;
            }

            for (int i = 0; i < milestones.Count; i++)
            {
                DifficultyMilestone milestone = milestones[i];
                milestone.fairnessChance = Mathf.Clamp01(milestone.fairnessChance);
                milestone.patternFragmentChance = Mathf.Clamp01(milestone.patternFragmentChance);
            }
        }

        private static DifficultyMilestone Milestone(int threshold, float speed, float interval, float gap, float fairness, float extraGap, float pattern)
        {
            return new DifficultyMilestone
            {
                scoreThreshold = threshold,
                normalizedProjectileSpeed = speed,
                baseSpawnInterval = interval,
                hardMinimumImpactGap = gap,
                fairnessChance = fairness,
                fairnessExtraGapPerClockwiseStep = extraGap,
                patternFragmentChance = pattern
            };
        }
    }
}
