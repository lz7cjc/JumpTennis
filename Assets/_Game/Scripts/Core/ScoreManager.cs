using UnityEngine;
using System.Collections;
using System;

public class ScoreManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameSettings gameSettings;

    [Header("Current Game State")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int currentLives;
    [SerializeField] private int currentCombo = 0;
    [SerializeField] private float currentComboMultiplier = 1f;

    // Events for UI updates
    public static event Action<int> OnScoreChanged;
    public static event Action<int> OnLevelChanged;
    public static event Action<int> OnLivesChanged;
    public static event Action<int, float> OnComboChanged; // combo count, multiplier
    public static event Action OnGameOver;
    public static event Action OnLevelUp;

    // Combo tracking
    private Coroutine comboDecayCoroutine;
    private float lastHitTime;

    // Level progression tracking
    private int pointsInCurrentLevel = 0;

    public int CurrentScore => currentScore;
    public int CurrentLevel => currentLevel;
    public int CurrentLives => currentLives;
    public int CurrentCombo => currentCombo;
    public float ComboMultiplier => currentComboMultiplier;

    private void Awake()
    {
        if (gameSettings == null)
        {
            Debug.LogError("ScoreManager: GameSettings not assigned!");
            return;
        }

        InitializeGame();
    }

    private void InitializeGame()
    {
        currentScore = 0;
        currentLevel = 1;
        currentLives = gameSettings.startingLives;
        currentCombo = 0;
        currentComboMultiplier = 1f;
        pointsInCurrentLevel = 0;
        lastHitTime = 0f;

        // Notify UI of initial values
        OnScoreChanged?.Invoke(currentScore);
        OnLevelChanged?.Invoke(currentLevel);
        OnLivesChanged?.Invoke(currentLives);
        OnComboChanged?.Invoke(currentCombo, currentComboMultiplier);

        Debug.Log($"Game Initialized - Score: {currentScore}, Level: {currentLevel}, Lives: {currentLives}");
    }

    public void RegisterHit(bool isPerfectTiming)
    {
        lastHitTime = Time.time;

        // Calculate base points
        int basePoints = gameSettings.baseHitPoints;
        if (isPerfectTiming)
        {
            basePoints += gameSettings.perfectHitBonus;
        }

        // Update combo
        currentCombo++;
        UpdateComboMultiplier();

        // Calculate final score with combo multiplier
        int finalPoints = Mathf.RoundToInt(basePoints * currentComboMultiplier);

        // Add to score
        currentScore += finalPoints;
        pointsInCurrentLevel += finalPoints;

        // Check for level up
        CheckLevelUp();

        // Restart combo decay timer
        if (comboDecayCoroutine != null)
        {
            StopCoroutine(comboDecayCoroutine);
        }
        comboDecayCoroutine = StartCoroutine(ComboDecayTimer());

        // Notify UI
        OnScoreChanged?.Invoke(currentScore);
        OnComboChanged?.Invoke(currentCombo, currentComboMultiplier);

        Debug.Log($"Hit! Base: {gameSettings.baseHitPoints}{(isPerfectTiming ? " + Perfect: " + gameSettings.perfectHitBonus : "")} " +
                  $"x {currentComboMultiplier:F1} = {finalPoints} points | Combo: {currentCombo} | Total: {currentScore}");
    }

    public void RegisterMiss()
    {
        currentLives--;
        ResetCombo();

        OnLivesChanged?.Invoke(currentLives);

        Debug.Log($"Miss! Lives remaining: {currentLives}");

        if (currentLives <= 0)
        {
            GameOver();
        }
    }

    private void UpdateComboMultiplier()
    {
        // Combo multiplier increases every few hits, capped at max
        int comboTiers = currentCombo / gameSettings.hitsPerComboTier;
        currentComboMultiplier = 1f + (comboTiers * (gameSettings.comboMultiplier - 1f));
        currentComboMultiplier = Mathf.Min(currentComboMultiplier, gameSettings.maxComboMultiplier);
    }

    private void ResetCombo()
    {
        currentCombo = 0;
        currentComboMultiplier = 1f;

        if (comboDecayCoroutine != null)
        {
            StopCoroutine(comboDecayCoroutine);
        }

        OnComboChanged?.Invoke(currentCombo, currentComboMultiplier);
        Debug.Log("Combo reset!");
    }

    private IEnumerator ComboDecayTimer()
    {
        yield return new WaitForSeconds(gameSettings.comboDecayTime);

        // Check if enough time has passed since last hit
        if (Time.time - lastHitTime >= gameSettings.comboDecayTime)
        {
            ResetCombo();
        }
    }

    private void CheckLevelUp()
    {
        if (pointsInCurrentLevel >= gameSettings.pointsPerLevel)
        {
            currentLevel++;
            pointsInCurrentLevel = 0; // Reset points for new level

            OnLevelChanged?.Invoke(currentLevel);
            OnLevelUp?.Invoke();

            Debug.Log($"LEVEL UP! Now at level {currentLevel}");
        }
    }

    private void GameOver()
    {
        Debug.Log($"GAME OVER! Final Score: {currentScore}, Level Reached: {currentLevel}");
        OnGameOver?.Invoke();

        // Save high score
        SaveHighScore();
    }

    public void RestartGame()
    {
        InitializeGame();
        Debug.Log("Game restarted!");
    }

    // Utility methods for other systems
    public float GetCurrentBallSpeed()
    {
        return gameSettings.GetBallSpeedForLevel(currentLevel);
    }

    public int GetBallCountForLevel()
    {
        if (currentLevel >= gameSettings.threeBallLevel)
            return 3;
        else if (currentLevel >= gameSettings.twoBallLevel)
            return 2;
        else
            return 1;
    }

    private void SaveHighScore()
    {
        int highScore = PlayerPrefs.GetInt("HighScore", 0);
        if (currentScore > highScore)
        {
            PlayerPrefs.SetInt("HighScore", currentScore);
            PlayerPrefs.SetInt("HighestLevel", currentLevel);
            PlayerPrefs.Save();
            Debug.Log($"New High Score: {currentScore}!");
        }
    }

    public int GetHighScore()
    {
        return PlayerPrefs.GetInt("HighScore", 0);
    }

    public int GetHighestLevel()
    {
        return PlayerPrefs.GetInt("HighestLevel", 1);
    }

    // Debug methods for testing
    [ContextMenu("Test Perfect Hit")]
    private void TestPerfectHit()
    {
        RegisterHit(true);
    }

    [ContextMenu("Test Regular Hit")]
    private void TestRegularHit()
    {
        RegisterHit(false);
    }

    [ContextMenu("Test Miss")]
    private void TestMiss()
    {
        RegisterMiss();
    }
}