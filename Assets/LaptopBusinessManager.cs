using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LaptopBusinessManager : MonoBehaviour
{
    private const float IncomeInterval = 10f;
    private const float AngelOpportunityRefreshInterval = 300f;
    private const int VisibleAngelOpportunityCount = 18;

    public static LaptopBusinessManager Instance { get; private set; }

    private RealEstateInvestment[] realEstateInvestments;
    private StockInvestment[] stocks;
    private AngelInvestment[] angelInvestmentCatalog;
    private AngelInvestment[] angelInvestments;
    private readonly List<AngelInvestmentRecord> angelInvestmentRecords = new List<AngelInvestmentRecord>();
    private float incomeTimer = IncomeInterval;
    private float stockTimer;
    private float angelTimer;
    private float angelRefreshTimer = AngelOpportunityRefreshInterval;
    private int nextAngelOpportunityIndex;
    private int angelOpportunityVersion;
    private int nextAngelInvestmentRecordId = 1;

    public event Action StateChanged;

    public RealEstateInvestment[] RealEstateInvestments => realEstateInvestments;
    public StockInvestment[] Stocks => stocks;
    public AngelInvestment[] AngelInvestments => angelInvestments;
    public IReadOnlyList<AngelInvestmentRecord> AngelInvestmentRecords => angelInvestmentRecords;
    public float IncomeTimeRemaining => incomeTimer;
    public float AngelOpportunityRefreshTimeRemaining => angelRefreshTimer;
    public int AngelOpportunityVersion => angelOpportunityVersion;
    public int AngelInvestmentTotalInvested
    {
        get
        {
            int total = 0;

            for (int i = 0; i < angelInvestmentRecords.Count; i++)
            {
                total += angelInvestmentRecords[i].InvestedAmount;
            }

            return total;
        }
    }

    public int AngelInvestmentTotalReturned
    {
        get
        {
            int total = 0;

            for (int i = 0; i < angelInvestmentRecords.Count; i++)
            {
                total += angelInvestmentRecords[i].ReturnedAmount;
            }

            return total;
        }
    }

    public int AngelInvestmentNetProfit => AngelInvestmentTotalReturned - AngelInvestmentTotalInvested;
    public int ActiveAngelInvestmentCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < angelInvestmentRecords.Count; i++)
            {
                if (angelInvestmentRecords[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }
    }
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

    public bool BuyAngelInvestment(int index)
    {
        if (!IsValidAngelInvestmentIndex(index))
        {
            return false;
        }

        AngelInvestment investment = angelInvestments[index];

        if (!investment.CanInvest)
        {
            return false;
        }

        CashManager.EnsureExists();

        if (CashManager.Instance == null || !CashManager.Instance.TrySpend(investment.AskPrice))
        {
            return false;
        }

        AngelInvestmentRecord record = new AngelInvestmentRecord(
            nextAngelInvestmentRecordId++,
            investment.DisplayName,
            investment.Category,
            investment.AskPrice,
            investment.EquityPercent);

        angelInvestmentRecords.Add(record);
        investment.Fund(record.Id);
        StateChanged?.Invoke();
        return true;
    }

    public bool ExitAngelInvestment(int index, out int recoveredCash)
    {
        recoveredCash = 0;

        if (!IsValidAngelInvestmentIndex(index))
        {
            return false;
        }

        AngelInvestment investment = angelInvestments[index];

        if (!investment.IsFunded)
        {
            return false;
        }

        recoveredCash = investment.ExitValue;
        AngelInvestmentRecord record = GetAngelInvestmentRecord(investment.RecordId);

        if (recoveredCash > 0)
        {
            CashManager.EnsureExists();
            CashManager.Instance?.AddCash(recoveredCash);
        }

        if (record != null && !investment.IsResolved)
        {
            record.MarkSold(recoveredCash);
        }

        angelInvestments[index] = GetNextAngelOpportunity(index);
        angelOpportunityVersion++;
        StateChanged?.Invoke();
        return true;
    }

    public void ResetForNewGame()
    {
        InitializeBusinessState();
        StateChanged?.Invoke();
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
        InitializeBusinessState();
    }

    private void InitializeBusinessState()
    {
        realEstateInvestments = CreateRealEstateInvestments();
        stocks = CreateStocks();
        angelInvestmentCatalog = CreateAngelInvestmentCatalog();
        angelInvestmentRecords.Clear();
        incomeTimer = IncomeInterval;
        stockTimer = 0f;
        angelTimer = 0f;
        angelRefreshTimer = AngelOpportunityRefreshInterval;
        nextAngelOpportunityIndex = 0;
        angelOpportunityVersion++;
        nextAngelInvestmentRecordId = 1;
        angelInvestments = CreateInitialAngelInvestments();
    }

    private void Update()
    {
        UpdateIncome();
        UpdateStocks();
        UpdateAngelInvestments();
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

    private void UpdateAngelInvestments()
    {
        angelRefreshTimer -= Time.deltaTime;
        angelTimer -= Time.deltaTime;
        bool changed = false;

        if (angelRefreshTimer <= 0f)
        {
            angelRefreshTimer = AngelOpportunityRefreshInterval;
            changed = RefreshOpenAngelOpportunities();
        }

        if (angelTimer > 0f)
        {
            if (changed)
            {
                StateChanged?.Invoke();
            }

            return;
        }

        angelTimer = 1f;

        for (int i = 0; i < angelInvestments.Length; i++)
        {
            if (!angelInvestments[i].Tick(1f, out int payout))
            {
                continue;
            }

            changed = true;

            AngelInvestmentRecord record = GetAngelInvestmentRecord(angelInvestments[i].RecordId);

            if (record != null)
            {
                record.MarkResolved(payout);
            }

            if (payout > 0)
            {
                CashManager.EnsureExists();
                CashManager.Instance?.AddCash(payout);
            }
        }

        if (changed)
        {
            StateChanged?.Invoke();
        }
    }

    private bool RefreshOpenAngelOpportunities()
    {
        bool refreshedAny = false;

        for (int i = 0; i < angelInvestments.Length; i++)
        {
            if (angelInvestments[i].IsFunded)
            {
                continue;
            }

            angelInvestments[i] = GetNextAngelOpportunity(i);
            refreshedAny = true;
        }

        if (refreshedAny)
        {
            angelOpportunityVersion++;
        }

        return refreshedAny;
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

    private bool IsValidAngelInvestmentIndex(int index)
    {
        return angelInvestments != null && index >= 0 && index < angelInvestments.Length;
    }

    private AngelInvestmentRecord GetAngelInvestmentRecord(int id)
    {
        if (id <= 0)
        {
            return null;
        }

        for (int i = 0; i < angelInvestmentRecords.Count; i++)
        {
            if (angelInvestmentRecords[i].Id == id)
            {
                return angelInvestmentRecords[i];
            }
        }

        return null;
    }

    private AngelInvestment[] CreateInitialAngelInvestments()
    {
        int visibleCount = Mathf.Min(VisibleAngelOpportunityCount, angelInvestmentCatalog.Length);
        AngelInvestment[] investments = new AngelInvestment[visibleCount];

        for (int i = 0; i < investments.Length; i++)
        {
            investments[i] = GetNextAngelOpportunity(i);
        }

        return investments;
    }

    private AngelInvestment GetNextAngelOpportunity(int slotIndex)
    {
        if (angelInvestmentCatalog == null || angelInvestmentCatalog.Length == 0)
        {
            return null;
        }

        string currentDisplayName = IsValidAngelInvestmentIndex(slotIndex) && angelInvestments[slotIndex] != null
            ? angelInvestments[slotIndex].DisplayName
            : null;

        for (int attempts = 0; attempts < angelInvestmentCatalog.Length; attempts++)
        {
            AngelInvestment candidate = angelInvestmentCatalog[nextAngelOpportunityIndex];
            nextAngelOpportunityIndex = (nextAngelOpportunityIndex + 1) % angelInvestmentCatalog.Length;

            if (candidate.DisplayName != currentDisplayName && !IsAngelOpportunityVisible(candidate.DisplayName, slotIndex))
            {
                return candidate.CreateCopy();
            }
        }

        AngelInvestment fallback = angelInvestmentCatalog[nextAngelOpportunityIndex];
        nextAngelOpportunityIndex = (nextAngelOpportunityIndex + 1) % angelInvestmentCatalog.Length;
        return fallback.CreateCopy();
    }

    private bool IsAngelOpportunityVisible(string displayName, int ignoredSlotIndex)
    {
        if (angelInvestments == null)
        {
            return false;
        }

        for (int i = 0; i < angelInvestments.Length; i++)
        {
            if (i == ignoredSlotIndex || angelInvestments[i] == null)
            {
                continue;
            }

            if (angelInvestments[i].DisplayName == displayName)
            {
                return true;
            }
        }

        return false;
    }

    private static RealEstateInvestment[] CreateRealEstateInvestments()
    {
        return new[]
        {
            new RealEstateInvestment("Beach Shack", "A tiny rental near the boardwalk.", 120, 2, 90, 4),
            new RealEstateInvestment("Studio Condo", "Low-cost apartment with steady rent.", 450, 6, 280, 5),
            new RealEstateInvestment("Family House", "A larger home with reliable tenants.", 1400, 22, 800, 5),
            new RealEstateInvestment("Beach Villa", "Premium vacation rental income.", 4200, 80, 2300, 4),
            new RealEstateInvestment("Lazy Resort", "High-end resort with major payouts.", 12000, 260, 6500, 3),
            new RealEstateInvestment("Boardwalk Hotel", "Busy hotel rooms above the main nightlife strip.", 36000, 900, 19000, 3),
            new RealEstateInvestment("Marina Complex", "Boat slips, storage, and waterfront retail leases.", 110000, 3200, 60000, 3),
            new RealEstateInvestment("Private Island Lots", "Exclusive island plots with ultra-premium vacation rentals.", 320000, 9800, 175000, 3),
            new RealEstateInvestment("Luxury Beach District", "A full street of luxury shops, suites, and restaurants.", 900000, 30000, 520000, 3),
            new RealEstateInvestment("LazyBeach Mega Resort", "Flagship resort campus with the highest rental income.", 2500000, 88000, 1400000, 3)
        };
    }

    private static StockInvestment[] CreateStocks()
    {
        return new[]
        {
            new StockInvestment("SunPeak Energy", "SPE", 18f, 4.2f, 0.2f),
            new StockInvestment("BlueWave Foods", "BWF", 42f, 3.1f, 1.7f),
            new StockInvestment("StormRide Robotics", "SRR", 650f, 18.5f, 3.4f, 100f, 1500f),
            new StockInvestment("Apex Quantum Systems", "AQS", 20000f, 22f, 5.8f, 12000f, 28000f)
        };
    }

    private static AngelInvestment[] CreateAngelInvestmentCatalog()
    {
        return new[]
        {
            new AngelInvestment(
                "TideCart Delivery",
                "Local logistics",
                "Beach-cart delivery app for food, water, towels, and small shop orders.",
                "Launched with two hotel pilots and grew from 30 to 220 weekly deliveries. The team already owns the routing software but still depends on seasonal staff.",
                "Ask: $2,500 buys 4.0% of the company. The most likely win is a small acquisition by a resort operator.",
                2500,
                4f,
                2.1f,
                65f),
            new AngelInvestment(
                "SaltSkin Sunscreen",
                "Consumer product",
                "Refillable reef-safe sunscreen pods for beach kiosks and hotels.",
                "Early kiosk tests sold well, but manufacturing costs doubled after the first supplier failed quality checks. Returns were high during the second test run.",
                "Ask: $4,200 buys 6.5% of the company. The product can work, but a bad supplier deal could wipe it out.",
                4200,
                6.5f,
                0f,
                80f),
            new AngelInvestment(
                "PalmPay Kiosks",
                "Payments",
                "Self-service payment kiosks for rentals, lockers, and beach activities.",
                "The founders previously sold a small POS plugin. PalmPay has signed letters of intent with three rental shops, but hardware certification is still pending.",
                "Ask: $6,800 buys 3.0% of the company. A modest exit is possible if certification lands.",
                6800,
                3f,
                1.35f,
                90f),
            new AngelInvestment(
                "Dockside Drones",
                "Tourism hardware",
                "Automated drone photo rentals for boardwalks, piers, and private events.",
                "Customer demos looked impressive, but insurance quotes came in far above the founders' plan. Two municipalities rejected their first permit applications.",
                "Ask: $9,500 buys 7.5% of the company. The company may fail if permits keep slipping.",
                9500,
                7.5f,
                0f,
                95f),
            new AngelInvestment(
                "CoralBox Storage",
                "Subscription service",
                "Smart lockers for beach bags, surfboards, and day-trip storage.",
                "Three locker walls have been running for six months with low maintenance costs. Revenue is steady, but growth depends on winning city contracts.",
                "Ask: $12,000 buys 8.0% of the company. The business looks boring, but the cash flow is real.",
                12000,
                8f,
                1.5f,
                105f),
            new AngelInvestment(
                "LazyLift Scooters",
                "Mobility",
                "Electric boardwalk scooters with tourist-friendly hourly rentals.",
                "The team copied proven scooter operations but focused on smaller beach towns. Their first summer had strong repeat rentals and low theft.",
                "Ask: $15,000 buys 5.0% of the company. A strong rollout could produce a meaningful exit.",
                15000,
                5f,
                3f,
                115f),
            new AngelInvestment(
                "Sunset Studios",
                "Creator tools",
                "Rental studio booths and editing tools for travel creators.",
                "The first location opened with a big launch but usage fell after the opening month. Several creators asked for cheaper hourly pricing.",
                "Ask: $18,000 buys 6.0% of the company. It needs strong demand quickly or it goes to zero.",
                18000,
                6f,
                0f,
                100f),
            new AngelInvestment(
                "Ramen Reef",
                "Food automation",
                "Hot ramen, soup, and water vending machines for late-night resort areas.",
                "A campus pilot was profitable, but beach humidity damaged two machines and support tickets were expensive. The founders are rebuilding the hardware.",
                "Ask: $22,000 buys 9.0% of the company. The idea is useful, but reliability risk is severe.",
                22000,
                9f,
                0f,
                120f),
            new AngelInvestment(
                "DriftMarket",
                "Marketplace",
                "Used surf gear marketplace with trade-in pickup at local shops.",
                "Transaction volume grew slowly, and shop partners like the trade-in traffic. Margins are thin because pickup costs are higher than planned.",
                "Ask: $27,000 buys 4.4% of the company. It may return some money without becoming a big winner.",
                27000,
                4.4f,
                0.8f,
                110f),
            new AngelInvestment(
                "WaveLedger AI",
                "SaaS",
                "AI fraud detection for hotel bookings, events, and short-term rentals.",
                "The founders built fraud tools at a payments company. A paid pilot caught several chargeback attempts, and two hotels asked for annual pricing.",
                "Ask: $38,000 buys 2.5% of the company. If pilots convert, this can become the biggest software winner.",
                38000,
                2.5f,
                4.2f,
                130f),
            new AngelInvestment(
                "AquaGrid Desalination",
                "Climate hardware",
                "Compact desalination units for beach businesses and small marinas.",
                "Prototype output improved each quarter, but the hardware is expensive and maintenance is unproven. A marina group is watching the next trial.",
                "Ask: $55,000 buys 1.8% of the company. It is risky, but a successful trial could create a valuable company.",
                55000,
                1.8f,
                2.4f,
                140f),
            new AngelInvestment(
                "MoonPier Hospitality",
                "Hospitality",
                "Prefab micro-cabins for premium overnight stays near beaches and piers.",
                "The model has strong booking demand and high nightly rates. Permitting is slow, but the team has already secured two private land options.",
                "Ask: $76,000 buys 3.2% of the company. Expensive entry, but the upside is high if locations open.",
                76000,
                3.2f,
                3.7f,
                150f),
            new AngelInvestment(
                "Horizon Resort OS",
                "Enterprise SaaS",
                "Operating system for large resort groups to manage rooms, events, staff, and guest upsells.",
                "Three regional hotel groups are testing the product. The pilots are large, but enterprise sales cycles are slow and support costs are rising.",
                "Ask: $95,000 buys 1.6% of the company. If one chain signs an annual contract, this can become a serious software exit.",
                95000,
                1.6f,
                2.8f,
                168f),
            new AngelInvestment(
                "BlueCurrent Ferries",
                "Clean transport",
                "Electric ferry fleet for island tours and hotel-to-marina transfers.",
                "The prototype boat is quiet and popular with tourists, but battery degradation is worse than expected and maintenance invoices are climbing.",
                "Ask: $125,000 buys 2.4% of the company. The market is attractive, but battery failure can still wipe the company out.",
                125000,
                2.4f,
                0f,
                180f),
            new AngelInvestment(
                "TideVault Capital",
                "Fintech",
                "Revenue financing platform for beach bars, rental shops, and seasonal tourism businesses.",
                "The team has underwriting data from 80 merchants and early repayments look strong. A larger lender wants proof across one more season.",
                "Ask: $180,000 buys 1.2% of the company. If default rates stay low, this can return a large multiple.",
                180000,
                1.2f,
                3.6f,
                190f),
            new AngelInvestment(
                "Sunspire Marina REIT",
                "Real estate finance",
                "Fractional marina ownership fund buying damaged slips and upgrading them for premium leases.",
                "The first marina renovation filled quickly, but property taxes and insurance are higher than planned. Growth should be steady, not explosive.",
                "Ask: $240,000 buys 1.9% of the fund. It is expensive but has a believable moderate-return path.",
                240000,
                1.9f,
                1.4f,
                210f),
            new AngelInvestment(
                "Neptune Neural Travel",
                "AI travel",
                "Personal AI concierge that books flights, beach villas, restaurants, and activities automatically.",
                "Usage spikes during demos, but paid retention is weak. Large travel agencies are watching, though nobody has signed a paid partnership yet.",
                "Ask: $320,000 buys 0.9% of the company. The upside is huge, but weak retention can send the stake to zero.",
                320000,
                0.9f,
                0f,
                220f),
            new AngelInvestment(
                "Atlas Island Holdings",
                "Luxury development",
                "Acquires small islands and builds ultra-luxury eco resorts for private groups.",
                "The company has signed an island purchase option and secured soft commitments from high-end travel brokers. Environmental approval is the main risk.",
                "Ask: $450,000 buys 1.1% of the company. If approvals clear, this can become the largest angel winner.",
                450000,
                1.1f,
                5.2f,
                150f),
            new AngelInvestment(
                "BreezeBrew Cans",
                "Beverage",
                "Low-calorie canned cocktails built for beach stores and music events.",
                "The founders won shelf space in four local stores, but repeat orders are inconsistent. Their best flavor sells well while the other two barely move.",
                "Ask: $8,500 buys 5.5% of the company. It could become a small brand, but inventory risk is high.",
                8500,
                5.5f,
                1.15f,
                92f),
            new AngelInvestment(
                "TowelTag",
                "Rental software",
                "RFID towel and umbrella tracking for hotels that lose too much inventory.",
                "A resort pilot cut missing towel costs by 38%. The product is simple, but the sales cycle is slow because hotels buy once per season.",
                "Ask: $11,500 buys 4.2% of the company. A practical niche product with modest upside.",
                11500,
                4.2f,
                1.7f,
                108f),
            new AngelInvestment(
                "FoamForge Boards",
                "Manufacturing",
                "Custom beginner surfboards made with cheaper recyclable foam cores.",
                "The first batch had great margins but cracked under heavy rental use. The founders believe a stronger coating fixes it, but testing is incomplete.",
                "Ask: $16,000 buys 8.5% of the company. Quality problems can still send this to zero.",
                16000,
                8.5f,
                0f,
                118f),
            new AngelInvestment(
                "BeachGuard Vision",
                "Safety AI",
                "Computer vision alerts for crowded beaches, missing swimmers, and closed zones.",
                "The model performs well on clear footage, but glare and rain create false positives. One private beach club is paying for a three-month pilot.",
                "Ask: $24,000 buys 3.8% of the company. If accuracy improves, safety buyers could move fast.",
                24000,
                3.8f,
                2.6f,
                128f),
            new AngelInvestment(
                "Oasis Laundry Loop",
                "Operations",
                "Same-day laundry pickup for small hotels, hostels, and beach rentals.",
                "Revenue has grown every month, but delivery labor is eating the margin. The founders need route density before the numbers work.",
                "Ask: $31,000 buys 7.0% of the company. It may survive, but profit could stay thin.",
                31000,
                7f,
                0.65f,
                112f),
            new AngelInvestment(
                "SunClaim Insurance",
                "Fintech",
                "Instant weather refund insurance for tours, boat rentals, and outdoor events.",
                "The actuarial model is promising, and tour operators like the refund pitch. Regulatory review has been slower than expected.",
                "Ask: $44,000 buys 2.2% of the company. High ceiling, but approval delays can hurt badly.",
                44000,
                2.2f,
                3.4f,
                145f),
            new AngelInvestment(
                "PierPets Boarding",
                "Pet services",
                "Short-stay pet care beside hotels where beaches do not allow dogs.",
                "Guests love the convenience, but staffing every weekend is expensive. The first location has strong reviews and weak margins.",
                "Ask: $19,500 buys 6.8% of the company. It needs operational discipline to become profitable.",
                19500,
                6.8f,
                0.9f,
                106f),
            new AngelInvestment(
                "LagoonVR Tours",
                "Entertainment",
                "VR previews that help tourists choose excursions before booking.",
                "Early demos looked good, but conversion rates were lower than promised. Tour operators asked for cheaper monthly plans.",
                "Ask: $13,500 buys 9.5% of the company. If sales do not improve, the stake can become worthless.",
                13500,
                9.5f,
                0f,
                96f),
            new AngelInvestment(
                "MarinaMind",
                "SaaS",
                "Scheduling and predictive maintenance software for small marinas.",
                "The founders know marina operations well and already manage 600 slips across pilot customers. The market is small but loyal.",
                "Ask: $33,000 buys 4.0% of the company. Not flashy, but the exit path is believable.",
                33000,
                4f,
                2.25f,
                126f),
            new AngelInvestment(
                "ClearCup Deposit",
                "Sustainability",
                "Reusable cup deposits for beach bars and event venues.",
                "Deposit return rates are high, but washing logistics are harder than expected. One festival canceled after a late supply shipment.",
                "Ask: $26,000 buys 5.8% of the company. The model can work if operations stop slipping.",
                26000,
                5.8f,
                1.25f,
                114f),
            new AngelInvestment(
                "SandSignal Mesh",
                "Connectivity",
                "Temporary mesh Wi-Fi towers for crowded beaches and festivals.",
                "The technology worked during a crowded holiday weekend, but hardware was damaged by salt and wind. Replacement costs are not yet under control.",
                "Ask: $48,000 buys 3.5% of the company. Strong demand, serious equipment risk.",
                48000,
                3.5f,
                0f,
                136f),
            new AngelInvestment(
                "HarborHarvest",
                "Food supply",
                "Direct seafood ordering platform between small boats and local restaurants.",
                "Restaurants like the freshness and fishermen like faster payments. Compliance paperwork has been the main bottleneck.",
                "Ask: $62,000 buys 2.9% of the company. If compliance clears, this can scale across coastal towns.",
                62000,
                2.9f,
                3.1f,
                152f)
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
    [SerializeField] private bool hasPriceBounds;
    [SerializeField] private float minimumPrice;
    [SerializeField] private float maximumPrice;

    private readonly float[] priceHistory = new float[HistoryLength];
    private int historyCount;

    public string CompanyName => companyName;
    public string Symbol => symbol;
    public float Price => price;
    public int SharesOwned => sharesOwned;
    public float[] PriceHistory => priceHistory;
    public int HistoryCount => historyCount;

    public StockInvestment(string companyName, string symbol, float startingPrice, float volatility, float phase)
        : this(companyName, symbol, startingPrice, volatility, phase, 1f, 0f, false)
    {
    }

    public StockInvestment(string companyName, string symbol, float startingPrice, float volatility, float phase, float minimumPrice, float maximumPrice)
        : this(companyName, symbol, startingPrice, volatility, phase, minimumPrice, maximumPrice, true)
    {
    }

    private StockInvestment(string companyName, string symbol, float startingPrice, float volatility, float phase, float minimumPrice, float maximumPrice, bool hasPriceBounds)
    {
        this.companyName = companyName;
        this.symbol = symbol;
        this.volatility = volatility;
        this.phase = phase;
        this.hasPriceBounds = hasPriceBounds;
        this.minimumPrice = Mathf.Max(1f, minimumPrice);
        this.maximumPrice = Mathf.Max(this.minimumPrice, maximumPrice);
        this.price = ClampPrice(startingPrice);

        for (int i = 0; i < priceHistory.Length; i++)
        {
            float position = i / Mathf.Max(1f, priceHistory.Length - 1f);
            float wave = Mathf.Sin(position * Mathf.PI * 4f + phase) * 0.018f;
            float pulse = Mathf.Sin(position * Mathf.PI * 9f + phase * 1.7f) * 0.007f;
            float trend = Mathf.Lerp(-0.01f, 0f, position);
            priceHistory[i] = ClampPrice(startingPrice * (1f + wave + pulse + trend));
        }

        priceHistory[priceHistory.Length - 1] = price;
        historyCount = priceHistory.Length;
    }

    public void UpdatePrice(float time)
    {
        float wave = Mathf.Sin(time * 0.31f + phase) * volatility * 0.0035f;
        float pulse = Mathf.Sin(time * 0.93f + phase * 2.1f) * volatility * 0.0018f;
        float random = UnityEngine.Random.Range(-volatility, volatility) * 0.0012f;
        price = ClampPrice(price * (1f + wave + pulse + random));
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

    private float ClampPrice(float value)
    {
        if (!hasPriceBounds)
        {
            return Mathf.Max(1f, value);
        }

        return Mathf.Clamp(value, minimumPrice, maximumPrice);
    }
}

[Serializable]
public sealed class AngelInvestment
{
    [SerializeField] private string displayName;
    [SerializeField] private string category;
    [SerializeField] private string description;
    [SerializeField] private string historicalDetails;
    [SerializeField] private string investmentTerms;
    [SerializeField] private int askPrice;
    [SerializeField] private float equityPercent;
    [SerializeField] private float outcomeMultiplier;
    [SerializeField] private float exitWindowSeconds;
    [SerializeField] private bool isFunded;
    [SerializeField] private bool isResolved;
    [SerializeField] private float secondsRemaining;
    [SerializeField] private int recordId;

    public string DisplayName => displayName;
    public string Category => category;
    public string Description => description;
    public string HistoricalDetails => historicalDetails;
    public string InvestmentTerms => investmentTerms;
    public int AskPrice => askPrice;
    public float EquityPercent => equityPercent;
    public bool IsFunded => isFunded;
    public bool IsResolved => isResolved;
    public bool CanInvest => !isFunded;
    public int RecordId => recordId;
    public int FinalPayout => Mathf.RoundToInt(askPrice * outcomeMultiplier);
    public int Profit => FinalPayout - askPrice;
    public int SecondsRemaining => Mathf.CeilToInt(secondsRemaining);
    public int ExitValue => isFunded && !isResolved ? Mathf.RoundToInt(askPrice * 0.5f) : 0;

    public AngelInvestment(
        string displayName,
        string category,
        string description,
        string historicalDetails,
        string investmentTerms,
        int askPrice,
        float equityPercent,
        float outcomeMultiplier,
        float exitWindowSeconds)
    {
        this.displayName = displayName;
        this.category = category;
        this.description = description;
        this.historicalDetails = historicalDetails;
        this.investmentTerms = investmentTerms;
        this.askPrice = askPrice;
        this.equityPercent = equityPercent;
        this.outcomeMultiplier = outcomeMultiplier;
        this.exitWindowSeconds = exitWindowSeconds;
    }

    public void Fund(int recordId)
    {
        isFunded = true;
        isResolved = false;
        this.recordId = recordId;
        secondsRemaining = exitWindowSeconds;
    }

    public AngelInvestment CreateCopy()
    {
        return new AngelInvestment(
            displayName,
            category,
            description,
            historicalDetails,
            investmentTerms,
            askPrice,
            equityPercent,
            outcomeMultiplier,
            exitWindowSeconds);
    }

    public bool Tick(float deltaSeconds, out int payout)
    {
        payout = 0;

        if (!isFunded || isResolved)
        {
            return false;
        }

        secondsRemaining = Mathf.Max(0f, secondsRemaining - deltaSeconds);

        if (secondsRemaining > 0f)
        {
            return true;
        }

        isResolved = true;
        payout = FinalPayout;
        return true;
    }
}

[Serializable]
public sealed class AngelInvestmentRecord
{
    [SerializeField] private int id;
    [SerializeField] private string displayName;
    [SerializeField] private string category;
    [SerializeField] private int investedAmount;
    [SerializeField] private float equityPercent;
    [SerializeField] private int returnedAmount;
    [SerializeField] private string status;

    public int Id => id;
    public string DisplayName => displayName;
    public string Category => category;
    public int InvestedAmount => investedAmount;
    public float EquityPercent => equityPercent;
    public int ReturnedAmount => returnedAmount;
    public int Profit => returnedAmount - investedAmount;
    public string Status => status;
    public bool IsActive => status == "Active";

    public AngelInvestmentRecord(int id, string displayName, string category, int investedAmount, float equityPercent)
    {
        this.id = id;
        this.displayName = displayName;
        this.category = category;
        this.investedAmount = investedAmount;
        this.equityPercent = equityPercent;
        status = "Active";
    }

    public void MarkResolved(int returnedAmount)
    {
        this.returnedAmount = returnedAmount;
        status = returnedAmount > 0 ? "Resolved" : "Failed";
    }

    public void MarkSold(int returnedAmount)
    {
        this.returnedAmount = returnedAmount;
        status = "Sold";
    }
}
