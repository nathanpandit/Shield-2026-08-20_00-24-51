using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShieldGame
{
    public sealed class GameUIController : MonoBehaviour
    {
        private const float ScoreBoundsToCoreDiameter = 0.82f;
        private const float ScoreHeightToCoreDiameter = 0.60f;
        private const float ScoreMaxFontToCoreDiameter = 0.32f;

        [Header("HUD")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private Button pauseButton;

        [Header("Pause")]
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseHomeButton;

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverOverlay;
        [SerializeField] private TMP_Text finalScoreText;
        [SerializeField] private TMP_Text bestScoreText;
        [SerializeField] private TMP_Text newBestText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button gameOverHomeButton;

        private GameManager gameManager;
        private Vector3 scoreBaseScale = Vector3.one;
        private Color scoreBaseColor = Color.white;
        private RectTransform effectTimerRoot;
        private TMP_Text greenTimerText;
        private TMP_Text blueTimerText;
        private TMP_Text purpleTimerText;
        private bool applyingScoreLayout;

        public void Configure(
            TMP_Text activeScore,
            Button pause,
            GameObject pausePanel,
            Button resume,
            Button pauseHome,
            GameObject gameOverPanel,
            TMP_Text finalScore,
            TMP_Text bestScore,
            TMP_Text newBest,
            Button restart,
            Button gameOverHome)
        {
            scoreText = activeScore;
            pauseButton = pause;
            pauseOverlay = pausePanel;
            resumeButton = resume;
            pauseHomeButton = pauseHome;
            gameOverOverlay = gameOverPanel;
            finalScoreText = finalScore;
            bestScoreText = bestScore;
            newBestText = newBest;
            restartButton = restart;
            gameOverHomeButton = gameOverHome;
        }

        private void Start()
        {
            gameManager = GameManager.Instance;
            ApplyCenteredScoreLayout();
            scoreBaseScale = scoreText.rectTransform.localScale;
            scoreBaseColor = scoreText.color;
            BuildEffectTimerHud();

            pauseButton.onClick.AddListener(gameManager.PauseGame);
            resumeButton.onClick.AddListener(gameManager.ResumeGame);
            restartButton.onClick.AddListener(gameManager.BeginRun);
            pauseHomeButton.onClick.AddListener(gameManager.LoadHome);
            gameOverHomeButton.onClick.AddListener(gameManager.LoadHome);

            gameManager.Score.ScoreChanged += HandleScoreChanged;
            gameManager.Difficulty.MilestoneChanged += HandleMilestoneChanged;
            gameManager.RunStarted += HandleRunStarted;
            gameManager.PauseChanged += HandlePauseChanged;
            gameManager.GameOver += HandleGameOver;
            gameManager.StatusEffectsChanged += HandleStatusEffectsChanged;

            HandleRunStarted();
            HandleScoreChanged(gameManager.Score.CurrentScore);
        }

        private void ApplyCenteredScoreLayout()
        {
            if (scoreText == null || applyingScoreLayout)
            {
                return;
            }

            applyingScoreLayout = true;
            RectTransform rect = scoreText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            float coreDiameter = 130f;
            RectTransform safeArea = rect.parent as RectTransform;
            GameplayConfig gameplay = gameManager != null ? gameManager.GameplayConfig : null;
            if (safeArea != null && gameplay != null)
            {
                float safeSquare = Mathf.Min(safeArea.rect.width, safeArea.rect.height);
                if (safeSquare > 0f)
                {
                    coreDiameter = safeSquare * gameplay.coreRadiusNormalized * 2f;
                }
            }

            float maxFontSize = Mathf.Max(12f, coreDiameter * ScoreMaxFontToCoreDiameter);
            rect.sizeDelta = new Vector2(
                coreDiameter * ScoreBoundsToCoreDiameter,
                coreDiameter * ScoreHeightToCoreDiameter);
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = Mathf.Min(maxFontSize, Mathf.Max(8f, maxFontSize * 0.55f));
            scoreText.fontSizeMax = maxFontSize;
            scoreText.fontSize = maxFontSize;
            scoreText.color = Color.black;
            scoreText.alignment = TextAlignmentOptions.Center;
            scoreText.raycastTarget = false;
            applyingScoreLayout = false;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (Application.isPlaying && gameManager != null)
            {
                ApplyCenteredScoreLayout();
            }
        }

        private void HandleRunStarted()
        {
            pauseOverlay.SetActive(false);
            gameOverOverlay.SetActive(false);
            pauseButton.gameObject.SetActive(true);
            scoreText.rectTransform.DOKill();
            scoreText.DOKill();
            scoreText.rectTransform.localScale = scoreBaseScale;
            scoreText.color = scoreBaseColor;
            HandleStatusEffectsChanged();
        }

        private void HandleScoreChanged(int score)
        {
            scoreText.text = score.ToString();
        }

        private void HandleMilestoneChanged(DifficultyMilestone milestone)
        {
            if (milestone == null || milestone.scoreThreshold <= 0 || gameManager == null || gameManager.Score.CurrentScore != milestone.scoreThreshold)
            {
                return;
            }

            float duration = gameManager.FeedbackConfig != null ? gameManager.FeedbackConfig.scoreMilestoneTweenDuration : 0.22f;
            scoreText.rectTransform.DOKill();
            scoreText.DOKill();
            scoreText.rectTransform.localScale = scoreBaseScale;
            scoreText.color = scoreBaseColor;
            scoreText.rectTransform
                .DOScale(scoreBaseScale * 1.25f, duration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo);
        }

        private void HandlePauseChanged(bool paused)
        {
            pauseOverlay.SetActive(paused);
            pauseButton.gameObject.SetActive(!paused);
        }

        private void HandleGameOver(int score, int best, bool isNewBest)
        {
            pauseOverlay.SetActive(false);
            pauseButton.gameObject.SetActive(false);
            finalScoreText.text = "SCORE: " + score;
            bestScoreText.text = "BEST: " + best;
            newBestText.gameObject.SetActive(isNewBest);
            gameOverOverlay.SetActive(true);
            if (effectTimerRoot != null)
            {
                effectTimerRoot.gameObject.SetActive(false);
            }
        }

        private void BuildEffectTimerHud()
        {
            if (scoreText == null || effectTimerRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("EffectTimers", typeof(RectTransform));
            effectTimerRoot = rootObject.GetComponent<RectTransform>();
            effectTimerRoot.SetParent(scoreText.rectTransform.parent, false);
            effectTimerRoot.anchorMin = new Vector2(0.5f, 1f);
            effectTimerRoot.anchorMax = new Vector2(0.5f, 1f);
            effectTimerRoot.pivot = new Vector2(0.5f, 1f);
            effectTimerRoot.sizeDelta = new Vector2(250f, 96f);
            effectTimerRoot.anchoredPosition = new Vector2(-350f, -42f);
            effectTimerRoot.SetSiblingIndex(scoreText.transform.GetSiblingIndex() + 1);

            FeedbackConfig feedback = gameManager != null ? gameManager.FeedbackConfig : null;
            greenTimerText = CreateEffectTimerText(
                "GreenTimer",
                0,
                feedback != null ? feedback.greenProjectileColor : Color.green);
            blueTimerText = CreateEffectTimerText(
                "BlueTimer",
                1,
                feedback != null ? feedback.blueProjectileColor : Color.blue);
            purpleTimerText = CreateEffectTimerText(
                "PurpleTimer",
                2,
                feedback != null ? feedback.purpleProjectileColor : Color.magenta);
        }

        private TMP_Text CreateEffectTimerText(string objectName, int row, Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(effectTimerRoot, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(0f, 30f);
            rect.anchoredPosition = new Vector2(0f, -row * 31f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = scoreText.font;
            text.fontSize = 26f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }

        private void HandleStatusEffectsChanged()
        {
            if (gameManager == null || effectTimerRoot == null)
            {
                return;
            }

            bool greenActive = gameManager.GreenProtectionActive;
            bool blueActive = gameManager.BlueSlowActive;
            bool purpleActive = gameManager.PurpleReverseActive;
            greenTimerText.gameObject.SetActive(greenActive);
            blueTimerText.gameObject.SetActive(blueActive);
            purpleTimerText.gameObject.SetActive(purpleActive);

            if (greenActive)
            {
                greenTimerText.text = "IMMUNE  " + gameManager.GreenProtectionRemaining.ToString("0.0") + "s";
            }

            if (blueActive)
            {
                blueTimerText.text = "SLOW  " + gameManager.BlueSlowRemaining.ToString("0.0") + "s";
            }

            if (purpleActive)
            {
                purpleTimerText.text = "REVERSE  " + gameManager.PurpleReverseRemaining.ToString("0.0") + "s";
            }

            effectTimerRoot.gameObject.SetActive(greenActive || blueActive || purpleActive);
        }

        private void OnDestroy()
        {
            scoreText?.rectTransform.DOKill();
            scoreText?.DOKill();
            if (gameManager == null)
            {
                return;
            }

            gameManager.Score.ScoreChanged -= HandleScoreChanged;
            gameManager.Difficulty.MilestoneChanged -= HandleMilestoneChanged;
            gameManager.RunStarted -= HandleRunStarted;
            gameManager.PauseChanged -= HandlePauseChanged;
            gameManager.GameOver -= HandleGameOver;
            gameManager.StatusEffectsChanged -= HandleStatusEffectsChanged;
        }
    }
}
