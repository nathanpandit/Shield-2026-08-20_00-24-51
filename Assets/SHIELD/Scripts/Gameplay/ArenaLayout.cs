using UnityEngine;

namespace ShieldGame
{
    public sealed class ArenaLayout : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private CoreController coreController;
        [SerializeField] private ShieldController shieldController;
        [SerializeField] private GameplayConfig gameplayConfig;
        [SerializeField] private FeedbackConfig feedbackConfig;

        private readonly Vector3[] spawnPositions = new Vector3[4];

        public Vector3 Center { get; private set; }
        public float ArenaSideWorld { get; private set; }
        public float ProjectileWorldSize => ArenaSideWorld * gameplayConfig.projectileSizeNormalized;

        public void Configure(Camera targetCamera, CoreController core, ShieldController shield, GameplayConfig gameplay, FeedbackConfig feedback)
        {
            gameplayCamera = targetCamera;
            coreController = core;
            shieldController = shield;
            gameplayConfig = gameplay;
            feedbackConfig = feedback;
        }

        private void Awake()
        {
            Recalculate();
        }

        public void Recalculate()
        {
            if (gameplayCamera == null || gameplayConfig == null)
            {
                return;
            }

            Rect safe = Screen.safeArea;
            float squarePixels = Mathf.Min(safe.width, safe.height);
            Vector2 pixelCenter = safe.center;
            float halfPixels = squarePixels * 0.5f;

            Center = ScreenToWorld(pixelCenter);
            Vector3 left = ScreenToWorld(new Vector2(pixelCenter.x - halfPixels, pixelCenter.y));
            Vector3 right = ScreenToWorld(new Vector2(pixelCenter.x + halfPixels, pixelCenter.y));
            Vector3 top = ScreenToWorld(new Vector2(pixelCenter.x, pixelCenter.y + halfPixels));
            Vector3 bottom = ScreenToWorld(new Vector2(pixelCenter.x, pixelCenter.y - halfPixels));

            ArenaSideWorld = Vector3.Distance(left, right);
            spawnPositions[(int)AttackDirection.Top] = top;
            spawnPositions[(int)AttackDirection.Right] = right;
            spawnPositions[(int)AttackDirection.Bottom] = bottom;
            spawnPositions[(int)AttackDirection.Left] = left;

            if (coreController != null)
            {
                Color coreColor = feedbackConfig != null ? feedbackConfig.coreColor : Color.cyan;
                coreController.ApplyLayout(Center, ArenaSideWorld * gameplayConfig.coreRadiusNormalized * 2f, coreColor);
            }

            shieldController?.ApplyLayout(Center, ArenaSideWorld);
        }

        public Vector3 GetSpawnPosition(AttackDirection direction)
        {
            return spawnPositions[(int)direction];
        }

        public Vector3 GetShieldImpactPosition(AttackDirection direction)
        {
            // Stop the projectile with its leading edge touching the outside edge of
            // the rendered shield rather than allowing its center to pass through it.
            float contactRadius = ArenaSideWorld * (
                gameplayConfig.shieldRadiusNormalized
                + gameplayConfig.shieldThicknessNormalized * 0.5f
                + gameplayConfig.projectileSizeNormalized * 0.5f);

            return Center + DirectionFromCore(direction) * contactRadius;
        }

        public Vector3 GetCoreImpactPosition(AttackDirection direction)
        {
            // Place the projectile center so its leading edge just touches the core.
            // This keeps both current placeholder art and future sprites out of the core.
            float contactRadius = ArenaSideWorld * (
                gameplayConfig.coreRadiusNormalized
                + gameplayConfig.projectileSizeNormalized * 0.5f);

            return Center + DirectionFromCore(direction) * contactRadius;
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

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            Vector3 world = gameplayCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -gameplayCamera.transform.position.z));
            world.z = 0f;
            return world;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                Recalculate();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || ArenaSideWorld <= 0f)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Center, new Vector3(ArenaSideWorld, ArenaSideWorld, 0f));
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                Gizmos.DrawSphere(spawnPositions[i], ArenaSideWorld * 0.01f);
                Gizmos.DrawLine(spawnPositions[i], Center);
            }
        }
#endif
    }
}
