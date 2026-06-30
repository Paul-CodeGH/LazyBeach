using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class EndingScreenController : MonoBehaviour
{
    [SerializeField] private string startSceneName = "SampleScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float fadeInDuration = 0.45f;

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureEventSystem();
        CreateInterface();
    }

    private void Restart()
    {
        ResetPersistentState();
        LoadScene(startSceneName);
    }

    private void OpenMainMenu()
    {
        ResetPersistentState();
        LoadScene(mainMenuSceneName);
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadScene(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneFadeTransition.LoadScene(sceneName, fadeOutDuration, fadeInDuration, Color.black);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private static void ResetPersistentState()
    {
        CashManager.EnsureExists();
        PlayerNeedsManager.EnsureExists();
        LaptopBusinessManager.EnsureExists();
        TimeOfDayManager.EnsureExists();

        CashManager.Instance?.ResetForNewGame();
        PlayerNeedsManager.Instance?.ResetForNewGame();
        LaptopBusinessManager.Instance?.ResetForNewGame();
        TimeOfDayManager.Instance?.ResetForNewGame();
    }

    private void CreateInterface()
    {
        Canvas canvas = new GameObject("Ending Screen UI", typeof(RectTransform)).AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvas.gameObject.AddComponent<GraphicRaycaster>();

        Image background = canvas.gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.026f, 0.023f, 1f);

        Text title = CreateText("You failed to survive, LazyBeach!", canvas.transform, 58, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.52f, 1f));
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.08f, 0.58f);
        titleRect.anchorMax = new Vector2(0.92f, 0.78f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        Text message = CreateText("Hunger and thirst wore you down. Start over, plan better, and survive the beach.", canvas.transform, 25, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.92f, 0.9f, 0.86f, 1f));
        RectTransform messageRect = message.rectTransform;
        messageRect.anchorMin = new Vector2(0.18f, 0.49f);
        messageRect.anchorMax = new Vector2(0.82f, 0.58f);
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        RectTransform buttons = CreateRect("Buttons", canvas.transform);
        buttons.anchorMin = new Vector2(0.5f, 0.17f);
        buttons.anchorMax = new Vector2(0.5f, 0.17f);
        buttons.pivot = new Vector2(0.5f, 0.5f);
        buttons.sizeDelta = new Vector2(330f, 284f);

        VerticalLayoutGroup layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Button restartButton = CreateButton("Restart", buttons);
        restartButton.onClick.AddListener(Restart);

        Button mainMenuButton = CreateButton("Main Menu", buttons);
        mainMenuButton.onClick.AddListener(OpenMainMenu);

        Button exitButton = CreateButton("Exit Game", buttons);
        exitButton.onClick.AddListener(ExitGame);
    }

    private static Button CreateButton(string label, Transform parent)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.32f, 0.17f, 0.07f, 0.94f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(330f, 84f);

        Text text = CreateText(label, buttonObject.transform, 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.89f, 0.48f, 1f));
        SetStretch(text.rectTransform, new Vector2(12f, 5f), new Vector2(-12f, -5f));

        MainMenuButtonFeedback feedback = buttonObject.AddComponent<MainMenuButtonFeedback>();
        feedback.Initialize(image);

        return button;
    }

    private static Text CreateText(string value, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        RectTransform textRect = CreateRect("Text", parent);
        Text text = textRect.gameObject.AddComponent<Text>();
        text.font = UiFontUtility.DefaultFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void SetStretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
