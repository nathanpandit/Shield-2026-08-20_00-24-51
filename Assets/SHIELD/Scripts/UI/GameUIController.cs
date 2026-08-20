using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShieldGame
{
    public sealed class GameUIController : MonoBehaviour
    {
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
            scoreBaseScale = scoreText.rectTransform.localScale;
            scoreBaseColor = scoreText.color;

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

            HandleRunStarted();
            HandleScoreChanged(gameManager.Score.CurrentScore);
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
            Color highlight = gameManager.FeedbackConfig != null ? gameManager.FeedbackConfig.milestoneHighlightColor : Color.magenta;
            scoreText.rectTransform.DOKill();
            scoreText.DOKill();
            scoreText.rectTransform.localScale = scoreBaseScale;
            scoreText.color = scoreBaseColor;
            scoreText.rectTransform
                .DOScale(scoreBaseScale * 1.25f, duration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo);
            scoreText.DOColor(highlight, duration * 0.5f).SetLoops(2, LoopType.Yoyo);
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
        }
    }
}
