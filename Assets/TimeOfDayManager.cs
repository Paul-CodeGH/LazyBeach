using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TimeOfDayManager : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string LaptopSceneName = "LaptopScene";
    private const string EndingScreenSceneName = "EndingScreen";
    private const float HoursPerDay = 24f;

    public static TimeOfDayManager Instance { get; private set; }

    [SerializeField] private float startHour = 8f;
    [SerializeField] private float fullDayDurationSeconds = 1200f;
    [SerializeField] private float nightLightIntensityMultiplier = 0.1f;
    [SerializeField] private Color nightLightColor = new Color(0.28f, 0.38f, 0.72f, 1f);
    [SerializeField] private Color sunriseSunsetLightColor = new Color(1f, 0.54f, 0.24f, 1f);
    [SerializeField] private Color dayAmbientColor = new Color(0.68f, 0.73f, 0.82f, 1f);
    [SerializeField] private Color nightAmbientColor = new Color(0.025f, 0.035f, 0.08f, 1f);
    [SerializeField] private float daySkyboxExposure = 1f;
    [SerializeField] private float nightSkyboxExposure = 0.22f;
    [SerializeField] private float sunAzimuthAngle = -30f;

    private readonly List<SunLightState> sunLights = new List<SunLightState>();

    private Canvas timeCanvas;
    private Text timeText;
    private Material runtimeSkybox;
    private bool hasRuntimeSkyboxExposure;
    private bool isGameplayScene;
    private float currentHour;
    private int lastDisplayedMinute = -1;

    public float CurrentHour => currentHour;
    public int CurrentMinuteOfDay => Mathf.FloorToInt(currentHour * 60f) % 1440;

    public static void EnsureExists()
    {
        if (Instance != null)
        {
            return;
        }

        new GameObject(nameof(TimeOfDayManager)).AddComponent<TimeOfDayManager>();
    }

    public void ResetForNewGame()
    {
        currentHour = Mathf.Repeat(startHour, HoursPerDay);
        lastDisplayedMinute = -1;
        RefreshHud();

        if (isGameplayScene)
        {
            ApplyLighting();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentHour = Mathf.Repeat(startHour, HoursPerDay);
        DontDestroyOnLoad(gameObject);
        CreateHud();
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateSceneState(SceneManager.GetActiveScene());
        RefreshHud();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        ReleaseRuntimeSkybox();
    }

    private void Update()
    {
        if (!isGameplayScene)
        {
            return;
        }

        float dayDuration = Mathf.Max(1f, fullDayDurationSeconds);
        currentHour = Mathf.Repeat(currentHour + Time.deltaTime * HoursPerDay / dayDuration, HoursPerDay);

        RefreshHud();
        ApplyLighting();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSceneState(scene);
    }

    private void UpdateSceneState(Scene scene)
    {
        isGameplayScene = !IsHudHiddenScene(scene.name);

        if (timeCanvas != null)
        {
            timeCanvas.gameObject.SetActive(isGameplayScene);
        }

        if (!isGameplayScene)
        {
            sunLights.Clear();
            ReleaseRuntimeSkybox();
            return;
        }

        CacheSceneLighting(scene);
        ApplyLighting();
    }

    private void CacheSceneLighting(Scene scene)
    {
        sunLights.Clear();

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            Light sceneLight = lights[i];

            if (sceneLight == null
                || sceneLight.type != LightType.Directional
                || sceneLight.gameObject.scene != scene
                || !sceneLight.isActiveAndEnabled)
            {
                continue;
            }

            sunLights.Add(new SunLightState(sceneLight));
        }

        PrepareRuntimeSkybox();
    }

    private void PrepareRuntimeSkybox()
    {
        ReleaseRuntimeSkybox();

        if (RenderSettings.skybox == null)
        {
            hasRuntimeSkyboxExposure = false;
            return;
        }

        runtimeSkybox = new Material(RenderSettings.skybox)
        {
            name = $"{RenderSettings.skybox.name} Runtime Time Of Day"
        };

        RenderSettings.skybox = runtimeSkybox;
        hasRuntimeSkyboxExposure = runtimeSkybox.HasProperty("_Exposure");
    }

    private void ReleaseRuntimeSkybox()
    {
        hasRuntimeSkyboxExposure = false;

        if (runtimeSkybox == null)
        {
            return;
        }

        Destroy(runtimeSkybox);
        runtimeSkybox = null;
    }

    private void ApplyLighting()
    {
        float daylight = GetDaylight01();
        float twilight = GetTwilight01();
        float lightMultiplier = Mathf.Lerp(nightLightIntensityMultiplier, 1f, daylight);
        float sunElevation = Mathf.Sin((currentHour - 6f) / 12f * Mathf.PI) * 75f;

        for (int i = 0; i < sunLights.Count; i++)
        {
            SunLightState state = sunLights[i];

            if (state.Light == null)
            {
                continue;
            }

            Color warmDayColor = Color.Lerp(state.BaseColor, sunriseSunsetLightColor, twilight * 0.45f);
            state.Light.intensity = state.BaseIntensity * lightMultiplier;
            state.Light.color = Color.Lerp(nightLightColor, warmDayColor, daylight);
            state.Light.transform.rotation = Quaternion.Euler(sunElevation, sunAzimuthAngle, 0f);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(nightAmbientColor, dayAmbientColor, daylight);

        if (runtimeSkybox != null && hasRuntimeSkyboxExposure)
        {
            runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(nightSkyboxExposure, daySkyboxExposure, daylight));
        }
    }

    private float GetDaylight01()
    {
        float morning = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 8f, currentHour));
        float evening = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(17f, 21f, currentHour));
        return Mathf.Clamp01(Mathf.Min(morning, evening));
    }

    private float GetTwilight01()
    {
        float sunrise = Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(currentHour * 15f, 6f * 15f)) / 45f);
        float sunset = Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(currentHour * 15f, 18f * 15f)) / 45f);
        return Mathf.Max(sunrise, sunset);
    }

    private void CreateHud()
    {
        timeCanvas = new GameObject("Time HUD", typeof(RectTransform)).AddComponent<Canvas>();
        timeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        timeCanvas.sortingOrder = 520;
        timeCanvas.transform.SetParent(transform, false);

        CanvasScaler scaler = timeCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject label = new GameObject("Time Label", typeof(RectTransform));
        label.transform.SetParent(timeCanvas.transform, false);

        Image background = label.AddComponent<Image>();
        background.color = new Color(0.035f, 0.05f, 0.075f, 0.66f);
        background.raycastTarget = false;

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(1f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(1f, 1f);
        labelRect.anchoredPosition = new Vector2(-24f, -24f);
        labelRect.sizeDelta = new Vector2(230f, 54f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(label.transform, false);

        timeText = textObject.AddComponent<Text>();
        timeText.font = UiFontUtility.DefaultFont;
        timeText.fontSize = 27;
        timeText.fontStyle = FontStyle.Bold;
        timeText.alignment = TextAnchor.MiddleCenter;
        timeText.color = new Color(0.9f, 0.96f, 1f, 1f);
        timeText.raycastTarget = false;

        RectTransform textRect = timeText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 0f);
        textRect.offsetMax = new Vector2(-14f, 0f);
    }

    private void RefreshHud()
    {
        if (timeText == null)
        {
            return;
        }

        int minuteOfDay = CurrentMinuteOfDay;

        if (minuteOfDay == lastDisplayedMinute)
        {
            return;
        }

        lastDisplayedMinute = minuteOfDay;
        int hour = minuteOfDay / 60;
        int minute = minuteOfDay % 60;
        timeText.text = $"Time: {hour:00}:{minute:00}";
    }

    private static bool IsHudHiddenScene(string sceneName)
    {
        return string.IsNullOrEmpty(sceneName)
            || sceneName == MainMenuSceneName
            || sceneName == LaptopSceneName
            || sceneName == EndingScreenSceneName;
    }

    private readonly struct SunLightState
    {
        public SunLightState(Light light)
        {
            Light = light;
            BaseIntensity = light.intensity;
            BaseColor = light.color;
        }

        public Light Light { get; }
        public float BaseIntensity { get; }
        public Color BaseColor { get; }
    }
}
