using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameSettings gameSettings;

    [Header("Debug Info")]
    [SerializeField] private int currentPosition = 1; // 0=Top, 1=Middle, 2=Bottom
    [SerializeField] private bool isMoving = false;

    // Movement
    private Vector3 targetPosition;
    private Vector3 startPosition;
    private float moveTimer = 0f;
    private bool canMove = true;

    // Visual feedback
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    // Events for other systems
    public System.Action<int> OnPositionChanged; // Notify other systems of position change
    public System.Action OnHitAttempt; // When player tries to hit ball

    void Awake()
    {
        // Get components
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    void Start()
    {
        // Validate GameSettings
        if (gameSettings == null)
        {
            Debug.LogError("PlayerController: GameSettings not assigned!");
            return;
        }

        // Initialize position (start in middle)
        currentPosition = 1;
        SetPositionImmediate(currentPosition);
    }

    void Update()
    {
        HandleInput();
        UpdateMovement();
        UpdateVisuals();
    }

    #region Input Handling

    void HandleInput()
    {
        if (!canMove || gameSettings == null) return;

        // Mouse/Touch input for Windows development
        if (Input.GetMouseButtonDown(0))
        {
            HandleTouchInput();
        }

        // FIXED: Sequential arrow key movement
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            // Move up one position (2→1→0)
            int newPosition = Mathf.Max(0, currentPosition - 1);
            MoveToPosition(newPosition);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            // Move down one position (0→1→2)
            int maxPosition = gameSettings.playerPositions.Length - 1;
            int newPosition = Mathf.Min(maxPosition, currentPosition + 1);
            MoveToPosition(newPosition);
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            MoveToPosition(1); // Middle
        }

        // Hit attempt (for testing)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(1))
        {
            AttemptHit();
        }
    }

    void HandleTouchInput()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float screenHeight = Camera.main.orthographicSize * 2;
        float screenThird = screenHeight / 3f;

        // Determine which third of screen was clicked
        float relativeY = mouseWorldPos.y + Camera.main.orthographicSize; // Convert to 0-screenHeight range

        if (relativeY > screenThird * 2)
        {
            MoveToPosition(0); // Top third = Top position
        }
        else if (relativeY < screenThird)
        {
            MoveToPosition(2); // Bottom third = Bottom position
        }
        else
        {
            MoveToPosition(1); // Middle third = Middle position
        }
    }

    #endregion

    #region Movement System

    public void MoveToPosition(int positionIndex)
    {
        // Validate position
        if (positionIndex < 0 || positionIndex >= gameSettings.playerPositions.Length)
        {
            Debug.LogWarning($"Invalid position index: {positionIndex}");
            return;
        }

        // Don't move if already at target position
        if (currentPosition == positionIndex && !isMoving)
        {
            return;
        }

        // Start movement
        startPosition = transform.position;
        targetPosition = new Vector3(transform.position.x, gameSettings.playerPositions[positionIndex], transform.position.z);
        currentPosition = positionIndex;
        isMoving = true;
        moveTimer = 0f;

        // Notify other systems
        OnPositionChanged?.Invoke(currentPosition);

        Debug.Log($"Moving to position {positionIndex} ({GetPositionName(positionIndex)})");
    }

    void UpdateMovement()
    {
        if (!isMoving || gameSettings == null) return;

        // Update timer
        moveTimer += Time.deltaTime;
        float progress = moveTimer / gameSettings.playerMovementSpeed;

        if (progress >= 1f)
        {
            // Movement complete
            transform.position = targetPosition;
            isMoving = false;
            moveTimer = 0f;
        }
        else
        {
            // Smooth movement using easing
            float easedProgress = EaseInOutQuad(progress);
            transform.position = Vector3.Lerp(startPosition, targetPosition, easedProgress);
        }
    }

    void SetPositionImmediate(int positionIndex)
    {
        if (gameSettings == null) return;

        currentPosition = positionIndex;
        transform.position = new Vector3(transform.position.x, gameSettings.playerPositions[positionIndex], transform.position.z);
        isMoving = false;
        moveTimer = 0f;
    }

    // Smooth easing function
    float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    #endregion

    #region Hit System

    public void AttemptHit()
    {
        if (isMoving)
        {
            Debug.Log("Cannot hit while moving!");
            return;
        }

        Debug.Log($"Hit attempt from {GetPositionName(currentPosition)}!");
        OnHitAttempt?.Invoke();

        // Visual feedback for hit attempt
        StartCoroutine(HitFeedback());
    }

    System.Collections.IEnumerator HitFeedback()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
        }
    }

    #endregion

    #region Visual Updates

    void UpdateVisuals()
    {
        // Change color based on position (for testing)
        if (spriteRenderer != null && gameSettings != null && !isMoving)
        {
            switch (currentPosition)
            {
                case 0: // Top
                    spriteRenderer.color = gameSettings.topPositionColor;
                    break;
                case 1: // Middle
                    spriteRenderer.color = gameSettings.middlePositionColor;
                    break;
                case 2: // Bottom
                    spriteRenderer.color = gameSettings.bottomPositionColor;
                    break;
            }
        }
    }

    #endregion

    #region Public Interface

    public int GetCurrentPosition() => currentPosition;
    public bool IsMoving() => isMoving;
    public bool CanMove() => canMove && !isMoving;

    public void SetCanMove(bool canMove)
    {
        this.canMove = canMove;
    }

    public Vector3 GetCurrentWorldPosition()
    {
        return transform.position;
    }

    public Vector3 GetPositionWorldCoordinate(int positionIndex)
    {
        if (gameSettings == null || positionIndex < 0 || positionIndex >= gameSettings.playerPositions.Length)
        {
            return transform.position;
        }

        return new Vector3(transform.position.x, gameSettings.playerPositions[positionIndex], transform.position.z);
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
        if (gameSettings == null) return;

        // Draw all possible positions
        Gizmos.color = Color.yellow;
        for (int i = 0; i < gameSettings.playerPositions.Length; i++)
        {
            Vector3 pos = new Vector3(transform.position.x, gameSettings.playerPositions[i], transform.position.z);
            Gizmos.DrawWireSphere(pos, 0.2f);

            // Highlight current position
            if (i == currentPosition)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(pos, 0.15f);
                Gizmos.color = Color.yellow;
            }
        }

        // Draw movement target
        if (isMoving)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }

    #endregion
}