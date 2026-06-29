using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private string startSceneName = "SampleScene";

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureEventSystem();

        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
            ConfigureButtonFeedback(startButton);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ExitGame);
            ConfigureButtonFeedback(exitButton);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitGame);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(startSceneName);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

    private static void ConfigureButtonFeedback(Button button)
    {
        button.transition = Selectable.Transition.None;

        if (!button.TryGetComponent(out MainMenuButtonFeedback feedback))
        {
            feedback = button.gameObject.AddComponent<MainMenuButtonFeedback>();
        }

        feedback.Initialize(button.targetGraphic);
    }
}

public sealed class MainMenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private static readonly Color NormalColor = new Color(0.32f, 0.17f, 0.07f, 0.94f);
    private static readonly Color HoverColor = new Color(0.86f, 0.47f, 0.12f, 0.97f);
    private static readonly Color PressedColor = new Color(0.17f, 0.07f, 0.03f, 0.98f);
    private static readonly Color NormalTextColor = new Color(1f, 0.89f, 0.48f, 1f);
    private static readonly Color HoverTextColor = new Color(1f, 0.98f, 0.78f, 1f);

    private Graphic targetGraphic;
    private Graphic labelGraphic;
    private RectTransform rectTransform;
    private Vector3 normalScale = Vector3.one;
    private bool isHovered;
    private bool isPressed;

    public void Initialize(Graphic graphic)
    {
        targetGraphic = graphic;
        labelGraphic = GetComponentInChildren<Text>(true);
        rectTransform = transform as RectTransform;

        if (rectTransform != null)
        {
            normalScale = rectTransform.localScale;
        }

        ApplyState(true);
    }

    private void Awake()
    {
        if (targetGraphic == null)
        {
            targetGraphic = GetComponent<Graphic>();
        }

        labelGraphic = GetComponentInChildren<Text>(true);
        rectTransform = transform as RectTransform;

        if (rectTransform != null)
        {
            normalScale = rectTransform.localScale;
        }
    }

    private void OnEnable()
    {
        ApplyState(true);
    }

    private void Update()
    {
        ApplyState(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        isHovered = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isHovered = false;
        isPressed = false;
    }

    private void ApplyState(bool instant)
    {
        Color targetColor = isPressed ? PressedColor : isHovered ? HoverColor : NormalColor;
        Color targetTextColor = isHovered ? HoverTextColor : NormalTextColor;
        Vector3 targetScale = normalScale * (isPressed ? 0.98f : isHovered ? 1.045f : 1f);

        if (targetGraphic != null)
        {
            targetGraphic.color = instant ? targetColor : Color.Lerp(targetGraphic.color, targetColor, Time.unscaledDeltaTime * 12f);
        }

        if (labelGraphic != null)
        {
            labelGraphic.color = instant ? targetTextColor : Color.Lerp(labelGraphic.color, targetTextColor, Time.unscaledDeltaTime * 12f);
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = instant ? targetScale : Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * 14f);
        }
    }
}
