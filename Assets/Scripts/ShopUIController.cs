using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopUIController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject shopPanel;
    public TextMeshProUGUI coinsText;
    public TextMeshProUGUI backpackFillText;
    public Button closeButton;

    [Header("Sell")]
    public Button sellAllButton;
    public TextMeshProUGUI sellResultText;

    [Header("Buy Seeds")]
    public Button buyTomatoButton;
    public Button buyCabbageButton;
    public Button buyCarrotButton;
    public Button buyStrawberryButton;
    public Button buyOrchidButton;
    public TextMeshProUGUI tomatoPriceText;
    public TextMeshProUGUI cabbagePriceText;
    public TextMeshProUGUI carrotPriceText;
    public TextMeshProUGUI strawberryPriceText;
    public TextMeshProUGUI orchidPriceText;

    [Header("Backpack Upgrade")]
    public Button upgradeBackpackButton;
    public TextMeshProUGUI upgradeBackpackText;

    void Start()
    {
        shopPanel.SetActive(false);

        closeButton.onClick.AddListener(CloseShop);
        sellAllButton.onClick.AddListener(OnSellAll);

        buyTomatoButton.onClick.AddListener(() => OnBuySeed(SeedType.Tomato));
        buyCabbageButton.onClick.AddListener(() => OnBuySeed(SeedType.Cabbage));
        buyCarrotButton.onClick.AddListener(() => OnBuySeed(SeedType.Carrot));
        buyStrawberryButton.onClick.AddListener(() => OnBuySeed(SeedType.Strawberry));
        buyOrchidButton.onClick.AddListener(() => OnBuySeed(SeedType.Orchid));

        upgradeBackpackButton.onClick.AddListener(OnUpgradeBackpack);

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += HandleCoinsChanged;
        }
        if (BackpackInventory.Instance != null)
        {
            BackpackInventory.Instance.OnBackpackChanged += HandleBackpackChanged;
        }
    }

    public void OpenShop()
    {
        shopPanel.SetActive(true);
        if (sellResultText != null)
        {
            sellResultText.text = "";
        }
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
        if (BackpackInventory.Instance == null)
        {
            return;
        }

        int earned = BackpackInventory.Instance.SellAll();
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

    void OnUpgradeBackpack()
    {
        if (BackpackInventory.Instance == null)
        {
            return;
        }

        BackpackInventory.Instance.TryUpgrade();
    }

    void HandleCoinsChanged(int newAmount)
    {
        RefreshUI();
    }

    void HandleBackpackChanged()
    {
        RefreshUI();
    }

    void RefreshUI()
    {
        if (CurrencyManager.Instance != null && coinsText != null)
        {
            coinsText.text = CurrencyManager.Instance.CurrentCoins + " coins";
        }

        if (BackpackInventory.Instance != null)
        {
            if (backpackFillText != null)
            {
                backpackFillText.text = "Backpack: " + BackpackInventory.Instance.Count + " / " + BackpackInventory.Instance.Capacity;
            }

            if (BackpackInventory.Instance.IsMaxTier)
            {
                upgradeBackpackText.text = "Backpack is at max capacity";
                upgradeBackpackButton.interactable = false;
            }
            else
            {
                int cost = BackpackInventory.Instance.NextUpgradeCost;
                int nextCapacity = BackpackInventory.Instance.NextTierCapacity;
                upgradeBackpackText.text = "Upgrade to " + nextCapacity + " slots: " + cost + " coins";
                upgradeBackpackButton.interactable = CurrencyManager.Instance != null && CurrencyManager.Instance.CurrentCoins >= cost;
            }
        }

        RefreshSeedPrices();
    }

    void RefreshSeedPrices()
    {
        if (PlantDatabase.Instance == null)
        {
            return;
        }

        SetPriceLabel(tomatoPriceText, buyTomatoButton, SeedType.Tomato);
        SetPriceLabel(cabbagePriceText, buyCabbageButton, SeedType.Cabbage);
        SetPriceLabel(carrotPriceText, buyCarrotButton, SeedType.Carrot);
        SetPriceLabel(strawberryPriceText, buyStrawberryButton, SeedType.Strawberry);
        SetPriceLabel(orchidPriceText, buyOrchidButton, SeedType.Orchid);
    }

    void SetPriceLabel(TextMeshProUGUI label, Button button, SeedType type)
    {
        PlantData data = PlantDatabase.Instance.GetPlantData(type);
        if (data == null || label == null)
        {
            return;
        }

        label.text = data.displayName + ": " + data.seedBuyPrice + " coins";

        if (button != null && CurrencyManager.Instance != null)
        {
            button.interactable = CurrencyManager.Instance.CurrentCoins >= data.seedBuyPrice;
        }
    }

    void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
        }
        if (BackpackInventory.Instance != null)
        {
            BackpackInventory.Instance.OnBackpackChanged -= HandleBackpackChanged;
        }
    }
}