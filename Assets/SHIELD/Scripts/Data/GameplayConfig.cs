using UnityEngine;

namespace ShieldGame
{
    public enum ProjectileType
    {
        Yellow = 0,
        Green = 1,
        Blue = 2,
        Purple = 3,
        Red = 4,
        Orange = 5
    }

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

        [Header("Projectile Types")]
        [Min(0)] public int specialProjectileWarmupCount = 8;
        [Min(0f)] public float yellowProjectileWeight = 0.92f;
        [Min(0f)] public float greenProjectileWeight = 0.02f;
        [Min(0f)] public float blueProjectileWeight = 0.02f;
        [Min(0f)] public float purpleProjectileWeight = 0.02f;
        [Min(0f)] public float redProjectileWeight = 0.01f;
        [Min(0f)] public float orangeProjectileWeight = 0.01f;
        [Min(0.01f)] public float orangeSwitchDuration = 0.50f;
        [Range(0.12f, 0.45f)] public float orangeSwitchRadiusNormalized = 0.25f;
        [Min(1f)] public float greenProjectileSpeedMultiplier = 2f;

        [Header("Projectile Effects")]
        [Min(0.01f)] public float greenProtectionDuration = 10f;
        [Min(0.01f)] public float blueSlowDuration = 1.5f;
        [Range(0.05f, 1f)] public float blueProjectileSpeedMultiplier = 0.5f;
        [Min(0.01f)] public float purpleReverseDuration = 5f;

        [Header("Run")]
        public AttackDirection firstAttackDirection = AttackDirection.Bottom;
        [Tooltip("Allows development-only controls in a non-development player build.")]
        public bool explicitDebugFeaturesEnabled;

        public float GetProjectileWeight(ProjectileType type)
        {
            switch (type)
            {
                case ProjectileType.Green: return greenProjectileWeight;
                case ProjectileType.Blue: return blueProjectileWeight;
                case ProjectileType.Purple: return purpleProjectileWeight;
                case ProjectileType.Red: return redProjectileWeight;
                case ProjectileType.Orange: return orangeProjectileWeight;
                default: return yellowProjectileWeight;
            }
        }

        public float GetProjectileSpeedMultiplier(ProjectileType type)
        {
            return type == ProjectileType.Green || type == ProjectileType.Blue
                ? Mathf.Max(1f, greenProjectileSpeedMultiplier)
                : 1f;
        }

        private void OnValidate()
        {
            specialProjectileWarmupCount = Mathf.Max(0, specialProjectileWarmupCount);
            yellowProjectileWeight = Mathf.Max(0f, yellowProjectileWeight);
            greenProjectileWeight = Mathf.Max(0f, greenProjectileWeight);
            blueProjectileWeight = Mathf.Max(0f, blueProjectileWeight);
            purpleProjectileWeight = Mathf.Max(0f, purpleProjectileWeight);
            redProjectileWeight = Mathf.Max(0f, redProjectileWeight);
            orangeProjectileWeight = Mathf.Max(0f, orangeProjectileWeight);
            orangeSwitchDuration = Mathf.Max(0.01f, orangeSwitchDuration);
            orangeSwitchRadiusNormalized = Mathf.Clamp(orangeSwitchRadiusNormalized, 0.12f, 0.45f);
            greenProjectileSpeedMultiplier = Mathf.Max(1f, greenProjectileSpeedMultiplier);
            greenProtectionDuration = Mathf.Max(0.01f, greenProtectionDuration);
            blueSlowDuration = Mathf.Max(0.01f, blueSlowDuration);
            blueProjectileSpeedMultiplier = Mathf.Clamp(blueProjectileSpeedMultiplier, 0.05f, 1f);
            purpleReverseDuration = Mathf.Max(0.01f, purpleReverseDuration);
        }
    }
}
