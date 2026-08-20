using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShieldGame
{
    public sealed class HomeUIController : MonoBehaviour
    {
        [SerializeField] private TMP_Text bestScoreText;
        [SerializeField] private Button playButton;
        [SerializeField] private Button soundButton;
        [SerializeField] private TMP_Text soundButtonText;
        [SerializeField] private Button hapticButton;
        [SerializeField] private TMP_Text hapticButtonText;

        private IPersistenceService persistence;

        public void Configure(TMP_Text best, Button play, Button sound, TMP_Text soundLabel, Button haptic, TMP_Text hapticLabel)
        {
            bestScoreText = best;
            playButton = play;
            soundButton = sound;
            soundButtonText = soundLabel;
            hapticButton = haptic;
            hapticButtonText = hapticLabel;
        }

        private void Awake()
        {
            persistence = new PlayerPrefsPersistenceService();
            playButton.onClick.AddListener(Play);
            soundButton.onClick.AddListener(ToggleSound);
            hapticButton.onClick.AddListener(ToggleHaptics);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Play()
        {
            SceneManager.LoadScene("Game");
        }

        private void ToggleSound()
        {
            persistence.SoundEnabled = !persistence.SoundEnabled;
            persistence.Save();
            Refresh();
        }

        private void ToggleHaptics()
        {
            persistence.HapticsEnabled = !persistence.HapticsEnabled;
            persistence.Save();
            Refresh();
        }

        private void Refresh()
        {
            if (persistence == null)
            {
                persistence = new PlayerPrefsPersistenceService();
            }

            bestScoreText.text = "BEST: " + persistence.BestScore;
            soundButtonText.text = persistence.SoundEnabled ? "SOUND: ON" : "SOUND: OFF";
            hapticButtonText.text = persistence.HapticsEnabled ? "HAPTIC: ON" : "HAPTIC: OFF";
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(Play);
            if (soundButton != null) soundButton.onClick.RemoveListener(ToggleSound);
            if (hapticButton != null) hapticButton.onClick.RemoveListener(ToggleHaptics);
        }
    }
}
