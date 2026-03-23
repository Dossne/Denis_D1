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
    private const int MinX = -10;
    private const int MaxX = 10;
    private const int MinY = -16;
    private const int MaxY = 16;

    private const int BaseApplesCount = 6;
    private const int ApplesPerLevel = 2;

    private const float BaseMoveInterval = 0.22f;
    private const float MinMoveInterval = 0.08f;
    private const float SpeedUpFactor = 0.96f;

    private static Sprite pixelSprite;

    private readonly List<Vector2Int> snakeSegments = new List<Vector2Int>();
    private readonly List<SpriteRenderer> snakeRenderers = new List<SpriteRenderer>();
    private readonly Dictionary<Vector2Int, SpriteRenderer> appleRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
    private readonly List<SpriteRenderer> wallRenderers = new List<SpriteRenderer>();

    private readonly System.Random random = new System.Random();

    private Transform worldRoot;
    private Transform wallRoot;
    private Transform snakeRoot;
    private Transform appleRoot;

    private Vector2Int currentDirection = Vector2Int.up;
    private Vector2Int queuedDirection = Vector2Int.up;

    private SnakeGameState state;
    private int currentLevel;
    private int applesTarget;
    private float moveInterval;
    private float moveTimer;
    private float timeRemaining;
    private string statusMessage = string.Empty;

    public SnakeGameState State => state;
    public int CurrentLevel => currentLevel;
    public int ApplesRemaining => appleRenderers.Count;
    public int ApplesTarget => applesTarget;
    public float TimeRemaining => Mathf.Max(0f, timeRemaining);
    public string StatusMessage => statusMessage;

    private void Awake()
    {
        ConfigureCamera();
        CreateRoots();
        BuildWalls();
        BeginLevel(1);
    }

    private void Update()
    {
        if (state == SnakeGameState.Won)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                StartNextLevel();
            }

            return;
        }

        if (state == SnakeGameState.Lost)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                RestartLevel();
            }

            return;
        }

        HandleKeyboardInput();

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            LoseLevel("Time is up");
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

        moveInterval = Mathf.Max(MinMoveInterval, BaseMoveInterval - (currentLevel - 1) * 0.005f);
        moveTimer = moveInterval;

        state = SnakeGameState.Playing;
        statusMessage = string.Empty;

        currentDirection = Vector2Int.up;
        queuedDirection = Vector2Int.up;

        ResetSnake();
        SpawnApples(applesTarget);
    }

    private int CalculateApplesCount(int level)
    {
        return BaseApplesCount + (level - 1) * ApplesPerLevel;
    }

    private float CalculateTimeLimit(int applesCount)
    {
        return Mathf.Clamp(30f + applesCount * 5f, 30f, 90f);
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
        foreach (var renderer in appleRenderers.Values)
        {
            if (renderer != null)
            {
                Destroy(renderer.gameObject);
            }
        }

        appleRenderers.Clear();

        var occupiedCells = new HashSet<Vector2Int>(snakeSegments);
        int maxAttempts = 5000;

        while (appleRenderers.Count < count && maxAttempts > 0)
        {
            maxAttempts--;

            int x = random.Next(MinX + 1, MaxX);
            int y = random.Next(MinY + 1, MaxY);
            var position = new Vector2Int(x, y);

            if (occupiedCells.Contains(position) || appleRenderers.ContainsKey(position))
            {
                continue;
            }

            var renderer = CreateBlockRenderer(appleRoot, "Apple", new Color32(237, 106, 94, 255), 5);
            renderer.transform.localPosition = new Vector3(position.x, position.y, 0f);
            appleRenderers[position] = renderer;
        }
    }

    private void StepSnake()
    {
        currentDirection = queuedDirection;

        Vector2Int nextHead = snakeSegments[0] + currentDirection;

        if (IsWallCell(nextHead))
        {
            LoseLevel("You hit the wall");
            return;
        }

        bool willEatApple = appleRenderers.ContainsKey(nextHead);
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

        SyncSnakeRenderers();

        if (appleRenderers.Count == 0)
        {
            WinLevel();
        }
    }

    private void EatApple(Vector2Int appleCell)
    {
        if (appleRenderers.TryGetValue(appleCell, out var renderer) && renderer != null)
        {
            Destroy(renderer.gameObject);
        }

        appleRenderers.Remove(appleCell);
        moveInterval = Mathf.Max(MinMoveInterval, moveInterval * SpeedUpFactor);
    }

    private void WinLevel()
    {
        state = SnakeGameState.Won;
        statusMessage = "Level complete";
    }

    private void LoseLevel(string reason)
    {
        state = SnakeGameState.Lost;
        statusMessage = reason;
    }

    private bool IsWallCell(Vector2Int cell)
    {
        return cell.x <= MinX || cell.x >= MaxX || cell.y <= MinY || cell.y >= MaxY;
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            QueueDirection(Vector2Int.up);
            return;
        }

        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            QueueDirection(Vector2Int.down);
            return;
        }

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            QueueDirection(Vector2Int.left);
            return;
        }

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            QueueDirection(Vector2Int.right);
        }
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

    private void ConfigureCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 18f;
        mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        mainCamera.backgroundColor = new Color32(17, 24, 39, 255);
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
    }

    private void BuildWalls()
    {
        foreach (var renderer in wallRenderers)
        {
            if (renderer != null)
            {
                Destroy(renderer.gameObject);
            }
        }

        wallRenderers.Clear();

        var wallColor = new Color32(45, 68, 92, 255);

        for (int x = MinX; x <= MaxX; x++)
        {
            CreateWallBlock(new Vector2Int(x, MinY), wallColor);
            CreateWallBlock(new Vector2Int(x, MaxY), wallColor);
        }

        for (int y = MinY + 1; y < MaxY; y++)
        {
            CreateWallBlock(new Vector2Int(MinX, y), wallColor);
            CreateWallBlock(new Vector2Int(MaxX, y), wallColor);
        }
    }

    private void CreateWallBlock(Vector2Int cell, Color color)
    {
        var renderer = CreateBlockRenderer(wallRoot, "Wall", color, 1);
        renderer.transform.localPosition = new Vector3(cell.x, cell.y, 0f);
        wallRenderers.Add(renderer);
    }

    private void SyncSnakeRenderers()
    {
        while (snakeRenderers.Count < snakeSegments.Count)
        {
            var renderer = CreateBlockRenderer(snakeRoot, "SnakeSegment", new Color32(126, 211, 33, 255), 10);
            snakeRenderers.Add(renderer);
        }

        while (snakeRenderers.Count > snakeSegments.Count)
        {
            int lastIndex = snakeRenderers.Count - 1;
            var renderer = snakeRenderers[lastIndex];
            snakeRenderers.RemoveAt(lastIndex);

            if (renderer != null)
            {
                Destroy(renderer.gameObject);
            }
        }

        for (int i = 0; i < snakeSegments.Count; i++)
        {
            var renderer = snakeRenderers[i];
            renderer.transform.localPosition = new Vector3(snakeSegments[i].x, snakeSegments[i].y, 0f);
            renderer.color = i == 0 ? new Color32(155, 232, 75, 255) : new Color32(126, 211, 33, 255);
        }
    }

    private SpriteRenderer CreateBlockRenderer(Transform parent, string baseName, Color color, int sortingOrder)
    {
        var block = new GameObject(baseName);
        block.transform.SetParent(parent, false);

        var renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPixelSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        return renderer;
    }

    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null)
        {
            return pixelSprite;
        }

        const int textureSize = 16;

        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(255, 255, 255, 255);
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        return pixelSprite;
    }
}
