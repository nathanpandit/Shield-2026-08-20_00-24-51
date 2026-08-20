using System;
using UnityEngine;

namespace ShieldGame
{
    public sealed class ScoreManager : MonoBehaviour
    {
        public event Action<int> ScoreChanged;

        public int CurrentScore { get; private set; }
        public int BestScore => persistence != null ? persistence.BestScore : 0;
        public bool DebugScoreWasInjected { get; private set; }

        private IPersistenceService persistence;

        public void Initialize(IPersistenceService persistenceService)
        {
            persistence = persistenceService;
        }

        public void ResetRun()
        {
            CurrentScore = 0;
            DebugScoreWasInjected = false;
            ScoreChanged?.Invoke(CurrentScore);
        }

        public void Increment()
        {
            CurrentScore++;
            ScoreChanged?.Invoke(CurrentScore);
        }

        public void SetDebugScore(int score)
        {
            CurrentScore = Mathf.Max(0, score);
            DebugScoreWasInjected = true;
            ScoreChanged?.Invoke(CurrentScore);
        }

        public bool FinalizeRun()
        {
            if (persistence == null || DebugScoreWasInjected || CurrentScore <= persistence.BestScore)
            {
                return false;
            }

            persistence.BestScore = CurrentScore;
            persistence.Save();
            return true;
        }
    }
}
