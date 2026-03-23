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
    private const int MinX = -10;
    private const int MaxX = 10;
    private const int MinY = -16;
    private const int MaxY = 16;

    private const int BaseApplesCount = 6;
    private const int ApplesPerLevel = 2;

    private const float BaseMoveInterval = 0.22f;
    private const float MinMoveInterval = 0.08f;
    private const float SpeedUpFactor = 0.96f;

    private const float GameplayViewportBottom = 0.24f;
    private const float GameplayViewportHeight = 0.62f;
    private const float GameplayViewportTargetAspect = 0.58f;

    private static Sprite pixelSprite;
    private static Sprite wallFallbackSprite;

    private readonly List<Vector2Int> snakeSegments = new List<Vector2Int>();
    private readonly List<SpriteRenderer> snakeRenderers = new List<SpriteRenderer>();
    private readonly Dictionary<Vector2Int, GameObject> appleObjects = new Dictionary<Vector2Int, GameObject>();
    private readonly List<GameObject> wallObjects = new List<GameObject>();

    private readonly System.Random random = new System.Random();
    private Font wallEmojiFont;

    private Transform worldRoot;
    private Transform wallRoot;
    private Transform snakeRoot;
    private Transform appleRoot;

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
    private float moveInterval;
    private float moveTimer;
    private float timeRemaining;
    private string statusMessage = string.Empty;

    public SnakeGameState State => state;
    public int CurrentLevel => currentLevel;
    public int ApplesRemaining => appleObjects.Count;
    public int ApplesTarget => applesTarget;
    public float TimeRemaining => Mathf.Max(0f, timeRemaining);
    public string StatusMessage => statusMessage;
    public Rect GameplayViewport => gameplayViewport;

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
        BuildWalls();
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
        foreach (var appleObject in appleObjects.Values)
        {
            if (appleObject != null)
            {
                Destroy(appleObject);
            }
        }

        appleObjects.Clear();

        var occupiedCells = new HashSet<Vector2Int>(snakeSegments);
        int maxAttempts = 5000;

        while (appleObjects.Count < count && maxAttempts > 0)
        {
            maxAttempts--;

            int x = random.Next(MinX + 1, MaxX);
            int y = random.Next(MinY + 1, MaxY);
            var position = new Vector2Int(x, y);

            if (occupiedCells.Contains(position) || appleObjects.ContainsKey(position))
            {
                continue;
            }

            appleObjects[position] = CreateAppleObject(position);
        }
    }

    private GameObject CreateAppleObject(Vector2Int cell)
    {
        var appleObject = new GameObject("Apple");
        appleObject.transform.SetParent(appleRoot, false);
        appleObject.transform.localPosition = new Vector3(cell.x, cell.y, 0f);

        var fallbackRenderer = appleObject.AddComponent<SpriteRenderer>();
        fallbackRenderer.sprite = GetPixelSprite();
        fallbackRenderer.color = new Color32(237, 106, 94, 255);
        fallbackRenderer.sortingOrder = 5;

        var emojiObject = new GameObject("Emoji");
        emojiObject.transform.SetParent(appleObject.transform, false);
        emojiObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

        var textMesh = emojiObject.AddComponent<TextMesh>();
        textMesh.text = "\uD83C\uDF4E";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.2f;
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

            emojiRenderer.sortingOrder = 6;
        }

        return appleObject;
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

        for (int x = MinX; x <= MaxX; x++)
        {
            CreateWallBlock(new Vector2Int(x, MinY));
            CreateWallBlock(new Vector2Int(x, MaxY));
        }

        for (int y = MinY + 1; y < MaxY; y++)
        {
            CreateWallBlock(new Vector2Int(MinX, y));
            CreateWallBlock(new Vector2Int(MaxX, y));
        }
    }

    private void CreateWallBlock(Vector2Int cell)
    {
        var wallObject = new GameObject("Wall");
        wallObject.transform.SetParent(wallRoot, false);
        wallObject.transform.localPosition = new Vector3(cell.x, cell.y, 0f);

        var fallbackRenderer = wallObject.AddComponent<SpriteRenderer>();
        fallbackRenderer.sprite = GetWallFallbackSprite();
        fallbackRenderer.color = Color.white;
        fallbackRenderer.sortingOrder = 1;

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

            emojiRenderer.sortingOrder = 2;
        }

        wallObjects.Add(wallObject);
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

