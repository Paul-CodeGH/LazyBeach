using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public sealed class BuyStuffTrigger : MonoBehaviour
{
    [SerializeField] private ShopItem[] items;

    private Canvas menuCanvas;
    private Text feedbackText;
    private bool playerInside;

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        if (items == null || items.Length == 0)
        {
            items = CreateDefaultItems();
        }

        CreateMenu();
        SetMenuVisible(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInside = true;
        SetMenuVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInside = false;
        SetMenuVisible(false);
    }

    private void BuyItem(ShopItem item)
    {
        CashManager.EnsureExists();
        PlayerNeedsManager.EnsureExists();

        if (CashManager.Instance == null || PlayerNeedsManager.Instance == null)
        {
            SetFeedback("Unavailable");
            return;
        }

        if (!CashManager.Instance.TrySpend(item.price))
        {
            SetFeedback("Not enough cash");
            return;
        }

        PlayerNeedsManager.Instance.AddValues(item.hungerValue, item.thirstValue, item.drunkValue);
        SetFeedback($"{item.itemName} bought");
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuCanvas != null)
        {
            menuCanvas.gameObject.SetActive(visible);
        }

        if (visible)
        {
            EnsureEventSystem();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetFeedback(string.Empty);
            return;
        }

        if (playerInside)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
        }
    }

    private void CreateMenu()
    {
        menuCanvas = new GameObject("BuyStuff Menu", typeof(RectTransform)).AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 1000;
        menuCanvas.transform.SetParent(transform, false);

        CanvasScaler scaler = menuCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        menuCanvas.gameObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(menuCanvas.transform, false);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.12f, 0.14f, 0.9f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(760f, 760f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(26, 26, 22, 22);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddText(panel.transform, "BuyStuff", 34, FontStyle.Bold, TextAnchor.MiddleCenter, 44f, new Color(1f, 0.9f, 0.55f, 1f));

        RectTransform content = CreateScrollableItemList(panel.transform);

        for (int i = 0; i < items.Length; i++)
        {
            ShopItem item = items[i];
            AddButton(content, item);
        }

        feedbackText = AddText(panel.transform, string.Empty, 22, FontStyle.Bold, TextAnchor.MiddleCenter, 32f, new Color(1f, 0.76f, 0.48f, 1f));
    }

    private Text AddText(Transform parent, string value, int size, FontStyle style, TextAnchor alignment, float height, Color color)
    {
        GameObject textObject = new GameObject(value.Length > 0 ? value : "Feedback", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = UiFontUtility.DefaultFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;

        return text;
    }

    private RectTransform CreateScrollableItemList(Transform parent)
    {
        GameObject scrollObject = new GameObject("Item Scroll View", typeof(RectTransform));
        scrollObject.transform.SetParent(parent, false);

        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = new Color(0.03f, 0.055f, 0.065f, 0.48f);

        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 34f;

        LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
        scrollLayout.preferredHeight = 600f;
        scrollLayout.flexibleHeight = 1f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollObject.transform, false);

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewportImage.raycastTarget = true;

        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup contentLayout = contentObject.AddComponent<GridLayoutGroup>();
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.cellSize = new Vector2(302f, 112f);
        contentLayout.spacing = new Vector2(12f, 12f);
        contentLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        contentLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        contentLayout.constraintCount = 2;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        return contentRect;
    }

    private void AddButton(Transform parent, ShopItem item)
    {
        GameObject buttonObject = new GameObject(item.itemName, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.68f, 0.38f, 0.13f, 0.96f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.68f, 0.38f, 0.13f, 0.96f);
        colors.highlightedColor = new Color(0.92f, 0.58f, 0.2f, 1f);
        colors.pressedColor = new Color(0.36f, 0.16f, 0.055f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => BuyItem(item));

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 112f;

        GameObject labelObject = new GameObject("Text", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        Text label = labelObject.AddComponent<Text>();
        label.font = UiFontUtility.DefaultFont;
        label.fontSize = 21;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(1f, 0.96f, 0.76f, 1f);
        label.lineSpacing = 1.05f;
        label.text = $"{item.itemName}  ${item.price}\n{CreateItemEffectLabel(item)}";
        label.raycastTarget = false;

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 6f);
        labelRect.offsetMax = new Vector2(-18f, -6f);
    }

    private static string CreateItemEffectLabel(ShopItem item)
    {
        string values = string.Empty;

        AppendEffect(ref values, "Hungry", item.hungerValue);
        AppendEffect(ref values, "Thursty", item.thirstValue);
        AppendEffect(ref values, "Drunk", item.drunkValue);

        return values.Length > 0 ? values : "No effect";
    }

    private static void AppendEffect(ref string values, string label, float amount)
    {
        if (Mathf.Approximately(amount, 0f))
        {
            return;
        }

        if (values.Length > 0)
        {
            values += "   ";
        }

        values += $"+{Mathf.RoundToInt(amount)} {label}";
    }

    private static bool IsPlayer(Collider other)
    {
        return other.GetComponentInParent<PlayerMovement>() != null;
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

    private static ShopItem[] CreateDefaultItems()
    {
        return new[]
        {
            new ShopItem("Beer", 12, 0f, 8f, 18f),
            new ShopItem("Vodka", 24, 0f, 0f, 34f),
            new ShopItem("Liquor", 30, 0f, 0f, 42f),
            new ShopItem("Ramen", 16, 30f, 8f, 0f),
            new ShopItem("Soup", 14, 22f, 16f, 0f),
            new ShopItem("Water", 6, 0f, 32f, 0f),
            new ShopItem("Juice", 9, 6f, 24f, 0f)
        };
    }

    [Serializable]
    private struct ShopItem
    {
        public string itemName;
        public int price;
        public float hungerValue;
        public float thirstValue;
        public float drunkValue;

        public ShopItem(string itemName, int price, float hungerValue, float thirstValue, float drunkValue)
        {
            this.itemName = itemName;
            this.price = price;
            this.hungerValue = hungerValue;
            this.thirstValue = thirstValue;
            this.drunkValue = drunkValue;
        }
    }
}
