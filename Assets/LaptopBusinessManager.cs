using System;
using UnityEngine;

public sealed class LaptopBusinessManager : MonoBehaviour
{
    private const float IncomeInterval = 10f;

    public static LaptopBusinessManager Instance { get; private set; }

    private RealEstateInvestment[] realEstateInvestments;
    private StockInvestment[] stocks;
    private float incomeTimer = IncomeInterval;
    private float stockTimer;

    public event Action StateChanged;

    public RealEstateInvestment[] RealEstateInvestments => realEstateInvestments;
    public StockInvestment[] Stocks => stocks;
    public float IncomeTimeRemaining => incomeTimer;
    public int PassiveIncomePerTick
    {
        get
        {
            int total = 0;

            for (int i = 0; i < realEstateInvestments.Length; i++)
            {
                total += realEstateInvestments[i].CurrentIncome;
            }

            return total;
        }
    }

    public static void EnsureExists()
    {
        if (Instance != null)
        {
            return;
        }

        new GameObject(nameof(LaptopBusinessManager)).AddComponent<LaptopBusinessManager>();
    }

    public bool BuyRealEstate(int index)
    {
        if (!IsValidRealEstateIndex(index))
        {
            return false;
        }

        RealEstateInvestment investment = realEstateInvestments[index];

        if (investment.IsOwned)
        {
            return false;
        }

        CashManager.EnsureExists();

        if (CashManager.Instance == null || !CashManager.Instance.TrySpend(investment.PurchaseCost))
        {
            return false;
        }

        investment.Buy();
        StateChanged?.Invoke();
        return true;
    }

    public bool UpgradeRealEstate(int index)
    {
        if (!IsValidRealEstateIndex(index))
        {
            return false;
        }

        RealEstateInvestment investment = realEstateInvestments[index];

        if (!investment.CanUpgrade)
        {
            return false;
        }

        CashManager.EnsureExists();

        if (CashManager.Instance == null || !CashManager.Instance.TrySpend(investment.NextUpgradeCost))
        {
            return false;
        }

        investment.Upgrade();
        StateChanged?.Invoke();
        return true;
    }

    public bool BuyStock(int index, int shares)
    {
        if (!IsValidStockIndex(index) || shares <= 0)
        {
            return false;
        }

        CashManager.EnsureExists();

        StockInvestment stock = stocks[index];
        int cost = Mathf.CeilToInt(stock.Price * shares);

        if (CashManager.Instance == null || !CashManager.Instance.TrySpend(cost))
        {
            return false;
        }

        stock.Buy(shares);
        StateChanged?.Invoke();
        return true;
    }

    public bool SellStock(int index, int shares)
    {
        if (!IsValidStockIndex(index) || shares <= 0)
        {
            return false;
        }

        StockInvestment stock = stocks[index];

        if (stock.SharesOwned < shares)
        {
            return false;
        }

        CashManager.EnsureExists();
        int proceeds = Mathf.FloorToInt(stock.Price * shares);
        stock.Sell(shares);
        CashManager.Instance?.AddCash(proceeds);
        StateChanged?.Invoke();
        return true;
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
        realEstateInvestments = CreateRealEstateInvestments();
        stocks = CreateStocks();
    }

    private void Update()
    {
        UpdateIncome();
        UpdateStocks();
    }

    private void UpdateIncome()
    {
        incomeTimer -= Time.deltaTime;

        if (incomeTimer > 0f)
        {
            return;
        }

        incomeTimer = IncomeInterval;
        int income = PassiveIncomePerTick;

        if (income <= 0)
        {
            StateChanged?.Invoke();
            return;
        }

        CashManager.EnsureExists();
        CashManager.Instance?.AddCash(income);
        StateChanged?.Invoke();
    }

    private void UpdateStocks()
    {
        stockTimer -= Time.deltaTime;

        if (stockTimer > 0f)
        {
            return;
        }

        stockTimer = 1f;

        for (int i = 0; i < stocks.Length; i++)
        {
            stocks[i].UpdatePrice(Time.time);
        }

        StateChanged?.Invoke();
    }

    private bool IsValidRealEstateIndex(int index)
    {
        return realEstateInvestments != null && index >= 0 && index < realEstateInvestments.Length;
    }

    private bool IsValidStockIndex(int index)
    {
        return stocks != null && index >= 0 && index < stocks.Length;
    }

    private static RealEstateInvestment[] CreateRealEstateInvestments()
    {
        return new[]
        {
            new RealEstateInvestment("Beach Shack", "A tiny rental near the boardwalk.", 120, 2, 90, 4),
            new RealEstateInvestment("Studio Condo", "Low-cost apartment with steady rent.", 450, 6, 280, 5),
            new RealEstateInvestment("Family House", "A larger home with reliable tenants.", 1400, 22, 800, 5),
            new RealEstateInvestment("Beach Villa", "Premium vacation rental income.", 4200, 80, 2300, 4),
            new RealEstateInvestment("Lazy Resort", "High-end resort with major payouts.", 12000, 260, 6500, 3)
        };
    }

    private static StockInvestment[] CreateStocks()
    {
        return new[]
        {
            new StockInvestment("SunPeak Energy", "SPE", 18f, 4.2f, 0.2f),
            new StockInvestment("BlueWave Foods", "BWF", 42f, 3.1f, 1.7f)
        };
    }
}

[Serializable]
public sealed class RealEstateInvestment
{
    [SerializeField] private string displayName;
    [SerializeField] private string description;
    [SerializeField] private int purchaseCost;
    [SerializeField] private int baseIncome;
    [SerializeField] private int upgradeCost;
    [SerializeField] private int maxLevel;
    [SerializeField] private int level;

    public string DisplayName => displayName;
    public string Description => description;
    public int PurchaseCost => purchaseCost;
    public int BaseIncome => baseIncome;
    public int UpgradeCost => upgradeCost;
    public int MaxLevel => maxLevel;
    public int Level => level;
    public bool IsOwned => level > 0;
    public bool CanUpgrade => IsOwned && level < maxLevel;
    public int CurrentIncome => level * baseIncome;
    public int NextUpgradeCost => Mathf.RoundToInt(upgradeCost * Mathf.Pow(1.75f, Mathf.Max(0, level - 1)));

    public RealEstateInvestment(string displayName, string description, int purchaseCost, int baseIncome, int upgradeCost, int maxLevel)
    {
        this.displayName = displayName;
        this.description = description;
        this.purchaseCost = purchaseCost;
        this.baseIncome = baseIncome;
        this.upgradeCost = upgradeCost;
        this.maxLevel = maxLevel;
    }

    public void Buy()
    {
        level = 1;
    }

    public void Upgrade()
    {
        level = Mathf.Min(level + 1, maxLevel);
    }
}

[Serializable]
public sealed class StockInvestment
{
    private const int HistoryLength = 48;

    [SerializeField] private string companyName;
    [SerializeField] private string symbol;
    [SerializeField] private float price;
    [SerializeField] private int sharesOwned;
    [SerializeField] private float volatility;
    [SerializeField] private float phase;

    private readonly float[] priceHistory = new float[HistoryLength];
    private int historyCount;

    public string CompanyName => companyName;
    public string Symbol => symbol;
    public float Price => price;
    public int SharesOwned => sharesOwned;
    public float[] PriceHistory => priceHistory;
    public int HistoryCount => historyCount;

    public StockInvestment(string companyName, string symbol, float startingPrice, float volatility, float phase)
    {
        this.companyName = companyName;
        this.symbol = symbol;
        this.price = startingPrice;
        this.volatility = volatility;
        this.phase = phase;

        for (int i = 0; i < priceHistory.Length; i++)
        {
            float position = i / Mathf.Max(1f, priceHistory.Length - 1f);
            float wave = Mathf.Sin(position * Mathf.PI * 4f + phase) * 0.018f;
            float pulse = Mathf.Sin(position * Mathf.PI * 9f + phase * 1.7f) * 0.007f;
            float trend = Mathf.Lerp(-0.01f, 0f, position);
            priceHistory[i] = Mathf.Max(1f, startingPrice * (1f + wave + pulse + trend));
        }

        priceHistory[priceHistory.Length - 1] = startingPrice;
        historyCount = priceHistory.Length;
    }

    public void UpdatePrice(float time)
    {
        float wave = Mathf.Sin(time * 0.31f + phase) * volatility * 0.0035f;
        float pulse = Mathf.Sin(time * 0.93f + phase * 2.1f) * volatility * 0.0018f;
        float random = UnityEngine.Random.Range(-volatility, volatility) * 0.0012f;
        price = Mathf.Max(1f, price * (1f + wave + pulse + random));
        PushHistory(price);
    }

    public void Buy(int shares)
    {
        sharesOwned += shares;
    }

    public void Sell(int shares)
    {
        sharesOwned = Mathf.Max(0, sharesOwned - shares);
    }

    private void PushHistory(float value)
    {
        for (int i = 1; i < priceHistory.Length; i++)
        {
            priceHistory[i - 1] = priceHistory[i];
        }

        priceHistory[priceHistory.Length - 1] = value;
        historyCount = priceHistory.Length;
    }
}
