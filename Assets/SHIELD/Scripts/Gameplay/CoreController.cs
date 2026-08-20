using DG.Tweening;
using UnityEngine;

namespace ShieldGame
{
    public sealed class CoreController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer coreVisual;

        private Sequence destructionSequence;
        private Vector3 layoutScale = Vector3.one;
        private Color layoutColor = Color.white;

        public Vector3 CenterPosition => transform.position;
        public bool IsDestructionPlaying => destructionSequence != null && destructionSequence.IsActive();
        public bool IsVisible => coreVisual != null && coreVisual.enabled && coreVisual.color.a > 0f;

        public void Configure(SpriteRenderer visual)
        {
            coreVisual = visual;
        }

        public void ApplyLayout(Vector3 center, float diameter, Color color)
        {
            transform.position = center;
            layoutScale = new Vector3(diameter, diameter, 1f);
            layoutColor = color;
            transform.localScale = layoutScale;
            if (coreVisual != null)
            {
                coreVisual.color = layoutColor;
            }
        }

        public void PlayDestroyed(float duration, float punchScale)
        {
            ResetVisual();
            float safeDuration = Mathf.Max(0.01f, duration);
            float punchDuration = safeDuration * 0.30f;
            float collapseDuration = safeDuration - punchDuration;

            destructionSequence = DOTween.Sequence()
                .SetUpdate(UpdateType.Normal)
                .Append(transform.DOScale(layoutScale * Mathf.Max(1f, punchScale), punchDuration).SetEase(Ease.OutBack))
                .Append(transform.DOScale(Vector3.zero, collapseDuration).SetEase(Ease.InBack));

            if (coreVisual != null)
            {
                destructionSequence.Join(coreVisual.DOFade(0f, collapseDuration));
            }

            destructionSequence.OnComplete(() =>
            {
                destructionSequence = null;
                if (coreVisual != null)
                {
                    coreVisual.enabled = false;
                }
            });
        }

        public void ResetVisual()
        {
            destructionSequence?.Kill(false);
            destructionSequence = null;
            transform.localScale = layoutScale;
            if (coreVisual != null)
            {
                coreVisual.enabled = true;
                coreVisual.color = layoutColor;
            }
        }

        private void OnDestroy()
        {
            destructionSequence?.Kill(false);
        }
    }
}
