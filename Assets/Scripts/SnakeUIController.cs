using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SnakeUIController : MonoBehaviour
{
    private SnakeGameController gameController;
    private Font uiFont;

    private RectTransform canvasRect;
    private RectTransform playAreaFrame;
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

    private void Awake()
    {
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

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        EnsureEventSystem();
        BuildUi();
        ApplyLayoutFromViewport(true);
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
        canvas.pixelPerfect = true;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = canvasObject.GetComponent<RectTransform>();

        var playAreaFrameObject = new GameObject("PlayAreaFrame", typeof(RectTransform), typeof(Image));
        playAreaFrameObject.transform.SetParent(canvasRect, false);
        playAreaFrame = playAreaFrameObject.GetComponent<RectTransform>();

        var playAreaImage = playAreaFrameObject.GetComponent<Image>();
        playAreaImage.color = new Color32(24, 38, 55, 100);
        playAreaFrame.SetAsFirstSibling();

        levelText = CreateText("LevelText", canvasRect, 34, TextAnchor.MiddleCenter);
        timerText = CreateText("TimerText", canvasRect, 30, TextAnchor.MiddleCenter);

        statusText = CreateText("StatusText", canvasRect, 44, TextAnchor.MiddleCenter);

        actionButton = CreateButton("ActionButton", canvasRect, out actionButtonText);
        actionButtonRect = actionButton.GetComponent<RectTransform>();
        actionButton.gameObject.SetActive(false);

        controlsRoot = CreateRectTransform("DirectionControls", canvasRect);

        CreateDirectionButton(controlsRoot, "UpButton", "UP", new Vector2(0f, 90f), gameController.QueueUp);
        CreateDirectionButton(controlsRoot, "DownButton", "DOWN", new Vector2(0f, -90f), gameController.QueueDown);
        CreateDirectionButton(controlsRoot, "LeftButton", "LEFT", new Vector2(-95f, 0f), gameController.QueueLeft);
        CreateDirectionButton(controlsRoot, "RightButton", "RIGHT", new Vector2(95f, 0f), gameController.QueueRight);
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

        SetRect(playAreaFrame, new Vector2(viewport.xMin, viewport.yMin), new Vector2(viewport.xMax, viewport.yMax), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        SetRect(levelText.rectTransform, new Vector2(centerX, Mathf.Clamp01(topY + 0.05f)), new Vector2(centerX, Mathf.Clamp01(topY + 0.05f)), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 80f));
        SetRect(timerText.rectTransform, new Vector2(centerX, Mathf.Clamp01(topY + 0.015f)), new Vector2(centerX, Mathf.Clamp01(topY + 0.015f)), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 70f));

        SetRect(statusText.rectTransform, new Vector2(centerX, Mathf.Clamp01(centerY + 0.08f)), new Vector2(centerX, Mathf.Clamp01(centerY + 0.08f)), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 120f));

        SetRect(actionButtonRect, new Vector2(centerX, centerY), new Vector2(centerX, centerY), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(380f, 110f));

        float controlsY = Mathf.Clamp01(bottomY - 0.12f);
        SetRect(controlsRoot, new Vector2(centerX, controlsY), new Vector2(centerX, controlsY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 280f));
    }

    private bool IsSameViewport(Rect previous, Rect current)
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

        if (gameController.State == SnakeGameState.Playing)
        {
            statusText.text = string.Empty;
        }
        else
        {
            statusText.text = gameController.StatusMessage;
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
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(120f, 80f));
    }

    private Button CreateButton(string name, Transform parent, out Text label)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color32(40, 58, 78, 230);

        var button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(40, 58, 78, 230);
        colors.highlightedColor = new Color32(58, 83, 109, 255);
        colors.pressedColor = new Color32(84, 122, 156, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color32(26, 35, 47, 180);
        button.colors = colors;

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        label = textObject.GetComponent<Text>();
        label.font = uiFont;
        label.fontSize = 30;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color32(224, 235, 245, 255);
        label.text = string.Empty;

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
        text.font = uiFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color32(224, 235, 245, 255);
        text.text = string.Empty;

        return text;
    }

    private RectTransform CreateRectTransform(string name, Transform parent)
    {
        var objectWithRect = new GameObject(name, typeof(RectTransform));
        objectWithRect.transform.SetParent(parent, false);
        return objectWithRect.GetComponent<RectTransform>();
    }

    private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }
}

