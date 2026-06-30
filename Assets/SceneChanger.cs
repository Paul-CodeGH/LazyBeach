using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public sealed class SceneChanger : MonoBehaviour
{
    [SerializeField] private string endSceneName;
    [SerializeField] private float fadeOutDuration = 0.45f;
    [SerializeField] private float fadeInDuration = 0.55f;
    [SerializeField] private Color fadeColor = Color.black;

    private bool isChangingScene;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isChangingScene || other.GetComponentInParent<PlayerMovement>() == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(endSceneName))
        {
            Debug.LogWarning($"{nameof(SceneChanger)} on {name} has no end scene assigned.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(endSceneName))
        {
            Debug.LogWarning($"{nameof(SceneChanger)} could not load scene '{endSceneName}'. Add it to Build Settings first.", this);
            return;
        }

        isChangingScene = true;
        SceneFadeTransition.LoadScene(endSceneName, fadeOutDuration, fadeInDuration, fadeColor);
    }

    private void OnDrawGizmos()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();

        if (boxCollider == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.8f);
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        Gizmos.matrix = previousMatrix;
    }
}

public sealed class SceneFadeTransition : MonoBehaviour
{
    private static SceneFadeTransition instance;

    private CanvasGroup canvasGroup;
    private Image fadeImage;
    private Coroutine transitionRoutine;

    public static void LoadScene(string sceneName, float fadeOutDuration, float fadeInDuration, Color fadeColor)
    {
        EnsureExists();
        instance.StartTransition(sceneName, fadeOutDuration, fadeInDuration, fadeColor);
    }

    private static void EnsureExists()
    {
        if (instance != null)
        {
            return;
        }

        GameObject transitionObject = new GameObject(nameof(SceneFadeTransition));
        instance = transitionObject.AddComponent<SceneFadeTransition>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateOverlay();
    }

    private void StartTransition(string sceneName, float fadeOutDuration, float fadeInDuration, Color fadeColor)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(TransitionRoutine(sceneName, fadeOutDuration, fadeInDuration, fadeColor));
    }

    private IEnumerator TransitionRoutine(string sceneName, float fadeOutDuration, float fadeInDuration, Color fadeColor)
    {
        fadeImage.color = fadeColor;
        canvasGroup.blocksRaycasts = true;

        yield return FadeTo(1f, Mathf.Max(0.01f, fadeOutDuration));

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        while (loadOperation != null && !loadOperation.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return FadeTo(0f, Mathf.Max(0.01f, fadeInDuration));

        canvasGroup.blocksRaycasts = false;
        transitionRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private void CreateOverlay()
    {
        Canvas canvas = new GameObject("Scene Fade Canvas", typeof(RectTransform)).AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvas.transform.SetParent(transform, false);

        canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        GameObject imageObject = new GameObject("Fade Image", typeof(RectTransform));
        imageObject.transform.SetParent(canvas.transform, false);

        fadeImage = imageObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = true;

        RectTransform imageRect = fadeImage.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
    }
}
