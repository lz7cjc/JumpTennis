using UnityEngine;
using UnityEngine.SceneManagement;

public class Ball : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameSettings gameSettings;

    [Header("Ball Configuration")]
    [SerializeField] private int targetPosition = 1; // 0=Top, 1=Middle, 2=Bottom
    [SerializeField] private float currentSpeed = 3f;

    [Header("Debug Info")]
    [SerializeField] private bool isActive = false;
    [SerializeField] private float distanceToPlayer = 0f;
    [SerializeField] private bool canBeHit = false;

    // Movement
    private Vector3 startPosition;
    private Vector3 targetWorldPosition;
    private float journeyLength;
    private float journeyTime;
    private float elapsedTime = 0f;

    // Components
    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;

    // References
    private PlayerController player;
    private ScoreManager scoreManager;

    // Events
    public System.Action<Ball> OnBallMissed;
    public System.Action<Ball> OnBallHit;
    public System.Action<Ball> OnBallReachPlayer;
    // NEW: Event for progressive difficulty integration
    public System.Action<GameObject> OnBallCompleted;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        trailRenderer = GetComponent<TrailRenderer>();
        player = FindFirstObjectByType<PlayerController>();
        scoreManager = FindFirstObjectByType<ScoreManager>();
    }

    void Start()
    {
        if (gameSettings == null)
        {
            Debug.LogError("Ball: GameSettings not assigned!");
        }

        if (scoreManager == null)
        {
            Debug.LogError("Ball: ScoreManager not found in scene!");
        }
    }

    void Update()
    {
        if (isActive)
        {
            UpdateMovement();
            UpdateVisualFeedback();
            CheckHitConditions();
        }
    }

    #region Ball Launch System

    public void LaunchBall(Vector3 fromPosition, int toPosition, float speed = -1f)
    {
        // Validate target position
        if (gameSettings == null || toPosition < 0 || toPosition >= gameSettings.playerPositions.Length)
        {
            Debug.LogError($"Invalid target position: {toPosition}");
            return;
        }

        // Set up ball parameters
        targetPosition = toPosition;
        currentSpeed = speed > 0 ? speed : gameSettings.baseBallSpeed;

        // Calculate trajectory
        startPosition = fromPosition;
        targetWorldPosition = new Vector3(
            player.transform.position.x,
            gameSettings.playerPositions[toPosition],
            transform.position.z
        );

        // Position at start
        transform.position = startPosition;

        // Calculate journey
        journeyLength = Vector3.Distance(startPosition, targetWorldPosition);
        journeyTime = journeyLength / currentSpeed;

        // Reset state
        elapsedTime = 0f;
        isActive = true;
        canBeHit = false;

        // Make sure ball is visible
        gameObject.SetActive(true);

        // Set visual feedback
        SetupVisualFeedback();

        Debug.Log($"Ball launched to {GetPositionName(toPosition)} position, journey time: {journeyTime:F2}s");
        Debug.Log($"Ball activated: {gameObject.activeInHierarchy}");
        Debug.Log($"Ball position: {transform.position}");
    }

    #endregion

    #region Movement System

    void UpdateMovement()
    {
        elapsedTime += Time.deltaTime;
        float progress = elapsedTime / journeyTime;

        if (progress >= 1f)
        {
            // Ball reached player position
            transform.position = targetWorldPosition;
            OnBallReachPlayer?.Invoke(this);
            CheckFinalHit();
        }
        else
        {
            // Smooth arc trajectory
            Vector3 currentPos = Vector3.Lerp(startPosition, targetWorldPosition, progress);

            // Add arc effect (parabolic trajectory)
            float arcHeight = 0.5f; // Height of the arc
            float arcProgress = 4f * progress * (1f - progress); // Parabolic curve
            currentPos.y += arcHeight * arcProgress;

            transform.position = currentPos;

            // Update hit window
            UpdateHitWindow(progress);
        }
    }

    void UpdateHitWindow(float progress)
    {
        // Use GameSettings values for hit window
        float hitWindowStart = gameSettings.hitWindowStart; // 70% default
        float hitWindowEnd = gameSettings.hitWindowEnd;     // 100% default

        bool wasHittable = canBeHit;
        canBeHit = progress >= hitWindowStart && progress <= hitWindowEnd;

        // Visual feedback when entering hit window
        if (canBeHit && !wasHittable)
        {
            Debug.Log("Ball entered hit window!");
        }
    }

    #endregion

    #region Hit Detection

    void CheckHitConditions()
    {
        if (!canBeHit || player == null) return;

        // Calculate distance to player
        distanceToPlayer = Vector3.Distance(transform.position, player.GetCurrentWorldPosition());

        // Check if player is in correct position
        bool playerInCorrectPosition = player.GetCurrentPosition() == targetPosition;
        bool playerCanHit = player.CanMove(); // Player not moving

        // Listen for hit attempt from player
        // (This will be triggered by PlayerController when player presses hit button)
    }

    public bool TryHit(int playerPosition)
    {
        if (!canBeHit)
        {
            Debug.Log("Ball not in hit window!");
            // Add visual feedback for early/late hits
            StartCoroutine(ShowMissEffect());
            return false;
        }

        if (playerPosition != targetPosition)
        {
            Debug.Log($"Wrong position! Ball needs {GetPositionName(targetPosition)}, player at {GetPositionName(playerPosition)}");
            // Add visual feedback for wrong position
            StartCoroutine(ShowWrongPositionEffect());
            return false;
        }

        // Successful hit!
        bool isPerfectTiming = IsInPerfectTimingWindow();

        // Register hit with ScoreManager
        if (scoreManager != null)
        {
            scoreManager.RegisterHit(isPerfectTiming);
        }

        OnBallHit?.Invoke(this);

        // NEW: Trigger completion event for BallManager
        OnBallCompleted?.Invoke(gameObject);

        Debug.Log($"Perfect hit at {GetPositionName(targetPosition)}! Perfect timing: {isPerfectTiming}");

        // Add visual feedback for successful hit
        StartCoroutine(ShowHitEffect());

        return true;
    }

    void CheckFinalHit()
    {
        if (!canBeHit)
        {
            // Player missed the ball completely - register miss with ScoreManager
            if (scoreManager != null)
            {
                scoreManager.RegisterMiss();
            }

            OnBallMissed?.Invoke(this);

            // ADD THIS LINE - Trigger completion event for BallManager  
            OnBallCompleted?.Invoke(gameObject);

            Debug.Log($"Ball missed at {GetPositionName(targetPosition)} position!");

            // Add visual feedback for complete miss
            StartCoroutine(ShowCompletelyMissedEffect());
            return;
        }

        DeactivateBall();
    }
    private bool IsInPerfectTimingWindow()
    {
        if (gameSettings == null) return false;

        float progress = GetProgress();
        float perfectWindowStart = gameSettings.hitWindowEnd - (gameSettings.perfectTimingWindow / journeyTime);
        float perfectWindowEnd = gameSettings.hitWindowEnd;

        return progress >= perfectWindowStart && progress <= perfectWindowEnd;
    }

    #endregion

    #region Visual Feedback Effects

    System.Collections.IEnumerator ShowHitEffect()
    {
        // Stop the ball from moving first
        isActive = false;

        // Ball explodes/flashes when hit successfully
        if (spriteRenderer != null)
        {
            // Flash bright white using GameSettings color
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = gameSettings.hitSuccessColor;
            transform.localScale = Vector3.one * gameSettings.hitScaleMultiplier;

            yield return new WaitForSeconds(gameSettings.hitFlashDuration);

            // Fade out
            for (int i = 0; i < 10; i++)
            {
                Color fadeColor = Color.Lerp(gameSettings.hitSuccessColor, Color.clear, i / 10f);
                spriteRenderer.color = fadeColor;
                transform.localScale = Vector3.Lerp(Vector3.one * gameSettings.hitScaleMultiplier, Vector3.one * 2f, i / 10f);
                yield return new WaitForSeconds(0.05f);
            }
        }

        DeactivateBall(); // Remove ball after effect
    }

    System.Collections.IEnumerator ShowMissEffect()
    {
        // Ball flashes red when timing is wrong
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            for (int i = 0; i < 3; i++)
            {
                spriteRenderer.color = gameSettings.hitFailColor;
                yield return new WaitForSeconds(0.1f);
                spriteRenderer.color = originalColor;
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    System.Collections.IEnumerator ShowWrongPositionEffect()
    {
        // Ball flashes magenta when position is wrong
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            for (int i = 0; i < 3; i++)
            {
                spriteRenderer.color = gameSettings.wrongPositionColor;
                yield return new WaitForSeconds(0.1f);
                spriteRenderer.color = originalColor;
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    System.Collections.IEnumerator ShowCompletelyMissedEffect()
    {
        // Ball fades to gray when completely missed
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            for (int i = 0; i < 10; i++)
            {
                Color fadeColor = Color.Lerp(originalColor, gameSettings.missColor, i / 10f);
                spriteRenderer.color = fadeColor;
                yield return new WaitForSeconds(0.05f);
            }

            yield return new WaitForSeconds(0.5f);
        }

        DeactivateBall();
    }

    #endregion

    #region Original Visual Feedback

    void SetupVisualFeedback()
    {
        if (gameSettings == null) return;

        // Set ball color based on target position
        Color ballColor = GetPositionColor(targetPosition);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = ballColor;
        }

        if (trailRenderer != null)
        {
            trailRenderer.startColor = ballColor;
            trailRenderer.endColor = new Color(ballColor.r, ballColor.g, ballColor.b, 0f);
            trailRenderer.time = gameSettings.trailLifetime;
        }
    }

    void UpdateVisualFeedback()
    {
        if (spriteRenderer != null && canBeHit)
        {
            // Flash when in hit window
            float flash = Mathf.Sin(Time.time * 10f) * 0.3f + 0.7f;
            Color baseColor = GetPositionColor(targetPosition);
            spriteRenderer.color = new Color(baseColor.r * flash, baseColor.g * flash, baseColor.b * flash, 1f);
        }
    }

    Color GetPositionColor(int position)
    {
        if (gameSettings == null) return Color.white;

        switch (position)
        {
            case 0: return gameSettings.topPositionColor;
            case 1: return gameSettings.middlePositionColor;
            case 2: return gameSettings.bottomPositionColor;
            default: return Color.white;
        }
    }

    #endregion

    #region Ball Management

    public void DeactivateBall()
    {
        isActive = false;
        canBeHit = false;
        elapsedTime = 0f;

        // Hide ball (or return to pool)
        gameObject.SetActive(false);
    }

    public void ResetBall()
    {
        DeactivateBall();
        transform.position = Vector3.zero;
        transform.localScale = Vector3.one; // Reset scale

        if (trailRenderer != null)
        {
            trailRenderer.Clear();
        }

        if (spriteRenderer != null)
        {
            // Reset color to default
            spriteRenderer.color = Color.white;
        }
    }

    #endregion

    #region Public Interface

    public bool IsActive() => isActive;
    public bool CanBeHit() => canBeHit;
    public int GetTargetPosition() => targetPosition;
    public float GetDistanceToPlayer() => distanceToPlayer;
    public float GetProgress() => journeyTime > 0 ? elapsedTime / journeyTime : 0f;

    // NEW: Progressive difficulty methods
    public void SetSpeed(float speed)
    {
        currentSpeed = speed;
    }

    public float GetCurrentSpeed()
    {
        return currentSpeed;
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
        if (!isActive || !gameSettings.showDebugGizmos) return;

        // Draw trajectory line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPosition, targetWorldPosition);

        // Draw target position
        Gizmos.color = GetPositionColor(targetPosition);
        Gizmos.DrawWireSphere(targetWorldPosition, 0.3f);

        // Draw hit window
        if (canBeHit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }

    #endregion
}