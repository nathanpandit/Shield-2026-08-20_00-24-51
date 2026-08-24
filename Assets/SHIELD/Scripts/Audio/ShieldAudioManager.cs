using UnityEngine;

namespace ShieldGame
{
    public sealed class ShieldAudioManager : MonoBehaviour
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        private IPersistenceService persistence;
        private FeedbackConfig feedbackConfig;

        public bool SoundEnabled => persistence == null || persistence.SoundEnabled;

        public void Configure(AudioSource music, AudioSource sfx)
        {
            musicSource = music;
            sfxSource = sfx;
        }

        public void Initialize(IPersistenceService persistenceService, FeedbackConfig feedback)
        {
            persistence = persistenceService;
            feedbackConfig = feedback;

            if (musicSource != null)
            {
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.volume = 0.35f;
                musicSource.clip = feedbackConfig != null ? feedbackConfig.musicClip : null;
                if (SoundEnabled && musicSource.clip != null)
                {
                    musicSource.Play();
                }
            }
        }

        public void PlayBlock() => PlayOneShot(feedbackConfig != null ? feedbackConfig.blockClip : null);
        public void PlayDeath() => PlayOneShot(feedbackConfig != null ? feedbackConfig.deathClip : null);
        public void PlayUi() => PlayOneShot(feedbackConfig != null ? feedbackConfig.uiClip : null);
        public void PlayShieldRotation() => PlayOneShot(feedbackConfig != null ? feedbackConfig.shieldRotationClip : null);
        public void PlayOrangeSwitch() => PlayOneShot(feedbackConfig != null ? feedbackConfig.orangeSwitchClip : null);

        public void SetSoundEnabled(bool enabled)
        {
            if (persistence == null)
            {
                return;
            }

            persistence.SoundEnabled = enabled;
            persistence.Save();
            if (musicSource == null)
            {
                return;
            }

            if (enabled && musicSource.clip != null) musicSource.Play();
            else musicSource.Stop();
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (SoundEnabled && clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip);
            }
        }
    }
}
