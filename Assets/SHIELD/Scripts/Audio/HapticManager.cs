using UnityEngine;

namespace ShieldGame
{
    public sealed class HapticManager : MonoBehaviour
    {
        private IPersistenceService persistence;

        public bool HapticsEnabled => persistence == null || persistence.HapticsEnabled;

        public void Initialize(IPersistenceService persistenceService)
        {
            persistence = persistenceService;
        }

        public void SetHapticsEnabled(bool enabled)
        {
            if (persistence == null)
            {
                return;
            }

            persistence.HapticsEnabled = enabled;
            persistence.Save();
        }

        public void PlayDeathHaptic()
        {
            if (HapticsEnabled)
            {
                Handheld.Vibrate();
            }
        }
    }
}
