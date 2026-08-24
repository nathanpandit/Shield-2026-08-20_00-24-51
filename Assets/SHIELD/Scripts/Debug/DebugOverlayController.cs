using System.Text;
using TMPro;
using UnityEngine;

namespace ShieldGame
{
    public sealed class DebugOverlayController : MonoBehaviour
    {
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private TMP_Text debugText;

        private readonly StringBuilder builder = new StringBuilder(768);
        private GameManager gameManager;

        public void Configure(GameObject root, TMP_Text text)
        {
            overlayRoot = root;
            debugText = text;
        }

        private void Start()
        {
            gameManager = GameManager.Instance;
            gameManager.GameplayTick += HandleGameplayTick;
            gameManager.StateChanged += HandleStateChanged;
            gameManager.DebugOverlayChanged += HandleOverlayChanged;
            HandleOverlayChanged(gameManager.DebugOverlayVisible);
        }

        private void HandleGameplayTick(float deltaTime)
        {
            if (overlayRoot.activeSelf)
            {
                Refresh();
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (overlayRoot.activeSelf)
            {
                Refresh();
            }
        }

        private void HandleOverlayChanged(bool visible)
        {
            overlayRoot.SetActive(visible && gameManager.DebugFeaturesAvailable);
            if (overlayRoot.activeSelf)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            DifficultyMilestone milestone = gameManager.Difficulty.CurrentMilestone;
            ProjectileSpawner spawner = gameManager.Spawner;
            builder.Clear();
            builder.Append("STATE: ").Append(gameManager.State).Append('\n');
            builder.Append("SCORE: ").Append(gameManager.Score.CurrentScore).Append('\n');
            builder.Append("MILESTONE: ").Append(milestone != null ? milestone.scoreThreshold : -1).Append('\n');
            builder.Append("NORM SPEED: ").Append(milestone != null ? milestone.normalizedProjectileSpeed.ToString("0.00") : "-").Append('\n');
            builder.Append("TRAVEL: ").Append(milestone != null ? (1f / milestone.normalizedProjectileSpeed).ToString("0.00") : "-").Append(" s\n");
            builder.Append("BASE INTERVAL: ").Append(milestone != null ? milestone.baseSpawnInterval.ToString("0.00") : "-").Append(" s\n");
            builder.Append("ACTUAL INTERVAL: ").Append(spawner.CurrentActualSpawnInterval.ToString("0.00")).Append(" s\n");
            builder.Append("HARD GAP: ").Append(milestone != null ? milestone.hardMinimumImpactGap.ToString("0.00") : "-").Append(" s\n");
            builder.Append("FAIRNESS: ").Append(milestone != null ? milestone.fairnessChance.ToString("P0") : "-").Append(" / applied ").Append(spawner.LastFairnessCushionApplied).Append('\n');
            builder.Append("ACTIVE: ").Append(gameManager.ProjectilePool.ActiveCount).Append('\n');
            builder.Append("SHIELD: ").Append(gameManager.Shield.LogicalDirection).Append('\n');
            builder.Append("REVERSED: ").Append(gameManager.PurpleReverseActive).Append('\n');
            builder.Append("VISUAL QUEUE: ").Append(gameManager.Shield.PendingVisualStepCount).Append('\n');
            builder.Append("LAST IMPACT DIR: ").Append(spawner.HasPreviousScheduledImpact ? spawner.LastScheduledImpactDirection.ToString() : "None").Append('\n');
            builder.Append("NEXT DIR: ").Append(spawner.PendingDirection.HasValue ? spawner.PendingDirection.Value.ToString() : "None").Append('\n');
            builder.Append("NEXT TYPE: ").Append(spawner.PendingProjectileType.HasValue ? spawner.PendingProjectileType.Value.ToString() : "None").Append('\n');
            builder.Append("NEXT SPAWN: ").Append(spawner.TimeUntilNextSpawn.ToString("0.00")).Append(" s\n");
            builder.Append("GREEN: ").Append(gameManager.GreenProtectionRemaining.ToString("0.0")).Append(" s\n");
            builder.Append("BLUE: ").Append(gameManager.BlueSlowRemaining.ToString("0.0")).Append(" s x").Append(gameManager.ProjectileSpeedMultiplier.ToString("0.00")).Append('\n');
            builder.Append("PURPLE: ").Append(gameManager.PurpleReverseRemaining.ToString("0.0")).Append(" s\n");
            builder.Append("INVINCIBLE: ").Append(gameManager.DebugInvincibility).Append('\n');
            builder.Append("LAST MISS: ").Append(gameManager.LastDebugMiss);
            debugText.text = builder.ToString();
        }

        private void OnDestroy()
        {
            if (gameManager == null)
            {
                return;
            }

            gameManager.GameplayTick -= HandleGameplayTick;
            gameManager.StateChanged -= HandleStateChanged;
            gameManager.DebugOverlayChanged -= HandleOverlayChanged;
        }
    }
}
