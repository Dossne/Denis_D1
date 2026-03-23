using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SnakeUIController : MonoBehaviour
{
    private SnakeGameController gameController;
    private Font uiFont;

    private Text levelText;
    private Text timerText;
    private Text statusText;

    private Button actionButton;
    private Text actionButtonText;

    private SnakeGameState lastState = (SnakeGameState)(-1);

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

        uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        EnsureEventSystem();
        BuildUi();
    }

    private void Update()
    {
        if (gameController == null)
        {
            return;
        }

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

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

        levelText = CreateText("LevelText", canvasRect, 30, TextAnchor.UpperLeft);
        SetRect(levelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(680f, 80f));

        timerText = CreateText("TimerText", canvasRect, 30, TextAnchor.UpperRight);
        SetRect(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(320f, 80f));

        statusText = CreateText("StatusText", canvasRect, 44, TextAnchor.MiddleCenter);
        SetRect(statusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(700f, 120f));

        actionButton = CreateButton("ActionButton", canvasRect, out actionButtonText);
        SetRect(actionButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(380f, 110f));
        actionButton.gameObject.SetActive(false);

        RectTransform controlsRoot = CreateRectTransform("DirectionControls", canvasRect);
        SetRect(controlsRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(360f, 280f));

        CreateDirectionButton(controlsRoot, "UpButton", "UP", new Vector2(0f, 90f), gameController.QueueUp);
        CreateDirectionButton(controlsRoot, "DownButton", "DOWN", new Vector2(0f, -90f), gameController.QueueDown);
        CreateDirectionButton(controlsRoot, "LeftButton", "LEFT", new Vector2(-95f, 0f), gameController.QueueLeft);
        CreateDirectionButton(controlsRoot, "RightButton", "RIGHT", new Vector2(95f, 0f), gameController.QueueRight);
    }

    private void UpdateHud()
    {
        int collectedApples = gameController.ApplesTarget - gameController.ApplesRemaining;
        levelText.text = string.Format("Level {0}  Apples {1}/{2}", gameController.CurrentLevel, collectedApples, gameController.ApplesTarget);
        timerText.text = string.Format("Time {0}", Mathf.CeilToInt(gameController.TimeRemaining));

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
