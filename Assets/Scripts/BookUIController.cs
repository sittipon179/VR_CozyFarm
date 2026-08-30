using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class PlantInfo
{
    public string plantName;
    public string difficultyLevel;
    public string wateringSchedule;
    public string harvestTime;
    public string notes;
}

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

    [Header("Plant Data")]
    public PlantInfo[] plants;

    private int currentPage = 0;

    void Awake()
    {
        if (plants == null || plants.Length == 0)
        {
            plants = new PlantInfo[]
            {
                new PlantInfo
                {
                    plantName = "Tomato",
                    difficultyLevel = "Beginner",
                    wateringSchedule = "Water 1 time per day",
                    harvestTime = "Harvest in 3-5 in-game days",
                    notes = "Tolerates missed watering well. Suitable for beginners."
                },
                new PlantInfo
                {
                    plantName = "Cabbage",
                    difficultyLevel = "Beginner",
                    wateringSchedule = "Water 1 time per day",
                    harvestTime = "Harvest in 3-5 in-game days",
                    notes = "Tolerates missed watering well. Suitable for beginners."
                },
                new PlantInfo
                {
                    plantName = "Carrot",
                    difficultyLevel = "Beginner",
                    wateringSchedule = "Water 1 time per day",
                    harvestTime = "Harvest in 3-5 in-game days",
                    notes = "Tolerates missed watering well. Suitable for beginners."
                }
            };
        }

        nextButton.onClick.AddListener(NextPage);
        prevButton.onClick.AddListener(PrevPage);
        closeButton.onClick.AddListener(CloseBook);

        bookPanel.SetActive(false);
    }

    public void OpenBook()
    {
        currentPage = 0;
        bookPanel.SetActive(true);
        UpdatePageDisplay();

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.RegisterUIOpened();
        }
    }

    public void CloseBook()
    {
        bookPanel.SetActive(false);

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.RegisterUIClosed();
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
        PlantInfo info = plants[currentPage];
        plantNameText.text = info.plantName;
        difficultyText.text = "Difficulty: " + info.difficultyLevel;
        wateringText.text = "Watering: " + info.wateringSchedule;
        harvestText.text = "Harvest Time: " + info.harvestTime;
        notesText.text = info.notes;
        pageIndicatorText.text = (currentPage + 1) + " / " + plants.Length;

        prevButton.interactable = currentPage > 0;
        nextButton.interactable = currentPage < plants.Length - 1;
    }

    void Update()
    {
        if (bookPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseBook();
        }
    }
}