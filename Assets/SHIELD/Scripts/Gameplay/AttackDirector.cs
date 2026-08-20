using System.Collections.Generic;
using UnityEngine;

namespace ShieldGame
{
    public sealed class AttackDirector : MonoBehaviour
    {
        [SerializeField] private DifficultyConfig config;

        private readonly List<AttackDirection> history = new List<AttackDirection>(16);
        private readonly Queue<AttackDirection> patternQueue = new Queue<AttackDirection>(8);

        public int PendingPatternSteps => patternQueue.Count;

        public void Configure(DifficultyConfig difficultyConfig)
        {
            config = difficultyConfig;
        }

        public void ResetRun()
        {
            history.Clear();
            patternQueue.Clear();
        }

        public AttackDirection GetNextDirection(float patternChance)
        {
            AttackDirection direction;
            if (patternQueue.Count > 0)
            {
                direction = patternQueue.Dequeue();
            }
            else if (TryBeginPattern(patternChance, out direction))
            {
            }
            else
            {
                direction = RollFilteredRandomDirection();
            }

            RecordDirection(direction);
            return direction;
        }

        public void RecordDirection(AttackDirection direction)
        {
            history.Add(direction);
            int maxCount = config != null ? Mathf.Max(2, config.RecentDirectionHistoryLength) : 8;
            while (history.Count > maxCount)
            {
                history.RemoveAt(0);
            }
        }

        private bool TryBeginPattern(float patternChance, out AttackDirection first)
        {
            first = default;
            if (config == null || config.AuthoredPatterns.Count == 0 || Random.value >= patternChance)
            {
                return false;
            }

            AttackPattern pattern = config.AuthoredPatterns[Random.Range(0, config.AuthoredPatterns.Count)];
            if (pattern == null || pattern.directions == null || pattern.directions.Length == 0)
            {
                return false;
            }

            int rotation = Random.Range(0, 4);
            for (int i = 0; i < pattern.directions.Length; i++)
            {
                patternQueue.Enqueue((AttackDirection)(((int)pattern.directions[i] + rotation) & 3));
            }

            first = patternQueue.Dequeue();
            return true;
        }

        private AttackDirection RollFilteredRandomDirection()
        {
            AttackDirection candidate = (AttackDirection)Random.Range(0, 4);
            if (config != null && config.HistoryQualityFilterEnabled && FormsExtremeAlternation(candidate))
            {
                // At most one reroll. The second result is always accepted, preserving all outcomes.
                candidate = (AttackDirection)Random.Range(0, 4);
            }

            return candidate;
        }

        private bool FormsExtremeAlternation(AttackDirection candidate)
        {
            if (history.Count < 7)
            {
                return false;
            }

            AttackDirection a = history[history.Count - 7];
            AttackDirection b = history[history.Count - 6];
            if (a == b)
            {
                return false;
            }

            for (int i = history.Count - 7; i < history.Count; i++)
            {
                AttackDirection expected = ((i - (history.Count - 7)) & 1) == 0 ? a : b;
                if (history[i] != expected)
                {
                    return false;
                }
            }

            return candidate == b;
        }
    }
}
