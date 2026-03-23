using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SnakeUIController : MonoBehaviour
{
    private static SnakeUIController instance;
    private static Sprite controlCircleSprite;

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
        CreateCenterPadIndicator();

        CreateDirectionButton(controlsRoot, "UpButton", "UP", new Vector2(0f, 128f), gameController.QueueUp);
        CreateDirectionButton(controlsRoot, "LeftButton", "LEFT", new Vector2(-118f, 10f), gameController.QueueLeft);
        CreateDirectionButton(controlsRoot, "RightButton", "RIGHT", new Vector2(118f, 10f), gameController.QueueRight);
        CreateDirectionButton(controlsRoot, "DownButton", "DOWN", new Vector2(0f, -108f), gameController.QueueDown);
    }

    private void CreateCenterPadIndicator()
    {
        RectTransform indicatorRoot = CreateRectTransform("CenterIndicator", controlsRoot);
        SetRect(indicatorRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(88f, 88f));

        CreateCenterDiamond(indicatorRoot, "Up", new Vector2(0f, 18f), new Vector2(14f, 14f));
        CreateCenterDiamond(indicatorRoot, "Left", new Vector2(-18f, 0f), new Vector2(14f, 14f));
        CreateCenterDiamond(indicatorRoot, "Right", new Vector2(18f, 0f), new Vector2(14f, 14f));
        CreateCenterDiamond(indicatorRoot, "Down", new Vector2(0f, -18f), new Vector2(14f, 14f));
        CreateCenterDiamond(indicatorRoot, "Center", Vector2.zero, new Vector2(12f, 12f));
    }

    private void CreateCenterDiamond(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        var markerObject = new GameObject(name + "Diamond", typeof(RectTransform), typeof(Image));
        markerObject.transform.SetParent(parent, false);

        var markerRect = markerObject.GetComponent<RectTransform>();
        SetRect(markerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
        markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

        var markerImage = markerObject.GetComponent<Image>();
        markerImage.sprite = GetControlCircleSprite();
        markerImage.color = new Color32(122, 122, 122, 255);
        markerImage.raycastTarget = false;
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

        float controlsY = Mathf.Clamp(bottomY - 0.15f, 0.14f, 0.38f);
        SetRect(controlsRoot, new Vector2(centerX, controlsY), new Vector2(centerX, controlsY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(470f, 440f));
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
        int applesRemaining = gameController.ApplesRemaining;

        string levelLine = string.Format("Level {0}  Apples left {1}", gameController.CurrentLevel, applesRemaining);
        if (!string.Equals(lastLevelLine, levelLine))
        {
            lastLevelLine = levelLine;
            levelText.text = levelLine;
        }

        int secondsLeft = Mathf.Max(0, Mathf.CeilToInt(gameController.TimeRemaining));
        string timerLine = string.Format("Time left {0:D2}", secondsLeft);
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
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(124f, 124f));

        var outerImage = buttonObject.GetComponent<Image>();
        outerImage.sprite = GetControlCircleSprite();
        outerImage.color = new Color32(88, 88, 88, 255);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = outerImage;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(245, 245, 245, 255);
        colors.pressedColor = new Color32(220, 220, 220, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color32(180, 180, 180, 160);
        button.colors = colors;

        var shadow = buttonObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(0, 0, 0, 110);
        shadow.effectDistance = new Vector2(0f, -5f);

        var innerObject = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        innerObject.transform.SetParent(buttonObject.transform, false);
        var innerRect = innerObject.GetComponent<RectTransform>();
        SetRect(innerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 104f));

        var innerImage = innerObject.GetComponent<Image>();
        innerImage.sprite = GetControlCircleSprite();
        innerImage.color = new Color32(247, 204, 28, 255);
        innerImage.raycastTarget = false;

        var shineOneObject = new GameObject("ShineOne", typeof(RectTransform), typeof(Image));
        shineOneObject.transform.SetParent(innerObject.transform, false);
        var shineOneRect = shineOneObject.GetComponent<RectTransform>();
        SetRect(shineOneRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-20f, 18f), new Vector2(32f, 22f));

        var shineOneImage = shineOneObject.GetComponent<Image>();
        shineOneImage.sprite = GetControlCircleSprite();
        shineOneImage.color = new Color32(255, 255, 255, 120);
        shineOneImage.raycastTarget = false;

        var shineTwoObject = new GameObject("ShineTwo", typeof(RectTransform), typeof(Image));
        shineTwoObject.transform.SetParent(innerObject.transform, false);
        var shineTwoRect = shineTwoObject.GetComponent<RectTransform>();
        SetRect(shineTwoRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-4f, 26f), new Vector2(14f, 10f));

        var shineTwoImage = shineTwoObject.GetComponent<Image>();
        shineTwoImage.sprite = GetControlCircleSprite();
        shineTwoImage.color = new Color32(255, 255, 255, 90);
        shineTwoImage.raycastTarget = false;

        button.onClick.AddListener(onClick);
        AddPointerDownListener(buttonObject, onClick);

        Text directionLabel = CreateText(name + "Label", parent, 24, TextAnchor.MiddleCenter);
        directionLabel.text = label;
        directionLabel.color = new Color32(206, 206, 206, 255);

        Vector2 labelOffset = label == "UP" ? new Vector2(0f, 88f) : new Vector2(0f, -86f);
        SetRect(directionLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition + labelOffset, new Vector2(150f, 34f));
    }

    private static void AddPointerDownListener(GameObject buttonObject, UnityEngine.Events.UnityAction onPress)
    {
        var eventTrigger = buttonObject.AddComponent<EventTrigger>();
        if (eventTrigger.triggers == null)
        {
            eventTrigger.triggers = new List<EventTrigger.Entry>();
        }

        var pointerDownEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDownEntry.callback.AddListener(_ => onPress.Invoke());
        eventTrigger.triggers.Add(pointerDownEntry);
    }

    private static Sprite GetControlCircleSprite()
    {
        if (controlCircleSprite != null)
        {
            return controlCircleSprite;
        }

        const int size = 128;
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f - 1f;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(radius + 0.75f - distance);
                byte alphaByte = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alphaByte);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        controlCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return controlCircleSprite;
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
