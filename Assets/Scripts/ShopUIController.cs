using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Renders the shop as a fullscreen grid of "buy cards", one per PlantData asset registered in
// PlantDatabase.allPlantData -- exactly like BookUIController auto-syncs its pages. Adding a new
// seed just means adding a new PlantData asset to the database; no new button/script wiring is
// needed, so the grid always has room for future seeds.
// Everything (grid, cards, title) is built at runtime from primitive UI components. On Start()
// it also removes whatever old per-seed buttons used to be manually placed under shopPanel, so
// only the new grid + the existing status texts/buttons remain.
public class ShopUIController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject shopPanel;
    public TextMeshProUGUI coinsText;
    public Button closeButton;

    [Header("Sell")]
    public Button sellAllButton;
    public TextMeshProUGUI sellResultText;

    [Header("Auto-Close")]
    [Tooltip("If the player walks farther than this from the shop panel while it's open, the shop closes itself automatically -- the panel is anchored to the shop counter (see WorldSpaceUIPlacement's anchor mode) so it no longer follows the player, and would otherwise stay open and readable from anywhere in the world.")]
    public float maxOpenDistance = 4f;

    [Header("Buy Grid")]
    [Tooltip("Parent that holds the seed buy cards. Leave empty to auto-create one under shopPanel.")]
    public RectTransform cardContainer;
    public int columns = 5;
    public Vector2 cardSpacing = new Vector2(16f, 16f);
    [Tooltip("Height-to-width ratio each card is forced to. >1 makes a tall/portrait rectangle (the requested look); 1 would be square.")]
    public float cardAspectRatio = 1.35f;
    [Tooltip("Cards are auto-sized to fill cardContainer at cardAspectRatio; this is only a floor so cards never shrink below a usable size.")]
    public Vector2 minCardSize = new Vector2(85f, 115f);
    public Color cardAffordableColor = new Color(1f, 1f, 1f, 0.5f);
    public Color cardUnaffordableColor = new Color(1f, 1f, 1f, 0.18f);
    public Color beginnerTierColor = new Color(0.45f, 0.75f, 0.35f, 1f);
    public Color mediumTierColor = new Color(0.9f, 0.65f, 0.2f, 1f);
    public Color hardTierColor = new Color(0.8f, 0.3f, 0.3f, 1f);
    public Color buyButtonColor = new Color(0.25f, 0.6f, 0.25f, 0.95f);

    // Inventory (tool/seed/crop grid) slot unlocking lives here now, not on the inventory page --
    // this is the single place in the game where any capacity is purchased. Both are
    // auto-created (no Inspector wiring needed) since neither existed in the scene before.
    private TextMeshProUGUI inventorySlotsText;
    private Button unlockInventorySlotButton;

    [Header("Panel Style")]
    [Tooltip("Alpha the shop panel's own background is forced to. Its RGB color is left as whatever it already is.")]
    [Range(0f, 1f)]
    public float panelBackgroundAlpha = 0.6f;

    private struct CardUI
    {
        public Image background;
        public Image icon;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI priceText;
        public Button buyButton;
        public SeedType seedType;
    }

    private TextMeshProUGUI titleText;
    private List<CardUI> cards = new List<CardUI>();
    private Vector2 currentCardSize;

    void Update()
    {
        if (!shopPanel.activeSelf)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        if (Vector3.Distance(cam.transform.position, shopPanel.transform.position) > maxOpenDistance)
        {
            CloseShop();
        }
    }

    void Start()
    {
        shopPanel.SetActive(false);

        CleanupLegacyChildren();
        StylePanelFullscreen();
        EnsureTitleText();
        EnsureInventoryUnlockRow();
        EnsureCardContainer();
        LayoutFixedElements();

        closeButton.onClick.AddListener(CloseShop);
        sellAllButton.onClick.AddListener(OnSellAll);
        unlockInventorySlotButton.onClick.AddListener(OnUnlockInventorySlot);

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += HandleCoinsChanged;
        }
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnEquipmentChanged += HandleEquipmentChanged;
        }
    }

    public void OpenShop()
    {
        shopPanel.SetActive(true);
        if (sellResultText != null)
        {
            sellResultText.text = "";
        }

        // Rebuild the grid every time the shop is opened so a PlantData asset added to
        // PlantDatabase.allPlantData while testing shows up immediately, with no code change.
        BuildCardGrid();
        RefreshUI();

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.RegisterUIOpened();
        }
    }

    public void CloseShop()
    {
        shopPanel.SetActive(false);

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.RegisterUIClosed();
        }
    }

    void OnSellAll()
    {
        if (EquipmentManager.Instance == null)
        {
            return;
        }

        int earned = EquipmentManager.Instance.SellAllCrops();
        if (sellResultText != null)
        {
            sellResultText.text = earned > 0 ? ("+" + earned + " coins") : "Nothing to sell";
        }
    }

    void OnBuySeed(SeedType type)
    {
        if (PlantDatabase.Instance == null || CurrencyManager.Instance == null || EquipmentManager.Instance == null)
        {
            return;
        }

        PlantData data = PlantDatabase.Instance.GetPlantData(type);
        if (data == null)
        {
            return;
        }

        if (CurrencyManager.Instance.TrySpendCoins(data.seedBuyPrice))
        {
            EquipmentManager.Instance.AddSeeds(type, 1);
        }
    }

    void OnUnlockInventorySlot()
    {
        if (EquipmentManager.Instance == null)
        {
            return;
        }

        EquipmentManager.Instance.TryUnlockNextSlot();
    }

    void HandleCoinsChanged(int newAmount)
    {
        RefreshUI();
    }

    void HandleEquipmentChanged()
    {
        RefreshUI();
    }

    // --- One-time cleanup of whatever old per-seed buy UI used to live under shopPanel ---
    //
    // The old buttons/texts were not necessarily direct children of shopPanel -- some scenes
    // nest status texts and buttons together inside a shared layout container (e.g. a "BuyList"
    // group). Destroying only non-kept *direct* children would leave that whole container (and
    // every old button inside it) alive just because one kept text happened to live in it. So
    // instead: pull every element we still want out to be a direct child of shopPanel first,
    // then delete everything else that remains.
    void CleanupLegacyChildren()
    {
        Transform[] keepers = new Transform[]
        {
            coinsText != null ? coinsText.transform : null,
            closeButton != null ? closeButton.transform : null,
            sellAllButton != null ? sellAllButton.transform : null,
            sellResultText != null ? sellResultText.transform : null,
        };

        HashSet<Transform> keepSet = new HashSet<Transform>();
        foreach (Transform t in keepers)
        {
            if (t == null)
            {
                continue;
            }
            t.SetParent(shopPanel.transform, false);
            keepSet.Add(t);
        }

        List<Transform> toDestroy = new List<Transform>();
        foreach (Transform child in shopPanel.transform)
        {
            if (!keepSet.Contains(child))
            {
                toDestroy.Add(child);
            }
        }

        foreach (Transform t in toDestroy)
        {
            Destroy(t.gameObject);
        }
    }

    void StylePanelFullscreen()
    {
        RectTransform panelRect = shopPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }

        Image panelImage = shopPanel.GetComponent<Image>();
        if (panelImage == null)
        {
            panelImage = shopPanel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 1f);
        }

        Color c = panelImage.color;
        panelImage.color = new Color(c.r, c.g, c.b, panelBackgroundAlpha);
    }

    // --- Runtime layout of the pieces that already existed in the scene ---

    void EnsureTitleText()
    {
        GameObject titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(shopPanel.transform, false);
        RectTransform rect = titleGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.03f, 0.90f);
        rect.anchorMax = new Vector2(0.5f, 0.98f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "SHOP";
        titleText.fontSize = 34f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.color = Color.white;
    }

    // Slot unlocking for the tool/seed inventory grid is bought here and only here -- the
    // Inventory page itself has no unlock control anymore.
    void EnsureInventoryUnlockRow()
    {
        GameObject textGO = new GameObject("InventorySlotsText", typeof(RectTransform));
        textGO.transform.SetParent(shopPanel.transform, false);
        inventorySlotsText = textGO.AddComponent<TextMeshProUGUI>();
        inventorySlotsText.fontSize = 16f;
        inventorySlotsText.alignment = TextAlignmentOptions.MidlineLeft;
        inventorySlotsText.color = Color.white;

        GameObject buttonGO = new GameObject("UnlockInventorySlotButton", typeof(RectTransform));
        buttonGO.transform.SetParent(shopPanel.transform, false);
        Image buttonBg = buttonGO.AddComponent<Image>();
        buttonBg.color = buyButtonColor;
        unlockInventorySlotButton = buttonGO.AddComponent<Button>();
        unlockInventorySlotButton.targetGraphic = buttonBg;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(buttonGO.transform, false);
        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = "Unlock Slot";
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 9f;
        label.fontSizeMax = 18f;
        SetAnchors(labelGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
    }

    void LayoutFixedElements()
    {
        // Top-right status: coins.
        SetAnchors(coinsText != null ? coinsText.rectTransform : null, new Vector2(0.55f, 0.91f), new Vector2(0.90f, 0.98f));

        // Close button: top-right corner.
        SetAnchors(closeButton != null ? closeButton.GetComponent<RectTransform>() : null, new Vector2(0.92f, 0.91f), new Vector2(0.98f, 0.98f));

        // Bottom section: two stacked rows, each "status text on the left, its buy/sell
        // button on the right" -- Inventory slots (top row), Sell All (bottom row). The old
        // middle Backpack-upgrade row is gone now that harvested crops live in this same grid.
        SetAnchors(inventorySlotsText != null ? inventorySlotsText.rectTransform : null, new Vector2(0.03f, 0.095f), new Vector2(0.55f, 0.155f));
        SetAnchors(unlockInventorySlotButton != null ? unlockInventorySlotButton.GetComponent<RectTransform>() : null, new Vector2(0.60f, 0.095f), new Vector2(0.97f, 0.155f));

        SetAnchors(sellResultText != null ? sellResultText.rectTransform : null, new Vector2(0.03f, 0.015f), new Vector2(0.55f, 0.075f));
        SetAnchors(sellAllButton != null ? sellAllButton.GetComponent<RectTransform>() : null, new Vector2(0.60f, 0.015f), new Vector2(0.97f, 0.075f));
    }

    static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // --- Runtime grid construction ---

    void EnsureCardContainer()
    {
        if (cardContainer != null)
        {
            return;
        }

        GameObject containerGO = new GameObject("BuyCardGrid", typeof(RectTransform));
        containerGO.transform.SetParent(shopPanel.transform, false);
        cardContainer = containerGO.GetComponent<RectTransform>();
        cardContainer.anchorMin = new Vector2(0.03f, 0.19f);
        cardContainer.anchorMax = new Vector2(0.97f, 0.85f);
        cardContainer.offsetMin = Vector2.zero;
        cardContainer.offsetMax = Vector2.zero;
    }

    void BuildCardGrid()
    {
        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }
        cards.Clear();

        if (PlantDatabase.Instance == null || PlantDatabase.Instance.allPlantData == null)
        {
            return;
        }

        List<PlantData> validData = new List<PlantData>();
        foreach (PlantData data in PlantDatabase.Instance.allPlantData)
        {
            if (data != null)
            {
                validData.Add(data);
            }
        }

        int count = Mathf.Max(1, validData.Count);
        GridLayoutGroup grid = cardContainer.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = cardContainer.gameObject.AddComponent<GridLayoutGroup>();
        }
        grid.spacing = cardSpacing;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        int cols = Mathf.Clamp(columns, 1, Mathf.Max(1, validData.Count));
        grid.constraintCount = cols;

        Canvas.ForceUpdateCanvases();
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)validData.Count / cols));
        float availableWidth = cardContainer.rect.width;
        float availableHeight = cardContainer.rect.height;
        float cellWidth = (availableWidth - cardSpacing.x * (cols - 1)) / cols;
        float cellHeight = (availableHeight - cardSpacing.y * (rows - 1)) / rows;
        // Tall/portrait cards at a fixed aspect ratio (height = width * cardAspectRatio) rather
        // than square -- start from the column-driven width, then shrink to fit the available
        // row height if needed (keeping the same ratio, not just squashing height), and finally
        // clamp to the configured floor so cards never shrink below a usable size.
        float cardWidth = cellWidth;
        float cardHeight = cardWidth * cardAspectRatio;
        if (cardHeight > cellHeight)
        {
            cardHeight = cellHeight;
            cardWidth = cardHeight / cardAspectRatio;
        }
        cardWidth = Mathf.Max(cardWidth, minCardSize.x);
        cardHeight = Mathf.Max(cardHeight, minCardSize.y);
        currentCardSize = new Vector2(cardWidth, cardHeight);
        grid.cellSize = currentCardSize;

        foreach (PlantData data in validData)
        {
            cards.Add(CreateCard(data));
        }
    }

    CardUI CreateCard(PlantData data)
    {
        GameObject root = new GameObject("Card_" + data.seedType, typeof(RectTransform));
        root.transform.SetParent(cardContainer, false);

        Image background = root.AddComponent<Image>();

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(root.transform, false);
        RectTransform iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.12f, 0.42f);
        iconRect.anchorMax = new Vector2(0.88f, 0.92f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        Image icon = iconGO.AddComponent<Image>();
        if (data.icon != null)
        {
            icon.sprite = data.icon;
            icon.color = Color.white;
        }
        else
        {
            icon.color = GetTierColor(data.tier);
        }

        GameObject nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(root.transform, false);
        RectTransform nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.05f, 0.28f);
        nameRect.anchorMax = new Vector2(0.95f, 0.42f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = data.displayName;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = new Color(0.15f, 0.12f, 0.1f, 1f);
        nameText.fontStyle = FontStyles.Bold;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 10f;
        nameText.fontSizeMax = Mathf.Clamp(currentCardSize.x * 0.14f, 14f, 30f);

        GameObject priceGO = new GameObject("Price", typeof(RectTransform));
        priceGO.transform.SetParent(root.transform, false);
        RectTransform priceRect = priceGO.GetComponent<RectTransform>();
        priceRect.anchorMin = new Vector2(0.05f, 0.16f);
        priceRect.anchorMax = new Vector2(0.95f, 0.28f);
        priceRect.offsetMin = Vector2.zero;
        priceRect.offsetMax = Vector2.zero;
        TextMeshProUGUI priceText = priceGO.AddComponent<TextMeshProUGUI>();
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.color = new Color(0.55f, 0.38f, 0.05f, 1f);
        priceText.enableAutoSizing = true;
        priceText.fontSizeMin = 9f;
        priceText.fontSizeMax = Mathf.Clamp(currentCardSize.x * 0.12f, 12f, 24f);

        // Soft yellow "aura" glow that appears behind the Buy button while the player is
        // aiming the VR ray at it (or hovering it with a mouse) -- a clear selection cue right
        // before they press Trigger/UI Press to buy. Starts fully transparent; UIHoverGlow
        // fades it in/out based on pointer enter/exit and only lights up while the button is
        // actually interactable (i.e. the player can afford the item).
        GameObject glowGO = new GameObject("BuyGlow", typeof(RectTransform));
        glowGO.transform.SetParent(root.transform, false);
        RectTransform glowRect = glowGO.GetComponent<RectTransform>();
        glowRect.anchorMin = new Vector2(0.02f, -0.06f);
        glowRect.anchorMax = new Vector2(0.98f, 0.22f);
        glowRect.offsetMin = Vector2.zero;
        glowRect.offsetMax = Vector2.zero;
        Image glowImage = glowGO.AddComponent<Image>();
        glowImage.color = new Color(1f, 0.85f, 0.15f, 0f);
        glowImage.raycastTarget = false;

        GameObject buttonGO = new GameObject("BuyButton", typeof(RectTransform));
        buttonGO.transform.SetParent(root.transform, false);
        RectTransform buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.1f, 0.02f);
        buttonRect.anchorMax = new Vector2(0.9f, 0.15f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        Image buttonBg = buttonGO.AddComponent<Image>();
        buttonBg.color = buyButtonColor;
        Button buyButton = buttonGO.AddComponent<Button>();
        buyButton.targetGraphic = buttonBg;

        UIHoverGlow hoverGlow = buttonGO.AddComponent<UIHoverGlow>();
        hoverGlow.glowImage = glowImage;
        hoverGlow.button = buyButton;

        GameObject buyLabelGO = new GameObject("Label", typeof(RectTransform));
        buyLabelGO.transform.SetParent(buttonGO.transform, false);
        RectTransform buyLabelRect = buyLabelGO.GetComponent<RectTransform>();
        buyLabelRect.anchorMin = Vector2.zero;
        buyLabelRect.anchorMax = Vector2.one;
        buyLabelRect.offsetMin = Vector2.zero;
        buyLabelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI buyLabel = buyLabelGO.AddComponent<TextMeshProUGUI>();
        buyLabel.text = "BUY";
        buyLabel.alignment = TextAlignmentOptions.Center;
        buyLabel.color = Color.white;
        buyLabel.fontStyle = FontStyles.Bold;
        buyLabel.enableAutoSizing = true;
        buyLabel.fontSizeMin = 9f;
        buyLabel.fontSizeMax = 22f;

        SeedType capturedType = data.seedType;
        buyButton.onClick.AddListener(() => OnBuySeed(capturedType));

        return new CardUI
        {
            background = background,
            icon = icon,
            nameText = nameText,
            priceText = priceText,
            buyButton = buyButton,
            seedType = data.seedType
        };
    }

    Color GetTierColor(PlantTier tier)
    {
        switch (tier)
        {
            case PlantTier.Medium:
                return mediumTierColor;
            case PlantTier.Hard:
                return hardTierColor;
            default:
                return beginnerTierColor;
        }
    }

    // --- Refresh ---

    void RefreshUI()
    {
        if (CurrencyManager.Instance != null && coinsText != null)
        {
            coinsText.text = CurrencyManager.Instance.CurrentCoins + " coins";
        }

        if (sellAllButton != null)
        {
            sellAllButton.interactable = EquipmentManager.Instance != null && EquipmentManager.Instance.HasAnyCrops;
        }

        RefreshInventoryUnlockRow();
        RefreshCards();
    }

    void RefreshInventoryUnlockRow()
    {
        if (EquipmentManager.Instance == null)
        {
            return;
        }

        if (inventorySlotsText != null)
        {
            inventorySlotsText.text = "Inventory: " + EquipmentManager.Instance.UnlockedSlotCount + " / " + EquipmentManager.Instance.MaxCapacity + " slots";
        }

        if (unlockInventorySlotButton == null)
        {
            return;
        }

        if (EquipmentManager.Instance.IsMaxCapacity)
        {
            unlockInventorySlotButton.gameObject.SetActive(false);
            return;
        }

        unlockInventorySlotButton.gameObject.SetActive(true);
        int cost = EquipmentManager.Instance.NextUnlockCost;
        TextMeshProUGUI label = unlockInventorySlotButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = "Unlock Slot: " + cost + " coins";
        }
        unlockInventorySlotButton.interactable = CurrencyManager.Instance != null && CurrencyManager.Instance.CurrentCoins >= cost;
    }

    void RefreshCards()
    {
        if (PlantDatabase.Instance == null)
        {
            return;
        }

        foreach (CardUI card in cards)
        {
            PlantData data = PlantDatabase.Instance.GetPlantData(card.seedType);
            if (data == null)
            {
                continue;
            }

            bool canAfford = CurrencyManager.Instance != null && CurrencyManager.Instance.CurrentCoins >= data.seedBuyPrice;
            card.priceText.text = data.seedBuyPrice + " coins";
            card.background.color = canAfford ? cardAffordableColor : cardUnaffordableColor;
            card.buyButton.interactable = canAfford;
        }
    }

    void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
        }
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnEquipmentChanged -= HandleEquipmentChanged;
        }
    }
}
