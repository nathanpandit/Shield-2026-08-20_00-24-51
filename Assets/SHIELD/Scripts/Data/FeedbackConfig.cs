using UnityEngine;

namespace ShieldGame
{
    [CreateAssetMenu(menuName = "SHIELD/Feedback Config", fileName = "FeedbackConfig")]
    public sealed class FeedbackConfig : ScriptableObject
    {
        [Header("Palette")]
        public Color backgroundColor = new Color(0.025f, 0.035f, 0.075f, 1f);
        public Color coreColor = new Color(0.35f, 1f, 0.90f, 1f);
        public Color shieldColor = new Color(0.95f, 0.33f, 0.78f, 1f);
        public Color projectileColor = new Color(1f, 0.88f, 0.30f, 1f);
        public Color scoreColor = new Color(0.88f, 0.97f, 1f, 1f);
        public Color milestoneHighlightColor = new Color(1f, 0.43f, 0.82f, 1f);

        [Header("Feedback")]
        [Min(0.01f)] public float blockBurstDuration = 0.22f;
        [Min(0.01f)] public float blockBurstScale = 0.20f;
        [Min(0.01f)] public float coreDeathDuration = 0.32f;
        [Min(1f)] public float coreDeathPunchScale = 1.18f;
        [Min(0.01f)] public float scoreMilestoneTweenDuration = 0.22f;

        [Header("Optional Audio")]
        public AudioClip blockClip;
        public AudioClip deathClip;
        public AudioClip uiClip;
        public AudioClip shieldRotationClip;
        public AudioClip musicClip;
    }
}
