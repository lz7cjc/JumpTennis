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

    [Header("Debug Info")]
    [SerializeField] private int activeBalls = 0;
    [SerializeField] private int ballsSpawned = 0;
    [SerializeField] private bool isSpawning = false;

    // Ball pool
    private List<Ball> ballPool = new List<Ball>();
    private List<Ball> activeBallsList = new List<Ball>();

    // Spawning control
    private Coroutine spawnCoroutine;

    // Events
    public System.Action<Ball> OnBallHit;
    public System.Action<Ball> OnBallMissed;
    public System.Action<int> OnScoreChanged;

    void Start()
    {
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
    }

    #region Ball Pool Management

    void InitializeBallPool()
    {
        // Create initial pool of balls
        int initialPoolSize = gameSettings.maxBalls + 2;

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

        spawnCoroutine = StartCoroutine(SpawnBallsCoroutine());
        isSpawning = true;
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
        while (isSpawning)
        {
            // Check if we should spawn a new ball
            if (ShouldSpawnBall())
            {
                SpawnRandomBall();
                yield return new WaitForSeconds(gameSettings.ballSpawnDelay);
            }
            else
            {
                yield return new WaitForSeconds(0.1f); // Check again soon
            }
        }
    }

    bool ShouldSpawnBall()
    {
        // Don't exceed max balls
        if (activeBalls >= gameSettings.maxBalls)
        {
            return false;
        }

        // Simple rule: always keep at least 1 ball active for testing
        // Later this will be controlled by level progression
        return activeBalls == 0;
    }

    void SpawnRandomBall()
    {
        // Random target position
        int randomPosition = Random.Range(0, gameSettings.playerPositions.Length);

        // Calculate spawn position (opposite side of court from player)
        Vector3 spawnPos = ballSpawnPoint != null ? ballSpawnPoint.position :
                          new Vector3(5f, gameSettings.playerPositions[randomPosition], 0f);

        SpawnBall(spawnPos, randomPosition);
    }

    public void SpawnBall(Vector3 fromPosition, int targetPosition, float speed = -1f)
    {
        Ball ball = GetBallFromPool();
        if (ball == null)
        {
            Debug.LogError("Failed to get ball from pool!");
            return;
        }

        // Launch the ball
        float ballSpeed = speed > 0 ? speed : gameSettings.baseBallSpeed;
        ball.LaunchBall(fromPosition, targetPosition, ballSpeed);

        // Add to active list
        if (!activeBallsList.Contains(ball))
        {
            activeBallsList.Add(ball);
        }

        activeBalls = activeBallsList.Count;
        ballsSpawned++;

        Debug.Log($"Ball spawned #{ballsSpawned}, active balls: {activeBalls}");
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
        // Don't immediately return to pool - let the ball's hit effect play first
        // The ball will deactivate itself after the visual effect
        OnBallHit?.Invoke(ball);

        // Remove from active list but don't deactivate yet
        if (activeBallsList.Contains(ball))
        {
            activeBallsList.Remove(ball);
            activeBalls = activeBallsList.Count;
        }

        // TODO: Add scoring, effects, etc.
    }

    void HandleBallMissed(Ball ball)
    {
        Debug.Log("Ball was missed!");
        ReturnBallToPool(ball);
        OnBallMissed?.Invoke(ball);

        // TODO: Reduce lives, game over check, etc.
    }

    void HandleBallReachPlayer(Ball ball)
    {
        // Ball reached player position, final check
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