using UnityEngine;

public enum PlotState
{
    Empty,
    Planted
}

public enum PlantHealthState
{
    Healthy,
    Thirsty,
    Critical,
    Dead
}

public class GroundPlot : MonoBehaviour
{
    [Header("References")]
    public GameObject seedVisual;
    public Renderer plotRenderer;
    public GameObject healthIconObject;

    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 1f, 0.4f, 1f);

    [Header("Health Colors")]
    public Color healthyColor = new Color(0.3f, 0.8f, 0.3f, 1f);
    public Color thirstyColor = new Color(0.85f, 0.8f, 0.3f, 1f);
    public Color criticalColor = new Color(0.7f, 0.45f, 0.15f, 1f);
    public Color deadColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    public PlotState State { get; private set; } = PlotState.Empty;
    public SeedType PlantedSeed { get; private set; } = SeedType.None;
    public PlantHealthState HealthState { get; private set; } = PlantHealthState.Healthy;
    public bool HadCriticalDamage { get; private set; }
    public bool IsReadyToHarvest { get; private set; }

    public bool IsEmpty => State == PlotState.Empty;
    public bool IsDead => HealthState == PlantHealthState.Dead;

    private Color originalColor;
    private bool isCurrentlyHighlighted;
    private PlantData plantData;

    private int daysSincePlanted;
    private bool wateredToday;
    private bool wateredMorningToday;
    private bool wateredEveningToday;
    private bool wateredInStrictWindowToday;
    private float overdueSinceRealTime = -1f;

    void Awake()
    {
        if (plotRenderer != null)
        {
            originalColor = MaterialColorHelper.GetColor(plotRenderer);
        }

        if (seedVisual != null)
        {
            seedVisual.SetActive(false);
        }

        if (healthIconObject != null)
        {
            healthIconObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnNewDay += HandleNewDay;
        }
    }

    void OnDisable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnNewDay -= HandleNewDay;
        }
    }

    void Update()
    {
        if (State != PlotState.Planted)
        {
            return;
        }

        if (HealthState == PlantHealthState.Healthy || HealthState == PlantHealthState.Dead)
        {
            return;
        }

        if (plantData == null || overdueSinceRealTime < 0f)
        {
            return;
        }

        float elapsedSeconds = Time.time - overdueSinceRealTime;
        float thirstyToCriticalSeconds = plantData.thirstyToCriticalHours * 3600f;
        float criticalToDeadSeconds = plantData.criticalToDeadHours * 3600f;

        if (HealthState == PlantHealthState.Thirsty && elapsedSeconds >= thirstyToCriticalSeconds)
        {
            HadCriticalDamage = true;
            SetHealthState(PlantHealthState.Critical);
        }
        else if (HealthState == PlantHealthState.Critical && elapsedSeconds >= thirstyToCriticalSeconds + criticalToDeadSeconds)
        {
            SetHealthState(PlantHealthState.Dead);
        }
    }

    public void Plant(SeedType seedType)
    {
        if (State != PlotState.Empty)
        {
            return;
        }

        State = PlotState.Planted;
        PlantedSeed = seedType;

        plantData = PlantDatabase.Instance != null ? PlantDatabase.Instance.GetPlantData(seedType) : null;
        if (plantData == null)
        {
            Debug.LogWarning("No PlantData found for seed type: " + seedType + ". Watering rules will not apply.");
        }

        daysSincePlanted = 0;
        HadCriticalDamage = false;
        IsReadyToHarvest = false;
        overdueSinceRealTime = -1f;
        ResetDailyWateringFlags();

        SetHealthState(PlantHealthState.Healthy);

        if (seedVisual != null)
        {
            seedVisual.SetActive(true);
        }

        Debug.Log("Planted " + seedType + " on plot: " + gameObject.name);
    }

    public void Water()
    {
        if (State != PlotState.Planted || HealthState == PlantHealthState.Dead)
        {
            return;
        }

        wateredToday = true;

        if (TimeManager.Instance != null)
        {
            if (TimeManager.Instance.CurrentTimeOfDay == TimeManager.TimeOfDay.Morning)
            {
                wateredMorningToday = true;
            }
            else if (TimeManager.Instance.CurrentTimeOfDay == TimeManager.TimeOfDay.Evening)
            {
                wateredEveningToday = true;
            }

            if (plantData != null && plantData.wateringRule == WateringRule.StrictTimeWindow &&
                TimeManager.Instance.IsWithinTimeWindow(plantData.strictWindowHour, plantData.strictWindowMinute, plantData.strictWindowToleranceMinutes))
            {
                wateredInStrictWindowToday = true;
            }
        }

        if (HealthState == PlantHealthState.Thirsty || HealthState == PlantHealthState.Critical)
        {
            SetHealthState(PlantHealthState.Healthy);
            overdueSinceRealTime = -1f;
        }

        Debug.Log("Watered plot: " + gameObject.name);
    }

    public void Uproot()
    {
        if (HealthState != PlantHealthState.Dead)
        {
            return;
        }

        State = PlotState.Empty;
        PlantedSeed = SeedType.None;
        plantData = null;
        daysSincePlanted = 0;
        HadCriticalDamage = false;
        IsReadyToHarvest = false;
        overdueSinceRealTime = -1f;
        ResetDailyWateringFlags();

        SetHealthState(PlantHealthState.Healthy);

        if (seedVisual != null)
        {
            seedVisual.SetActive(false);
        }

        Debug.Log("Uprooted dead plant on plot: " + gameObject.name);
    }

    // Harvests a ready plant into the player's backpack. Works even while the plant is
    // Critical (rescuing it before it dies) — the harvested item is flagged as reduced
    // quality if this plant ever took Critical damage. Returns false if not ready,
    // dead, or if the backpack is full.
    public bool Harvest()
    {
        if (State != PlotState.Planted || !IsReadyToHarvest || HealthState == PlantHealthState.Dead)
        {
            return false;
        }

        if (BackpackInventory.Instance == null)
        {
            Debug.LogWarning("Ground_Plot: No BackpackInventory found in scene.");
            return false;
        }

        bool added = BackpackInventory.Instance.TryAddItem(PlantedSeed, HadCriticalDamage);
        if (!added)
        {
            Debug.Log("Backpack is full. Cannot harvest right now.");
            return false;
        }

        Debug.Log("Harvested " + PlantedSeed + " from plot: " + gameObject.name);

        State = PlotState.Empty;
        PlantedSeed = SeedType.None;
        plantData = null;
        daysSincePlanted = 0;
        HadCriticalDamage = false;
        IsReadyToHarvest = false;
        overdueSinceRealTime = -1f;
        ResetDailyWateringFlags();

        SetHealthState(PlantHealthState.Healthy);

        if (seedVisual != null)
        {
            seedVisual.SetActive(false);
        }

        return true;
    }

    public void SetHighlight(bool isHighlighted)
    {
        isCurrentlyHighlighted = isHighlighted;
        ApplyRendererColor();
    }

    private void HandleNewDay(int newDay)
    {
        if (State != PlotState.Planted || HealthState == PlantHealthState.Dead)
        {
            return;
        }

        bool requirementMet = CheckWateringRequirementMet();

        if (requirementMet)
        {
            if (HealthState != PlantHealthState.Healthy)
            {
                SetHealthState(PlantHealthState.Healthy);
            }
            overdueSinceRealTime = -1f;
        }
        else if (HealthState == PlantHealthState.Healthy)
        {
            overdueSinceRealTime = Time.time;
            SetHealthState(PlantHealthState.Thirsty);
        }

        if (HealthState == PlantHealthState.Healthy || HealthState == PlantHealthState.Thirsty)
        {
            daysSincePlanted++;
            CheckHarvestReady();
        }

        ResetDailyWateringFlags();
    }

    private bool CheckWateringRequirementMet()
    {
        if (plantData == null)
        {
            return true;
        }

        switch (plantData.wateringRule)
        {
            case WateringRule.OncePerDay:
                return wateredToday;
            case WateringRule.MorningAndEvening:
                return wateredMorningToday && wateredEveningToday;
            case WateringRule.StrictTimeWindow:
                return wateredInStrictWindowToday;
            default:
                return true;
        }
    }

    private void ResetDailyWateringFlags()
    {
        wateredToday = false;
        wateredMorningToday = false;
        wateredEveningToday = false;
        wateredInStrictWindowToday = false;
    }

    private void CheckHarvestReady()
    {
        if (plantData == null)
        {
            return;
        }

        IsReadyToHarvest = daysSincePlanted >= plantData.minHarvestDays;
    }

    private void SetHealthState(PlantHealthState newState)
    {
        HealthState = newState;
        ApplyRendererColor();

        if (healthIconObject != null)
        {
            bool showIcon = newState == PlantHealthState.Thirsty || newState == PlantHealthState.Critical;
            healthIconObject.SetActive(showIcon);
        }
    }

    private void ApplyRendererColor()
    {
        if (plotRenderer == null)
        {
            return;
        }

        Color baseColor = GetCurrentBaseColor();
        MaterialColorHelper.SetColor(plotRenderer, isCurrentlyHighlighted ? highlightColor : baseColor);
    }

    private Color GetCurrentBaseColor()
    {
        if (State == PlotState.Empty)
        {
            return originalColor;
        }

        switch (HealthState)
        {
            case PlantHealthState.Healthy:
                return healthyColor;
            case PlantHealthState.Thirsty:
                return thirstyColor;
            case PlantHealthState.Critical:
                return criticalColor;
            case PlantHealthState.Dead:
                return deadColor;
            default:
                return originalColor;
        }
    }
}