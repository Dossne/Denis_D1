using System.Collections.Generic;
using UnityEngine;

public enum SnakeGameState
{
    Playing,
    Won,
    Lost
}

public sealed class SnakeGameController : MonoBehaviour
{
    private static SnakeGameController instance;
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };
    private const int MinX = -10;
    private const int MaxX = 10;
    private const int MinY = -16;
    private const int MaxY = 16;

    private const int BaseApplesCount = 6;
    private const int ApplesPerLevel = 2;

    private const float BaseMoveInterval = 0.22f;
    private const float MinMoveInterval = 0.08f;
    private const float SpeedUpFactor = 0.96f;
    private const float HoldBoostSpeedMultiplier = 1.5f;
    private const float MaxInteriorWallCoverage = 0.20f;
    private const int MinInteriorWallLength = 2;
    private const int MaxInteriorWallLength = 7;
    private const int BugStartLevel = 3;
    private const int BugMaxActive = 3;
    private const float BugBaseSpawnInterval = 10f;
    private const float BugMinSpawnInterval = 5f;
    private const float BugSpawnStepPerLevel = 0.5f;
    private const float AppleVisualScale = 1.2f;
    private const float BugVisualScale = 1.2f;
    private const int HeartStartLevel = 2;
    private const float HeartRespawnDelay = 20f;
    private const float HeartShieldDuration = 5f;
    private const float HeartBlinkInterval = 0.5f;

    private const float GameplayViewportBottom = 0.24f;
    private const float GameplayViewportHeight = 0.62f;
    private const float GameplayViewportTargetAspect = 0.58f;
    private const string SnakeBodyEmoji = "\uD83D\uDFE9";
    private static readonly Color32[] SnakeColorPalette =
    {
        new Color32(126, 211, 33, 255),
        new Color32(88, 220, 150, 255),
        new Color32(72, 196, 255, 255),
        new Color32(184, 132, 255, 255),
        new Color32(255, 208, 94, 255),
        new Color32(255, 143, 199, 255)
    };

    private static Sprite wallFallbackSprite;
    private static Sprite appleFallbackSprite;
    private static Sprite snakeHeadOpenFallbackSprite;
    private static Sprite snakeHeadClosedFallbackSprite;
    private static Sprite snakeBodyFallbackSprite;
    private static Sprite bugFallbackSprite;
    private static Sprite heartFallbackSprite;

    private readonly List<Vector2Int> snakeSegments = new List<Vector2Int>();
    private readonly List<SnakeSegmentView> snakeViews = new List<SnakeSegmentView>();
    private readonly Dictionary<Vector2Int, GameObject> appleObjects = new Dictionary<Vector2Int, GameObject>();
    private readonly List<GameObject> wallObjects = new List<GameObject>();
    private readonly HashSet<Vector2Int> interiorWallCells = new HashSet<Vector2Int>();
    private readonly List<GameObject> interiorWallObjects = new List<GameObject>();
    private readonly List<BugView> bugViews = new List<BugView>();
    private readonly HashSet<Vector2Int> borderWallCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, GameObject> borderWallByCell = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<Vector2Int, GameObject> interiorWallByCell = new Dictionary<Vector2Int, GameObject>();

    private readonly System.Random random = new System.Random();
    private Font wallEmojiFont;

    private Transform worldRoot;
    private Transform wallRoot;
    private Transform snakeRoot;
    private Transform appleRoot;
    private Transform bugRoot;
    private Transform heartRoot;

    private Camera mainCamera;
    private Camera backgroundCamera;
    private Rect gameplayViewport = new Rect(0.2f, GameplayViewportBottom, 0.6f, GameplayViewportHeight);
    private int cachedScreenWidth;
    private int cachedScreenHeight;

    private Vector2Int currentDirection = Vector2Int.up;
    private Vector2Int queuedDirection = Vector2Int.up;

    private SnakeGameState state;
    private int currentLevel;
    private int applesTarget;
    private float baseMoveInterval;
    private float moveInterval;
    private float moveTimer;
    private float timeRemaining;
    private float bugSpawnInterval;
    private float bugSpawnTimer;
    private float bugMoveSpeed;
    private int snakeColorIndex;
    private Color32 currentSnakeColor = new Color32(126, 211, 33, 255);
    private int uiHeldDirectionsCount;
    private bool isKeyboardDirectionHeld;
    private bool isBoostActive;
    private float boostSourceMoveInterval;
    private GameObject heartObject;
    private Vector2Int heartCell;
    private float heartRespawnTimer;
    private float heartShieldRemaining;
    private float heartBlinkTimer;
    private bool isHeartActiveOnField;
    private bool isSnakeVisible = true;
    private string statusMessage = string.Empty;

    public SnakeGameState State => state;
    public int CurrentLevel => currentLevel;
    public int ApplesRemaining => appleObjects.Count;
    public int ApplesTarget => applesTarget;
    public float TimeRemaining => Mathf.Max(0f, timeRemaining);
    public string StatusMessage => statusMessage;
    public Rect GameplayViewport => gameplayViewport;
    private sealed class SnakeSegmentView
    {
        public GameObject Root;
        public SpriteRenderer FallbackRenderer;
        public TextMesh EmojiText;
        public MeshRenderer EmojiRenderer;
    }

    private sealed class BugView
    {
        public GameObject Root;
        public TextMesh EmojiText;
        public MeshRenderer EmojiRenderer;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        ConfigureCamera();
        CreateRoots();
        BeginLevel(1);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        EnsureCameraViewport();

        if (state == SnakeGameState.Won)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                StartNextLevel();
            }

            UpdateSnakeHeadVisual();
            return;
        }

        if (state == SnakeGameState.Lost)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                RestartLevel();
            }

            UpdateSnakeHeadVisual();
            return;
        }

        HandleKeyboardInput();
        UpdateHeartState(Time.deltaTime);
        UpdateBugs(Time.deltaTime);
        if (state != SnakeGameState.Playing)
        {
            UpdateSnakeHeadVisual();
            return;
        }

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            LoseLevel("Time is up");
            UpdateSnakeHeadVisual();
            return;
        }

        moveTimer -= Time.deltaTime;
        while (moveTimer <= 0f)
        {
            moveTimer += moveInterval;
            StepSnake();

            if (state != SnakeGameState.Playing)
            {
                break;
            }
        }

        UpdateSnakeHeadVisual();
    }
    public void QueueUp()
    {
        QueueDirection(Vector2Int.up);
    }

    public void QueueDown()
    {
        QueueDirection(Vector2Int.down);
    }

    public void QueueLeft()
    {
        QueueDirection(Vector2Int.left);
    }

    public void QueueRight()
    {
        QueueDirection(Vector2Int.right);
    }

    public void NotifyUiDirectionPressed()
    {
        if (state != SnakeGameState.Playing)
        {
            return;
        }

        uiHeldDirectionsCount++;
        RegisterDirectionPressForBoost();
    }

    public void NotifyUiDirectionReleased()
    {
        uiHeldDirectionsCount = Mathf.Max(0, uiHeldDirectionsCount - 1);
        UpdateDirectionBoostState();
    }

    public void StartNextLevel()
    {
        if (state != SnakeGameState.Won)
        {
            return;
        }

        BeginLevel(currentLevel + 1);
    }

    public void RestartLevel()
    {
        if (state != SnakeGameState.Lost)
        {
            return;
        }

        BeginLevel(currentLevel);
    }

    private void BeginLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        applesTarget = CalculateApplesCount(currentLevel);
        timeRemaining = CalculateTimeLimit(applesTarget);

        baseMoveInterval = Mathf.Max(MinMoveInterval, BaseMoveInterval - (currentLevel - 1) * 0.005f);
        moveInterval = baseMoveInterval;
        moveTimer = moveInterval;

        state = SnakeGameState.Playing;
        statusMessage = string.Empty;

        currentDirection = Vector2Int.up;
        queuedDirection = Vector2Int.up;
        ResetDirectionBoostState();

        ResetSnakeColor();
        SetSnakeVisibility(true);
        ResetSnake();
        ClearHeartObject();
        ClearInteriorWalls();
        BuildWalls();
        GenerateInteriorWalls(currentLevel);
        SpawnApples(applesTarget);
        InitializeBugSystem(currentLevel, baseMoveInterval);
        InitializeHeartSystem();
    }
    private void InitializeBugSystem(int level, float levelStartMoveInterval)
    {
        ClearBugs();

        if (level < BugStartLevel || levelStartMoveInterval <= 0f)
        {
            bugSpawnInterval = float.PositiveInfinity;
            bugSpawnTimer = float.PositiveInfinity;
            bugMoveSpeed = 0f;
            return;
        }

        bugSpawnInterval = Mathf.Max(BugMinSpawnInterval, BugBaseSpawnInterval - (level - BugStartLevel) * BugSpawnStepPerLevel);
        bugSpawnTimer = bugSpawnInterval;
        bugMoveSpeed = 1f / levelStartMoveInterval;
    }

    private void UpdateBugs(float deltaTime)
    {
        if (currentLevel < BugStartLevel || bugMoveSpeed <= 0f)
        {
            return;
        }

        for (int i = bugViews.Count - 1; i >= 0; i--)
        {
            BugView bug = bugViews[i];
            if (bug == null || bug.Root == null)
            {
                bugViews.RemoveAt(i);
                continue;
            }

            Vector3 position = bug.Root.transform.localPosition;
            position.y -= bugMoveSpeed * deltaTime;
            bug.Root.transform.localPosition = position;

            if (position.y < MinY - 0.75f)
            {
                RemoveBugAt(i);
            }
        }

        if (snakeSegments.Count > 0 && IsHeadCollidingWithBug(snakeSegments[0]))
        {
            if (IsHeartShieldActive())
            {
                TryRemoveBugsAtCell(snakeSegments[0]);
            }
            else
            {
                LoseLevel("You hit a bug");
                return;
            }
        }

        bugSpawnTimer -= deltaTime;
        while (bugSpawnTimer <= 0f)
        {
            bugSpawnTimer += bugSpawnInterval;
            if (bugViews.Count < BugMaxActive)
            {
                SpawnBug();
            }
        }
    }
    private void SpawnBug()
    {
        if (bugRoot == null)
        {
            return;
        }

        int x = random.Next(MinX + 1, MaxX);
        BugView bug = CreateBugView(new Vector3(x, MaxY, 0f));
        bugViews.Add(bug);
    }

    private bool IsHeadCollidingWithBug(Vector2Int headCell)
    {
        return IsBugAtCell(headCell);
    }
    private void RemoveBugAt(int index)
    {
        if (index < 0 || index >= bugViews.Count)
        {
            return;
        }

        BugView bug = bugViews[index];
        bugViews.RemoveAt(index);

        if (bug != null && bug.Root != null)
        {
            Destroy(bug.Root);
        }
    }

    private void ClearBugs()
    {
        for (int i = 0; i < bugViews.Count; i++)
        {
            BugView bug = bugViews[i];
            if (bug != null && bug.Root != null)
            {
                Destroy(bug.Root);
            }
        }

        bugViews.Clear();
    }
    private void UpdateHeartState(float deltaTime)
    {
        if (heartShieldRemaining > 0f)
        {
            heartShieldRemaining = Mathf.Max(0f, heartShieldRemaining - deltaTime);
            heartBlinkTimer -= deltaTime;

            while (heartShieldRemaining > 0f && heartBlinkTimer <= 0f)
            {
                heartBlinkTimer += HeartBlinkInterval;
                SetSnakeVisibility(!isSnakeVisible);
            }

            if (heartShieldRemaining <= 0f)
            {
                heartBlinkTimer = HeartBlinkInterval;
                SetSnakeVisibility(true);
            }
        }

        if (currentLevel < HeartStartLevel || isHeartActiveOnField)
        {
            return;
        }

        if (heartRespawnTimer > 0f && !float.IsPositiveInfinity(heartRespawnTimer))
        {
            heartRespawnTimer -= deltaTime;
        }

        if (heartRespawnTimer <= 0f)
        {
            if (TrySpawnHeart())
            {
                heartRespawnTimer = float.PositiveInfinity;
            }
            else
            {
                heartRespawnTimer = 0f;
            }
        }
    }

    private void InitializeHeartSystem()
    {
        heartRespawnTimer = float.PositiveInfinity;
        heartShieldRemaining = 0f;
        heartBlinkTimer = HeartBlinkInterval;
        SetSnakeVisibility(true);

        if (currentLevel < HeartStartLevel)
        {
            return;
        }

        heartRespawnTimer = 0f;
        TrySpawnHeart();
    }

    private bool TrySpawnHeart()
    {
        if (heartRoot == null || snakeSegments.Count == 0 || currentLevel < HeartStartLevel)
        {
            return false;
        }

        var occupiedBySnake = new HashSet<Vector2Int>(snakeSegments);
        List<Vector2Int> candidates = GetReachableFreeCells(snakeSegments[0], occupiedBySnake);
        if (candidates.Count == 0)
        {
            return false;
        }

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            Vector2Int cell = candidates[i];
            if (cell.x <= MinX || cell.x >= MaxX || cell.y <= MinY || cell.y >= MaxY)
            {
                candidates.RemoveAt(i);
                continue;
            }

            if (appleObjects.ContainsKey(cell) || IsBugAtCell(cell) || occupiedBySnake.Contains(cell) || IsWallCell(cell))
            {
                candidates.RemoveAt(i);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        int index = random.Next(0, candidates.Count);
        heartCell = candidates[index];
        heartObject = CreateHeartObject(heartCell);
        isHeartActiveOnField = heartObject != null;
        return isHeartActiveOnField;
    }

    private GameObject CreateHeartObject(Vector2Int cell)
    {
        var newHeart = new GameObject("Heart");
        newHeart.transform.SetParent(heartRoot, false);
        newHeart.transform.localPosition = new Vector3(cell.x, cell.y, 0f);
        newHeart.transform.localScale = Vector3.one * AppleVisualScale;

        var fallbackRenderer = newHeart.AddComponent<SpriteRenderer>();
        fallbackRenderer.sprite = GetHeartFallbackSprite();
        fallbackRenderer.color = Color.white;
        fallbackRenderer.sortingOrder = 5;

        var emojiObject = new GameObject("Emoji");
        emojiObject.transform.SetParent(newHeart.transform, false);
        emojiObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

        var textMesh = emojiObject.AddComponent<TextMesh>();
        textMesh.text = "\u2764\uFE0F";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.22f;
        textMesh.fontSize = 96;
        textMesh.color = Color.white;
        textMesh.richText = false;

        Font emojiFont = GetWallEmojiFont();
        bool canRenderEmoji = emojiFont != null;
        if (canRenderEmoji)
        {
            textMesh.font = emojiFont;
        }

        MeshRenderer emojiRenderer = emojiObject.GetComponent<MeshRenderer>();
        if (emojiRenderer != null)
        {
            if (canRenderEmoji)
            {
                emojiRenderer.material = emojiFont.material;
            }

            emojiRenderer.sortingOrder = 6;
        }

        fallbackRenderer.enabled = true;
        return newHeart;
    }

    private void CollectHeart()
    {
        if (!isHeartActiveOnField)
        {
            return;
        }

        ClearHeartObject();
        heartRespawnTimer = HeartRespawnDelay;
        heartShieldRemaining += HeartShieldDuration;
        heartBlinkTimer = HeartBlinkInterval;
        SetSnakeVisibility(true);
    }

    private void ClearHeartObject()
    {
        if (heartObject != null)
        {
            Destroy(heartObject);
        }

        heartObject = null;
        heartCell = new Vector2Int(int.MinValue, int.MinValue);
        isHeartActiveOnField = false;
    }

    private static bool IsBorderCell(Vector2Int cell)
    {
        return cell.x == MinX || cell.x == MaxX || cell.y == MinY || cell.y == MaxY;
    }

    private static bool IsOutsidePlayableBounds(Vector2Int cell)
    {
        return cell.x < MinX || cell.x > MaxX || cell.y < MinY || cell.y > MaxY;
    }

    private bool IsHeartShieldActive()
    {
        return heartShieldRemaining > 0f;
    }

    private bool IsBugAtCell(Vector2Int cell)
    {
        for (int i = 0; i < bugViews.Count; i++)
        {
            BugView bug = bugViews[i];
            if (bug == null || bug.Root == null)
            {
                continue;
            }

            Vector3 position = bug.Root.transform.localPosition;
            var bugCell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
            if (bugCell == cell)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryRemoveBugsAtCell(Vector2Int cell)
    {
        bool removed = false;
        for (int i = bugViews.Count - 1; i >= 0; i--)
        {
            BugView bug = bugViews[i];
            if (bug == null || bug.Root == null)
            {
                continue;
            }

            Vector3 position = bug.Root.transform.localPosition;
            var bugCell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
            if (bugCell != cell)
            {
                continue;
            }

            RemoveBugAt(i);
            removed = true;
        }

        return removed;
    }

    private int CalculateApplesCount(int level)
    {
        return BaseApplesCount + (level - 1) * ApplesPerLevel;
    }

    private float CalculateTimeLimit(int applesCount)
    {
        return Mathf.Clamp(30f + applesCount * 5f, 30f, 90f);
    }

    private void ResetSnakeColor()
    {
        snakeColorIndex = 0;
        currentSnakeColor = SnakeColorPalette[snakeColorIndex];
    }

    private void AdvanceSnakeColor()
    {
        if (SnakeColorPalette.Length == 0)
        {
            return;
        }

        snakeColorIndex = (snakeColorIndex + 1) % SnakeColorPalette.Length;
        currentSnakeColor = SnakeColorPalette[snakeColorIndex];
    }

    private void ResetSnake()
    {
        snakeSegments.Clear();

        int startY = MinY + 3;
        snakeSegments.Add(new Vector2Int(0, startY));
        snakeSegments.Add(new Vector2Int(0, startY - 1));
        snakeSegments.Add(new Vector2Int(0, startY - 2));

        SyncSnakeRenderers();
    }

    private void SpawnApples(int count)
    {
        foreach (var appleObject in appleObjects.Values)
        {
            if (appleObject != null)
            {
                Destroy(appleObject);
            }
        }

        appleObjects.Clear();

        if (count <= 0 || snakeSegments.Count == 0)
        {
            return;
        }

        var occupiedCells = new HashSet<Vector2Int>(snakeSegments);
        List<Vector2Int> reachableCells = GetReachableFreeCells(snakeSegments[0], occupiedCells);
        if (reachableCells.Count == 0)
        {
            return;
        }

        int spawnCount = Mathf.Min(count, reachableCells.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            int swapIndex = random.Next(i, reachableCells.Count);
            Vector2Int temp = reachableCells[i];
            reachableCells[i] = reachableCells[swapIndex];
            reachableCells[swapIndex] = temp;

            Vector2Int position = reachableCells[i];
            appleObjects[position] = CreateAppleObject(position);
        }

        if (spawnCount < count)
        {
            Debug.LogWarning($"Could only place {spawnCount} apples out of requested {count} on level {currentLevel}.");
        }
    }

    private List<Vector2Int> GetReachableFreeCells(Vector2Int startCell, HashSet<Vector2Int> occupiedCells)
    {
        var reachableCells = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();

        visited.Add(startCell);
        queue.Enqueue(startCell);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int next = current + CardinalDirections[i];
                if (visited.Contains(next) || IsWallCell(next) || occupiedCells.Contains(next))
                {
                    continue;
                }

                visited.Add(next);
                queue.Enqueue(next);
                reachableCells.Add(next);
            }
        }

        return reachableCells;
    }
    private GameObject CreateAppleObject(Vector2Int cell)
    {
        var appleObject = new GameObject("Apple");
        appleObject.transform.SetParent(appleRoot, false);
        appleObject.transform.localPosition = new Vector3(cell.x, cell.y, 0f);
        appleObject.transform.localScale = Vector3.one * AppleVisualScale;

        var fallbackRenderer = appleObject.AddComponent<SpriteRenderer>();
        fallbackRenderer.sprite = GetAppleFallbackSprite();
        fallbackRenderer.color = Color.white;
        fallbackRenderer.sortingOrder = 5;

        var emojiObject = new GameObject("Emoji");
        emojiObject.transform.SetParent(appleObject.transform, false);
        emojiObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

        var textMesh = emojiObject.AddComponent<TextMesh>();
        textMesh.text = "\uD83C\uDF4E";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.22f;
        textMesh.fontSize = 96;
        textMesh.color = Color.white;
        textMesh.richText = false;

        Font emojiFont = GetWallEmojiFont();
        bool canRenderEmoji = emojiFont != null;

        if (canRenderEmoji)
        {
            textMesh.font = emojiFont;
        }

        MeshRenderer emojiRenderer = emojiObject.GetComponent<MeshRenderer>();
        if (emojiRenderer != null)
        {
            if (canRenderEmoji)
            {
                emojiRenderer.material = emojiFont.material;
            }

            emojiRenderer.sortingOrder = 6;
        }

        fallbackRenderer.enabled = true;

        return appleObject;
    }

    private void StepSnake()
    {
        currentDirection = queuedDirection;

        Vector2Int nextHead = snakeSegments[0] + currentDirection;
        if (!TryResolveBorderTransition(ref nextHead))
        {
            return;
        }

        if (IsWallCell(nextHead))
        {
            if (!IsHeartShieldActive() || !RemoveWallAt(nextHead))
            {
                LoseLevel("You hit the wall");
                return;
            }
        }

        bool willCollectHeart = isHeartActiveOnField && nextHead == heartCell;
        bool hasShieldForThisStep = IsHeartShieldActive() || willCollectHeart;

        if (IsHeadCollidingWithBug(nextHead))
        {
            if (hasShieldForThisStep)
            {
                TryRemoveBugsAtCell(nextHead);
            }
            else
            {
                LoseLevel("You hit a bug");
                return;
            }
        }

        bool willEatApple = appleObjects.ContainsKey(nextHead);
        int tailIndex = snakeSegments.Count - 1;

        for (int i = 0; i < snakeSegments.Count; i++)
        {
            if (!willEatApple && i == tailIndex)
            {
                continue;
            }

            if (snakeSegments[i] == nextHead)
            {
                LoseLevel("You hit yourself");
                return;
            }
        }

        snakeSegments.Insert(0, nextHead);

        if (willEatApple)
        {
            EatApple(nextHead);
        }
        else
        {
            snakeSegments.RemoveAt(tailIndex + 1);
        }

        if (willCollectHeart)
        {
            CollectHeart();
        }

        SyncSnakeRenderers();

        if (appleObjects.Count == 0)
        {
            WinLevel();
        }
    }
    private void EatApple(Vector2Int appleCell)
    {
        if (appleObjects.TryGetValue(appleCell, out var appleObject) && appleObject != null)
        {
            Destroy(appleObject);
        }

        appleObjects.Remove(appleCell);
        AdvanceSnakeColor();
        baseMoveInterval = Mathf.Max(MinMoveInterval, baseMoveInterval * SpeedUpFactor);
        if (!isBoostActive)
        {
            moveInterval = baseMoveInterval;
        }
    }

    private void WinLevel()
    {
        ResetDirectionBoostState();
        heartShieldRemaining = 0f;
        heartBlinkTimer = HeartBlinkInterval;
        SetSnakeVisibility(true);
        state = SnakeGameState.Won;
        statusMessage = "Level complete";
    }

    private void LoseLevel(string reason)
    {
        ResetDirectionBoostState();
        heartShieldRemaining = 0f;
        heartBlinkTimer = HeartBlinkInterval;
        SetSnakeVisibility(true);
        state = SnakeGameState.Lost;
        statusMessage = reason;
    }
    private bool IsWallCell(Vector2Int cell)
    {
        if (IsOutsidePlayableBounds(cell))
        {
            return true;
        }

        return borderWallCells.Contains(cell) || interiorWallCells.Contains(cell);
    }

    private bool TryResolveBorderTransition(ref Vector2Int nextHead)
    {
        if (!IsBorderCell(nextHead))
        {
            return true;
        }

        if (borderWallCells.Contains(nextHead))
        {
            if (!IsHeartShieldActive())
            {
                LoseLevel("You hit the wall");
                return false;
            }

            RemoveBorderWallAt(nextHead);
            Vector2Int oppositeBorderCell = GetOppositeBorderCell(nextHead, currentDirection);
            RemoveBorderWallAt(oppositeBorderCell);
        }

        if (!borderWallCells.Contains(nextHead))
        {
            nextHead = GetTunnelExitCell(nextHead, currentDirection);
        }

        return true;
    }

    private static Vector2Int GetOppositeBorderCell(Vector2Int borderCell, Vector2Int direction)
    {
        if (direction == Vector2Int.left)
        {
            return new Vector2Int(MaxX, borderCell.y);
        }

        if (direction == Vector2Int.right)
        {
            return new Vector2Int(MinX, borderCell.y);
        }

        if (direction == Vector2Int.up)
        {
            return new Vector2Int(borderCell.x, MinY);
        }

        return new Vector2Int(borderCell.x, MaxY);
    }

    private static Vector2Int GetTunnelExitCell(Vector2Int borderCell, Vector2Int direction)
    {
        if (direction == Vector2Int.left)
        {
            return new Vector2Int(MaxX - 1, borderCell.y);
        }

        if (direction == Vector2Int.right)
        {
            return new Vector2Int(MinX + 1, borderCell.y);
        }

        if (direction == Vector2Int.up)
        {
            return new Vector2Int(borderCell.x, MinY + 1);
        }

        return new Vector2Int(borderCell.x, MaxY - 1);
    }

    private bool RemoveWallAt(Vector2Int cell)
    {
        if (RemoveInteriorWallAt(cell))
        {
            return true;
        }

        return RemoveBorderWallAt(cell);
    }

    private bool RemoveBorderWallAt(Vector2Int cell)
    {
        if (!borderWallCells.Remove(cell))
        {
            return false;
        }

        if (borderWallByCell.TryGetValue(cell, out var wallObject))
        {
            borderWallByCell.Remove(cell);
            wallObjects.Remove(wallObject);
            if (wallObject != null)
            {
                Destroy(wallObject);
            }
        }

        return true;
    }

    private bool RemoveInteriorWallAt(Vector2Int cell)
    {
        if (!interiorWallCells.Remove(cell))
        {
            return false;
        }

        if (interiorWallByCell.TryGetValue(cell, out var wallObject))
        {
            interiorWallByCell.Remove(cell);
            interiorWallObjects.Remove(wallObject);
            if (wallObject != null)
            {
                Destroy(wallObject);
            }
        }

        return true;
    }
    private void HandleKeyboardInput()
    {
        bool directionPressedThisFrame = false;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            QueueDirection(Vector2Int.up);
            directionPressedThisFrame = true;
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            QueueDirection(Vector2Int.down);
            directionPressedThisFrame = true;
        }
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            QueueDirection(Vector2Int.left);
            directionPressedThisFrame = true;
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            QueueDirection(Vector2Int.right);
            directionPressedThisFrame = true;
        }

        if (directionPressedThisFrame)
        {
            RegisterDirectionPressForBoost();
        }

        UpdateDirectionBoostState();
    }

    private void QueueDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
        {
            return;
        }

        if (direction + currentDirection == Vector2Int.zero)
        {
            return;
        }

        queuedDirection = direction;
    }

    private void RegisterDirectionPressForBoost()
    {
        if (state != SnakeGameState.Playing)
        {
            return;
        }

        boostSourceMoveInterval = baseMoveInterval;
        isBoostActive = true;
        moveInterval = boostSourceMoveInterval / HoldBoostSpeedMultiplier;
    }

    private void UpdateDirectionBoostState()
    {
        isKeyboardDirectionHeld = IsAnyKeyboardDirectionHeld();
        bool anyDirectionHeld = uiHeldDirectionsCount > 0 || isKeyboardDirectionHeld;

        if (anyDirectionHeld)
        {
            if (!isBoostActive)
            {
                RegisterDirectionPressForBoost();
            }

            return;
        }

        if (isBoostActive)
        {
            isBoostActive = false;
            moveInterval = baseMoveInterval;
        }
    }

    private static bool IsAnyKeyboardDirectionHeld()
    {
        return Input.GetKey(KeyCode.W)
            || Input.GetKey(KeyCode.UpArrow)
            || Input.GetKey(KeyCode.S)
            || Input.GetKey(KeyCode.DownArrow)
            || Input.GetKey(KeyCode.A)
            || Input.GetKey(KeyCode.LeftArrow)
            || Input.GetKey(KeyCode.D)
            || Input.GetKey(KeyCode.RightArrow);
    }

    private void ResetDirectionBoostState()
    {
        uiHeldDirectionsCount = 0;
        isKeyboardDirectionHeld = false;
        isBoostActive = false;
        boostSourceMoveInterval = 0f;
        moveInterval = baseMoveInterval;
    }

    private void ConfigureCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 18f;
        mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color32(17, 24, 39, 255);
        mainCamera.depth = -1f;
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.farClipPlane = 100f;

        EnsureBackgroundCamera();
        ConfigurePortraitOrientation();
        ApplyGameplayViewport();
    }

    private void EnsureBackgroundCamera()
    {
        if (backgroundCamera == null)
        {
            Transform existingBackground = transform.Find("Background Camera");
            if (existingBackground != null)
            {
                backgroundCamera = existingBackground.GetComponent<Camera>();
            }
        }

        if (backgroundCamera == null)
        {
            var backgroundObject = new GameObject("Background Camera");
            backgroundObject.transform.SetParent(transform, false);
            backgroundCamera = backgroundObject.AddComponent<Camera>();
        }

        backgroundCamera.orthographic = true;
        backgroundCamera.orthographicSize = 5f;
        backgroundCamera.transform.position = new Vector3(0f, 0f, -50f);
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = new Color32(5, 8, 16, 255);
        backgroundCamera.cullingMask = 0;
        backgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
        backgroundCamera.depth = -10f;
        backgroundCamera.nearClipPlane = 0.1f;
        backgroundCamera.farClipPlane = 100f;
    }

    private void EnsureCameraViewport()
    {
        if (mainCamera == null)
        {
            return;
        }

        if (backgroundCamera == null)
        {
            EnsureBackgroundCamera();
        }

        if (cachedScreenWidth != Screen.width || cachedScreenHeight != Screen.height)
        {
            ApplyGameplayViewport();
        }
    }

    private void ConfigurePortraitOrientation()
    {
        if (!Application.isMobilePlatform)
        {
            return;
        }

        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
    }

    private void ApplyGameplayViewport()
    {
        cachedScreenWidth = Mathf.Max(1, Screen.width);
        cachedScreenHeight = Mathf.Max(1, Screen.height);

        float screenAspect = (float)cachedScreenWidth / cachedScreenHeight;
        float viewportWidth = GameplayViewportHeight * GameplayViewportTargetAspect / screenAspect;
        viewportWidth = Mathf.Clamp(viewportWidth, 0.22f, 0.92f);

        float viewportX = (1f - viewportWidth) * 0.5f;
        gameplayViewport = new Rect(viewportX, GameplayViewportBottom, viewportWidth, GameplayViewportHeight);
        mainCamera.rect = gameplayViewport;

        if (backgroundCamera != null)
        {
            backgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    private void CreateRoots()
    {
        worldRoot = new GameObject("World").transform;
        worldRoot.SetParent(transform);

        wallRoot = new GameObject("Walls").transform;
        wallRoot.SetParent(worldRoot);

        appleRoot = new GameObject("Apples").transform;
        appleRoot.SetParent(worldRoot);

        snakeRoot = new GameObject("Snake").transform;
        snakeRoot.SetParent(worldRoot);

        bugRoot = new GameObject("Bugs").transform;
        bugRoot.SetParent(worldRoot);

        heartRoot = new GameObject("Hearts").transform;
        heartRoot.SetParent(worldRoot);
    }

    private void BuildWalls()
    {
        foreach (var wallObject in wallObjects)
        {
            if (wallObject != null)
            {
                Destroy(wallObject);
            }
        }

        wallObjects.Clear();
        borderWallCells.Clear();
        borderWallByCell.Clear();

        for (int x = MinX; x <= MaxX; x++)
        {
            Vector2Int bottomCell = new Vector2Int(x, MinY);
            GameObject bottomWall = CreateWallBlock(bottomCell, wallRoot, wallObjects, 1, 2);
            borderWallCells.Add(bottomCell);
            borderWallByCell[bottomCell] = bottomWall;

            Vector2Int topCell = new Vector2Int(x, MaxY);
            GameObject topWall = CreateWallBlock(topCell, wallRoot, wallObjects, 1, 2);
            borderWallCells.Add(topCell);
            borderWallByCell[topCell] = topWall;
        }

        for (int y = MinY + 1; y < MaxY; y++)
        {
            Vector2Int leftCell = new Vector2Int(MinX, y);
            GameObject leftWall = CreateWallBlock(leftCell, wallRoot, wallObjects, 1, 2);
            borderWallCells.Add(leftCell);
            borderWallByCell[leftCell] = leftWall;

            Vector2Int rightCell = new Vector2Int(MaxX, y);
            GameObject rightWall = CreateWallBlock(rightCell, wallRoot, wallObjects, 1, 2);
            borderWallCells.Add(rightCell);
            borderWallByCell[rightCell] = rightWall;
        }
    }

    private void ClearInteriorWalls()
    {
        foreach (var wallObject in interiorWallObjects)
        {
            if (wallObject != null)
            {
                Destroy(wallObject);
            }
        }

        interiorWallObjects.Clear();
        interiorWallCells.Clear();
        interiorWallByCell.Clear();
    }

    private void GenerateInteriorWalls(int level)
    {
        if (level <= 1 || snakeSegments.Count == 0)
        {
            return;
        }

        int playableWidth = MaxX - MinX - 1;
        int playableHeight = MaxY - MinY - 1;
        int playableCells = playableWidth * playableHeight;

        int maxWallCells = Mathf.FloorToInt(playableCells * MaxInteriorWallCoverage);
        if (maxWallCells < MinInteriorWallLength)
        {
            return;
        }

        int maxWallsByCoverage = maxWallCells / MinInteriorWallLength;
        int targetWalls = Mathf.Min(level - 1, maxWallsByCoverage);
        if (targetWalls <= 0)
        {
            return;
        }

        var reservedCells = new HashSet<Vector2Int>(snakeSegments);
        System.Random levelRandom = CreateLevelRandom(level);

        int usedWallCells = 0;
        int wallsPlaced = 0;
        int attempts = 0;
        int maxAttempts = 12000;

        while (wallsPlaced < targetWalls && attempts < maxAttempts)
        {
            attempts++;

            int remainingCellsBudget = maxWallCells - usedWallCells;
            if (remainingCellsBudget < MinInteriorWallLength)
            {
                break;
            }

            if (!TryCreateInteriorWall(levelRandom, remainingCellsBudget, reservedCells, out List<Vector2Int> wallCells))
            {
                continue;
            }

            for (int i = 0; i < wallCells.Count; i++)
            {
                Vector2Int cell = wallCells[i];
                interiorWallCells.Add(cell);
                GameObject wallObject = CreateWallBlock(cell, wallRoot, interiorWallObjects, 1, 2);
                interiorWallByCell[cell] = wallObject;
            }

            usedWallCells += wallCells.Count;
            wallsPlaced++;
        }
    }
    private bool TryCreateInteriorWall(System.Random levelRandom, int remainingCellsBudget, HashSet<Vector2Int> reservedCells, out List<Vector2Int> wallCells)
    {
        int playableWidth = MaxX - MinX - 1;
        int playableHeight = MaxY - MinY - 1;

        bool firstHorizontal = levelRandom.Next(0, 2) == 0;
        for (int orientationAttempt = 0; orientationAttempt < 2; orientationAttempt++)
        {
            bool isHorizontal = orientationAttempt == 0 ? firstHorizontal : !firstHorizontal;
            int maxLengthByDimension = (isHorizontal ? playableWidth : playableHeight) - 1;
            int maxLength = Mathf.Min(MaxInteriorWallLength, maxLengthByDimension, remainingCellsBudget);
            if (maxLength < MinInteriorWallLength)
            {
                continue;
            }

            int length = levelRandom.Next(MinInteriorWallLength, maxLength + 1);
            if (TryBuildInteriorWallCells(levelRandom, isHorizontal, length, reservedCells, out wallCells))
            {
                return true;
            }
        }

        wallCells = null;
        return false;
    }

    private bool TryBuildInteriorWallCells(System.Random levelRandom, bool isHorizontal, int length, HashSet<Vector2Int> reservedCells, out List<Vector2Int> wallCells)
    {
        wallCells = null;

        int minX = MinX + 1;
        int maxX = MaxX - 1;
        int minY = MinY + 1;
        int maxY = MaxY - 1;

        int startXMin = minX;
        int startXMax = isHorizontal ? maxX - length + 1 : maxX;
        int startYMin = minY;
        int startYMax = isHorizontal ? maxY : maxY - length + 1;

        if (startXMin > startXMax || startYMin > startYMax)
        {
            return false;
        }

        int positionAttempts = 120;
        for (int attempt = 0; attempt < positionAttempts; attempt++)
        {
            int startX = levelRandom.Next(startXMin, startXMax + 1);
            int startY = levelRandom.Next(startYMin, startYMax + 1);

            if (isHorizontal && startX == minX && length >= (maxX - minX + 1))
            {
                continue;
            }

            if (!isHorizontal && startY == minY && length >= (maxY - minY + 1))
            {
                continue;
            }

            var candidate = new List<Vector2Int>(length);
            bool blocked = false;

            for (int i = 0; i < length; i++)
            {
                Vector2Int cell = isHorizontal
                    ? new Vector2Int(startX + i, startY)
                    : new Vector2Int(startX, startY + i);

                if (reservedCells.Contains(cell) || interiorWallCells.Contains(cell))
                {
                    blocked = true;
                    break;
                }

                candidate.Add(cell);
            }

            if (blocked)
            {
                continue;
            }

            wallCells = candidate;
            return true;
        }

        return false;
    }

    private static System.Random CreateLevelRandom(int level)
    {
        unchecked
        {
            int seed = 486187739 ^ (level * 16777619);
            return new System.Random(seed);
        }
    }

    private GameObject CreateWallBlock(Vector2Int cell, Transform parent, List<GameObject> targetList, int fallbackSortingOrder, int emojiSortingOrder)
    {
        var wallObject = new GameObject("Wall");
        wallObject.transform.SetParent(parent, false);
        wallObject.transform.localPosition = new Vector3(cell.x, cell.y, 0f);

        var fallbackRenderer = wallObject.AddComponent<SpriteRenderer>();
        fallbackRenderer.sprite = GetWallFallbackSprite();
        fallbackRenderer.color = Color.white;
        fallbackRenderer.sortingOrder = fallbackSortingOrder;

        var emojiObject = new GameObject("Emoji");
        emojiObject.transform.SetParent(wallObject.transform, false);
        emojiObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

        var textMesh = emojiObject.AddComponent<TextMesh>();
        textMesh.text = "\uD83E\uDDF1";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.22f;
        textMesh.fontSize = 96;
        textMesh.color = Color.white;
        textMesh.richText = false;

        Font emojiFont = GetWallEmojiFont();
        if (emojiFont != null)
        {
            textMesh.font = emojiFont;
        }

        MeshRenderer emojiRenderer = emojiObject.GetComponent<MeshRenderer>();
        if (emojiRenderer != null)
        {
            if (emojiFont != null)
            {
                emojiRenderer.material = emojiFont.material;
            }

            emojiRenderer.sortingOrder = emojiSortingOrder;
        }

        targetList?.Add(wallObject);
        return wallObject;
    }

    private BugView CreateBugView(Vector3 localPosition)
    {
        var bugObject = new GameObject("Bug");
        bugObject.transform.SetParent(bugRoot, false);
        bugObject.transform.localPosition = localPosition;
        bugObject.transform.localScale = Vector3.one * BugVisualScale;

        var fallbackRenderer = bugObject.AddComponent<SpriteRenderer>();
        fallbackRenderer.sprite = GetBugFallbackSprite();
        fallbackRenderer.color = Color.white;
        fallbackRenderer.sortingOrder = 7;

        var emojiObject = new GameObject("Emoji");
        emojiObject.transform.SetParent(bugObject.transform, false);
        emojiObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

        var textMesh = emojiObject.AddComponent<TextMesh>();
        textMesh.text = "\uD83D\uDC1B";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.24f;
        textMesh.fontSize = 96;
        textMesh.color = Color.white;
        textMesh.richText = false;

        Font emojiFont = GetWallEmojiFont();
        if (emojiFont != null)
        {
            textMesh.font = emojiFont;
        }

        MeshRenderer emojiRenderer = emojiObject.GetComponent<MeshRenderer>();
        if (emojiRenderer != null)
        {
            if (emojiFont != null)
            {
                emojiRenderer.material = emojiFont.material;
            }

            emojiRenderer.sortingOrder = 8;
        }

        return new BugView
        {
            Root = bugObject,
            EmojiText = textMesh,
            EmojiRenderer = emojiRenderer
        };
    }

    private static Sprite GetBugFallbackSprite()
    {
        if (bugFallbackSprite != null)
        {
            return bugFallbackSprite;
        }

        const int textureSize = 16;
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var transparent = new Color32(0, 0, 0, 0);
        var bodyColor = new Color32(83, 56, 138, 255);
        var wingColor = new Color32(130, 87, 198, 255);
        var eyeColor = new Color32(241, 245, 250, 255);
        var pupilColor = new Color32(30, 38, 52, 255);

        var pixels = new Color32[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }

        for (int y = 3; y <= 12; y++)
        {
            for (int x = 4; x <= 11; x++)
            {
                float dx = (x - 7.5f) / 4.0f;
                float dy = (y - 7.5f) / 5.0f;
                if (dx * dx + dy * dy > 1f)
                {
                    continue;
                }

                pixels[y * textureSize + x] = x <= 7 ? wingColor : bodyColor;
            }
        }

        pixels[11 * textureSize + 6] = eyeColor;
        pixels[11 * textureSize + 9] = eyeColor;
        pixels[11 * textureSize + 7] = pupilColor;
        pixels[11 * textureSize + 8] = pupilColor;

        for (int x = 6; x <= 9; x++)
        {
            pixels[13 * textureSize + x] = bodyColor;
        }

        pixels[14 * textureSize + 7] = bodyColor;
        pixels[14 * textureSize + 8] = bodyColor;

        texture.SetPixels32(pixels);
        texture.Apply();

        bugFallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        return bugFallbackSprite;
    }
    private static Sprite GetHeartFallbackSprite()
    {
        if (heartFallbackSprite != null)
        {
            return heartFallbackSprite;
        }

        const int textureSize = 16;
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var transparent = new Color32(0, 0, 0, 0);
        var mainColor = new Color32(224, 66, 99, 255);
        var shadeColor = new Color32(186, 44, 74, 255);
        var highlightColor = new Color32(246, 131, 156, 255);

        var pixels = new Color32[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }

        for (int y = 1; y < textureSize; y++)
        {
            for (int x = 1; x < textureSize - 1; x++)
            {
                float nx = (x - 7.5f) / 7f;
                float ny = (y - 6f) / 7f;
                float heart = Mathf.Pow(nx * nx + ny * ny - 1f, 3f) - (nx * nx * ny * ny * ny);
                if (heart > 0f)
                {
                    continue;
                }

                Color32 color = x <= 7 ? shadeColor : mainColor;
                if (y >= 9 && x >= 4 && x <= 10)
                {
                    color = highlightColor;
                }

                pixels[y * textureSize + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        heartFallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        return heartFallbackSprite;
    }

    private Font GetWallEmojiFont()
    {
        if (wallEmojiFont != null)
        {
            return wallEmojiFont;
        }

        string[] emojiFonts =
        {
            "Segoe UI Emoji",
            "Apple Color Emoji",
            "Noto Color Emoji",
            "Segoe UI Symbol"
        };

        wallEmojiFont = Font.CreateDynamicFontFromOSFont(emojiFonts, 64);
        if (wallEmojiFont == null)
        {
            wallEmojiFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI" }, 64);
        }

        return wallEmojiFont;
    }

    private static Sprite GetAppleFallbackSprite()
    {
        if (appleFallbackSprite != null)
        {
            return appleFallbackSprite;
        }

        const int textureSize = 16;
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var transparent = new Color32(0, 0, 0, 0);
        var bodyColor = new Color32(214, 67, 63, 255);
        var bodyShade = new Color32(173, 49, 45, 255);
        var highlight = new Color32(244, 132, 115, 255);
        var stemColor = new Color32(122, 84, 43, 255);
        var leafColor = new Color32(98, 171, 79, 255);

        var pixels = new Color32[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }

        for (int y = 2; y <= 12; y++)
        {
            for (int x = 2; x <= 13; x++)
            {
                float dx = (x - 7.5f) / 5.2f;
                float dy = (y - 7.0f) / 5.0f;
                if (dx * dx + dy * dy > 1f)
                {
                    continue;
                }

                Color32 color = x <= 7 ? bodyShade : bodyColor;
                if (x >= 9 && y >= 8 && y <= 11)
                {
                    color = highlight;
                }

                pixels[y * textureSize + x] = color;
            }
        }

        pixels[12 * textureSize + 7] = transparent;
        pixels[12 * textureSize + 8] = transparent;

        pixels[13 * textureSize + 7] = stemColor;
        pixels[14 * textureSize + 7] = stemColor;
        pixels[13 * textureSize + 8] = stemColor;

        pixels[13 * textureSize + 9] = leafColor;
        pixels[14 * textureSize + 10] = leafColor;
        pixels[14 * textureSize + 11] = leafColor;
        pixels[15 * textureSize + 10] = leafColor;

        texture.SetPixels32(pixels);
        texture.Apply();

        appleFallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        return appleFallbackSprite;
    }

    private static Sprite GetWallFallbackSprite()
    {
        if (wallFallbackSprite != null)
        {
            return wallFallbackSprite;
        }

        const int textureSize = 16;
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var brickColor = new Color32(177, 95, 70, 255);
        var mortarColor = new Color32(114, 66, 49, 255);
        var pixels = new Color32[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                bool horizontalMortar = y == 0 || y == 7 || y == 15;
                bool topVerticalMortar = y > 7 && x == 8;
                bool bottomVerticalMortar = y <= 7 && (x == 4 || x == 12);
                bool isMortar = horizontalMortar || topVerticalMortar || bottomVerticalMortar;

                pixels[y * textureSize + x] = isMortar ? mortarColor : brickColor;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        wallFallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        return wallFallbackSprite;
    }

    private void SyncSnakeRenderers()
    {
        while (snakeViews.Count < snakeSegments.Count)
        {
            snakeViews.Add(CreateSnakeSegmentView(snakeRoot));
        }

        while (snakeViews.Count > snakeSegments.Count)
        {
            int lastIndex = snakeViews.Count - 1;
            SnakeSegmentView view = snakeViews[lastIndex];
            snakeViews.RemoveAt(lastIndex);

            if (view != null && view.Root != null)
            {
                Destroy(view.Root);
            }
        }

        bool mouthOpen = IsHeadMouthOpen();

        for (int i = 0; i < snakeSegments.Count; i++)
        {
            SnakeSegmentView view = snakeViews[i];
            if (view == null || view.Root == null)
            {
                continue;
            }

            bool isHead = i == 0;
            view.Root.transform.localPosition = new Vector3(snakeSegments[i].x, snakeSegments[i].y, 0f);
            view.Root.transform.localRotation = isHead
                ? Quaternion.Euler(0f, 0f, GetDirectionRotation(currentDirection))
                : Quaternion.identity;

            if (view.FallbackRenderer != null)
            {
                view.FallbackRenderer.sprite = isHead
                    ? GetSnakeHeadFallbackSprite(mouthOpen)
                    : GetSnakeBodyFallbackSprite();
                view.FallbackRenderer.color = currentSnakeColor;
                view.FallbackRenderer.enabled = isSnakeVisible;
            }

            if (view.EmojiText != null)
            {
                view.EmojiText.text = isHead ? string.Empty : SnakeBodyEmoji;
                view.EmojiText.color = currentSnakeColor;
            }

            if (view.EmojiRenderer != null)
            {
                view.EmojiRenderer.enabled = !isHead && isSnakeVisible;
                view.EmojiRenderer.sortingOrder = 11;
            }
        }
    }

    private void UpdateSnakeHeadVisual()
    {
        if (snakeViews.Count == 0)
        {
            return;
        }

        SnakeSegmentView headView = snakeViews[0];
        if (headView == null || headView.Root == null)
        {
            return;
        }

        bool mouthOpen = IsHeadMouthOpen();

        headView.Root.transform.localRotation = Quaternion.Euler(0f, 0f, GetDirectionRotation(currentDirection));

        if (headView.FallbackRenderer != null)
        {
            headView.FallbackRenderer.sprite = GetSnakeHeadFallbackSprite(mouthOpen);
            headView.FallbackRenderer.color = currentSnakeColor;
            headView.FallbackRenderer.enabled = isSnakeVisible;
        }

        if (headView.EmojiText != null)
        {
            headView.EmojiText.text = string.Empty;
        }

        if (headView.EmojiRenderer != null)
        {
            headView.EmojiRenderer.enabled = false;
        }
    }

    private void SetSnakeVisibility(bool visible)
    {
        isSnakeVisible = visible;

        for (int i = 0; i < snakeViews.Count; i++)
        {
            SnakeSegmentView view = snakeViews[i];
            if (view == null)
            {
                continue;
            }

            if (view.FallbackRenderer != null)
            {
                view.FallbackRenderer.enabled = visible;
            }

            if (view.EmojiRenderer != null)
            {
                bool isHead = i == 0;
                view.EmojiRenderer.enabled = visible && !isHead;
            }
        }
    }
    private bool IsHeadMouthOpen()
    {
        if (state != SnakeGameState.Playing || moveInterval <= 0.0001f)
        {
            return false;
        }

        float phase = Mathf.Clamp01(1f - (moveTimer / moveInterval));
        return phase < 0.55f;
    }

    private static float GetDirectionRotation(Vector2Int direction)
    {
        if (direction == Vector2Int.right)
        {
            return -90f;
        }

        if (direction == Vector2Int.down)
        {
            return 180f;
        }

        if (direction == Vector2Int.left)
        {
            return 90f;
        }

        return 0f;
    }
    private SnakeSegmentView CreateSnakeSegmentView(Transform parent)
    {
        var segmentObject = new GameObject("SnakeSegment");
        segmentObject.transform.SetParent(parent, false);

        var fallbackRenderer = segmentObject.AddComponent<SpriteRenderer>();
        fallbackRenderer.sprite = GetSnakeBodyFallbackSprite();
        fallbackRenderer.color = Color.white;
        fallbackRenderer.sortingOrder = 10;

        var emojiObject = new GameObject("Emoji");
        emojiObject.transform.SetParent(segmentObject.transform, false);
        emojiObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

        var textMesh = emojiObject.AddComponent<TextMesh>();
        textMesh.text = SnakeBodyEmoji;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.28f;
        textMesh.fontSize = 96;
        textMesh.color = Color.white;
        textMesh.richText = false;

        Font emojiFont = GetWallEmojiFont();
        if (emojiFont != null)
        {
            textMesh.font = emojiFont;
        }

        MeshRenderer emojiRenderer = emojiObject.GetComponent<MeshRenderer>();
        if (emojiRenderer != null)
        {
            if (emojiFont != null)
            {
                emojiRenderer.material = emojiFont.material;
            }

            emojiRenderer.sortingOrder = 11;
        }

        return new SnakeSegmentView
        {
            Root = segmentObject,
            FallbackRenderer = fallbackRenderer,
            EmojiText = textMesh,
            EmojiRenderer = emojiRenderer
        };
    }
    private static Sprite GetSnakeHeadFallbackSprite(bool mouthOpen)
    {
        if (mouthOpen)
        {
            if (snakeHeadOpenFallbackSprite == null)
            {
                snakeHeadOpenFallbackSprite = CreateSnakeFallbackSprite(true, true);
            }

            return snakeHeadOpenFallbackSprite;
        }

        if (snakeHeadClosedFallbackSprite == null)
        {
            snakeHeadClosedFallbackSprite = CreateSnakeFallbackSprite(true, false);
        }

        return snakeHeadClosedFallbackSprite;
    }

    private static Sprite GetSnakeBodyFallbackSprite()
    {
        if (snakeBodyFallbackSprite != null)
        {
            return snakeBodyFallbackSprite;
        }

        snakeBodyFallbackSprite = CreateSnakeFallbackSprite(false, false);
        return snakeBodyFallbackSprite;
    }

    private static Sprite CreateSnakeFallbackSprite(bool isHead, bool mouthOpen)
    {
        const int textureSize = 16;

        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var transparent = new Color32(0, 0, 0, 0);
        var mainColor = new Color32(126, 211, 33, 255);
        var shadeColor = new Color32(92, 163, 24, 255);
        var highlightColor = new Color32(163, 234, 85, 255);
        var mouthColor = new Color32(52, 34, 30, 255);
        var eyeWhite = new Color32(244, 249, 252, 255);
        var eyePupil = new Color32(28, 36, 46, 255);

        var pixels = new Color32[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }

        for (int y = 1; y <= 14; y++)
        {
            for (int x = 1; x <= 14; x++)
            {
                float dx = (x - 7.5f) / 6.5f;
                float dy = (y - 7.5f) / 6.5f;
                if (dx * dx + dy * dy > 1f)
                {
                    continue;
                }

                Color32 color = x <= 7 ? shadeColor : mainColor;
                if (y >= 9 && x >= 4 && x <= 11)
                {
                    color = highlightColor;
                }

                pixels[y * textureSize + x] = color;
            }
        }

        if (isHead)
        {
            if (mouthOpen)
            {
                for (int y = 9; y <= 15; y++)
                {
                    int halfWidth = y - 8;
                    for (int x = 7 - halfWidth; x <= 8 + halfWidth; x++)
                    {
                        if (x < 0 || x >= textureSize)
                        {
                            continue;
                        }

                        pixels[y * textureSize + x] = transparent;
                    }
                }
            }
            else
            {
                for (int x = 5; x <= 10; x++)
                {
                    pixels[11 * textureSize + x] = mouthColor;
                }

                pixels[10 * textureSize + 7] = mouthColor;
                pixels[10 * textureSize + 8] = mouthColor;
            }

            for (int y = 7; y <= 8; y++)
            {
                for (int x = 4; x <= 5; x++)
                {
                    pixels[y * textureSize + x] = eyeWhite;
                }

                for (int x = 10; x <= 11; x++)
                {
                    pixels[y * textureSize + x] = eyeWhite;
                }
            }

            pixels[7 * textureSize + 5] = eyePupil;
            pixels[8 * textureSize + 5] = eyePupil;
            pixels[7 * textureSize + 10] = eyePupil;
            pixels[8 * textureSize + 10] = eyePupil;
        }
        else
        {
            for (int x = 4; x <= 11; x++)
            {
                pixels[7 * textureSize + x] = highlightColor;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
    }
}
