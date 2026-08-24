using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShieldGame
{
    public sealed class DuoGameUIController : MonoBehaviour
    {
        private const float ScoreBoundsToCoreDiameter = 0.82f;
        private const float ScoreHeightToCoreDiameter = 0.60f;
        private const float ScoreMaxFontToCoreDiameter = 0.42f;

        [SerializeField] private RectTransform firstArenaPanel;
        [SerializeField] private RectTransform secondArenaPanel;
        [SerializeField] private TMP_Text firstScoreText;
        [SerializeField] private TMP_Text secondScoreText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseHomeButton;
        [SerializeField] private GameObject gameOverOverlay;
        [SerializeField] private TMP_Text firstFinalScoreText;
        [SerializeField] private TMP_Text secondFinalScoreText;
        [SerializeField] private TMP_Text totalScoreText;
        [SerializeField] private TMP_Text bestScoreText;
        [SerializeField] private TMP_Text newBestText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button gameOverHomeButton;

        private readonly TMP_Text[] scoreTexts = new TMP_Text[2];
        private readonly RectTransform[] arenaPanels = new RectTransform[2];
        private readonly RectTransform[] effectTimerRoots = new RectTransform[2];
        private readonly TMP_Text[,] effectTimerTexts = new TMP_Text[2, 3];
        private readonly Vector3[] scoreBaseScales = { Vector3.one, Vector3.one };
        private readonly Color[] scoreBaseColors = { Color.black, Color.black };
        private DuoGameManager gameManager;
        private bool applyingLayout;

        public void Configure(
            RectTransform firstPanel,
            RectTransform secondPanel,
            TMP_Text firstScore,
            TMP_Text secondScore,
            Button pause,
            GameObject pausePanel,
            Button resume,
            Button pauseHome,
            GameObject gameOverPanel,
            TMP_Text firstFinal,
            TMP_Text secondFinal,
            TMP_Text total,
            TMP_Text best,
            TMP_Text newBest,
            Button restart,
            Button gameOverHome)
        {
            firstArenaPanel = firstPanel;
            secondArenaPanel = secondPanel;
            firstScoreText = firstScore;
            secondScoreText = secondScore;
            pauseButton = pause;
            pauseOverlay = pausePanel;
            resumeButton = resume;
            pauseHomeButton = pauseHome;
            gameOverOverlay = gameOverPanel;
            firstFinalScoreText = firstFinal;
            secondFinalScoreText = secondFinal;
            totalScoreText = total;
            bestScoreText = best;
            newBestText = newBest;
            restartButton = restart;
            gameOverHomeButton = gameOverHome;
        }

        private void Start()
        {
            gameManager = DuoGameManager.Instance;
            arenaPanels[0] = firstArenaPanel;
            arenaPanels[1] = secondArenaPanel;
            scoreTexts[0] = firstScoreText;
            scoreTexts[1] = secondScoreText;
            ApplyArenaPanelLayout();
            for (int i = 0; i < 2; i++)
            {
                scoreBaseScales[i] = scoreTexts[i].rectTransform.localScale;
                scoreBaseColors[i] = scoreTexts[i].color;
                BuildEffectTimerHud(i);
            }

            pauseButton.onClick.AddListener(gameManager.PauseGame);
            resumeButton.onClick.AddListener(gameManager.ResumeGame);
            restartButton.onClick.AddListener(gameManager.BeginRun);
            pauseHomeButton.onClick.AddListener(gameManager.LoadHome);
            gameOverHomeButton.onClick.AddListener(gameManager.LoadHome);

            gameManager.Arenas[0].Score.ScoreChanged += HandleFirstScoreChanged;
            gameManager.Arenas[1].Score.ScoreChanged += HandleSecondScoreChanged;
            gameManager.Arenas[0].Difficulty.MilestoneChanged += HandleFirstMilestoneChanged;
            gameManager.Arenas[1].Difficulty.MilestoneChanged += HandleSecondMilestoneChanged;
            gameManager.Arenas[0].StatusEffectsChanged += HandleFirstEffectsChanged;
            gameManager.Arenas[1].StatusEffectsChanged += HandleSecondEffectsChanged;
            gameManager.RunStarted += HandleRunStarted;
            gameManager.PauseChanged += HandlePauseChanged;
            gameManager.GameOver += HandleGameOver;

            HandleRunStarted();
            HandleFirstScoreChanged(gameManager.Arenas[0].Score.CurrentScore);
            HandleSecondScoreChanged(gameManager.Arenas[1].Score.CurrentScore);
        }

        private void ApplyArenaPanelLayout()
        {
            if (applyingLayout || gameManager == null)
            {
                return;
            }

            applyingLayout = true;
            if (gameManager.Layout.LandscapeSplit)
            {
                SetPanelAnchors(firstArenaPanel, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
                SetPanelAnchors(secondArenaPanel, new Vector2(0.5f, 0f), new Vector2(1f, 1f));
            }
            else
            {
                SetPanelAnchors(firstArenaPanel, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
                SetPanelAnchors(secondArenaPanel, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
            }

            ApplyScoreLayout(0);
            ApplyScoreLayout(1);
            applyingLayout = false;
        }

        private static void SetPanelAnchors(RectTransform panel, Vector2 min, Vector2 max)
        {
            panel.anchorMin = min;
            panel.anchorMax = max;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }

        private void ApplyScoreLayout(int arenaIndex)
        {
            RectTransform panel = arenaPanels[arenaIndex];
            TMP_Text score = scoreTexts[arenaIndex];
            if (panel == null || score == null)
            {
                return;
            }

            RectTransform rect = score.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            float panelSide = Mathf.Min(panel.rect.width, panel.rect.height);
            if (panelSide <= 0f)
            {
                panelSide = 540f;
            }

            GameplayConfig gameplay = gameManager.Arenas[arenaIndex].GameplayConfig;
            float coreDiameter = panelSide * gameplay.coreRadiusNormalized * 2f;
            float maxFontSize = Mathf.Max(10f, coreDiameter * ScoreMaxFontToCoreDiameter);
            rect.sizeDelta = new Vector2(coreDiameter * ScoreBoundsToCoreDiameter, coreDiameter * ScoreHeightToCoreDiameter);
            score.enableAutoSizing = true;
            score.fontSizeMin = Mathf.Max(7f, maxFontSize * 0.48f);
            score.fontSizeMax = maxFontSize;
            score.fontSize = maxFontSize;
            score.color = Color.black;
            score.alignment = TextAlignmentOptions.Center;
            score.raycastTarget = false;
        }

        private void BuildEffectTimerHud(int arenaIndex)
        {
            var rootObject = new GameObject("EffectTimers", typeof(RectTransform));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(arenaPanels[arenaIndex], false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0.46f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(18f, -18f);
            root.sizeDelta = new Vector2(-18f, 84f);
            effectTimerRoots[arenaIndex] = root;

            FeedbackConfig feedback = gameManager.Arenas[arenaIndex].FeedbackConfig;
            effectTimerTexts[arenaIndex, 0] = CreateEffectTimerText(root, "GreenTimer", 0, feedback.greenProjectileColor);
            effectTimerTexts[arenaIndex, 1] = CreateEffectTimerText(root, "BlueTimer", 1, feedback.blueProjectileColor);
            effectTimerTexts[arenaIndex, 2] = CreateEffectTimerText(root, "PurpleTimer", 2, feedback.purpleProjectileColor);
        }

        private TMP_Text CreateEffectTimerText(RectTransform root, string objectName, int row, Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(0f, 26f);
            rect.anchoredPosition = new Vector2(0f, -row * 27f);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = firstScoreText.font;
            text.enableAutoSizing = true;
            text.fontSizeMin = 11f;
            text.fontSizeMax = 22f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }

        private void HandleRunStarted()
        {
            pauseOverlay.SetActive(false);
            gameOverOverlay.SetActive(false);
            pauseButton.gameObject.SetActive(true);
            for (int i = 0; i < 2; i++)
            {
                scoreTexts[i].rectTransform.DOKill();
                scoreTexts[i].DOKill();
                scoreTexts[i].rectTransform.localScale = scoreBaseScales[i];
                scoreTexts[i].color = scoreBaseColors[i];
                RefreshEffects(i);
            }
        }

        private void HandleFirstScoreChanged(int score) => firstScoreText.text = score.ToString();
        private void HandleSecondScoreChanged(int score) => secondScoreText.text = score.ToString();
        private void HandleFirstMilestoneChanged(DifficultyMilestone milestone) => AnimateMilestone(0, milestone);
        private void HandleSecondMilestoneChanged(DifficultyMilestone milestone) => AnimateMilestone(1, milestone);
        private void HandleFirstEffectsChanged() => RefreshEffects(0);
        private void HandleSecondEffectsChanged() => RefreshEffects(1);

        private void AnimateMilestone(int arenaIndex, DifficultyMilestone milestone)
        {
            DuoArenaSession arena = gameManager.Arenas[arenaIndex];
            if (milestone == null || milestone.scoreThreshold <= 0 || arena.Score.CurrentScore != milestone.scoreThreshold)
            {
                return;
            }

            TMP_Text score = scoreTexts[arenaIndex];
            float duration = arena.FeedbackConfig != null ? arena.FeedbackConfig.scoreMilestoneTweenDuration : 0.22f;
            score.rectTransform.DOKill();
            score.DOKill();
            score.rectTransform.localScale = scoreBaseScales[arenaIndex];
            score.color = scoreBaseColors[arenaIndex];
            score.rectTransform.DOScale(scoreBaseScales[arenaIndex] * 1.25f, duration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo);
        }

        private void RefreshEffects(int arenaIndex)
        {
            if (gameManager == null || effectTimerRoots[arenaIndex] == null)
            {
                return;
            }

            DuoArenaSession arena = gameManager.Arenas[arenaIndex];
            TMP_Text green = effectTimerTexts[arenaIndex, 0];
            TMP_Text blue = effectTimerTexts[arenaIndex, 1];
            TMP_Text purple = effectTimerTexts[arenaIndex, 2];
            green.gameObject.SetActive(arena.GreenProtectionActive);
            blue.gameObject.SetActive(arena.BlueSlowActive);
            purple.gameObject.SetActive(arena.PurpleReverseActive);
            if (arena.GreenProtectionActive) green.text = "IMMUNE  " + arena.GreenProtectionRemaining.ToString("0.0") + "s";
            if (arena.BlueSlowActive) blue.text = "SLOW  " + arena.BlueSlowRemaining.ToString("0.0") + "s";
            if (arena.PurpleReverseActive) purple.text = "REVERSE  " + arena.PurpleReverseRemaining.ToString("0.0") + "s";
            effectTimerRoots[arenaIndex].gameObject.SetActive(
                arena.GreenProtectionActive || arena.BlueSlowActive || arena.PurpleReverseActive);
        }

        private void HandlePauseChanged(bool paused)
        {
            pauseOverlay.SetActive(paused);
            pauseButton.gameObject.SetActive(!paused);
        }

        private void HandleGameOver(int first, int second, int total, int best, bool isNewBest)
        {
            pauseOverlay.SetActive(false);
            pauseButton.gameObject.SetActive(false);
            firstFinalScoreText.text = "P1: " + first;
            secondFinalScoreText.text = "P2: " + second;
            totalScoreText.text = "TOTAL: " + total;
            bestScoreText.text = "DUO BEST: " + best;
            newBestText.gameObject.SetActive(isNewBest);
            gameOverOverlay.SetActive(true);
            for (int i = 0; i < 2; i++)
            {
                effectTimerRoots[i].gameObject.SetActive(false);
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (Application.isPlaying && gameManager != null)
            {
                ApplyArenaPanelLayout();
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < 2; i++)
            {
                scoreTexts[i]?.rectTransform.DOKill();
                scoreTexts[i]?.DOKill();
            }

            if (gameManager == null)
            {
                return;
            }

            gameManager.Arenas[0].Score.ScoreChanged -= HandleFirstScoreChanged;
            gameManager.Arenas[1].Score.ScoreChanged -= HandleSecondScoreChanged;
            gameManager.Arenas[0].Difficulty.MilestoneChanged -= HandleFirstMilestoneChanged;
            gameManager.Arenas[1].Difficulty.MilestoneChanged -= HandleSecondMilestoneChanged;
            gameManager.Arenas[0].StatusEffectsChanged -= HandleFirstEffectsChanged;
            gameManager.Arenas[1].StatusEffectsChanged -= HandleSecondEffectsChanged;
            gameManager.RunStarted -= HandleRunStarted;
            gameManager.PauseChanged -= HandlePauseChanged;
            gameManager.GameOver -= HandleGameOver;
        }
    }
}
