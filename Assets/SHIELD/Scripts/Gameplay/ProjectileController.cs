using UnityEngine;

namespace ShieldGame
{
    public sealed class ProjectileController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer projectileVisual;

        private ProjectilePool ownerPool;
        private GameManager gameManager;
        private Vector3 spawnPosition;
        private Vector3 shieldImpactPosition;
        private Vector3 coreImpactPosition;
        private float elapsedTravelTime;
        private float travelDuration;
        private float shieldContactTime;
        private bool shieldContactResolved;
        private bool resolved;

        public AttackDirection AttackDirection { get; private set; }
        public bool IsActive { get; private set; }
        public float TravelDuration => travelDuration;

        public void Configure(SpriteRenderer visual)
        {
            projectileVisual = visual;
        }

        public void Initialize(ProjectilePool pool, GameManager manager)
        {
            ownerPool = pool;
            gameManager = manager;
        }

        public void Activate(
            AttackDirection direction,
            Vector3 spawn,
            Vector3 shieldImpact,
            Vector3 coreImpact,
            float duration,
            float worldSize,
            Color color)
        {
            AttackDirection = direction;
            spawnPosition = spawn;
            shieldImpactPosition = shieldImpact;
            coreImpactPosition = coreImpact;
            travelDuration = Mathf.Max(0.001f, duration);
            float fullPathDistance = Vector3.Distance(spawnPosition, coreImpactPosition);
            float shieldPathDistance = Vector3.Distance(spawnPosition, shieldImpactPosition);
            float shieldContactProgress = fullPathDistance > Mathf.Epsilon
                ? Mathf.Clamp01(shieldPathDistance / fullPathDistance)
                : 0f;
            shieldContactTime = travelDuration * shieldContactProgress;
            elapsedTravelTime = 0f;
            shieldContactResolved = false;
            resolved = false;
            IsActive = true;

            transform.position = spawnPosition;
            transform.localScale = new Vector3(worldSize, worldSize, 1f);
            if (projectileVisual != null)
            {
                projectileVisual.color = color;
            }

            gameObject.SetActive(true);
            gameManager.GameplayTick += HandleGameplayTick;
        }

        public void Deactivate()
        {
            if (gameManager != null)
            {
                gameManager.GameplayTick -= HandleGameplayTick;
            }

            IsActive = false;
            resolved = true;
            shieldContactResolved = false;
            elapsedTravelTime = 0f;
            travelDuration = 0f;
            shieldContactTime = 0f;
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private void HandleGameplayTick(float deltaTime)
        {
            if (!IsActive || resolved || gameManager == null || gameManager.State != GameState.Playing)
            {
                return;
            }

            elapsedTravelTime += deltaTime;
            float progress = Mathf.Clamp01(elapsedTravelTime / travelDuration);
            transform.position = Vector3.LerpUnclamped(spawnPosition, coreImpactPosition, progress);

            if (!shieldContactResolved && elapsedTravelTime >= shieldContactTime)
            {
                shieldContactResolved = true;
                if (AttackDirection == gameManager.Shield.LogicalDirection)
                {
                    ResolveBlockAtShield();
                    return;
                }
            }

            if (elapsedTravelTime >= travelDuration)
            {
                resolved = true;
                ResolveMissAtCore();
            }
        }

        private void ResolveBlockAtShield()
        {
            // Shield contact is derived from deterministic elapsed time and logical
            // direction. Render geometry, transform overlap, and physics remain cosmetic.
            resolved = true;
            Vector3 position = shieldImpactPosition;
            transform.position = position;
            ownerPool.Release(this);
            gameManager.HandleProjectileBlocked(position);
        }

        private void ResolveMissAtCore()
        {
            // Once the projectile passes a mismatched shield side, changing the shield
            // later cannot catch it. It continues visually to the core before the miss.
            Vector3 missPosition = coreImpactPosition;
            ownerPool.Release(this);
            gameManager.HandleProjectileMissed(AttackDirection, missPosition);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.GameplayTick -= HandleGameplayTick;
            }
        }
    }
}
