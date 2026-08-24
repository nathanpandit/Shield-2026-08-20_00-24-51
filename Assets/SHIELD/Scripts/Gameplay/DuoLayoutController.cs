using UnityEngine;

namespace ShieldGame
{
    public sealed class DuoLayoutController : MonoBehaviour
    {
        [SerializeField] private ArenaLayout firstArena;
        [SerializeField] private ArenaLayout secondArena;

        public bool LandscapeSplit { get; private set; }
        public Rect FirstPixelRegion { get; private set; }
        public Rect SecondPixelRegion { get; private set; }

        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private bool initialized;

        public void Configure(ArenaLayout first, ArenaLayout second)
        {
            firstArena = first;
            secondArena = second;
        }

        public void InitializeLockedLayout()
        {
            LandscapeSplit = ShouldUseLandscapeSplit(Screen.width, Screen.height);
            initialized = true;
            ApplyLayout();
        }

        public static bool ShouldUseLandscapeSplit(float width, float height)
        {
            return width >= height;
        }

        public static void CalculateRegions(Rect safe, bool landscapeSplit, out Rect first, out Rect second)
        {
            if (landscapeSplit)
            {
                float halfWidth = safe.width * 0.5f;
                first = new Rect(safe.xMin, safe.yMin, halfWidth, safe.height);
                second = new Rect(safe.xMin + halfWidth, safe.yMin, safe.width - halfWidth, safe.height);
            }
            else
            {
                float halfHeight = safe.height * 0.5f;
                first = new Rect(safe.xMin, safe.yMin + halfHeight, safe.width, safe.height - halfHeight);
                second = new Rect(safe.xMin, safe.yMin, safe.width, halfHeight);
            }
        }

        public void RefreshLayoutIfNeeded(bool force = false)
        {
            if (!initialized)
            {
                InitializeLockedLayout();
                return;
            }

            if (force || lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            {
                ApplyLayout();
            }
        }

        public int GetArenaIndex(Vector2 screenPosition)
        {
            if (FirstPixelRegion.Contains(screenPosition)) return 0;
            if (SecondPixelRegion.Contains(screenPosition)) return 1;

            // Safe-area cutouts and exact split-edge pixels still route deterministically.
            return LandscapeSplit
                ? (screenPosition.x < Screen.width * 0.5f ? 0 : 1)
                : (screenPosition.y >= Screen.height * 0.5f ? 0 : 1);
        }

        private void ApplyLayout()
        {
            Rect safe = Screen.safeArea;
            CalculateRegions(safe, LandscapeSplit, out Rect first, out Rect second);
            FirstPixelRegion = first;
            SecondPixelRegion = second;

            firstArena.SetPixelRegion(FirstPixelRegion);
            secondArena.SetPixelRegion(SecondPixelRegion);
            lastSafeArea = safe;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
