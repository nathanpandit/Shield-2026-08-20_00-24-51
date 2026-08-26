using UnityEngine;

namespace ShieldGame
{
    /// <summary>
    /// DUO's White projectile is deliberately not part of either arena pool. It owns
    /// one cross-arena path, ignores the source arena, and resolves exclusively against
    /// the destination session.
    /// </summary>
    public sealed class DuoWhiteProjectileController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer projectileVisual;

        private DuoGameManager coordinator;
        private GameplayConfig gameplayConfig;
        private FeedbackConfig feedbackConfig;
        private DuoArenaSession sourceSession;
        private DuoArenaSession destinationSession;
        private ArenaLayout sourceLayout;
        private ArenaLayout destinationLayout;
        private Vector3 spawnPosition;
        private Vector3 sourceOrbitStart;
        private Vector3 sourceOrbitEnd;
        private Vector3 destinationShieldImpact;
        private Vector3 destinationCoreImpact;
        private float baseElapsed;
        private float curveStartTime;
        private float curveEndTime;
        private float handoffTime;
        private float shieldContactTime;
        private float totalDuration;
        private int sourceLayoutRevision;
        private int destinationLayoutRevision;
        private bool shieldContactResolved;
        private bool crossingCuePlayed;
        private bool resolved;

        public bool IsActive { get; private set; }
        public int SourceArenaIndex { get; private set; } = -1;
        public int DestinationArenaIndex { get; private set; } = -1;
        public AttackDirection DestinationDirection { get; private set; }
        public float BaseElapsed => baseElapsed;
        public float CurveStartTime => curveStartTime;
        public float CurveEndTime => curveEndTime;
        public float HandoffTime => handoffTime;
        public float ShieldContactTime => shieldContactTime;
        public float TotalDuration => totalDuration;
        public bool HasEnteredDestination => IsActive && baseElapsed >= handoffTime;
        public Color VisualColor => projectileVisual != null ? projectileVisual.color : Color.clear;

        public void Configure(SpriteRenderer visual)
        {
            projectileVisual = visual;
        }

        public void Initialize(
            DuoGameManager owner,
            GameplayConfig gameplay,
            FeedbackConfig feedback,
            DuoArenaSession[] sessions,
            DuoLayoutController layoutController)
        {
            coordinator = owner;
            gameplayConfig = gameplay;
            feedbackConfig = feedback;
            if (sessions != null && sessions.Length == 2 && layoutController != null)
            {
                sourceLayout = layoutController.GetArenaLayout(0);
                destinationLayout = layoutController.GetArenaLayout(1);
            }

            Deactivate();
        }

        public float CalculateTravelDuration(int sourceArenaIndex, float normalizedYellowSpeed)
        {
            if (!TryGetRoute(sourceArenaIndex, out ArenaLayout routeSource, out ArenaLayout routeDestination, out AttackDirection direction))
            {
                return 1f / Mathf.Max(0.01f, normalizedYellowSpeed);
            }

            CalculateRouteTiming(
                routeSource,
                routeDestination,
                direction,
                normalizedYellowSpeed,
                out _,
                out _,
                out _,
                out _,
                out _,
                out float duration);
            return duration;
        }

        public bool Activate(int sourceArenaIndex, float normalizedYellowSpeed)
        {
            if (IsActive || coordinator == null ||
                !TryGetRoute(sourceArenaIndex, out sourceLayout, out destinationLayout, out AttackDirection direction))
            {
                return false;
            }

            SourceArenaIndex = sourceArenaIndex;
            DestinationArenaIndex = 1 - sourceArenaIndex;
            DestinationDirection = direction;
            sourceSession = coordinator.Arenas[SourceArenaIndex];
            destinationSession = coordinator.Arenas[DestinationArenaIndex];
            baseElapsed = 0f;
            shieldContactResolved = false;
            crossingCuePlayed = false;
            resolved = false;
            IsActive = true;

            RebuildRoute(normalizedYellowSpeed, true);
            transform.position = spawnPosition;
            Color color = feedbackConfig != null
                ? feedbackConfig.GetProjectileColor(ProjectileType.White)
                : Color.white;
            if (projectileVisual != null)
            {
                projectileVisual.color = color;
            }

            gameObject.SetActive(true);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive || resolved || coordinator == null || coordinator.State != GameState.Playing)
            {
                return;
            }

            RefreshRouteGeometryIfNeeded();
            AdvanceTimeline(Mathf.Max(0f, deltaTime));

            if (!crossingCuePlayed && baseElapsed >= curveStartTime)
            {
                crossingCuePlayed = true;
                coordinator.PlayWhiteCrossing();
            }

            transform.position = EvaluatePosition(baseElapsed);
            if (!shieldContactResolved && baseElapsed >= shieldContactTime)
            {
                shieldContactResolved = true;
                if (destinationSession != null &&
                    DestinationDirection == destinationSession.Shield.LogicalDirection)
                {
                    ResolveBlock();
                    return;
                }
            }

            if (baseElapsed >= totalDuration)
            {
                ResolveMiss();
            }
        }

        public void Deactivate()
        {
            IsActive = false;
            resolved = true;
            shieldContactResolved = false;
            crossingCuePlayed = false;
            baseElapsed = 0f;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void AdvanceTimeline(float deltaTime)
        {
            float remainingRealTime = deltaTime;
            if (baseElapsed < handoffTime)
            {
                float sourcePhaseTime = Mathf.Min(remainingRealTime, handoffTime - baseElapsed);
                baseElapsed += sourcePhaseTime;
                remainingRealTime -= sourcePhaseTime;
            }

            if (remainingRealTime > 0f)
            {
                float destinationMultiplier = destinationSession != null
                    ? destinationSession.ProjectileSpeedMultiplier
                    : 1f;
                baseElapsed += remainingRealTime * Mathf.Max(0f, destinationMultiplier);
            }

            baseElapsed = Mathf.Min(baseElapsed, totalDuration);
        }

        private void RebuildRoute(float normalizedYellowSpeed, bool rebuildTiming)
        {
            if (sourceLayout == null || destinationLayout == null)
            {
                return;
            }

            CalculateRouteTiming(
                sourceLayout,
                destinationLayout,
                DestinationDirection,
                normalizedYellowSpeed,
                out spawnPosition,
                out sourceOrbitStart,
                out sourceOrbitEnd,
                out destinationShieldImpact,
                out destinationCoreImpact,
                out float calculatedDuration,
                out float calculatedCurveStart,
                out float calculatedCurveEnd,
                out float calculatedHandoff,
                out float calculatedShieldContact);

            if (rebuildTiming)
            {
                totalDuration = calculatedDuration;
                curveStartTime = calculatedCurveStart;
                curveEndTime = calculatedCurveEnd;
                handoffTime = calculatedHandoff;
                shieldContactTime = calculatedShieldContact;
            }

            float worldSize = Mathf.Min(sourceLayout.ProjectileWorldSize, destinationLayout.ProjectileWorldSize);
            transform.localScale = new Vector3(worldSize, worldSize, 1f);
            sourceLayoutRevision = sourceLayout.LayoutRevision;
            destinationLayoutRevision = destinationLayout.LayoutRevision;
        }

        private void RefreshRouteGeometryIfNeeded()
        {
            if (sourceLayout == null || destinationLayout == null ||
                (sourceLayoutRevision == sourceLayout.LayoutRevision &&
                 destinationLayoutRevision == destinationLayout.LayoutRevision))
            {
                return;
            }

            // Preserve the active projectile's time budget and progress during a resize.
            // Only its world-space route is reflowed, matching ordinary projectiles.
            RebuildRoute(1f, false);
        }

        private Vector3 EvaluatePosition(float elapsed)
        {
            if (elapsed < curveStartTime)
            {
                float approach = curveStartTime > Mathf.Epsilon ? elapsed / curveStartTime : 1f;
                return Vector3.LerpUnclamped(spawnPosition, sourceOrbitStart, approach);
            }

            if (elapsed < curveEndTime)
            {
                float span = curveEndTime - curveStartTime;
                float progress = span > Mathf.Epsilon ? (elapsed - curveStartTime) / span : 1f;
                float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                Vector3 startOffset = sourceOrbitStart - sourceLayout.Center;
                return sourceLayout.Center
                    + Quaternion.AngleAxis(-180f * easedProgress, Vector3.forward) * startOffset;
            }

            float finalSpan = totalDuration - curveEndTime;
            float finalProgress = finalSpan > Mathf.Epsilon ? (elapsed - curveEndTime) / finalSpan : 1f;
            return Vector3.LerpUnclamped(sourceOrbitEnd, destinationCoreImpact, finalProgress);
        }

        private void ResolveBlock()
        {
            resolved = true;
            Vector3 impactPosition = destinationShieldImpact;
            transform.position = impactPosition;
            coordinator.NotifyWhiteProjectileResolved(this);
            Deactivate();
            destinationSession?.HandleProjectileBlocked(ProjectileType.White, impactPosition);
        }

        private void ResolveMiss()
        {
            resolved = true;
            Vector3 impactPosition = destinationCoreImpact;
            transform.position = impactPosition;
            coordinator.NotifyWhiteProjectileResolved(this);
            Deactivate();
            destinationSession?.HandleProjectileMissed(ProjectileType.White, DestinationDirection, impactPosition);
        }

        private bool TryGetRoute(
            int sourceArenaIndex,
            out ArenaLayout routeSource,
            out ArenaLayout routeDestination,
            out AttackDirection direction)
        {
            routeSource = null;
            routeDestination = null;
            direction = AttackDirection.Left;
            if (coordinator == null || coordinator.Layout == null ||
                sourceArenaIndex < 0 || sourceArenaIndex > 1)
            {
                return false;
            }

            routeSource = coordinator.Layout.GetArenaLayout(sourceArenaIndex);
            routeDestination = coordinator.Layout.GetArenaLayout(1 - sourceArenaIndex);
            direction = coordinator.GetWhiteTravelDirection(sourceArenaIndex);
            return routeSource != null && routeDestination != null;
        }

        private void CalculateRouteTiming(
            ArenaLayout routeSource,
            ArenaLayout routeDestination,
            AttackDirection direction,
            float normalizedYellowSpeed,
            out Vector3 calculatedSpawn,
            out Vector3 calculatedOrbitStart,
            out Vector3 calculatedOrbitEnd,
            out Vector3 calculatedShieldImpact,
            out Vector3 calculatedCoreImpact,
            out float calculatedDuration)
        {
            CalculateRouteTiming(
                routeSource,
                routeDestination,
                direction,
                normalizedYellowSpeed,
                out calculatedSpawn,
                out calculatedOrbitStart,
                out calculatedOrbitEnd,
                out calculatedShieldImpact,
                out calculatedCoreImpact,
                out calculatedDuration,
                out _,
                out _,
                out _,
                out _);
        }

        private void CalculateRouteTiming(
            ArenaLayout routeSource,
            ArenaLayout routeDestination,
            AttackDirection direction,
            float normalizedYellowSpeed,
            out Vector3 calculatedSpawn,
            out Vector3 calculatedOrbitStart,
            out Vector3 calculatedOrbitEnd,
            out Vector3 calculatedShieldImpact,
            out Vector3 calculatedCoreImpact,
            out float calculatedDuration,
            out float calculatedCurveStart,
            out float calculatedCurveEnd,
            out float calculatedHandoff,
            out float calculatedShieldContact)
        {
            Vector3 sourceDirection = DirectionFromCore(direction);
            calculatedSpawn = routeSource.GetSpawnPosition(direction);
            calculatedOrbitStart = routeSource.Center + sourceDirection * routeSource.OrangeOrbitRadius;
            calculatedOrbitEnd = routeSource.Center - sourceDirection * routeSource.OrangeOrbitRadius;
            calculatedShieldImpact = routeDestination.GetShieldImpactPosition(direction);
            calculatedCoreImpact = routeDestination.GetCoreImpactPosition(direction);

            float yellowDuration = 1f / Mathf.Max(0.01f, normalizedYellowSpeed);
            float yellowReferenceDistance = Vector3.Distance(
                routeSource.GetSpawnPosition(direction),
                routeSource.GetCoreImpactPosition(direction));
            float yellowWorldSpeed = yellowReferenceDistance / Mathf.Max(0.001f, yellowDuration);
            float approachDuration = Vector3.Distance(calculatedSpawn, calculatedOrbitStart) / Mathf.Max(0.001f, yellowWorldSpeed);
            float curveDuration = gameplayConfig != null ? Mathf.Max(0.01f, gameplayConfig.duoWhiteCurveDuration) : 0.50f;
            float finalDuration = Vector3.Distance(calculatedOrbitEnd, calculatedCoreImpact) / Mathf.Max(0.001f, yellowWorldSpeed);

            calculatedCurveStart = approachDuration;
            calculatedCurveEnd = calculatedCurveStart + curveDuration;
            calculatedDuration = calculatedCurveEnd + finalDuration;

            Vector3 splitBoundary = (routeSource.Center + routeDestination.Center) * 0.5f;
            Vector3 finalVector = calculatedCoreImpact - calculatedOrbitEnd;
            float finalLengthSquared = finalVector.sqrMagnitude;
            float handoffFraction = finalLengthSquared > Mathf.Epsilon
                ? Mathf.Clamp01(Vector3.Dot(splitBoundary - calculatedOrbitEnd, finalVector) / finalLengthSquared)
                : 0f;
            Vector3 handoffPosition = Vector3.LerpUnclamped(calculatedOrbitEnd, calculatedCoreImpact, handoffFraction);
            calculatedHandoff = calculatedCurveEnd
                + Vector3.Distance(calculatedOrbitEnd, handoffPosition) / Mathf.Max(0.001f, yellowWorldSpeed);
            calculatedShieldContact = calculatedCurveEnd
                + Vector3.Distance(calculatedOrbitEnd, calculatedShieldImpact) / Mathf.Max(0.001f, yellowWorldSpeed);
            calculatedHandoff = Mathf.Clamp(calculatedHandoff, calculatedCurveEnd, calculatedDuration);
            calculatedShieldContact = Mathf.Clamp(calculatedShieldContact, calculatedHandoff, calculatedDuration);
        }

        private static Vector3 DirectionFromCore(AttackDirection direction)
        {
            switch (direction)
            {
                case AttackDirection.Top: return Vector3.up;
                case AttackDirection.Right: return Vector3.right;
                case AttackDirection.Bottom: return Vector3.down;
                case AttackDirection.Left: return Vector3.left;
                default: return Vector3.zero;
            }
        }
    }
}
