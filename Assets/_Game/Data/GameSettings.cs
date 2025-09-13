using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Pocket Rally/Game Settings")]
public class GameSettings : ScriptableObject
{
    [Header("Player Movement")]
    [Tooltip("Time in seconds to move between positions")]
    public float playerMovementSpeed = 0.5f;
    [Tooltip("Array of Y positions [Top, Middle, Bottom]")]
    public float[] playerPositions = { 1.5f, 0f, -1.5f };

    [Header("Ball Physics")]
    [Tooltip("Base ball speed in units per second")]
    public float baseBallSpeed = 3f;
    [Tooltip("Speed increase per level")]
    public float ballSpeedIncrease = 0.1f;
    [Tooltip("Delay between ball spawns")]
    public float ballSpawnDelay = 2f;
    [Tooltip("Window in seconds for perfect timing")]
    public float perfectTimingWindow = 0.2f;
    [Range(0f, 1f)]
    [Tooltip("Start of hit window as percentage of ball journey")]
    public float hitWindowStart = 0.7f; // 70% of journey
    [Range(0f, 1f)]
    [Tooltip("End of hit window as percentage of ball journey")]
    public float hitWindowEnd = 1f; // 100% of journey
    [Tooltip("Trail lifetime for ball trails")]
    public float trailLifetime = 0.3f;

    [Header("Multi-Ball Progression")]
    [Tooltip("Maximum balls that can be active at once")]
    public int maxBalls = 3;
    [Tooltip("Level when 2 balls start spawning")]
    public int twoBallLevel = 6;
    [Tooltip("Level when 3 balls start spawning")]
    public int threeBallLevel = 11;
    [Tooltip("Minimum time between ball spawns")]
    public float minSpawnInterval = 1f;
    [Tooltip("Maximum time between ball spawns")]
    public float maxSpawnInterval = 3f;

    [Header("Scoring System")]
    [Space(10)]
    [Tooltip("Base points for any successful hit")]
    public int baseHitPoints = 10;
    [Tooltip("Bonus points for perfect timing")]
    public int perfectHitBonus = 5;
    [Tooltip("Multiplier applied for combo hits")]
    public float comboMultiplier = 1.5f;
    [Tooltip("Maximum combo multiplier achievable")]
    public int maxComboMultiplier = 5;
    [Tooltip("Seconds without hit before combo resets")]
    public int comboDecayTime = 2;
    [Tooltip("Hits needed to increase combo tier")]
    public int hitsPerComboTier = 3;

    [Header("Lives System")]
    [Tooltip("Number of lives player starts with")]
    public int startingLives = 3;
    [Tooltip("Maximum lives player can have")]
    public int maxLives = 5;

    [Header("Level Progression")]
    [Tooltip("Points needed to advance to next level")]
    public int pointsPerLevel = 100;
    [Tooltip("Multiplier for difficulty increase per level")]
    public float difficultyRampRate = 1.2f;
    [Tooltip("How much spawn interval decreases per level")]
    public float spawnIntervalDecrease = 0.05f;

    [Header("Visual Colors")]
    [Tooltip("Color for balls targeting top position")]
    public Color topPositionColor = Color.red;
    [Tooltip("Color for balls targeting middle position")]
    public Color middlePositionColor = Color.yellow;
    [Tooltip("Color for balls targeting bottom position")]
    public Color bottomPositionColor = Color.blue;
    [Tooltip("Color flash for successful hits")]
    public Color hitSuccessColor = Color.white;
    [Tooltip("Color flash for timing failure")]
    public Color hitFailColor = Color.red;
    [Tooltip("Color flash for wrong position")]
    public Color wrongPositionColor = Color.magenta;
    [Tooltip("Color for missed balls")]
    public Color missColor = Color.gray;

    [Header("Visual Effects")]
    [Tooltip("Duration of hit success flash effect")]
    public float hitFlashDuration = 0.2f;
    [Tooltip("Scale multiplier for hit success effect")]
    public float hitScaleMultiplier = 1.3f;
    [Tooltip("Duration of ball growth effect")]
    public float ballGrowthDuration = 0.15f;

    [Header("Audio Settings")]
    [Tooltip("Volume for hit success sounds")]
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.8f;
    [Tooltip("Volume for miss sounds")]
    [Range(0f, 1f)]
    public float missSoundVolume = 0.6f;
    [Tooltip("Volume for UI sounds")]
    [Range(0f, 1f)]
    public float uiSoundVolume = 0.5f;

    [Header("Debug Settings")]
    [Tooltip("Enable debug logging")]
    public bool enableDebugLogs = true;
    [Tooltip("Show debug gizmos in scene view")]
    public bool showDebugGizmos = true;
    [Tooltip("Enable manual ball spawning with number keys")]
    public bool enableManualSpawning = true;

    // Backward compatibility properties for your existing code
    public float movementSpeed => playerMovementSpeed;
    public float ballSpeed => baseBallSpeed;
    public float topPosition => playerPositions[0];
    public float middlePosition => playerPositions[1];
    public float bottomPosition => playerPositions[2];
    public Color topBallColor => topPositionColor;
    public Color middleBallColor => middlePositionColor;
    public Color bottomBallColor => bottomPositionColor;
    public float trailTime => trailLifetime;

    // Calculated properties for easy access
    public Vector3 TopWorldPosition => new Vector3(0, playerPositions[0], 0);
    public Vector3 MiddleWorldPosition => new Vector3(0, playerPositions[1], 0);
    public Vector3 BottomWorldPosition => new Vector3(0, playerPositions[2], 0);

    /// <summary>
    /// Get the world position for a given player height
    /// </summary>
    public Vector3 GetPositionForHeight(int height)
    {
        if (height >= 0 && height < playerPositions.Length)
            return new Vector3(0, playerPositions[height], 0);
        return MiddleWorldPosition;
    }

    /// <summary>
    /// Get the color for a given ball target position
    /// </summary>
    public Color GetBallColorForHeight(int height)
    {
        return height switch
        {
            0 => topPositionColor,
            1 => middlePositionColor,
            2 => bottomPositionColor,
            _ => middlePositionColor
        };
    }

    /// <summary>
    /// Calculate spawn interval based on current level
    /// </summary>
    public float GetSpawnIntervalForLevel(int level)
    {
        float baseInterval = Mathf.Lerp(maxSpawnInterval, minSpawnInterval, (level - 1) * spawnIntervalDecrease);
        return Mathf.Max(baseInterval, minSpawnInterval);
    }

    /// <summary>
    /// Get current ball speed for given level
    /// </summary>
    public float GetBallSpeedForLevel(int level)
    {
        return baseBallSpeed + ((level - 1) * ballSpeedIncrease);
    }

    /// <summary>
    /// Validate settings on load/change
    /// </summary>
    private void OnValidate()
    {
        // Ensure we have exactly 3 positions
        if (playerPositions == null || playerPositions.Length != 3)
        {
            playerPositions = new float[] { 1.5f, 0f, -1.5f };
        }

        // Ensure positions are in correct order (top > middle > bottom)
        if (playerPositions[0] <= playerPositions[1])
        {
            Debug.LogWarning("GameSettings: Top position should be higher than middle position");
        }
        if (playerPositions[1] <= playerPositions[2])
        {
            Debug.LogWarning("GameSettings: Middle position should be higher than bottom position");
        }

        // Ensure timing windows make sense
        if (hitWindowStart >= hitWindowEnd)
        {
            Debug.LogWarning("GameSettings: Hit window start should be less than hit window end");
        }

        // Ensure multi-ball progression makes sense
        if (twoBallLevel >= threeBallLevel)
        {
            Debug.LogWarning("GameSettings: Two ball level should be less than three ball level");
        }

        // Ensure spawn intervals make sense
        if (minSpawnInterval >= maxSpawnInterval)
        {
            Debug.LogWarning("GameSettings: Min spawn interval should be less than max spawn interval");
        }

        // Clamp values to reasonable ranges
        playerMovementSpeed = Mathf.Max(0.1f, playerMovementSpeed);
        baseBallSpeed = Mathf.Max(0.5f, baseBallSpeed);
        perfectTimingWindow = Mathf.Max(0.05f, perfectTimingWindow);
        baseHitPoints = Mathf.Max(1, baseHitPoints);
        startingLives = Mathf.Max(1, startingLives);
        pointsPerLevel = Mathf.Max(10, pointsPerLevel);
        maxBalls = Mathf.Max(1, maxBalls);
    }
}