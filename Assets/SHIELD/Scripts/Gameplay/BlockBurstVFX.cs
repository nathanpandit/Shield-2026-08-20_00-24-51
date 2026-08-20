using UnityEngine;

namespace ShieldGame
{
    public sealed class BlockBurstVFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem burstParticles;
        [SerializeField] private FeedbackConfig feedbackConfig;

        public void Configure(ParticleSystem particles, FeedbackConfig feedback)
        {
            burstParticles = particles;
            feedbackConfig = feedback;
        }

        public void PlayAt(Vector3 worldPosition)
        {
            Color color = feedbackConfig != null ? feedbackConfig.shieldColor : Color.magenta;
            float duration = feedbackConfig != null ? feedbackConfig.blockBurstDuration : 0.22f;
            float scale = feedbackConfig != null ? feedbackConfig.blockBurstScale : 0.20f;
            Emit(worldPosition, color, duration, scale, 10);
        }

        public void PlayCoreDeathAt(Vector3 worldPosition)
        {
            Color color = feedbackConfig != null ? feedbackConfig.coreColor : Color.cyan;
            float duration = feedbackConfig != null ? feedbackConfig.coreDeathDuration : 0.32f;
            float scale = feedbackConfig != null ? feedbackConfig.blockBurstScale * 1.6f : 0.32f;
            Emit(worldPosition, color, duration, scale, 18);
        }

        private void Emit(Vector3 worldPosition, Color color, float duration, float scale, int particleCount)
        {
            if (burstParticles == null)
            {
                return;
            }

            transform.position = worldPosition;
            ParticleSystem.MainModule main = burstParticles.main;
            main.startColor = color;
            main.startLifetime = duration;
            main.startSpeed = scale * 5f;
            main.startSize = scale;

            burstParticles.Emit(particleCount);
        }
    }
}
