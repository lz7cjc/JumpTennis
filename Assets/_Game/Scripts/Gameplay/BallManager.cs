using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class BallManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameSettings gameSettings;

    [Header("References")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform ballSpawnPoint;
    [SerializeField] private PlayerController player;

    [Header("Progressive Difficulty")]
    [SerializeField] private float baseSpawnInterval = 2.0f;
    [SerializeField] private float minSpawnInterval = 0.8f;
    [SerializeField] private float spawnIntervalReduction = 0.05f; // Per level
    [SerializeField] private float currentSpawnInterval;
    [SerializeField] private int targetBallCount = 1;

    [Header("Debug Info")]
    [SerializeField] private int activeBalls = 0;
    [SerializeField] private int ballsSpawned = 0;
    [SerializeField] private bool isSpawning = false;

    // Ball pool
    private List<Ball> ballPool = new List<Ball>();
    private List<Ball> activeBallsList = new List<Ball>();

    // Spawning control
    private Coroutine spawnCoroutine;

    // Progressive difficulty
    private ScoreManager scoreManager;

    // Events
    public System.Action<Ball> OnBallHit;
    public System.Action<Ball> OnBallMissed;
    public System.Action<int> OnScoreChanged;

    void Start()
    {
        // Get ScoreManager reference
        scoreManager = FindFirstObjectByType<ScoreManager>();
        if (scoreManager != null)
        {
            Debug.Log("ScoreManager found, subscribing to OnLevelUp event");
            ScoreManager.OnLevelUp += UpdateDifficulty;
        }
        else
        {
            Debug.LogError("ScoreManager not found!");
        }

        // Initialize spawn interval
        currentSpawnInterval = baseSpawnInterval;

        // Test ScoreManager methods
        if (scoreManager != null)
        {
            try
            {
                float testSpeed = scoreManager.GetCurrentBallSpeed();
                int testCount = scoreManager.GetBallCountForLevel();
                Debug.Log($"ScoreManager methods working: Speed={testSpeed}, Count={testCount}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ScoreManager methods failed: {e.Message}");
            }
        }

        // Validate references
        if (gameSettings == null)
        {
            Debug.LogError("BallManager: GameSettings not assigned!");
            return;
        }

        if (ballPrefab == null)
        {
            Debug.LogError("BallManager: Ball prefab not assigned!");
            return;
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        // Initialize ball pool
        InitializeBallPool();

        // Connect to player events
        if (player != null)
        {
            player.OnHitAttempt += HandlePlayerHitAttempt;
        }

        // Start spawning balls
        StartBallSpawning();
    }

    void OnDestroy()
    {
        // Disconnect events
        if (player != null)
        {
            player.OnHitAttempt -= HandlePlayerHitAttempt;
        }

        // Unsubscribe from ScoreManager events
        ScoreManager.OnLevelUp -= UpdateDifficulty;
    }

    #region Progressive Difficulty

    private void UpdateDifficulty()
    {
        if (scoreManager == null) return;

        // Get current ball speed to estimate level
        float currentSpeed = scoreManager.GetCurrentBallSpeed();
        int estimatedLevel = Mathf.Max(1, Mathf.RoundToInt((currentSpeed - 3.0f) / 0.1f) + 1);

        // Update spawn interval (faster spawning at higher levels)
        currentSpawnInterval = Mathf.Max(
            minSpawnInterval,
            baseSpawnInterval - (estimatedLevel * spawnIntervalReduction)
        );

        // Update target ball count based on level
        targetBallCount = scoreManager.GetBallCountForLevel();

        Debug.Log($"Difficulty updated for Level {estimatedLevel}: " +
                 $"Spawn Interval: {currentSpawnInterval:F2}s, " +
                 $"Target Ball Count: {targetBallCount}, " +
                 $"Ball Speed: {currentSpeed:F2}");
    }

    #endregion

    #region Ball Pool Management

    void InitializeBallPool()
    {
        // Create initial pool of balls - increase for multi-ball levels
        int initialPoolSize = Mathf.Max(gameSettings.maxBalls + 2, 8);

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewBall();
        }

        Debug.Log($"Ball pool initialized with {initialPoolSize} balls");
    }

    void CreateNewBall()
    {
        GameObject ballObj = Instantiate(ballPrefab, transform);
        Debug.Log($"Created ball object: {ballObj.name}");
        Ball ball = ballObj.GetComponent<Ball>();

        if (ball == null)
        {
            Debug.LogError("Ball prefab missing Ball component!");
            return;
        }

        // Connect events
        ball.OnBallHit += HandleBallHit;
        ball.OnBallMissed += HandleBallMissed;
        ball.OnBallReachPlayer += HandleBallReachPlayer;

        // Add to pool
        ballPool.Add(ball);
        ballObj.SetActive(false);
    }

    Ball GetBallFromPool()
    {
        // Find inactive ball
        foreach (Ball ball in ballPool)
        {
            if (!ball.gameObject.activeInHierarchy)
            {
                ball.gameObject.SetActive(true);
                ball.ResetBall();
                return ball;
            }
        }

        // No inactive balls, create new one
        CreateNewBall();
        return ballPool[ballPool.Count - 1];
    }

    void ReturnBallToPool(Ball ball)
    {
        if (activeBallsList.Contains(ball))
        {
            activeBallsList.Remove(ball);
        }

        ball.gameObject.SetActive(false);
        activeBalls = activeBallsList.Count;
    }

    #endregion

    #region Ball Spawning

    public void StartBallSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }

        isSpawning = true;  // Set this BEFORE starting coroutine
        spawnCoroutine = StartCoroutine(SpawnBallsCoroutine());
        Debug.Log("Ball spawning started");
    }

    public void StopBallSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        isSpawning = false;
        Debug.Log("Ball spawning stopped");
    }

    IEnumerator SpawnBallsCoroutine()
    {
        Debug.Log("SpawnBallsCoroutine started");

        while (isSpawning)
        {
            Debug.Log("Spawn coroutine loop iteration");

            // Check if we should spawn balls
            if (ShouldSpawnBall())
            {
                int ballsToSpawn = targetBallCount - activeBalls;

                Debug.Log($"Spawning {ballsToSpawn} balls automatically (Target: {targetBallCount}, Active: {activeBalls})");

                // Spawn multiple balls if needed (for multi-ball levels)
                for (int i = 0; i < ballsToSpawn && i < 3; i++)
                {
                    SpawnRandomBall();

                    // Small delay between simultaneous spawns for visual clarity
                    if (i < ballsToSpawn - 1)
                    {
                        yield return new WaitForSeconds(0.3f);
                    }
                }

                // Wait for current spawn interval after spawning
                Debug.Log($"Waiting {currentSpawnInterval} seconds before next spawn check");
                yield return new WaitForSeconds(currentSpawnInterval);
            }
            else
            {
                Debug.Log("No spawn needed, checking again in 0.1s");
                // Check again soon if no spawning needed
                yield return new WaitForSeconds(0.1f);
            }
        }

        Debug.Log("SpawnBallsCoroutine ended");
    }

    bool ShouldSpawnBall()
    {
        if (gameSettings == null)
        {
            Debug.LogError("GameSettings is null! Cannot determine spawn rules.");
            return false;
        }

        Debug.Log($"ShouldSpawnBall check: activeBalls={activeBalls}, maxBalls={gameSettings.maxBalls}, targetBallCount={targetBallCount}");

        // Don't exceed max balls
        if (activeBalls >= gameSettings.maxBalls)
        {
            Debug.Log("Not spawning: at max balls limit");
            return false;
        }

        // Spawn if we have fewer active balls than target count
        bool shouldSpawn = activeBalls < targetBallCount;
        Debug.Log($"Should spawn: {shouldSpawn} (need {targetBallCount - activeBalls} more balls)");
        return shouldSpawn;
    }

    void SpawnRandomBall()
    {
        // Random target position
        int randomPosition = Random.Range(0, gameSettings.playerPositions.Length);

        // Calculate spawn position (opposite side of court from player)
        Vector3 spawnPos = ballSpawnPoint != null ? ballSpawnPoint.position :
                          new Vector3(5f, gameSettings.playerPositions[randomPosition], 0f);

        // Use progressive difficulty speed
        float ballSpeed = scoreManager != null ? scoreManager.GetCurrentBallSpeed() : gameSettings.baseBallSpeed;
        SpawnBall(spawnPos, randomPosition, ballSpeed);
    }

    public void SpawnBall(Vector3 fromPosition, int targetPosition, float speed = -1f)
    {
        Ball ball = GetBallFromPool();
        if (ball == null)
        {
            Debug.LogError("Failed to get ball from pool!");
            return;
        }

        // Use progressive difficulty speed if not specified
        float ballSpeed = speed > 0 ? speed :
                         (scoreManager != null ? scoreManager.GetCurrentBallSpeed() : gameSettings.baseBallSpeed);

        // Launch the ball
        ball.LaunchBall(fromPosition, targetPosition, ballSpeed);

        // Add to active list
        if (!activeBallsList.Contains(ball))
        {
            activeBallsList.Add(ball);
        }

        activeBalls = activeBallsList.Count;
        ballsSpawned++;

        Debug.Log($"Ball spawned #{ballsSpawned} at speed {ballSpeed:F2}, active balls: {activeBalls}");
    }

    #endregion

    #region Event Handlers

    void HandlePlayerHitAttempt()
    {
        if (player == null) return;

        int playerPosition = player.GetCurrentPosition();
        bool hitAnyBall = false;

        // Check all active balls for hits
        for (int i = activeBallsList.Count - 1; i >= 0; i--)
        {
            Ball ball = activeBallsList[i];
            if (ball.TryHit(playerPosition))
            {
                hitAnyBall = true;
                break; // Only hit one ball per attempt
            }
        }

        if (!hitAnyBall)
        {
            Debug.Log("Hit attempt missed all balls!");
        }
    }

    void HandleBallHit(Ball ball)
    {
        Debug.Log("Ball hit successfully!");
        OnBallHit?.Invoke(ball);

        // Remove from active list but don't deactivate yet
        // The ball will deactivate itself after the visual effect
        if (activeBallsList.Contains(ball))
        {
            activeBallsList.Remove(ball);
            activeBalls = activeBallsList.Count;
        }
    }

    void HandleBallMissed(Ball ball)
    {
        Debug.Log("Ball was missed!");
        ReturnBallToPool(ball);
        OnBallMissed?.Invoke(ball);
    }

    void HandleBallReachPlayer(Ball ball)
    {
        Debug.Log("Ball reached player zone");
    }

    #endregion

    #region Testing Controls

    void Update()
    {
        // Testing controls (remove later)
        HandleTestingInput();
    }

    void HandleTestingInput()
    {
        // Manual ball spawning for testing
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SpawnBall(new Vector3(5f, gameSettings.playerPositions[0], 0f), 0); // Top
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SpawnBall(new Vector3(5f, gameSettings.playerPositions[1], 0f), 1); // Middle
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SpawnBall(new Vector3(5f, gameSettings.playerPositions[2], 0f), 2); // Bottom
        }

        // Stop/start spawning
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (isSpawning)
                StopBallSpawning();
            else
                StartBallSpawning();
        }

        // Clear all balls
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearAllBalls();
        }
    }

    #endregion

    #region Public Interface

    public void ClearAllBalls()
    {
        foreach (Ball ball in activeBallsList.ToArray())
        {
            ReturnBallToPool(ball);
        }

        activeBallsList.Clear();
        activeBalls = 0;
        Debug.Log("All balls cleared");
    }

    public int GetActiveBallCount() => activeBalls;
    public int GetTotalBallsSpawned() => ballsSpawned;
    public bool IsSpawning() => isSpawning;

    // Progressive difficulty getters
    public float GetCurrentSpawnInterval() => currentSpawnInterval;
    public int GetTargetBallCount() => targetBallCount;

    // Reset difficulty for new game
    public void ResetDifficulty()
    {
        currentSpawnInterval = baseSpawnInterval;
        targetBallCount = 1;
        ClearAllBalls();
    }

    #endregion

    #region Debug

    void OnDrawGizmos()
    {
        // Draw spawn point
        if (ballSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ballSpawnPoint.position, 0.3f);
        }

        // Draw all player positions for reference
        if (gameSettings != null && player != null)
        {
            Gizmos.color = Color.gray;
            for (int i = 0; i < gameSettings.playerPositions.Length; i++)
            {
                Vector3 pos = new Vector3(player.transform.position.x, gameSettings.playerPositions[i], 0f);
                Gizmos.DrawWireCube(pos, Vector3.one * 0.5f);
            }
        }
    }

    #endregion
}