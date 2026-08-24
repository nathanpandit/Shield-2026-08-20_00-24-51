using UnityEngine;

namespace ShieldGame
{
    public sealed class ProjectileController : MonoBehaviour
    {
        private const float OrangeTrailLifetime = 0.18f;

        [SerializeField] private SpriteRenderer projectileVisual;

        private TrailRenderer orangeSwitchTrail;
        private ProjectilePool ownerPool;
        private IGameplaySession gameSession;
        private Vector3 spawnPosition;
        private Vector3 shieldImpactPosition;
        private Vector3 coreImpactPosition;
        private Vector3 orangePathCenter;
        private Vector3 orangeFakeOrbitPosition;
        private Vector3 orangeTrueOrbitPosition;
        private float travelProgress;
        private float travelDuration;
        private float shieldContactProgress;
        private float orangeSwitchStartProgress;
        private float orangeSwitchEndProgress;
        private float orangeOrbitRadius;
        private float orangeRadialSpeed;
        private bool shieldContactResolved;
        private bool orangeSwitchStarted;
        private bool orangeSwitchCompleted;
        private bool resolved;

        public AttackDirection AttackDirection { get; private set; }
        public AttackDirection VisualSpawnDirection { get; private set; }
        public ProjectileType ProjectileType { get; private set; }
        public bool IsActive { get; private set; }
        public float TravelDuration => travelDuration;
        public float TravelProgress => travelProgress;
        public float ShieldContactProgress => shieldContactProgress;
        public float OrangeSwitchStartProgress => orangeSwitchStartProgress;
        public float OrangeSwitchEndProgress => orangeSwitchEndProgress;
        public float OrangeOrbitRadius => orangeOrbitRadius;
        public float OrangeSwitchDuration =>
            (orangeSwitchEndProgress - orangeSwitchStartProgress) * travelDuration;
        public float OrangeRadialSpeed => orangeRadialSpeed;
        public bool OrangeSwitchStarted => orangeSwitchStarted;
        public bool OrangeSwitchCompleted => orangeSwitchCompleted;
        public bool HasOrangeSwitchTrail => orangeSwitchTrail != null;
        public bool OrangeSwitchTrailEmitting => orangeSwitchTrail != null && orangeSwitchTrail.emitting;
        public Color VisualColor => projectileVisual != null ? projectileVisual.color : Color.clear;

        public void Configure(SpriteRenderer visual)
        {
            projectileVisual = visual;
        }

        public void Initialize(ProjectilePool pool, IGameplaySession session)
        {
            ownerPool = pool;
            gameSession = session;
        }

        public void Activate(
            ProjectileType projectileType,
            AttackDirection direction,
            Vector3 spawn,
            Vector3 shieldImpact,
            Vector3 coreImpact,
            float duration,
            float worldSize,
            Color color,
            Vector3 pathCenter,
            float requestedOrangeOrbitRadius,
            float requestedOrangeSwitchDuration)
        {
            ProjectileType = projectileType;
            AttackDirection = direction;
            VisualSpawnDirection = projectileType == ProjectileType.Orange
                ? AttackDirectionUtility.Opposite(direction)
                : direction;
            spawnPosition = spawn;
            shieldImpactPosition = shieldImpact;
            coreImpactPosition = coreImpact;
            travelDuration = Mathf.Max(0.001f, duration);
            travelProgress = 0f;
            shieldContactResolved = false;
            orangeSwitchStarted = false;
            orangeSwitchCompleted = false;
            resolved = false;
            IsActive = true;

            if (projectileType == ProjectileType.Orange)
            {
                ConfigureOrangePath(pathCenter, requestedOrangeOrbitRadius, requestedOrangeSwitchDuration, worldSize);
            }
            else
            {
                ConfigureStraightPath();
            }

            transform.position = spawnPosition;
            transform.localScale = new Vector3(worldSize, worldSize, 1f);
            if (projectileVisual != null)
            {
                projectileVisual.color = color;
            }

            ConfigureOrangeTrail(worldSize, color, projectileType == ProjectileType.Orange);

            gameObject.SetActive(true);
            gameSession.GameplayTick += HandleGameplayTick;
        }

        public void Deactivate()
        {
            if (gameSession != null)
            {
                gameSession.GameplayTick -= HandleGameplayTick;
            }

            IsActive = false;
            resolved = true;
            shieldContactResolved = false;
            orangeSwitchStarted = false;
            orangeSwitchCompleted = false;
            travelProgress = 0f;
            travelDuration = 0f;
            shieldContactProgress = 0f;
            orangeSwitchStartProgress = 0f;
            orangeSwitchEndProgress = 0f;
            orangeOrbitRadius = 0f;
            orangeRadialSpeed = 0f;
            if (orangeSwitchTrail != null)
            {
                orangeSwitchTrail.emitting = false;
                orangeSwitchTrail.Clear();
            }
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        public void ApplyLayout(
            Vector3 spawn,
            Vector3 shieldImpact,
            Vector3 coreImpact,
            float worldSize,
            Vector3 pathCenter,
            float requestedOrangeOrbitRadius,
            float requestedOrangeSwitchDuration)
        {
            if (!IsActive)
            {
                return;
            }

            spawnPosition = spawn;
            shieldImpactPosition = shieldImpact;
            coreImpactPosition = coreImpact;
            if (ProjectileType == ProjectileType.Orange)
            {
                ConfigureOrangePath(pathCenter, requestedOrangeOrbitRadius, requestedOrangeSwitchDuration, worldSize);
                transform.position = EvaluateOrangePosition(travelProgress);
                ConfigureOrangeTrail(worldSize, VisualColor, true);
                if (orangeSwitchTrail != null)
                {
                    orangeSwitchTrail.emitting = orangeSwitchStarted && !orangeSwitchCompleted;
                }
            }
            else
            {
                ConfigureStraightPath();
                transform.position = Vector3.LerpUnclamped(spawnPosition, coreImpactPosition, travelProgress);
            }

            transform.localScale = new Vector3(worldSize, worldSize, 1f);
        }

        private void HandleGameplayTick(float deltaTime)
        {
            if (!IsActive || resolved || gameSession == null || gameSession.State != GameState.Playing)
            {
                return;
            }

            float globalSpeedMultiplier = gameSession.ProjectileSpeedMultiplier;
            travelProgress = Mathf.Clamp01(travelProgress + deltaTime * globalSpeedMultiplier / travelDuration);
            if (ProjectileType == ProjectileType.Orange)
            {
                UpdateOrangeSwitchState();
                transform.position = EvaluateOrangePosition(travelProgress);
            }
            else
            {
                transform.position = Vector3.LerpUnclamped(spawnPosition, coreImpactPosition, travelProgress);
            }

            if (!shieldContactResolved && travelProgress >= shieldContactProgress)
            {
                shieldContactResolved = true;
                if (AttackDirection == gameSession.Shield.LogicalDirection)
                {
                    ResolveBlockAtShield();
                    return;
                }
            }

            if (travelProgress >= 1f)
            {
                resolved = true;
                ResolveMissAtCore();
            }
        }

        private void ConfigureStraightPath()
        {
            float fullPathDistance = Vector3.Distance(spawnPosition, coreImpactPosition);
            float shieldPathDistance = Vector3.Distance(spawnPosition, shieldImpactPosition);
            shieldContactProgress = fullPathDistance > Mathf.Epsilon
                ? Mathf.Clamp01(shieldPathDistance / fullPathDistance)
                : 0f;
            orangeSwitchStartProgress = 0f;
            orangeSwitchEndProgress = 0f;
            orangeOrbitRadius = 0f;
            orangeRadialSpeed = 0f;
        }

        private void ConfigureOrangePath(
            Vector3 pathCenter,
            float requestedOrbitRadius,
            float requestedSwitchDuration,
            float worldSize)
        {
            orangePathCenter = pathCenter;
            Vector3 fakeDirection = spawnPosition - orangePathCenter;
            Vector3 trueDirection = shieldImpactPosition - orangePathCenter;
            if (fakeDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                fakeDirection = -trueDirection;
            }

            if (trueDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                trueDirection = -fakeDirection;
            }

            fakeDirection.Normalize();
            trueDirection.Normalize();
            float trueShieldRadius = Vector3.Distance(orangePathCenter, shieldImpactPosition);
            orangeOrbitRadius = Mathf.Max(requestedOrbitRadius, trueShieldRadius + worldSize * 0.5f);
            orangeFakeOrbitPosition = orangePathCenter + fakeDirection * orangeOrbitRadius;
            orangeTrueOrbitPosition = orangePathCenter + trueDirection * orangeOrbitRadius;

            float effectiveSwitchDuration = Mathf.Clamp(
                requestedSwitchDuration,
                0.01f,
                Mathf.Max(0.01f, travelDuration - 0.001f));
            float radialTravelDuration = Mathf.Max(0.001f, travelDuration - effectiveSwitchDuration);
            float fakeApproachDistance = Vector3.Distance(spawnPosition, orangeFakeOrbitPosition);
            float trueApproachDistance = Vector3.Distance(orangeTrueOrbitPosition, coreImpactPosition);
            float totalRadialDistance = fakeApproachDistance + trueApproachDistance;
            float fakeApproachDuration = totalRadialDistance > Mathf.Epsilon
                ? radialTravelDuration * fakeApproachDistance / totalRadialDistance
                : radialTravelDuration * 0.5f;
            orangeSwitchStartProgress = Mathf.Clamp01(fakeApproachDuration / travelDuration);
            orangeSwitchEndProgress = Mathf.Clamp01(
                (fakeApproachDuration + effectiveSwitchDuration) / travelDuration);
            orangeRadialSpeed = totalRadialDistance / radialTravelDuration;

            float finalPathDistance = Vector3.Distance(orangeTrueOrbitPosition, coreImpactPosition);
            float finalShieldDistance = Vector3.Distance(orangeTrueOrbitPosition, shieldImpactPosition);
            float finalShieldFraction = finalPathDistance > Mathf.Epsilon
                ? Mathf.Clamp01(finalShieldDistance / finalPathDistance)
                : 0f;
            shieldContactProgress = Mathf.Lerp(orangeSwitchEndProgress, 1f, finalShieldFraction);
        }

        private Vector3 EvaluateOrangePosition(float progress)
        {
            if (progress < orangeSwitchStartProgress)
            {
                float approach = orangeSwitchStartProgress > Mathf.Epsilon
                    ? progress / orangeSwitchStartProgress
                    : 1f;
                return Vector3.LerpUnclamped(spawnPosition, orangeFakeOrbitPosition, approach);
            }

            if (progress < orangeSwitchEndProgress)
            {
                float switchSpan = orangeSwitchEndProgress - orangeSwitchStartProgress;
                float switchProgress = switchSpan > Mathf.Epsilon
                    ? (progress - orangeSwitchStartProgress) / switchSpan
                    : 1f;
                float easedProgress = Mathf.SmoothStep(0f, 1f, switchProgress);
                Vector3 fakeOffset = orangeFakeOrbitPosition - orangePathCenter;
                return orangePathCenter
                    + Quaternion.AngleAxis(-180f * easedProgress, Vector3.forward) * fakeOffset;
            }

            float finalSpan = 1f - orangeSwitchEndProgress;
            float finalApproach = finalSpan > Mathf.Epsilon
                ? (progress - orangeSwitchEndProgress) / finalSpan
                : 1f;
            return Vector3.LerpUnclamped(orangeTrueOrbitPosition, coreImpactPosition, finalApproach);
        }

        private void UpdateOrangeSwitchState()
        {
            if (!orangeSwitchStarted && travelProgress >= orangeSwitchStartProgress)
            {
                orangeSwitchStarted = true;
                if (orangeSwitchTrail != null)
                {
                    orangeSwitchTrail.emitting = true;
                }

                gameSession.PlayOrangeSwitchCue();
            }

            if (!orangeSwitchCompleted && travelProgress >= orangeSwitchEndProgress)
            {
                orangeSwitchCompleted = true;
                if (orangeSwitchTrail != null)
                {
                    orangeSwitchTrail.emitting = false;
                }
            }
        }

        private void ConfigureOrangeTrail(float worldSize, Color color, bool enabledForOrange)
        {
            if (!enabledForOrange)
            {
                if (orangeSwitchTrail != null)
                {
                    orangeSwitchTrail.emitting = false;
                    orangeSwitchTrail.Clear();
                }

                return;
            }

            if (orangeSwitchTrail == null)
            {
                orangeSwitchTrail = gameObject.AddComponent<TrailRenderer>();
                orangeSwitchTrail.textureMode = LineTextureMode.Stretch;
                orangeSwitchTrail.alignment = LineAlignment.View;
                orangeSwitchTrail.numCornerVertices = 3;
                orangeSwitchTrail.numCapVertices = 2;
            }

            orangeSwitchTrail.emitting = false;
            orangeSwitchTrail.Clear();
            orangeSwitchTrail.time = OrangeTrailLifetime;
            orangeSwitchTrail.minVertexDistance = Mathf.Max(0.005f, worldSize * 0.12f);
            orangeSwitchTrail.startWidth = worldSize * 0.72f;
            orangeSwitchTrail.endWidth = 0f;
            orangeSwitchTrail.startColor = new Color(color.r, color.g, color.b, 0.58f);
            orangeSwitchTrail.endColor = new Color(color.r, color.g, color.b, 0f);
            if (projectileVisual != null)
            {
                orangeSwitchTrail.sharedMaterial = projectileVisual.sharedMaterial;
                orangeSwitchTrail.sortingLayerID = projectileVisual.sortingLayerID;
                orangeSwitchTrail.sortingOrder = projectileVisual.sortingOrder - 1;
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
            gameSession.HandleProjectileBlocked(ProjectileType, position);
        }

        private void ResolveMissAtCore()
        {
            // Once the projectile passes a mismatched shield side, changing the shield
            // later cannot catch it. It continues visually to the core before the miss.
            Vector3 missPosition = coreImpactPosition;
            ownerPool.Release(this);
            gameSession.HandleProjectileMissed(ProjectileType, AttackDirection, missPosition);
        }

        private void OnDestroy()
        {
            if (gameSession != null)
            {
                gameSession.GameplayTick -= HandleGameplayTick;
            }
        }
    }
}
