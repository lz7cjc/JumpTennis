using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUI : MonoBehaviour
{
    [Header("Main UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI comboText;

    [Header("Game Over Panel")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI highScoreText;
    [SerializeField] private Button restartButton;

    [Header("Visual Effects")]
    [SerializeField] private Animator comboAnimator; // Optional for combo animations

    private ScoreManager scoreManager;

    private void Awake()
    {
        // Find ScoreManager
        scoreManager = FindObjectOfType<ScoreManager>();
        if (scoreManager == null)
        {
            Debug.LogError("GameUI: ScoreManager not found in scene!");
        }

        // Ensure game over panel is initially hidden
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        // Setup restart button
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
    }

    private void OnEnable()
    {
        // Subscribe to ScoreManager events
        ScoreManager.OnScoreChanged += UpdateScore;
        ScoreManager.OnLevelChanged += UpdateLevel;
        ScoreManager.OnLivesChanged += UpdateLives;
        ScoreManager.OnComboChanged += UpdateCombo;
        ScoreManager.OnGameOver += ShowGameOver;
        ScoreManager.OnLevelUp += OnLevelUp;
    }

    private void OnDisable()
    {
        // Unsubscribe from events to prevent errors
        ScoreManager.OnScoreChanged -= UpdateScore;
        ScoreManager.OnLevelChanged -= UpdateLevel;
        ScoreManager.OnLivesChanged -= UpdateLives;
        ScoreManager.OnComboChanged -= UpdateCombo;
        ScoreManager.OnGameOver -= ShowGameOver;
        ScoreManager.OnLevelUp -= OnLevelUp;
    }

    private void Start()
    {
        // Initialize UI with current values
        if (scoreManager != null)
        {
            UpdateScore(scoreManager.CurrentScore);
            UpdateLevel(scoreManager.CurrentLevel);
            UpdateLives(scoreManager.CurrentLives);
            UpdateCombo(scoreManager.CurrentCombo, scoreManager.ComboMultiplier);
        }
    }

    private void UpdateScore(int newScore)
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {newScore:N0}";
        }
    }

    private void UpdateLevel(int newLevel)
    {
        if (levelText != null)
        {
            levelText.text = $"Level: {newLevel}";
        }
    }

    private void UpdateLives(int newLives)
    {
        if (livesText != null)
        {
            livesText.text = $"Lives: {newLives}";

            // Change color based on lives remaining
            if (newLives <= 1)
            {
                livesText.color = Color.red; // Critical
            }
            else if (newLives <= 2)
            {
                livesText.color = Color.yellow; // Warning
            }
            else
            {
                livesText.color = Color.white; // Normal
            }
        }
    }

    private void UpdateCombo(int comboCount, float multiplier)
    {
        if (comboText != null)
        {
            if (comboCount > 0)
            {
                comboText.text = $"Combo: {comboCount} x{multiplier:F1}";
                comboText.gameObject.SetActive(true);

                // Trigger combo animation if available
                if (comboAnimator != null && comboCount > 1)
                {
                    comboAnimator.SetTrigger("ComboIncrease");
                }
            }
            else
            {
                comboText.text = "";
                comboText.gameObject.SetActive(false);
            }
        }
    }

    private void OnLevelUp()
    {
        // Show level up visual feedback
        if (levelText != null)
        {
            StartCoroutine(LevelUpEffect());
        }
    }

    private System.Collections.IEnumerator LevelUpEffect()
    {
        // Flash level text
        Color originalColor = levelText.color;
        Vector3 originalScale = levelText.transform.localScale;

        for (int i = 0; i < 3; i++)
        {
            levelText.color = Color.green;
            levelText.transform.localScale = originalScale * 1.2f;
            yield return new WaitForSeconds(0.2f);

            levelText.color = originalColor;
            levelText.transform.localScale = originalScale;
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            // Update final score display
            if (finalScoreText != null && scoreManager != null)
            {
                finalScoreText.text = $"Final Score: {scoreManager.CurrentScore:N0}";
            }

            // Update high score display
            if (highScoreText != null && scoreManager != null)
            {
                int highScore = scoreManager.GetHighScore();
                if (scoreManager.CurrentScore > highScore)
                {
                    highScoreText.text = "NEW HIGH SCORE!";
                    highScoreText.color = Color.yellow;
                }
                else
                {
                    highScoreText.text = $"High Score: {highScore:N0}";
                    highScoreText.color = Color.white;
                }
            }
        }
    }

    public void RestartGame()
    {
        if (scoreManager != null)
        {
            scoreManager.RestartGame();
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        Debug.Log("Game restarted via UI");
    }

    // Public method for testing
    [ContextMenu("Test Game Over")]
    public void TestGameOver()
    {
        ShowGameOver();
    }
}