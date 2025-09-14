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

    [Header("Wave Spawning System")]
    [SerializeField] private float baseWaveInterval = 3.0f;
    [SerializeField] private float minWaveInterval = 1.5f;
    [SerializeField] private float waveIntervalReduction = 0.1f;

    [Header("Debug Info")]
    [SerializeField] private int activeBalls = 0;
    [SerializeField] private int ballsSpawned = 0;
    [SerializeField] private bool isSpawning = false;
    [SerializeField] private int currentWave = 0;
    [SerializeField] private int currentLevel = 1;

    // Ball pool
    private List<Ball> ballPool = new List<Ball>();
    private List<Ball> activeBallsList = new List<Ball>();

    // Wave spawning control
    private Coroutine waveSpawnCoroutine;
    private List<BallWave> activeWaves = new List<BallWave>();

    // Progressive difficulty
    private ScoreManager scoreManager;
    private float currentWaveInterval;

    // Events
    public System.Action<Ball> OnBallHit;
    public System.Action<Ball> OnBallMissed;

    // Wave tracking class
    [System.Serializable]
    public class BallWave
    {
        public int waveId;
        public List<Ball> balls = new List<Ball>();
        public List<WaveBallConfig> ballConfigs = new List<WaveBallConfig>();
        public bool isComplete = false;
        public float waveStartTime;

        [System.Serializable]
        public class WaveBallConfig
        {
            public int targetPosition;
            public float spawnDelay;
            public float speed;
            public bool hasSpawned = false;
        }
    }

    void Start()
    {
        // Get ScoreManager reference
        scoreManager = FindFirstObjectByType<ScoreManager>();
        if (scoreManager != null)
        {
            ScoreManager.OnLevelUp += UpdateDifficulty;
            Debug.Log("Connected to ScoreManager OnLevelUp event");
        }
        else
        {
            Debug.LogError("ScoreManager not found!");
        }

        // Initialize wave interval
        currentWaveInterval = baseWaveInterval;

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

        // Start wave spawning
        StartWaveSpawning();
    }

    void OnDestroy()
    {
        if (player != null)
        {
            player.OnHitAttempt -= HandlePlayerHitAttempt;
        }
        ScoreManager.OnLevelUp -= UpdateDifficulty;
    }

    #region Progressive Difficulty

    private void UpdateDifficulty()
    {
        // For manual testing, use the manually set currentLevel
        // For normal gameplay, get from ScoreManager
        if (scoreManager != null && Application.isPlaying)
        {
            // Only update from ScoreManager if we're not manually testing
            bool isManualTest = Input.GetKey(KeyCode.F1) || Input.GetKey(KeyCode.F2) ||
                               Input.GetKey(KeyCode.F3) || Input.GetKey(KeyCode.F4);

            if (!isManualTest)
            {
                currentLevel = scoreManager.CurrentLevel;
            }
        }

        // DEBUG: Check what level we're on
        Debug.Log($"=== DIFFICULTY UPDATE ===");
        Debug.Log($"Current Level: {currentLevel}");
        if (scoreManager != null)
            Debug.Log($"ScoreManager Level: {scoreManager.CurrentLevel}");

        // DEBUG: Check level config
        LevelConfig config = gameSettings.GetLevelConfig(currentLevel);
        Debug.Log($"Level Config - Ball Count: {config.ballCount}, Speed: {config.speedMultiplier}");

        // Update wave interval (faster waves at higher levels)
        currentWaveInterval = Mathf.Max(
            minWaveInterval,
            baseWaveInterval - (currentLevel * waveIntervalReduction)
        );

        Debug.Log($"Wave interval updated to: {currentWaveInterval:F2}s");
    }

    #endregion

    #region Ball Pool Management

    void InitializeBallPool()
    {
        int initialPoolSize = Mathf.Max(gameSettings.maxBalls + 3, 10);

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewBall();
        }

        Debug.Log($"Ball pool initialized with {initialPoolSize} balls");
    }

    void CreateNewBall()
    {
        GameObject ballObj = Instantiate(ballPrefab, transform);
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
        ball.OnBallCompleted += HandleBallCompleted;

        ballPool.Add(ball);
        ballObj.SetActive(false);
    }

    Ball GetBallFromPool()
    {
        foreach (Ball ball in ballPool)
        {
            if (!ball.gameObject.activeInHierarchy)
            {
                ball.gameObject.SetActive(true);
                ball.ResetBall();
                return ball;
            }
        }

        // Create new ball if none available
        CreateNewBall();
        Ball newBall = ballPool[ballPool.Count - 1];
        newBall.gameObject.SetActive(true);
        newBall.ResetBall();
        return newBall;
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

    #region Wave Spawning System

    public void StartWaveSpawning()
    {
        if (waveSpawnCoroutine != null)
        {
            StopCoroutine(waveSpawnCoroutine);
        }

        isSpawning = true;
        waveSpawnCoroutine = StartCoroutine(WaveSpawningCoroutine());
        Debug.Log("Wave spawning started");
    }

    public void StopWaveSpawning()
    {
        if (waveSpawnCoroutine != null)
        {
            StopCoroutine(waveSpawnCoroutine);
            waveSpawnCoroutine = null;
        }

        isSpawning = false;
        Debug.Log("Wave spawning stopped");
    }

    IEnumerator WaveSpawningCoroutine()
    {
        while (isSpawning)
        {
            // Create new wave
            BallWave newWave = CreateWaveForCurrentLevel();

            if (newWave != null)
            {
                activeWaves.Add(newWave);
                StartCoroutine(ProcessWave(newWave));

                Debug.Log($"Started wave #{newWave.waveId} with {newWave.ballConfigs.Count} balls for level {currentLevel}");
            }

            // Wait for next wave
            yield return new WaitForSeconds(currentWaveInterval);
        }
    }

    BallWave CreateWaveForCurrentLevel()
    {
        // Get current level from ScoreManager if available
        if (scoreManager != null)
        {
            currentLevel = scoreManager.CurrentLevel;
        }

        LevelConfig levelConfig = gameSettings.GetLevelConfig(currentLevel);

        BallWave wave = new BallWave
        {
            waveId = ++currentWave,
            waveStartTime = Time.time
        };

        float baseSpeed = gameSettings.GetBallSpeedForLevel(currentLevel);
        float spawnVariance = gameSettings.GetSpawnVariance(currentLevel);

        Debug.Log($"Creating wave for level {currentLevel}: {levelConfig.ballCount} balls at {baseSpeed:F2} speed");

        // Store first ball's journey time for consistent timing
        float firstBallJourneyTime = 0f;
        float secondBallJourneyTime = 0f;

        // Create ball configs for this wave
        for (int i = 0; i < levelConfig.ballCount; i++)
        {
            int randomPosition = Random.Range(0, gameSettings.playerPositions.Length);

            // Calculate journey time for this specific ball
            float journeyTime = CalculateJourneyTime(randomPosition, baseSpeed);

            float spawnDelay = 0f;

            if (i == 0) // First ball
            {
                firstBallJourneyTime = journeyTime;
                spawnDelay = 0f; // First ball spawns immediately
            }
            else if (i == 1) // Second ball
            {
                // Second ball spawns when first ball reaches trigger percentage
                float triggerPercent = levelConfig.secondBallTrigger;
                float variance = Random.Range(-spawnVariance, spawnVariance);
                spawnDelay = firstBallJourneyTime * (triggerPercent + variance);
                secondBallJourneyTime = journeyTime;
                Debug.Log($"Second ball delay: {spawnDelay:F2}s (first ball journey: {firstBallJourneyTime:F2}s, trigger: {triggerPercent:F2})");
            }
            else if (i == 2) // Third ball
            {
                // Third ball spawns when second ball reaches trigger percentage
                float triggerPercent = levelConfig.thirdBallTrigger;
                float variance = Random.Range(-spawnVariance, spawnVariance);
                float secondBallTriggerTime = firstBallJourneyTime * levelConfig.secondBallTrigger;
                spawnDelay = secondBallTriggerTime + (secondBallJourneyTime * (triggerPercent + variance));
                Debug.Log($"Third ball delay: {spawnDelay:F2}s (second ball journey: {secondBallJourneyTime:F2}s, trigger: {triggerPercent:F2})");
            }

            var ballConfig = new BallWave.WaveBallConfig
            {
                targetPosition = randomPosition,
                spawnDelay = spawnDelay,
                speed = baseSpeed,
                hasSpawned = false
            };

            wave.ballConfigs.Add(ballConfig);
        }

        return wave;
    }

    float CalculateJourneyTime(int targetPosition, float ballSpeed)
    {
        // Calculate journey time for a ball to a specific position
        Vector3 spawnPos = ballSpawnPoint != null ? ballSpawnPoint.position :
                          new Vector3(5f, gameSettings.playerPositions[targetPosition], 0f);
        Vector3 targetPos = new Vector3(player.transform.position.x, gameSettings.playerPositions[targetPosition], 0f);
        float journeyDistance = Vector3.Distance(spawnPos, targetPos);

        return journeyDistance / ballSpeed;
    }

    IEnumerator ProcessWave(BallWave wave)
    {
        float waveStartTime = Time.time;

        while (!wave.isComplete)
        {
            float elapsed = Time.time - waveStartTime;

            // Check each ball config to see if it's time to spawn
            foreach (var ballConfig in wave.ballConfigs)
            {
                if (!ballConfig.hasSpawned && elapsed >= ballConfig.spawnDelay)
                {
                    SpawnBallForWave(wave, ballConfig);
                    ballConfig.hasSpawned = true;
                }
            }

            // Check if wave is complete (all balls spawned)
            bool allSpawned = true;
            foreach (var ballConfig in wave.ballConfigs)
            {
                if (!ballConfig.hasSpawned)
                {
                    allSpawned = false;
                    break;
                }
            }

            if (allSpawned)
            {
                wave.isComplete = true;
                Debug.Log($"Wave #{wave.waveId} completed - all balls spawned");

                // Wait a bit longer before allowing next wave (let balls travel)
                yield return new WaitForSeconds(currentWaveInterval);
            }
            else
            {
                yield return new WaitForSeconds(0.1f); // Check every 0.1 seconds
            }
        }

        // Clean up completed wave
        activeWaves.Remove(wave);
        Debug.Log($"Wave #{wave.waveId} cleaned up from active waves");
    }

    void SpawnBallForWave(BallWave wave, BallWave.WaveBallConfig ballConfig)
    {
        Vector3 spawnPos = ballSpawnPoint != null ? ballSpawnPoint.position :
                          new Vector3(5f, gameSettings.playerPositions[ballConfig.targetPosition], 0f);

        Ball ball = GetBallFromPool();
        if (ball == null)
        {
            Debug.LogError("Failed to get ball from pool!");
            return;
        }

        ball.LaunchBall(spawnPos, ballConfig.targetPosition, ballConfig.speed);

        if (!activeBallsList.Contains(ball))
        {
            activeBallsList.Add(ball);
        }

        wave.balls.Add(ball);
        activeBalls = activeBallsList.Count;
        ballsSpawned++;

        Debug.Log($"Wave #{wave.waveId} spawned ball #{ballsSpawned} to {GetPositionName(ballConfig.targetPosition)} (delay: {ballConfig.spawnDelay:F2}s, speed: {ballConfig.speed:F2})");
    }

    #endregion

    #region Event Handlers

    void HandlePlayerHitAttempt()
    {
        if (player == null) return;

        int playerPosition = player.GetCurrentPosition();
        bool hitAnyBall = false;

        for (int i = activeBallsList.Count - 1; i >= 0; i--)
        {
            Ball ball = activeBallsList[i];
            if (ball.TryHit(playerPosition))
            {
                hitAnyBall = true;
                break;
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
    }

    void HandleBallMissed(Ball ball)
    {
        Debug.Log("Ball was missed!");
        OnBallMissed?.Invoke(ball);
    }

    void HandleBallReachPlayer(Ball ball)
    {
        Debug.Log("Ball reached player zone");
    }

    void HandleBallCompleted(Ball ball)
    {
        Debug.Log($"Ball completed lifecycle - returning to pool");
        ReturnBallToPool(ball);
        Debug.Log($"Ball returned to pool. Active balls: {activeBalls}");
    }

    #endregion

    #region Testing Controls

    void Update()
    {
        HandleTestingInput();
    }

    void HandleTestingInput()
    {
        // Force test specific levels
        if (Input.GetKeyDown(KeyCode.F1))
        {
            currentLevel = 1;
            Debug.Log("FORCED TO LEVEL 1");
            UpdateDifficulty();
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            currentLevel = 2;
            Debug.Log("FORCED TO LEVEL 2");
            UpdateDifficulty();
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            currentLevel = 3;
            Debug.Log("FORCED TO LEVEL 3");
            UpdateDifficulty();
        }
        if (Input.GetKeyDown(KeyCode.F4))
        {
            currentLevel = 4;
            Debug.Log("FORCED TO LEVEL 4");
            UpdateDifficulty();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            // Test spawn wave for current level
            BallWave testWave = CreateWaveForCurrentLevel();
            if (testWave != null)
            {
                activeWaves.Add(testWave);
                StartCoroutine(ProcessWave(testWave));
                Debug.Log($"Manual test wave spawned for level {currentLevel} with {testWave.ballConfigs.Count} balls");
            }
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (isSpawning)
                StopWaveSpawning();
            else
                StartWaveSpawning();
        }

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

        // Clear active waves
        activeWaves.Clear();

        Debug.Log("All balls and waves cleared");
    }

    public int GetActiveBallCount() => activeBalls;
    public int GetTotalBallsSpawned() => ballsSpawned;
    public bool IsSpawning() => isSpawning;
    public float GetCurrentWaveInterval() => currentWaveInterval;
    public int GetActiveWaveCount() => activeWaves.Count;

    public void ResetDifficulty()
    {
        currentLevel = 1;
        currentWaveInterval = baseWaveInterval;
        currentWave = 0;
        ClearAllBalls();
    }

    #endregion

    #region Utility

    string GetPositionName(int position)
    {
        switch (position)
        {
            case 0: return "Top";
            case 1: return "Middle";
            case 2: return "Bottom";
            default: return "Unknown";
        }
    }

    #endregion

    #region Debug

    void OnDrawGizmos()
    {
        if (ballSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ballSpawnPoint.position, 0.3f);
        }

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