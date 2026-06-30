using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PlayerNeedsManager : MonoBehaviour
{
    private const string PrefabPath = "Prefabs/PlayerNeedsManager";
    private const string MainMenuSceneName = "MainMenu";
    private const string LaptopSceneName = "LaptopScene";
    private const string EndingScreenSceneName = "EndingScreen";
    private const int NeedCount = 4;
    private const float MaxNeedValue = 100f;

    public static PlayerNeedsManager Instance { get; private set; }

    [SerializeField] private float hungerDecayPerSecond = 0.225f;
    [SerializeField] private float thirstDecayPerSecond = 0.275f;
    [SerializeField] private float drunkDecayPerSecond = 0.175f;
    [SerializeField] private float hungerHealthDrainPerSecond = 2f;
    [SerializeField] private float thirstHealthDrainPerSecond = 3f;
    [SerializeField] private float healthRecoveryPerSecond = 1.5f;
    [SerializeField] private float healthDangerThreshold = 20f;
    [SerializeField] private float healthRecoveryThreshold = 35f;

    private Canvas needsCanvas;
    private Text[] needValues;
    private Image[] needBarFills;
    private float hunger;
    private float thirst;
    private float drunk;
    private float health;
    private float cameraCheckTimer;
    private bool endingTriggered;

    public event Action ValuesChanged;

    public float Hunger => hunger;
    public float Thirst => thirst;
    public float Drunk => drunk;
    public float Health => health;
    public float Drunk01 => Mathf.Clamp01(drunk / MaxNeedValue);
    public float Health01 => Mathf.Clamp01(health / MaxNeedValue);

    public static void EnsureExists()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);

        if (prefab != null)
        {
            Instantiate(prefab);
            return;
        }

        new GameObject(nameof(PlayerNeedsManager)).AddComponent<PlayerNeedsManager>();
    }

    public void AddValues(float hungerAmount, float thirstAmount, float drunkAmount)
    {
        hunger = Mathf.Clamp(hunger + hungerAmount, 0f, MaxNeedValue);
        thirst = Mathf.Clamp(thirst + thirstAmount, 0f, MaxNeedValue);
        drunk = Mathf.Clamp(drunk + drunkAmount, 0f, MaxNeedValue);
        RefreshHud();
        ValuesChanged?.Invoke();
    }

    public void ResetForNewGame()
    {
        hunger = MaxNeedValue;
        thirst = MaxNeedValue;
        drunk = 0f;
        health = MaxNeedValue;
        endingTriggered = false;
        RefreshHud();
        ValuesChanged?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetForNewGame();
        CreateHud();
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateSceneVisibility(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Update()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        if (IsHudHiddenScene(activeSceneName))
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        float previousHunger = hunger;
        float previousThirst = thirst;
        float previousDrunk = drunk;
        float previousHealth = health;

        hunger = Mathf.MoveTowards(hunger, 0f, hungerDecayPerSecond * deltaTime);
        thirst = Mathf.MoveTowards(thirst, 0f, thirstDecayPerSecond * deltaTime);
        drunk = Mathf.MoveTowards(drunk, 0f, drunkDecayPerSecond * deltaTime);
        UpdateHealth(deltaTime);

        if (!Mathf.Approximately(previousHunger, hunger)
            || !Mathf.Approximately(previousThirst, thirst)
            || !Mathf.Approximately(previousDrunk, drunk)
            || !Mathf.Approximately(previousHealth, health))
        {
            RefreshHud();
            ValuesChanged?.Invoke();
        }

        if (health <= 0f && !endingTriggered)
        {
            TriggerEndingScreen();
            return;
        }

        cameraCheckTimer -= deltaTime;

        if (cameraCheckTimer <= 0f)
        {
            cameraCheckTimer = 1f;
            AttachDrunkEffects();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSceneVisibility(scene);
        AttachDrunkEffects();
    }

    private void UpdateSceneVisibility(Scene scene)
    {
        bool shouldHide = IsHudHiddenScene(scene.name);

        if (needsCanvas != null)
        {
            needsCanvas.gameObject.SetActive(!shouldHide);
        }
    }

    private void AttachDrunkEffects()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        if (IsHudHiddenScene(activeSceneName))
        {
            return;
        }

        Camera[] cameras = Camera.allCameras;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera == null || !camera.isActiveAndEnabled || camera.CompareTag("MainCamera") == false)
            {
                continue;
            }

            if (!camera.TryGetComponent(out DrunkCameraEffect effect))
            {
                effect = camera.gameObject.AddComponent<DrunkCameraEffect>();
            }

            effect.SetManager(this);
        }
    }

    private void CreateHud()
    {
        needsCanvas = new GameObject("Needs HUD", typeof(RectTransform)).AddComponent<Canvas>();
        needsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        needsCanvas.sortingOrder = 510;
        needsCanvas.transform.SetParent(transform, false);

        CanvasScaler scaler = needsCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Needs Panel", typeof(RectTransform));
        panel.transform.SetParent(needsCanvas.transform, false);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.035f, 0.05f, 0.06f, 0.78f);
        background.raycastTarget = false;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-24f, 24f);
        panelRect.sizeDelta = new Vector2(430f, 196f);

        needValues = new Text[NeedCount];
        needBarFills = new Image[NeedCount];

        CreateNeedRow(panel.transform, 0, "Hungry", new Color(1f, 0.62f, 0.25f, 1f));
        CreateNeedRow(panel.transform, 1, "Thirsty", new Color(0.32f, 0.82f, 1f, 1f));
        CreateNeedRow(panel.transform, 2, "Drunk", new Color(0.82f, 0.48f, 1f, 1f));
        CreateNeedRow(panel.transform, 3, "Health", new Color(1f, 0.26f, 0.25f, 1f));

        RefreshHud();
    }

    private void CreateNeedRow(Transform parent, int index, string label, Color color)
    {
        const float rowHeight = 38f;
        const float rowSpacing = 8f;
        const float topPadding = 16f;
        float y = -topPadding - index * (rowHeight + rowSpacing);

        RectTransform row = CreateRect($"{label} Row", parent);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, y);
        row.sizeDelta = new Vector2(-28f, rowHeight);

        Text labelText = CreateHudText(label, row, 21, FontStyle.Bold, TextAnchor.MiddleLeft, color);
        SetStretch(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(-312f, 0f));

        RectTransform track = CreateRect($"{label} Bar", row);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.pivot = new Vector2(0.5f, 0.5f);
        track.offsetMin = new Vector2(104f, -9f);
        track.offsetMax = new Vector2(-62f, 9f);

        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.12f);
        trackImage.raycastTarget = false;

        RectTransform fill = CreateRect($"{label} Fill", track);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = color;
        fillImage.raycastTarget = false;
        needBarFills[index] = fillImage;

        Text valueText = CreateHudText("0", row, 19, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.94f, 0.98f, 1f, 1f));
        SetStretch(valueText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f));
        valueText.rectTransform.anchorMin = new Vector2(1f, 0f);
        valueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        valueText.rectTransform.pivot = new Vector2(1f, 0.5f);
        valueText.rectTransform.anchoredPosition = Vector2.zero;
        valueText.rectTransform.sizeDelta = new Vector2(50f, 0f);
        needValues[index] = valueText;
    }

    private void RefreshHud()
    {
        if (needBarFills == null || needValues == null)
        {
            return;
        }

        SetNeedValue(0, hunger);
        SetNeedValue(1, thirst);
        SetNeedValue(2, drunk);
        SetNeedValue(3, health);
    }

    private void SetNeedValue(int index, float value)
    {
        if (index < 0 || index >= needBarFills.Length || index >= needValues.Length)
        {
            return;
        }

        float normalizedValue = Mathf.Clamp01(value / MaxNeedValue);

        if (needBarFills[index] != null)
        {
            RectTransform fillRect = needBarFills[index].rectTransform;
            fillRect.anchorMax = new Vector2(normalizedValue, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            needBarFills[index].enabled = normalizedValue > 0.001f;
        }

        if (needValues[index] != null)
        {
            needValues[index].text = Mathf.RoundToInt(value).ToString();
        }
    }

    private static Text CreateHudText(string value, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
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

    private void UpdateHealth(float deltaTime)
    {
        float hungerDanger = Mathf.InverseLerp(healthDangerThreshold, 0f, hunger);
        float thirstDanger = Mathf.InverseLerp(healthDangerThreshold, 0f, thirst);
        float healthDrain = hungerDanger * hungerHealthDrainPerSecond + thirstDanger * thirstHealthDrainPerSecond;

        if (healthDrain > 0f)
        {
            health = Mathf.MoveTowards(health, 0f, healthDrain * deltaTime);
            return;
        }

        if (hunger >= healthRecoveryThreshold && thirst >= healthRecoveryThreshold)
        {
            health = Mathf.MoveTowards(health, MaxNeedValue, healthRecoveryPerSecond * deltaTime);
        }
    }

    private void TriggerEndingScreen()
    {
        endingTriggered = true;

        if (Application.CanStreamedLevelBeLoaded(EndingScreenSceneName))
        {
            SceneFadeTransition.LoadScene(EndingScreenSceneName, 0.35f, 0.45f, Color.black);
            return;
        }

        SceneManager.LoadScene(EndingScreenSceneName);
    }

    private static bool IsHudHiddenScene(string sceneName)
    {
        return sceneName == MainMenuSceneName || sceneName == LaptopSceneName || sceneName == EndingScreenSceneName;
    }
}
