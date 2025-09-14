using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Game/Settings")]
public class GameSettings : ScriptableObject
{
    [Header("Player Settings")]
    public float playerMovementSpeed = 0.5f;
    public float[] playerPositions = { 1.5f, 0f, -1.5f }; // Top, Middle, Bottom

    [Header("Ball Physics")]
    public float baseBallSpeed = 3f;
    public float ballSpeedIncrease = 0.1f; // Speed increase per level
    public float ballSpawnDelay = 2f; // Base delay between balls

    [Header("Hit Detection")]
    public float perfectTimingWindow = 0.2f; // Perfect hit window in seconds
    public float hitWindowStart = 0.7f; // 70% of journey
    public float hitWindowEnd = 1f; // 100% of journey

    [Header("Visual Effects")]
    public float trailLifetime = 1f;
    public int maxBalls = 5; // Maximum simultaneous balls

    [Header("Multi-Ball Level Configuration")]
    public LevelConfig[] levelConfigs = new LevelConfig[]
    {
        new LevelConfig { levelNumber = 1, ballCount = 1, speedMultiplier = 1f },
        new LevelConfig { levelNumber = 2, ballCount = 2, secondBallTrigger = 0.5f, speedMultiplier = 1.1f },
        new LevelConfig { levelNumber = 3, ballCount = 3, secondBallTrigger = 0.5f, thirdBallTrigger = 0.5f, speedMultiplier = 1.2f },
        new LevelConfig { levelNumber = 4, ballCount = 3, secondBallTrigger = 0.4f, thirdBallTrigger = 0.6f, speedMultiplier = 1.3f, spawnVariance = 0.1f },
        new LevelConfig { levelNumber = 5, ballCount = 3, secondBallTrigger = 0.3f, thirdBallTrigger = 0.7f, speedMultiplier = 1.4f, spawnVariance = 0.15f }
    };

    [Header("Legacy Multi-Ball Settings")]
    public int twoBallLevel = 2;
    public int threeBallLevel = 3;
    public float minSpawnInterval = 1f;
    public float maxSpawnInterval = 3f;

    [Header("Scoring")]
    public int baseHitPoints = 10;
    public int perfectHitBonus = 5;
    public float comboMultiplier = 1.5f;
    public float maxComboMultiplier = 5f;
    public float comboDecayTime = 6f;
    public int hitsPerComboTier = 3;

    [Header("Lives & Progression")]
    public int startingLives = 3;
    public int maxLives = 5;
    public int pointsPerLevel = 100;
    public float difficultyRampRate = 1.2f;
    public float spawnIntervalDecrease = 0.05f; // Decrease spawn interval per level

    [Header("Visual Colors")]
    public Color topPositionColor = Color.red;
    public Color middlePositionColor = Color.yellow;
    public Color bottomPositionColor = Color.blue;
    public Color hitSuccessColor = Color.white;
    public Color hitFailColor = Color.red;
    public Color wrongPositionColor = Color.magenta;
    public Color missColor = Color.gray;

    [Header("Visual Effects Settings")]
    public float hitFlashDuration = 0.2f;
    public float hitScaleMultiplier = 1.3f;
    public float ballGrowthDuration = 0.15f;

    [Header("Audio Settings")]
    public float hitSoundVolume = 0.8f;
    public float missSoundVolume = 0.6f;
    public float uiSoundVolume = 0.5f;

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool showDebugGizmos = true;
    public bool enableManualSpawning = true;

    // Helper methods for level configuration
    public LevelConfig GetLevelConfig(int level)
    {
        // Find exact level match first
        foreach (var config in levelConfigs)
        {
            if (config.levelNumber == level)
                return config;
        }

        // If no exact match, find the highest level config that's <= current level
        LevelConfig bestConfig = levelConfigs[0];
        foreach (var config in levelConfigs)
        {
            if (config.levelNumber <= level && config.levelNumber > bestConfig.levelNumber)
                bestConfig = config;
        }

        return bestConfig;
    }

    public float GetBallSpeedForLevel(int level)
    {
        LevelConfig config = GetLevelConfig(level);
        return baseBallSpeed * config.speedMultiplier;
    }

    public int GetBallCountForLevel(int level)
    {
        LevelConfig config = GetLevelConfig(level);
        return config.ballCount;
    }

    public float GetSecondBallTrigger(int level)
    {
        LevelConfig config = GetLevelConfig(level);
        return config.secondBallTrigger;
    }

    public float GetThirdBallTrigger(int level)
    {
        LevelConfig config = GetLevelConfig(level);
        return config.thirdBallTrigger;
    }

    public float GetSpawnVariance(int level)
    {
        LevelConfig config = GetLevelConfig(level);
        return config.spawnVariance;
    }

    // Validation method to be called in the editor
    void OnValidate()
    {
        // Ensure we have at least one level config
        if (levelConfigs == null || levelConfigs.Length == 0)
        {
            levelConfigs = new LevelConfig[]
            {
                new LevelConfig { levelNumber = 1, ballCount = 1, speedMultiplier = 1f }
            };
        }

        // Ensure level numbers are sequential starting from 1
        for (int i = 0; i < levelConfigs.Length; i++)
        {
            if (levelConfigs[i] == null)
                levelConfigs[i] = new LevelConfig();

            levelConfigs[i].levelNumber = i + 1;
        }
    }
}

[System.Serializable]
public class LevelConfig
{
    [Tooltip("Level number")]
    public int levelNumber = 1;
    [Tooltip("Number of balls in this level")]
    public int ballCount = 1;

    [Range(0f, 1f)]
    [Tooltip("When to spawn 2nd ball (% of 1st ball journey)")]
    public float secondBallTrigger = 0.5f;
    [Range(0f, 1f)]
    [Tooltip("When to spawn 3rd ball (% of 2nd ball journey)")]
    public float thirdBallTrigger = 0.5f;

    [Range(0.8f, 2f)]
    [Tooltip("Speed multiplier for this level")]
    public float speedMultiplier = 1f;

    [Range(0f, 0.2f)]
    [Tooltip("Random variance in spawn timing (±%)")]
    public float spawnVariance = 0f;
}