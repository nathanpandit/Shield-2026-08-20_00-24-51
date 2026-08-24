using System.Collections.Generic;
using UnityEngine;

namespace ShieldGame
{
    public sealed class ProjectilePool : MonoBehaviour
    {
        [SerializeField] private ProjectileController projectilePrefab;
        [SerializeField] private Transform projectileRoot;
        [SerializeField] private GameplayConfig gameplayConfig;
        [SerializeField] private FeedbackConfig feedbackConfig;

        private readonly Stack<ProjectileController> inactive = new Stack<ProjectileController>(16);
        private readonly List<ProjectileController> active = new List<ProjectileController>(16);
        private IGameplaySession gameSession;
        private bool initialized;

        public int ActiveCount => active.Count;

        public void Configure(ProjectileController prefab, Transform root, GameplayConfig gameplay, FeedbackConfig feedback)
        {
            projectilePrefab = prefab;
            projectileRoot = root;
            gameplayConfig = gameplay;
            feedbackConfig = feedback;
        }

        public void Initialize(IGameplaySession session)
        {
            gameSession = session;
            if (initialized)
            {
                return;
            }

            initialized = true;
            int count = gameplayConfig != null ? gameplayConfig.projectilePoolInitialSize : 12;
            for (int i = 0; i < count; i++)
            {
                inactive.Push(CreateProjectile());
            }
        }

        public ProjectileController Acquire(
            AttackDirection direction,
            Vector3 spawn,
            Vector3 shieldImpact,
            Vector3 coreImpact,
            float travelDuration,
            float worldSize)
        {
            return Acquire(ProjectileType.Yellow, direction, spawn, shieldImpact, coreImpact, travelDuration, worldSize);
        }

        public ProjectileController Acquire(
            ProjectileType projectileType,
            AttackDirection direction,
            Vector3 spawn,
            Vector3 shieldImpact,
            Vector3 coreImpact,
            float travelDuration,
            float worldSize)
        {
            return Acquire(
                projectileType,
                direction,
                spawn,
                shieldImpact,
                coreImpact,
                travelDuration,
                worldSize,
                Vector3.zero,
                0f);
        }

        public ProjectileController Acquire(
            ProjectileType projectileType,
            AttackDirection direction,
            Vector3 spawn,
            Vector3 shieldImpact,
            Vector3 coreImpact,
            float travelDuration,
            float worldSize,
            Vector3 pathCenter,
            float orangeOrbitRadius)
        {
            if (!initialized)
            {
                Initialize(GameManager.Instance);
            }

            ProjectileController projectile = inactive.Count > 0 ? inactive.Pop() : CreateProjectile();
            active.Add(projectile);
            Color color = feedbackConfig != null ? feedbackConfig.GetProjectileColor(projectileType) : Color.yellow;
            float orangeSwitchDuration = gameplayConfig != null ? gameplayConfig.orangeSwitchDuration : 0.50f;
            projectile.Activate(
                projectileType,
                direction,
                spawn,
                shieldImpact,
                coreImpact,
                travelDuration,
                worldSize,
                color,
                pathCenter,
                orangeOrbitRadius,
                orangeSwitchDuration);
            return projectile;
        }

        public void Release(ProjectileController projectile)
        {
            if (projectile == null || !active.Remove(projectile))
            {
                return;
            }

            projectile.Deactivate();
            inactive.Push(projectile);
        }

        public void ReturnAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ProjectileController projectile = active[i];
                active.RemoveAt(i);
                projectile.Deactivate();
                inactive.Push(projectile);
            }
        }

        public void ApplyLayoutToActive(ArenaLayout arenaLayout)
        {
            if (arenaLayout == null)
            {
                return;
            }

            float orangeSwitchDuration = gameplayConfig != null ? gameplayConfig.orangeSwitchDuration : 0.50f;
            for (int i = 0; i < active.Count; i++)
            {
                ProjectileController projectile = active[i];
                projectile.ApplyLayout(
                    arenaLayout.GetSpawnPosition(projectile.VisualSpawnDirection),
                    arenaLayout.GetShieldImpactPosition(projectile.AttackDirection),
                    arenaLayout.GetCoreImpactPosition(projectile.AttackDirection),
                    arenaLayout.ProjectileWorldSize,
                    arenaLayout.Center,
                    arenaLayout.OrangeOrbitRadius,
                    orangeSwitchDuration);
            }
        }

        private ProjectileController CreateProjectile()
        {
            ProjectileController projectile = Instantiate(projectilePrefab, projectileRoot != null ? projectileRoot : transform);
            projectile.name = "Projectile_Pooled";
            projectile.Initialize(this, gameSession);
            projectile.gameObject.SetActive(false);
            return projectile;
        }
    }
}
