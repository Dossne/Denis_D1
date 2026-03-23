using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SnakeUIController : MonoBehaviour
{
    private static SnakeUIController instance;

    private SnakeGameController gameController;
    private Font uiFont;

    private RectTransform canvasRect;
    private RectTransform controlsRoot;

    private Text levelText;
    private Text timerText;
    private Text statusText;

    private Button actionButton;
    private RectTransform actionButtonRect;
    private Text actionButtonText;

    private SnakeGameState lastState = (SnakeGameState)(-1);
    private Rect lastViewport = new Rect(-1f, -1f, -1f, -1f);
    private string lastLevelLine = string.Empty;
    private string lastTimerLine = string.Empty;
    private string lastStatusLine = string.Empty;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        gameController = GetComponent<SnakeGameController>();
        if (gameController == null)
        {
            gameController = FindObjectOfType<SnakeGameController>();
        }

        if (gameController == null)
        {
            Debug.LogError("SnakeUIController requires SnakeGameController.");
            enabled = false;
            return;
        }

        uiFont = CreateUiFont();
        if (uiFont == null)
        {
            Debug.LogError("SnakeUIController could not initialize UI font.");
            enabled = false;
            return;
        }

        EnsureEventSystem();
        BuildUi();
        ApplyLayoutFromViewport(true);
        UpdateHud();
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
        if (gameController == null)
        {
            return;
        }

        ApplyLayoutFromViewport(false);
        UpdateHud();
        UpdateStateControls();
    }

    private Font CreateUiFont()
    {
        string[] preferredFontNames =
        {
            "Segoe UI",
            "Arial",
            "Tahoma",
            "Verdana",
            "Noto Sans",
            "Liberation Sans"
        };

        Font font = Font.CreateDynamicFontFromOSFont(preferredFontNames, 32);
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("SnakeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = canvasObject.GetComponent<RectTransform>();

        levelText = CreateText("LevelText", canvasRect, 36, TextAnchor.MiddleCenter);
        timerText = CreateText("TimerText", canvasRect, 32, TextAnchor.MiddleCenter);
        statusText = CreateText("StatusText", canvasRect, 46, TextAnchor.MiddleCenter);

        actionButton = CreateButton("ActionButton", canvasRect, out actionButtonText);
        actionButtonRect = actionButton.GetComponent<RectTransform>();
        actionButton.gameObject.SetActive(false);

        controlsRoot = CreateRectTransform("DirectionControls", canvasRect);

        CreateDirectionButton(controlsRoot, "UpButton", "UP", new Vector2(0f, 100f), gameController.QueueUp);
        CreateDirectionButton(controlsRoot, "DownButton", "DOWN", new Vector2(0f, -100f), gameController.QueueDown);
        CreateDirectionButton(controlsRoot, "LeftButton", "LEFT", new Vector2(-115f, 0f), gameController.QueueLeft);
        CreateDirectionButton(controlsRoot, "RightButton", "RIGHT", new Vector2(115f, 0f), gameController.QueueRight);
    }

    private void ApplyLayoutFromViewport(bool force)
    {
        Rect viewport = gameController.GameplayViewport;
        if (viewport.width <= 0f || viewport.height <= 0f)
        {
            viewport = new Rect(0.2f, 0.24f, 0.6f, 0.62f);
        }

        if (!force && IsSameViewport(lastViewport, viewport))
        {
            return;
        }

        lastViewport = viewport;

        float topY = Mathf.Clamp01(viewport.yMax);
        float bottomY = Mathf.Clamp01(viewport.yMin);
        float centerX = Mathf.Clamp01(viewport.center.x);
        float centerY = Mathf.Clamp01(viewport.center.y);

        SetRect(levelText.rectTransform, new Vector2(centerX, Mathf.Clamp01(topY + 0.055f)), new Vector2(centerX, Mathf.Clamp01(topY + 0.055f)), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 84f));
        SetRect(timerText.rectTransform, new Vector2(centerX, Mathf.Clamp01(topY + 0.02f)), new Vector2(centerX, Mathf.Clamp01(topY + 0.02f)), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 72f));

        SetRect(statusText.rectTransform, new Vector2(centerX, Mathf.Clamp01(centerY + 0.09f)), new Vector2(centerX, Mathf.Clamp01(centerY + 0.09f)), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 120f));

        SetRect(actionButtonRect, new Vector2(centerX, centerY), new Vector2(centerX, centerY), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(380f, 110f));

        float controlsY = Mathf.Clamp01(bottomY - 0.13f);
        SetRect(controlsRoot, new Vector2(centerX, controlsY), new Vector2(centerX, controlsY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 320f));
    }

    private static bool IsSameViewport(Rect previous, Rect current)
    {
        const float epsilon = 0.001f;

        return Mathf.Abs(previous.x - current.x) < epsilon
            && Mathf.Abs(previous.y - current.y) < epsilon
            && Mathf.Abs(previous.width - current.width) < epsilon
            && Mathf.Abs(previous.height - current.height) < epsilon;
    }

    private void UpdateHud()
    {
        int collectedApples = gameController.ApplesTarget - gameController.ApplesRemaining;

        string levelLine = string.Format("Level {0}  Apples {1}/{2}", gameController.CurrentLevel, collectedApples, gameController.ApplesTarget);
        if (!string.Equals(lastLevelLine, levelLine))
        {
            lastLevelLine = levelLine;
            levelText.text = levelLine;
        }

        string timerLine = string.Format("Time {0:D2}", Mathf.Max(0, Mathf.CeilToInt(gameController.TimeRemaining)));
        if (!string.Equals(lastTimerLine, timerLine))
        {
            lastTimerLine = timerLine;
            timerText.text = timerLine;
        }

        string statusLine = gameController.State == SnakeGameState.Playing ? string.Empty : gameController.StatusMessage;
        if (!string.Equals(lastStatusLine, statusLine))
        {
            lastStatusLine = statusLine;
            statusText.text = statusLine;
        }
    }

    private void UpdateStateControls()
    {
        if (lastState == gameController.State)
        {
            return;
        }

        lastState = gameController.State;
        actionButton.onClick.RemoveAllListeners();

        if (lastState == SnakeGameState.Won)
        {
            actionButton.gameObject.SetActive(true);
            actionButtonText.text = "Next Level";
            actionButton.onClick.AddListener(gameController.StartNextLevel);
            return;
        }

        if (lastState == SnakeGameState.Lost)
        {
            actionButton.gameObject.SetActive(true);
            actionButtonText.text = "Try Again";
            actionButton.onClick.AddListener(gameController.RestartLevel);
            return;
        }

        actionButton.gameObject.SetActive(false);
    }

    private void CreateDirectionButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        Button button = CreateButton(name, parent, out Text buttonText);
        buttonText.text = label;
        button.onClick.AddListener(onClick);

        RectTransform rect = button.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(140f, 86f));
    }

    private Button CreateButton(string name, Transform parent, out Text label)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color32(8, 12, 30, 230);

        var button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(8, 12, 30, 230);
        colors.highlightedColor = new Color32(20, 30, 58, 255);
        colors.pressedColor = new Color32(30, 44, 77, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color32(8, 12, 30, 180);
        button.colors = colors;

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        label = textObject.GetComponent<Text>();
        ConfigureTextStyle(label, 34, TextAnchor.MiddleCenter, new Color32(229, 238, 245, 255));

        RectTransform textRect = label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        var text = textObject.GetComponent<Text>();
        ConfigureTextStyle(text, fontSize, alignment, new Color32(231, 240, 247, 255));

        return text;
    }

    private void ConfigureTextStyle(Text text, int fontSize, TextAnchor alignment, Color color)
    {
        text.font = uiFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignByGeometry = true;
        text.raycastTarget = false;
        text.text = string.Empty;
    }

    private RectTransform CreateRectTransform(string name, Transform parent)
    {
        var objectWithRect = new GameObject(name, typeof(RectTransform));
        objectWithRect.transform.SetParent(parent, false);
        return objectWithRect.GetComponent<RectTransform>();
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }
}
