using UnityEngine;

namespace ShieldGame
{
    [CreateAssetMenu(menuName = "SHIELD/Gameplay Config", fileName = "GameplayConfig")]
    public sealed class GameplayConfig : ScriptableObject
    {
        [Header("Runtime")]
        [Min(30)] public int targetFrameRate = 60;
        [Min(0f)] public float initialSpawnDelay = 0.30f;
        [Min(0.01f)] public float shieldRotationDuration = 0.10f;
        [Min(1)] public int projectilePoolInitialSize = 12;

        [Header("Normalized Arena Visuals")]
        [Range(0.01f, 0.25f)] public float coreRadiusNormalized = 0.06f;
        [Range(0.01f, 0.30f)] public float shieldRadiusNormalized = 0.095f;
        [Range(20f, 160f)] public float shieldArcDegrees = 75f;
        [Range(0.002f, 0.05f)] public float shieldThicknessNormalized = 0.012f;
        [Range(0.005f, 0.10f)] public float projectileSizeNormalized = 0.026f;

        [Header("Run")]
        public AttackDirection firstAttackDirection = AttackDirection.Bottom;
        [Tooltip("Allows development-only controls in a non-development player build.")]
        public bool explicitDebugFeaturesEnabled;
    }
}
