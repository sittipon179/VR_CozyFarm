using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class InventoryUIController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject inventoryPanel;

    [Header("Tool Buttons")]
    public Button hoeButton;
    public Button wateringCanButton;

    [Header("Seed Buttons")]
    public Button tomatoSeedButton;
    public Button cabbageSeedButton;
    public Button carrotSeedButton;
    public Button strawberrySeedButton;
    public Button orchidSeedButton;
    public TextMeshProUGUI tomatoCountText;
    public TextMeshProUGUI cabbageCountText;
    public TextMeshProUGUI carrotCountText;
    public TextMeshProUGUI strawberryCountText;
    public TextMeshProUGUI orchidCountText;

    [Header("Status Display")]
    public TextMeshProUGUI equippedText;

    [Header("Backpack & Currency")]
    public TextMeshProUGUI coinsText;
    public TextMeshProUGUI backpackFillText;

    private bool isOpen = false;

    void Start()
    {
        inventoryPanel.SetActive(false);

        hoeButton.onClick.AddListener(() => EquipmentManager.Instance.EquipHoe());
        wateringCanButton.onClick.AddListener(() => EquipmentManager.Instance.EquipWateringCan());
        tomatoSeedButton.onClick.AddListener(() => EquipmentManager.Instance.EquipSeed(SeedType.Tomato));
        cabbageSeedButton.onClick.AddListener(() => EquipmentManager.Instance.EquipSeed(SeedType.Cabbage));
        carrotSeedButton.onClick.AddListener(() => EquipmentManager.Instance.EquipSeed(SeedType.Carrot));
        strawberrySeedButton.onClick.AddListener(() => EquipmentManager.Instance.EquipSeed(SeedType.Strawberry));
        orchidSeedButton.onClick.AddListener(() => EquipmentManager.Instance.EquipSeed(SeedType.Orchid));

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnEquipmentChanged += RefreshUI;
        }
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += HandleCoinsChanged;
        }
        if (BackpackInventory.Instance != null)
        {
            BackpackInventory.Instance.OnBackpackChanged += RefreshUI;
        }

        RefreshUI();
    }

    void Update()
    {
        bool toggleKeyPressed = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I);
        if (!toggleKeyPressed)
        {
            return;
        }

        if (isOpen)
        {
            CloseInventory();
        }
        else if (UIStateManager.Instance != null && !UIStateManager.Instance.IsAnyUIOpen)
        {
            OpenInventory();
        }
    }

    void OpenInventory()
    {
        isOpen = true;
        inventoryPanel.SetActive(true);
        UIStateManager.Instance.RegisterUIOpened();
        RefreshUI();
    }

    void CloseInventory()
    {
        isOpen = false;
        inventoryPanel.SetActive(false);
        UIStateManager.Instance.RegisterUIClosed();
    }

    void HandleCoinsChanged(int newAmount)
    {
        RefreshUI();
    }

    void RefreshUI()
    {
        if (EquipmentManager.Instance == null)
        {
            return;
        }

        tomatoCountText.text = EquipmentManager.Instance.SeedCounts[SeedType.Tomato].ToString();
        cabbageCountText.text = EquipmentManager.Instance.SeedCounts[SeedType.Cabbage].ToString();
        carrotCountText.text = EquipmentManager.Instance.SeedCounts[SeedType.Carrot].ToString();
        strawberryCountText.text = EquipmentManager.Instance.SeedCounts[SeedType.Strawberry].ToString();
        orchidCountText.text = EquipmentManager.Instance.SeedCounts[SeedType.Orchid].ToString();

        string equippedLabel = "None";
        switch (EquipmentManager.Instance.CurrentEquipped)
        {
            case EquippedType.Hoe:
                equippedLabel = "Hoe";
                break;
            case EquippedType.WateringCan:
                equippedLabel = "Watering Can";
                break;
            case EquippedType.Seed:
                string seedName = EquipmentManager.Instance.CurrentSeed switch
                {
                    SeedType.Tomato => "Tomato",
                    SeedType.Cabbage => "Cabbage",
                    SeedType.Carrot => "Carrot",
                    SeedType.Strawberry => "Strawberry",
                    SeedType.Orchid => "Orchid",
                    _ => "Unknown"
                };
                equippedLabel = "Seed: " + seedName;
                break;
        }
        equippedText.text = "Equipped: " + equippedLabel;

        if (CurrencyManager.Instance != null && coinsText != null)
        {
            coinsText.text = CurrencyManager.Instance.CurrentCoins + " coins";
        }

        if (BackpackInventory.Instance != null && backpackFillText != null)
        {
            backpackFillText.text = "Backpack: " + BackpackInventory.Instance.Count + " / " + BackpackInventory.Instance.Capacity;
        }
    }

    void OnDestroy()
    {
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnEquipmentChanged -= RefreshUI;
        }
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
        }
        if (BackpackInventory.Instance != null)
        {
            BackpackInventory.Instance.OnBackpackChanged -= RefreshUI;
        }
    }
}