using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PlayerNeedsManager : MonoBehaviour
{
    private const string PrefabPath = "Prefabs/PlayerNeedsManager";
    private const string MainMenuSceneName = "MainMenu";
    private const string LaptopSceneName = "LaptopScene";

    public static PlayerNeedsManager Instance { get; private set; }

    [SerializeField] private float hungerDecayPerSecond = 0.45f;
    [SerializeField] private float thirstDecayPerSecond = 0.55f;
    [SerializeField] private float drunkDecayPerSecond = 0.35f;

    private Canvas needsCanvas;
    private Text needsText;
    private float hunger;
    private float thirst;
    private float drunk;
    private float cameraCheckTimer;

    public event Action ValuesChanged;

    public float Hunger => hunger;
    public float Thirst => thirst;
    public float Drunk => drunk;
    public float Drunk01 => Mathf.Clamp01(drunk / 100f);

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
        hunger = Mathf.Clamp(hunger + hungerAmount, 0f, 100f);
        thirst = Mathf.Clamp(thirst + thirstAmount, 0f, 100f);
        drunk = Mathf.Clamp(drunk + drunkAmount, 0f, 100f);
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

        if (activeSceneName == MainMenuSceneName || activeSceneName == LaptopSceneName)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        float previousHunger = hunger;
        float previousThirst = thirst;
        float previousDrunk = drunk;

        hunger = Mathf.MoveTowards(hunger, 0f, hungerDecayPerSecond * deltaTime);
        thirst = Mathf.MoveTowards(thirst, 0f, thirstDecayPerSecond * deltaTime);
        drunk = Mathf.MoveTowards(drunk, 0f, drunkDecayPerSecond * deltaTime);

        if (!Mathf.Approximately(previousHunger, hunger)
            || !Mathf.Approximately(previousThirst, thirst)
            || !Mathf.Approximately(previousDrunk, drunk))
        {
            RefreshHud();
            ValuesChanged?.Invoke();
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
        bool shouldHide = scene.name == MainMenuSceneName || scene.name == LaptopSceneName;

        if (needsCanvas != null)
        {
            needsCanvas.gameObject.SetActive(!shouldHide);
        }
    }

    private void AttachDrunkEffects()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        if (activeSceneName == MainMenuSceneName || activeSceneName == LaptopSceneName)
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

        GameObject label = new GameObject("Needs Label", typeof(RectTransform));
        label.transform.SetParent(needsCanvas.transform, false);

        Image background = label.AddComponent<Image>();
        background.color = new Color(0.06f, 0.09f, 0.11f, 0.62f);
        background.raycastTarget = false;

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(1f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(1f, 0f);
        labelRect.anchoredPosition = new Vector2(-24f, 24f);
        labelRect.sizeDelta = new Vector2(340f, 126f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(label.transform, false);

        needsText = textObject.AddComponent<Text>();
        needsText.font = UiFontUtility.DefaultFont;
        needsText.fontSize = 23;
        needsText.fontStyle = FontStyle.Bold;
        needsText.alignment = TextAnchor.MiddleLeft;
        needsText.color = new Color(0.94f, 0.98f, 1f, 1f);
        needsText.lineSpacing = 1.08f;
        needsText.raycastTarget = false;

        RectTransform textRect = needsText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 8f);
        textRect.offsetMax = new Vector2(-18f, -8f);

        RefreshHud();
    }

    private void RefreshHud()
    {
        if (needsText == null)
        {
            return;
        }

        needsText.text =
            $"Hungry:  {CreateBar(hunger)} {Mathf.RoundToInt(hunger)}\n"
            + $"Thursty: {CreateBar(thirst)} {Mathf.RoundToInt(thirst)}\n"
            + $"Drunk:   {CreateBar(drunk)} {Mathf.RoundToInt(drunk)}";
    }

    private static string CreateBar(float value)
    {
        int filled = Mathf.RoundToInt(Mathf.Clamp01(value / 100f) * 10f);
        return $"[{new string('#', filled)}{new string('.', 10 - filled)}]";
    }
}
