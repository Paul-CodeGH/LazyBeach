using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CashManager : MonoBehaviour
{
    private const string PrefabPath = "Prefabs/CashManager";
    private const string MainMenuSceneName = "MainMenu";
    private const string LaptopSceneName = "LaptopScene";
    private const string EndingScreenSceneName = "EndingScreen";

    public static CashManager Instance { get; private set; }

    [SerializeField] private int startingAmount = 500;

    private Canvas cashCanvas;
    private Text cashText;
    private int currentCash;

    public event Action<int> CashChanged;

    public int CurrentCash => currentCash;

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

        new GameObject(nameof(CashManager)).AddComponent<CashManager>();
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (currentCash < amount)
        {
            return false;
        }

        SetCash(currentCash - amount);
        return true;
    }

    public void AddCash(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetCash(currentCash + amount);
    }

    public void ResetForNewGame()
    {
        SetCash(startingAmount);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentCash = startingAmount;
        DontDestroyOnLoad(gameObject);
        CreateHud();
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateSceneVisibility(SceneManager.GetActiveScene());
        RefreshHud();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSceneVisibility(scene);
    }

    private void UpdateSceneVisibility(Scene scene)
    {
        if (cashCanvas != null)
        {
            cashCanvas.gameObject.SetActive(scene.name != MainMenuSceneName && scene.name != LaptopSceneName && scene.name != EndingScreenSceneName);
        }
    }

    private void SetCash(int amount)
    {
        currentCash = Mathf.Max(0, amount);
        RefreshHud();
        CashChanged?.Invoke(currentCash);
    }

    private void CreateHud()
    {
        cashCanvas = new GameObject("Cash HUD", typeof(RectTransform)).AddComponent<Canvas>();
        cashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cashCanvas.sortingOrder = 500;
        cashCanvas.transform.SetParent(transform, false);

        CanvasScaler scaler = cashCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject label = new GameObject("Cash Label", typeof(RectTransform));
        label.transform.SetParent(cashCanvas.transform, false);

        Image background = label.AddComponent<Image>();
        background.color = new Color(0.08f, 0.12f, 0.15f, 0.62f);
        background.raycastTarget = false;

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 0f);
        labelRect.pivot = new Vector2(0f, 0f);
        labelRect.anchoredPosition = new Vector2(24f, 24f);
        labelRect.sizeDelta = new Vector2(230f, 54f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(label.transform, false);

        cashText = textObject.AddComponent<Text>();
        cashText.font = UiFontUtility.DefaultFont;
        cashText.fontSize = 28;
        cashText.fontStyle = FontStyle.Bold;
        cashText.alignment = TextAnchor.MiddleLeft;
        cashText.color = new Color(1f, 0.92f, 0.55f, 1f);
        cashText.raycastTarget = false;

        RectTransform textRect = cashText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 0f);
        textRect.offsetMax = new Vector2(-18f, 0f);
    }

    private void RefreshHud()
    {
        if (cashText != null)
        {
            cashText.text = $"Cash: {currentCash}";
        }
    }
}
