using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// The book's page list is pulled live from PlantDatabase.Instance.allPlantData every time
// the book is opened. There is no hardcoded plant list here anymore -- add or remove a
// PlantData asset in PlantDatabase and the book's page count updates automatically.
public class BookUIController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject bookPanel;
    public TextMeshProUGUI plantNameText;
    public TextMeshProUGUI difficultyText;
    public TextMeshProUGUI wateringText;
    public TextMeshProUGUI harvestText;
    public TextMeshProUGUI notesText;
    public TextMeshProUGUI pageIndicatorText;
    public Button nextButton;
    public Button prevButton;
    public Button closeButton;

    [Header("Full Immersion Lock (Book only)")]
    [Tooltip("Rig turn providers (SnapTurnProvider / ContinuousTurnProvider) to disable while the book is open, so the player can't spin the rig around away from the fullscreen page -- unlike Shop/Inventory, the book is meant to fully hold the player's attention. Movement is already locked via UIStateManager.RegisterUIOpened(); this just also locks turning, which UIStateManager deliberately leaves alone for the other panels.")]
    public Behaviour[] turnProvidersToLock;

    [Header("VR Input")]
    [Tooltip("Optional. Bind to XRI Default Input Actions' Left/Right Interaction -> Select (the same Grip/'G' button used to open the door and pick up the book) so pressing it while the book is open closes it too, without needing to aim precisely at the small Close button -- movement/turning are locked while reading anyway, so aiming a ray is awkward. The Escape key below still works as a harmless desktop-testing fallback either way.")]
    public InputActionReference closeBookActionLeft;
    public InputActionReference closeBookActionRight;

    private PlantData[] plants = new PlantData[0];
    private int currentPage = 0;

    void Awake()
    {
        nextButton.onClick.AddListener(NextPage);
        prevButton.onClick.AddListener(PrevPage);
        closeButton.onClick.AddListener(CloseBook);

        bookPanel.SetActive(false);
    }

    public void OpenBook()
    {
        RefreshPlantList();
        currentPage = 0;
        bookPanel.SetActive(true);
        UpdatePageDisplay();

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.RegisterUIOpened();
        }

        SetTurnLocked(true);
    }

    public void CloseBook()
    {
        bookPanel.SetActive(false);

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.RegisterUIClosed();
        }

        SetTurnLocked(false);
    }

    void SetTurnLocked(bool locked)
    {
        if (turnProvidersToLock == null)
        {
            return;
        }

        foreach (Behaviour provider in turnProvidersToLock)
        {
            if (provider != null)
            {
                provider.enabled = !locked;
            }
        }
    }

    void RefreshPlantList()
    {
        if (PlantDatabase.Instance != null && PlantDatabase.Instance.allPlantData != null)
        {
            plants = PlantDatabase.Instance.allPlantData;
        }
        else
        {
            plants = new PlantData[0];
        }

        if (currentPage >= plants.Length)
        {
            currentPage = Mathf.Max(0, plants.Length - 1);
        }
    }

    void NextPage()
    {
        if (currentPage < plants.Length - 1)
        {
            currentPage++;
            UpdatePageDisplay();
        }
    }

    void PrevPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdatePageDisplay();
        }
    }

    void UpdatePageDisplay()
    {
        if (plants == null || plants.Length == 0)
        {
            plantNameText.text = "No plants recorded yet";
            difficultyText.text = "";
            wateringText.text = "";
            harvestText.text = "";
            notesText.text = "";
            pageIndicatorText.text = "0 / 0";
            prevButton.interactable = false;
            nextButton.interactable = false;
            return;
        }

        PlantData data = plants[currentPage];

        if (data == null)
        {
            return;
        }

        plantNameText.text = string.IsNullOrEmpty(data.displayName) ? data.seedType.ToString() : data.displayName;
        difficultyText.text = "Difficulty: " + TierToDisplayText(data.tier);
        wateringText.text = "Watering: " + WateringRuleToDisplayText(data);
        harvestText.text = "Harvest Time: " + HarvestRangeToDisplayText(data);
        notesText.text = string.IsNullOrEmpty(data.notes) ? DefaultNotesForTier(data.tier) : data.notes;
        pageIndicatorText.text = (currentPage + 1) + " / " + plants.Length;

        prevButton.interactable = currentPage > 0;
        nextButton.interactable = currentPage < plants.Length - 1;
    }

    static string TierToDisplayText(PlantTier tier)
    {
        switch (tier)
        {
            case PlantTier.Beginner:
                return "Beginner";
            case PlantTier.Medium:
                return "Medium";
            case PlantTier.Hard:
                return "Hard";
            default:
                return tier.ToString();
        }
    }

    static string WateringRuleToDisplayText(PlantData data)
    {
        switch (data.wateringRule)
        {
            case WateringRule.OncePerDay:
                return "Water 1 time per day";
            case WateringRule.MorningAndEvening:
                return "Water in the morning and evening";
            case WateringRule.StrictTimeWindow:
                return "Water at exactly " + data.strictWindowHour.ToString("00") + ":" + data.strictWindowMinute.ToString("00")
                    + " (+/- " + data.strictWindowToleranceMinutes + " min)";
            default:
                return data.wateringRule.ToString();
        }
    }

    static string HarvestRangeToDisplayText(PlantData data)
    {
        if (data.minHarvestDays == data.maxHarvestDays)
        {
            return "Harvest in " + data.minHarvestDays + " in-game days";
        }
        return "Harvest in " + data.minHarvestDays + "-" + data.maxHarvestDays + " in-game days";
    }

    static string DefaultNotesForTier(PlantTier tier)
    {
        switch (tier)
        {
            case PlantTier.Beginner:
                return "Tolerates missed watering well. Suitable for beginners.";
            case PlantTier.Medium:
                return "Needs a consistent watering schedule. Starting to turn a real profit.";
            case PlantTier.Hard:
                return "Requires precise timing to water. High risk, high reward.";
            default:
                return "";
        }
    }

    void Update()
    {
        if (!bookPanel.activeSelf)
        {
            return;
        }

        bool closeKeyPressed = Input.GetKeyDown(KeyCode.Escape);
        bool closeButtonPressed = (closeBookActionLeft != null && closeBookActionLeft.action != null && closeBookActionLeft.action.WasPerformedThisFrame())
            || (closeBookActionRight != null && closeBookActionRight.action != null && closeBookActionRight.action.WasPerformedThisFrame());

        if (closeKeyPressed || closeButtonPressed)
        {
            CloseBook();
        }
    }
}
