using System;
using UnityEngine;

namespace ShieldGame
{
    public sealed class DifficultyManager : MonoBehaviour
    {
        [SerializeField] private DifficultyConfig config;

        public event Action<DifficultyMilestone> MilestoneChanged;

        public DifficultyMilestone CurrentMilestone { get; private set; }
        public DifficultyConfig Config => config;

        private ScoreManager scoreManager;

        public void Configure(DifficultyConfig difficultyConfig)
        {
            config = difficultyConfig;
        }

        public void Initialize(ScoreManager score)
        {
            if (scoreManager != null)
            {
                scoreManager.ScoreChanged -= ApplyScore;
            }

            scoreManager = score;
            scoreManager.ScoreChanged += ApplyScore;
            ResetRun();
        }

        public void ResetRun()
        {
            ApplyScore(0, true);
        }

        public void ApplyScore(int score)
        {
            ApplyScore(score, false);
        }

        private void ApplyScore(int score, bool force)
        {
            if (config == null)
            {
                Debug.LogError("SHIELD DifficultyManager has no DifficultyConfig.", this);
                return;
            }

            DifficultyMilestone next = config.GetMilestoneForScore(score);
            if (next == null)
            {
                Debug.LogError("SHIELD DifficultyConfig contains no usable milestone.", config);
                return;
            }

            if (force || !ReferenceEquals(CurrentMilestone, next))
            {
                CurrentMilestone = next;
                MilestoneChanged?.Invoke(CurrentMilestone);
            }
        }

        private void OnDestroy()
        {
            if (scoreManager != null)
            {
                scoreManager.ScoreChanged -= ApplyScore;
            }
        }
    }
}
