using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LaptopSceneController : MonoBehaviour
{
    private enum LaptopTab
    {
        RealEstate,
        Stocks,
        AngelInvestments
    }

    [SerializeField] private string characterHouseSceneName = "CharacterHouse";
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float fadeInDuration = 0.4f;

    private LaptopBusinessManager businessManager;
    private Text cashText;
    private Text incomeText;
    private Text payoutText;
    private Text messageText;
    private RectTransform contentRoot;
    private LaptopTab activeTab;
    private Text[] stockPriceTexts = Array.Empty<Text>();
    private Text[] stockOwnedTexts = Array.Empty<Text>();
    private InputField[] stockInputs = Array.Empty<InputField>();
    private StockGraphGraphic[] stockGraphs = Array.Empty<StockGraphGraphic>();
    private int selectedAngelIndex;
    private int displayedAngelVersion = -1;
    private bool showingAngelPortfolio;
    private ScrollRect angelScrollRect;
    private Text angelRefreshText;
    private Text[] angelStatusTexts = Array.Empty<Text>();
    private Button[] angelInvestButtons = Array.Empty<Button>();

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        CashManager.EnsureExists();
        LaptopBusinessManager.EnsureExists();
        businessManager = LaptopBusinessManager.Instance;

        if (businessManager != null)
        {
            businessManager.StateChanged += Refresh;
        }

        if (CashManager.Instance != null)
        {
            CashManager.Instance.CashChanged += OnCashChanged;
        }

        EnsureEventSystem();
        CreateInterface();
        ShowRealEstate();
    }

    private void OnDestroy()
    {
        if (businessManager != null)
        {
            businessManager.StateChanged -= Refresh;
        }

        if (CashManager.Instance != null)
        {
            CashManager.Instance.CashChanged -= OnCashChanged;
        }
    }

    private void Update()
    {
        RefreshHeader();

        if (activeTab == LaptopTab.Stocks)
        {
            RefreshStockWidgets();
        }
        else if (activeTab == LaptopTab.AngelInvestments && !showingAngelPortfolio)
        {
            RefreshAngelTab();
        }
    }

    private void OnCashChanged(int cash)
    {
        RefreshHeader();
    }

    private void CreateInterface()
    {
        Canvas canvas = new GameObject("Laptop Screen UI", typeof(RectTransform)).AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvas.gameObject.AddComponent<GraphicRaycaster>();

        Image background = canvas.gameObject.AddComponent<Image>();
        background.color = new Color(0.01f, 0.015f, 0.018f, 1f);

        RectTransform screen = CreateRect("Laptop Screen", canvas.transform);
        screen.anchorMin = new Vector2(0.045f, 0.065f);
        screen.anchorMax = new Vector2(0.955f, 0.935f);
        screen.offsetMin = Vector2.zero;
        screen.offsetMax = Vector2.zero;

        Image screenImage = screen.gameObject.AddComponent<Image>();
        screenImage.color = new Color(0.035f, 0.105f, 0.12f, 1f);

        RectTransform topBar = CreateRect("Top Bar", screen);
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = new Vector2(1f, 1f);
        topBar.pivot = new Vector2(0.5f, 1f);
        topBar.anchoredPosition = Vector2.zero;
        topBar.sizeDelta = new Vector2(0f, 104f);

        Image topBarImage = topBar.gameObject.AddComponent<Image>();
        topBarImage.color = new Color(0.055f, 0.16f, 0.18f, 1f);

        Text title = CreateText("Laptop Workstation", topBar, 32, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.76f, 1f, 0.92f, 1f));
        SetStretch(title.rectTransform, new Vector2(28f, 42f), new Vector2(-700f, -10f));

        cashText = CreateText(string.Empty, topBar, 24, FontStyle.Bold, TextAnchor.MiddleRight, new Color(1f, 0.92f, 0.55f, 1f));
        SetStretch(cashText.rectTransform, new Vector2(840f, 54f), new Vector2(-96f, -12f));

        incomeText = CreateText(string.Empty, topBar, 20, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.64f, 1f, 0.74f, 1f));
        SetStretch(incomeText.rectTransform, new Vector2(840f, 18f), new Vector2(-96f, -50f));

        payoutText = CreateText(string.Empty, topBar, 18, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.72f, 0.9f, 1f, 1f));
        SetStretch(payoutText.rectTransform, new Vector2(840f, 0f), new Vector2(-96f, -76f));

        Button closeButton = CreateButton("X", topBar, 30, new Color(0.62f, 0.12f, 0.11f, 1f), new Color(1f, 0.86f, 0.78f, 1f));
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-24f, -22f);
        closeRect.sizeDelta = new Vector2(54f, 54f);
        closeButton.onClick.AddListener(CloseLaptop);

        RectTransform tabBar = CreateRect("Tabs", screen);
        tabBar.anchorMin = new Vector2(0f, 1f);
        tabBar.anchorMax = new Vector2(1f, 1f);
        tabBar.pivot = new Vector2(0.5f, 1f);
        tabBar.anchoredPosition = new Vector2(0f, -118f);
        tabBar.sizeDelta = new Vector2(0f, 60f);

        HorizontalLayoutGroup tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.padding = new RectOffset(28, 28, 0, 0);
        tabLayout.spacing = 16f;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = true;

        Button realEstateTab = CreateButton("Real Estate", tabBar, 23, new Color(0.12f, 0.34f, 0.26f, 1f), Color.white);
        realEstateTab.gameObject.AddComponent<LayoutElement>().preferredWidth = 230f;
        realEstateTab.onClick.AddListener(ShowRealEstate);

        Button stocksTab = CreateButton("Stocks", tabBar, 23, new Color(0.12f, 0.24f, 0.42f, 1f), Color.white);
        stocksTab.gameObject.AddComponent<LayoutElement>().preferredWidth = 190f;
        stocksTab.onClick.AddListener(ShowStocks);

        Button angelTab = CreateButton("Angel Investments", tabBar, 22, new Color(0.33f, 0.19f, 0.42f, 1f), Color.white);
        angelTab.gameObject.AddComponent<LayoutElement>().preferredWidth = 300f;
        angelTab.onClick.AddListener(ShowAngelInvestments);

        contentRoot = CreateRect("Content", screen);
        contentRoot.anchorMin = new Vector2(0f, 0f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.offsetMin = new Vector2(30f, 78f);
        contentRoot.offsetMax = new Vector2(-30f, -194f);

        messageText = CreateText(string.Empty, screen, 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.52f, 1f));
        messageText.rectTransform.anchorMin = new Vector2(0f, 0f);
        messageText.rectTransform.anchorMax = new Vector2(1f, 0f);
        messageText.rectTransform.pivot = new Vector2(0.5f, 0f);
        messageText.rectTransform.anchoredPosition = new Vector2(0f, 24f);
        messageText.rectTransform.sizeDelta = new Vector2(-60f, 42f);

        RefreshHeader();
    }

    private void ShowRealEstate()
    {
        activeTab = LaptopTab.RealEstate;
        ClearContent();
        stockPriceTexts = Array.Empty<Text>();
        stockOwnedTexts = Array.Empty<Text>();
        stockInputs = Array.Empty<InputField>();
        stockGraphs = Array.Empty<StockGraphGraphic>();
        showingAngelPortfolio = false;
        angelScrollRect = null;
        angelRefreshText = null;
        angelStatusTexts = Array.Empty<Text>();
        angelInvestButtons = Array.Empty<Button>();

        RectTransform listContent = CreateScrollContent(contentRoot, new Color(0.018f, 0.06f, 0.058f, 1f));
        RealEstateInvestment[] investments = businessManager.RealEstateInvestments;

        for (int i = 0; i < investments.Length; i++)
        {
            AddRealEstateCard(listContent, investments[i], i);
        }

        SetMessage("Buy properties and upgrade them to increase automatic income every 10 seconds.");
        RefreshHeader();
    }

    private void ShowStocks()
    {
        activeTab = LaptopTab.Stocks;
        ClearContent();

        StockInvestment[] stocks = businessManager.Stocks;
        stockPriceTexts = new Text[stocks.Length];
        stockOwnedTexts = new Text[stocks.Length];
        stockInputs = new InputField[stocks.Length];
        stockGraphs = new StockGraphGraphic[stocks.Length];
        showingAngelPortfolio = false;
        angelScrollRect = null;
        angelRefreshText = null;
        angelStatusTexts = Array.Empty<Text>();
        angelInvestButtons = Array.Empty<Button>();

        RectTransform listContent = CreateScrollContent(contentRoot, new Color(0.018f, 0.045f, 0.075f, 1f));

        for (int i = 0; i < stocks.Length; i++)
        {
            AddStockCard(listContent, stocks[i], i);
        }

        SetMessage("Stock prices move automatically. Type a share amount, then buy or sell.");
        RefreshStockWidgets();
    }

    private void ShowAngelInvestments()
    {
        ShowAngelInvestments(-1f);
    }

    private void ShowAngelInvestments(float scrollPosition)
    {
        activeTab = LaptopTab.AngelInvestments;
        showingAngelPortfolio = false;
        ClearContent();
        stockPriceTexts = Array.Empty<Text>();
        stockOwnedTexts = Array.Empty<Text>();
        stockInputs = Array.Empty<InputField>();
        stockGraphs = Array.Empty<StockGraphGraphic>();

        AngelInvestment[] investments = businessManager.AngelInvestments;
        angelStatusTexts = new Text[investments.Length];
        angelInvestButtons = new Button[investments.Length];
        selectedAngelIndex = Mathf.Clamp(selectedAngelIndex, 0, Mathf.Max(0, investments.Length - 1));
        displayedAngelVersion = businessManager.AngelOpportunityVersion;

        RectTransform listContent = CreateScrollContent(contentRoot, new Color(0.055f, 0.035f, 0.075f, 1f));
        angelScrollRect = listContent.GetComponentInParent<ScrollRect>();
        AddAngelRefreshCard(listContent);

        for (int i = 0; i < investments.Length; i++)
        {
            AddAngelInvestmentCard(listContent, investments[i], i);
        }

        SetMessage("Open angel deals refresh every 5 minutes. Funded deals stay pinned until you sell or close them.");
        RefreshAngelWidgets();

        if (scrollPosition >= 0f)
        {
            StartCoroutine(RestoreAngelScrollPosition(scrollPosition));
        }
    }

    private void ShowAngelPortfolio()
    {
        ShowAngelPortfolio(-1f);
    }

    private void ShowAngelPortfolio(float scrollPosition)
    {
        activeTab = LaptopTab.AngelInvestments;
        showingAngelPortfolio = true;
        ClearContent();
        stockPriceTexts = Array.Empty<Text>();
        stockOwnedTexts = Array.Empty<Text>();
        stockInputs = Array.Empty<InputField>();
        stockGraphs = Array.Empty<StockGraphGraphic>();
        angelRefreshText = null;
        angelStatusTexts = Array.Empty<Text>();
        angelInvestButtons = Array.Empty<Button>();

        RectTransform listContent = CreateScrollContent(contentRoot, new Color(0.048f, 0.038f, 0.065f, 1f));
        angelScrollRect = listContent.GetComponentInParent<ScrollRect>();

        AddAngelPortfolioSummaryCard(listContent);
        AddAngelPortfolioRecordCards(listContent);

        SetMessage("Angel investment history shows active positions, exits, sold stakes, and total gain or loss.");

        if (scrollPosition >= 0f)
        {
            StartCoroutine(RestoreAngelScrollPosition(scrollPosition));
        }
    }

    private void AddRealEstateCard(Transform parent, RealEstateInvestment investment, int index)
    {
        RectTransform card = CreateCard(parent, 132f, new Color(0.08f, 0.18f, 0.13f, 0.96f));

        Text info = CreateText(string.Empty, card, 21, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.91f, 1f, 0.88f, 1f));
        SetStretch(info.rectTransform, new Vector2(20f, 14f), new Vector2(-250f, -14f));

        string state = investment.IsOwned
            ? $"Level {investment.Level}/{investment.MaxLevel}   Income: ${investment.CurrentIncome}/10s"
            : $"Cost: ${investment.PurchaseCost}   Income: ${investment.BaseIncome}/10s";

        info.text = $"{investment.DisplayName}\n{investment.Description}\n{state}";
        info.lineSpacing = 1.1f;

        Button actionButton = CreateButton(GetRealEstateButtonLabel(investment), card, 20, new Color(0.78f, 0.48f, 0.17f, 1f), new Color(1f, 0.96f, 0.75f, 1f));
        RectTransform buttonRect = actionButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-18f, 0f);
        buttonRect.sizeDelta = new Vector2(204f, 56f);
        actionButton.interactable = !investment.IsOwned || investment.CanUpgrade;
        actionButton.onClick.AddListener(() => HandleRealEstateAction(index));
    }

    private void AddStockCard(Transform parent, StockInvestment stock, int index)
    {
        RectTransform card = CreateCard(parent, 238f, new Color(0.06f, 0.12f, 0.2f, 0.96f));

        Text title = CreateText($"{stock.CompanyName} ({stock.Symbol})", card, 24, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.94f, 1f, 1f));
        SetStretch(title.rectTransform, new Vector2(20f, 182f), new Vector2(-20f, -14f));

        stockPriceTexts[index] = CreateText(string.Empty, card, 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.8f, 1f, 0.76f, 1f));
        SetStretch(stockPriceTexts[index].rectTransform, new Vector2(20f, 142f), new Vector2(-420f, -54f));

        stockOwnedTexts[index] = CreateText(string.Empty, card, 19, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
        SetStretch(stockOwnedTexts[index].rectTransform, new Vector2(20f, 108f), new Vector2(-420f, -88f));

        RectTransform graphRoot = CreateRect("Graph", card);
        graphRoot.anchorMin = new Vector2(0f, 0f);
        graphRoot.anchorMax = new Vector2(1f, 0f);
        graphRoot.pivot = new Vector2(0.5f, 0f);
        graphRoot.anchoredPosition = new Vector2(-170f, 18f);
        graphRoot.sizeDelta = new Vector2(-380f, 82f);

        Image graphBackground = graphRoot.gameObject.AddComponent<Image>();
        graphBackground.color = new Color(0.025f, 0.09f, 0.12f, 1f);

        Outline graphOutline = graphRoot.gameObject.AddComponent<Outline>();
        graphOutline.effectColor = new Color(0.22f, 0.9f, 1f, 0.7f);
        graphOutline.effectDistance = new Vector2(2f, -2f);

        GameObject lineObject = new GameObject("Line", typeof(RectTransform));
        lineObject.transform.SetParent(graphRoot, false);

        StockGraphGraphic graph = lineObject.AddComponent<StockGraphGraphic>();
        graph.color = new Color(0.18f, 1f, 0.54f, 1f);
        graph.raycastTarget = false;
        RectTransform graphRect = graph.GetComponent<RectTransform>();
        graphRect.anchorMin = new Vector2(0f, 0f);
        graphRect.anchorMax = new Vector2(1f, 1f);
        graphRect.offsetMin = new Vector2(10f, 10f);
        graphRect.offsetMax = new Vector2(-10f, -10f);
        stockGraphs[index] = graph;

        InputField input = CreateInput(card, "Shares");
        RectTransform inputRect = input.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(1f, 0.5f);
        inputRect.anchorMax = new Vector2(1f, 0.5f);
        inputRect.pivot = new Vector2(1f, 0.5f);
        inputRect.anchoredPosition = new Vector2(-230f, -26f);
        inputRect.sizeDelta = new Vector2(150f, 48f);
        stockInputs[index] = input;

        Button buyButton = CreateButton("Buy", card, 20, new Color(0.14f, 0.48f, 0.24f, 1f), Color.white);
        RectTransform buyRect = buyButton.GetComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(1f, 0.5f);
        buyRect.anchorMax = new Vector2(1f, 0.5f);
        buyRect.pivot = new Vector2(1f, 0.5f);
        buyRect.anchoredPosition = new Vector2(-118f, -26f);
        buyRect.sizeDelta = new Vector2(96f, 48f);
        buyButton.onClick.AddListener(() => BuyStock(index));

        Button sellButton = CreateButton("Sell", card, 20, new Color(0.52f, 0.18f, 0.14f, 1f), Color.white);
        RectTransform sellRect = sellButton.GetComponent<RectTransform>();
        sellRect.anchorMin = new Vector2(1f, 0.5f);
        sellRect.anchorMax = new Vector2(1f, 0.5f);
        sellRect.pivot = new Vector2(1f, 0.5f);
        sellRect.anchoredPosition = new Vector2(-18f, -26f);
        sellRect.sizeDelta = new Vector2(88f, 48f);
        sellButton.onClick.AddListener(() => SellStock(index));
    }

    private void AddAngelRefreshCard(Transform parent)
    {
        RectTransform card = CreateCard(parent, 78f, new Color(0.09f, 0.055f, 0.125f, 0.98f));

        angelRefreshText = CreateText(string.Empty, card, 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.86f, 1f, 1f));
        SetStretch(angelRefreshText.rectTransform, new Vector2(20f, 8f), new Vector2(-240f, -8f));

        Button investmentsButton = CreateButton("Investments", card, 18, new Color(0.36f, 0.22f, 0.5f, 1f), Color.white);
        RectTransform buttonRect = investmentsButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-20f, 0f);
        buttonRect.sizeDelta = new Vector2(190f, 48f);
        investmentsButton.onClick.AddListener(ShowAngelPortfolio);
    }

    private void AddAngelPortfolioSummaryCard(Transform parent)
    {
        RectTransform card = CreateCard(parent, 156f, new Color(0.12f, 0.075f, 0.16f, 0.98f));

        int totalInvested = businessManager.AngelInvestmentTotalInvested;
        int totalReturned = businessManager.AngelInvestmentTotalReturned;
        int netProfit = businessManager.AngelInvestmentNetProfit;
        string netLabel = netProfit >= 0 ? $"Profit: ${netProfit}" : $"Loss: ${Mathf.Abs(netProfit)}";

        Text title = CreateText("Angel Investments Portfolio", card, 26, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.88f, 1f, 1f));
        SetStretch(title.rectTransform, new Vector2(20f, 96f), new Vector2(-260f, -12f));

        Text summary = CreateText($"Invested: ${totalInvested}   Returned: ${totalReturned}   {netLabel}   Active: {businessManager.ActiveAngelInvestmentCount}", card, 21, FontStyle.Bold, TextAnchor.MiddleLeft, netProfit >= 0 ? new Color(0.72f, 1f, 0.82f, 1f) : new Color(1f, 0.66f, 0.58f, 1f));
        SetStretch(summary.rectTransform, new Vector2(20f, 42f), new Vector2(-260f, -66f));

        Button backButton = CreateButton("Opportunities", card, 18, new Color(0.27f, 0.18f, 0.36f, 1f), Color.white);
        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(1f, 0.5f);
        backRect.anchorMax = new Vector2(1f, 0.5f);
        backRect.pivot = new Vector2(1f, 0.5f);
        backRect.anchoredPosition = new Vector2(-20f, 0f);
        backRect.sizeDelta = new Vector2(190f, 52f);
        backButton.onClick.AddListener(ShowAngelInvestments);
    }

    private void AddAngelPortfolioRecordCards(Transform parent)
    {
        var records = businessManager.AngelInvestmentRecords;

        if (records.Count == 0)
        {
            RectTransform emptyCard = CreateCard(parent, 96f, new Color(0.09f, 0.06f, 0.12f, 0.96f));
            Text emptyText = CreateText("No angel investments yet.", emptyCard, 21, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.9f, 1f, 1f));
            SetStretch(emptyText.rectTransform, new Vector2(20f, 10f), new Vector2(-20f, -10f));
            return;
        }

        for (int i = records.Count - 1; i >= 0; i--)
        {
            AddAngelPortfolioRecordCard(parent, records[i]);
        }
    }

    private void AddAngelPortfolioRecordCard(Transform parent, AngelInvestmentRecord record)
    {
        AngelInvestment activeInvestment = FindVisibleAngelInvestmentByRecordId(record.Id);
        RectTransform card = CreateCard(parent, 144f, record.IsActive ? new Color(0.15f, 0.09f, 0.2f, 0.97f) : new Color(0.095f, 0.065f, 0.12f, 0.96f));

        Text title = CreateText($"{record.DisplayName}  |  {record.Category}", card, 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.88f, 1f, 1f));
        SetStretch(title.rectTransform, new Vector2(20f, 88f), new Vector2(-20f, -12f));

        Text status = CreateText(GetAngelPortfolioRecordStatus(record, activeInvestment), card, 19, FontStyle.Bold, TextAnchor.MiddleLeft, GetAngelPortfolioRecordColor(record));
        SetStretch(status.rectTransform, new Vector2(20f, 42f), new Vector2(-20f, -58f));

        Text detail = CreateText($"Invested: ${record.InvestedAmount} for {record.EquityPercent:0.#}% equity   Returned: ${record.ReturnedAmount}   Net: {FormatSignedCash(record.Profit)}", card, 18, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.92f, 0.9f, 1f, 1f));
        SetStretch(detail.rectTransform, new Vector2(20f, 10f), new Vector2(-20f, -96f));
    }

    private void AddAngelInvestmentCard(Transform parent, AngelInvestment investment, int index)
    {
        bool isSelected = index == selectedAngelIndex;
        RectTransform card = CreateCard(parent, isSelected ? 360f : 190f, isSelected ? new Color(0.18f, 0.09f, 0.22f, 0.97f) : new Color(0.11f, 0.07f, 0.16f, 0.96f));

        Text title = CreateText($"{investment.DisplayName}  |  {investment.Category}", card, 23, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.88f, 1f, 1f));
        SetStretch(title.rectTransform, new Vector2(20f, isSelected ? 306f : 138f), new Vector2(-360f, -12f));

        Text summary = CreateText(investment.Description, card, 18, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.94f, 0.92f, 1f, 1f));
        SetStretch(summary.rectTransform, new Vector2(20f, isSelected ? 248f : 82f), new Vector2(-360f, isSelected ? -58f : -56f));

        Text terms = CreateText($"Ask: ${investment.AskPrice} for {investment.EquityPercent:0.#}% equity", card, 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.88f, 0.48f, 1f));
        SetStretch(terms.rectTransform, new Vector2(20f, isSelected ? 208f : 48f), new Vector2(-360f, isSelected ? -116f : -112f));

        angelStatusTexts[index] = CreateText(string.Empty, card, 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.72f, 1f, 0.82f, 1f));
        SetStretch(angelStatusTexts[index].rectTransform, new Vector2(20f, isSelected ? 170f : 14f), new Vector2(-360f, isSelected ? -156f : -140f));

        Button analyseButton = CreateButton(isSelected ? "Analysing" : "Analyse", card, 19, new Color(0.28f, 0.17f, 0.42f, 1f), Color.white);
        RectTransform analyseRect = analyseButton.GetComponent<RectTransform>();
        analyseRect.anchorMin = new Vector2(1f, 0.5f);
        analyseRect.anchorMax = new Vector2(1f, 0.5f);
        analyseRect.pivot = new Vector2(1f, 0.5f);
        analyseRect.anchoredPosition = new Vector2(-212f, isSelected ? 92f : 34f);
        analyseRect.sizeDelta = new Vector2(168f, 50f);
        analyseButton.onClick.AddListener(() => SelectAngelInvestment(index));

        Button investButton = CreateButton(GetAngelInvestmentButtonLabel(investment), card, 17, GetAngelInvestmentButtonColor(investment), Color.white);
        RectTransform investRect = investButton.GetComponent<RectTransform>();
        investRect.anchorMin = new Vector2(1f, 0.5f);
        investRect.anchorMax = new Vector2(1f, 0.5f);
        investRect.pivot = new Vector2(1f, 0.5f);
        investRect.anchoredPosition = new Vector2(-28f, isSelected ? 92f : 34f);
        investRect.sizeDelta = new Vector2(174f, 50f);
        investButton.onClick.AddListener(() => HandleAngelInvestmentAction(index));
        angelInvestButtons[index] = investButton;

        if (!isSelected)
        {
            return;
        }

        Text details = CreateText($"{investment.HistoricalDetails}\n\n{investment.InvestmentTerms}", card, 18, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.93f, 0.96f, 1f, 1f));
        SetStretch(details.rectTransform, new Vector2(20f, 18f), new Vector2(-28f, -214f));
        details.lineSpacing = 1.05f;
    }

    private void HandleRealEstateAction(int index)
    {
        RealEstateInvestment investment = businessManager.RealEstateInvestments[index];
        bool success = investment.IsOwned
            ? businessManager.UpgradeRealEstate(index)
            : businessManager.BuyRealEstate(index);

        SetMessage(success ? $"{investment.DisplayName} updated." : "Not enough cash for that real estate action.");
        ShowRealEstate();
    }

    private void BuyStock(int index)
    {
        int shares = GetRequestedShares(index);
        bool success = businessManager.BuyStock(index, shares);
        SetMessage(success ? $"Bought {shares} shares." : "Enter shares and make sure you have enough cash.");
        RefreshStockWidgets();
    }

    private void SellStock(int index)
    {
        int shares = GetRequestedShares(index);
        bool success = businessManager.SellStock(index, shares);
        SetMessage(success ? $"Sold {shares} shares." : "Enter shares and make sure you own enough to sell.");
        RefreshStockWidgets();
    }

    private void SelectAngelInvestment(int index)
    {
        float scrollPosition = GetAngelScrollPosition();
        selectedAngelIndex = index;
        ShowAngelInvestments(scrollPosition);
    }

    private void HandleAngelInvestmentAction(int index)
    {
        AngelInvestment investment = businessManager.AngelInvestments[index];

        if (investment.IsFunded)
        {
            ExitAngelInvestment(index);
            return;
        }

        BuyAngelInvestment(index);
    }

    private void BuyAngelInvestment(int index)
    {
        bool success = businessManager.BuyAngelInvestment(index);
        AngelInvestment investment = businessManager.AngelInvestments[index];

        SetMessage(success
            ? $"Invested ${investment.AskPrice} into {investment.DisplayName} for {investment.EquityPercent:0.#}% equity."
            : "Not enough cash, or that angel investment is already in your portfolio.");

        RefreshAngelWidgets();
    }

    private void ExitAngelInvestment(int index)
    {
        AngelInvestment investment = businessManager.AngelInvestments[index];
        string investmentName = investment.DisplayName;
        bool wasResolved = investment.IsResolved;
        bool success = businessManager.ExitAngelInvestment(index, out int recoveredCash);

        if (!success)
        {
            SetMessage("That angel position is not available to sell or close.");
            RefreshAngelWidgets();
            return;
        }

        string message = wasResolved
            ? $"{investmentName} removed from your angel portfolio."
            : $"Sold your {investmentName} stake and recovered ${recoveredCash}.";

        ShowAngelInvestments();
        SetMessage(message);
    }

    private int GetRequestedShares(int index)
    {
        if (stockInputs == null || index < 0 || index >= stockInputs.Length || stockInputs[index] == null)
        {
            return 0;
        }

        return int.TryParse(stockInputs[index].text, out int shares) ? Mathf.Max(0, shares) : 0;
    }

    private void Refresh()
    {
        RefreshHeader();

        if (activeTab == LaptopTab.Stocks)
        {
            RefreshStockWidgets();
        }
        else if (activeTab == LaptopTab.AngelInvestments)
        {
            if (showingAngelPortfolio)
            {
                ShowAngelPortfolio(GetAngelScrollPosition());
            }
            else
            {
                RefreshAngelTab();
            }
        }
    }

    private void RefreshHeader()
    {
        if (businessManager == null)
        {
            return;
        }

        int cash = CashManager.Instance != null ? CashManager.Instance.CurrentCash : 0;

        if (cashText != null)
        {
            cashText.text = $"Cash: ${cash}";
        }

        if (incomeText != null)
        {
            incomeText.text = $"Real estate income: ${businessManager.PassiveIncomePerTick}/10s";
        }

        if (payoutText != null)
        {
            payoutText.text = $"Next payout: {Mathf.CeilToInt(businessManager.IncomeTimeRemaining)}s";
        }
    }

    private void RefreshStockWidgets()
    {
        StockInvestment[] stocks = businessManager.Stocks;

        for (int i = 0; i < stocks.Length; i++)
        {
            StockInvestment stock = stocks[i];

            if (i < stockPriceTexts.Length && stockPriceTexts[i] != null)
            {
                stockPriceTexts[i].text = $"Price: ${stock.Price:0.00}";
            }

            if (i < stockOwnedTexts.Length && stockOwnedTexts[i] != null)
            {
                stockOwnedTexts[i].text = $"Owned: {stock.SharesOwned}   Value: ${stock.SharesOwned * stock.Price:0.00}";
            }

            if (i < stockGraphs.Length && stockGraphs[i] != null)
            {
                stockGraphs[i].SetValues(stock.PriceHistory, stock.HistoryCount);
            }
        }

        RefreshHeader();
    }

    private void RefreshAngelTab()
    {
        if (displayedAngelVersion != businessManager.AngelOpportunityVersion)
        {
            ShowAngelInvestments();
            return;
        }

        RefreshAngelWidgets();
    }

    private void RefreshAngelWidgets()
    {
        AngelInvestment[] investments = businessManager.AngelInvestments;

        if (angelRefreshText != null)
        {
            angelRefreshText.text = $"New open deals refresh in {FormatTime(businessManager.AngelOpportunityRefreshTimeRemaining)}. Funded deals stay visible until you sell or close them.";
        }

        for (int i = 0; i < investments.Length; i++)
        {
            AngelInvestment investment = investments[i];

            if (i < angelStatusTexts.Length && angelStatusTexts[i] != null)
            {
                angelStatusTexts[i].text = GetAngelInvestmentStatus(investment);
            }

            if (i < angelInvestButtons.Length && angelInvestButtons[i] != null)
            {
                angelInvestButtons[i].interactable = true;
                Color actionColor = GetAngelInvestmentButtonColor(investment);
                Image targetImage = angelInvestButtons[i].targetGraphic as Image;

                if (targetImage != null)
                {
                    targetImage.color = actionColor;
                }

                ColorBlock colors = angelInvestButtons[i].colors;
                colors.normalColor = actionColor;
                colors.highlightedColor = Color.Lerp(actionColor, Color.white, 0.22f);
                colors.pressedColor = Color.Lerp(actionColor, Color.black, 0.25f);
                colors.selectedColor = colors.highlightedColor;
                angelInvestButtons[i].colors = colors;

                Text buttonText = angelInvestButtons[i].GetComponentInChildren<Text>();

                if (buttonText != null)
                {
                    buttonText.text = GetAngelInvestmentButtonLabel(investment);
                }
            }
        }

        RefreshHeader();
    }

    private AngelInvestment FindVisibleAngelInvestmentByRecordId(int recordId)
    {
        AngelInvestment[] investments = businessManager.AngelInvestments;

        for (int i = 0; i < investments.Length; i++)
        {
            if (investments[i] != null && investments[i].RecordId == recordId)
            {
                return investments[i];
            }
        }

        return null;
    }

    private float GetAngelScrollPosition()
    {
        return angelScrollRect != null ? angelScrollRect.verticalNormalizedPosition : 1f;
    }

    private IEnumerator RestoreAngelScrollPosition(float scrollPosition)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (angelScrollRect != null)
        {
            angelScrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollPosition);
        }
    }

    private void CloseLaptop()
    {
        if (!Application.CanStreamedLevelBeLoaded(characterHouseSceneName))
        {
            Debug.LogWarning($"{nameof(LaptopSceneController)} could not load scene '{characterHouseSceneName}'. Add it to Build Settings first.", this);
            return;
        }

        SceneFadeTransition.LoadScene(characterHouseSceneName, fadeOutDuration, fadeInDuration, Color.black);
    }

    private void ClearContent()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }
    }

    private RectTransform CreateScrollContent(Transform parent, Color backgroundColor)
    {
        RectTransform scrollRoot = CreateRect("Scroll View", parent);
        scrollRoot.anchorMin = Vector2.zero;
        scrollRoot.anchorMax = Vector2.one;
        scrollRoot.offsetMin = Vector2.zero;
        scrollRoot.offsetMax = Vector2.zero;

        Image background = scrollRoot.gameObject.AddComponent<Image>();
        background.color = backgroundColor;

        ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 38f;

        RectTransform viewport = CreateRect("Viewport", scrollRoot);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(12f, 12f);
        viewport.offsetMax = new Vector2(-12f, -12f);

        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);

        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;

        return content;
    }

    private RectTransform CreateCard(Transform parent, float height, Color color)
    {
        RectTransform card = CreateRect("Card", parent);
        card.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

        Image image = card.gameObject.AddComponent<Image>();
        image.color = color;

        return card;
    }

    private Text CreateText(string value, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = UiFontUtility.DefaultFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;

        return text;
    }

    private Button CreateButton(string label, Transform parent, int fontSize, Color backgroundColor, Color textColor)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = backgroundColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.22f);
        colors.pressedColor = Color.Lerp(backgroundColor, Color.black, 0.25f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Text text = CreateText(label, buttonObject.transform, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
        SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);

        return button;
    }

    private InputField CreateInput(Transform parent, string placeholder)
    {
        GameObject inputObject = new GameObject("Share Input", typeof(RectTransform));
        inputObject.transform.SetParent(parent, false);

        Image image = inputObject.AddComponent<Image>();
        image.color = new Color(0.02f, 0.035f, 0.045f, 1f);

        InputField input = inputObject.AddComponent<InputField>();
        input.contentType = InputField.ContentType.IntegerNumber;
        input.caretColor = Color.white;
        input.selectionColor = new Color(0.3f, 0.75f, 1f, 0.45f);

        Text valueText = CreateText(string.Empty, inputObject.transform, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetStretch(valueText.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        Text placeholderText = CreateText(placeholder, inputObject.transform, 18, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.45f));
        SetStretch(placeholderText.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        input.textComponent = valueText;
        input.placeholder = placeholderText;

        return input;
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

    private static string GetRealEstateButtonLabel(RealEstateInvestment investment)
    {
        if (!investment.IsOwned)
        {
            return $"Buy ${investment.PurchaseCost}";
        }

        return investment.CanUpgrade ? $"Upgrade ${investment.NextUpgradeCost}" : "Max Level";
    }

    private static string GetAngelInvestmentButtonLabel(AngelInvestment investment)
    {
        if (!investment.IsFunded)
        {
            return $"Invest ${investment.AskPrice}";
        }

        if (!investment.IsResolved)
        {
            return $"Sell ${investment.ExitValue}";
        }

        return "Close";
    }

    private static Color GetAngelInvestmentButtonColor(AngelInvestment investment)
    {
        if (!investment.IsFunded)
        {
            return new Color(0.58f, 0.36f, 0.11f, 1f);
        }

        if (!investment.IsResolved)
        {
            return new Color(0.34f, 0.28f, 0.56f, 1f);
        }

        return new Color(0.22f, 0.22f, 0.26f, 1f);
    }

    private static string GetAngelInvestmentStatus(AngelInvestment investment)
    {
        if (!investment.IsFunded)
        {
            return "Open deal. Review the history before investing.";
        }

        if (!investment.IsResolved)
        {
            return $"In portfolio: {investment.EquityPercent:0.#}% stake. Review outcome in {investment.SecondsRemaining}s.";
        }

        if (investment.FinalPayout <= 0)
        {
            return "Outcome: company failed. Your stake is now $0.";
        }

        if (investment.Profit > 0)
        {
            return $"Outcome: exit paid ${investment.FinalPayout}. Profit: ${investment.Profit}.";
        }

        return $"Outcome: exit paid ${investment.FinalPayout}. Loss: ${Mathf.Abs(investment.Profit)}.";
    }

    private static string GetAngelPortfolioRecordStatus(AngelInvestmentRecord record, AngelInvestment activeInvestment)
    {
        if (record.IsActive && activeInvestment != null)
        {
            return $"Active: waiting for outcome in {activeInvestment.SecondsRemaining}s. Sell now for ${activeInvestment.ExitValue}.";
        }

        if (record.IsActive)
        {
            return "Active: currently in your angel portfolio.";
        }

        if (record.Status == "Sold")
        {
            return $"Sold early: recovered ${record.ReturnedAmount}. Net {FormatSignedCash(record.Profit)}.";
        }

        if (record.Status == "Failed")
        {
            return $"Failed: company went to $0. Net {FormatSignedCash(record.Profit)}.";
        }

        return $"Resolved: payout ${record.ReturnedAmount}. Net {FormatSignedCash(record.Profit)}.";
    }

    private static Color GetAngelPortfolioRecordColor(AngelInvestmentRecord record)
    {
        if (record.IsActive)
        {
            return new Color(0.82f, 0.86f, 1f, 1f);
        }

        return record.Profit >= 0
            ? new Color(0.72f, 1f, 0.82f, 1f)
            : new Color(1f, 0.66f, 0.58f, 1f);
    }

    private static string FormatSignedCash(int amount)
    {
        return amount >= 0 ? $"+${amount}" : $"-${Mathf.Abs(amount)}";
    }

    private static string FormatTime(float seconds)
    {
        int remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = remainingSeconds / 60;
        int secondsPart = remainingSeconds % 60;
        return $"{minutes:0}:{secondsPart:00}";
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
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
