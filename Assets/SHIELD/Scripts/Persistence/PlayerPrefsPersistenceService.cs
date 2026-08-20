using UnityEngine;

namespace ShieldGame
{
    public sealed class PlayerPrefsPersistenceService : IPersistenceService
    {
        private const string BestScoreKey = "SHIELD_v1_BestScore";
        private const string SoundEnabledKey = "SHIELD_v1_SoundEnabled";
        private const string HapticsEnabledKey = "SHIELD_v1_HapticsEnabled";
        private const string TutorialCompletedKey = "SHIELD_v1_TutorialCompleted";

        public int BestScore
        {
            get => PlayerPrefs.GetInt(BestScoreKey, 0);
            set => PlayerPrefs.SetInt(BestScoreKey, Mathf.Max(0, value));
        }

        public bool SoundEnabled
        {
            get => PlayerPrefs.GetInt(SoundEnabledKey, 1) != 0;
            set => PlayerPrefs.SetInt(SoundEnabledKey, value ? 1 : 0);
        }

        public bool HapticsEnabled
        {
            get => PlayerPrefs.GetInt(HapticsEnabledKey, 1) != 0;
            set => PlayerPrefs.SetInt(HapticsEnabledKey, value ? 1 : 0);
        }

        public bool TutorialCompleted
        {
            get => PlayerPrefs.GetInt(TutorialCompletedKey, 0) != 0;
            set => PlayerPrefs.SetInt(TutorialCompletedKey, value ? 1 : 0);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
