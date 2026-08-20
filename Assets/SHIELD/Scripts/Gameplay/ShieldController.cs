using DG.Tweening;
using UnityEngine;

namespace ShieldGame
{
    public sealed class ShieldController : MonoBehaviour
    {
        [SerializeField] private Transform visualPivot;
        [SerializeField] private LineRenderer arcVisual;
        [SerializeField] private GameplayConfig gameplayConfig;
        [SerializeField] private FeedbackConfig feedbackConfig;

        private Tween activeTween;
        private int pendingVisualClockwiseSteps;

        public AttackDirection LogicalDirection { get; private set; } = AttackDirection.Top;
        public int PendingVisualClockwiseSteps => pendingVisualClockwiseSteps;
        public bool IsVisualTweenRunning => activeTween != null && activeTween.IsActive();

        public void Configure(Transform pivot, LineRenderer arc, GameplayConfig gameplay, FeedbackConfig feedback)
        {
            visualPivot = pivot;
            arcVisual = arc;
            gameplayConfig = gameplay;
            feedbackConfig = feedback;
        }

        public void RotateClockwise()
        {
            LogicalDirection = AttackDirectionUtility.Clockwise(LogicalDirection);
            pendingVisualClockwiseSteps++;
            if (!IsVisualTweenRunning)
            {
                PlayNextVisualStep();
            }
        }

        public void ResetShield()
        {
            activeTween?.Kill(false);
            activeTween = null;
            pendingVisualClockwiseSteps = 0;
            LogicalDirection = AttackDirection.Top;
            if (visualPivot != null)
            {
                visualPivot.localRotation = Quaternion.identity;
            }
        }

        public void StopVisualQueue()
        {
            activeTween?.Kill(false);
            activeTween = null;
            pendingVisualClockwiseSteps = 0;
        }

        public void ApplyLayout(Vector3 center, float arenaSide)
        {
            transform.position = center;
            if (visualPivot != null)
            {
                visualPivot.position = center;
            }

            if (arcVisual == null || gameplayConfig == null)
            {
                return;
            }

            const int segmentCount = 24;
            arcVisual.positionCount = segmentCount;
            arcVisual.useWorldSpace = false;
            float radius = arenaSide * gameplayConfig.shieldRadiusNormalized;
            float halfArc = gameplayConfig.shieldArcDegrees * 0.5f;
            for (int i = 0; i < segmentCount; i++)
            {
                float t = i / (float)(segmentCount - 1);
                float angle = Mathf.Lerp(90f - halfArc, 90f + halfArc, t) * Mathf.Deg2Rad;
                arcVisual.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }

            float width = arenaSide * gameplayConfig.shieldThicknessNormalized;
            arcVisual.startWidth = width;
            arcVisual.endWidth = width;
            if (feedbackConfig != null)
            {
                arcVisual.startColor = feedbackConfig.shieldColor;
                arcVisual.endColor = feedbackConfig.shieldColor;
            }
        }

        private void PlayNextVisualStep()
        {
            if (pendingVisualClockwiseSteps <= 0 || visualPivot == null || gameplayConfig == null)
            {
                activeTween = null;
                return;
            }

            float targetZ = visualPivot.localEulerAngles.z - 90f;
            activeTween = visualPivot
                .DOLocalRotate(new Vector3(0f, 0f, targetZ), gameplayConfig.shieldRotationDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Normal)
                .OnComplete(CompleteVisualStep);
        }

        private void CompleteVisualStep()
        {
            activeTween = null;
            pendingVisualClockwiseSteps = Mathf.Max(0, pendingVisualClockwiseSteps - 1);
            if (pendingVisualClockwiseSteps > 0)
            {
                PlayNextVisualStep();
            }
        }

        private void OnDestroy()
        {
            activeTween?.Kill(false);
        }
    }
}
