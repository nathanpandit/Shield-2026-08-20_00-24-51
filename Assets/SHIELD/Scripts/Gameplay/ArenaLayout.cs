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
        private Rect lastSafeArea;
        private Rect lastCameraPixelRect;
        private Vector2Int lastScreenSize;
        private float lastCameraAspect;
        private float lastCameraOrthographicSize;
        private bool screenStateRecorded;
        private bool useCustomPixelRegion;
        private Rect customPixelRegion;
        private Rect lastCustomPixelRegion;

        public Vector3 Center { get; private set; }
        public float ArenaSideWorld { get; private set; }
        public int LayoutRevision { get; private set; }
        public float ProjectileWorldSize => ArenaSideWorld * gameplayConfig.projectileSizeNormalized;
        public float OrangeOrbitRadius => ArenaSideWorld * Mathf.Max(
            gameplayConfig.orangeSwitchRadiusNormalized,
            gameplayConfig.shieldRadiusNormalized
                + gameplayConfig.shieldThicknessNormalized * 0.5f
                + gameplayConfig.projectileSizeNormalized);

        public void Configure(Camera targetCamera, CoreController core, ShieldController shield, GameplayConfig gameplay, FeedbackConfig feedback)
        {
            gameplayCamera = targetCamera;
            coreController = core;
            shieldController = shield;
            gameplayConfig = gameplay;
            feedbackConfig = feedback;
        }

        public void SetPixelRegion(Rect pixelRegion)
        {
            useCustomPixelRegion = true;
            customPixelRegion = pixelRegion;
            Recalculate();
        }

        public void ClearPixelRegion()
        {
            useCustomPixelRegion = false;
            Recalculate();
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

            Rect safe = useCustomPixelRegion ? customPixelRegion : Screen.safeArea;
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
            RecordScreenState();
            LayoutRevision++;
        }

        public bool RefreshIfScreenChanged()
        {
            if (!ScreenGeometryChanged())
            {
                return false;
            }

            Recalculate();
            return true;
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

        private bool ScreenGeometryChanged()
        {
            if (!screenStateRecorded || gameplayCamera == null)
            {
                return true;
            }

            return lastScreenSize.x != Screen.width ||
                   lastScreenSize.y != Screen.height ||
                   lastSafeArea != Screen.safeArea ||
                   (useCustomPixelRegion && lastCustomPixelRegion != customPixelRegion) ||
                   lastCameraPixelRect != gameplayCamera.pixelRect ||
                   !Mathf.Approximately(lastCameraAspect, gameplayCamera.aspect) ||
                   !Mathf.Approximately(lastCameraOrthographicSize, gameplayCamera.orthographicSize);
        }

        private void RecordScreenState()
        {
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            lastCustomPixelRegion = customPixelRegion;
            lastCameraPixelRect = gameplayCamera.pixelRect;
            lastCameraAspect = gameplayCamera.aspect;
            lastCameraOrthographicSize = gameplayCamera.orthographicSize;
            screenStateRecorded = true;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                RefreshIfScreenChanged();
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
